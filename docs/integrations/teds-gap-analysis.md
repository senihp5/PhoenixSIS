# TEDS Gap Analysis — national Ed-Fi 7.3 vs real Texas TSDS

**Date:** 2026-11-06
**Purpose:** State honestly what the Phoenix prototype's Ed-Fi setup demonstrates and
what real Texas TSDS submission would additionally require, so nothing in the repo or
a demo overstates conformance.
**See also:** `texas-tsds-requirements.md` (the TSDS/TEDS requirements research),
`tea-vendor-inquiry-email.md` (the open-question email to TEA).

---

## What we actually run

- **Ed-Fi ODS/API 7.3.20432**, **Data Standard 6.1** — the *national* Ed-Fi model.
- The GrandBend national populated sample, plus (Sprint 6) a New-Heights-shaped LEA,
  campus, adult-HS sample students, and a minimal set of descriptor values.
- Reached over OAuth2 client credentials; data exercised via the Ed-Fi REST API.

This proves the **mechanism** end to end: OAuth, the Ed-Fi resource/descriptor model,
org → student → enrollment relationships, and (Sprint 7) a Phoenix → Ed-Fi sync.

## What real Texas TSDS requires beyond this

### 1. Platform-generation gap
TEA's TEDS is built on an **older Ed-Fi generation** (Ed-Fi 3.x-based) than our
7.3 / Data Standard 6.1. Entity shapes, enumerations, and the extension framework differ.
Our ODS is **not** a TEA ODS and cannot be made one by configuration — TEA hosts its own
per-LEA ODSs. We target the *shape* of Ed-Fi, not TEA's exact version.

### 2. Texas extensions & descriptor namespaces
TEDS adds Texas-specific entities and, crucially, **Texas descriptor namespaces**
(`uri://tea.texas.gov/...`) with Texas code values — PEIMS codes for program
participation, demographics, leaver/graduation, campus/district identifiers, etc. Our
national model uses `uri://ed-fi.org/...` values. Real conformance means emitting the
Texas-namespaced descriptors and extension entities, not the national defaults.

### 3. Collections
TSDS is organized into collections a system of record must populate:
**PEIMS** Fall (FALL), Mid-Year (MDYR), Summer (SUMR), Extended Year (EXYR);
**TSDS Unique ID** (student/staff ID assignment); **TREx** (transcript exchange);
**Core Collections** (Class Roster, ECDS, SELA, SPPI-14, SPEDS). For an adult-HS
charter the heart is PEIMS attendance + course completion + Unique ID; several
collections (ECDS PK/KG, etc.) don't apply to the population.

### 4. Submission path & validation
Since Aug 5, 2024, TSDS is **API-only** — loaded "directly from source-system vendors."
A source system must obtain **Key/Secret credentials per LEA ODS**, conform to the
**current TEDS publication** (via TWEDS), and pass **Level 1 (API/structural), Level 2
and Level 3 business validations**, tracked on TEA's **Vendor Data Completeness
Scorecard**.

### 5. The open blocker (only TEA can answer)
No public TEA guidance states whether an **LEA-developed, in-house SIS** can register as
its own source-system vendor to obtain API credentials — every documented path is
commercial-vendor-centric. This is the one hard blocker to Phoenix ever submitting, and
it is a question for TEA (see `tea-vendor-inquiry-email.md`). It is **not** a product
certification: Texas has no SIS product-cert requirement (the "TSDS Certified Vendors"
list is a training/support credential only).

## What this means for the prototype

- **Not urgent / not a prototype blocker.** PowerSchool remains New Heights' TSDS
  submitter today; Phoenix does not need to submit to be useful. Sprint 6 makes the ODS
  Texas-*shaped* and demonstrates the mechanism — it does **not** claim TSDS conformance.
- **Honest claim:** "Phoenix speaks Ed-Fi and can produce Texas-shaped student data end
  to end against a standard Ed-Fi ODS." Not: "Phoenix is TEDS/TSDS conformant."
- **Sequence to real submission (future, gated by §5):** (a) TEA confirms in-house
  vendor registration → (b) obtain credentials for the NH ODS → (c) map Phoenix data to
  the current TEDS descriptors/extensions and the applicable PEIMS collections →
  (d) parallel-submit and clear L1–L3 validations alongside PowerSchool → (e) cut over.

## Prototype vs real TSDS — at a glance

| Dimension | Prototype (now) | Real TSDS |
|---|---|---|
| Platform | Ed-Fi ODS/API 7.3, DS 6.1 (self-hosted) | TEA-hosted, Ed-Fi 3.x-based TEDS |
| Descriptors | national `uri://ed-fi.org` + sample | Texas `uri://tea.texas.gov` + PEIMS codes |
| Data shape | national model, Texas-shaped sample | TEDS extensions + collections |
| Auth | OAuth2 client credentials (our sandbox) | per-LEA-ODS Key/Secret from TEA |
| Validation | none (sample data) | L1–L3 + Vendor Data Completeness Scorecard |
| Submitter of record | PowerSchool (unchanged) | Phoenix, only if TEA allows in-house vendor |
