# Texas-Shaped Sample Data

**Sprint:** 6 (PHX-6.2–6.3), 2026-11-06
**See also:** `docs\integrations\teds-gap-analysis.md` (what this is NOT — real TEDS),
`docs\integrations\texas-tsds-requirements.md`.

Sprint 6 makes the prototype read as a New Heights adult-high-school scenario on
both ends of the (future) Phoenix → Ed-Fi sync. **Sample data only.**

## Ed-Fi ODS reference data (the sync target)

`_tools\Seed-EdfiReference.ps1` — run from the workstation **on the VPN** (uses the
sandbox key/secret in `C:\temp\edfi-sandbox.ps1`; since hardening #1 on 2026-11-20 the
Ed-Fi API is private-endpoint only). Idempotent (Ed-Fi POST is upsert by
natural key; re-run returns HTTP 200, no duplicates).

Creates:

| Resource | Natural key | Shape |
|---|---|---|
| LocalEducationAgency | `localEducationAgencyId` **220822** | "New Heights", category **Charter**, Fort Worth / Tarrant |
| School | `schoolId` **220822001** | "New Heights Adult High School", category **Adult School**, grade level **Adult Education**, under LEA 220822 |
| Calendar (PHX-7.0) | `calendarCode` **220822001-2027**, school, school year 2027 | type **School**, grade level **Adult Education** |
| Session (PHX-7.0) | `sessionName` **2026-2027 Fall Semester**, school, school year 2027 | Fall Semester, 2026-08-17..2026-12-18, 80 instructional days (sample dates) |
| Session (PHX-7.0) | `sessionName` **2026-2027 Spring Semester**, school, school year 2027 | Spring Semester, 2027-01-05..2027-05-28, 95 instructional days (sample dates) |

`220822` is New Heights' real TEA county-district number; `220822001` is a campus
under it. Descriptors referenced are the **national** Ed-Fi values already in the
ODS (`uri://ed-fi.org/...`). The seed deliberately does **not** create
`uri://tea.texas.gov` descriptors — Texas namespaces/extensions are a real-TEDS,
TEA-platform concern (gap doc §2).

Run:

```powershell
F:\Github\PhoenixSIS\_tools\Seed-EdfiReference.ps1
```

### PHX-7.0 prerequisite: the API client must cover LEA 220822

The sandbox API client was provisioned for the Ed-Fi sample LEA **255901** only
(`/oauth/token_info` 2026-11-20: 255901 + its schools/department; no 220822). The
Ed-Fi Sandbox claim set authorizes calendars, sessions, studentSchoolAssociations etc.
by relationship to the caller's EdOrgs, so every enrollment-side write for school
220822001 returns **403** until LEA 220822 is added:

```powershell
F:\Github\PhoenixSIS\_tools\Add-EdfiClientEdOrg.ps1 -Apply
```

It adds `ApplicationEducationOrganizations` + `ApiClientApplicationEducationOrganizations`
rows in EdFi_Admin (scoped to the one ApiClient whose key is in `C:\temp\edfi-sandbox.ps1`)
and verifies a fresh token. `New-EdfiSandbox.ps1` now defaults to `-EdOrgIds 255901,220822`,
so a rebuilt sandbox includes it.

### Descriptor URIs for Sprint 7 (probed live 2026-11-20, ODS/API 7.3 / DS 6.1)

All are national `uri://ed-fi.org` values present in the sandbox ODS.

| Use | Descriptor URI |
|---|---|
| School year | `schoolYearTypeReference.schoolYear` **2027** (= 2026-2027; 1991..2040 exist; sample data flags 2026 as current) |
| Calendar type | `uri://ed-fi.org/CalendarTypeDescriptor#School` |
| Session terms | `uri://ed-fi.org/TermDescriptor#Fall Semester`, `#Spring Semester` (also Semester, Summer Semester, quarters, trimesters, Year Round, Other) |
| SSA enrollment type | `uri://ed-fi.org/EnrollmentTypeDescriptor#Current` (also Summer, Preregistered) |
| SSA entry grade level | `uri://ed-fi.org/GradeLevelDescriptor#Adult Education` (also Twelfth grade, Ungraded) |
| Phoenix ID (StudentIdentificationCode) | `uri://ed-fi.org/StudentIdentificationSystemDescriptor#Local` |
| TSDS Unique ID (later, real data only) | `uri://ed-fi.org/StudentIdentificationSystemDescriptor#State` |
| Never used by Phoenix | `uri://ed-fi.org/StudentIdentificationSystemDescriptor#SSN` |

