namespace Phoenix.Infrastructure.Data;

/// <summary>
/// Maps a source system's identifier for a student to the canonical Phoenix
/// <see cref="Student"/>. One student may carry many identifiers (one per
/// source). (SourceSystem, Value) is unique. This is the anti-corruption
/// seam from ADR-002 — adding a source adds rows, not schema.
/// </summary>
public class StudentIdentifier
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    /// <summary>e.g. "Ed-Fi", "PowerSchool", "TSDS".</summary>
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>The identifier value in that source system.</summary>
    public string Value { get; set; } = string.Empty;

    public Student? Student { get; set; }
}
