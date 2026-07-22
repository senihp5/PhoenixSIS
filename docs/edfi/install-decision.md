# Ed-Fi Install Decision — PHX-3.1

**Date:** 2026-07-22 (CST)
**Status:** Decided

## Current Ed-Fi landscape (verified 2026-07-22)

- **Ed-Fi API v8.0** (Data Management Service platform) released **2026-07-15** — complete platform redesign, Docker-first, PostgreSQL + SQL Server (SQL Server gaps until v8.1). Full replacement of ODS/API planned by school year 2029-30.
- **ODS/API 7.3.x** — the stable workhorse: fully supported **through at least SY 2028-29**, annual enhancements, .NET 10 upgrade planned in 2026, Data Standard 4/5 (6 planned).
- Ed-Fi ODS Docker release **v3.2.0** (Jan 2026) targets ODS/API 7.3 + Admin API 2.2.
- Sources: docs.ed-fi.org v8.0 announcement (2026-07-15), ODS/API-vs-DMS FAQ, Docker deployment docs, Ed-Fi-ODS-Docker releases.

## Decisions

| # | Decision | Choice |
|---|---|---|
| 1 | **Platform** | **ODS/API 7.3.x** — stable, supported past prototype timeline, mature docs, Texas ecosystem alignment. v8 revisit queued (see below). |
| 2 | **Databases** | **EdFi_* databases on the DecisionED SQL VM** (`vm-decisioned-data`, SQL Server 2022 Standard, private VNet) — local machine lacks headroom for SQL; the VM is already licensed and running. |
| 3 | **ODS/API host** | **Azure App Service** (VNet-integrated to reach the VM) — web/data separation mirroring the CMS pattern. Exact packaging (container image vs binary deploy) verified in PHX-3.2/3.3. |
| 4 | **Phoenix app host** | **Azure App Service on the existing `plan-nh-cus` (S1, Central US)** — zero added cost. Domain: **phoenix.newheightsed.com** (covered by the `*.newheightsed.com` wildcard cert). |
| 5 | **Hosting posture** | Phoenix runs on New Heights infrastructure as the **New Heights pilot / design-partner instance**. Code, repo, and identity remain clean-room (PhoenixSIS repo, Phoenix Entra tenant). Written IP clarification with New Heights: deferred by Patrick's decision — remains on the risk register at Very High. |

## Superseded

- Sprint 3's original "Docker Compose on the dev machine" path — dropped for processing-power reasons. Docker artifacts (images/compose) may still be *used* on App Service if container deploy proves cleanest.
- The "everything local, zero cloud spend through Sprint 13" infrastructure phase of the v1.0 plan — Phoenix dev now runs against shared NH infrastructure from Sprint 3 onward.

## v8 watch item (backlog)

Revisit Ed-Fi API v8 when **v8.1** closes the SQL Server gaps, and **before Epic 3's client code hardens**. The REST surface carries over; the evaluation is a 1-story spike.

## Security notes (bind PHX-3.2/3.3 to these)

- The Ed-Fi ODS/API App Service must **not** be publicly reachable: App Service access restrictions (allow Patrick's IP + the Phoenix app's outbound) at minimum; private endpoint if warranted later.
- Ed-Fi client keys/secrets and SQL connection strings live in App Service settings — never in the repo.
- The SQL VM stays private (no public endpoint); all access via VNet integration through `subnet-appsvc`.
- Operational note: the VM is also the future CMS production DB home + Cognos — watch its headroom as EdFi_* databases and sync loads land.

## Consequences for Sprint 3 cards

- PHX-3.2 becomes Azure provisioning (App Services, VNet integration, DNS, cert) instead of Docker Desktop install.
- PHX-3.3 becomes ODS/API deployment to App Service + EdFi_* database deployment to the VM.
- PHX-3.6's reset script becomes a SQL restore script on the VM (restore EdFi_Ods to post-install state) instead of a container/volume reset.
- PHX-3.4/3.5 (OAuth, student round-trip) unchanged — just pointed at the App Service URL.
