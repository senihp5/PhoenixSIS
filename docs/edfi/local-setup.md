# Ed-Fi Environment Setup — Phoenix SIS

**Scope:** the Azure environment for Ed-Fi ODS/API 7.3.x and the Phoenix app (PHX-3.2, provisioned 2026-07-22), and — as PHX-3.3 progresses — the ODS/API deployment steps. Decision record: `install-decision.md`.

## Provisioned resources (PHX-3.2 — 2026-07-22)

| Resource | Value |
|---|---|
| Phoenix app | **nh-phoenix-cus** — App Service, RG `IDCardOCR`, plan `plan-nh-cus` (S1, Central US), Windows, .NET **v10.0**, AlwaysOn, HTTPS-only, FTPS-only |
| Ed-Fi app | **nh-edfi-cus** — same plan/RG, Windows, .NET **v8.0** (ODS/API 7.3 target), AlwaysOn, HTTPS-only, FTPS-only |
| VNet integration | Both apps → `vnet-decisioned` / `subnet-appsvc` (10.10.4.0/26, RG `rg-decisioned-prod`); `WEBSITE_VNET_ROUTE_ALL=1`, `WEBSITE_DNS_SERVER=168.63.129.16` |
| SQL target | `vm-decisioned-data` — 10.10.2.4 / `sql.internal.newheightsed.com`, SQL Server 2022 Standard (private, VNet-only) |
| Domain | **phoenix.newheightsed.com** → CNAME `nh-phoenix-cus.azurewebsites.net`; `asuid.phoenix` TXT verification record; `*.newheightsed.com` wildcard cert bound (thumbprint `BB3C4410…FAAC`, source KV `kv-decisioned-prod`) |
| Ed-Fi lockdown | `nh-edfi-cus` access restrictions: Allow rule for Patrick's IP only (default deny). Phoenix app outbound IPs get added in Epic 3 when the app starts calling the API. |
| Entra | `https://phoenix.newheightsed.com/signin-oidc` added to PhoenixSIS-Dev redirect URIs (Phoenix tenant) |

## Connectivity verification (2026-07-22)

Kudu → Debug console → `tcpping 10.10.2.4:1433`:
- **nh-edfi-cus:** 4/4 successful (first hit ~763ms cold, then 31-62ms).
- **nh-phoenix-cus:** initially **0/4** ("socket forbidden") despite registered VNet integration — the worker had not picked up the integration/route-all settings. **Fix: `az webapp restart`**, fresh Kudu session → 4/4 (136-312ms). *Lesson: after adding VNet integration + route-all settings, always restart the app; a pre-existing Kudu session tests the old network stack.*
- Kudu quirk: re-pasting into the console can throw "Window title cannot be longer than 1023 characters" — cosmetic, ignore.
- Kudu can spin for 2-3 minutes after a restart while the SCM site recycles — wait it out or stop/start.

## Packaging decision (PHX-3.2 research)

Ed-Fi's Docker images for ODS/API 7.x treat **SQL Server as experimental** ("not a widely tested deployment path"; no pre-built MSSQL containers — PostgreSQL is the default). Therefore: **binary deployment of ODS/API 7.3 to the Windows App Service** against SQL Server on the VM — the well-trodden SQL Server path. (Docker Hub registry for reference: `edfialliance`; Docker release v3.2.0 targets ODS/API 7.3 + Admin API 2.2.)

## PHX-3.3 — ODS/API deployment (to be filled in as executed)

- [ ] EdFi_* databases on the VM (minimal template) + `edfi_app` login
- [ ] ODS/API binaries deployed to nh-edfi-cus; connection strings in App Service settings
- [ ] Discovery endpoint + Swagger verified
- [ ] Admin App / Admin API decision + setup
- [ ] Post-install baseline backup of EdFi_* (feeds reset-edfi.ps1)
- Walls & fixes log: *(append as encountered)*
