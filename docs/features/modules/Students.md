# Students Module

**Flag:** `Students` (in `Features`, default OFF in base `appsettings.json`; ON in the `Phoenix-Dev` overlay)
**Born:** Sprint 8 (PHX-8.3), 2026-11-20
**Epic:** Epic 4 - Data Migration & Clean-Room Identity

## Design brief

| | |
|---|---|
| **Who** | Phoenix admins (`PhoenixSIS.Admin` group): today Patrick; later New Heights registrars and the Director of Data. |
| **What** | Find a student, open the student record (identifiers, enrollments), enroll and exit a student, and bulk-enroll every unenrolled student for a campus and school year. |
| **Where** | `admin/students` (search list, bulk enroll) and `admin/students/{id}` (record, enroll, exit) in Phoenix.Web; data in the PhoenixSIS database (`Student`, `StudentIdentifier`, `Enrollment`, `AuditEvent`). |
| **Why** | Enrollments are the data Texas funding and the Ed-Fi StudentSchoolAssociation depend on; Phoenix must hold them itself instead of a configured default (Sprint 7). Before real data, every view and change must be attributable to a person (ADR-004). |

## Shape

```
src\Phoenix.Core\Students\            StudentDirectoryContracts (IStudentDirectory, StudentPage, StudentDetail ...)
                                       EnrollmentContracts (core - IEnrollmentService, ICampusDirectory, codes)
src\Phoenix.Infrastructure\Students\   StudentsModule, StudentDirectory (flag-guarded, audited reads)
                                       EnrollmentService, CampusDirectory (core, always registered)
src\Phoenix.Web\Components\Pages\Students\  StudentList.razor, StudentRecordPage.razor
```

The module registers only `IStudentDirectory`. Enrollment, campus, identity and audit services
are core (Program.cs) because the Ed-Fi sync depends on them and modules may not depend on
modules. No module-owned tables: `Enrollment` is core domain (ADR-002), so it is not prefixed
`Students_`.

## Pages

- **`admin/students`** - search by name or any identifier value (e.g. a PHX number), 25 per
  page, with the selected school year's enrollment per student. **Bulk enroll**: campus, school
  year, entry date, grade -> Preview (count only, nothing written) -> Enroll. Students who would
  break a rule are skipped and left for their record page.
- **`admin/students/{id}`** - identifiers, enrollments (with Exit date + reason for open ones),
  and an Enroll form (campus, year, entry date, grade, kind, FTE, primary). Rule violations
  (`EnrollmentRuleException`) show as a message; nothing is saved.

Both pages: `RequireAdmin`, `InteractiveServer`, friendly message when the flag is off, nav link
only for admins with the flag on.

## Auditing (ADR-004)

| Action | Recorded |
|---|---|
| List page shows students | one `View` per student on the page ("Listed in student search.") |
| Record page opened (or reloaded after a change) | one `View` ("Opened student record.") |
| Enroll / bulk enroll | one `Enroll` per enrollment, in the same save |
| Exit | one `Exit`, in the same save |

Views are written before data is returned; if the audit write fails, the page shows an error
and no data. Summaries never contain names, dates, grades or exit reasons.

## Enrollment rules

Campus must be configured (`Phoenix:Campuses`); entry and exit inside the school year (Jul 1 -
Jun 30, `SchoolYear` = ending year); 0 < FTE <= 1; no overlapping enrollments at one campus;
one open primary enrollment per student per year; exit on or after entry, once. Entry dates are
immutable (Ed-Fi keys the enrollment on them); there are no deletes.

## MODULE_TEMPLATE checklist

- [x] Flag in `Features` (default OFF in base, ON in Phoenix-Dev)
- [x] `StudentsModule : IModule`; one line in `ModuleBootstrapper.Modules`
- [x] Services flag-guarded at entry points; pages guarded in markup; nav conditional
- [x] Tables: none module-owned (`Enrollment` is core)
- [x] This doc (with design brief)
- [ ] Architecture tests green (run with PHX-8.3 build)
