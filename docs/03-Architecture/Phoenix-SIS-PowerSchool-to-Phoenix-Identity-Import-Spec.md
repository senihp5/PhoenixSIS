# Phoenix SIS — PowerSchool → Phoenix Identity Import Spec

**Subtitle:** StudentUniqueId continuity for the New Heights cutover
**Document Version:** 1.0 (draft)
**Date:** 2026-07-22 (CST)
**Status:** Design spec — for review. Corresponds to the Ed-Fi student-sync sprint (renumbered
Sprint 7 in `docs/04-Planning/Sprint-Plan-Amendment-v1.1.md`).
**Author:** Claude / Patrick
**Related:**
- PhoenixSIS: `docs/03-Architecture/Phoenix-SIS-Hybrid-Architecture-Design.docx` (universal StudentUniqueId),
  `docs/05-Execution/Phoenix-SIS-EdFi-ODS-Deployment-Guide.docx`
- CMS project: `2026-07-22 - ADR - SIS-Agnostic Identity - Ed-Fi-Aligned Anti-Corruption Layer.md` (the CMS-side design this spec pairs with)

---

## 1. Purpose and the one rule

When New Heights moves off PowerSchool, Phoenix imports NH's data into its Ed-Fi ODS. CMS
(the case-management overlay) is already live on the same people and has **already minted a stable
`StudentUniqueId` / `StaffUniqueId`** for each of them (in add-on mode over PowerSchool). Every
CMS-native row — advising, cases, Roll Call, ID cards, documents — is keyed on that id.

**The one rule of this import:**

> The import ADOPTS the `StudentUniqueId` CMS already assigned. It never mints a fresh Phoenix id
> for a person CMS already knows.

If that rule holds, the cutover is nearly free:
- Nothing in `CaseManagementDB` moves — every CMS-native row keeps resolving on its `StudentUniqueId`.
- Flipping CMS from `PowerSchoolSisProvider` to `EdFiSisProvider` is a deployment-config change.
- The only reconciliation is proving each person mapped 1:1 (§6).

If the rule is broken (Phoenix mints its own ids), every CMS-native table would need a re-key — the
exact pain this whole architecture exists to avoid.

## 2. Person source — read CMS's resolved identity, not raw PowerSchool

There are two ways to source the people for the import. This spec recommends Option B.

**Option A — import from raw PowerSchool, reconcile to CMS afterward.** Re-reads PS students, then
matches each back to a CMS `StudentUniqueId`. Rejected as the primary path: it re-does the identity
resolution CMS already performed, and re-exposes the DCID multi-row / per-campus dedup problems.

**Option B (recommended) — import from CMS's resolved identity.** CMS has already resolved and deduped
every person and assigned the `StudentUniqueId`, collapsed multi-campus DCIDs into one person, and
recorded campus associations. The import becomes: **materialize CMS's resolved people into Ed-Fi**,
carrying their `StudentUniqueId` verbatim. Demographic detail (name, DOB, race/sex, contact) comes
from CMS's resolved cache (`Core_Student`), which itself was populated from PowerSchool, or from a
targeted PS pull if a field CMS doesn't cache is required for PEIMS.

Consequence: the import's system-of-record for **identity and enrollment structure** is the CMS
crosswalk/cache; PowerSchool is at most a demographic-detail source. This keeps one resolution engine,
not two.

## 3. Inputs and preconditions

Before running the import:
- **CMS identity seed available** — the crosswalk `Core_ExternalIdentity` + `Core_Student` /
  `Core_Staff` + the association tables are populated and the CMS merge/review queue is empty (no
  unresolved ambiguous people). See §4 for the exact seed contract.
- **Phoenix Ed-Fi ODS stood up** — v7.3 / Data Standard 5.2, Texas descriptors loaded, NH
  `LocalEducationAgency` + `School` (Stop Six, McCart) created, an OAuth API client provisioned.
- **Tenant code agreed** — `NHTS`, matching the `PHX-NHTS-{seq}` prefix CMS mints.
- **Import window** — CMS identity writes are frozen or snapshotted so the seed does not move mid-import
  (DCID/enrollment can change under a live sync).

## 4. The identity-seed contract (what CMS exposes)

CMS exposes a read-only, point-in-time seed the import consumes. A view is the simplest handoff.

```sql
CREATE VIEW dbo.vw_PhoenixIdentitySeed_Student AS
SELECT
    s.StudentUniqueId,
    s.StateStudentId,
    s.StudentNumber,
    s.FirstName,
    s.LastName,
    s.BirthDate,
    s.IsActive
FROM dbo.Core_Student AS s;
```

