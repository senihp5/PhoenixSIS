# Sprint 9: Multi-Identifier Sources, Addresses + Adult Demographics

**Revision:** v2, 2026-11-23 (Patrick added addresses + automatic block-group lookup; 9.0 done; demographics are not core Ed-Fi 6.1)
**Sprint Goal:** A source record can carry every identifier the source knows for a student,
so the state ID reaches Ed-Fi as its own identification code. Phoenix holds student
addresses and sends them to Ed-Fi. Phoenix holds the two facts Texas adult high school
funding depends on - census block group (looked up from the address) and previous
attendance as an adult - ready for the TX extension. The adult-only gap analysis becomes a planning doc
that later sprints are cut from.

**Total Estimated Hours:** 34-46
**Epics:** Epic 4 - Data Migration & Clean-Room Identity (9.1, 9.2); Epic 3 - EdFi
Integration (9.2, 9.4); Epic 5 - AdultEd (9.5, 9.6)

**Modeling decisions (defaults - Patrick may change before the story starts):**
- **Identifiers stay mapped rows (ADR-002).** `SourceStudentRecord` gains a list of extra
  identifiers `(Kind, Value)`. `ExternalId` remains the source's primary key and the only
  value used to match on re-import. Extra identifiers are stored as `StudentIdentifier` rows
  with source system `<Source>:<Kind>` (for example `Flightdeck:StateStudentNumber`). No new
  columns on `Student`.
- **State ID is not an SSN.** Only the TSDS Unique ID is sent to Ed-Fi as the "State"
  identification code. The existing SSN guard stays. Which source kind is the state ID is
  config (`EdFiApi:StateIdentifierSource`), default `Flightdeck:StateStudentNumber`.
- **Addresses and demographics are core domain, Phoenix-native names.** Ed-Fi shapes are
  mapped at the EdFi boundary only. Addresses keep history (start / end dates); nothing is
  deleted. The census block group is looked up from the physical address and can be
  overridden by hand.
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
- [x] Run the sync once more live (Preview, Run). DONE 2026-11-23 18:33 UTC: 19 updated,
      0 failed; PHX-000001 "updated in Ed-Fi" with its only enrollment exited; verified by
      SQL (19 send-started + 19 result rows) and Ed-Fi GET (19 SSAs, one exited).
- [x] Not needed - no 403. (Was: if PHX-000001 fails with 403 on students, add the rule to the sync (skip the student
      write when every matching enrollment is exited and the student already exists, or the
      order the live API accepts), model it in the test fake, re-run.)

### Definition of Done
- [x] Result recorded in `docs\features\modules\EdFi.md`; build + tests green.

---

## PHX-9.1: Multi-identifier source records (task #36)

**Estimate:** 5-7 hrs

### Tasks
- [x] Core `Phoenix.Core\Migration\ISisSourceConnector.cs`: `SourceIdentifier(Kind, Value)`;
      `SourceStudentRecord` gains `IReadOnlyList<SourceIdentifier> Identifiers` with an empty
      default, so the Sample and TexasSample connectors compile unchanged.
- [x] `IStudentIdentityService`: one call that resolves or creates by the primary id and
      adds any missing extra identifiers in the same save (each audited; a value already
      mapped to a different student is a reported conflict, never a silent re-point).
- [x] `MigrationService` + wizard preview: show how many identifiers will be added and list
      conflicts before Apply.
- [x] `FlightdeckSourceConnector` (sample): emit `StudentNumber`, `PowerSchoolStudentID` and
      `StateStudentNumber` for the 6 sample students (made-up values, 10-digit state ids).
- [x] Tests: re-import adds only missing identifiers; conflict reported; no duplicates;
      audit rows written; existing 88 tests unchanged.

**Built 2026-11-23 (code + tests; NOT yet run live).** Differences from the tasks: no new identity
call - the migration uses the existing `AddIdentifierAsync` per identifier (each its own audited
save); a blank value is ignored and a repeated kind keeps its first value. The wizard shows the
count per row and names conflicting kinds. Live check owed: restart the app (it was running the old
build), Data Migration -> Flightdeck -> Preview (expect 6 would match, 18 to add) -> Apply, then open
one of the six students and see four identifiers.

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

## PHX-9.3: Student addresses in Phoenix

**Estimate:** 6-8 hrs

### Tasks
- [ ] Core `Phoenix.Core\Students`: `StudentAddressRecord`, `AddressKind` (Physical, Mailing),
      `IStudentAddressService` (`GetForStudentAsync`, `AddAsync`, `EndAsync`). Fields follow
      what DS 6.1 requires (probed 2026-11-23: address type, street number and name, city,
      state, postal code) plus apartment / suite, county, latitude, longitude, start date
      and end date.
