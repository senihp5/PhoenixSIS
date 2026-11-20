using Microsoft.EntityFrameworkCore;
using Phoenix.Core.Auditing;
using Phoenix.Core.Students;
using Phoenix.Infrastructure.Auditing;
using Phoenix.Infrastructure.Data;

namespace Phoenix.Infrastructure.Tests;

/// <summary>PHX-8.1 / ADR-004 record-access audit trail.</summary>
public class AuditTrailTests
{
    private static readonly StudentDetails Maria = new("Maria", null, "Hernandez", new DateOnly(1999, 4, 12));

    private static PhoenixDbContext NewDb(string? name = null) =>
        new(new DbContextOptionsBuilder<PhoenixDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Creating_a_student_records_a_create_event_without_pii()
    {
        await using var db = NewDb();
        var identity = TestSupport.Identity(db);

        var student = await identity.ResolveOrCreateAsync("TexasSample", "NH-24001", Maria);

        var evt = Assert.Single(await db.AuditEvents.ToListAsync());
        Assert.Equal(AuditAction.Create, evt.Action);
        Assert.Equal("Student", evt.EntityType);
        Assert.Equal(student.Id, evt.StudentId);
        Assert.Equal(student.Id, evt.EntityId);
        Assert.Equal(TestSupport.TestActor.Id, evt.ActorId);
        Assert.Equal(TestSupport.TestActor.Name, evt.ActorName);
        Assert.False(string.IsNullOrEmpty(evt.CorrelationId));
        Assert.True(DateTimeOffset.UtcNow - evt.OccurredUtc < TimeSpan.FromMinutes(1));
        AssertNoPii(evt.Summary, "Maria", "Hernandez", "1999", "NH-24001");
    }

    [Fact]
    public async Task Resolving_an_existing_student_records_nothing_new()
    {
        await using var db = NewDb();
        var identity = TestSupport.Identity(db);

        await identity.ResolveOrCreateAsync("TexasSample", "NH-24001", Maria);
        await identity.ResolveOrCreateAsync("TexasSample", "NH-24001", Maria);

        Assert.Equal(1, await db.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Adding_an_identifier_records_an_update_without_the_value()
    {
        await using var db = NewDb();
        var identity = TestSupport.Identity(db);
        var student = await identity.ResolveOrCreateAsync("TexasSample", "NH-24001", Maria);

        await identity.AddIdentifierAsync(student.Id, "Ed-Fi", "PHX-000001");

        var evt = await db.AuditEvents.SingleAsync(e => e.Action == AuditAction.Update);
        Assert.Equal("StudentIdentifier", evt.EntityType);
        Assert.Equal(student.Id, evt.StudentId);
        Assert.Contains("Ed-Fi", evt.Summary);
        AssertNoPii(evt.Summary, "PHX-000001", "Maria", "Hernandez");
    }

    [Fact]
    public async Task Change_fails_when_the_audit_cannot_be_written()
    {
        var name = Guid.NewGuid().ToString();
        await using (var db = NewDb(name))
        {
            var identity = TestSupport.Identity(db, new FailingCurrentUser());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => identity.ResolveOrCreateAsync("TexasSample", "NH-24001", Maria));
        }

        await using var fresh = NewDb(name);
        Assert.Equal(0, await fresh.Students.CountAsync());
        Assert.Equal(0, await fresh.StudentIdentifiers.CountAsync());
        Assert.Equal(0, await fresh.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Actor_without_an_id_is_refused()
    {
        await using var db = NewDb();
        var audit = TestSupport.Audit(db, new FakeCurrentUser(new AuditActor("", "nobody")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => audit.RecordAsync(new AuditEntry(AuditAction.View, "Student", StudentId: Guid.NewGuid())));

        Assert.Equal(0, await db.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Record_saves_now_and_stage_waits_for_the_callers_save()
    {
        await using var db = NewDb();
        var audit = TestSupport.Audit(db);
        var studentId = Guid.NewGuid();

        await audit.StageAsync(new AuditEntry(AuditAction.View, "Student", studentId, studentId));
        Assert.Equal(0, await db.AuditEvents.CountAsync());

        await audit.RecordAsync(new AuditEntry(AuditAction.View, "Student", studentId, studentId));
        Assert.Equal(2, await db.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Long_summary_is_truncated()
    {
        await using var db = NewDb();
        var audit = TestSupport.Audit(db);

        await audit.RecordAsync(new AuditEntry(AuditAction.View, "Student", Summary: new string('x', 900)));

        var evt = await db.AuditEvents.SingleAsync();
        Assert.Equal(AuditLog.MaxSummaryLength, evt.Summary!.Length);
    }

    private static void AssertNoPii(string? summary, params string[] values)
    {
        Assert.False(string.IsNullOrWhiteSpace(summary));
        foreach (var value in values)
            Assert.DoesNotContain(value, summary!, StringComparison.OrdinalIgnoreCase);
    }
}
