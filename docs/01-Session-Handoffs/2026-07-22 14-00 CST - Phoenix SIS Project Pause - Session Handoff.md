# 2026-07-22 14-00 CST — Phoenix SIS Project Pause — Session Handoff

**Status: PROJECT PAUSED by Patrick's decision, 2026-07-22.** Azure resources torn down (see §6). Code, docs, GitHub repo, and the Phoenix Entra tenant survive. This document is the single re-entry point.

---

## 1. What Phoenix SIS is and how it evolved

A purpose-built Student Information System for Adult High School students (New Heights). Original plan (2026-07-13/14): evolve the CMS codebase into a modular monolith serving both CMS-Prod and Phoenix. That premise was **superseded 2026-07-17** by the clean-room decision: Phoenix lives in its own repo (`senihp5/PhoenixSIS`, private) with its own Entra tenant, no CMS code ever copied in (IP separation from employer-owned CMS). Sprint Plan Amendment v1.1 (2026-07-21) re-based the roadmap on that reality, cutting ~2 sprints of now-moot CMS-protection work.

## 2. State at pause — what is DONE and verified

**Sprint 1 ✅ (tagged `phoenix-sprint-01`):** solution scaffold (Phoenix.Core / Phoenix.Infrastructure / Phoenix.Web, Blazor Server interactivity), `Features` config (8 modules), `IFeatureFlagService` + startup logging, **Entra ID auth** in the dedicated Phoenix tenant (group-GUID RBA, `RequireAdmin` on PhoenixSIS.Admin, fallback policy = auth everywhere), `/admin/feature-flags` dashboard behind RequireAdmin, README + sprint-01 retro scaffold. **.NET 10 LTS** (upgraded 2026-07-21; 8/9 EOL 2026-11-10).

**Sprint 2 ✅ (tagged `phoenix-sprint-02`):** `IModule` contract + `ModuleBootstrapper` (explicit registry, flag-gated init) + CoreModule sample; `MODULE_TEMPLATE.md` + `ADR-001-modular-monolith.md`; **first test project** `tests\Phoenix.ArchitectureTests` (NetArchTest boundary rules, `dotnet test` green); `Phoenix-Dev`/`Phoenix-Demo` deployment configs via **`PHOENIX_DEPLOYMENT` overlay** (deliberately NOT ASPNETCORE_ENVIRONMENT, so user-secrets keep loading); deployment banner in UI + `/admin/feature-flags` + title.

**Sprint 3 partial:**
- PHX-3.1 ✅ — Ed-Fi platform decision: **ODS/API 7.3.x** (v8.0/DMS released 2026-07-15, too new; 7.3 supported thru SY 2028-29; v8 spike queued for 8.1). `docs\edfi\install-decision.md`.
- PHX-3.2 ✅ — Azure infrastructure provisioned (now torn down, §6): nh-phoenix-cus + nh-edfi-cus App Services on plan-nh-cus, VNet-integrated to the SQL VM (tcpping 4/4 both), phoenix.newheightsed.com + wildcard cert, Ed-Fi app IP-locked, Entra redirect URI added.
- PHX-3.3 **Phase A ✅** — EdFi_Admin, EdFi_Security, EdFi_Ods_Minimal_Template, EdFi_Ods_Populated_Template deployed on `vm-decisioned-data` via `EdFi.Suite3.RestApi.Databases.Standard.6.1.0` v7.3.20297 (Data Standard 6.1, InstallType Sandbox). **Five walls + fixes documented in `docs\edfi\local-setup.md`** (.NET 10 SDK requirement; scroll-up-for-real-error; Encrypt=false; SQL-auth login `edfi_app` needed incl. the undocumented EdFi_Ods `{0}` connection string; JSON comma). Databases dropped at teardown — recreation is a ~20-minute clean run now that the walls are documented.
- PHX-3.3 Phase B ⬜ — WebApi deploy to App Service: NOT started (was next: download `EdFi.Suite3.Ods.WebApi.Standard.6.1.0` v7.3.20297 from the EdFi Azure Artifacts feed, inspect appsettings, App Service settings, zip deploy). PHX-3.4-3.6 not started. **Deferred item:** EdFi_* baseline backups + SIMPLE recovery + nightly-Backup-to-URL scope check (moot after teardown; becomes part of Phase A on resume).

## 3. Locked decisions (do not re-litigate on resume)

1. **Clean-room repo** `F:\Github\PhoenixSIS` / `senihp5/PhoenixSIS`; CMS code never ported. `main` only branch; incomplete work behind flags; main always builds; tag `phoenix-sprint-NN` per sprint.
2. **Dedicated Entra tenant** `phoenixsis.onmicrosoft.com` (tenant `b9221ae6-0702-49b0-9d96-3e7a0a3e33aa`) — **KEPT at pause** (free, painful to rebuild). App reg **PhoenixSIS-Dev** client `1fdd8ec2-b221-46ef-9208-dfc8405d57ae`; group **PhoenixSIS.Admin** `cf1f69d0-dee9-4a0e-94e4-8a1eb0a00add`; admin `Patrick.Hines@PhoenixSIS.onmicrosoft.com`. Client secret in dotnet user-secrets on Patrick's machine (expires ~2027-01 — **regenerate on resume if past that**).
3. **.NET 10 LTS**, all projects net10.0.
4. **Ed-Fi: ODS/API 7.3.x**, binary deploy (SQL Server Docker path is experimental), Data Standard 6.1 current.
5. **NH pilot posture** (2026-07-22): hosting on New Heights infra as pilot/design-partner instance; IP clarification deferred by Patrick — risk register: Very High. On resume, re-decide hosting (NH infra again vs personal Azure).
6. Epic roadmap per Amendment v1.1: Sprint 3-4 Ed-Fi setup → 5-8 EdFi module → 9-11 AdultEd → 12-17 Portal → 18-19 investor prototype; CMS-parity modules = post-prototype backlog.

