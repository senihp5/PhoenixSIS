using Microsoft.EntityFrameworkCore;
using Phoenix.Core.Auditing;
using Phoenix.Infrastructure.Data;

namespace Phoenix.Infrastructure.Auditing;

/// <summary>
/// PHX-8.5 audit viewer read side. See <see cref="IAuditReader"/>. The
/// AuditView event names which filters were used, never their values, and
/// carries no student id (ADR-004 decision 7).
/// </summary>
public sealed class AuditReader : IAuditReader
{
    public const int PageSize = 50;
    private const string EntityType = "AuditEvent";

    private readonly PhoenixDbContext _db;
    private readonly IAuditLog _audit;

    public AuditReader(PhoenixDbContext db, IAuditLog audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<AuditPage> SearchAsync(
        AuditQuery query,
        int page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.From is { } first && query.To is { } last && last < first)
            throw new ArgumentException("The 'to' date is before the 'from' date.", nameof(query));

        var events = _db.AuditEvents.AsNoTracking();
        var filters = new List<string>();

        if (!string.IsNullOrWhiteSpace(query.Student))
        {
            filters.Add("student");
            var studentIds = await ResolveStudentIdsAsync(query.Student.Trim(), cancellationToken);
            events = events.Where(e => e.StudentId != null && studentIds.Contains(e.StudentId.Value));
        }

        if (!string.IsNullOrWhiteSpace(query.Actor))
        {
            filters.Add("actor");
            var actor = query.Actor.Trim();
            events = events.Where(e => e.ActorName.Contains(actor) || e.ActorId == actor);
        }

        if (query.From is { } fromDay)
        {
            filters.Add("from date");
            var fromUtc = AuditTimeZone.StartOfDay(fromDay).ToUniversalTime();
            events = events.Where(e => e.OccurredUtc >= fromUtc);
        }

        if (query.To is { } toDay)
        {
            filters.Add("to date");
            var beforeUtc = AuditTimeZone.StartOfDay(toDay.AddDays(1)).ToUniversalTime();
            events = events.Where(e => e.OccurredUtc < beforeUtc);
        }

        var total = await events.CountAsync(cancellationToken);
        var pageCount = total == 0 ? 1 : (total + PageSize - 1) / PageSize;
        var safePage = Math.Clamp(page, 1, pageCount);

        var rows = await events
            .OrderByDescending(e => e.OccurredUtc)
            .ThenByDescending(e => e.Id)
            .Skip((safePage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(
                AuditAction.AuditView,
                EntityType,
                Summary: filters.Count == 0
                    ? $"Viewed audit trail: page {safePage}, no filters."
                    : $"Viewed audit trail: page {safePage}, filtered by {string.Join(", ", filters)}."),
            cancellationToken);

        var items = rows
            .Select(e => new AuditEventInfo(
                e.Id, e.OccurredUtc, e.ActorId, e.ActorName, e.Action, e.EntityType,
                e.EntityId, e.StudentId, e.Summary, e.CorrelationId))
            .ToList();

        return new AuditPage(items, total, safePage, PageSize);
    }

    /// <summary>
    /// A GUID is taken as the student id. Anything else is looked up as a
    /// mapped identifier value in any source system; no match gives an empty
    /// list, so the search returns nothing rather than everything.
    /// </summary>
    private async Task<List<Guid>> ResolveStudentIdsAsync(string student, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(student, out var id))
            return [id];

        return await _db.StudentIdentifiers
            .AsNoTracking()
            .Where(i => i.Value == student)
            .Select(i => i.StudentId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
