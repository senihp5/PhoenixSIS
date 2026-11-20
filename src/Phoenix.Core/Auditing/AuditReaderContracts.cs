namespace Phoenix.Core.Auditing;

/// <summary>
/// Filters for the audit viewer (PHX-8.5). All are optional and combine with AND.
/// <see cref="Student"/> is a student GUID or any mapped identifier value
/// (for example PHX-000003). <see cref="Actor"/> matches part of the actor's
/// name (UPN) or the whole actor id. <see cref="From"/> and <see cref="To"/>
/// are inclusive Central-time dates.
/// </summary>
public sealed record AuditQuery(
    string? Student = null,
    string? Actor = null,
    DateOnly? From = null,
    DateOnly? To = null);

/// <summary>One audit row as shown to an admin. Carries no student PII (ADR-004).</summary>
public sealed record AuditEventInfo(
    Guid Id,
    DateTimeOffset OccurredUtc,
    string ActorId,
    string ActorName,
    AuditAction Action,
    string EntityType,
    Guid? EntityId,
    Guid? StudentId,
    string? Summary,
    string? CorrelationId);

public sealed record AuditPage(
    IReadOnlyList<AuditEventInfo> Items,
    int Total,
    int Page,
    int PageSize)
{
    public int PageCount => Total == 0 ? 1 : (Total + PageSize - 1) / PageSize;
}

/// <summary>
/// Reads the audit trail for the admin viewer (ADR-004 decision 7). Newest
/// first, paged. Every call records one AuditView event - with no student id -
/// before the rows are returned; if that write fails the call throws and no
/// rows are returned (fail closed). Always on; never behind a feature flag.
/// </summary>
public interface IAuditReader
{
    Task<AuditPage> SearchAsync(
        AuditQuery query,
        int page,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The audit trail stores UTC. People read and filter it in Central time
/// (the project time zone).
/// </summary>
public static class AuditTimeZone
{
    public static readonly TimeZoneInfo Central =
        TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

    public static DateTimeOffset ToCentral(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, Central);

    /// <summary>The instant a Central calendar day starts.</summary>
    public static DateTimeOffset StartOfDay(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Central.GetUtcOffset(local));
    }
}
