using Microsoft.EntityFrameworkCore;
using Phoenix.Core.Students;
using Phoenix.Infrastructure.Data;
using Phoenix.Infrastructure.Students;

namespace Phoenix.Infrastructure.Tests;

public class StudentIdentityServiceTests
{
    private static PhoenixDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PhoenixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static StudentDetails Sample =>
        new("Lisa", null, "Woods", new DateOnly(2005, 3, 1));

    [Fact]
    public async Task ResolveOrCreate_is_idempotent()
    {
        await using var db = NewDb();
        var svc = new StudentIdentityService(db);

        var a = await svc.ResolveOrCreateAsync("Ed-Fi", "100001", Sample);
        var b = await svc.ResolveOrCreateAsync("Ed-Fi", "100001", Sample);

        Assert.Equal(a.Id, b.Id);
        Assert.Equal(1, await db.Students.CountAsync());
        Assert.Equal(1, await db.StudentIdentifiers.CountAsync());
    }

    [Fact]
    public async Task GetByIdentifier_returns_null_then_the_student()
    {
        await using var db = NewDb();
        var svc = new StudentIdentityService(db);

        Assert.Null(await svc.GetByIdentifierAsync("Ed-Fi", "X"));

        var created = await svc.ResolveOrCreateAsync("Ed-Fi", "X", Sample);
        var got = await svc.GetByIdentifierAsync("Ed-Fi", "X");

        Assert.NotNull(got);
        Assert.Equal(created.Id, got!.Id);
        Assert.Equal("Woods", got.LastName);
    }

    [Fact]
    public async Task AddIdentifier_links_second_source_and_is_idempotent()
    {
        await using var db = NewDb();
        var svc = new StudentIdentityService(db);

        var s = await svc.ResolveOrCreateAsync("Ed-Fi", "100001", Sample);
        await svc.AddIdentifierAsync(s.Id, "PowerSchool", "PS-42");
        await svc.AddIdentifierAsync(s.Id, "PowerSchool", "PS-42"); // idempotent

        var viaPs = await svc.GetByIdentifierAsync("PowerSchool", "PS-42");
        Assert.Equal(s.Id, viaPs!.Id);
        Assert.Equal(2, await db.StudentIdentifiers.CountAsync());
    }

    [Fact]
    public async Task AddIdentifier_conflict_throws()
    {
        await using var db = NewDb();
        var svc = new StudentIdentityService(db);

        var s1 = await svc.ResolveOrCreateAsync("Ed-Fi", "1", Sample);
        _ = s1;
        var s2 = await svc.ResolveOrCreateAsync("Ed-Fi", "2", Sample);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AddIdentifierAsync(s2.Id, "Ed-Fi", "1")); // "1" already maps to s1
    }
}
