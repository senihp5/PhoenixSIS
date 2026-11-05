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
