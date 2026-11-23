# EdFi Module

**Flag:** `EdFi` (in `Features`, default OFF in base `appsettings.json`; ON in the `Phoenix-Dev` overlay)
**Born:** Sprint 4 (PHX-4.1–4.5), 2026-11-05
**Epic:** Epic 3 — EdFi Integration Module

## Purpose

Lets the Phoenix application talk to the Ed-Fi ODS/API. Sprint 4 delivers the
scaffold, a typed OAuth-authenticating API client, and two admin pages that
read from the connected ODS. Writing data back (student sync) comes in a later
sprint.

## Shape (follows MODULE_TEMPLATE, Phase 1 — folders)

```
src\Phoenix.Core\Modules\EdFi\        contracts: IEdFiApiClient, EdFiApiInfo, EdFiStudentSummary,
                                     EdFiStudentWrite, EdFiEnrollmentWrite, EdFiStudentEdOrgWrite,
                                     EdFiStudentIdentificationCodeWrite, EdFiWriteResult, EdFiProblem
src\Phoenix.Infrastructure\EdFi\      EdFiModule, EdFiApiOptions, EdFiApiClient,
                                     EdFiWritePayloads (internal), EdFiProblemParser (internal)
src\Phoenix.Web\Components\Pages\EdFi\ Health.razor, Students.razor, Sync.razor
```

`EdFiModule : IModule` registers the client; one line in `ModuleBootstrapper.Modules`.

## Configuration

Two separate things — don't confuse them:

- **`Features:EdFi`** — the feature flag that gates the module.
- **`EdFiApi`** — the API client's connection settings:

```json
"EdFiApi": {
  "BaseUrl": "https://nh-edfi-cus.azurewebsites.net"
}
```

`BaseUrl` lives in `appsettings.json`. **Key and Secret are secrets** and live in
dotnet user-secrets locally (App Service settings / Key Vault when deployed) —
never in the repo:

```
dotnet user-secrets set "EdFiApi:Key" <sandbox-key>      --project src\Phoenix.Web
dotnet user-secrets set "EdFiApi:Secret" <sandbox-secret> --project src\Phoenix.Web
```

The sandbox key/secret are produced by `_tools\New-EdfiSandbox.ps1` into
`C:\temp\edfi-sandbox.ps1`.

## API client (`IEdFiApiClient`)

- `GetApiInfoAsync()` — reads the anonymous discovery endpoint (version, build,
  data models). Proves reachability.
- `GetAccessTokenAsync()` — OAuth client-credentials; caches the bearer token
  until ~60s before expiry (thread-safe). Proves authentication.
- `GetStudentsAsync(limit)` — bearer-authenticated read of `/data/v3/ed-fi/students`.

### Write path (PHX-7.1, DS 6.1)

| Method | Resource (POST = upsert by natural key) | Natural key |
|---|---|---|
| `UpsertStudentAsync` | `students` | studentUniqueId |
| `UpsertStudentSchoolAssociationAsync` | `studentSchoolAssociations` | student, school, entryDate |
| `UpsertStudentEducationOrganizationAssociationAsync` | `studentEducationOrganizationAssociations` | student, education organization |
| `UpsertStudentIdentificationCodeAsync` | `studentIdentificationCodes` | student, education organization, identification system |

- Returns `EdFiWriteResult` (resource, success, status, attempts, `EdFiProblem?`); API
  errors never throw. 201 = created, 200 = updated.
- `EdFiProblem` = RFC 7807 Problem Details as ODS/API 7.3 sends it (`application/json`):
  `type`, `title`, `status`, `detail`, `correlationId`, `errors[]` (401/403) and
  `validationErrors` keyed by JSON path (400, e.g. `$.firstName`). A 409 unresolved
  reference is `urn:ed-fi:api:data-conflict:unresolved-reference`. A non-JSON body (the
  cold-start page) becomes a problem with the status and a length note, never the HTML.
- Retries: cold-start 404, 5xx, timeouts and transport failures, up to
  `EdFiApi:WriteRetryCount` (default 3) with `EdFiApi:WriteRetryDelayMs` (default 2000).
  400/403/409 are never retried. A 401 refreshes the cached token once.
- **SSN guard:** `UpsertStudentIdentificationCodeAsync` throws before any network call for
  the `#SSN` identification system. Phoenix never sends Social Security numbers.
