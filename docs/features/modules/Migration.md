# Migration Module

**Flag:** `Migration` (in `Features`, default OFF in base `appsettings.json`; ON in the `Phoenix-Dev` overlay)
**Born:** Sprint 5 (PHX-5.4–5.5), 2026-11-05
**Epic:** Epic 4 — Data Migration & Clean-Room Identity (`epic:data-migration`)
**See also:** ADR-002 (source-agnostic identity & clean-room schema)

## Purpose

Imports students from a source SIS into Phoenix's own clean-room store, mapping
each source record to a canonical Phoenix student. Built source-agnostic so new
SIS's plug in without touching Phoenix's schema — the anti-corruption layer of
ADR-002 in working code.

## Shape

```
src\Phoenix.Core\Migration\          ISisSourceConnector, SourceStudentRecord, IMigrationService, MigrationResult
src\Phoenix.Infrastructure\Migration\ MigrationModule, MigrationService, SampleSourceConnector
src\Phoenix.Web\Components\Pages\Migration\ Wizard.razor
```

> Note: the folders are named `Migration`, but the namespaces are
> `Phoenix.*.DataMigration` — EF Core's own `Migration` base type collides with
> a `Migration` namespace in generated migration files, so the data-migration
> code uses `DataMigration`.

## How it works

- **`ISisSourceConnector`** describes a source and yields source-neutral
  `SourceStudentRecord`s. A source's native shape is mapped here, at the
  boundary, and never travels further.
- **`SampleSourceConnector`** is a built-in dataset so the pipeline is runnable
  without an external SIS. A real file/CSV or PowerSchool connector plugs into
  the same interface in a later sprint (under epic:data-migration).
- **`MigrationService.RunAsync(source, apply)`** pipes records through
  `IStudentIdentityService`:
  - **Dry run** (`apply: false`) — classifies each record as *would create* or
    *would match*; writes nothing.
  - **Apply** (`apply: true`) — resolves-or-creates each student; idempotent, so
    re-running matches existing students instead of duplicating.

## Wizard page

`admin/migration` (RequireAdmin, InteractiveServer, flag-guarded in markup;
conditional nav link). Pick the source, **Preview (dry run)** to see the mapped
outcome and counts, then **Apply**. Never throws to the user — errors render in
an alert.

## Registration

`MigrationModule : IModule` (flag `Migration`) registers `IMigrationService` and
`ISisSourceConnector` (scoped); one line in `ModuleBootstrapper.Modules`. The
canonical identity service and `PhoenixDbContext` it builds on are core services
(always registered), not part of this module.

## MODULE_TEMPLATE checklist

- [x] Flag in `Features` (default OFF in base, ON in Phoenix-Dev)
- [x] `MigrationModule : IModule`; one line in `ModuleBootstrapper.Modules`
- [x] Services flag-guarded (registration safe while off; page guarded in markup); nav conditional
- [x] No module-owned tables — writes go through the core `Student`/`StudentIdentifier` schema
- [x] This doc + ADR-002
- [x] Architecture tests green

## Later (epic:data-migration)

Live PowerSchool connector; file/CSV upload; field-mapping UI; additional source
connectors — all behind `ISisSourceConnector`, so Phoenix's schema never changes
to accommodate a source.
