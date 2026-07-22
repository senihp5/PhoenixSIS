# Sprint 2: Module Architecture + Deployment Configurations

**Sprint Goal:** Establish the module pattern every future module (EdFi, AdultEd, Portal) will follow, enforce its boundaries with the repo's first test project, and create the two named deployment configurations with a visible environment indicator.

**Total Estimated Hours:** 15-19
**Dependencies:** Sprint 1 complete (feature flag service, Entra auth)
**Replaces:** original Sprints 3-4 (reworked for the fresh repo per Sprint Plan Amendment v1.1)

> **Why this sprint matters:** Sprint 3 (Ed-Fi ODS) starts Epic 2, and Sprint 5 builds the first real module. This sprint decides what "a module" *is* before any module exists — the cheapest possible moment to decide it.

---

## PHX-2.1: IModule Interface + Module Bootstrapper

**Estimate:** 3-4 hrs

### User Story
As a developer building future modules, I want an `IModule` contract and a startup bootstrapper so every module registers and initializes the same way, gated by its feature flag.

### Acceptance Criteria
- `IModule` interface in `Phoenix.Core\Modules\`: `FeatureFlagName`, `DisplayName`, `RegisterServices(IServiceCollection, IConfiguration)`, `InitializeAsync(IServiceProvider, ILogger)`
- Module discovery/registration helper in `Phoenix.Infrastructure` that Program.cs calls once: registers all modules' services, then initializes only flag-enabled modules
- Startup log line per module: "Module '{DisplayName}' registered — {ENABLED|disabled}"
- A trivial `CoreModule` (or inline sample) proves the wiring end-to-end

### Definition of Done
- [ ] Build clean, app runs identically
- [ ] Startup logs show module registration alongside the existing 8 flag lines
- [ ] Unit-testable: bootstrapper skips InitializeAsync for disabled modules

---

## PHX-2.2: Module Template + ADR-001

**Estimate:** 2-3 hrs

### User Story
As my future self scaffolding the EdFi module in Sprint 5, I want a documented module template and the architecture decision on record so new modules are mechanical, not inventive.

### Acceptance Criteria
- `docs\features\modules\MODULE_TEMPLATE.md`: folder structure (`src\Modules\{Name}\{Name}.Domain|.Data|.UI`), naming conventions, DI registration via IModule, flag-guard patterns for services/pages/nav (reference the FeatureFlags page + NavMenu AuthorizeView patterns already in the repo)
- `docs\adr\ADR-001-modular-monolith.md`: modular monolith over microservices/fork, fresh-repo context, clean-room policy (no CMS code ports)

### Definition of Done
- [ ] Both documents complete
- [ ] Template's boundary rules match what PHX-2.3 enforces

---

## PHX-2.3: Architecture Test Project (Boundary Enforcement)

**Estimate:** 3-4 hrs

### User Story
As a developer preventing architectural drift, I want automated tests that fail when a module references another module's internals, so boundaries survive solo-dev shortcuts.

### Acceptance Criteria
- New project `tests\Phoenix.ArchitectureTests` (xUnit + NetArchTest.Rules), added to solution — the repo's first test project
- Rules: Core references no other Phoenix project; Infrastructure references only Core; future `Phoenix.Modules.*` assemblies must not reference other modules' non-public namespaces (rule written now, activates as modules appear)
- `dotnet test` passes locally

### Definition of Done
- [ ] Tests green on `dotnet test`
- [ ] MODULE_TEMPLATE.md references the tests as the enforcement mechanism
- [ ] Resolves the deferred "test project: add now or later?" decision — answered: now, via architecture tests

---

## PHX-2.4: Deployment Configurations (Phoenix-Dev, Phoenix-Demo)

**Estimate:** 3-4 hrs

### User Story
As a developer, I want two named deployment configurations so flag combinations are reproducible and I always know which environment I'm running.

### Acceptance Criteria
- `appsettings.Phoenix-Dev.json` and `appsettings.Phoenix-Demo.json` in Phoenix.Web with a `Deployment` section (`Name`, `Description`) and full `Features` blocks (Demo mirrors Dev until demo data exists)
- `launchSettings.json` profiles "Phoenix-Dev" and "Phoenix-Demo" setting `ASPNETCORE_ENVIRONMENT` accordingly; both work with `dotnet run --launch-profile` and VS F5
- Entra redirect URIs still valid for both profiles (same localhost ports)
- `docs\deployment\configurations.md` explains each config's purpose and how layering works (base appsettings.json + environment overlay)

### Definition of Done
- [ ] Both profiles boot and log their Deployment:Name
- [ ] Flag differences between configs verified via the dashboard
- [ ] Docs complete

---

## PHX-2.5: Environment Banner + Sprint Close

**Estimate:** 3-4 hrs

### User Story
As the operator, I want the running deployment visibly identified in the UI so I can never confuse Dev with Demo (and later, Demo with anything customer-facing).

### Acceptance Criteria
- Banner/badge in MainLayout top bar showing `Deployment:Name` (subtle for Phoenix-Dev, distinct color for Phoenix-Demo); also shown on `/admin/feature-flags` and in the browser title
- Sprint close per operating model: `docs\retros\sprint-02.md` (5-question template), 3-minute Loom (module bootstrapper logs → architecture tests running → config switch with banner change → dashboard)

### Definition of Done
- [ ] Banner correct in both profiles
- [ ] Loom recorded and linked
- [ ] Retro written, Actual Hours logged in GitHub project
- [ ] Git tag `phoenix-sprint-02` pushed
- [ ] Sprint 3 (Ed-Fi ODS local deployment) goal + stories drafted