- Logs carry resource, status, problem type and correlation id only — never the body.
- Write order matters: student first (a missing student is a 409), then the enrollment, then
  the other student records (403 until the enrollment exists). Updating an existing student
  is also 403 until they are enrolled.

### Student sync (PHX-7.2) — `IStudentSyncService`

`SyncAllAsync(EdFiSyncTarget target, bool apply)` pushes every canonical Phoenix student.
`GetDefaultTarget()` builds the target (LEA, campus, school year, calendar) from config.
Per student, in order: Student → one StudentSchoolAssociation per Phoenix enrollment at
the target campus for the target school year → StudentEducationOrganizationAssociation
(LEA) → StudentIdentificationCode (Local = the PHX number).

**Enrollments come from Phoenix (PHX-8.4).** Entry date, exit date, grade level, kind, FTE
and primary are read from the `Enrollment` table; the target no longer carries an entry
date. Phoenix codes are mapped to Ed-Fi descriptors at this boundary
(`EdFiApi:GradeLevelDescriptors`, `EnrollmentTypeDescriptors`,
`ExitWithdrawTypeDescriptors`; table in `docs\edfi\texas-sample.md`). An exited
enrollment is re-sent with `exitWithdrawDate` + `exitWithdrawTypeDescriptor`; an exit and
re-entry in the same year sends two associations in entry order. A student with no
matching enrollment is Skipped and gets no PHX number.

**Audited (ADR-004).** Preview records one `SyncPreview` event and Run one `SyncApply`
event per student listed (skipped students included), saved before anything is shown or
sent. If the audit write fails the sync throws and nothing reaches Ed-Fi. The pre-send Run
event says "send started"; after the writes a second `SyncApply` event per sent student
records the result (created, updated, or "failed at <resource>"). If that second write
fails, the data has already gone: the page still shows the rows, with a warning, and the
error is logged. Summaries carry campus, school year and outcome only - never the Ed-Fi
problem text.

**Open mapping question (2026-11-20, for Patrick + Hailee):** `ExitReason.NonAttendance`
is sent as `#Withdrawn`, the same value as `Withdrew`, so Ed-Fi cannot tell the two apart.
Core Ed-Fi has no exact match ("No show" = never attended). Candidates: keep Withdrawn,
`#Dropout`, `#Involuntarily Removed`. One config key:
`EdFiApi:ExitWithdrawTypeDescriptors:NonAttendance`.

**Exited students stay visible to the client (read probe 2026-11-20):** two students in
the sandbox whose only enrollment has an `exitWithdrawDate` (605260, 605522 at 255901001)
are still returned by `GET students` and `GET studentEducationOrganizationAssociations`.
**Writes work too (PHX-9.0, 2026-11-23):** with PHX-000001's only enrollment exited on
2026-11-20, a full sync run returned 200 for her student, enrollment, LEA association and
identification code - 19 of 19 students "updated in Ed-Fi", 0 failed. No special rule for
exited students is needed.

**Not in core Ed-Fi 6.1 (probed 2026-11-23):** census block group and adult previous
attendance. nh-edfi-cus exposes one data model (Ed-Fi 6.1.0, 1223 schemas) and no schema or
property mentions either; `studentDemographics` has neither. They are Texas elements and
arrive with the TX extension (SDK 2028.0.0). Addresses are core: `studentDirectories`
(key: student + education organization) with `addresses` requiring address type, state,
city, postal code and street number/name.

**Order is forced by Ed-Fi authorization.** The enrollment must come straight after the
student: relationship-based authorization only lets the client write the LEA association and
the identification code for a student already enrolled in one of its schools. The first live
run (PHX-7.4) used the card's order (LEA association before enrollment) and every student got
`403 Authorization Denied` on studentEducationOrganizationAssociations with the hint "You may
need to create a corresponding 'StudentSchoolAssociation' item".