```sql
CREATE VIEW dbo.vw_PhoenixIdentitySeed_StudentSchool AS
SELECT
    s.StudentUniqueId,
    c.CampusCode,
    a.EntryDate,
    a.ExitDate,
    a.EnrollmentStatus,
    a.IsActive
FROM dbo.Core_StudentSchoolAssociation AS a
JOIN dbo.Core_Student AS s ON s.StudentId = a.StudentId
JOIN dbo.Campus AS c ON c.CampusId = a.CampusId;
```

```sql
CREATE VIEW dbo.vw_PhoenixIdentitySeed_Xref AS
SELECT
    x.EntityType,
    s.StudentUniqueId,
    x.SourceSystem,
    x.IdType,
    x.IdValue
FROM dbo.Core_ExternalIdentity AS x
JOIN dbo.Core_Student AS s ON s.StudentId = x.InternalId
WHERE x.EntityType = 'Student';
```

Staff seed follows the same shape over `Core_Staff` / `Core_StaffEdOrgAssignment` and
`StaffUniqueId`. The **match rule** used to build these ids (StateId → StudentNumber+DOB →
name+DOB→review for students; StateStaffId/TeacherNumber → EntraObjectId → Email → name→review for
staff) is documented in the CMS ADR §5 and MUST be the rule the import reuses if it ever has to
resolve a person not already in the seed (§6, delta case).

## 5. Import algorithm

### 5.1 Student

For each row in `vw_PhoenixIdentitySeed_Student`:

Step 1 — create the Ed-Fi Student with the CMS id as the natural key. Ed-Fi lets you set
`studentUniqueId` on create; that is exactly the seam that preserves continuity.

```json
POST /data/v3/ed-fi/students
{
  "studentUniqueId": "PHX-NHTS-000001176",
  "firstName": "Jordan",
  "lastSurname": "Rivera",
  "birthDate": "1998-04-12"
}
```

Step 2 — create the demographics/enrollment-org association.

```json
POST /data/v3/ed-fi/studentEducationOrganizationAssociations
{
  "studentReference": { "studentUniqueId": "PHX-NHTS-000001176" },
  "educationOrganizationReference": { "educationOrganizationId": 255901 }
}
```

Step 3 — create one `studentSchoolAssociation` per campus row in
`vw_PhoenixIdentitySeed_StudentSchool` for that `StudentUniqueId`. A multi-campus person becomes ONE
Ed-Fi Student with N school associations — never N students.

```json
POST /data/v3/ed-fi/studentSchoolAssociations
{
  "studentReference": { "studentUniqueId": "PHX-NHTS-000001176" },
  "schoolReference": { "schoolId": 255901001 },
  "entryDate": "2025-08-18",
  "entryGradeLevelDescriptor": "uri://ed-fi.org/GradeLevelDescriptor#Adult Education"
}
```

Step 4 — the Ed-Fi ODS assigns its own internal `StudentUSI` (an int surrogate) and returns it. That
USI is ODS-internal and is NOT propagated to CMS. CMS joins on `StudentUniqueId`, which is unchanged,
so no CMS write is needed. Optionally record `StudentUSI ↔ StudentUniqueId` in a Phoenix-side map for
Phoenix's own convenience.

### 5.2 Staff

Same pattern: `POST /data/v3/ed-fi/staffs` with `staffUniqueId` = the CMS `StaffUniqueId`, then one
`staffEducationOrganizationAssignmentAssociation` per campus assignment from the staff seed.

### 5.3 Notes

- Exact resource/field names must be confirmed against the deployed **DS 5.2** metadata (`/metadata`
  / Swagger). The `studentUniqueId` / `staffUniqueId` settable-natural-key mechanism is the load-bearing
  assumption; verify it first on one record before bulk load.
- **New Heights students are adults** — do not import guardian/parent `studentContactAssociation`
  records or add minor/guardian branching. (Standing NH rule.)
- Prefer the Ed-Fi API for the load (it enforces referential integrity and descriptors). If volume
  makes the API too slow, Ed-Fi's bulk-load (`/data/v3` with a loader, or the Bulk Load Client) is the
  fallback — still setting `studentUniqueId` explicitly.

## 6. Edge cases

- **Multi-campus person** — one Ed-Fi Student + N `studentSchoolAssociation`. This is the whole point;
  the CMS seed already collapsed the DCIDs, so the seed yields one student row and multiple school rows.
- **Ambiguous identity** — anyone still in the CMS merge/review queue is NOT imported. Resolve in CMS
  first; the seed must be clean. Importing an ambiguous person risks two Ed-Fi students for one human.
- **Person created in CMS after the seed snapshot (delta)** — re-export the seed and import the delta,
  using the same match rule. Do not let the import mint; if a person truly has no CMS `StudentUniqueId`
  yet, mint it in CMS first, then import.
