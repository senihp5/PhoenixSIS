# Sprint 4: EdFi Module Scaffold + API Client

**Sprint Goal:** The Phoenix application talks to the live Ed-Fi API for the first time. A flag-gated `EdFi` module (following MODULE_TEMPLATE) with a typed, OAuth-authenticating API client, surfaced in an admin health page that reads students from the sandbox. All local dev — no on-site-hardening dependency, sample data only.

**Total Estimated Hours:** 12-18
**Epic:** Epic 3 — EdFi Integration Module (`epic:edfi-module`)
**Sequencing note (2026-11-05):** pulled forward from the amendment's Sprint 5 because our executed Sprint 3 already delivered the ODS + API + sample-loaded sandbox (amendment S3+S4 scope). Texas descriptors (amendment S4) reslotted to before the first real sync — see Amendment v1.2 addendum.

**Guardrails:** sample data only until nh-edfi-cus is IP-locked (hardening list). CMS code is never copied (ADR-001). `main` always builds and runs; incomplete work stays behind the `EdFi` flag (default OFF).

---

## PHX-4.1: EdFi module scaffold

**Estimate:** 2-3 hrs

### Story
As the developer, I want an `EdFiModule : IModule` registered and flag-gated per MODULE_TEMPLATE, so the Ed-Fi integration has a home that is invisible until the `EdFi` flag is on.

### Tasks
- [ ] Create `src\Phoenix.Infrastructure\EdFi\EdFiModule.cs` implementing `Phoenix.Core.Modules.IModule`: `FeatureFlagName => "EdFi"`, `DisplayName => "Ed-Fi Integration"`, empty-safe `RegisterServices`, `InitializeAsync` logging a one-line "Ed-Fi module initialized" (no network calls at init)
- [ ] Add `new EdFiModule()` to `ModuleBootstrapper.Modules` (one line, after CoreModule)
- [ ] Create the contracts folder `src\Phoenix.Core\Modules\EdFi\` (empty placeholder or the first DTO from 4.4)
- [ ] Confirm `Features:EdFi` already exists in appsettings.json (it does, default OFF) — leave OFF
- [ ] `EdFi` flag OFF → startup logs `Module 'Ed-Fi Integration' registered — disabled`; flip ON locally → logs `ENABLED` + the init line

### Definition of Done
- [ ] `dotnet build` + `dotnet test` green (architecture boundary tests still pass — module references only Core/Infrastructure)
- [ ] Startup log shows correct flag state both ways
- [ ] No behavior change with the flag OFF

---

## PHX-4.2: Typed Ed-Fi API client + OAuth token caching

**Estimate:** 4-6 hrs

### Story
As the developer, I want a typed client that authenticates to nh-edfi-cus with OAuth client-credentials and caches the bearer token, so module code calls the Ed-Fi API without handling auth each time.

### Tasks
- [ ] `EdFiApiOptions` (BaseUrl, Key, Secret) bound from an **`EdFiApi`** config section — kept distinct from the `Features:EdFi` flag. BaseUrl in appsettings (`https://nh-edfi-cus.azurewebsites.net`); **Key/Secret in dotnet user-secrets**, never in the repo
- [ ] `IEdFiApiClient` (in `Phoenix.Core\Modules\EdFi\`) + `EdFiApiClient` (in `Phoenix.Infrastructure\EdFi\`): registered via `IHttpClientFactory`; `GetAccessTokenAsync` posts client-credentials to `/oauth/token` and caches the token until ~60s before `expires_in`; a `GetApiInfoAsync` hitting the discovery endpoint (`/`) returning version/build
- [ ] Register the client + options inside `EdFiModule.RegisterServices` (safe when disabled — registration only, no calls)
- [ ] Flag-guard any public entry point per MODULE_TEMPLATE (log + no-op when disabled)
- [ ] A small unit test for token-cache reuse (no real network — fake the token response)

### Definition of Done
- [ ] With the flag ON and user-secrets set, a dev-only smoke (temporary endpoint or test) retrieves a token and the discovery version
- [ ] Token is reused within its lifetime, refreshed after expiry
- [ ] Secrets confirmed absent from committed files (`git grep` for the key value finds nothing)

---

## PHX-4.3: Ed-Fi health admin page

**Estimate:** 2-3 hrs

### Story
As an admin, I want an "Ed-Fi Health" page that shows whether the API is reachable and authenticating, so I can confirm the integration at a glance.

### Tasks
- [ ] `src\Phoenix.Web\Components\Pages\EdFi\Health.razor` at route `admin/edfi/health`; `[Authorize(Policy = "RequireAdmin")]`
- [ ] Flag-guard in markup (friendly "Ed-Fi Integration Not Enabled" when OFF, per template); content calls `IEdFiApiClient.GetApiInfoAsync` and shows version/build/dataModels + token-acquisition OK/fail with the error if it fails
- [ ] Conditional nav link in `NavMenu.razor` (admin + `EdFi` flag on), matching the existing Feature Flags link pattern
- [ ] Never throws when the API is unreachable — shows a red status card instead

### Definition of Done
- [ ] Flag OFF → friendly message, no nav link; flag ON + reachable → green with version; flag ON + unreachable → red status, no crash
- [ ] `dotnet build` + `dotnet test` green

---

## PHX-4.4: Student read-through to the page

**Estimate:** 3-4 hrs

### Story
As an admin, I want the Ed-Fi page to list students pulled live from the sandbox, proving the Phoenix app reads real Ed-Fi data end to end.

### Tasks
- [ ] `EdFiStudentSummary` DTO in `Phoenix.Core\Modules\EdFi\` (studentUniqueId, firstName, lastSurname, birthDate) — a contract, not the raw Ed-Fi resource
- [ ] `IEdFiApiClient.GetStudentsAsync(int limit)` → maps `/data/v3/ed-fi/students` to the DTO
- [ ] Section on the Health page (or a sibling `admin/edfi/students`) listing the first N students in a table; loading + empty + error states
- [ ] Note in code/docs: read uses the sandbox key/secret; relationship-based auth means only associated students are visible (lesson from PHX-3.5)

### Definition of Done
- [ ] Flag ON + configured → page lists sandbox sample students
- [ ] No secrets in markup or committed config
- [ ] Still green: build + test

---

## PHX-4.5: Module docs + sprint close

**Estimate:** 1-2 hrs

### Story
As the developer, I want the EdFi module documented and the sprint closed, so the next sprint (student identity model) starts clean.

### Tasks
- [ ] `docs\features\modules\EdFi.md`: purpose, the `EdFiApi` config/secrets setup (user-secrets keys), the health/students pages, flag behavior, and the "sample data only until IP lockdown" guardrail
- [ ] Run the MODULE_TEMPLATE new-module checklist and tick it off
- [ ] Retro `docs\retros\sprint-04.md` (5 questions)
- [ ] Tag `phoenix-sprint-04`; update the project status doc

### Definition of Done
- [ ] Module doc complete; checklist all ticked
- [ ] `main` builds, runs, tests green with the flag both ON and OFF
- [ ] Sprint tagged
