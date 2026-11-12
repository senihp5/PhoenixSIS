# Sprint Plan Amendment v1.4 - Epic-Numbering Reconciliation + Reporting & Data Access Placement

**Date:** 2026-11-09 (CST)
**Amends:** Phoenix-SIS-Solo-Dev-Sprint-Plan.docx (v1.0) and Sprint-Plan-Amendment-v1.1.md (incl. Addenda v1.2, v1.3)
**Trigger:** (1) the epic numbering drifted when Addendum v1.3 inserted a new epic; (2) a new capability request - data-team extract access for the Director of Data - needs a home in the structure. This amendment reconciles the numbering and places the new work per the original design, rather than minting a standalone "reporting" epic.

---

## 1. The numbering drift and the fix

Addendum v1.3 added "Epic 4 - Data Migration & Clean-Room Identity," but the v1.0 plan already had "Epic 4 - AdultEd Module." The module docs since then tag EdFi as Epic 3 and Data Migration as Epic 4; AdultEd / Portal / Investor-Prototype were never formally renumbered, so the roadmap is internally inconsistent. Canonical list going forward:

| Epic | Name | Tag | Status |
|---|---|---|---|
| 1 | Modular Refactor | - | Done (sprints 01-02) |
| 2 | Ed-Fi ODS Setup | - | Done (sprint 03) |
| 3 | EdFi Integration Module | epic:edfi-module | In progress (Sprints 4, 7) |
| 4 | Data Migration & Clean-Room Identity | epic:data-migration | Foundation done (Sprint 5); more later |
| 5 | AdultEd Module (was Epic 4) | epic:adulted | Not started |
| 6 | Portal MVP (was Epic 5) | epic:portal | Not started |
| 7 | Investor-Ready Prototype (was Epic 6) | epic:prototype | Not started |
| Backlog | CMS-parity modules (clean-room): Advising, Reports, IdCard, TimeClock | epic:cms-parity | Backlog |

The v1.1 conclusions ("~2 sprints removed," "destination unchanged") still hold; only the labels move. The v1.1 renumber table (its section 5) is superseded by the table above.

## 2. Reporting & Data Access is NOT a new epic

Design intent - Strategy doc section 4 ("State/District Reporter"), Hybrid Architecture doc section 7 (the `Compliance` module) and its Phase D ("Direct PEIMS Submission"), and Amendment v1.1 section 3 - deliberately split reporting across the Ed-Fi core and the CMS-parity backlog. It was never a standalone pre-prototype epic. The Director-of-Data request maps onto three existing homes:

**(a) PEIMS / regulatory extracts -> Compliance capability on the Ed-Fi core.** Belongs to Epic 3 plus the Hybrid doc's `Compliance` module (`PEIMSSubmissionService`, `WIOAReportingService`) and migration Phase D. State extracts come out of the TEDS-conformant ODS, not a bolt-on reporting feature - this is the "Ed-Fi saves 18+ months of compliance work" thesis. Gated on the open TEA in-house-vendor registration (`docs\integrations\teds-gap-analysis.md`). In-app templates are a convenience; the authoritative submission is Ed-Fi -> TSDS.

**(b) In-app export / ad-hoc builder + saved templates + audit trail (DDE) -> clean-room "Reports" module (CMS-parity backlog).** "Reports" is already one of the four CMS-parity modules named in Amendment v1.1 section 3. Maps to Strategy section 4 "Custom Reports / Audit Trails." Pullable forward from the backlog if the data team needs it before the prototype; rebuilt clean-room, no CMS code copied (ADR-001).

**(c) Direct read-only SQL (DDA) -> data-access / security enabler, not an epic.** A `reporting` schema of curated views over PhoenixSIS, a scoped read-only login, and a new `PhoenixSIS.DataTeam` Entra group with a `RequireDataTeam` policy. Fits the architecture's app-layer-join reality (Ed-Fi ODS and PhoenixSIS cannot be cross-database joined natively; a curated view layer is the safe read surface). Can ship early and independently of the app deploy because it reads the PhoenixSIS DB directly over the VPN - **sample data only until the on-site hardening is complete**, the same guardrail that governs the EdFi module.

**(d) Director-of-Data access = RBAC, not an epic.** Full `PhoenixSIS.Admin` now (decided 2026-11-06); a narrower `DataTeam` role once (c) exists.

## 3. "Certification SIS" - two distinct meanings, both already in the design

- **Student credentialing / certification** = the CBE / `CredentialAward` / competency track (Strategy section 6; the Hybrid `CBE` module; Phase 3). Completion and credential extracts serve this.
- **Vendor / state certification to submit** = TEA in-house-vendor registration + TEDS conformance. This is the real gate on any official regulatory extract, and the one open blocker on the compliance side.

## 4. Supersession

This corrects the "Epic 6 - Reporting & Data Access" label used in the 2026-11-06 project-status entry: Epic 6 is Portal MVP, and "reporting" is not a standalone epic at all. The DataAccess design brief is revised against this structure before it is committed to `docs\features\modules\`.

## 5. Forward sequencing (priority unchanged)

Sprint 7 (Epic 3, first Phoenix -> Ed-Fi sync) remains the current priority. Data-access work follows:
- the DDA / DataTeam read path (2c) is the natural first independent slice - low risk, unblocks analysts against sample data over the VPN;
- the in-app Reports module (2b) and Compliance / PEIMS (2a) follow, the latter behind the TEA blocker.

All in-app use - including anything the data team does through the SIS UI - remains gated on the first Phoenix.Web deploy to nh-phoenix-cus.
