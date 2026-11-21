# Sprint 8: Audit Trail + Enrollment Model

**Revision:** v1, 2026-11-20
**Sprint Goal:** Make Phoenix ready to hold real student data, and make the Ed-Fi sync
honest. Every view and change of a student is recorded in an append-only audit trail
(ADR-004). Enrollments become Phoenix data (campus, school year, entry/exit dates, grade
level, FTE), and the sync sends what Phoenix holds instead of one configured entry date.

**Total Estimated Hours:** 27-37
**Epics:** Epic 4 - Data Migration & Clean-Room Identity (audit + enrollment are core
domain); Epic 3 - EdFi Integration (sync from enrollments)

**Modeling decisions (defaults - Patrick may change before the story starts):**
- **Audit** per ADR-004: core, always on, append-only (trigger), no PII in rows, per-student
  events, fail closed.
- **Enrollment** is core domain (like `Student`), Phoenix-native names: `CampusNumber`
  (9-digit TEA campus, e.g. 220822001), `SchoolYear` (ending year: 2027 = 2026-27),
  `EntryDate`, `EntryGradeLevel` (Phoenix code: AdultEducation, Grade09..Grade12, Ungraded),
  `EnrollmentKind` (Phoenix code), `FullTimeEquivalency` (0-1), `IsPrimary`, `ExitDate?`,
  `ExitReason?` (Phoenix code). Ed-Fi descriptors are mapped at the EdFi boundary, never
  stored in core.
- **Campuses** come from config (`Phoenix:Campuses`: number, name, LEA) - one campus today.
  A `Campus` table waits for a second campus.
- **Entry date is immutable once created**; the only change is an exit. No deletes this
  sprint. (Ed-Fi keys the enrollment on entry date, so an edit would orphan the synced one.)
- **Rules:** exit date >= entry date; entry date inside the school year (Jul 1 .. Jun 30);
  no two open enrollments for one student at one campus; one primary enrollment per student
  per school year.
- **UI** for students and enrollments sits behind a new `Students` flag (OFF in base, ON in
  Phoenix-Dev). Recording the audit is never behind a flag.

**Guardrails:** sample data only. `main` always builds. Migrations are applied by Patrick
(`dotnet ef database update`); verify DB changes by querying. Architecture tests stay green.

---

## PHX-8.1: Audit trail core

**Estimate:** 6-8 hrs

### Tasks
- [x] Core `Phoenix.Core\Auditing`: `IAuditLog` (`RecordAsync`, `RecordManyAsync`),
      `AuditEntry`, `AuditAction` (View, Create, Update, Enroll, Exit, SyncPreview, SyncApply,
      AuditView), `ICurrentUser` (`ActorId`, `ActorName`, system actor).
- [x] Infrastructure: `AuditEvent` entity + EF migration `AddAuditEvent` (indexes on
      StudentId+OccurredUtc and ActorId+OccurredUtc) + trigger rejecting UPDATE/DELETE;
      `AuditLog` service (stages rows on the shared DbContext; `RecordAsync` for reads saves
      immediately).
- [x] Phoenix.Web: `CurrentUser` from `AuthenticationStateProvider` (scoped per circuit).
- [x] `StudentIdentityService`: student created and identifier added are audited in the same
      save (fail closed).
- [x] Tests (InMemory + fake current user): create/identifier writes emit events with actor
      and student; no names in `Summary`; a failing audit write fails the operation.

### Definition of Done
- [x] Migration builds; identity writes audited; tests + architecture tests green.

---

## PHX-8.2: Enrollment model

**Estimate:** 6-8 hrs

### Tasks
- [x] Core `Phoenix.Core\Students`: `EnrollmentRecord`, `NewEnrollment`, codes
      (`GradeLevel`, `EnrollmentKind`, `ExitReason`), `IEnrollmentService`
      (`GetForStudentAsync`, `EnrollAsync`, `ExitAsync`, `EnrollManyAsync`), `CampusInfo` +
      campus list from config.
- [x] Infrastructure: `Enrollment` entity + migration `AddEnrollment` (FK to Student, index
      on StudentId+SchoolYear); `EnrollmentService` enforcing the rules above; every enroll and
      exit audited in the same save.
