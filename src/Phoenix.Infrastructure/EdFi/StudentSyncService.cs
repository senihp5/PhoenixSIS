using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Phoenix.Core.Auditing;
using Phoenix.Core.Features;
using Phoenix.Core.Modules.EdFi;
using Phoenix.Core.Students;
using Phoenix.Infrastructure.Data;

namespace Phoenix.Infrastructure.EdFi;

/// <summary>
/// Phoenix -> Ed-Fi student sync (PHX-7.2, enrollments from Phoenix since
/// PHX-8.4). See <see cref="IStudentSyncService"/>.
///
/// Identity: the Ed-Fi studentUniqueId is a mapped identifier (ADR-002),
/// source system "Ed-Fi", value PHX-NNNNNN (next number after the highest
/// already mapped). For a new student the identifier is recorded BEFORE the
/// writes, so a student whose writes fail keeps the same number on the next
/// run and Ed-Fi never collects orphan records under abandoned numbers.
/// Every run re-upserts mapped students (Ed-Fi POST is upsert by natural
/// key), so a failed or changed student heals on the next apply.
///
/// Enrollment: one StudentSchoolAssociation per Phoenix enrollment at the
/// target campus for the target school year. Phoenix codes are mapped to
/// Ed-Fi descriptors here, at the boundary (EdFiApiOptions). A student with
/// no matching enrollment is skipped and gets no Ed-Fi number.
///
/// Audit (ADR-004): one SyncPreview or SyncApply event per student listed,
/// saved BEFORE anything is shown or sent. If the audit write fails, the
/// sync throws and nothing reaches Ed-Fi (fail closed). The pre-send apply
/// event says only that the send started; after the writes a second
/// SyncApply event per sent student records what happened (created,
/// updated, or the resource it failed at). If that second write fails the
/// data has already gone, so the result is still returned, with a warning.
/// </summary>
public sealed class StudentSyncService : IStudentSyncService
{
    public const string EdFiSourceSystem = "Ed-Fi";
    private const string FeatureFlag = "EdFi";
    private const string UniqueIdPrefix = "PHX-";
    private const string AuditEntityType = "Student";

    private readonly PhoenixDbContext _db;
    private readonly IStudentIdentityService _identity;
    private readonly IEdFiApiClient _client;
    private readonly EdFiApiOptions _options;
    private readonly IFeatureFlagService _features;
    private readonly IAuditLog _audit;
    private readonly ILogger<StudentSyncService> _logger;

    public StudentSyncService(
        PhoenixDbContext db,
        IStudentIdentityService identity,
        IEdFiApiClient client,
        IOptions<EdFiApiOptions> options,
        IFeatureFlagService features,
        IAuditLog audit,
        ILogger<StudentSyncService> logger)
    {
        _db = db;
        _identity = identity;
        _client = client;
        _options = options.Value;
        _features = features;
        _audit = audit;
        _logger = logger;
    }

    public EdFiSyncTarget GetDefaultTarget() =>
        new(
            _options.DefaultLeaId,
            _options.DefaultSchoolId,
            _options.SchoolYear,
            string.IsNullOrWhiteSpace(_options.CalendarCode) ? null : _options.CalendarCode);

    public async Task<StudentSyncResult> SyncAllAsync(
        EdFiSyncTarget target,
        bool apply,
        CancellationToken cancellationToken = default)
    {
        _features.RequireEnabled(FeatureFlag);
        ArgumentNullException.ThrowIfNull(target);

        var students = await _db.Students
            .AsNoTracking()
            .Include(s => s.Identifiers)
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

        var campusNumber = target.SchoolId.ToString(CultureInfo.InvariantCulture);
        var yearLabel = FormatYear(target.SchoolYear);

        var enrollments = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.CampusNumber == campusNumber && e.SchoolYear == target.SchoolYear)
            .ToListAsync(cancellationToken);
        var enrollmentsByStudent = enrollments.ToLookup(e => e.StudentId);

        var nextNumber = await GetNextUniqueNumberAsync(cancellationToken);
        var plans = new List<SyncPlan>(students.Count);

