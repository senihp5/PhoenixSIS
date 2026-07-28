# Ed-Fi Environment Setup — Phoenix SIS

**Scope:** the Azure environment for Ed-Fi ODS/API 7.3.x and the Phoenix app (PHX-3.2, provisioned 2026-07-22), and the ODS/API deployment (PHX-3.3, in progress). Decision record: `install-decision.md`.

## Provisioned resources (PHX-3.2 — 2026-07-22)

| Resource | Value |
|---|---|
| Phoenix app | **nh-phoenix-cus** — App Service, RG `IDCardOCR`, plan `plan-nh-cus` (S1, Central US), Windows, .NET **v10.0**, AlwaysOn, HTTPS-only, FTPS-only |
| Ed-Fi app | **nh-edfi-cus** — same plan/RG, Windows, .NET **v8.0** (ODS/API 7.3 target), AlwaysOn, HTTPS-only, FTPS-only |
| VNet integration | Both apps → `vnet-decisioned` / `subnet-appsvc` (10.10.4.0/26, RG `rg-decisioned-prod`); `WEBSITE_VNET_ROUTE_ALL=1`, `WEBSITE_DNS_SERVER=168.63.129.16` |
| SQL target | `vm-decisioned-data` — 10.10.2.4 / `sql.internal.newheightsed.com`, SQL Server 2022 Standard (private, VNet-only), Mixed Mode confirmed (LoginMode=2) |
| Domain | **phoenix.newheightsed.com** → CNAME `nh-phoenix-cus.azurewebsites.net`; `asuid.phoenix` TXT; `*.newheightsed.com` wildcard cert bound (thumbprint `BB3C4410…FAAC`) |
| Ed-Fi lockdown | `nh-edfi-cus` access restrictions: Allow Patrick's IP only (default deny). Phoenix app outbound IPs added in Epic 3. |
| Entra | `https://phoenix.newheightsed.com/signin-oidc` added to PhoenixSIS-Dev redirect URIs (Phoenix tenant) |

## Connectivity verification (2026-07-22)

Kudu `tcpping 10.10.2.4:1433`: nh-edfi-cus 4/4 immediately; nh-phoenix-cus 0/4 ("socket forbidden") until **`az webapp restart`** + fresh Kudu session → 4/4. *Lesson: restart the app after adding VNet integration + route-all; old Kudu sessions test the old network stack.* Kudu can spin 2-3 min after restart (SCM recycle); console re-paste throws a cosmetic "Window title" error — ignore.

## Packaging decision (PHX-3.2 research)

Ed-Fi's Docker images treat SQL Server as **experimental** (no pre-built MSSQL containers). Therefore: **binary deployment** — ODS/API 7.3 binaries to the Windows App Service against SQL Server on the VM. App Service **virtual applications** (Path mappings: `/WebApi`, `/SwaggerUI`, `/SandboxAdmin`) will mirror the IIS layout the official installers produce.

## PHX-3.3 Phase A — EdFi_* databases on the VM ✅ (2026-07-22)

**Result:** `EdFi_Admin`, `EdFi_Security`, `EdFi_Ods_Minimal_Template`, `EdFi_Ods_Populated_Template` deployed on `vm-decisioned-data` via `EdFi.Suite3.RestApi.Databases.Standard.6.1.0` v7.3.20297 (`Initialize-DeploymentEnvironment`, InstallType=Sandbox, Engine=SQLServer, Data Standard 6.1). Total run 19.7s once unblocked.

**Procedure (as executed):** RDP to VM → download `EdFi.Suite3.RestApi.Databases.Standard.6.1.0` nupkg from the docs Package Links → rename .zip, Unblock, extract to `C:\Temp\...` → delete `configuration.postgreSQL.json` → edit `configuration.json` (see walls below for the final connection-string shape) → admin PowerShell: TLS12 line, `Import-Module .\Deployment.psm1`, `Initialize-DeploymentEnvironment`.

### Walls hit + fixes (the valuable part)

1. **".NET Core SDK 10.0+ required"** — the 7.3.20297 scripts require the .NET 10 SDK (the roadmap's 2026 .NET 10 upgrade has landed). Fix: `dotnet-install.ps1 -Channel 10.0 -InstallDir "C:\Program Files\dotnet"`, new shell. On a fresh server this is wall #1.
2. **Db.Deploy exit -1, no visible cause** — the real error prints ABOVE the ToolsHelper throw; always scroll up.
3. **Modern SqlClient encrypts by default** — add `Encrypt=false` to every connection string for a local instance without a trusted cert (the docs only mention `UnEncryptedConnection` for the WebApi installer; the DB config needs it manually).
4. **`Login failed for user 'AzureAD\...'`** — `trusted_connection=True` uses the RDP identity, which had no SQL login. Then **error 233** with `decisioned-admin` (not a valid SQL login — Windows account name ≠ SQL login). Fix: created SQL login **`edfi_app`** (temporarily sysadmin), used it in ALL FOUR connection strings — including **`EdFi_Ods`**, which is not in the shipped config template but is used for the template restores (`database=EdFi_{0}` — keep the `{0}`).
5. **Invalid JSON after editing** — missing comma between EdFi_Master and the added EdFi_Ods line; the tool then silently falls back to default settings (trusted_connection everywhere), which is confusing. Validate the JSON after editing.

### Post-Phase-A tasks

- [ ] Baseline backups of the four EdFi_* databases (→ feeds `scripts\reset-edfi.ps1`)
- [ ] After WebApi runs: downgrade `edfi_app` from sysadmin to db_owner on EdFi_* only
- [ ] After sprint: delete `C:\Temp\EdFi.*` (contains the edfi_app password in configuration.json); rotate exposed passwords (edfi_app, decisioned-admin appeared in console/chat)

## PHX-3.3 Phase B — WebApi to nh-edfi-cus (next)

- [ ] Obtain `EdFi.Suite3.Ods.WebApi.Standard.6.1.0` v7.3.20297 from the EdFi Azure Artifacts feed
- [ ] App Service settings: connection strings → `10.10.2.4` as `edfi_app`, sandbox mode config
- [ ] Zip deploy; discovery endpoint + Swagger verify
- Walls & fixes log: *(append as encountered)*
