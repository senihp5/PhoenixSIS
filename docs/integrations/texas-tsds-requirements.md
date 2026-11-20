# Texas State Reporting Requirements for an SIS (TSDS / TEDS / Ed-Fi)

**Date:** 2026-11-05
**Status:** Research complete — one open question requires a written answer from TEA
**Why it matters:** If Phoenix SIS ever becomes New Heights' system of record for
state-reported data, it must be able to submit to TSDS. Until then, PowerSchool
remains the state-reporting system (the PowerSchool module/flag exists for this).

---

## 1. The short answer

Texas does NOT run a product-certification program that an SIS must pass before
an LEA may use it. What Texas requires is functional:

1. **Submit via the TEA Ed-Fi API.** As of August 5, 2024, TSDS officially
   transitioned from XML loading to API transactions "loaded directly from
   source system vendors." XML is gone; the API is the only path.
2. **Conform to TEDS** (Texas Education Data Standards — TEA's Ed-Fi 3.x-based
   standard with Texas-specific collections), published via TWEDS.
3. **Register as a source-system vendor with TEA** to obtain API Key/Secret
   credentials per LEA ODS (the TSDS Upgrade created "new individual ODSs" per
   LEA). The 2024 TAA letter: LEAs must "ensure Key and Secret security
   credentials have been created for each SIS and ERP vendor."
4. **Pass TEA's validations:** Level 1 (API/structural), Level 2 and Level 3
   business validations; TEA tracks vendors on a public **Vendor Data
   Completeness Scorecard** by collection.

## 2. "TSDS Certified Vendors" is NOT SIS certification

The list at texasstudentdatasystem.org ("TSDS Training & Support Certified
Vendors" — Aeries, PowerSchool, Skyward, Tyler, Frontline, Infinite Campus,
etc.) certifies vendors **to train and support LEAs** on TSDS components after
passing TEA-designed courses/exams. It is a training/support credential, not a
license to submit data, and not a legal prerequisite for an LEA's SIS choice.

## 3. Collections an SIS of record must cover

- **TSDS PEIMS:** Fall (FALL), Mid-Year (MDYR), Summer (SUMR), Extended Year
  (EXYR) — student, staff, budget/actual, attendance, course completion.
- **Core Collections:** Class Roster Winter/Fall (CSW), ECDS (KG + PK), SELA,
  RF Tracker, SPPI-14, SPEDS (special ed).
- **TSDS Unique ID** — student/staff ID assignment (eScholar-based).
- **TREx** — transcript exchange for student transfers/graduates (separate
  system, SIS must export the TREx XML schema).
- TEAL is the access/login layer over everything.

For an **adult high school charter**, Fall/Summer PEIMS attendance + course
completion and Unique ID are the heart of it; several Core Collections
(ECDS PK/KG, SPPI-14) won't apply to the population but the applicable ones
still do.

## 4. The open question (ask TEA in writing)

Public TEA/TSDS pages never address an **LEA-built (in-house) SIS**: whether a
district/charter can register as its own source-system vendor and receive API
credentials, and what readiness testing TEA imposes on a new vendor entering
the program. The TWEDS API overview page is blocked to automated reading, and
no public doc spells out new-vendor onboarding.

**Action:** email TSDSCustomerSupport@tea.texas.gov (vendor coordination) and
TSDSDeployment@tea.texas.gov and ask:

1. Can an open-enrollment charter register an in-house/district-developed SIS
   as a source-system vendor for the TSDS Ed-Fi API, and what is the process?
2. What testing/parallel-submission requirements apply to a new source system
   before first live PEIMS submission?
3. Where is new-vendor API onboarding documented (TWEDS API overview access)?

## 4a. Third-party vendor reframing (2026-11-11)

