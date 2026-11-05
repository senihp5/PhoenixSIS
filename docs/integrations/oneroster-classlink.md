# OneRoster / ClassLink Roster Server — Research Note

**Date:** 2026-11-05
**Status:** Research complete — backlog candidate (post-Sprint 3)
**Decision needed:** confirm with ClassLink which OneRoster version Roster Server accepts and whether the SFTP CSV path is open to a homegrown SIS.

---

## 1. What this is

ClassLink Roster Server is the district-side rostering hub: it ingests roster data
from the SIS and serves it to vendor apps in the OneRoster standard (1EdTech,
formerly IMS Global). If New Heights runs ClassLink, Phoenix SIS must be able to
feed Roster Server; OneRoster is also the lingua franca for nearly every other
rostering consumer (Clever-style syncs, curriculum vendors, LMS).

## 2. How data gets INTO Roster Server

Two paths exist:

1. **OneRoster CSV upload** — Roster Server provides **dedicated SFTP drop
   boxes** for file uploads and a **Preprocessor** that lets districts reshape
   SIS exports into OneRoster-conformant files. Syncs can be scheduled
   (automated daily syncs via OneSync).
2. **OneRoster REST API pull** — the SIS hosts a OneRoster provider API and
   Roster Server connects to it. ClassLink's public pages do not document this
   path's requirements for custom systems.

**For a custom/homegrown SIS the realistic v1 path is the CSV zip dropped on
the SFTP drop box on a nightly schedule.** No named SIS connector or 1EdTech
certification is required to feed your own district's Roster Server (ClassLink
certifies Roster Server itself; nothing on their pages requires the district's
SIS to be certified).

## 3. OneRoster 1.2 essentials (CSV binding)

- Exchange = a **zip file**, data CSVs at the root, plus **`manifest.csv`**
  (the only file the spec marks Required). Manifest declares
  `oneroster.version = 1.2` and, for every data file, `absent | bulk | delta`.
- Core rostering files Phoenix would emit:
  `orgs`, `academicSessions`, `courses`, `classes`, `users`, `enrollments`
  (optionally `demographics`, `roles`). All other files (gradebook, resources)
  can be `absent`.
- **Bulk mode = full snapshot.** `status` and `dateLastModified` columns stay
  empty; anything missing from the new drop is treated as deleted by the
  consumer. Start with bulk nightly; delta is an optimization for later.
- Files with zero data rows must be omitted, not sent empty.
- UTF-8, header row required, case-sensitive headers, timestamps as
  `YYYY-MM-DDTHH:MM:SS.sssZ`.
- Referential completeness: every sourcedId referenced must exist in the set
  (classes → courses/academicSessions/orgs; enrollments → classes/users/orgs).
- Transport is explicitly out of spec — SFTP is fine.

## 4. OneRoster 1.2 essentials (REST binding) — Phase 2 only

- OAuth 1.0a is **removed** in 1.2; auth is **OAuth 2.0 client credentials /
  bearer tokens**.
- Provider must serve the Rostering service endpoints (orgs, users, classes,
  courses, academicSessions, enrollments, students, teachers, schools, terms,
  demographics) with concurrent consumers.
- 1EdTech conformance certification requires a **paid 1EdTech membership** and
  the conformance test suite. Skip unless a consumer demands the badge — not
  needed for a single-district internal SIS.

## 5. Version note

OneRoster 1.2 is Final (CSV binding at 1.2.1) and is where certification lives;
1.1's OAuth 1.0a/SHA1 path is prohibited and its certification retired. Some
older consumer docs (including third-party ClassLink write-ups) still speak
1.1-only. **Confirm with ClassLink which version Roster Server ingests** —
1.2 CSV is the safe build target since the rostering CSV shapes are nearly
identical and 1.1 output would be a downgrade transform, not a redesign.

## 6. Proposed Phoenix design

- New module **`Phoenix.Modules.Roster`** following MODULE_TEMPLATE.md, feature
  flag **`OneRoster`** (add to the `Features` section — currently 8 flags).
- v1 scope: a OneRoster 1.2 **CSV bulk exporter** — queries Phoenix (and/or the
  Ed-Fi ODS) and emits the manifest + 6 core files as a zip; a scheduled job
  (nightly) pushes it to the ClassLink SFTP drop box; admin page shows last
  run/row counts under the existing Admin area.
- Natural data source order: Phoenix enrollment tables once PHX student CRUD
  lands (3.5+); the exporter is read-only, so it slots cleanly into the modular
  monolith with no cross-module writes.
- v2 (only if ClassLink wants API pull or a vendor requires it): OneRoster 1.2
  REST provider endpoints + OAuth2 client credentials, reusing the same query
  layer.

## 7. Open questions for ClassLink

1. Which OneRoster version(s) does Roster Server ingest today — 1.1, 1.2, both?
2. Is the SFTP CSV drop box available for a homegrown SIS, and what cadence?
3. Does an API-pull option exist for custom providers, and what would onboarding
   require?
4. Any district-side cost implications (Roster Server ships inside the IAM
   suite)?

## Sources

- ClassLink Roster Server product page (SFTP drop boxes, Preprocessor,
  scheduled syncs, 1EdTech certification of Roster Server):
  https://www.classlink.com/products/roster-server
- Edlink community: Understanding ClassLink / Integrating with ClassLink:
  https://ed.link/community/understanding-classlink ,
  https://ed.link/community/integrating-with-classlink/
- 1EdTech OneRoster 1.2 standard: https://standards.1edtech.org/oneroster/specifications/standards/v1p2
- OneRoster 1.2 CSV binding (manifest, bulk/delta, file rules):
  https://standards.1edtech.org/oneroster/specifications/standards/v1p2/csv
- 1EdTech conformance/certification process:
  https://www.1edtech.org/standards/oneroster/conformance-testing