- [ ] Rules: one current address per kind per student; a new one ends the old one (history
      is kept, nothing is deleted); state is a 2-letter code; postal code is 5 or 9 digits.
- [ ] Infrastructure: `StudentAddress` entity + migration `AddStudentAddress` (FK NO ACTION);
      every add and end audited in the same save. Audit summaries carry the kind only -
      never the address.
- [ ] `StudentRecordPage.razor`: an Addresses card (current + history, add, end) behind the
      `Students` flag.
- [ ] `SourceStudentRecord` may carry addresses; the Flightdeck sample emits one made-up
      Fort Worth address per student. Tests + `docs\features\modules\Students.md`.

### Definition of Done
- [ ] Addresses persist with rules enforced and audited; build + tests green.

---

## PHX-9.4: Addresses in the Ed-Fi sync (StudentDirectory)

**Estimate:** 4-6 hrs

### Tasks
- [ ] Probe live: `addressTypeDescriptors`, `stateAbbreviationDescriptors`, and whether
      `studentDirectories` needs the enrollment first (authorization order). Record in
      `docs\edfi\texas-sample.md`.
- [ ] `IEdFiApiClient.UpsertStudentDirectoryAsync` + payload (natural key: student +
      education organization; addresses with their periods). Problem Details as elsewhere.
- [ ] `StudentSyncService`: send the directory after the enrollment when the student has a
      current address; none -> nothing sent.
- [ ] Test fake + tests: order, omitted when empty, failure names the resource and is audited.
- [ ] One live write early to flush out authorization rules (Sprint 7 lesson).

### Definition of Done
- [ ] Live: the 6 Flightdeck students have a StudentDirectory with one address, verified by
      GET; the sync run twice with 0 failures; build + tests green.

---

## PHX-9.5: Adult demographics + automatic census block group

**Estimate:** 7-10 hrs

**Finding (probed 2026-11-23):** census block group and adult previous attendance are NOT in
core Ed-Fi 6.1. nh-edfi-cus exposes one data model (Ed-Fi 6.1.0, 1223 schemas) and none
mentions either. They are Texas elements and arrive with the TX extension (SDK 2028.0.0,
Jan/Feb 2027). So this story stores them in Phoenix and does not send them yet.

### Tasks
- [ ] Core: `StudentDemographicsRecord` (`CensusBlockGroup` 12 digits, where it came from
      (Lookup / Manual), when it was looked up; `AttendedPreviouslyAsAdult` yes / no /
      unknown), `IStudentDemographicsService`.
- [ ] Core `ICensusGeocoder` + Infrastructure implementation against the U.S. Census Bureau
      geocoder (public, no key). Probe the live response first and record the request and
      the field that holds the block group. Timeouts and "no match" are results, not errors.
- [ ] Lookup runs when a physical address is added or changed and on a "Look up" button; a
      manual value is never overwritten without a confirm. Behind a `Geocoding` config
      switch, default OFF in base and ON in Phoenix-Dev.
- [ ] Infrastructure: `StudentDemographics` entity + migration (1:1 with Student); saves
      audited; the lookup itself is audited (that an address left Phoenix, not the address).
- [ ] Demographics card on `StudentRecordPage.razor`. Tests use a fake geocoder.
- [ ] `docs\features\modules\Students.md` + a note in ADR-003: a lookup sends the student's
      address to a federal service, so it must be named in the data-sharing authorization
      before it is used on real data.

### Definition of Done
- [ ] Adding a physical address for a sample student fills the block group; a manual
      override sticks; build + tests green. Sending both elements to Ed-Fi is deferred to
      the TX extension layer.

---
## PHX-9.6: Adult-only SIS gap analysis (planning doc)

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

## PHX-9.7: Close

**Estimate:** 2 hrs

### Tasks
- [ ] Live verification summary; retro `docs\retros\sprint-09.md`; tick these cards;
      project status doc; tag `phoenix-sprint-09`.

---

## Open questions (do not block the sprint)
- `ExitReason.NonAttendance` -> which Ed-Fi exit type (Patrick + Hailee).
- Sync result-audit failure: warn (current) or throw.
- Phone, email and contacts are still missing from the student record (DS 6.1 carries them
  on StudentDirectory and Contact). Not in this sprint.
- Which education organization owns the StudentDirectory - the LEA or the campus? Default: LEA.
- "Push data back" = TSDS or Flightdeck (open since ADR-003).
