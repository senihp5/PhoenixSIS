using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Phoenix.Core.Auditing;
using Phoenix.Core.Features;
using Phoenix.Core.Modules.EdFi;
using Phoenix.Core.Students;
using Phoenix.Infrastructure.Data;
using Phoenix.Infrastructure.EdFi;
using Phoenix.Infrastructure.Students;

namespace Phoenix.Infrastructure.Tests;

public class StudentSyncServiceTests
{
    private static readonly EdFiSyncTarget Target =
        new(220822, 220822001, 2027, "220822001-2027");

    private static readonly DateOnly EntryDate = new(2026, 8, 17);

    [Fact]
    public async Task DryRun_classifies_and_previews_ids_without_writing()
    {
        await using var db = NewDb();
        await SeedAsync(db, ("Maria", "Hernandez"), ("Jamal", "Washington"));
        var client = new FakeEdFiClient();
        var svc = NewService(db, client);

        var result = await svc.SyncAllAsync(Target, apply: false);

        Assert.False(result.Applied);
        Assert.Equal(2, result.ToCreate);
        Assert.All(result.Rows, r => Assert.Equal(StudentSyncOutcome.WouldCreate, r.Outcome));
        Assert.Equal(new[] { "PHX-000001", "PHX-000002" }, result.Rows.Select(r => r.StudentUniqueId));
        Assert.Empty(client.Calls);
        Assert.Equal(2, await db.StudentIdentifiers.CountAsync()); // only the seed identifiers
    }

    [Fact]
    public async Task Apply_writes_four_resources_in_order_and_records_the_identifier()
    {
        await using var db = NewDb();
        var studentId = (await SeedAsync(db, ("Maria", "Hernandez")))[0];
        var client = new FakeEdFiClient();
        var svc = NewService(db, client);

        var result = await svc.SyncAllAsync(Target, apply: true);

        var row = Assert.Single(result.Rows);
        Assert.Equal(StudentSyncOutcome.Created, row.Outcome);
        Assert.Equal("PHX-000001", row.StudentUniqueId);

        Assert.Equal(
            new[] { "students", "studentSchoolAssociations", "studentEducationOrganizationAssociations", "studentIdentificationCodes" },
            client.Calls.Select(c => c.Resource));

        var student = Assert.IsType<EdFiStudentWrite>(client.Calls[0].Payload);
        Assert.Equal("PHX-000001", student.StudentUniqueId);
        Assert.Equal("Maria", student.FirstName);
        Assert.Equal("Hernandez", student.LastSurname);

        var edOrg = Assert.IsType<EdFiStudentEdOrgWrite>(client.Calls[2].Payload);
        Assert.Equal(220822, edOrg.EducationOrganizationId);

        var enrollment = Assert.IsType<EdFiEnrollmentWrite>(client.Calls[1].Payload);
        Assert.Equal(220822001, enrollment.SchoolId);
        Assert.Equal(new DateOnly(2026, 8, 17), enrollment.EntryDate);
        Assert.Equal(2027, enrollment.SchoolYear);
        Assert.Equal("220822001-2027", enrollment.CalendarCode);
        Assert.True(enrollment.PrimarySchool);
        Assert.Equal("uri://ed-fi.org/GradeLevelDescriptor#Adult Education", enrollment.EntryGradeLevelDescriptor);
        Assert.Equal("uri://ed-fi.org/EnrollmentTypeDescriptor#Current", enrollment.EnrollmentTypeDescriptor);
        Assert.Equal(1.0, enrollment.FullTimeEquivalency);

        var code = Assert.IsType<EdFiStudentIdentificationCodeWrite>(client.Calls[3].Payload);
        Assert.Equal("uri://ed-fi.org/StudentIdentificationSystemDescriptor#Local", code.StudentIdentificationSystemDescriptor);
        Assert.Equal("PHX-000001", code.IdentificationCode);
        Assert.Equal(220822, code.EducationOrganizationId);

        var mapped = await TestSupport.Identity(db).GetByIdentifierAsync("Ed-Fi", "PHX-000001");
        Assert.Equal(studentId, mapped!.Id);
    }