        foreach (var student in students)
        {
            var name = $"{student.FirstName} {student.LastName}";
            var mappedId = student.Identifiers
                .FirstOrDefault(i => i.SourceSystem == EdFiSourceSystem)?.Value;

            if (student.BirthDate is null)
            {
                plans.Add(SyncPlan.Skip(student, name, mappedId,
                    "No birth date in Phoenix; Ed-Fi requires one."));
                continue;
            }

            var mine = enrollmentsByStudent[student.Id]
                .OrderBy(e => e.EntryDate)
                .ToList();

            if (mine.Count == 0)
            {
                plans.Add(SyncPlan.Skip(student, name, mappedId,
                    $"No {yearLabel} enrollment at campus {campusNumber}."));
                continue;
            }

            var uniqueId = mappedId ?? FormatUniqueId(nextNumber);
            var writes = new List<EdFiEnrollmentWrite>(mine.Count);
            string? mappingError = null;

            foreach (var enrollment in mine)
            {
                if (!TryMapEnrollment(enrollment, uniqueId, target, out var write, out mappingError))
                    break;

                writes.Add(write!);
            }

            if (mappingError is not null)
            {
                plans.Add(SyncPlan.Skip(student, name, mappedId, mappingError));
                continue;
            }

            if (mappedId is null)
                nextNumber++;

            plans.Add(new SyncPlan(student, name, uniqueId, mappedId is null, writes, null));
        }

        await RecordAuditAsync(plans, apply, campusNumber, yearLabel, cancellationToken);

        var rows = new List<StudentSyncRow>(plans.Count);

        foreach (var plan in plans)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (plan.SkipReason is not null)
            {
                rows.Add(new StudentSyncRow(
                    plan.Student.Id, plan.Name, plan.UniqueId, StudentSyncOutcome.Skipped,
                    Reason: plan.SkipReason));
                continue;
            }

            if (!apply)
            {
                rows.Add(new StudentSyncRow(
                    plan.Student.Id, plan.Name, plan.UniqueId,
                    plan.IsNew ? StudentSyncOutcome.WouldCreate : StudentSyncOutcome.WouldUpdate));
                continue;
            }

