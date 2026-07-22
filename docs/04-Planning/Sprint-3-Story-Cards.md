# Sprint 3: Ed-Fi ODS Deployment (Azure App Service + SQL VM)

**Sprint Goal:** Ed-Fi ODS/API 7.3.x running as an Azure App Service against EdFi_* databases on the DecisionED SQL VM, authenticated, with a test student round-tripping through the API from Postman. Phoenix's own App Service provisioned at phoenix.newheightsed.com.

**Total Estimated Hours:** 18-28 (walls expected — community.ed-fi.org when stuck >2 hrs)
**Epic:** Epic 2 — Ed-Fi ODS Setup (`epic:edfi-setup`)
**Revised 2026-07-22** per `docs\edfi\install-decision.md`: local Docker path dropped (machine headroom); NH-hosted pilot infrastructure adopted.

---

## PHX-3.1: Verify current Ed-Fi release + choose install path — ✅ DONE 2026-07-22

Decided: ODS/API 7.3.x; EdFi_* DBs on `vm-decisioned-data`; ODS/API as VNet-integrated App Service; Phoenix app on `plan-nh-cus` at phoenix.newheightsed.com; NH pilot posture. v8 spike queued for when v8.1 lands. See `docs\edfi\install-decision.md`. Remaining human task: register on community.ed-fi.org.

---

## PHX-3.2: Provision Azure infrastructure (REVISED)

**Estimate:** 3-5 hrs

### Story
As the developer, I want the Phoenix and Ed-Fi App Services provisioned with private connectivity to the SQL VM, DNS, and certs, so deployments have a home before anything is installed.

### Tasks
- [ ] Create App Service **nh-phoenix-cus** on `plan-nh-cus` (RG IDCardOCR, Windows, .NET 10) — Phoenix app
- [ ] Create App Service **nh-edfi-cus** on `plan-nh-cus` — Ed-Fi ODS/API host (stack per PHX-3.3 packaging decision; if container deploy is chosen this may need to be Linux — confirm before creating, or create in 3.3)
- [ ] VNet integration: both apps → `subnet-appsvc` (10.10.4.0/26) in `vnet-decisioned`; confirm NSG `Allow-AppSvc-SQL-1433` covers them; `WEBSITE_VNET_ROUTE_ALL=1`, `WEBSITE_DNS_SERVER=168.63.129.16`
- [ ] Verify VM reachability from each app (Kudu console: tcpping 10.10.2.4:1433)
- [ ] DNS: CNAME `phoenix` → nh-phoenix-cus.azurewebsites.net (+ asuid TXT); bind `*.newheightsed.com` cert
- [ ] **Access restrictions on nh-edfi-cus**: deny-all except Patrick's IP + what needs it — the Ed-Fi API must not be public
- [ ] Add `https://phoenix.newheightsed.com/signin-oidc` + `/signout-oidc` to the PhoenixSIS-Dev app registration redirect URIs

### Definition of Done
- [ ] Both apps exist, VNet-integrated, tcpping to the VM succeeds from Kudu
- [ ] phoenix.newheightsed.com resolves with valid cert (default parking page is fine)
- [ ] Ed-Fi app locked down by access restrictions
- [ ] Steps recorded in docs\edfi\local-setup.md (renamed scope: environment-setup)

---

## PHX-3.3: Deploy Ed-Fi ODS/API 7.3 + EdFi_* databases (REVISED)

**Estimate:** 6-10 hrs

### Story
As the developer, I want ODS/API 7.3.x serving from nh-edfi-cus against EdFi_Ods/EdFi_Admin/EdFi_Security on the SQL VM, so I have a real Ed-Fi API to build against.

### Tasks
- [ ] Decide packaging: official Docker image on App Service for Containers vs binary/NuGet deploy to Windows App Service — verify SQL Server support in the 7.3 Docker images (Docker release v3.2.0) before choosing
- [ ] Create EdFi_* databases on `vm-decisioned-data` (minimal template via Ed-Fi's SQL Server backups or Db.Deploy tool); create `edfi_app` SQL login (least privilege)
- [ ] Configure connection strings + settings on nh-edfi-cus (App Service settings — no secrets in files)
- [ ] Deploy; discovery endpoint returns version metadata; Swagger UI lists resources
- [ ] Admin App / Admin API (per 7.3 tooling) available for key management — may be a second small app or CLI-based; decide and document
- [ ] Every wall + fix logged in docs\edfi\local-setup.md as you go

### Definition of Done
- [ ] Discovery endpoint + Swagger reachable (from allowed IPs)
- [ ] EdFi_* databases visible on the VM in SSMS
- [ ] Setup reproducible from the doc alone

---

## PHX-3.4: OAuth client credentials + Postman collection — unchanged

**Estimate:** 2-3 hrs — as originally written, pointed at the nh-edfi-cus URL.

## PHX-3.5: Student round-trip (POST/GET/PUT/DELETE) — unchanged

**Estimate:** 3-4 hrs — as originally written.

## PHX-3.6: Reset script + docs + sprint close (reset approach revised)

**Estimate:** 3-4 hrs

Changes from original: `scripts\reset-edfi.ps1` becomes a **SQL restore on the VM** — restore EdFi_Ods (and Admin/Security if touched) from the post-install backup taken at the end of PHX-3.3. Take that baseline backup as part of 3.3's close. Everything else (docs, retro, Loom, `phoenix-sprint-03` tag, Sprint 4 draft) as originally written.
