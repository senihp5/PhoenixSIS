using Microsoft.EntityFrameworkCore;
using Phoenix.Core.Auditing;
using Phoenix.Core.Students;
using Phoenix.Infrastructure.Data;

namespace Phoenix.Infrastructure.Tests;

/// <summary>PHX-8.2 enrollment model rules and auditing.</summary>
public class EnrollmentServiceTests
{
    private static readonly DateOnly Aug17 = new(2026, 8, 17);

    private static PhoenixDbContext NewDb(string? name = null) =>
        new(new DbContextOptionsBuilder<PhoenixDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options);

    private static async Task<Guid> NewStudentAsync(PhoenixDbContext db, string last = "Hernandez", string external = "NH-1")
    {
        var s = await TestSupport.Identity(db).ResolveOrCreateAsync(
            "TexasSample", external, new StudentDetails("Maria", null, last, new DateOnly(1999, 4, 12)));
        return s.Id;
    }

    private static NewEnrollment Enroll(Guid studentId, DateOnly? entry = null, string campus = TestSupport.Campus, bool primary = true) =>
        new(studentId, campus, 2027, entry ?? Aug17, GradeLevel.AdultEducation, IsPrimary: primary);

    [Fact]
    public async Task Enroll_persists_and_is_audited_without_record_content()
    {
        await using var db = NewDb();
        var studentId = await NewStudentAsync(db);
        var svc = TestSupport.Enrollments(db);

        var record = await svc.EnrollAsync(Enroll(studentId));

        Assert.True(record.IsOpen);
        Assert.Equal(TestSupport.Campus, record.CampusNumber);
        Assert.Equal(1.0m, record.FullTimeEquivalency);

        var evt = await db.AuditEvents.SingleAsync(e => e.Action == AuditAction.Enroll);
        Assert.Equal("Enrollment", evt.EntityType);
        Assert.Equal(record.Id, evt.EntityId);
        Assert.Equal(studentId, evt.StudentId);
        Assert.Equal("Enrolled at campus 220822001, school year 2026-27.", evt.Summary);
    }

    [Fact]
    public async Task Unknown_campus_is_rejected()
    {
        await using var db = NewDb();
        var studentId = await NewStudentAsync(db);
        var svc = TestSupport.Enrollments(db);

        var ex = await Assert.ThrowsAsync<EnrollmentRuleException>(() => svc.EnrollAsync(Enroll(studentId, campus: "999999999")));
        Assert.Contains("not a configured", ex.Message);
        await Assert.ThrowsAsync<EnrollmentRuleException>(() => svc.EnrollAsync(Enroll(studentId, campus: "BAD")));
    }

    [Theory]
    [InlineData(2026, 6, 30)]
    [InlineData(2027, 7, 1)]
    public async Task Entry_date_outside_the_school_year_is_rejected(int y, int m, int d)
    {
        await using var db = NewDb();
        var studentId = await NewStudentAsync(db);
        var svc = TestSupport.Enrollments(db);

        var ex = await Assert.ThrowsAsync<EnrollmentRuleException>(() => svc.EnrollAsync(Enroll(studentId, new DateOnly(y, m, d))));
        Assert.Contains("outside school year 2026-27", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.5)]
    [InlineData(-0.5)]
    public async Task Fte_out_of_range_is_rejected(double fte)
    {
        await using var db = NewDb();
        var studentId = await NewStudentAsync(db);
        var svc = TestSupport.Enrollments(db);

        await Assert.ThrowsAsync<EnrollmentRuleException>(() => svc.EnrollAsync(
            Enroll(studentId) with { FullTimeEquivalency = (decimal)fte }));
    }

    [Fact]
    public async Task Unknown_student_is_rejected()
    {
        await using var db = NewDb();
        var svc = TestSupport.Enrollments(db);

        await Assert.ThrowsAsync<EnrollmentRuleException>(() => svc.EnrollAsync(Enroll(Guid.NewGuid())));
    }

    [Fact]
    public async Task Second_open_enrollment_at_the_same_campus_is_rejected()
    {
        await using var db = NewDb();
        var studentId = await NewStudentAsync(db);
        var svc = TestSupport.Enrollments(db);
        await svc.EnrollAsync(Enroll(studentId));

        var ex = await Assert.ThrowsAsync<EnrollmentRuleException>(() =>
            svc.EnrollAsync(Enroll(studentId, new DateOnly(2026, 10, 1), primary: false)));
        Assert.Contains("open enrollment", ex.Message);
    }

    [Fact]
    public async Task Re_entry_after_an_exit_is_allowed_but_not_before_it()
    {
        await using var db = NewDb();
        var studentId = await NewStudentAsync(db);
        var svc = TestSupport.Enrollments(db);
        var first = await svc.EnrollAsync(Enroll(studentId));
        await svc.ExitAsync(first.Id, new DateOnly(2026, 10, 15), ExitReason.Withdrew);

        await Assert.ThrowsAsync<EnrollmentRuleException>(() => svc.EnrollAsync(Enroll(studentId, new DateOnly(2026, 10, 15))));

        var second = await svc.EnrollAsync(Enroll(studentId, new DateOnly(2027, 1, 5)));
        Assert.True(second.IsOpen);
        Assert.Equal(2, (await svc.GetForStudentAsync(studentId)).Count);
    }

