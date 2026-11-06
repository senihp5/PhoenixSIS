# Sprint 6: Texas-Shaped Sample Data + TEDS Gap Analysis

**Sprint Goal:** Make the prototype read as a Texas adult-high-school scenario end to end — a New-Heights-shaped LEA/campus and Texas-relevant descriptor values seeded into the Ed-Fi ODS, Texas-shaped adult-HS sample students in Phoenix ready to sync — and document, honestly, the gap between this national Ed-Fi 7.3 / Data Standard 6.1 setup and real TEDS/TSDS conformance.

**Total Estimated Hours:** 12-18
**Epic:** Epic 5 — State Reporting Readiness (TEDS) (`epic:state-reporting`)
**Scope decision (2026-11-06, product owner):** Texas-shaped seed + gap doc — NOT installing the real TEA TEDS extension (TEDS targets an older Ed-Fi generation than our 7.3/DS 6.1, and TSDS submission is blocked on the open TEA in-house-vendor question). See `docs\integrations\texas-tsds-requirements.md`.

**Guardrails:** sample data only (nh-edfi-cus still public behind OAuth until the on-site lockdown). Seeders are idempotent and reproducible, secrets from the local creds files, never the repo. `main` always builds.

---

## PHX-6.1: TEDS gap-analysis doc

**Estimate:** 3-4 hrs

### Story
As the team, I want an honest, written delta between what we have (national Ed-Fi 7.3/DS 6.1) and what real Texas TSDS submission requires, so the prototype's claims are accurate and the future path is clear.

### Tasks
- [ ] `docs\integrations\teds-gap-analysis.md` covering: the platform-generation gap (our 7.3/DS 6.1 vs TEA's Ed-Fi 3.x-based TEDS); Texas extensions & descriptor namespaces (uri://tea.texas.gov/...) absent from our national model; PEIMS collections (FALL/MDYR/SUMR/EXYR), Unique ID, TREx, Class Roster that TSDS needs; the API/validation path (L1–L3, Vendor Data Completeness Scorecard); and the still-open TEA question (can an in-house SIS register as a source-system vendor)
- [ ] State plainly what the prototype demonstrates (the mechanism, against the national model) vs what real submission would additionally require
- [ ] Cross-link `texas-tsds-requirements.md`

### Definition of Done
- [ ] Doc committed; no overstated conformance claims anywhere in the repo

---

## PHX-6.2: Texas-shaped Ed-Fi reference seed (org + descriptors)

**Estimate:** 4-6 hrs

### Story
As the developer, I want the Ed-Fi ODS to hold a New-Heights-shaped LEA/campus and the Texas-relevant descriptor values the scenario needs, so synced students have valid targets.

### Tasks
- [ ] `_tools\Seed-EdfiReference.ps1` (uses the sandbox key/secret from `C:\temp\edfi-sandbox.ps1`): POSTs via the Ed-Fi API, idempotent (check-then-create / PUT by natural key)
- [ ] A New-Heights-shaped LEA (Texas county-district-number style) + at least one campus (school), consistent with the Ed-Fi Sandbox claim set's permissions
- [ ] The descriptor values the adult-HS scenario references (e.g., grade levels, entry/exit, a Texas-namespaced example where the model allows) — kept minimal and documented
- [ ] Verify via GET: org + descriptors present; script re-run is a no-op (idempotent)

### Definition of Done
- [ ] Seeder creates the org + descriptors; re-running changes nothing
- [ ] Reproducible from the script alone; what it creates is documented

---

## PHX-6.3: Texas-shaped adult-HS sample students (Phoenix source data)

**Estimate:** 3-4 hrs

### Story
As the developer, I want Texas-shaped adult-HS sample students in Phoenix, so Sprint 7's Phoenix → Ed-Fi sync has realistic source data to push.

### Tasks
- [ ] A Texas adult-HS sample dataset (older students, realistic names/DOBs, NH-campus enrollment intent) surfaced through a connector — either enrich `SampleSourceConnector` or add a `TexasSampleSourceConnector` (ISisSourceConnector)
- [ ] Import into Phoenix via the migration wizard/pipeline (dry-run then apply); confirm canonical students + identifiers land
- [ ] Keep the clean-room boundary — source fields mapped at the connector (ADR-002)
- [ ] Unit test for the new connector's records; wizard still works with the source selectable

### Definition of Done
- [ ] Texas-shaped students import into Phoenix idempotently
- [ ] build + test green

---

## PHX-6.4: Seeder docs + Sprint 6 close

**Estimate:** 2-3 hrs

### Story
As the developer, I want the Texas seed documented and the sprint closed, so Sprint 7 (first sync) starts from a known, reproducible Texas-shaped baseline.

### Tasks
- [ ] `docs\edfi\texas-sample.md`: what the reference seed creates, how to run it, how it maps toward (and differs from) real TEDS (link the gap doc)
- [ ] Retro `docs\retros\sprint-06.md`
- [ ] Tag `phoenix-sprint-06`; update the project status doc

### Definition of Done
- [ ] Docs complete; `main` green
- [ ] Sprint tagged; ready for Sprint 7 (Phoenix → Ed-Fi sync)
