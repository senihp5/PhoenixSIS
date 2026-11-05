# EdFi Module

**Flag:** `EdFi` (in `Features`, default OFF in base `appsettings.json`; ON in the `Phoenix-Dev` overlay)
**Born:** Sprint 4 (PHX-4.1–4.5), 2026-11-05
**Epic:** Epic 3 — EdFi Integration Module

## Purpose

Lets the Phoenix application talk to the Ed-Fi ODS/API. Sprint 4 delivers the
scaffold, a typed OAuth-authenticating API client, and two admin pages that
read from the connected ODS. Writing data back (student sync) comes in a later
sprint.

## Shape (follows MODULE_TEMPLATE, Phase 1 — folders)

```
src\Phoenix.Core\Modules\EdFi\        contracts: IEdFiApiClient, EdFiApiInfo, EdFiStudentSummary
src\Phoenix.Infrastructure\EdFi\      EdFiModule, EdFiApiOptions, EdFiApiClient
src\Phoenix.Web\Components\Pages\EdFi\ Health.razor, Students.razor
```

`EdFiModule : IModule` registers the client; one line in `ModuleBootstrapper.Modules`.

## Configuration

Two separate things — don't confuse them:

- **`Features:EdFi`** — the feature flag that gates the module.
- **`EdFiApi`** — the API client's connection settings:

```json
"EdFiApi": {
  "BaseUrl": "https://nh-edfi-cus.azurewebsites.net"
}
```

`BaseUrl` lives in `appsettings.json`. **Key and Secret are secrets** and live in
dotnet user-secrets locally (App Service settings / Key Vault when deployed) —
never in the repo:

```
dotnet user-secrets set "EdFiApi:Key" <sandbox-key>      --project src\Phoenix.Web
dotnet user-secrets set "EdFiApi:Secret" <sandbox-secret> --project src\Phoenix.Web
```

The sandbox key/secret are produced by `_tools\New-EdfiSandbox.ps1` into
`C:\temp\edfi-sandbox.ps1`.

## API client (`IEdFiApiClient`)

- `GetApiInfoAsync()` — reads the anonymous discovery endpoint (version, build,
  data models). Proves reachability.
- `GetAccessTokenAsync()` — OAuth client-credentials; caches the bearer token
  until ~60s before expiry (thread-safe). Proves authentication.
- `GetStudentsAsync(limit)` — bearer-authenticated read of `/data/v3/ed-fi/students`.

Registered as a **singleton** so the token cache persists; pulls a fresh
`HttpClient` from `IHttpClientFactory` per call. Every method is guarded by
`IFeatureFlagService.RequireEnabled("EdFi")`.

## Pages

- **`admin/edfi/health`** — reachability + auth status cards. Never throws on an
  unreachable API; shows a red status instead.
- **`admin/edfi/students`** — first 25 students from the ODS.

Both require the `RequireAdmin` policy, are flag-guarded in markup (friendly
message when OFF), and render interactively (`InteractiveServer`). Nav links
appear only for admins **and** only when the flag is on.

## Relationship-based authorization

With the `Ed-Fi Sandbox` claim set, a student is only visible once a
`studentSchoolAssociation` links them to a school under the API client's
education organization (LEA 255901). This is why a freshly created student is
unreadable until associated — see the PHX-3.5 lesson in `docs\edfi\local-setup.md`.

## Guardrail

`nh-edfi-cus` is reachable on the public internet behind OAuth only until the
on-site IP lockdown (hardening list). **Use sample data only** — no real student
data through this module until the lockdown is in place.

## MODULE_TEMPLATE checklist

- [x] Flag in `Features` (default OFF in base)
- [x] `EdFiModule : IModule`; one line in `ModuleBootstrapper.Modules`
- [x] Services flag-guarded at entry points; pages guarded in markup; nav conditional
- [x] No module-owned DB tables yet (read-only integration this sprint)
- [x] This doc
- [x] Architecture tests green
