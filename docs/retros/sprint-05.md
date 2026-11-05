# Sprint 5 Retrospective — Student Identity Model + Migration Foundation

**Sprint window:** 2026-11-05 (single session, per-story go/no-go)
**Demo:** admin/migration wizard — preview then apply the sample source into PhoenixSIS (needs an admin sign-in to view)

## Stories completed

| Story | Description | Status |
|---|---|---|
| PHX-5.1 | Phoenix DB on NH VM + EF Core foundation | ✅ |
| PHX-5.2 | Clean-room student identity schema (ADR-002) | ✅ |
| PHX-5.3 | Student identity resolution service | ✅ |
| PHX-5.4 | Source connector abstraction + sample connector + pipeline | ✅ |
| PHX-5.5 | Migration wizard page + docs + close | ✅ |

## Context notes

Reshaped at kickoff by a product-owner decision: clean-room schema (no source's
structure — explicitly not PowerSchool's — infects Phoenix), mapping-based
identity, and a source-agnostic migration wizard, persisted via EF Core on the
NH VM. Captured in ADR-002 and Amendment v1.3; the migration wizard became its
own epic (`data-migration`), with this sprint delivering the foundation against
a sample source. Live PowerSchool connector deferred.

## 1. What went well?

- The clean-room/anti-corruption design fell out naturally into code: a canonical
  `Student`, a `StudentIdentifier` mapping table, and an `ISisSourceConnector`
  boundary. Adding a source is now "write a connector", not "reshape the schema".
- EF Core on the VM went in cleanly — first migration applied over the VPN, and a
  rolled-back round-trip test proved schema + FK without leaving residue.
- Heavy use of the InMemory provider kept identity/pipeline logic fully unit-tested
  (14 Infrastructure tests), with opt-in live checks for the real VM.

## 2. What was harder than expected?

- A namespace collision: naming the feature `Migration` clashed with EF Core's
  `Migration` base type in generated migration files. Renamed to `DataMigration`.
- EF tooling needed the Design package on the startup project (Phoenix.Web), not
  just the context project — the `PrivateAssets=all` reference didn't flow.

## 3. What did I learn?

- Keep domain namespaces clear of framework-reserved words (`Migration`) when that
  framework generates code into your assembly.
- A singleton-free, DTO-based service boundary (Core interface, Infra impl) keeps
  EF entities out of the UI and keeps the architecture tests green.

## 4. What will I do differently next sprint?

- Stand up a Development-only test auth handler so the auth-gated wizard/pages can
  be verified headlessly (carried from Sprint 4).

## 5. Open items / follow-ups

- **Human eyeball:** sign in as admin, open `admin/migration`, Preview then Apply
  the sample source, confirm 5 students land in PhoenixSIS (and re-Apply matches).
- **Hardening (task #5)** still gates real data — now via Private Endpoint + VPN
  for nh-edfi-cus (product-owner choice 2026-11-05), plus the 32-bit worker,
  edfi_app downgrade, key backup, Entra rotation.
- **Pkcs advisory (task #12):** transitive high-severity NU1903 — pin a patched version.
- **Next:** Sprint 6 — Texas descriptors + TEDS-shaped sample data (reslotted),
  before the first Phoenix → Ed-Fi sync (Sprint 7). Live PowerSchool connector
  sits under epic:data-migration.
