# Sprint 7: First Phoenix -> Ed-Fi Student Sync (Data Standard 6.1)

**Revision:** v2, 2026-11-20 - re-cut per Amendment v1.5 (v1 was commits ae7c868 / 9ad15af).
**Sprint Goal:** Push canonical Phoenix students to the Ed-Fi ODS in the shape Texas will accept for SY 2027-28 (Ed-Fi Data Standard 6.1 on ODS/API 7.3): each student created, enrolled at the New Heights adult-HS campus (220822001) with the certification-scenario enrollment fields, associated to the LEA, and carrying Phoenix's identifier - and record the Ed-Fi identifier back on the Phoenix student so the mapping closes the loop.

**Total Estimated Hours:** 20-28
**Epic:** Epic 3 - EdFi Integration Module (`epic:edfi-module`)
**Target:** nh-edfi-cus - ODS/API 7.3.20432, data model Ed-Fi 6.1.0 (verified live 2026-11-20).

**Modeling decisions:**
- **studentUniqueId** = Phoenix-generated, readable, source-independent `PHX-NNNNNN` (decided 2026-11-06) for the sandbox. Assigned once, stored as the student's `Ed-Fi` identifier via `IStudentIdentityService.AddIdentifierAsync`. A real NH -> TSDS submission will carry the student's existing State ID (Flightdeck `StateStudentNumber`) - later, gated (ADR-003, task #36).
- **Phoenix identifier to Ed-Fi:** `studentIdentificationCodes` with the *Local* identification system and `identificationCode` = `PHX-NNNNNN`, education organization = LEA 220822. **SSN is never sent.**
- **Target campus** = schoolId 220822001 (seeded in 6.2), configurable; entry grade level Adult Education; LEA 220822.
- **Scope of a run** = all Phoenix students (Texas sample set); idempotent.

**Out of scope (Amendment v1.5):** TX extension resources (wait for SDK 2028.0.0, Jan/Feb 2027); the DS 4.0 adapter (dropped); studentDemographics / studentDirectories / contacts (Phoenix does not hold that data yet); real NH data (ADR-003 gates).

**Guardrails:** sample data only. Warm the API before batch calls (cold-start 404s). `main` always builds; sync stays behind the `EdFi` flag. **Every descriptor value and resource shape is probed against the live API before code is written** - no assumed values.

---

## PHX-7.0: Seed the school-year scaffolding the enrollment needs

**Estimate:** 3-4 hrs

### Story
As the developer, I want the campus to have a school year, calendar, and session in the ODS, so enrollments can carry `schoolYearTypeReference` and `calendarReference` like a real SIS.

### Tasks
- [ ] Probe live: `schoolYearTypes` (does 2027 exist?), and the descriptor values available for `calendarTypeDescriptor`, `termDescriptor`, `enrollmentTypeDescriptor`, `entryGradeLevelDescriptor` (Adult Education), `studentIdentificationSystemDescriptor` (Local). Record the exact URIs in `docs\edfi\texas-sample.md`.
- [ ] Extend `_tools\Seed-EdfiReference.ps1` (idempotent POST upserts): `calendars` for school 220822001 / school year 2027 (required: calendarCode, calendarTypeDescriptor, schoolReference, schoolYearTypeReference); `sessions` for the same (required: sessionName, termDescriptor, beginDate, endDate, totalInstructionalDays, schoolReference, schoolYearTypeReference).
- [ ] Re-run is a no-op; GET verifies both.

### Definition of Done
- [ ] Calendar + session exist for 220822001 / 2027; descriptor URIs documented; seeder idempotent.

---

## PHX-7.1: Ed-Fi write client (DS 6.1, Problem Details)

**Estimate:** 6-8 hrs

### Story
As the developer, I want the Ed-Fi client to write students, enrollments, LEA associations, and identification codes, and to surface Ed-Fi's errors precisely, so the sync has a correct write path.

### Tasks
- [ ] Core contracts (`Phoenix.Core\Modules\EdFi`): `EdFiStudentWrite`; `EdFiEnrollmentWrite` (studentUniqueId, schoolId, entryDate, entryGradeLevelDescriptor, schoolYear, calendarCode?, primarySchool?, enrollmentTypeDescriptor?, fullTimeEquivalency?); `EdFiStudentEdOrgWrite`; `EdFiStudentIdentificationCodeWrite`; `EdFiWriteResult` (success, status, `EdFiProblem?`).
- [ ] `EdFiProblem` = RFC 7807 Problem Details as ODS/API 7.3 returns it: `type`, `title`, `status`, `detail`, `correlationId`, `errors[]`.
- [ ] `IEdFiApiClient`: `UpsertStudentAsync`, `UpsertStudentSchoolAssociationAsync`, `UpsertStudentEducationOrganizationAssociationAsync`, `UpsertStudentIdentificationCodeAsync`.
- [ ] Impl: bearer POST (reuse the token cache); 200/201 = success (POST is upsert-by-natural-key); map DTOs to Ed-Fi JSON (references nested); parse Problem Details on 4xx/5xx; retry **only** transient cold-start 404 and 5xx; never retry 400/403/409.
- [ ] Unit tests (fake handler): JSON shape incl. the enrollment scenario fields and nested references; bearer sent; 200/201 OK; a 409 Problem Details body parsed into `EdFiProblem` (detail, type, errors[]); 400 not retried; 503 retried.

### Definition of Done
- [ ] All four writes + Problem Details parsing proven against a fake handler; build + test green; arch tests green.

---

## PHX-7.2: Student sync service

**Estimate:** 5-7 hrs

### Story
As the developer, I want a service that pushes Phoenix students to Ed-Fi and records the Ed-Fi identifier, so one call syncs the roster idempotently.

### Tasks
- [ ] `IStudentSyncService` (Core): `SyncAllAsync(long schoolId, bool apply, CancellationToken)` -> `StudentSyncResult`.
- [ ] Impl (Infrastructure, EdFi area), per student: resolve studentUniqueId (existing `Ed-Fi` identifier, else new sequential `PHX-NNNNNN`) -> UpsertStudent -> UpsertStudentEducationOrganizationAssociation (LEA 220822) -> UpsertStudentSchoolAssociation (220822001, Adult Education, entry date, school year 2027, calendar from 7.0, primarySchool = true, enrollment type + FTE from config) -> UpsertStudentIdentificationCode (Local, PHX id) -> `AddIdentifierAsync(studentId, "Ed-Fi", uniqueId)`.
- [ ] Dry run: classify would-sync / already-synced, no writes.
- [ ] Per-student failure captured with the `EdFiProblem` detail - never fatal to the batch.
- [ ] Config (`EdFiApi` section, non-secret): `DefaultSchoolId`, `DefaultLeaId`, `SchoolYear`, `CalendarCode`, `DefaultEnrollmentTypeDescriptor`, `DefaultFullTimeEquivalency`.
- [ ] Unit tests (InMemory Phoenix + fake client): apply writes all four resources in order and records the identifier; re-run = AlreadySynced; one failure doesn't stop the rest and its Problem Details reason is kept.

### Definition of Done
- [ ] Apply syncs + records Ed-Fi identifiers; dry run writes nothing; re-run idempotent; build + test green.

---

## PHX-7.3: Sync admin page

**Estimate:** 3-4 hrs

### Story
As an admin, I want a page to preview and run the Phoenix -> Ed-Fi sync, so it's usable and demoable.

### Tasks
- [ ] `admin/edfi/sync` (RequireAdmin, InteractiveServer, flag-guarded, conditional nav): target campus, school year, calendar; **Preview** (to-sync vs already-synced) then **Run**; results table (student, uniqueId, outcome, Ed-Fi error detail + correlationId on failure).
- [ ] Never throws to the user.

### Definition of Done
- [ ] Flag OFF -> friendly message; flag ON -> preview + run; Ed-Fi errors shown with detail; build + test green.

---

## PHX-7.4: End-to-end live sync + docs + close

**Estimate:** 3-5 hrs

### Story
As the team, I want the real sync proven against the DS 6.1 sandbox and documented, so Sprint 7 closes on a working end-to-end demo in the shape Texas will accept in SY 2027-28.

### Tasks
- [ ] Texas sample students in Phoenix (migration wizard); NH org + 7.0 scaffolding seeded.
- [ ] Run the sync: GET verifies student, studentEducationOrganizationAssociation, studentSchoolAssociation (with calendar, school year, FTE, primarySchool), and studentIdentificationCode; Phoenix students carry an `Ed-Fi` identifier.
- [ ] Re-run -> all AlreadySynced (no duplicates). Force one failure (e.g. a bad descriptor) and confirm the page shows the Problem Details detail.
- [ ] `docs\features\modules\EdFi.md` updated; retro `docs\retros\sprint-07.md`; tag `phoenix-sprint-07`; project status updated.
- [ ] Human eyeball note: admin verifies the sync page end to end.

### Definition of Done
- [ ] Live sync verified on DS 6.1; idempotent; errors legible; docs updated; `main` green; sprint tagged.
