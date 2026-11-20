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
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

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

        // ADR-004 append-only audit trail. No FK to Student on purpose: the
        // trail must outlive any record it describes. HasTrigger tells EF the
        // table has a trigger (TR_AuditEvent_AppendOnly, created in the
        // AddAuditEvent migration) so it never uses OUTPUT without INTO.
        modelBuilder.Entity<AuditEvent>(e =>
        {
            e.ToTable("AuditEvent", t => t.HasTrigger(AuditEvent.AppendOnlyTriggerName));
            e.HasKey(a => a.Id);
            e.Property(a => a.ActorId).HasMaxLength(100).IsRequired();
            e.Property(a => a.ActorName).HasMaxLength(256).IsRequired();
            e.Property(a => a.Action).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(a => a.EntityType).HasMaxLength(50).IsRequired();
            e.Property(a => a.Summary).HasMaxLength(400);
            e.Property(a => a.CorrelationId).HasMaxLength(100);
            e.HasIndex(a => new { a.StudentId, a.OccurredUtc });
            e.HasIndex(a => new { a.ActorId, a.OccurredUtc });
            e.HasIndex(a => a.OccurredUtc);
        });
    }
}
