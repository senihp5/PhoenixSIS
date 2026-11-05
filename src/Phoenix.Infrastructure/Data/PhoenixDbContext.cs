using Microsoft.EntityFrameworkCore;

namespace Phoenix.Infrastructure.Data;

/// <summary>
/// EF Core context for Phoenix's own clean-room data store (PhoenixSIS on the
/// NH SQL VM). Schema is Phoenix-native; external identifiers are mapped, not
/// adopted. See ADR-002.
/// </summary>
public class PhoenixDbContext : DbContext
{
    public PhoenixDbContext(DbContextOptions<PhoenixDbContext> options)
        : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentIdentifier> StudentIdentifiers => Set<StudentIdentifier>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>(e =>
        {
            e.ToTable("Student");
            e.HasKey(s => s.Id);
            e.Property(s => s.FirstName).HasMaxLength(100).IsRequired();
            e.Property(s => s.MiddleName).HasMaxLength(100);
            e.Property(s => s.LastName).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<StudentIdentifier>(e =>
        {
            e.ToTable("StudentIdentifier");
            e.HasKey(i => i.Id);
            e.Property(i => i.SourceSystem).HasMaxLength(50).IsRequired();
            e.Property(i => i.Value).HasMaxLength(200).IsRequired();
            e.HasIndex(i => new { i.SourceSystem, i.Value }).IsUnique();
            e.HasOne(i => i.Student)
                .WithMany(s => s.Identifiers)
                .HasForeignKey(i => i.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
