# Sprint 3 Retrospective — Ed-Fi ODS/API on NH-Pilot Infrastructure

**Sprint window:** 2026-07-22 → 2026-11-05 (paused 2026-07-22, reactivated 2026-11-04)
**Demo:** live Ed-Fi API at https://nh-edfi-cus.azurewebsites.net (behind OAuth; IP lockdown pending on-site)

## Stories completed

| Story | Description | Status |
|---|---|---|
| PHX-3.1 | Ed-Fi version decision (7.3 vs 8.x) + install research | ✅ |
| PHX-3.2 | Azure App Services (phoenix + edfi), VNet, DNS, cert | ✅ (rebuilt 11-05) |
| PHX-3.3 | Ed-Fi databases (Phase A) + WebApi deploy (Phase B) | ✅ (rebuilt 11-05) |
| PHX-3.4 | OAuth client + sandbox (direct-in-EdFi_Admin) | ✅ |
| PHX-3.5 | Student CRUD cycle | ✅ (10/10) |
| PHX-3.6 | Reset script + docs + close | ✅ |

## Context notes

This sprint spanned a full project pause (Azure teardown 2026-07-22) and a reactivation
(2026-11-04). The pause was clean: all shared DecisionED/NH resources were preserved and
the rebuild re-ran PHX-3.2 and PHX-3.3 from scripts rather than from memory. Everything
operational now lives in `_tools\` (New-EdfiLogin, Run-EdfiPhaseA, Patch-EdfiConfiguration,
Backup-EdfiBaseline, New-EdfiSandbox, Reset-EdfiSandbox, Test-EdfiStudentCrud) and the nine
install walls are in `docs\edfi\local-setup.md`.

## 1. What went well?

- The pause/rebuild validated the "scripts are the source of truth" bet: the second build
  of the infrastructure was faster and cleaner than the first, because July's walls were
  already pre-fixed in code.
- Discovering schema/FKs from the live database (`sys.foreign_keys`, Admin column dump)
  before writing destructive SQL caught three things memory would have gotten wrong
  (ApiClientApplicationEducationOrganizations, OperationalContextUri NOT NULL, and the full
  delete-order graph including ClientAccessTokens).
- Reading Ed-Fi's own source to diagnose the encryption-key requirement (wall #9) turned a
  cryptic 500 into a one-line app setting.

## 2. What was harder than expected?

- The 32-bit App Service worker (wall #7) and the xxHash `Bmi2.X64` bug (wall #8) cost the
  most time — a library bug surfaced only by App Service's default bitness. The real fix
  (true 64-bit worker) is still open; `DOTNET_EnableHWIntrinsic=0` is a working stopgap.
- Relationship-based authorization (wall, PHX-3.5): a freshly-created student is unreadable
  until a studentSchoolAssociation exists. Non-obvious until the 403 hint spelled it out.

## 3. What did I learn?

- Retail .NET ignores per-ISA `DOTNET_Enable{ISA}` knobs; only `DOTNET_EnableHWIntrinsic`
  (master) is honored. Proven by running a diagnostic on the App Service itself.
- Ed-Fi 7.x encrypts OdsInstances connection strings and auto-encrypts plaintext rows in
  place once the key is supplied — seed plaintext, let the API convert.
- Ed-Fi sandbox provisioning does NOT require SandboxAdmin; direct EdFi_Admin seeding works
  and is scriptable/repeatable.

## 4. What will I do differently next sprint?

- Resolve the 32-bit worker before building anything perf-sensitive on nh-edfi-cus, and
  drop the HWIntrinsic stopgap.
- Treat "discover the live schema first" as a standing rule for any DB-touching script.

## 5. Open items carried out of Sprint 3 (→ hardening, on-site next week)

- IP lockdown on nh-edfi-cus (public behind OAuth until then — **sample data only**).
- 64-bit worker fix → remove `DOTNET_EnableHWIntrinsic=0`.
- Downgrade `edfi_app` from sysadmin to db_owner on EdFi_* (after sandbox churn settles).
- Back up `ApiSettings__OdsConnectionStringEncryptionKey` (Key Vault) + rotate PhoenixSIS-Dev
  Entra secret (~2027-01) + verify phoenix.newheightsed.com/signin-oidc redirect URI.
- Confirm VM `C:\Temp` cleanup (EdFiRebuild, Run-EdfiPhaseA.ps1, dotnet-install.ps1).
- Written IP clarification with New Heights (Very High risk, deferred since 2026-07-22).
