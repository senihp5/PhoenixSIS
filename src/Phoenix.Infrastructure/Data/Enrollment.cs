using Phoenix.Core.Students;

namespace Phoenix.Infrastructure.Data;

/// <summary>
/// A Phoenix enrollment (Sprint 8): one student at one campus for one school
/// year. Phoenix-native names and codes (ADR-002); Ed-Fi descriptors are mapped
/// at the EdFi boundary. (StudentId, CampusNumber, EntryDate) is unique - the
/// same natural key Ed-Fi uses for a StudentSchoolAssociation.
/// </summary>
public class Enrollment
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    /// <summary>9-digit TEA campus number, e.g. 220822001.</summary>
    public string CampusNumber { get; set; } = string.Empty;

    /// <summary>Ending year: 2027 = 2026-27.</summary>
    public int SchoolYear { get; set; }

    public DateOnly EntryDate { get; set; }

    public GradeLevel EntryGradeLevel { get; set; }

    public EnrollmentKind Kind { get; set; }

    /// <summary>0 &lt; FTE &lt;= 1.</summary>
    public decimal FullTimeEquivalency { get; set; }

    public bool IsPrimary { get; set; }

    public DateOnly? ExitDate { get; set; }

    public ExitReason? ExitReason { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }

    public Student? Student { get; set; }
}
