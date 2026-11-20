using Phoenix.Core.Auditing;

namespace Phoenix.Infrastructure.Data;

/// <summary>
/// One row of the append-only audit trail (ADR-004). The application never
/// updates or deletes these rows, and the TR_AuditEvent_AppendOnly trigger
/// rejects UPDATE and DELETE in the database. No student PII is stored here:
/// the actor, GUIDs and a short non-identifying summary only.
/// </summary>
public class AuditEvent
{
    public const string AppendOnlyTriggerName = "TR_AuditEvent_AppendOnly";

    public Guid Id { get; set; }

    public DateTimeOffset OccurredUtc { get; set; }

    /// <summary>Entra object id of the person, or system:&lt;process&gt;.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>The person's UPN, or system:&lt;process&gt;.</summary>
    public string ActorName { get; set; } = string.Empty;

    public AuditAction Action { get; set; }

    /// <summary>e.g. "Student", "StudentIdentifier", "Enrollment".</summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    /// <summary>The student the event concerns, for per-student access history.</summary>
    public Guid? StudentId { get; set; }

    public string? Summary { get; set; }

    /// <summary>Groups the events of one operation (request trace or circuit scope).</summary>
    public string? CorrelationId { get; set; }
}
