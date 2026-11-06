# Sprint 7: First Phoenix → Ed-Fi Student Sync

**Sprint Goal:** Push canonical Phoenix students to the Ed-Fi ODS — create each as an Ed-Fi student enrolled at the New Heights adult-HS campus (220822001), and record the Ed-Fi identifier back on the Phoenix student so the mapping closes the loop. The end-to-end demo: a student that originated in a source SIS, was imported into Phoenix's clean-room store, and now lands in Ed-Fi.

**Total Estimated Hours:** 14-20
**Epic:** Epic 3 — EdFi Integration Module (`epic:edfi-module`)

**Modeling decisions (2026-11-06, stated — not blocking):**
- **studentUniqueId** = the Phoenix student's primary source identifier value (e.g. the `TexasSample` `NH-24001`). Readable and stable; Phoenix stays the hub.
- After a successful push, record an **`Ed-Fi`** identifier (= that studentUniqueId) on the Phoenix student via `IStudentIdentityService.AddIdentifierAsync` — the mapping table now links Phoenix ↔ Ed-Fi.
- **Target campus** = schoolId **220822001** (seeded in 6.2), configurable; entry grade level **Adult Education**, a sample entry date.
- **Scope of a run** = all Phoenix students (we only have the Texas sample set); idempotent, so re-sync matches.

**Guardrails:** sample data only (nh-edfi-cus still public behind OAuth until the on-site lockdown). Warm the API before batch calls (cold-start 404s, Sprint 6 lesson). `main` always builds; sync is behind the `EdFi` flag.

---

## PHX-7.1: Ed-Fi write client

**Estimate:** 4-6 hrs

### Story
As the developer, I want the Ed-Fi client to create students and enrollments, so the sync has a write path.

### Tasks
- [ ] Extend `IEdFiApiClient` (Core): `UpsertStudentAsync(EdFiStudentWrite)` → POST `/data/v3/ed-fi/students`; `UpsertStudentSchoolAssociationAsync(EdFiEnrollmentWrite)` → POST `/data/v3/ed-fi/studentSchoolAssociations`
- [ ] Core write DTOs: `EdFiStudentWrite` (studentUniqueId, firstName, middleName?, lastSurname, birthDate); `EdFiEnrollmentWrite` (studentUniqueId, schoolId, entryDate, entryGradeLevelDescriptor)
- [ ] Impl in `EdFiApiClient`: bearer-authenticated POST (reuse the token cache); Ed-Fi POST is upsert-by-natural-key, so treat 200/201 as success; map the request DTOs to the Ed-Fi JSON shape (studentReference/schoolReference)
- [ ] Flag-guarded; retry transient cold-start 404/5xx
- [ ] Unit tests (fake handler): correct JSON shape, bearer sent, 200/201 both OK

### Definition of Done
- [ ] Both writes work against a fake handler; build + test green

---

## PHX-7.2: Student sync service

**Estimate:** 4-6 hrs

### Story
As the developer, I want a service that pushes Phoenix students to Ed-Fi and records the Ed-Fi identifier, so one call syncs the roster idempotently.

### Tasks
- [ ] `IStudentSyncService` (Core): `SyncAllAsync(long schoolId, bool apply, CancellationToken)` → `StudentSyncResult`
- [ ] Impl (Infrastructure, EdFi area): read Phoenix `Student`s (+ identifiers) from `PhoenixDbContext`; pick studentUniqueId = primary source identifier; **apply**: UpsertStudent → UpsertStudentSchoolAssociation(school, Adult Education, entryDate) → `AddIdentifierAsync(studentId, "Ed-Fi", uniqueId)`; **dry-run**: classify would-sync/already-synced (student already has an `Ed-Fi` identifier) with no writes
- [ ] `StudentSyncResult` (Core): per-student outcome (Synced / AlreadySynced / Failed+reason) + counts
- [ ] Idempotent: re-run → AlreadySynced (Ed-Fi upsert + AddIdentifier both idempotent); flag-guarded
- [ ] Unit tests (InMemory Phoenix + fake IEdFiApiClient): apply creates + records identifier; re-run is AlreadySynced; a write failure is captured per-student, not fatal

### Definition of Done
- [ ] Apply syncs + records Ed-Fi identifiers; dry-run writes nothing; re-run idempotent; build + test green

---

## PHX-7.3: Sync admin page

**Estimate:** 3-4 hrs

### Story
As an admin, I want a page to preview and run the Phoenix → Ed-Fi sync, so it's usable and demoable.

### Tasks
- [ ] `admin/edfi/sync` (RequireAdmin, InteractiveServer, flag-guarded, conditional nav): shows target campus (220822001), **Preview** (counts: to-sync vs already-synced) then **Run**, results table (student, uniqueId, outcome)
- [ ] Target schoolId from config (`EdFiApi:DefaultSchoolId`, default 220822001)
- [ ] Never throws to the user; per-student failures shown inline

### Definition of Done
- [ ] Flag OFF → friendly message; flag ON → preview + run; errors surfaced, no crash; build + test green

---

## PHX-7.4: End-to-end live sync + docs + close

**Estimate:** 3-4 hrs

### Story
As the team, I want the real sync proven against the sandbox and documented, so Sprint 7 closes on a working end-to-end demo.

### Tasks
- [ ] Ensure the Texas sample students are in Phoenix (migration wizard) and the NH org is seeded (`Seed-EdfiReference.ps1`)
- [ ] Run the sync (opt-in live test or the page): students appear in Ed-Fi at campus 220822001; GET verifies student + studentSchoolAssociation; Phoenix students now carry an `Ed-Fi` identifier
- [ ] Re-run → all AlreadySynced (no duplicates)
- [ ] `docs\features\modules\EdFi.md` updated with the sync; retro `docs\retros\sprint-07.md`; tag `phoenix-sprint-07`; update project status
- [ ] Human eyeball note: admin verifies the sync page end to end

### Definition of Done
- [ ] Live sync verified in Ed-Fi; idempotent; docs updated; `main` green; sprint tagged
