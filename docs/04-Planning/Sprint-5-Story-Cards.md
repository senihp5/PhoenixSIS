# Sprint 5: Student Identity Model + Migration Foundation

**Sprint Goal:** Phoenix gets its own clean-room student identity — a canonical Phoenix student with source identifiers mapped (never adopted) — persisted via EF Core to a `PhoenixSIS` database on the NH SQL VM, plus the source-agnostic migration pipeline proven end to end against a sample source. No PowerSchool API this sprint; the live connector comes later under the same epic.

**Total Estimated Hours:** 16-24
**Epic:** Epic 4 — Data Migration & Clean-Room Identity (`epic:data-migration`)
**Design decisions (2026-11-05, product owner):**
- Phoenix uses **clean, Phoenix-native naming** — a source SIS's table/column structure never infects Phoenix (anti-corruption layer). Captured in **ADR-002**.
- Student identity is **mapping-based**: one canonical Phoenix `Student`; external IDs (Ed-Fi, PowerSchool, TSDS, …) live in a `StudentIdentifier` table, so adding a source is adding rows, not reshaping schema.
- A **data migration wizard** connects a source SIS to PhoenixSIS and translates into the clean schema — built source-agnostic so other SIS's plug in later.
- **Persistence: EF Core, tables on the NH VM SQL server (10.10.2.4)**, new `PhoenixSIS` database. (Same-VM choice is consistent with the Ed-Fi decision — no local SQL horsepower.)

**Guardrails:** clean-room (ADR-001 + ADR-002) — no CMS/PowerSchool code or naming copied in. `main` always builds; incomplete work behind the `Migration` flag (default OFF). Secrets (SQL creds) in user-secrets, never the repo.

---

## PHX-5.1: Phoenix database + EF Core foundation

**Estimate:** 3-5 hrs

### Story
As the developer, I want a `PhoenixSIS` database on the NH VM and EF Core wired into Phoenix, so the app has a clean place to persist its own data.

### Tasks
- [ ] `_tools\New-PhoenixDb.ps1` (CMS ci-sql pattern): create database **PhoenixSIS** on 10.10.2.4 and SQL login **phoenix_app** (least privilege — db_owner on PhoenixSIS only, NOT sysadmin); creds to `C:\temp\phoenix-sql.ps1`, never the repo
- [ ] Add EF Core to Phoenix.Infrastructure: `Microsoft.EntityFrameworkCore.SqlServer` (+ `Microsoft.EntityFrameworkCore.Design` where tooling needs it)
- [ ] `PhoenixDbContext` in Phoenix.Infrastructure\Data; connection string key `PhoenixDb` (BaseUrl-style: non-secret parts in appsettings, credentials in user-secrets)
- [ ] Register DbContext in a module's RegisterServices (see 5.5 module) or a core data registration; `Encrypt=false` for the VM like Ed-Fi
- [ ] Verify `dotnet ef` tooling runs and the context connects to the VM DB (CanConnect check)

### Definition of Done
- [ ] PhoenixSIS DB + phoenix_app login exist on the VM (reproducible from the script)
- [ ] EF Core migrations tooling works; context connects over the VPN
- [ ] build + test green

---

## PHX-5.2: Clean-room student identity schema (ADR-002)

**Estimate:** 3-4 hrs

### Story
As the architect, I want a clean Phoenix student schema with source identifiers mapped separately, so no source system's structure dictates ours.

### Tasks
- [ ] Write **ADR-002 — Source-agnostic identity & clean-room schema**: Phoenix-native naming; sources translated at the boundary; identity reconciliation via a mapping table; rationale (avoid PowerSchool structural lock-in, enable multi-source)
- [ ] `Student` entity — canonical Phoenix PK (GUID), clean columns (FirstName, MiddleName, LastName, BirthDate, …). No source-specific columns
- [ ] `StudentIdentifier` entity — (StudentId FK, SourceSystem, Value), unique index on (SourceSystem, Value); this is where Ed-Fi studentUniqueId / PowerSchool number live
- [ ] First EF migration; apply to the VM DB
- [ ] Contracts the UI/other modules consume go in Phoenix.Core (e.g. a Student DTO); EF entities stay in Infrastructure

### Definition of Done
- [ ] Migration creates clean-named tables on the VM; no PowerSchool naming anywhere
- [ ] Round-trip insert/read integration check passes
- [ ] ADR-002 committed; architecture tests green

---

## PHX-5.3: Identity resolution service

**Estimate:** 3-4 hrs

### Story
As a caller (sync or migration), I want to resolve-or-create a Phoenix student by any source identifier, so the same person is never duplicated across sources.

### Tasks
- [ ] `IStudentIdentityService` (Phoenix.Core contract): `ResolveOrCreateAsync(sourceSystem, externalId, StudentDetails)`, `GetByIdentifierAsync(sourceSystem, externalId)`, `AddIdentifierAsync(...)`
- [ ] Implementation over `PhoenixDbContext` in Infrastructure; idempotent (same source+externalId → same Student); enforces identifier uniqueness
- [ ] Unit tests on the resolution logic (EF Core SQLite in-memory or the InMemory provider for the service logic) + one VM integration smoke

### Definition of Done
- [ ] Resolve-or-create is idempotent; duplicate identifiers rejected/ignored correctly
- [ ] tests green

---

## PHX-5.4: Source connector abstraction + sample connector + migration pipeline

**Estimate:** 4-6 hrs

### Story
As the developer, I want a source-agnostic connector + a migration pipeline with a dry-run, so any SIS can feed Phoenix without its structure leaking in.

### Tasks
- [ ] `ISisSourceConnector` (Core): describes a source and yields source-neutral `SourceStudentRecord`s (not a source's native shape)
- [ ] A **sample/CSV connector** (no external API) implementing it — proves the pipeline and keeps PowerSchool's API out of this sprint
- [ ] `MigrationService`: pipes connector records → `IStudentIdentityService` (map → resolve/create), with **DRY-RUN** (preview, no writes) and **APPLY** modes; returns a result (would-create vs would-match counts, per-row outcomes)
- [ ] Mapping is explicit at the boundary (source field → Phoenix field) — the anti-corruption layer in code

### Definition of Done
- [ ] Dry-run over the sample source yields a correct preview with no writes
- [ ] Apply persists students + identifiers to Phoenix
- [ ] Re-running apply is idempotent (matches, doesn't duplicate)
- [ ] tests green

---

## PHX-5.5: Migration wizard (preview) page + docs + close

**Estimate:** 3-5 hrs

### Story
As an admin, I want a wizard page to preview and run a migration from a source, so the pipeline is usable and demoable.

### Tasks
- [ ] `Migration` feature flag (Features, default OFF) + a `MigrationModule : IModule` (registers DbContext + services; flag-guarded)
- [ ] Admin page `admin/migration` (RequireAdmin, flag-guarded, conditional nav): choose the sample source, run dry-run, show mapped preview (would-create/would-match + sample rows), confirm → apply, show result
- [ ] `docs\features\modules\Migration.md` + link ADR-002; note live PowerSchool connector + full wizard UX are later sprints under epic:data-migration
- [ ] Retro `docs\retros\sprint-05.md`; tag `phoenix-sprint-05`; update project status doc

### Definition of Done
- [ ] Page runs a dry-run and an apply against the sample source
- [ ] `main` builds/runs/tests green with the Migration flag ON and OFF
- [ ] Sprint tagged
