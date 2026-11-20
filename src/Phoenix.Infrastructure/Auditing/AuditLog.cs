using System.Diagnostics;
using Phoenix.Core.Auditing;
using Phoenix.Infrastructure.Data;

namespace Phoenix.Infrastructure.Auditing;

/// <summary>
/// ADR-004 audit trail on the scoped <see cref="PhoenixDbContext"/>. Staged
/// events share the caller's unit of work, so a change and its audit rows
/// commit together or not at all.
/// </summary>
public sealed class AuditLog : IAuditLog
{
    public const int MaxSummaryLength = 400;

    private readonly PhoenixDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly string _scopeCorrelationId = Guid.NewGuid().ToString("N");

    public AuditLog(PhoenixDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task StageAsync(IEnumerable<AuditEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var list = entries.ToList();
        if (list.Count == 0)
            return;

        var actor = await _currentUser.GetActorAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(actor.Id) || string.IsNullOrWhiteSpace(actor.Name))
            throw new InvalidOperationException("Audit actor is missing; the action is refused (ADR-004 fail closed).");

        var now = DateTimeOffset.UtcNow;
        var correlationId = Activity.Current?.TraceId.ToString() ?? _scopeCorrelationId;

        foreach (var entry in list)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.EntityType);

            _db.AuditEvents.Add(new AuditEvent
            {
                Id = Guid.NewGuid(),
                OccurredUtc = now,
                ActorId = actor.Id,
                ActorName = actor.Name,
                Action = entry.Action,
                EntityType = entry.EntityType,
                EntityId = entry.EntityId,
                StudentId = entry.StudentId,
                Summary = Truncate(entry.Summary),
                CorrelationId = correlationId
            });
        }
    }

    public async Task RecordAsync(IEnumerable<AuditEntry> entries, CancellationToken cancellationToken = default)
    {
        await StageAsync(entries, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string? Truncate(string? summary) =>
        summary is { Length: > MaxSummaryLength } ? summary[..MaxSummaryLength] : summary;
}
