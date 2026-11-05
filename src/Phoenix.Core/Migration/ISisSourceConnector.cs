namespace Phoenix.Core.DataMigration;

/// <summary>
/// A source-neutral student record. A connector maps its source's native shape
/// into this at the boundary (ADR-002) — downstream code never sees the
/// source's own structure.
/// </summary>
public sealed record SourceStudentRecord(
    string ExternalId,
    string FirstName,
    string? MiddleName,
    string LastName,
    DateOnly? BirthDate);

/// <summary>
/// Reads students from some source SIS as source-neutral records. Each source
/// (PowerSchool, a CSV export, another SIS) gets its own implementation; the
/// migration pipeline depends only on this abstraction.
/// </summary>
public interface ISisSourceConnector
{
    /// <summary>Identifier namespace for this source, e.g. "PowerSchool", "Sample".</summary>
    string SourceSystem { get; }

    /// <summary>Human-readable source name for the wizard.</summary>
    string DisplayName { get; }

    Task<IReadOnlyList<SourceStudentRecord>> ReadStudentsAsync(
        CancellationToken cancellationToken = default);
}
