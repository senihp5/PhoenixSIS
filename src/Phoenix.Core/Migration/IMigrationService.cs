namespace Phoenix.Core.DataMigration;

public enum MigrationAction
{
    /// <summary>A new Phoenix student was (or would be) created for this record.</summary>
    Created,

    /// <summary>The record already maps to an existing Phoenix student.</summary>
    Matched
}

/// <summary>
/// <see cref="IdentifiersAdded"/> counts the extra identifiers added (or, in a
/// dry run, that would be added) for this student. <see cref="Conflicts"/>
/// names the identifier kinds whose value is already mapped to a different
/// student; those are never added or moved (PHX-9.1).
/// </summary>
public sealed record MigrationRowResult(
    string ExternalId,
    string DisplayName,
    MigrationAction Action,
    int IdentifiersAdded = 0,
    IReadOnlyList<string>? Conflicts = null)
{
    public IReadOnlyList<string> ConflictKinds => Conflicts ?? [];
}

/// <summary>
/// Outcome of a migration run. When <see cref="Applied"/> is false (dry run),
/// <see cref="MigrationAction.Created"/> means "would create" and
/// <see cref="MigrationAction.Matched"/> means "would match".
/// </summary>
public sealed record MigrationResult(
    string SourceSystem,
    bool Applied,
    IReadOnlyList<MigrationRowResult> Rows)
{
    public int Total => Rows.Count;
    public int Created => Rows.Count(r => r.Action == MigrationAction.Created);
    public int Matched => Rows.Count(r => r.Action == MigrationAction.Matched);
    public int IdentifiersAdded => Rows.Sum(r => r.IdentifiersAdded);
    public int Conflicts => Rows.Sum(r => r.ConflictKinds.Count);
}

/// <summary>
/// Pipes a source connector's records through the identity service, mapping
/// each to a canonical Phoenix student. Supports a no-write dry run and an
/// apply; apply is idempotent (re-running matches, never duplicates).
/// </summary>
public interface IMigrationService
{
    Task<MigrationResult> RunAsync(
        ISisSourceConnector source,
        bool apply,
        CancellationToken cancellationToken = default);
}