## 4. Working patterns (what made sessions effective)

- Patrick runs all git/builds/Azure/portal/RDP work; Claude gives exact commands (one per block), FIND/REPLACE edits, "Test the fix" expected outcomes, and writes files via the device bridge (SendUserFile → commit, verify after write).
- Research-before-build every time the docs might have moved (paid off: Ed-Fi v8.0 had shipped a week earlier; docs site needs Chrome rendering, WebFetch can't).
- Walls logged in-doc as they happen, not after. GitHub Project "Phoenix SIS - Solo Dev" (users/senihp5/projects/2), epic labels only, paste-ready issue bodies from chat.
- Handoff/decision docs to BOTH the repo (`docs\`) and the Claude project knowledge.

## 5. Asset inventory at pause

| Survives | Where |
|---|---|
| Code + all docs | `F:\Github\PhoenixSIS` + GitHub (private), tags phoenix-sprint-01/02 |
| Planning corpus | `docs\` 01-05 folders (8 design docs), amendments, sprint cards, ADR-001, module template, edfi decision + setup docs, retros |
| Phoenix Entra tenant + app reg + group | phoenixsis.onmicrosoft.com (free; kept) |
| Claude project knowledge | Phoenix SIS project: this handoff + decision doc + working preferences |
| GitHub Project board | Sprints 1-3 issues, actuals |

| Removed (teardown §6) | Notes |
|---|---|
| nh-phoenix-cus, nh-edfi-cus App Services | Were on existing plan-nh-cus — **$0/mo incremental**; deletion is hygiene, not savings |
| phoenix + asuid.phoenix DNS records | Zone newheightsed.com |
| EdFi_* databases ×4 + edfi_app login | vm-decisioned-data (recovers VM disk) |
| VM + workstation temp files | C:\Temp EdFi packages (contained passwords) |

**Security follow-ups from the sprint (do regardless of pause):** rotate `decisioned-admin` SQL password (exposed in console/chat); `edfi_app` dropped entirely at teardown. .NET 10 SDK left on the VM (harmless, useful).

## 6. Teardown runbook (executed 2026-07-22 — re-usable if anything remains)

Ordered so nothing shared with CMS is touched. **Do NOT delete:** plan-nh-cus, the wildcard cert resource, vnet-decisioned/subnet-appsvc, NSG rules, the DNS zone, kv-decisioned-prod, anything CMS.

1. `az webapp delete --name nh-phoenix-cus --resource-group IDCardOCR` (removes its VNet integration, hostname + cert *bindings*; the shared cert resource is untouched)
2. `az webapp delete --name nh-edfi-cus --resource-group IDCardOCR`
3. `az network dns record-set cname delete --resource-group cloud-shell-storage-southcentralus --zone-name newheightsed.com --name phoenix --yes`
4. `az network dns record-set txt delete --resource-group cloud-shell-storage-southcentralus --zone-name newheightsed.com --name asuid.phoenix --yes`
5. SSMS on the VM: `DROP DATABASE EdFi_Admin; DROP DATABASE EdFi_Security; DROP DATABASE EdFi_Ods_Minimal_Template; DROP DATABASE EdFi_Ods_Populated_Template;` then `DROP LOGIN edfi_app;`
6. VM: delete `C:\Temp\EdFi.Suite3.RestApi.Databases.*` and `C:\temp\dotnet-install.ps1`; rotate `decisioned-admin` password.
7. Workstation: delete `C:\Temp\EdFi.WebApi*` / `C:\Temp\edfi-*` if present.
8. Entra (optional, done at Patrick's discretion): remove `https://phoenix.newheightsed.com/signin-oidc` from PhoenixSIS-Dev redirect URIs. Tenant itself KEPT.
9. Verify: `az webapp list --resource-group IDCardOCR -o table` shows no nh-phoenix/nh-edfi; DNS zone has no phoenix records; SSMS shows no EdFi_* DBs.

## 7. Resume path (first session back)

1. Read this doc + `docs\edfi\local-setup.md` (the walls list is the time-saver) + Amendment v1.1.
2. Verify: repo clones/builds (`dotnet build`, `dotnet test`); .NET 10 still current LTS; Entra secret not expired (regenerate + user-secrets if so); localhost sign-in works (redirect URIs for localhost:7091 kept).
3. Re-decide hosting (locked decision #5 was NH-pilot; revisit with fresh eyes + IP question).
4. Check Ed-Fi landscape again: is v8.1+ out and SQL-Server-solid? The v8-vs-7.3 decision was time-sensitive (made 1 week after v8.0 shipped). If v8 matured, the Sprint 3 remainder may target it instead.
5. Resume at PHX-3.2/3.3 re-provisioning (fast — every command is in this repo's docs) or re-plan Sprint 3 per #4.
6. Loom demo + Sprint 1/2 velocity actuals are on the project board; retro files in `docs\retros\`.

## 8. Collaboration context for future sessions

25-yr IT Director, SCRUM master, solo dev with Claude. Direct, concise; explicit steps with full paths; single-command PowerShell blocks; expects honest pushback once (IP flag was raised and decided — don't re-nag); moves fast, pivots decisively (fresh repo, tenant choice reversal, local→cloud SQL, pause). Handoff notification at 80% token; docs in both repo and project knowledge.

*End of pause handoff. The project sleeps; nothing is lost.*
