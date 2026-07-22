# Module Template

How to build a Phoenix SIS module. Every module — EdFi, AdultEd, Portal, and anything after — follows this template. If a module deviates, the deviation goes in an ADR.

## What a module is

A feature-flagged, self-registering unit of functionality with a hard public boundary. A deployment turns modules on and off in configuration (`Features` section); the code never assumes a module is present.

## Folder structure

Small modules start as folders inside the existing projects. A module graduates to its own projects only when size demands it (rule of thumb: >15 files or its own EF migrations).

**Phase 1 — folders (default for new modules):**

```
src\Phoenix.Core\Modules\{Name}\          contracts/DTOs other modules may use
src\Phoenix.Infrastructure\{Name}\        services, clients, data access
src\Phoenix.Web\Components\Pages\{Name}\  Blazor pages
```

**Phase 2 — dedicated projects (when graduated):**

```
src\Modules\{Name}\
├── Phoenix.Modules.{Name}.Domain\    entities, value objects, domain services, {Name}Module.cs
├── Phoenix.Modules.{Name}.Data\      EF entities, DbContext (or extension), migrations
└── Phoenix.Modules.{Name}.UI\        Blazor pages + components
```

## Naming conventions

- Feature flag key = module name, PascalCase, matching the `Features` section: `EdFi`, `AdultEd`, `Portal`.
- Module class: `{Name}Module` implementing `IModule` (`Phoenix.Core.Modules.IModule`).
- DB tables owned by a module carry its prefix: `AdultEd_StudentBarrier`, `Portal_Message`, `EdFi_SyncLog`.
- Docs: `docs\features\modules\{Name}.md` — one per module, written the sprint the module is born.

## Registration (IModule)

Modules never register services directly in Program.cs. Each implements `IModule`:

- `FeatureFlagName` — the `Features` key gating it (empty string = always-on, Core only).
- `RegisterServices(services, configuration)` — all DI registrations. Runs for every module at startup regardless of flag state, so registrations must be safe while disabled.
- `InitializeAsync(services, logger)` — startup work (cache warm, migration check). Runs only when the flag is enabled.

Then one line in `ModuleBootstrapper.Modules` (in `Phoenix.Infrastructure\Modules\ModuleBootstrapper.cs`). That's the entire integration surface with the host.

## Flag-guard patterns

**Service entry points** — guard at the top of the public method:

```csharp
if (!_features.IsEnabled("EdFi"))
{
    _logger.LogInformation("EdFi module disabled - sync skipped");
    return SyncResult.Skipped("Module disabled");
}
```

**Blazor pages** — guard in markup; page shows a friendly message, never crashes:

```razor
@if (!FeatureFlagService.IsEnabled("EdFi"))
{
    <div class="alert alert-info">
        <h4>Ed-Fi Integration Not Enabled</h4>
        <p>This feature requires the EdFi module.</p>
    </div>
}
else
{
    @* page content *@
}
```

**Navigation** — the link doesn't render when the flag is off (see the `AuthorizeView`-wrapped Feature Flags link in `NavMenu.razor` for the combined auth + flag pattern):

```razor
@if (FeatureFlagService.IsEnabled("EdFi"))
{
    <NavLink class="nav-link" href="admin/edfi/health">Ed-Fi Health</NavLink>
}
```

**Background/hosted services** — gate at registration inside `RegisterServices` AND at execution, so a stale registration can never run work for a disabled module.

## Boundary rules (enforced by tests\Phoenix.ArchitectureTests)

- A module may reference: `Phoenix.Core` (including other modules' contracts placed there), `Phoenix.Infrastructure` shared services.
- A module may NOT reference another module's internals — its services, data access, or UI. Cross-module needs go through a contract in `Phoenix.Core`.
- `Phoenix.Core` references no other Phoenix project. `Phoenix.Infrastructure` references only `Phoenix.Core`.
- Violations fail `dotnet test` (NetArchTest rules) — fix the dependency, don't loosen the rule.

## Clean-room policy

CMS (CaseManagementSystem) code is never copied into this repo. Concepts, lessons, and UI ideas carry over; source code does not. See ADR-001.

## Checklist for a new module

- [ ] Flag added to `Features` in appsettings.json (default OFF)
- [ ] `{Name}Module : IModule` created; one line added to `ModuleBootstrapper.Modules`
- [ ] Services flag-guarded at entry points; pages guarded in markup; nav conditional
- [ ] Tables prefixed `{Name}_`
- [ ] `docs\features\modules\{Name}.md` written
- [ ] Architecture tests still green
