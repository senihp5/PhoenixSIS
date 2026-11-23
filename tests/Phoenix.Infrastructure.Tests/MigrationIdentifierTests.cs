using Microsoft.EntityFrameworkCore;
using Phoenix.Core.Auditing;
using Phoenix.Core.DataMigration;
using Phoenix.Core.Students;
using Phoenix.Infrastructure.Data;
using Phoenix.Infrastructure.DataMigration;

namespace Phoenix.Infrastructure.Tests;

/// <summary>PHX-9.1 extra identifiers on source records.</summary>
public class MigrationIdentifierTests
{
    private static PhoenixDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PhoenixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MigrationService NewService(PhoenixDbContext db) =>
        new(TestSupport.Identity(db));

    [Fact]
    public async Task DryRun_counts_identifiers_without_writing()
    {
        await using var db = NewDb();

        var result = await NewService(db).RunAsync(new FlightdeckSourceConnector(), apply: false);

        Assert.Equal(6, result.Created);
        Assert.Equal(18, result.IdentifiersAdded);
        Assert.Equal(0, result.Conflicts);
        Assert.Equal(0, await db.StudentIdentifiers.CountAsync());
    }

    [Fact]
    public async Task Apply_stores_each_identifier_under_source_and_kind_and_audits_it()
    {
        await using var db = NewDb();

        var result = await NewService(db).RunAsync(new FlightdeckSourceConnector(), apply: true);

        Assert.Equal(6, result.Created);
        Assert.Equal(18, result.IdentifiersAdded);
        Assert.Equal(6, await db.Students.CountAsync());
        Assert.Equal(24, await db.StudentIdentifiers.CountAsync());

        var student = await TestSupport.Identity(db).GetByIdentifierAsync("Flightdeck", "100457");
        var state = await TestSupport.Identity(db)
            .GetByIdentifierAsync("Flightdeck:StateStudentNumber", "9900100457");
        Assert.NotNull(student);
        Assert.Equal(student!.Id, state!.Id);

        var added = await db.AuditEvents
            .Where(e => e.Action == AuditAction.Update && e.EntityType == "StudentIdentifier")
            .ToListAsync();
        Assert.Equal(18, added.Count);
        Assert.All(added, e => Assert.DoesNotContain("99001", e.Summary));
    }

    [Fact]
    public async Task Rerun_adds_nothing()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        await svc.RunAsync(new FlightdeckSourceConnector(), apply: true);
        var second = await svc.RunAsync(new FlightdeckSourceConnector(), apply: true);

        Assert.Equal(6, second.Matched);
        Assert.Equal(0, second.IdentifiersAdded);
        Assert.Equal(0, second.Conflicts);
        Assert.Equal(24, await db.StudentIdentifiers.CountAsync());
    }

    [Fact]
    public async Task Students_imported_before_the_change_gain_only_the_missing_identifiers()
    {
        await using var db = NewDb();
        var identity = TestSupport.Identity(db);
        var existing = await identity.ResolveOrCreateAsync(
            "Flightdeck", "100457", new StudentDetails("Marcus", null, "Ellison", new DateOnly(1990, 3, 18)));
        await identity.AddIdentifierAsync(existing.Id, "Flightdeck:StudentNumber", "260457");

        var result = await NewService(db).RunAsync(new FlightdeckSourceConnector(), apply: true);

        var row = result.Rows.Single(r => r.ExternalId == "100457");
        Assert.Equal(MigrationAction.Matched, row.Action);
        Assert.Equal(2, row.IdentifiersAdded);
        Assert.Equal(4, await db.StudentIdentifiers.CountAsync(i => i.StudentId == existing.Id));
    }

    [Fact]
    public async Task Identifier_owned_by_another_student_is_a_conflict_and_is_not_moved()
    {
        await using var db = NewDb();
        var identity = TestSupport.Identity(db);
        var other = await identity.ResolveOrCreateAsync(
            "Sample", "S-1", new StudentDetails("Other", null, "Person", new DateOnly(1991, 1, 1)));
        await identity.AddIdentifierAsync(other.Id, "Flightdeck:StateStudentNumber", "9900100457");
        var svc = NewService(db);

        var preview = await svc.RunAsync(new FlightdeckSourceConnector(), apply: false);
        var applied = await svc.RunAsync(new FlightdeckSourceConnector(), apply: true);

        foreach (var result in new[] { preview, applied })
        {
            var row = result.Rows.Single(r => r.ExternalId == "100457");
            Assert.Equal(new[] { "StateStudentNumber" }, row.ConflictKinds);
            Assert.Equal(2, row.IdentifiersAdded);
            Assert.Equal(1, result.Conflicts);
        }

        var owner = await identity.GetByIdentifierAsync("Flightdeck:StateStudentNumber", "9900100457");
        Assert.Equal(other.Id, owner!.Id);
    }

    [Fact]
    public async Task Blank_and_repeated_kinds_are_ignored()
    {
        await using var db = NewDb();
        var source = new OneRecordSource(new SourceStudentRecord(
            "X-1", "Ana", null, "Alvarez", new DateOnly(1995, 1, 1),
            [
                new SourceIdentifier("StudentNumber", " 123 "),
                new SourceIdentifier("StudentNumber", "999"),
                new SourceIdentifier("StateStudentNumber", "  "),
                new SourceIdentifier(" ", "abc")
            ]));

        var result = await NewService(db).RunAsync(source, apply: true);

        Assert.Equal(1, result.IdentifiersAdded);
        Assert.NotNull(await TestSupport.Identity(db).GetByIdentifierAsync("Test:StudentNumber", "123"));
        Assert.Equal(2, await db.StudentIdentifiers.CountAsync());
    }

    [Fact]
    public async Task Record_without_extra_identifiers_behaves_as_before()
    {
        await using var db = NewDb();

        var result = await NewService(db).RunAsync(new SampleSourceConnector(), apply: true);

        Assert.Equal(5, result.Created);
        Assert.Equal(0, result.IdentifiersAdded);
        Assert.Equal(5, await db.StudentIdentifiers.CountAsync());
    }

    private sealed class OneRecordSource(SourceStudentRecord record) : ISisSourceConnector
    {
        public string SourceSystem => "Test";
        public string DisplayName => "Test";

        public Task<IReadOnlyList<SourceStudentRecord>> ReadStudentsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceStudentRecord>>([record]);
    }
}
