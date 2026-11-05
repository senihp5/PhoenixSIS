using Microsoft.EntityFrameworkCore;

namespace Phoenix.Infrastructure.Data;

/// <summary>
/// EF Core context for Phoenix's own clean-room data store (PhoenixSIS on the
/// NH SQL VM). Entities are added in PHX-5.2 (Student, StudentIdentifier) —
/// this sprint establishes the context + connectivity.
/// </summary>
public class PhoenixDbContext : DbContext
{
    public PhoenixDbContext(DbContextOptions<PhoenixDbContext> options)
        : base(options)
    {
    }
}