**Second rule (run #2):** Ed-Fi lets the client *create* a new student freely, but only lets
it *update* an existing student who is enrolled in one of its schools. Run #1 had left 13
students created but never enrolled, so run #2 got 403 on the student POST itself. The sync
now recovers: on a 403 for the student it writes the enrollment first (allowed for an
existing student), then writes the student again, then the LEA association and code. The
test fake mimics both rules so the unit tests catch an order regression.

| Outcome | Meaning |
|---|---|
| WouldCreate / WouldUpdate | Dry run — nothing sent to Ed-Fi, no student data changed (audit rows only) |
| Created / Updated | Applied; the student POST returned 201 / 200 and all four writes succeeded |
| Skipped | Not sent — no enrollment at the campus for the year, no birth date, or an unmapped grade / kind / exit reason. `Reason` says which |
| Failed | One write failed: `FailedResource` + Ed-Fi `Problem`, or `Reason` for a non-API error. Never stops the batch |

- **Ed-Fi ID:** `studentUniqueId` = `PHX-NNNNNN`, stored as a Phoenix identifier (source
  system `Ed-Fi`, ADR-002). New numbers continue after the highest one already mapped.
- **Two deliberate departures from the card:** (1) the number is recorded *before* the
  writes, so a student whose writes fail keeps the same number on the next run and Ed-Fi
  never collects orphan records; (2) mapped students are re-upserted on every apply
  (`Updated`) rather than skipped, so failures heal and Phoenix name/DOB corrections reach
  Ed-Fi. The card's signature `SyncAllAsync(schoolId, apply)` became a target record so the
  PHX-7.3 page can choose campus, year and calendar.
- **Config (`EdFiApi`, non-secret, defaults in `EdFiApiOptions` match the seeded sample ODS):**
  `DefaultLeaId` 220822, `DefaultSchoolId` 220822001, `SchoolYear` 2027, `CalendarCode`
  220822001-2027, `LocalIdentificationSystemDescriptor` Local, plus the three descriptor
  maps above. The Sprint 7 `DefaultEntryDate`, `DefaultEntryGradeLevelDescriptor`,
  `DefaultEnrollmentTypeDescriptor` and `DefaultFullTimeEquivalency` keys were removed in
  PHX-8.4 (never set in any appsettings file).

Registered as a **singleton** so the token cache persists; pulls a fresh
`HttpClient` from `IHttpClientFactory` per call. Every method is guarded by
`IFeatureFlagService.RequireEnabled("EdFi")`.

## Pages

- **`admin/edfi/health`** — reachability + auth status cards. Never throws on an
  unreachable API; shows a red status instead.
- **`admin/edfi/students`** — first 25 students from the ODS.
- **`admin/edfi/sync`** (PHX-7.3) — target fields (LEA, campus, school year, calendar code,
  entry date) prefilled from config; **Preview** shows would-create / would-update /
  skipped with the PHX numbers apply would use; **Run** is enabled only after a preview of
  the same target and shows how many students it will send. Results table: student, Ed-Fi
  unique ID, outcome badge, and for failures the resource, Problem Details summary and
  correlationId. Config or API failures show as an alert; the page never throws.

Both require the `RequireAdmin` policy, are flag-guarded in markup (friendly
message when OFF), and render interactively (`InteractiveServer`). Nav links
appear only for admins **and** only when the flag is on.

## Relationship-based authorization

With the `Ed-Fi Sandbox` claim set, a student is only visible once a
`studentSchoolAssociation` links them to a school under the API client's
education organization. This is why a freshly created student is
unreadable until associated — see the PHX-3.5 lesson in `docs\edfi\local-setup.md`.
Since PHX-7.0 the sandbox client covers both the Ed-Fi sample LEA 255901 and New Heights
LEA 220822 (`_tools\Add-EdfiClientEdOrg.ps1`); writes for school 220822001 return 403
without the latter.

## Guardrail

`nh-edfi-cus` has been private-endpoint only since 2026-11-20 (hardening #1); the
workstation reaches it over the VPN. **Use sample data only** — no real student data
through this module until New Heights' written data-sharing authorization is signed
(ADR-003 gate b; draft parked 2026-11-20).

## MODULE_TEMPLATE checklist

- [x] Flag in `Features` (default OFF in base)
- [x] `EdFiModule : IModule`; one line in `ModuleBootstrapper.Modules`
- [x] Services flag-guarded at entry points; pages guarded in markup; nav conditional
- [x] No module-owned DB tables (the Ed-Fi id is a `StudentIdentifier` row, ADR-002)
- [x] Live sync verified 2026-11-20 on ODS/API 7.3 / DS 6.1 (Sprint 7 retro)
- [x] This doc
- [x] Architecture tests green
