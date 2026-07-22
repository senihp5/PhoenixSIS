# Phoenix SIS

A purpose-built Student Information System for Adult High School programs.

Traditional K-12 SIS platforms assume a traditional student. Adult learners bring different realities — work schedules, family obligations, life barriers, interrupted transcripts, non-linear paths to graduation. Phoenix SIS is designed around those realities from the ground up: an Ed-Fi ODS core for standards compliance and state reporting, with purpose-built extensions for the adult education mission.

## Architecture

Modular monolith: a single codebase where every major capability is a feature-flagged module. One repo, multiple deployment configurations — a deployment enables only the modules it needs.

```
PhoenixSIS.sln
└── src\
    ├── Phoenix.Core\            Interfaces, domain contracts, configuration types
    ├── Phoenix.Infrastructure\  Service implementations, external integrations
    └── Phoenix.Web\             Blazor web app (Server interactivity), DI composition root
```

Conventions: interfaces in `Phoenix.Core`, implementations in `Phoenix.Infrastructure`, dependency injection registered in `Phoenix.Web\Program.cs`.

### Feature flags

Modules are gated by the `Features` section of `appsettings.json`, read at startup by `IFeatureFlagService` (all flag states are logged on boot). Current modules: Advising, PowerSchool, EdFi, AdultEd, Portal, Reports, IdCard, TimeClock. Admins can view flag states at `/admin/feature-flags`.

### Authentication and authorization

Microsoft Entra ID (dedicated Phoenix SIS tenant), OpenID Connect via `Microsoft.Identity.Web`. Authorization is group-based: the token's `groups` claim carries Entra security-group Object IDs, and policies (e.g. `RequireAdmin`) check group GUIDs from the `AzureAd:Groups` config section. All pages require sign-in by default (fallback policy); anonymous access is opt-in per endpoint.

## Development setup

Prerequisites: .NET 10 SDK (the solution targets net10.0, the current LTS), Visual Studio 2022 (or the dotnet CLI), and access to the Phoenix SIS Entra tenant.

1. Clone the repo.

2. Store the Entra client secret in user secrets (never in appsettings.json):

```powershell
dotnet user-secrets set "AzureAd:ClientSecret" "<secret-value>" --project src\Phoenix.Web
```

3. Trust the dev certificate if you haven't before:

```powershell
dotnet dev-certs https --trust
```

4. Run with the https profile (the Entra redirect URI is registered for `https://localhost:7091`):

```powershell
dotnet run --project src\Phoenix.Web --launch-profile https
```

5. Browse to https://localhost:7091 and sign in with a Phoenix SIS tenant account. Admin pages require membership in the `PhoenixSIS.Admin` security group.

## Documentation

Design and planning documents live in `docs\`: `01-Session-Handoffs`, `02-Strategy` (design conversation, stakeholder questionnaires, CMS fit assessment), `03-Architecture` (hybrid architecture design), `04-Planning` (sprint plan, story cards), `05-Execution` (Ed-Fi deployment guide, setup guides), and `retros` (sprint retrospectives).

## Project management

Work is tracked on the GitHub Project "Phoenix SIS - Solo Dev" as PHX-N.N stories grouped into sprints. Each sprint ends with a demo recording, a retrospective in `docs\retros\sprint-NN.md`, and a `phoenix-sprint-NN` git tag.

## Roadmap

| Epic | Focus |
|---|---|
| 1. Modular Refactor | Feature flags, module boundaries, deployment configs |
| 2. Ed-Fi ODS Setup | Local Ed-Fi ODS/API install, Texas descriptors |
| 3. EdFi Module + Sync | Ed-Fi client, student sync, data quality dashboard |
| 4. AdultEd Module | Adult education extensions (barriers, life circumstances) |
| 5. Portal MVP | Student portal — schedule, progress, attendance, messaging |
| 6. Investor Prototype | Polish, demo data, pitch materials |
