using Microsoft.EntityFrameworkCore;
using Phoenix.Core.Auditing;
using Phoenix.Core.Students;
using Phoenix.Infrastructure.Auditing;
using Phoenix.Infrastructure.Data;

namespace Phoenix.Infrastructure.Tests;

/// <summary>PHX-8.5 audit viewer read side (ADR-004 decision 7).</summary>
public class AuditReaderTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 20, 18, 0, 0, TimeSpan.Zero);

    private static PhoenixDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PhoenixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AuditReader NewReader(PhoenixDbContext db, ICurrentUser? user = null) =>
        new(db, TestSupport.Audit(db, user));

    private static async Task<AuditEvent> AddEventAsync(
        PhoenixDbContext db,
        DateTimeOffset occurredUtc,
        Guid? studentId = null,
        string actorName = "someone@phoenixsis.test",
        string actorId = "00000000-0000-0000-0000-0000000000bb",
        AuditAction action = AuditAction.View)
    {
        var row = new AuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredUtc = occurredUtc,
            ActorId = actorId,
            ActorName = actorName,
            Action = action,
            EntityType = "Student",
            EntityId = studentId,
            StudentId = studentId,
            Summary = "Listed in student search.",
            CorrelationId = "test"
        };
        db.AuditEvents.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    [Fact]
    public async Task Search_is_newest_first_and_pages_by_50()
    {
        await using var db = NewDb();
        for (var i = 0; i < 60; i++)
            await AddEventAsync(db, Noon.AddMinutes(i));
        var reader = NewReader(db);

        // The actor filter keeps the reader's own AuditView rows out of the count.
        var page1 = await reader.SearchAsync(new AuditQuery(Actor: "someone@"), 1);
        var page2 = await reader.SearchAsync(new AuditQuery(Actor: "someone@"), 2);

        Assert.Equal(60, page1.Total);
        Assert.Equal(2, page1.PageCount);
        Assert.Equal(50, page1.Items.Count);
        Assert.Equal(10, page2.Items.Count);
        Assert.Equal(Noon.AddMinutes(59), page1.Items[0].OccurredUtc);
        Assert.Equal(Noon, page2.Items[^1].OccurredUtc);
    }

    [Fact]
    public async Task Student_filter_takes_a_guid_or_a_mapped_identifier()
    {
        await using var db = NewDb();
        var identity = TestSupport.Identity(db);
        var ana = await identity.ResolveOrCreateAsync(
            "Sample", "S-1", new StudentDetails("Ana", null, "Alvarez", new DateOnly(1995, 1, 1)));
        var ben = await identity.ResolveOrCreateAsync(
            "Sample", "S-2", new StudentDetails("Ben", null, "Brooks", new DateOnly(1995, 1, 2)));
        await identity.AddIdentifierAsync(ana.Id, "Ed-Fi", "PHX-000003");
        var reader = NewReader(db);

        var byGuid = await reader.SearchAsync(new AuditQuery(Student: ben.Id.ToString()), 1);
        var byNumber = await reader.SearchAsync(new AuditQuery(Student: " PHX-000003 "), 1);
        var unknown = await reader.SearchAsync(new AuditQuery(Student: "PHX-999999"), 1);

        Assert.NotEmpty(byGuid.Items);
        Assert.All(byGuid.Items, e => Assert.Equal(ben.Id, e.StudentId));
        Assert.NotEmpty(byNumber.Items);
        Assert.All(byNumber.Items, e => Assert.Equal(ana.Id, e.StudentId));
        Assert.Empty(unknown.Items);
        Assert.Equal(0, unknown.Total);
    }

    [Fact]
    public async Task Actor_filter_matches_part_of_the_name_or_the_whole_id()
    {
        await using var db = NewDb();
        await AddEventAsync(db, Noon, actorName: "hailee@phoenixsis.test", actorId: "id-hailee");
        await AddEventAsync(db, Noon.AddMinutes(1), actorName: "patrick.hines@phoenixsis.test", actorId: "id-patrick");
        var reader = NewReader(db);

        var byName = await reader.SearchAsync(new AuditQuery(Actor: "hailee"), 1);
        var byId = await reader.SearchAsync(new AuditQuery(Actor: "id-patrick"), 1);
        var partOfId = await reader.SearchAsync(new AuditQuery(Actor: "id-pat"), 1);

        Assert.Equal("id-hailee", Assert.Single(byName.Items).ActorId);
        Assert.Equal("id-patrick", Assert.Single(byId.Items).ActorId);
        Assert.Empty(partOfId.Items);
    }

    [Fact]
    public async Task Date_filters_are_inclusive_Central_days()
    {
        await using var db = NewDb();
        // 2026-11-20 05:30 UTC = 2026-11-19 23:30 Central (CST, UTC-6).
        var lateOn19 = await AddEventAsync(db, new DateTimeOffset(2026, 11, 20, 5, 30, 0, TimeSpan.Zero));
        // 2026-11-20 06:30 UTC = 2026-11-20 00:30 Central.
        var earlyOn20 = await AddEventAsync(db, new DateTimeOffset(2026, 11, 20, 6, 30, 0, TimeSpan.Zero));
        var reader = NewReader(db);
        var day19 = new DateOnly(2026, 11, 19);
        var day20 = new DateOnly(2026, 11, 20);

        var on19 = await reader.SearchAsync(new AuditQuery(From: day19, To: day19), 1);
        var from20 = await reader.SearchAsync(new AuditQuery(Actor: "someone@", From: day20), 1);

        Assert.Equal(lateOn19.Id, Assert.Single(on19.Items).Id);
        Assert.Equal(earlyOn20.Id, Assert.Single(from20.Items).Id);
        await Assert.ThrowsAsync<ArgumentException>(
            () => reader.SearchAsync(new AuditQuery(From: day20, To: day19), 1));
    }

    [Fact]
    public async Task Every_search_records_one_AuditView_without_student_or_filter_values()
    {
        await using var db = NewDb();
        var studentId = Guid.NewGuid();
        await AddEventAsync(db, Noon, studentId);
        var reader = NewReader(db);

        var first = await reader.SearchAsync(new AuditQuery(Student: studentId.ToString(), Actor: "someone"), 1);
        await reader.SearchAsync(new AuditQuery(), 1);

        Assert.DoesNotContain(first.Items, e => e.Action == AuditAction.AuditView);
        var views = await db.AuditEvents.Where(e => e.Action == AuditAction.AuditView).ToListAsync();
        Assert.Equal(2, views.Count);
        Assert.All(views, v =>
        {
            Assert.Null(v.StudentId);
            Assert.Null(v.EntityId);
            Assert.Equal(TestSupport.TestActor.Id, v.ActorId);
            Assert.DoesNotContain(studentId.ToString(), v.Summary);
            Assert.DoesNotContain("someone", v.Summary);
        });
        Assert.Contains(views, v => v.Summary == "Viewed audit trail: page 1, filtered by student, actor.");
        Assert.Contains(views, v => v.Summary == "Viewed audit trail: page 1, no filters.");
    }

    [Fact]
    public async Task No_rows_are_returned_when_the_view_cannot_be_recorded()
    {
        await using var db = NewDb();
        await AddEventAsync(db, Noon);
        var reader = NewReader(db, new FailingCurrentUser());

        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.SearchAsync(new AuditQuery(), 1));
        Assert.False(await db.AuditEvents.AnyAsync(e => e.Action == AuditAction.AuditView));
    }
}
