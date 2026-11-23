# Sprint 9: Multi-Identifier Sources + DS 6.1 Adult Demographics

**Revision:** v1, 2026-11-20 (Proposed - Patrick chose the scope; details below are defaults)
**Sprint Goal:** A source record can carry every identifier the source knows for a student,
so the state ID reaches Ed-Fi as its own identification code. Phoenix holds the two DS 6.1
facts Texas adult high school funding depends on - census block group and previous
attendance as an adult - and sends them. The adult-only gap analysis becomes a planning doc
that later sprints are cut from.

**Total Estimated Hours:** 24-33
**Epics:** Epic 4 - Data Migration & Clean-Room Identity (9.1, 9.2); Epic 3 - EdFi
Integration (9.2, 9.4); Epic 5 - AdultEd (9.3, 9.5)

**Modeling decisions (defaults - Patrick may change before the story starts):**
- **Identifiers stay mapped rows (ADR-002).** `SourceStudentRecord` gains a list of extra
  identifiers `(Kind, Value)`. `ExternalId` remains the source's primary key and the only
  value used to match on re-import. Extra identifiers are stored as `StudentIdentifier` rows
  with source system `<Source>:<Kind>` (for example `Flightdeck:StateStudentNumber`). No new
  columns on `Student`.
- **State ID is not an SSN.** Only the TSDS Unique ID is sent to Ed-Fi as the "State"
  identification code. The existing SSN guard stays. Which source kind is the state ID is
  config (`EdFiApi:StateIdentifierSource`), default `Flightdeck:StateStudentNumber`.
- **Demographics are core domain, Phoenix-native names:** `StudentDemographics` (one row per
  student): `CensusBlockGroup` (12 digits, validated, optional) and
  `AttendedPreviouslyAsAdult` (optional yes/no) plus whatever the live probe shows DS 6.1
  requires alongside them. Ed-Fi shapes are mapped at the EdFi boundary only.
- **Every view and change is audited** (ADR-004). New pages that read students load only
  when `RendererInfo.IsInteractive`.
- **Nothing here needs real data.** The Flightdeck live read stays gated (ADR-003).

**Guardrails:** sample data only. `main` always builds. Probe the live Ed-Fi API before
writing a payload. Migrations are applied by Patrick; verify DB changes by querying.
Architecture tests stay green. Value-based secret check after every `git add`.

---

## PHX-9.0: Carry-over - prove an exited student can be updated

**Estimate:** 0.5-2 hrs

### Tasks
- [ ] Run the sync once more live (Preview, Run). Expect 19 updated, 0 failed, PHX-000001
      "Updated"; verify by SQL (two new SyncApply rows per student) and Ed-Fi GET.
- [ ] If PHX-000001 fails with 403 on students: add the rule to the sync (skip the student
      write when every matching enrollment is exited and the student already exists, or the
      order the live API accepts), model it in the test fake, re-run.

### Definition of Done
- [ ] Result recorded in `docs\features\modules\EdFi.md`; build + tests green.

---

## PHX-9.1: Multi-identifier source records (task #36)

**Estimate:** 5-7 hrs

### Tasks
- [ ] Core `Phoenix.Core\Migration\ISisSourceConnector.cs`: `SourceIdentifier(Kind, Value)`;
      `SourceStudentRecord` gains `IReadOnlyList<SourceIdentifier> Identifiers` with an empty
      default, so the Sample and TexasSample connectors compile unchanged.
- [ ] `IStudentIdentityService`: one call that resolves or creates by the primary id and
      adds any missing extra identifiers in the same save (each audited; a value already
      mapped to a different student is a reported conflict, never a silent re-point).
- [ ] `MigrationService` + wizard preview: show how many identifiers will be added and list
      conflicts before Apply.
- [ ] `FlightdeckSourceConnector` (sample): emit `StudentNumber`, `PowerSchoolStudentID` and
      `StateStudentNumber` for the 6 sample students (made-up values, 10-digit state ids).
- [ ] Tests: re-import adds only missing identifiers; conflict reported; no duplicates;
      audit rows written; existing 88 tests unchanged.

### Definition of Done
- [ ] Re-importing the Flightdeck sample adds three identifiers per student and creates no
      students; `admin/students/{id}` lists them; build + tests green.

