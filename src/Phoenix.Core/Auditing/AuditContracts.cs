namespace Phoenix.Core.Auditing;

/// <summary>What a person did to a record (ADR-004).</summary>
public enum AuditAction
{
    View,
    Create,
    Update,
    Enroll,
    Exit,
    SyncPreview,
    SyncApply,
    AuditView
}

/// <summary>
/// One audit event to record. <see cref="StudentId"/> is the student the
/// event concerns. <see cref="Summary"/> is short and must not contain
/// student PII (no names, birth dates, SSNs or record content) - ADR-004.
/// </summary>
public sealed record AuditEntry(
    AuditAction Action,
    string EntityType,
    Guid? EntityId = null,
    Guid? StudentId = null,
    string? Summary = null);

/// <summary>Who performed an audited action.</summary>
public sealed record AuditActor(string Id, string Name)
{
    /// <summary>An actor for work done with no signed-in user.</summary>
    public static AuditActor System(string process) => new($"system:{process}", $"system:{process}");
}

/// <summary>
/// The person behind the current request or Blazor circuit. Implemented in
/// Phoenix.Web from the authentication state.
/// </summary>
public interface ICurrentUser
{
    ValueTask<AuditActor> GetActorAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Append-only record-access audit trail (ADR-004). Always on; never behind a
/// feature flag.
/// </summary>
public interface IAuditLog
{
    /// <summary>
    /// Adds the events to the current unit of work. They are saved by the
    /// caller's next save, together with the change they describe - so a
    /// change cannot commit without its audit rows (fail closed).
    /// </summary>
    Task StageAsync(IEnumerable<AuditEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the events now. Read paths call this before returning data; if
    /// it throws, the caller must not show the data (fail closed).
    /// </summary>
    Task RecordAsync(IEnumerable<AuditEntry> entries, CancellationToken cancellationToken = default);
}

public static class AuditLogExtensions
{
    public static Task StageAsync(this IAuditLog log, AuditEntry entry, CancellationToken cancellationToken = default) =>
        log.StageAsync([entry], cancellationToken);

    public static Task RecordAsync(this IAuditLog log, AuditEntry entry, CancellationToken cancellationToken = default) =>
        log.RecordAsync([entry], cancellationToken);
}