- **Termed / inactive person** — decide per policy: import as inactive (historical continuity for
  advising records) vs skip. Recommend import inactive so CMS-native history still resolves post-cutover.
- **`StudentUniqueId` length/format** — `PHX-{tenant}-{seq}` fits `VARCHAR(32)`; confirm Ed-Fi's
  `studentUniqueId` max length in DS 5.2 accepts it (Ed-Fi allows up to 32).
- **DCID churn during the window** — because identity comes from the frozen CMS seed, in-window DCID
  changes in PowerSchool do not affect the import. This is a benefit of Option B (§2).

## 7. The 1:1 verification (gate before cutover)

The import is not "done" until every check passes.

```sql
SELECT COUNT(*) AS CmsActiveStudents
FROM dbo.vw_PhoenixIdentitySeed_Student
WHERE IsActive = 1;
```

Compare to the Ed-Fi student count for the NH LEA (via API `totalCount` or an ODS query). Then:

- **No orphan CMS ids** — every seed `StudentUniqueId` exists as exactly one `edfi.Student`.
- **No extra Ed-Fi students** — no `edfi.Student` whose `studentUniqueId` is absent from the seed.
- **No collisions** — `studentUniqueId` is unique in Ed-Fi (enforced), but confirm the natural-key
  match didn't merge two distinct humans upstream (spot-check a sample against PS).
- **Association counts reconcile** — sum of `studentSchoolAssociation` == sum of seed campus rows.
- **Staff** — same four checks over `StaffUniqueId`.

Record the counts and the diff (should be empty) as the cutover sign-off artifact.

## 8. Cutover sequence (ties to CMS ADR Phase 4)

Step 1 — freeze CMS identity writes / snapshot the seed.
Step 2 — run the import (§5).
Step 3 — run verification (§7); do not proceed unless the diff is empty.
Step 4 — in CMS, flip `ActiveSisProvider` from `PowerSchool` to `EdFi` and point it at Phoenix's Ed-Fi
API (deployment config only — no code change, no data move).
Step 5 — smoke test: open several student profiles and Roll Call / advising records in CMS; confirm
each resolves through `EdFiSisProvider` on its unchanged `StudentUniqueId`.
Step 6 — unfreeze.

**Rollback** — because no CMS-native data moved, rollback is: flip `ActiveSisProvider` back to
`PowerSchool`. The Ed-Fi ODS can be dropped and reloaded from a corrected seed without touching
`CaseManagementDB`.

## 9. What explicitly does NOT change

- `CaseManagementDB` — no rows move, no keys change. Advising, cases, Roll Call, ID cards, documents,
  audit all keep resolving on their existing `StudentUniqueId`.
- The `StudentUniqueId` values themselves — minted once by CMS, adopted by Phoenix, owned by Phoenix
  thereafter.
- Only two things change: which provider CMS runs (`PowerSchool` → `EdFi`) and where the person's
  system-of-record lives (PowerSchool → Phoenix Ed-Fi ODS).

## 10. Coordination checklist (CMS side ↔ Phoenix side)

CMS side (this repo's sibling, `CaseManagementSystem`):
- [ ] Expose `vw_PhoenixIdentitySeed_*` (or an equivalent export).
- [ ] Empty the merge/review queue before the seed snapshot.
- [ ] Freeze identity minting during the cutover window.
- [ ] Publish the natural-key match rule (from the CMS ADR §5).

Phoenix side (this repo):
- [ ] Import consumes the seed and sets `studentUniqueId`/`staffUniqueId` explicitly — never mints.
- [ ] One Student + N school associations for multi-campus people.
- [ ] Run and archive the §7 verification diff.

Shared / agreed:
- [ ] Same natural-key match rule on both sides.
- [ ] Same `StudentUniqueId` values (import reads CMS's crosswalk as the identity seed).
- [ ] Tenant code `NHTS`.

## 11. Open decisions

- **Person source** — confirm Option B (import from CMS resolved identity), §2. Recommended.
- **Integration surface** — the running CMS↔Phoenix link uses the standard **Ed-Fi ODS/API** (so the
  same `EdFiSisProvider` works against any Ed-Fi ODS), reserving a Phoenix-app API only for adult-ed
  extensions Ed-Fi doesn't model. (Mirrors the CMS ADR §3 open item.)
- **Termed-person policy** — import inactive vs skip (§6). Recommend import inactive.
- **Load mechanism** — Ed-Fi API vs Bulk Load Client, decided by measured volume (§5.3).
- **Demographic detail source** — CMS cache vs targeted PS pull for any PEIMS field CMS doesn't cache.