- [x] Tests: each rule; enroll + exit audited; bulk enroll skips students already enrolled for
      that year.

### Definition of Done
- [x] Enrollments persist with rules enforced and audited; build + tests green.

---

## PHX-8.3: Student pages + bulk enroll (`Students` flag)

**Estimate:** 6-8 hrs

### Tasks
- [x] `Students` flag (base OFF, Phoenix-Dev ON); `StudentsModule : IModule` (pages only -
      services are core).
- [x] `admin/students`: paged list (25), search by name or PHX id, columns name / Ed-Fi id /
      current-year enrollment; one View event per student shown.
- [x] `admin/students/{id}`: identifiers, enrollments, **Enroll** form, **Exit** form; one View
      event on open.
- [x] **Bulk enroll** on the list page: "Enroll every student without a {year} enrollment at
      {campus}, entry date {date}" - preview count, then apply; audited per student.
- [x] One-page design brief (Who/What/Where/Why) + module doc `docs\features\modules\Students.md`.

### Definition of Done
- [x] Flag OFF -> friendly message; flag ON -> list, detail, enroll, exit, bulk enroll; every
      view and change audited; build + tests green.

---

## PHX-8.4: Sync from Phoenix enrollments

**Estimate:** 4-6 hrs

### Tasks
- [x] Probe live: `exitWithdrawTypeDescriptors`, `enrollmentTypeDescriptors`, grade levels;
      record URIs in `docs\edfi\texas-sample.md`.
- [x] EdFi boundary mapping: Phoenix `GradeLevel` / `EnrollmentKind` / `ExitReason` ->
      Ed-Fi descriptor URIs (config-overridable).
- [x] `StudentSyncService`: one SSA per Phoenix enrollment matching the target campus +
      school year (entry/exit dates, grade, kind, FTE, primary); no matching enrollment ->
      Skipped ("no 2026-27 enrollment at 220822001"); drop `EntryDate` from `EdFiSyncTarget`
      and the page; keep the 403 enroll-first recovery.
- [x] Sync audited: preview = SyncPreview per student listed; apply = SyncApply per student.
- [x] Tests updated: enrollments drive the SSA; exit sent; unenrolled skipped; audit events.

### Definition of Done
- [x] Sync sends Phoenix enrollment data; build + tests green.

---

## PHX-8.5: Audit viewer, live verification, close

**Estimate:** 4-6 hrs

### Tasks
- [x] `admin/audit` (RequireAdmin, no flag): newest first, paged, filter by student / actor /
      date range; opening it records AuditView.
- [x] Live: apply both migrations to PhoenixSIS; bulk-enroll the 13 sample students
      (campus 220822001, SY 2027, entry 2026-08-17); exit one student; sync preview + run;
      verify in Ed-Fi (SSA dates, one exitWithdrawDate) and in SQL (audit rows per student,
      actor = Patrick, no names in Summary); prove the trigger blocks UPDATE and DELETE.
- [x] Docs: ADR-004 -> Accepted; module docs; retro `docs\retros\sprint-08.md`; tag
      `phoenix-sprint-08`; project status.

### Definition of Done
- [x] Every student view/change in Phoenix is in the audit trail; sync uses enrollments;
      verified live; sprint tagged.

---

## Open questions (do not block the sprint)
- Audit retention period - New Heights to set (default: keep indefinitely).
- Separate the application SQL login from the schema owner so the app cannot disable the
  audit trigger (later hardening).
- Correcting an entry date after sync (needs Ed-Fi SSA delete + re-create) - later sprint.

---

## Close note (2026-11-20)

All five stories done; retro in `docs\retros\sprint-08.md`. Differences from the cards:
- 8.1: the contract is `StageAsync` / `RecordAsync` (no `RecordManyAsync`).
- 8.4: apply writes two SyncApply rows per sent student (send started, then the result).
- 8.5: 19 students were bulk-enrolled, not 13 (the 6 Flightdeck sample students were added).
- 8.5: one item is carried over - a second live sync run to prove that a student whose only
  enrollment is exited can still be updated in Ed-Fi.