    [Fact]
    public async Task Rerun_reuses_the_mapped_id_and_reports_updated()
    {
        await using var db = NewDb();
        await SeedAsync(db, ("Maria", "Hernandez"), ("Jamal", "Washington"));
        var client = new FakeEdFiClient();
        var svc = NewService(db, client);

        await svc.SyncAllAsync(Target, apply: true);
        var identifiersAfterFirst = await db.StudentIdentifiers.CountAsync();

        var dryRun = await svc.SyncAllAsync(Target, apply: false);
        Assert.All(dryRun.Rows, r => Assert.Equal(StudentSyncOutcome.WouldUpdate, r.Outcome));

        var second = await svc.SyncAllAsync(Target, apply: true);

        Assert.Equal(2, second.ToUpdate);
        Assert.All(second.Rows, r => Assert.Equal(StudentSyncOutcome.Updated, r.Outcome));
        Assert.Equal(new[] { "PHX-000001", "PHX-000002" }, second.Rows.Select(r => r.StudentUniqueId));
        Assert.Equal(identifiersAfterFirst, await db.StudentIdentifiers.CountAsync());
    }

    [Fact]
    public async Task One_failure_keeps_its_problem_and_does_not_stop_the_batch()
    {
        await using var db = NewDb();
        await SeedAsync(db, ("Ana", "Alvarez"), ("Ben", "Brooks"), ("Cara", "Cruz"));
        var problem = new EdFiProblem(
            "urn:ed-fi:api:data-conflict:unresolved-reference",
            "Unresolved Reference",
            409,
            "The referenced 'Calendar' item does not exist.",
            "00000000-0000-0000-0000-000000000409",
            [],
            new Dictionary<string, IReadOnlyList<string>>());
        var client = new FakeEdFiClient
        {
            FailWhen = (resource, uniqueId) =>
                resource == "studentSchoolAssociations" && uniqueId == "PHX-000002" ? problem : null
        };
        var svc = NewService(db, client);

        var result = await svc.SyncAllAsync(Target, apply: true);

        Assert.Equal(2, result.ToCreate);
        Assert.Equal(1, result.Failed);

        var failed = result.Rows.Single(r => r.Outcome == StudentSyncOutcome.Failed);
        Assert.Equal("Ben Brooks", failed.DisplayName);
        Assert.Equal("PHX-000002", failed.StudentUniqueId);
        Assert.Equal("studentSchoolAssociations", failed.FailedResource);
        Assert.Same(problem, failed.Problem);

        Assert.DoesNotContain(client.Calls, c => c.UniqueId == "PHX-000002" && c.Resource == "studentEducationOrganizationAssociations");
        Assert.DoesNotContain(client.Calls, c => c.UniqueId == "PHX-000002" && c.Resource == "studentIdentificationCodes");
        Assert.Contains(client.Calls, c => c.UniqueId == "PHX-000003" && c.Resource == "studentIdentificationCodes");

        client.FailWhen = null;
        var retry = await svc.SyncAllAsync(Target, apply: true);
        var healed = retry.Rows.Single(r => r.DisplayName == "Ben Brooks");
        Assert.Equal("PHX-000002", healed.StudentUniqueId); // same number on the retry
        Assert.Equal(StudentSyncOutcome.Updated, healed.Outcome);
    }

    [Fact]
    public async Task Existing_unenrolled_student_is_enrolled_first_then_updated()
    {
        await using var db = NewDb();
        var ids = await SeedAsync(db, ("Fatima", "Al-Rashid"));
        await TestSupport.Identity(db).AddIdentifierAsync(ids[0], "Ed-Fi", "PHX-000001");
        var client = new FakeEdFiClient();
        client.AddExistingUnenrolledStudent("PHX-000001"); // left behind by an earlier failed run
        var svc = NewService(db, client);

        var result = await svc.SyncAllAsync(Target, apply: true);

        var row = Assert.Single(result.Rows);
        Assert.Equal(StudentSyncOutcome.Updated, row.Outcome);
        Assert.Equal(
            new[] { "students", "studentSchoolAssociations", "students", "studentEducationOrganizationAssociations", "studentIdentificationCodes" },
            client.Calls.Select(c => c.Resource));
    }

