# ADR-002: Source-Agnostic Identity and Clean-Room Schema

**Status:** Accepted
**Date:** 2026-11-05
**Deciders:** Patrick Hines

## Context

Phoenix must ingest student data from source systems — PowerSchool today, other SIS's later — and will also exchange data with Ed-Fi and Texas TSDS. Each of those systems has its own schema, naming conventions, and identifier scheme. The tempting shortcut is to shape Phoenix's own tables after whichever source we integrate first (PowerSchool). That shortcut is a trap: it welds Phoenix to one vendor's data model, makes a second source awkward, and drags a foreign (and, in PowerSchool's case, awkwardly named) structure into the heart of a system meant to outlast any one source.

## Decision

1. **Clean-room schema.** Phoenix tables and columns use Phoenix-native, intention-revealing names. No source system's structure or naming — explicitly **not** PowerSchool's — is adopted into Phoenix's schema. Sources are translated at the boundary, never mirrored.

2. **Mapping-based identity (anti-corruption layer).** There is one canonical Phoenix student (`Student`, with a Phoenix-generated GUID key). Every external identifier — Ed-Fi `studentUniqueId`, a PowerSchool student number, a TSDS unique ID — is a row in `StudentIdentifier` (`SourceSystem`, `Value`), unique per source system. Adding a new source is adding rows and a connector, never reshaping the schema.

3. **Source connectors are explicit translators.** Each source is reached through a connector that maps its native shape into Phoenix's model at the boundary (see the Data Migration epic). The mapping is code we own and can see; the source's representation stops at the connector.

4. **Identity is core, not a module.** The canonical `Student` and its identifiers are core domain (always present), used by both inbound migration and outbound sync. Feature-flagged modules (e.g. Migration, EdFi) depend on it; it depends on none of them.

## Consequences

**Positive:** Phoenix's data model is owned and legible; a second or third SIS plugs in without schema churn; the same canonical student serves inbound migration and outbound Ed-Fi/TSDS reporting; identifier reconciliation is explicit and queryable.

**Negative:** every source needs a connector and an explicit mapping (no "just point EF at their tables"); the mapping layer is ongoing work as sources are added; a canonical identity means resolve-or-create logic must be correct to avoid duplicate students (addressed by the identity resolution service, PHX-5.3).

**Neutral/notes:** this is the data-layer counterpart to ADR-001's clean-room *code* policy — ADR-001 keeps foreign code out, ADR-002 keeps foreign *schema* out. Persistence is EF Core against the `PhoenixSIS` database on the NH SQL VM (same-VM decision, consistent with Ed-Fi).

## References

- `docs\04-Planning\Sprint-Plan-Amendment-v1.1.md` (Addendum v1.3 — identity/persistence decision + Data Migration epic)
- `docs\04-Planning\Sprint-5-Story-Cards.md`
- ADR-001 (clean-room code policy)