---

## PHX-9.2: State identification code in the Ed-Fi sync

**Estimate:** 3-4 hrs

### Tasks
- [ ] Probe live: `studentIdentificationSystemDescriptors` (confirm the State value) and
      whether a second identification code for the same student + LEA is accepted.
- [ ] `EdFiApiOptions`: `StateIdentificationSystemDescriptor`, `StateIdentifierSource`.
- [ ] `StudentSyncService`: after the Local code, send the State code when the student has
      the configured identifier; none -> nothing sent (not a failure). SSN guard unchanged.
- [ ] Tests: sent when present, absent when not, order kept, failure names the resource.

### Definition of Done
- [ ] Live: the 6 Flightdeck students have two identification codes in Ed-Fi (Local, State),
      the other 13 have one; verified by GET; build + tests green.

---

## PHX-9.3: Adult demographics in Phoenix

**Estimate:** 6-8 hrs

### Tasks
- [ ] Probe live first: the DS 6.1 `studentDemographics` resource - natural key, required
      fields, the exact shape of the census block group set and the adult previous attendance
      set, and their descriptors. Record it in `docs\edfi\texas-sample.md`.
- [ ] Core `Phoenix.Core\Students`: `StudentDemographicsRecord`, `IStudentDemographicsService`
      (`GetAsync`, `SaveAsync`); rules (block group = 12 digits; Texas state FIPS 48 warned,
      not enforced).
- [ ] Infrastructure: `StudentDemographics` entity + migration `AddStudentDemographics`
      (1:1 with Student, FK NO ACTION); every save audited in the same unit of work.
- [ ] `StudentRecordPage.razor`: a Demographics card (view + edit) behind the `Students`
      flag; the view is covered by the existing record View event.
- [ ] Tests: rules, audit, round trip. Update `docs\features\modules\Students.md`.

### Definition of Done
- [ ] Demographics persist per student with rules enforced and audited; build + tests green.

---

## PHX-9.4: Demographics in the Ed-Fi sync

**Estimate:** 4-6 hrs

### Tasks
- [ ] `IEdFiApiClient.UpsertStudentDemographicAsync` + payload built from the 9.3 probe;
      Problem Details handled like the other writes.
- [ ] `StudentSyncService`: send demographics after the enrollment (authorization order)
      when the student has any; none -> nothing sent.
- [ ] Test fake + tests: order, omitted when empty, failure names the resource and is audited.
- [ ] Do one live write early to flush out authorization rules (Sprint 7 lesson).

### Definition of Done
- [ ] Live: demographics for at least 3 sample students visible by GET with the values
      entered in Phoenix; the sync run twice with 0 failures; build + tests green.

---

## PHX-9.5: Adult-only SIS gap analysis (planning doc)

**Estimate:** 3-4 hrs

### Tasks
- [ ] `docs\04-Planning\Adult-Only-SIS-Gap-Analysis.md` from the 2026-11-12 notes in the
      project status doc: how Texas funds an adult high school charter (enrollment-share
      tiers, outcome weights, industry-certification bonus, compensatory education via census
      block group) and the four gap tiers.
- [ ] For each gap: what Phoenix has today, the DS 6.1 / TSDS element it maps to (or "none"),
      what is unknown, a rough size, and which epic owns it.
- [ ] Verify every funding and TEDS claim against a TEA or Ed-Fi source and cite it; mark
      anything not verified as unverified.
- [ ] End with a proposed order for Sprints 10-12.

### Definition of Done
- [ ] Patrick has read it and marked the order accepted or changed.

---

## PHX-9.6: Close

**Estimate:** 2 hrs

### Tasks
- [ ] Live verification summary; retro `docs\retros\sprint-09.md`; tick these cards;
      project status doc; tag `phoenix-sprint-09`.

---

## Open questions (do not block the sprint)
- `ExitReason.NonAttendance` -> which Ed-Fi exit type (Patrick + Hailee).
- Sync result-audit failure: warn (current) or throw.
- Is the census block group entered by staff, or derived from an address? Phoenix has no
  address model yet; this sprint assumes it is entered.
- "Push data back" = TSDS or Flightdeck (open since ADR-003).
