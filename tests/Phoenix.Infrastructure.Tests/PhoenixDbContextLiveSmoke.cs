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

    [Fact]
    public async Task Live_student_round_trip()
    {
        var conn = Environment.GetEnvironmentVariable("PHOENIX_DB_SMOKE");
        if (string.IsNullOrWhiteSpace(conn))
            return; // no connection string — skip

        var options = new DbContextOptionsBuilder<PhoenixDbContext>()
            .UseSqlServer(conn)
            .Options;

        await using var ctx = new PhoenixDbContext(options);
        await using var tx = await ctx.Database.BeginTransactionAsync();

        var student = new Student
        {
            Id = Guid.NewGuid(),
            FirstName = "Round",
            LastName = "Trip",
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };
        student.Identifiers.Add(new StudentIdentifier
        {
            Id = Guid.NewGuid(),
            SourceSystem = "Test",
            Value = Guid.NewGuid().ToString()
        });
        ctx.Students.Add(student);
        await ctx.SaveChangesAsync();

        var read = await ctx.Students
            .Include(s => s.Identifiers)
            .FirstAsync(s => s.Id == student.Id);

        Assert.Equal("Round", read.FirstName);
        Assert.Single(read.Identifiers);

        await tx.RollbackAsync(); // leave no residue
    }
}