Phoenix is being developed independently - own repos and accounts, clean-room from CMS
(ADR-001). That reframes the open question above. Instead of the undocumented path ("can
an LEA register its own in-house build?"), the system can approach TSDS as a **commercial
third-party SIS vendor** - the documented, routine path: register as a source-system
vendor, pursue Ed-Fi API certification and onboarding (likely via the Texas Education
Exchange), obtain per-LEA Key/Secret, and clear L1-L3 validations. The paperwork TEA
already has is written for exactly this.

Two things do not change:

1. **A vendor never submits on its own behalf.** TSDS data belongs to an LEA. Live
   submission requires a client LEA authorizing the vendor's product for that LEA's ODS.
   New Heights becomes the prospective design-partner / first pilot LEA, not "the
   builder."
2. **Dual-role caveat (not legal advice).** Patrick is both New Heights' IT Director and
   the independent vendor - a related-party arrangement. It needs written IP and
   vendor/customer clarity with New Heights (the long-open "IP clarification," Very High)
   before any pilot, ideally reviewed by counsel.

The TEA email (`tea-vendor-inquiry-email.md`) is rewritten to this third-party framing.

## 4b. TEA's answers and the TSDS Ed-Fi version (2026-11-13)

TSDS Deployment (TSDSDeployment@tea.texas.gov) answered the third-party-vendor inquiry:

- **No registration process.** "TEA does not have a formal registration process for SIS
  vendors to become source-system vendors for TSDS state reporting." TEA *encourages*
  Ed-Fi certification "(version 4 or higher)" - encouraged, not required.
- **Dev/test:** the public **TSDS Vendor SDK** (texasstudentdatasystem.org, Resources).
  For advanced testing TEA offers a **vendor sandbox**; Ed-Fi certification is not
  required to use it, but access is coordinated with TSDS staff.
- **Credentials come from the LEA, not TEA.** "The LEA creates and manages Key/Secret
  credentials through the TSDS Data Management Center (DMC)" and gives them to each
  source vendor for the LEA's individual ODS (IODS). The DMC lets the LEA set
  Read/Write/Update/Delete access per vendor application.
- **No pre-submission gate.** "TEA does not impose formal requirements before a new
  source system is used for its first live PEIMS submission." The LEA is accountable
  and runs its own local parallel testing. The Vendor Data Completeness Scorecard has
  been retired.
- **Docs:** no dedicated new-vendor onboarding document; TSDS Homepage, TEDS XML, TWEDS
  Upgrade, and Resources pages.

**The TSDS Ed-Fi version (verified from the SDK image, not stated in TEA's docs):**
TSDS Vendor SDK 2027.2.0 (October 2026, built from the TEDS publication of 2026-07-01)
is a Docker stack of TEA-built images (`ghcr.io/teaivvdeploy/tsdsodssdk/*:2027.2.0`:
webapi, swagger, adminapp, db-tools; SQL Server 2019). The webapi image's
`appsettings.json` declares **`"StandardVersion": "4.0.0"`** and
**`"ExtensionVersion": "2027.2.0"`**, ships `EdFi.Ods.Extensions.TX.dll`, and the image
env is `VERSION=5.3.1434` on .NET 8 (a TEA build). **Texas state reporting runs on Ed-Fi
Data Standard 4.0 + the TX extension** - not the Data Standard 6.1 our self-hosted ODS
runs. Ed-Fi lists DS 4.0 as out of support after SY 2025-26; TSDS has a multi-year
upgrade initiative but publishes no target version.

**Implications:** a Phoenix TSDS publisher must speak DS 4.0 + TX to the LEA's IODS. Under
ADR-002 that is an outbound adapter at the boundary, not a change to Phoenix's schema.
The dev/test target for Texas is the TSDS SDK (and later the TEA vendor sandbox), not
the generic DS 6.1 ODS. The "open question" in section 4 is answered: no registration
is needed; the LEA (New Heights) issues the IODS credentials and decides readiness.

## 4c. SY 2027-28: TSDS moves to Ed-Fi DS 6.1 + ODS/API 7.3 (2026-11-20)

Sources: TEA TSDS Vendor Webinar 2026-10-29 (slides from Ed-Fi's Nathan Gandomi) and
Nathan's reply of 2026-11-20. **This supersedes the section 4b "implications" paragraph.**

- **Platform:** "For the 2027-2028 school year, TSDS will be upgraded from Ed-Fi
  ODS/API platform version 7.1 to version 7.3" (.NET 8 -> .NET 10). TEA reviewed TEDS
  for "the planned transition to the Ed-Fi version 6.1." So the current TSDS (SDK
  2027.x) is ODS/API 7.1 + DS 4.0 + TX; **SY 2027-28 is ODS/API 7.3 + DS 6.1 + TX** -
  Phoenix's own stack.
- **Dates:** preliminary TEDS **2028.0.0 published 2026-12-01**; **SDK 2028.0.0 target
  Jan/Feb 2027** (requires .NET 10; TEA Ed-Fi Handbook published with it). Next vendor
  webinars 2026-12-03, 2027-01-28, 2027-03-04. Plans for SY 2028-29 not announced.
- **Unchanged:** all TX extension resources/entities/descriptors, API endpoints and URL
  structure, OAuth 2.0 client credentials, security claim sets, interchange XSD.
- **Changed for vendors:** error bodies become RFC 7807 Problem Details (`message`->
  `detail`, `code`->`status`, plus `type`, `title`, `correlationId`, `errors[]`; HTTP
  status codes unchanged); profile-restricted creates return an explicit 403.
- **DS 6.1 shape changes TEA adopts (student side):** Parent/StudentParentAssociation
  sunset -> **Contact / StudentContactAssociation** (ParentUniqueId -> ContactUniqueId);
  new **StudentDemographic** (Sex, Race, HispanicLatinoEthnicity, EconomicDisadvantage,
  EmergentBilingual, FosterCare, Homeless, Truancy, UnaccompaniedYouth,
  UnschooledAsyleeRefugee, MilitaryConnected, DyslexiaRisk, Language,
  StudentCharacteristic, **StudentCensusBlockGroupSet**, **AdultPreviousAttendanceSet**);
  new **StudentDirectory** (Address, ElectronicMail, Telephone); new
  **StudentIdentificationCode** (LocalStudentId/StudentId moved here;
  StudentIdentificationSystem C379 = 01 SSN, 02 Local Student ID, 03 State/S-Number);
  CrisisEventSet -> StudentEducationOrganizationAssociation; SchoolYear added to
  StudentAssessment. Staff side: StaffDemographic, StaffIdentificationCode.
- **Adult-HS relevance:** `AdultPreviousAttendanceSet` (E1660, C362) and the census
  block group (State Compensatory Ed funding) both live on StudentDemographic.
  StudentIdentificationCode maps one-to-one onto Phoenix's ADR-002 StudentIdentifier
  rows (Local / State / SSN systems) - SSN must be treated as restricted data.
- **Ed-Fi (Nathan Gandomi, 2026-11-20):** DS **v6 SIS certification targeted early
  2027**; Ed-Fi will work with a DS v6 build - don't regress to 5.2. Certification
  covers *core, unextended* Ed-Fi only; TSDS's TX extension is outside it - "build
  toward those TSDS requirements and specifications first." **No partner or
  certification fees** (Dell Foundation-funded). **An organization is required** (no
  individual partners). New Heights could be the initial verification environment;
  Ed-Fi works with new entrants on the path to the other two. Testing = vendor
  preparation + Alliance review (mappings, transactional behavior, connection mgmt,
  resync, error handling, descriptors, paging/change queries, admin troubleshooting).
  Offered a readiness call.

**Net implication:** Phoenix stays on DS 6.1 + ODS/API 7.3. The DS 4.0 adapter is
unnecessary. The Texas dev target becomes SDK 2028.0.0 (Jan/Feb 2027); until then the
self-hosted DS 6.1 ODS (nh-edfi-cus) is a faithful stand-in for core Ed-Fi shapes.

## 5. What this means for Phoenix sequencing

- **No blocker today.** PowerSchool stays the TSDS submitter; Phoenix's
  PowerSchool module keeps Phoenix in sync with the system of record.
- **Strategic advantage:** Phoenix already deploys the Ed-Fi ODS/API (7.3,
  Data Standard 6.1 — PHX-3.x). TEDS is an Ed-Fi 3.x-family standard, so the
  Phoenix data model is being built in the same dialect Texas reports in.
  Mapping Phoenix → TEDS is an extension exercise, not a rearchitecture.
- **Gate for "Phoenix as system of record":** TEA's written answer on in-house
  vendor registration + a TEDS gap analysis (Texas descriptors/extensions vs
  Ed-Fi core) + a parallel-run plan against PowerSchool's submissions.

## Sources

- TAA letter 2024-09-19, TSDS Upgrade Project (API cutover 2024-08-05,
  Key/Secret per vendor, L1-3 validations, completeness scorecard):
  https://tea.texas.gov/sites/default/files/taa-2024-09-19-texas-student-data-system-tsds-upgrade-project_0.pdf
- TAA letter 2021-09-30, TSDS upgrade to Ed-Fi 3.x (pilot → parallel → go-live,
  vendors required to submit via Ed-Fi 3.x API):
  https://tea.texas.gov/about-tea/news-and-multimedia/correspondence/taa-letters/texas-student-data-system-tsds-upgrade-to-ed-fi-3.x
- TSDS Upgrade Project overview (TEDS/ODS upgrade, DMC, per-LEA ODSs):
  https://www.texasstudentdatasystem.org/tsds/about/tsds-upgrade-project
- TSDS Training & Support Certified Vendors (training certification, vendor
  list and components incl. PEIMS FALL/MDYR/SUMR/EXYR, Class Roster, ECDS,
  SPEDS): https://www.texasstudentdatasystem.org/TSDS/About/Training_and_Support/TSDS_Training___Support_Certified_Vendors___ESCs
- TWEDS (TEDS publication portal): https://tealprod.tea.state.tx.us/TWEDS
- Vendor Data Completeness Scorecard:
  https://www.texasstudentdatasystem.org/resources/vendor-data-completeness-scorecard.xlsx
