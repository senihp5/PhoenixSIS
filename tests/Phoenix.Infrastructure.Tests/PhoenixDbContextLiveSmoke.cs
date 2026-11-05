using Microsoft.EntityFrameworkCore;
using Phoenix.Infrastructure.Data;

namespace Phoenix.Infrastructure.Tests;

/// <summary>
/// Opt-in live check that the Phoenix DbContext can reach the PhoenixSIS
/// database on the NH VM. Runs only when PHOENIX_DB_SMOKE holds a connection
/// string; a no-op pass otherwise (CI / no VPN).
/// </summary>
public class PhoenixDbContextLiveSmoke
{
    [Fact]
    public async Task Live_can_connect()
    {
        var conn = Environment.GetEnvironmentVariable("PHOENIX_DB_SMOKE");
        if (string.IsNullOrWhiteSpace(conn))
            return; // no connection string — skip

        var options = new DbContextOptionsBuilder<PhoenixDbContext>()
            .UseSqlServer(conn)
            .Options;

        await using var ctx = new PhoenixDbContext(options);
        Assert.True(await ctx.Database.CanConnectAsync());
    }
}