    [Fact]
    public async Task Client_exception_is_captured_per_student()
    {
        await using var db = NewDb();
        await SeedAsync(db, ("Ana", "Alvarez"), ("Ben", "Brooks"));
        var client = new FakeEdFiClient
        {
            ThrowWhen = uniqueId => uniqueId == "PHX-000001"
                ? new HttpRequestException("Connection refused")
                : null
        };
        var svc = NewService(db, client);

        var result = await svc.SyncAllAsync(Target, apply: true);

        var failed = result.Rows.Single(r => r.StudentUniqueId == "PHX-000001");
        Assert.Equal(StudentSyncOutcome.Failed, failed.Outcome);
        Assert.Equal("Connection refused", failed.Reason);
        Assert.Equal(StudentSyncOutcome.Created, result.Rows.Single(r => r.StudentUniqueId == "PHX-000002").Outcome);
    }

    [Fact]
    public async Task Student_without_birth_date_is_skipped_and_not_sent()
    {
        await using var db = NewDb();
        await SeedAsync(db, ("Ana", "Alvarez"));
        db.Students.Single().BirthDate = null;
        await db.SaveChangesAsync();
        var client = new FakeEdFiClient();
        var svc = NewService(db, client);

        var result = await svc.SyncAllAsync(Target, apply: true);

        var row = Assert.Single(result.Rows);
        Assert.Equal(StudentSyncOutcome.Skipped, row.Outcome);
        Assert.Empty(client.Calls);
        Assert.False(await db.StudentIdentifiers.AnyAsync(i => i.SourceSystem == "Ed-Fi"));
    }

    [Fact]
    public async Task Student_without_a_matching_enrollment_is_skipped_and_gets_no_number()
    {
        await using var db = NewDb();
        var ids = await SeedAsync(db, false, ("Ana", "Alvarez"), ("Ben", "Brooks"), ("Cara", "Cruz"));
        await EnrollAsync(db, ids[0]);
        await EnrollAsync(db, ids[1], campus: TestSupport.SecondCampus);
        await EnrollAsync(db, ids[2]);
        var client = new FakeEdFiClient();
        var svc = NewService(db, client);

        var result = await svc.SyncAllAsync(Target, apply: true);

        var skipped = result.Rows.Single(r => r.Outcome == StudentSyncOutcome.Skipped);
        Assert.Equal("Ben Brooks", skipped.DisplayName);
        Assert.Null(skipped.StudentUniqueId);
        Assert.Equal("No 2026-27 enrollment at campus 220822001.", skipped.Reason);
        Assert.DoesNotContain(client.Calls, c => c.Payload is EdFiStudentWrite { FirstName: "Ben" });
        Assert.Equal("PHX-000001", result.Rows.Single(r => r.DisplayName == "Ana Alvarez").StudentUniqueId);
        Assert.Equal("PHX-000002", result.Rows.Single(r => r.DisplayName == "Cara Cruz").StudentUniqueId);
        Assert.Equal(2, await db.StudentIdentifiers.CountAsync(i => i.SourceSystem == "Ed-Fi"));
    }

    [Fact]
    public async Task Enrollment_fields_come_from_the_Phoenix_enrollment()
    {
        await using var db = NewDb();
        var ids = await SeedAsync(db, false, ("Ana", "Alvarez"));
        await TestSupport.Enrollments(db).EnrollAsync(new NewEnrollment(
            ids[0], TestSupport.Campus, 2027, new DateOnly(2026, 10, 5),
            GradeLevel.Grade11, EnrollmentKind.Preregistered, 0.5m, IsPrimary: false));
        var client = new FakeEdFiClient();
        var svc = NewService(db, client);

        await svc.SyncAllAsync(Target, apply: true);

        var enrollment = Assert.IsType<EdFiEnrollmentWrite>(
            client.Calls.Single(c => c.Resource == "studentSchoolAssociations").Payload);
        Assert.Equal(new DateOnly(2026, 10, 5), enrollment.EntryDate);
        Assert.Equal("uri://ed-fi.org/GradeLevelDescriptor#Eleventh grade", enrollment.EntryGradeLevelDescriptor);
        Assert.Equal("uri://ed-fi.org/EnrollmentTypeDescriptor#Preregistered", enrollment.EnrollmentTypeDescriptor);
        Assert.Equal(0.5, enrollment.FullTimeEquivalency);
        Assert.False(enrollment.PrimarySchool);
        Assert.Null(enrollment.ExitWithdrawDate);
        Assert.Null(enrollment.ExitWithdrawTypeDescriptor);
    }