Required fields (from the live OpenAPI): calendar = calendarCode, calendarTypeDescriptor,
schoolReference, schoolYearTypeReference; session = sessionName, termDescriptor, beginDate,
endDate, totalInstructionalDays, schoolReference, schoolYearTypeReference.

PS 5.1 gotcha seen in this probe: `@(Invoke-RestMethod ...).Count` printed **1** for an
empty `[]` result. Assign first (`$x = Invoke-RestMethod ...; $x.Count`) or read the
`Total-Count` header with `totalCount=true`.

## Phoenix source data (the sync source)

`TexasSampleSourceConnector` (`ISisSourceConnector`, SourceSystem `TexasSample`) —
8 adult-HS students with realistic names/DOBs and `NH-`prefixed source IDs. Import
them through the Data Migration wizard (`admin/migration`): pick **Texas adult-HS
sample**, Preview, then Apply. Idempotent — re-apply matches instead of duplicating.
They land as canonical Phoenix students with `TexasSample` identifiers, ready for the
Sprint 7 sync.

## How this maps toward real TEDS

- **Matches TEDS in shape:** Ed-Fi education-organization hierarchy (LEA → School),
  Texas-style county-district/campus identifiers, a charter/adult-education scenario.
- **Differs from real TEDS:** national (`uri://ed-fi.org`) descriptors instead of
  Texas namespaces; no TEDS extensions; no PEIMS collections; our ODS is Ed-Fi
  7.3/DS 6.1 core only (TSDS SY 2027-28 moves to the same 7.3/DS 6.1 + the TX
  extension, Amendment v1.5); no submission/validation. The honest delta is in
  `teds-gap-analysis.md` (the vendor-registration blocker was resolved in §5 —
  TEA has no vendor registration).

## Enrollment descriptors (probed live 2026-11-20, PHX-8.4)

Read from nh-edfi-cus (`/data/v3/ed-fi/...Descriptors`). The sync maps Phoenix codes to
these at the EdFi boundary; defaults live in `EdFiApiOptions` and each key can be
overridden in config (`EdFiApi:GradeLevelDescriptors:<code>` and so on).

| Phoenix code | Ed-Fi descriptor URI |
|---|---|
| GradeLevel.AdultEducation | `uri://ed-fi.org/GradeLevelDescriptor#Adult Education` |
| GradeLevel.Grade09 | `uri://ed-fi.org/GradeLevelDescriptor#Ninth grade` |
| GradeLevel.Grade10 | `uri://ed-fi.org/GradeLevelDescriptor#Tenth grade` |
| GradeLevel.Grade11 | `uri://ed-fi.org/GradeLevelDescriptor#Eleventh grade` |
| GradeLevel.Grade12 | `uri://ed-fi.org/GradeLevelDescriptor#Twelfth grade` |
| GradeLevel.Ungraded | `uri://ed-fi.org/GradeLevelDescriptor#Ungraded` |
| EnrollmentKind.Regular | `uri://ed-fi.org/EnrollmentTypeDescriptor#Current` |
| EnrollmentKind.Summer | `uri://ed-fi.org/EnrollmentTypeDescriptor#Summer` |
| EnrollmentKind.Preregistered | `uri://ed-fi.org/EnrollmentTypeDescriptor#Preregistered` |
| ExitReason.Graduated | `uri://ed-fi.org/ExitWithdrawTypeDescriptor#Graduated` |
| ExitReason.TransferredOut | `uri://ed-fi.org/ExitWithdrawTypeDescriptor#Transferred` |
| ExitReason.Withdrew | `uri://ed-fi.org/ExitWithdrawTypeDescriptor#Withdrawn` |
| ExitReason.NonAttendance | `uri://ed-fi.org/ExitWithdrawTypeDescriptor#Withdrawn` (no exact core value) |
| ExitReason.Other | `uri://ed-fi.org/ExitWithdrawTypeDescriptor#Other` |

The ODS has 3 enrollment types (all mapped) and 15 exit/withdraw types. Unused exit types:
Completed, Died or is permanently incapacitated, Dropout, End of school year, Enrolled in
a high school diploma program, Expelled, Incarcerated, Invalid enrollment, Involuntarily
Removed, No show, Reached maximum age. "No show" means the student never attended, so it is
not used for NonAttendance. Texas leaver codes replace this mapping with the TX extension.

## Reset / re-run

Both seeders are idempotent and safe to re-run. If the Ed-Fi sandbox is rebuilt
(`_tools\Reset-EdfiSandbox.ps1`), re-run `Seed-EdfiReference.ps1` afterward to
recreate the org, calendar and sessions. The rebuilt client already covers LEA 220822
(new `New-EdfiSandbox.ps1` default); `Add-EdfiClientEdOrg.ps1` in report mode confirms it.
