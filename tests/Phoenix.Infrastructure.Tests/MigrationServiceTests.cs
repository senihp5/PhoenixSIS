using Microsoft.EntityFrameworkCore;
using Phoenix.Infrastructure.Data;
using Phoenix.Infrastructure.DataMigration;
using Phoenix.Infrastructure.Students;

namespace Phoenix.Infrastructure.Tests;

public class MigrationServiceTests
{
    private static PhoenixDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PhoenixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MigrationService NewService(PhoenixDbContext db) =>
        new(new StudentIdentityService(db));

    [Fact]
    public async Task DryRun_classifies_without_writing()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        var result = await svc.RunAsync(new SampleSourceConnector(), apply: false);

        Assert.False(result.Applied);
        Assert.Equal(5, result.Total);
        Assert.Equal(5, result.Created); // "would create"
        Assert.Equal(0, result.Matched);
        Assert.Equal(0, await db.Students.CountAsync()); // nothing persisted
    }

    [Fact]
    public async Task Apply_persists_students_and_identifiers()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        var result = await svc.RunAsync(new SampleSourceConnector(), apply: true);

        Assert.True(result.Applied);
        Assert.Equal(5, result.Created);
        Assert.Equal(5, await db.Students.CountAsync());
        Assert.Equal(5, await db.StudentIdentifiers.CountAsync());
    }

    [Fact]
    public async Task Apply_is_idempotent_on_rerun()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        await svc.RunAsync(new SampleSourceConnector(), apply: true);
        var second = await svc.RunAsync(new SampleSourceConnector(), apply: true);

        Assert.Equal(0, second.Created);
        Assert.Equal(5, second.Matched);
        Assert.Equal(5, await db.Students.CountAsync()); // no duplicates
    }
}