    [Fact]
    public async Task Exit_is_sent_with_its_date_and_descriptor()
    {
        await using var db = NewDb();
        var ids = await SeedAsync(db, false, ("Ana", "Alvarez"));
        var enrolled = await EnrollAsync(db, ids[0]);
        await TestSupport.Enrollments(db).ExitAsync(enrolled.Id, new DateOnly(2026, 11, 20), ExitReason.TransferredOut);
        var client = new FakeEdFiClient();
        var svc = NewService(db, client);

        var result = await svc.SyncAllAsync(Target, apply: true);

        Assert.Equal(StudentSyncOutcome.Created, Assert.Single(result.Rows).Outcome);
        var enrollment = Assert.IsType<EdFiEnrollmentWrite>(
            client.Calls.Single(c => c.Resource == "studentSchoolAssociations").Payload);
        Assert.Equal(EntryDate, enrollment.EntryDate);
        Assert.Equal(new DateOnly(2026, 11, 20), enrollment.ExitWithdrawDate);
        Assert.Equal("uri://ed-fi.org/ExitWithdrawTypeDescriptor#Transferred", enrollment.ExitWithdrawTypeDescriptor);
    }

    [Fact]
    public async Task Reentry_sends_one_association_per_enrollment_in_entry_order()
    {
        await using var db = NewDb();
        var ids = await SeedAsync(db, false, ("Ana", "Alvarez"));
        var first = await EnrollAsync(db, ids[0]);
        await TestSupport.Enrollments(db).ExitAsync(first.Id, new DateOnly(2026, 10, 1), ExitReason.Withdrew);
        await EnrollAsync(db, ids[0], entryDate: new DateOnly(2026, 11, 2));
        var client = new FakeEdFiClient();
        var svc = NewService(db, client);

        await svc.SyncAllAsync(Target, apply: true);

        Assert.Equal(
            new[] { "students", "studentSchoolAssociations", "studentSchoolAssociations", "studentEducationOrganizationAssociations", "studentIdentificationCodes" },
            client.Calls.Select(c => c.Resource));
        var sent = client.Calls
            .Where(c => c.Resource == "studentSchoolAssociations")
            .Select(c => (EdFiEnrollmentWrite)c.Payload)
            .ToList();
        Assert.Equal(EntryDate, sent[0].EntryDate);
        Assert.Equal(new DateOnly(2026, 10, 1), sent[0].ExitWithdrawDate);
        Assert.Equal(new DateOnly(2026, 11, 2), sent[1].EntryDate);
        Assert.Null(sent[1].ExitWithdrawDate);
    }

    [Fact]
    public async Task Unmapped_grade_level_is_skipped_with_the_config_key()
    {
        await using var db = NewDb();
        await SeedAsync(db, ("Ana", "Alvarez"));
        var options = new EdFiApiOptions();
        options.GradeLevelDescriptors.Remove("AdultEducation");
        var client = new FakeEdFiClient();
        var svc = NewService(db, client, options: options);

        var result = await svc.SyncAllAsync(Target, apply: true);

        var row = Assert.Single(result.Rows);
        Assert.Equal(StudentSyncOutcome.Skipped, row.Outcome);
        Assert.Contains("EdFiApi:GradeLevelDescriptors", row.Reason);
        Assert.Empty(client.Calls);
    }