    [Fact]
    public async Task Second_open_primary_in_the_same_year_is_rejected_but_secondary_is_allowed()
    {
        await using var db = NewDb();
        var studentId = await NewStudentAsync(db);
        var svc = TestSupport.Enrollments(db);
        await svc.EnrollAsync(Enroll(studentId));

        var ex = await Assert.ThrowsAsync<EnrollmentRuleException>(() =>
            svc.EnrollAsync(Enroll(studentId, campus: TestSupport.SecondCampus)));
        Assert.Contains("primary", ex.Message);

        var secondary = await svc.EnrollAsync(Enroll(studentId, campus: TestSupport.SecondCampus, primary: false));
        Assert.False(secondary.IsPrimary);
    }

    [Fact]
    public async Task Exit_is_recorded_and_audited_once()
    {
        await using var db = NewDb();
        var studentId = await NewStudentAsync(db);
        var svc = TestSupport.Enrollments(db);
        var record = await svc.EnrollAsync(Enroll(studentId));

        var exited = await svc.ExitAsync(record.Id, new DateOnly(2027, 5, 28), ExitReason.Graduated);

        Assert.False(exited.IsOpen);
        Assert.Equal(new DateOnly(2027, 5, 28), exited.ExitDate);
        Assert.Equal(ExitReason.Graduated, exited.ExitReason);

        var evt = await db.AuditEvents.SingleAsync(e => e.Action == AuditAction.Exit);
        Assert.Equal(record.Id, evt.EntityId);
        Assert.DoesNotContain("Graduated", evt.Summary);

        await Assert.ThrowsAsync<EnrollmentRuleException>(() =>
            svc.ExitAsync(record.Id, new DateOnly(2027, 5, 29), ExitReason.Other));
    }

    [Theory]
    [InlineData(2026, 8, 16)]
    [InlineData(2027, 7, 1)]
    public async Task Exit_before_entry_or_after_the_year_is_rejected(int y, int m, int d)
    {
        await using var db = NewDb();
        var studentId = await NewStudentAsync(db);
        var svc = TestSupport.Enrollments(db);
        var record = await svc.EnrollAsync(Enroll(studentId));

        await Assert.ThrowsAsync<EnrollmentRuleException>(() =>
            svc.ExitAsync(record.Id, new DateOnly(y, m, d), ExitReason.Withdrew));
    }

    [Fact]
    public async Task Bulk_preview_writes_nothing_and_apply_enrolls_only_the_unenrolled()
    {
        await using var db = NewDb();
        var a = await NewStudentAsync(db, "Alvarez", "NH-1");
        var b = await NewStudentAsync(db, "Brooks", "NH-2");
        var c = await NewStudentAsync(db, "Cruz", "NH-3");
        var svc = TestSupport.Enrollments(db);
        await svc.EnrollAsync(Enroll(b));
        var auditsBefore = await db.AuditEvents.CountAsync();
        var request = new BulkEnrollRequest(TestSupport.Campus, 2027, Aug17, GradeLevel.AdultEducation);

        var preview = await svc.EnrollManyAsync(request, apply: false);

        Assert.False(preview.Applied);
        Assert.Equal(new[] { a, c }, preview.StudentIds);
        Assert.Equal(1, preview.AlreadyEnrolled);
        Assert.Equal(1, await db.Enrollments.CountAsync());
        Assert.Equal(auditsBefore, await db.AuditEvents.CountAsync());

        var applied = await svc.EnrollManyAsync(request, apply: true);

        Assert.True(applied.Applied);
        Assert.Equal(3, await db.Enrollments.CountAsync());
        Assert.Equal(2, await db.AuditEvents.CountAsync(e => e.Action == AuditAction.Enroll && (e.StudentId == a || e.StudentId == c)));

        var rerun = await svc.EnrollManyAsync(request, apply: true);
        Assert.Empty(rerun.StudentIds);
        Assert.Equal(3, rerun.AlreadyEnrolled);
        Assert.Equal(3, await db.Enrollments.CountAsync());
    }

    [Fact]
    public async Task Bulk_skips_a_student_with_an_open_primary_elsewhere()
    {
        await using var db = NewDb();
        var a = await NewStudentAsync(db, "Alvarez", "NH-1");
        var svc = TestSupport.Enrollments(db);
        await svc.EnrollAsync(Enroll(a, campus: TestSupport.SecondCampus));

        var result = await svc.EnrollManyAsync(
            new BulkEnrollRequest(TestSupport.Campus, 2027, Aug17, GradeLevel.AdultEducation), apply: true);

        Assert.Empty(result.StudentIds);
        Assert.Equal(1, result.AlreadyEnrolled);
    }

    [Fact]
    public async Task Enroll_fails_and_saves_nothing_when_the_audit_cannot_be_written()
    {
        var name = Guid.NewGuid().ToString();
        Guid studentId;
        await using (var db = NewDb(name))
        {
            studentId = await NewStudentAsync(db);
        }

        await using (var db = NewDb(name))
        {
            var svc = TestSupport.Enrollments(db, new FailingCurrentUser());
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.EnrollAsync(Enroll(studentId)));
        }

        await using var fresh = NewDb(name);
        Assert.Equal(0, await fresh.Enrollments.CountAsync());
    }
}
