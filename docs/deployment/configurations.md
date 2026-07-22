# Deployment Configurations

How Phoenix SIS selects which named configuration it runs.

## The two configurations

| Config | Purpose | Where | Flags |
|---|---|---|---|
| **Phoenix-Dev** | Daily development | Local machine, local SQL/Ed-Fi | EdFi, AdultEd, Portal ON; CMS-parity modules OFF |
| **Phoenix-Demo** | Investor demos (Epic 6) | Azure (deferred until needed) | Mirrors Phoenix-Dev until demo data lands |

## How selection works

The `PHOENIX_DEPLOYMENT` environment variable names the configuration. At startup, Program.cs layers `appsettings.{PHOENIX_DEPLOYMENT}.json` on top of the base `appsettings.json`. If the variable is unset, **Phoenix-Dev is the default** — a bare `dotnet run` is always a dev run.

Config layering order (later wins): `appsettings.json` → user secrets (Development only) → environment variables → `appsettings.{Deployment}.json`.

### Why not ASPNETCORE_ENVIRONMENT?

ASP.NET loads **user secrets only when the environment is `Development`** — and the Entra client secret lives in user secrets. Using a custom variable keeps every local profile in the `Development` environment (secrets, detailed errors) while still selecting a named flag set. When Phoenix-Demo deploys to Azure, its App Service sets `ASPNETCORE_ENVIRONMENT=Production` *and* `PHOENIX_DEPLOYMENT=Phoenix-Demo`, with the client secret in App Service settings/Key Vault instead of user secrets.

## Running a configuration

Visual Studio: pick the **Phoenix-Dev** or **Phoenix-Demo** profile from the run dropdown (F5). CLI:

```powershell
dotnet run --project src\Phoenix.Web --launch-profile Phoenix-Dev
```

```powershell
dotnet run --project src\Phoenix.Web --launch-profile Phoenix-Demo
```

The startup log announces `Deployment configuration: {Name}`, and the UI shows the deployment banner (PHX-2.5). Verify flag differences at `/admin/feature-flags`.

## Adding a configuration

1. Create `appsettings.{Name}.json` with a `Deployment` section (`Name`, `Description`) and a full `Features` block.
2. Add a launch profile in `Properties\launchSettings.json` setting `PHOENIX_DEPLOYMENT={Name}`.
3. Document it here.

## Rules

- Flags in overlays are **explicit and complete** — every module listed in every config, no silent inheritance surprises.
- Secrets never go in any appsettings file — user secrets locally, App Service settings/Key Vault in Azure.
- The base `appsettings.json` keeps conservative defaults; overlays express intent per environment.
