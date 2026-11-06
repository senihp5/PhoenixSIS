using Microsoft.EntityFrameworkCore;
using Phoenix.Infrastructure.Data;
using Phoenix.Infrastructure.DataMigration;
using Phoenix.Infrastructure.Students;

namespace Phoenix.Infrastructure.Tests;

public class TexasSampleSourceConnectorTests
{
    private static PhoenixDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PhoenixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Yields_adult_hs_sample_records()
    {
        var connector = new TexasSampleSourceConnector();
        var records = await connector.ReadStudentsAsync();

        Assert.Equal("TexasSample", connector.SourceSystem);
        Assert.Equal(8, records.Count);
        Assert.All(records, r => Assert.StartsWith("NH-", r.ExternalId));
        // Adult population: everyone is an adult (born well before today).
        Assert.All(records, r => Assert.True(r.BirthDate < new DateOnly(2006, 1, 1)));
    }

    [Fact]
    public async Task Imports_into_phoenix_via_pipeline_idempotently()
    {
        await using var db = NewDb();
        var svc = new MigrationService(new StudentIdentityService(db));
        var connector = new TexasSampleSourceConnector();

        var applied = await svc.RunAsync(connector, apply: true);
        Assert.Equal(8, applied.Created);
        Assert.Equal(8, await db.Students.CountAsync());
        Assert.Equal(8, await db.StudentIdentifiers.CountAsync());

        var second = await svc.RunAsync(connector, apply: true);
        Assert.Equal(0, second.Created);
        Assert.Equal(8, second.Matched);
        Assert.Equal(8, await db.Students.CountAsync()); // no duplicates
    }
}
