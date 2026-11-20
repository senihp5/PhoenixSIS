using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Phoenix.Core.Features;
using Phoenix.Core.Modules.EdFi;
using Phoenix.Core.Students;
using Phoenix.Infrastructure.Data;

namespace Phoenix.Infrastructure.EdFi;

/// <summary>
/// PHX-7.2 Phoenix -> Ed-Fi student sync. See <see cref="IStudentSyncService"/>.
///
/// Identity: the Ed-Fi studentUniqueId is a mapped identifier (ADR-002),
/// source system "Ed-Fi", value PHX-NNNNNN (next number after the highest
/// already mapped). For a new student the identifier is recorded BEFORE the
/// writes, so a student whose writes fail keeps the same number on the next
/// run and Ed-Fi never collects orphan records under abandoned numbers.
/// Every run re-upserts mapped students (Ed-Fi POST is upsert by natural
/// key), so a failed or changed student heals on the next apply.
/// </summary>
public sealed class StudentSyncService : IStudentSyncService
{
    public const string EdFiSourceSystem = "Ed-Fi";
    private const string FeatureFlag = "EdFi";
    private const string UniqueIdPrefix = "PHX-";

    private readonly PhoenixDbContext _db;
    private readonly IStudentIdentityService _identity;
    private readonly IEdFiApiClient _client;
    private readonly EdFiApiOptions _options;
    private readonly IFeatureFlagService _features;
    private readonly ILogger<StudentSyncService> _logger;

    public StudentSyncService(
        PhoenixDbContext db,
        IStudentIdentityService identity,
        IEdFiApiClient client,
        IOptions<EdFiApiOptions> options,
        IFeatureFlagService features,
        ILogger<StudentSyncService> logger)
    {
        _db = db;
        _identity = identity;
        _client = client;
        _options = options.Value;
        _features = features;
        _logger = logger;
    }

    public EdFiSyncTarget GetDefaultTarget()
    {
        if (!DateOnly.TryParseExact(
                _options.DefaultEntryDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var entryDate))
        {
            throw new InvalidOperationException(
                $"EdFiApi:DefaultEntryDate '{_options.DefaultEntryDate}' is not a yyyy-MM-dd date.");
        }

        return new EdFiSyncTarget(
            _options.DefaultLeaId,
            _options.DefaultSchoolId,
            _options.SchoolYear,
            string.IsNullOrWhiteSpace(_options.CalendarCode) ? null : _options.CalendarCode,
            entryDate);
    }

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

        var nextNumber = await GetNextUniqueNumberAsync(cancellationToken);
        var rows = new List<StudentSyncRow>(students.Count);

        foreach (var student in students)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = $"{student.FirstName} {student.LastName}";
            var mappedId = student.Identifiers
                .FirstOrDefault(i => i.SourceSystem == EdFiSourceSystem)?.Value;

            if (student.BirthDate is null)
            {
                rows.Add(new StudentSyncRow(
                    student.Id, name, mappedId, StudentSyncOutcome.Skipped,
                    Reason: "No birth date in Phoenix; Ed-Fi requires one."));
                continue;
            }

            var uniqueId = mappedId ?? FormatUniqueId(nextNumber++);

            if (!apply)
            {
                rows.Add(new StudentSyncRow(
                    student.Id, name, uniqueId,
                    mappedId is null ? StudentSyncOutcome.WouldCreate : StudentSyncOutcome.WouldUpdate));
                continue;
            }

            rows.Add(await SyncOneAsync(student, name, uniqueId, mappedId is null, target, cancellationToken));
        }

        var result = new StudentSyncResult(target, apply, rows);
        _logger.LogInformation(
            "Ed-Fi student sync ({Mode}) for school {SchoolId}, school year {SchoolYear}: {Total} students, {Create} create, {Update} update, {Skipped} skipped, {Failed} failed.",
            apply ? "apply" : "dry run", target.SchoolId, target.SchoolYear,
            result.Total, result.ToCreate, result.ToUpdate, result.Skipped, result.Failed);

        return result;
    }

    private async Task<StudentSyncRow> SyncOneAsync(
        Student student,
        string name,
        string uniqueId,
        bool reserveIdentifier,
        EdFiSyncTarget target,
        CancellationToken cancellationToken)
    {
        try
        {
            if (reserveIdentifier)
            {
                await _identity.AddIdentifierAsync(
                    student.Id, EdFiSourceSystem, uniqueId, cancellationToken);
            }

            var studentWrite = await _client.UpsertStudentAsync(
                new EdFiStudentWrite(
                    uniqueId,
                    student.FirstName,
                    student.LastName,
                    student.BirthDate!.Value,
                    student.MiddleName),
                cancellationToken);
            if (!studentWrite.Success)
                return Failed(student.Id, name, uniqueId, studentWrite);

            var edOrgWrite = await _client.UpsertStudentEducationOrganizationAssociationAsync(
                new EdFiStudentEdOrgWrite(uniqueId, target.LeaId),
                cancellationToken);
            if (!edOrgWrite.Success)
                return Failed(student.Id, name, uniqueId, edOrgWrite);

            var enrollmentWrite = await _client.UpsertStudentSchoolAssociationAsync(
                new EdFiEnrollmentWrite(
                    uniqueId,
                    target.SchoolId,
                    target.EntryDate,
                    _options.DefaultEntryGradeLevelDescriptor,
                    target.SchoolYear,
                    target.CalendarCode,
                    PrimarySchool: true,
                    EnrollmentTypeDescriptor: _options.DefaultEnrollmentTypeDescriptor,
                    FullTimeEquivalency: _options.DefaultFullTimeEquivalency),
                cancellationToken);
            if (!enrollmentWrite.Success)
                return Failed(student.Id, name, uniqueId, enrollmentWrite);

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
}