    [Fact]
    public async Task Preview_and_apply_are_audited_per_student_listed_without_names()
    {
        await using var db = NewDb();
        var ids = await SeedAsync(db, false, ("Ana", "Alvarez"), ("Ben", "Brooks"));
        await EnrollAsync(db, ids[0]);
        var svc = NewService(db, new FakeEdFiClient());

        await svc.SyncAllAsync(Target, apply: false);

        var previews = await db.AuditEvents.Where(e => e.Action == AuditAction.SyncPreview).ToListAsync();
        Assert.Equal(ids.OrderBy(i => i), previews.Select(e => e.StudentId!.Value).OrderBy(i => i));
        Assert.False(await db.AuditEvents.AnyAsync(e => e.Action == AuditAction.SyncApply));

        await svc.SyncAllAsync(Target, apply: true);

        var applies = await db.AuditEvents.Where(e => e.Action == AuditAction.SyncApply).ToListAsync();
        Assert.Equal(2, applies.Count);
        Assert.Contains("sent to Ed-Fi", applies.Single(e => e.StudentId == ids[0]).Summary);
        Assert.Contains("not sent", applies.Single(e => e.StudentId == ids[1]).Summary);

        Assert.All(previews.Concat(applies), e =>
        {
            Assert.Equal(TestSupport.TestActor.Id, e.ActorId);
            Assert.Equal("Student", e.EntityType);
            Assert.DoesNotContain("Alvarez", e.Summary);
            Assert.DoesNotContain("Brooks", e.Summary);
            Assert.DoesNotContain("Ana", e.Summary);
            Assert.DoesNotContain("Ben", e.Summary);
        });
    }

    [Fact]
    public async Task Audit_failure_stops_the_sync_before_anything_is_sent()
    {
        await using var db = NewDb();
        await SeedAsync(db, ("Ana", "Alvarez"));
        var client = new FakeEdFiClient();
        var svc = NewService(db, client, user: new FailingCurrentUser());

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SyncAllAsync(Target, apply: true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SyncAllAsync(Target, apply: false));

        Assert.Empty(client.Calls);
        Assert.False(await db.StudentIdentifiers.AnyAsync(i => i.SourceSystem == "Ed-Fi"));
        Assert.False(await db.AuditEvents.AnyAsync(e =>
            e.Action == AuditAction.SyncApply || e.Action == AuditAction.SyncPreview));
    }

    [Fact]
    public async Task New_ids_continue_after_the_highest_mapped_number()
    {
        await using var db = NewDb();
        var ids = await SeedAsync(db, ("Ana", "Alvarez"), ("Ben", "Brooks"));
        await TestSupport.Identity(db).AddIdentifierAsync(ids[0], "Ed-Fi", "PHX-000007");
        var svc = NewService(db, new FakeEdFiClient());

        var result = await svc.SyncAllAsync(Target, apply: false);

        Assert.Equal("PHX-000007", result.Rows.Single(r => r.DisplayName == "Ana Alvarez").StudentUniqueId);
        Assert.Equal("PHX-000008", result.Rows.Single(r => r.DisplayName == "Ben Brooks").StudentUniqueId);
    }

    [Fact]
    public async Task GetDefaultTarget_reads_configuration()
    {
        await using var db = NewDb();
        var svc = NewService(db, new FakeEdFiClient());

        var target = svc.GetDefaultTarget();

        Assert.Equal(Target, target);
    }

    [Fact]
    public async Task Sync_throws_when_flag_disabled()
    {
        await using var db = NewDb();
        var svc = NewService(db, new FakeEdFiClient(), enabled: false);

        await Assert.ThrowsAsync<FeatureNotEnabledException>(() => svc.SyncAllAsync(Target, apply: false));
    }

    private static PhoenixDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PhoenixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static StudentSyncService NewService(
        PhoenixDbContext db,
        FakeEdFiClient client,
        bool enabled = true,
        EdFiApiOptions? options = null,
        ICurrentUser? user = null) =>
        new(
            db,
            TestSupport.Identity(db),
            client,
            Options.Create(options ?? new EdFiApiOptions()),
            new StubFeatureFlags(enabled),
            TestSupport.Audit(db, user),
            NullLogger<StudentSyncService>.Instance);

    /// <summary>Seeds students, each enrolled at the target campus for SY 2027 (entry 2026-08-17).</summary>
    private static Task<List<Guid>> SeedAsync(PhoenixDbContext db, params (string First, string Last)[] names) =>
        SeedAsync(db, true, names);

    private static Task<EnrollmentRecord> EnrollAsync(
        PhoenixDbContext db,
        Guid studentId,
        string campus = TestSupport.Campus,
        DateOnly? entryDate = null) =>
        TestSupport.Enrollments(db).EnrollAsync(new NewEnrollment(
            studentId, campus, 2027, entryDate ?? EntryDate, GradeLevel.AdultEducation));

