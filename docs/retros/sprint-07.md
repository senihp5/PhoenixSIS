# Sprint 7 Retrospective - First Phoenix -> Ed-Fi Student Sync (DS 6.1)

**Sprint window:** 2026-11-20 (single session, after on-site hardening #1/#2/#3/#6)
**Demo:** admin/edfi/sync -> Preview -> Run: 13 Phoenix sample students land in the
Ed-Fi ODS at New Heights campus 220822001 for school year 2026-27
**Target:** Ed-Fi ODS/API 7.3 + Data Standard 6.1 - the stack TSDS moves to in SY 2027-28
(Amendment v1.5)

## Stories completed

| Story | Description | Commit | Status |
|---|---|---|---|
| PHX-7.0 | Sandbox client granted LEA 220822; SY 2027 calendar + Fall/Spring sessions seeded; DS 6.1 descriptor URIs documented | db66d67 | Done |
| PHX-7.1 | Ed-Fi write client: Student, StudentSchoolAssociation, StudentEducationOrganizationAssociation, StudentIdentificationCode; RFC 7807 Problem Details parsing; transient-only retry; SSN guard | 582b08b | Done |
| PHX-7.2 | `IStudentSyncService`: dry run / apply, PHX-NNNNNN Ed-Fi ids as mapped identifiers, per-student failure capture | 767d090 | Done |
| PHX-7.3 | `admin/edfi/sync` page: preview-then-run, results with Problem Details + correlationId | 0159cf6 | Done |
| PHX-7.4 | Live sync, two authorization fixes, verification, docs, close | 4fd3c1c, 0f83389, close commit | Done |

## Live verification (2026-11-20, sample data only)

- Phoenix: 13 students (5 Sample + 8 Texas sample), each with exactly one `Ed-Fi`
  identifier, PHX-000001..PHX-000013.
- Ed-Fi, checked per student by GET (not by reading the page): exactly 1 Student,
  1 StudentSchoolAssociation (school 220822001, calendar 220822001-2027, school year 2027,
  FTE 1.00, primarySchool true, entry 2026-08-17), 1 StudentEducationOrganizationAssociation
  (LEA 220822), 1 StudentIdentificationCode (Local = PHX number, edOrg 220822). No SSN codes.
- ODS student total 1959 -> 1972 (exactly +13; no duplicates).
- Clean re-run: 13 Updated, 52/52 writes 200, no new identifiers.
- Forced failure (calendar `NOPE`): 13 Failed on studentSchoolAssociations, 409
  `urn:ed-fi:api:data-conflict:unresolved-reference`, detail + correlationId shown per row.
  Next run with the real calendar: 13 Updated, 0 failed (failures self-heal).
- Tests: 43/43 (unit + architecture). The admin verified the sync page end to end in the
  browser.

## 1. What went well?

- **Probing the live API before writing code** paid off again. The OpenAPI gave the exact
  DS 6.1 shapes (StudentIdentificationCode is its own resource; demographics left Student),
  and two deliberately rejected writes gave real error bodies. That is how the parser
  learned that ODS/API 7.3 sends Problem Details as `application/json` and that 400s carry
  a `validationErrors` map the card never mentioned.
- **The probe also caught a blocker before any code ran:** the sandbox API client only
  covered the Ed-Fi sample LEA 255901. `Add-EdfiClientEdOrg.ps1` fixed it in EdFi_Admin,
  verified through `/oauth/token_info`.
- **Legible errors made the live failures cheap.** Both authorization failures were
  diagnosed straight from the page: the Ed-Fi hint text named the fix.
- **Recording the PHX number before the writes** meant the two failed runs left no orphan
  Ed-Fi records under abandoned numbers; the recovery run reused PHX-000001..13.

## 2. What was harder than expected?

- **Ed-Fi relationship-based authorization, twice.** The card's write order was wrong:
  1. Run #1: the LEA association (and identification code) is 403 until the student is
     enrolled in one of the client's schools. Order fixed to Student -> SSA -> SEOA -> SIC.
  2. Run #2: creating a NEW student is allowed, but UPDATING an existing student is 403
     until they are enrolled. Run #1 had left 13 students created but unenrolled. The sync
     now enrolls first when the student POST is 403, then re-writes the student.
- The unit-test fake did not model either rule, so 42 green tests did not catch them. The
  fake now enforces both, and a dedicated test covers the recovery path.

## 3. What did I learn?

- With the Ed-Fi Sandbox claim set, **the StudentSchoolAssociation is the key that unlocks
  everything else about a student.** Create the student, enroll immediately, then write
  the rest. Sprint 3 found the read side of this rule; Sprint 7 found the write side.
- A test fake is only as good as the rules it models. Where the real system enforces
  authorization or ordering, model it in the fake, or the tests prove only the happy path.
- `git grep` without `--cached` skips untracked new files: run the secret check after
  `git add`.

## 4. What will I do differently next sprint?

- When a story writes to Ed-Fi, do a one-record live write early (before building the
  service around it) to flush out authorization rules, then encode them in the fake.
- Keep each live run's evidence (summary + correlationIds) in the retro, not only in chat.

## 5. Open items / follow-ups

- **Real New Heights data stays blocked:** the data-sharing authorization (ADR-003 gate b)
  is drafted and parked by Patrick; the written IP clarification (#7) is still open.
  Sample data only.
- **Record-access audit logging** is promised in the draft authorization and not yet built:
  it must exist before any real data loads.
- **Sync scope:** demographics, contacts and directory (DS 6.1 StudentDemographic incl.
  census block group, StudentDirectory, Contact) need Phoenix fields first. Exit/withdraw
  is in the client but not driven by Phoenix yet.
- **Real enrollment dates:** every student uses the configured entry date; Phoenix needs an
  enrollment model before entry/exit dates can come from it.
- **TX extension + Azure-hosted TEA SDK:** deferred to SDK 2028.0.0 (Jan/Feb 2027).
- **Ed-Fi readiness call** with Nathan Gandomi: book for the last week of January 2027.
- **Hardening leftovers:** #9 `decisioned-admin` password rotation (deferred), #5 Entra
  client secret rotation (~2027-01).
- **WebApi redeploy pitfall:** re-run `Set-EdfiWebApi64Bit.ps1 -Apply` after any redeploy.