            rows.Add(await SyncOneAsync(plan, target, cancellationToken));
        }

        var warning = apply
            ? await RecordOutcomesAsync(rows, campusNumber, yearLabel, cancellationToken)
            : null;

        var result = new StudentSyncResult(target, apply, rows, warning);
        _logger.LogInformation(
            "Ed-Fi student sync ({Mode}) for school {SchoolId}, school year {SchoolYear}: {Total} students, {Create} create, {Update} update, {Skipped} skipped, {Failed} failed.",
            apply ? "apply" : "dry run", target.SchoolId, target.SchoolYear,
            result.Total, result.ToCreate, result.ToUpdate, result.Skipped, result.Failed);

        return result;
    }

    private async Task RecordAuditAsync(
        List<SyncPlan> plans,
        bool apply,
        string campusNumber,
        string yearLabel,
        CancellationToken cancellationToken)
    {
        if (plans.Count == 0)
            return;

        var action = apply ? AuditAction.SyncApply : AuditAction.SyncPreview;
        var scope = $"campus {campusNumber}, school year {yearLabel}";

        var entries = plans.Select(plan => new AuditEntry(
            action,
            AuditEntityType,
            plan.Student.Id,
            plan.Student.Id,
            plan.SkipReason is not null
                ? $"Ed-Fi sync {(apply ? "apply" : "preview")}: {scope}; not sent (skipped)."
                : apply
                    ? $"Ed-Fi sync apply: {scope}; send started."
                    : $"Ed-Fi sync preview: {scope}; listed."));

        try
        {
            await _audit.RecordAsync(entries, cancellationToken);
        }
        catch
        {
            // Leave no half-staged audit rows on the scoped context for a
            // later save to pick up; the caller sees the failure and nothing
            // is shown or sent (ADR-004 fail closed).
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>
    /// Records what the apply did for each student it tried to send. Summaries
    /// carry the outcome and, on failure, the Ed-Fi resource name only - never
    /// the problem text, which can quote record content. Returns a warning for
    /// the caller when the rows could not be saved.
    /// </summary>
    private async Task<string?> RecordOutcomesAsync(
        List<StudentSyncRow> rows,
        string campusNumber,
        string yearLabel,
        CancellationToken cancellationToken)
    {
        var scope = $"campus {campusNumber}, school year {yearLabel}";

        var entries = rows
            .Where(r => r.Outcome is StudentSyncOutcome.Created or StudentSyncOutcome.Updated or StudentSyncOutcome.Failed)
            .Select(r => new AuditEntry(
                AuditAction.SyncApply,
                AuditEntityType,
                r.StudentId,
                r.StudentId,
                r.Outcome switch
                {
                    StudentSyncOutcome.Created => $"Ed-Fi sync apply result: {scope}; created in Ed-Fi.",
                    StudentSyncOutcome.Updated => $"Ed-Fi sync apply result: {scope}; updated in Ed-Fi.",
                    _ => r.FailedResource is null
                        ? $"Ed-Fi sync apply result: {scope}; failed without an Ed-Fi response."
                        : $"Ed-Fi sync apply result: {scope}; failed at {r.FailedResource}."
                }))
            .ToList();

        if (entries.Count == 0)
            return null;

        try
        {
            await _audit.RecordAsync(entries, cancellationToken);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _db.ChangeTracker.Clear();
            _logger.LogError(
                ex,
                "Ed-Fi sync results for {Count} student(s) could not be written to the audit trail.",
                entries.Count);
            return "The sync ran, but its results could not be written to the audit trail. "
                + "The audit trail shows each send as started, with no result.";
        }
    }

    private bool TryMapEnrollment(
        Enrollment enrollment,
        string uniqueId,
        EdFiSyncTarget target,
        out EdFiEnrollmentWrite? write,
        out string? error)
    {
        write = null;

        var grade = EdFiApiOptions.FindDescriptor(_options.GradeLevelDescriptors, enrollment.EntryGradeLevel.ToString());
        if (grade is null)
        {
            error = $"No Ed-Fi descriptor is configured for grade level '{enrollment.EntryGradeLevel}' (EdFiApi:GradeLevelDescriptors).";
            return false;
        }

        var kind = EdFiApiOptions.FindDescriptor(_options.EnrollmentTypeDescriptors, enrollment.Kind.ToString());
        if (kind is null)
        {
            error = $"No Ed-Fi descriptor is configured for enrollment kind '{enrollment.Kind}' (EdFiApi:EnrollmentTypeDescriptors).";
            return false;
        }

        string? exitType = null;
        if (enrollment.ExitDate is not null)
        {
            if (enrollment.ExitReason is not { } reason)
            {
                error = "The enrollment has an exit date but no exit reason.";
                return false;
            }

            exitType = EdFiApiOptions.FindDescriptor(_options.ExitWithdrawTypeDescriptors, reason.ToString());
            if (exitType is null)
            {
                error = $"No Ed-Fi descriptor is configured for exit reason '{reason}' (EdFiApi:ExitWithdrawTypeDescriptors).";
                return false;
            }
        }

        write = new EdFiEnrollmentWrite(
            uniqueId,
            target.SchoolId,
            enrollment.EntryDate,
            grade,
            target.SchoolYear,
            target.CalendarCode,
            PrimarySchool: enrollment.IsPrimary,
            EnrollmentTypeDescriptor: kind,
            FullTimeEquivalency: (double)enrollment.FullTimeEquivalency,
            ExitWithdrawDate: enrollment.ExitDate,
            ExitWithdrawTypeDescriptor: exitType);
        error = null;
        return true;
    }

    private async Task<StudentSyncRow> SyncOneAsync(
        SyncPlan plan,
        EdFiSyncTarget target,
        CancellationToken cancellationToken)
    {
        var student = plan.Student;
        var name = plan.Name;
        var uniqueId = plan.UniqueId!;

        try
        {
            if (plan.IsNew)
            {
                await _identity.AddIdentifierAsync(
                    student.Id, EdFiSourceSystem, uniqueId, cancellationToken);
            }

            var studentRecord = new EdFiStudentWrite(
                uniqueId,
                student.FirstName,
                student.LastName,
                student.BirthDate!.Value,
                student.MiddleName);

            // Ed-Fi relationship-based authorization (both rules found in the
            // PHX-7.4 live runs, 2026-11-20):
            //  1. A NEW student can be created freely, but an EXISTING student
            //     can only be updated once enrolled in one of this client's
            //     schools - otherwise the student POST is 403. That happens
            //     when an earlier run created the student and then failed.
            //     Recovery: create the enrollment (allowed for an existing
            //     student), then write the student again.
            //  2. The LEA association and identification code are 403 until
            //     the enrollment exists, so they always come after it.
            var studentWrite = await _client.UpsertStudentAsync(studentRecord, cancellationToken);
            var enrollmentsSent = 0;

            if (studentWrite.StatusCode == 403)
            {
                var earlyEnrollment = await _client.UpsertStudentSchoolAssociationAsync(
                    plan.Enrollments[0], cancellationToken);
                if (!earlyEnrollment.Success)
                    return Failed(student.Id, name, uniqueId, earlyEnrollment);

                enrollmentsSent = 1;
                studentWrite = await _client.UpsertStudentAsync(studentRecord, cancellationToken);
            }

            if (!studentWrite.Success)
                return Failed(student.Id, name, uniqueId, studentWrite);

            for (var i = enrollmentsSent; i < plan.Enrollments.Count; i++)
            {
                var enrollmentWrite = await _client.UpsertStudentSchoolAssociationAsync(
                    plan.Enrollments[i], cancellationToken);
                if (!enrollmentWrite.Success)
                    return Failed(student.Id, name, uniqueId, enrollmentWrite);
            }

            var edOrgWrite = await _client.UpsertStudentEducationOrganizationAssociationAsync(
                new EdFiStudentEdOrgWrite(uniqueId, target.LeaId),
                cancellationToken);
            if (!edOrgWrite.Success)
                return Failed(student.Id, name, uniqueId, edOrgWrite);

            var codeWrite = await _client.UpsertStudentIdentificationCodeAsync(
                new EdFiStudentIdentificationCodeWrite(
                    uniqueId,
                    target.LeaId,
                    _options.LocalIdentificationSystemDescriptor,
                    uniqueId),
                cancellationToken);
            if (!codeWrite.Success)
                return Failed(student.Id, name, uniqueId, codeWrite);

            return new StudentSyncRow(
                student.Id, name, uniqueId,
                studentWrite.Created ? StudentSyncOutcome.Created : StudentSyncOutcome.Updated);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FeatureNotEnabledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A failed SaveChanges (e.g. an identifier collision from a
            // concurrent run) leaves the entity tracked; clear it so the next
            // student's save does not retry it.
            _db.ChangeTracker.Clear();
            _logger.LogWarning(ex, "Ed-Fi sync failed for Phoenix student {StudentId}.", student.Id);
            return new StudentSyncRow(
                student.Id, name, uniqueId, StudentSyncOutcome.Failed, Reason: ex.Message);
        }
    }

    private static StudentSyncRow Failed(Guid studentId, string name, string uniqueId, EdFiWriteResult write) =>
        new(
            studentId,
            name,
            uniqueId,
            StudentSyncOutcome.Failed,
            FailedResource: write.Resource,
            Problem: write.Problem,
            Reason: write.Problem is null ? $"HTTP {write.StatusCode}" : null);

    private async Task<int> GetNextUniqueNumberAsync(CancellationToken cancellationToken)
    {
        var values = await _db.StudentIdentifiers
            .AsNoTracking()
            .Where(i => i.SourceSystem == EdFiSourceSystem && i.Value.StartsWith(UniqueIdPrefix))
            .Select(i => i.Value)
            .ToListAsync(cancellationToken);

        var max = 0;
        foreach (var value in values)
        {
            if (int.TryParse(
                    value.AsSpan(UniqueIdPrefix.Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var number)
                && number > max)
            {
                max = number;
            }
        }

        return max + 1;
    }

    private static string FormatUniqueId(int number) =>
        UniqueIdPrefix + number.ToString("D6", CultureInfo.InvariantCulture);

    private static string FormatYear(int schoolYear) => $"{schoolYear - 1}-{schoolYear % 100:D2}";

    /// <summary>
    /// What the sync will do for one student. <see cref="UniqueId"/> is the
    /// mapped Ed-Fi id, or the number apply would assign; for a skipped
    /// student it is the mapped id if there is one, otherwise null.
    /// </summary>
    private sealed record SyncPlan(
        Student Student,
        string Name,
        string? UniqueId,
        bool IsNew,
        IReadOnlyList<EdFiEnrollmentWrite> Enrollments,
        string? SkipReason)
    {
        public static SyncPlan Skip(Student student, string name, string? mappedId, string reason) =>
            new(student, name, mappedId, false, [], reason);
    }
}
