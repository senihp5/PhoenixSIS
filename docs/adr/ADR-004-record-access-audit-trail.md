# ADR-004: Record-Access Audit Trail

**Status:** Proposed (Sprint 8)
**Date:** 2026-11-20
**Deciders:** Patrick Hines

## Context

Phoenix holds student education records. Before any real New Heights data loads, Phoenix
must be able to answer "who viewed or changed which student, and when". The draft
data-sharing authorization (ADR-003 gate b) promises exactly that, and FERPA's direct-control
expectation for a school official depends on it. Today Phoenix records nothing: the migration
wizard, the Ed-Fi sync and every page that shows a student leave no trace of who acted.

## Decision

1. **Core, always on.** The audit trail is core domain, like identity (ADR-002). It is not a
   feature-flagged module, and no flag can turn recording off. Modules depend on the Core
   contract `IAuditLog`; it depends on none of them.

2. **One append-only table.** `AuditEvent` in the PhoenixSIS database: `Id`, `OccurredUtc`,
   `ActorId` (Entra object id), `ActorName` (UPN), `Action`, `EntityType`, `EntityId`,
   `StudentId` (the student the event concerns, for "who touched this student"), `Summary`,
   `CorrelationId`. The application has no update or delete path, and a database trigger
   rejects `UPDATE` and `DELETE` on the table.

3. **No student PII in audit rows.** Rows carry the actor's identity, GUIDs and short
   non-identifying summaries (e.g. "Enrollment added: campus 220822001, SY 2027"). Never names,
   birth dates, SSNs or other record content.

4. **Per-student granularity.** Anything that displays or changes an identifiable student
   writes one event per student: detail view, list rows shown, create, identifier added,
   enroll, exit, sync preview, sync apply. A list page is paged (25 rows), so a page view is
   at most 25 events. Events for one operation are written in one `SaveChanges`.

5. **Fail closed.** For changes, the audit rows are saved in the same unit of work as the
   change, so a change without its audit row cannot commit. For reads, the events are written
   before the data is returned. If the audit write fails, the operation fails and the page
   shows an error, not the data.

6. **Actor.** `ICurrentUser` (Core) is implemented in Phoenix.Web from the Blazor
   authentication state (`oid` and `preferred_username` claims). Work with no signed-in user
   records a system actor (`system:<process>`).

7. **Viewing the trail.** An admin-only page (`RequireAdmin`) lists events newest first,
   filtered by student, actor and date. Reading the trail is itself recorded (one event, no
   student ids).

## Consequences

**Positive:** Phoenix can answer the data owner's access questions per student; the
authorization's audit promise becomes real; tampering through the application is impossible
and through the database requires disabling a trigger, which is itself visible.

**Negative:** every read path that shows students gains a write; list views generate one row
per student shown; services that touch students take an `IAuditLog` dependency; the table
grows without bound until New Heights sets a retention policy.

**Neutral/notes:** retention is indefinite until New Heights sets a policy (open question).
A login with `db_owner` can still disable the trigger; separating the application login from
the schema owner is a later hardening item. Ed-Fi's own logs are out of scope; Phoenix audits
what Phoenix users do.

## References

- ADR-002 (identity is core), ADR-003 (data gates), Sprint 8 cards
- Draft "Phoenix SIS Data Sharing Authorization", section 5 (audit logging commitment)
- 34 CFR 99.31(a)(1)(i)(B) (school official under direct control)
