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
    DateOnly? BirthDate,
    IReadOnlyList<SourceIdentifier>? Identifiers = null)
{
    /// <summary>The extra identifiers, never null.</summary>
    public IReadOnlyList<SourceIdentifier> ExtraIdentifiers => Identifiers ?? [];
}

/// <summary>
/// One more identifier the source holds for a student, besides
/// <see cref="SourceStudentRecord.ExternalId"/> (PHX-9.1). <see cref="Kind"/>
/// is the source's own name for it, for example "StateStudentNumber". It is
/// stored as a mapped identifier row (ADR-002) under the source system
/// "&lt;Source&gt;:&lt;Kind&gt;" - see <see cref="SourceSystemFor"/>. Only
/// ExternalId is used to match a record to a student on re-import.
/// </summary>
public sealed record SourceIdentifier(string Kind, string Value)
{
    public static string SourceSystemFor(string sourceSystem, string kind) =>
        $"{sourceSystem}:{kind}";
}

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