    private static async Task<List<Guid>> SeedAsync(PhoenixDbContext db, bool enroll, params (string First, string Last)[] names)
    {
        var identity = TestSupport.Identity(db);
        var ids = new List<Guid>();
        var n = 0;
        foreach (var (first, last) in names)
        {
            n++;
            var record = await identity.ResolveOrCreateAsync(
                "Sample",
                $"S-{n}",
                new StudentDetails(first, null, last, new DateOnly(1995, 1, n)));
            ids.Add(record.Id);

            if (enroll)
                await EnrollAsync(db, record.Id);
        }

        return ids;
    }

    private sealed class StubFeatureFlags(bool enabled) : IFeatureFlagService
    {
        public bool IsEnabled(string featureName) => enabled;
        public IReadOnlyDictionary<string, FeatureFlagState> GetAll() =>
            new Dictionary<string, FeatureFlagState>();
        public void RequireEnabled(string featureName)
        {
            if (!enabled) throw new FeatureNotEnabledException(featureName);
        }
    }

    private sealed record Call(string Resource, string UniqueId, object Payload);

    /// <summary>
    /// Records every write and mimics the Ed-Fi Sandbox claim set seen live:
    /// a student POST returns 201 for a new studentUniqueId, 200 for an
    /// existing student who is enrolled, and 403 for an existing student who
    /// is not enrolled in one of the client's schools. A successful
    /// enrollment POST enrolls the student.
    /// </summary>
    private sealed class FakeEdFiClient : IEdFiApiClient
    {
        private static readonly EdFiProblem NotEnrolled = new(
            "urn:ed-fi:api:security:authorization",
            "Authorization Denied",
            403,
            "Access to the requested data could not be authorized. Hint: You may need to create a corresponding 'StudentSchoolAssociation' item.",
            null,
            [],
            new Dictionary<string, IReadOnlyList<string>>());

        private readonly HashSet<string> _knownStudents = [];
        private readonly HashSet<string> _enrolledStudents = [];

        public void AddExistingUnenrolledStudent(string uniqueId) => _knownStudents.Add(uniqueId);

        public List<Call> Calls { get; } = [];
        public Func<string, string, EdFiProblem?>? FailWhen { get; set; }
        public Func<string, Exception?>? ThrowWhen { get; set; }

        public Task<EdFiApiInfo> GetApiInfoAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<EdFiStudentSummary>> GetStudentsAsync(int limit, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EdFiWriteResult> UpsertStudentAsync(EdFiStudentWrite student, CancellationToken cancellationToken = default)
        {
            var id = student.StudentUniqueId;
            if (_knownStudents.Contains(id) && !_enrolledStudents.Contains(id))
                return Record("students", id, student, 200, NotEnrolled);

            var created = _knownStudents.Add(id);
            return Record("students", id, student, created ? 201 : 200);
        }

        public async Task<EdFiWriteResult> UpsertStudentSchoolAssociationAsync(EdFiEnrollmentWrite enrollment, CancellationToken cancellationToken = default)
        {
            var result = await Record("studentSchoolAssociations", enrollment.StudentUniqueId, enrollment, 201);
            if (result.Success)
                _enrolledStudents.Add(enrollment.StudentUniqueId);
            return result;
        }

        public Task<EdFiWriteResult> UpsertStudentEducationOrganizationAssociationAsync(EdFiStudentEdOrgWrite association, CancellationToken cancellationToken = default) =>
            Record("studentEducationOrganizationAssociations", association.StudentUniqueId, association, 201);

        public Task<EdFiWriteResult> UpsertStudentIdentificationCodeAsync(EdFiStudentIdentificationCodeWrite code, CancellationToken cancellationToken = default) =>
            Record("studentIdentificationCodes", code.StudentUniqueId, code, 201);

        private Task<EdFiWriteResult> Record(
            string resource, string uniqueId, object payload, int successStatus, EdFiProblem? forced = null)
        {
            if (ThrowWhen?.Invoke(uniqueId) is { } ex)
                throw ex;

            Calls.Add(new Call(resource, uniqueId, payload));

            if ((forced ?? FailWhen?.Invoke(resource, uniqueId)) is { } problem)
                return Task.FromResult(new EdFiWriteResult(resource, false, problem.Status ?? 409, 1, problem));

            return Task.FromResult(new EdFiWriteResult(resource, true, successStatus, 1, null));
        }
    }
}
