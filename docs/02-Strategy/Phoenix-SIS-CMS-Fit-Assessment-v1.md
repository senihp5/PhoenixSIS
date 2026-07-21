# CMS → Phoenix SIS: Strategic Fit Assessment

**Document Version:** 1.0
**Date:** July 13, 2026 (CST)
**Author:** Claude / Patrick
**Companion Documents:**
- `Adult-HS-SIS-Design-Conversation-v1.md` (original vision, needs revision in light of this)
- `Adult-HS-SIS-Stakeholder-Questionnaires.md` (still valid — discovery still needed)
- CMS: `CaseManagement_Database_Schema_Reference.md`
- CMS: `2026-06-23 - Unify IDSuite3 + TimeClock into CMS - Build Plan.md`
- CMS: `2026-06-23 - IDSuite3 to CMS Integration - Research and Options.md`

---

## Executive Summary

The Case Management System (CMS) is **not a foundation to build the Phoenix SIS on top of** — it is **~60-70% of a functional adult-education SIS already**, missing four specific transformations to become the vision described in the v1.0 design doc.

**Original v1.0 estimate:** $5–15M, 2–3 years to market-ready
**Revised estimate with CMS as base:** ~$1.5–4M, **12–18 months to internal pilot**, 24–30 months to multi-tenant SaaS market entry

This changes the funding calculus, the go-to-market timeline, and — critically — the strategic question from "should we build this?" to **"which of the four transformations do we sequence first, and does the answer change whether this is a New Heights internal tool, an open-source project, or a fundable SaaS venture?"**

---

## 1. What the CMS Actually Is (from source code review)

### Technical Reality

| Attribute | CMS Today | Phoenix SIS Vision | Match |
|---|---|---|---|
| Framework | .NET 8, Blazor Server | .NET 8 / Next.js | ✅ (same C# base) |
| Database | Azure SQL, EF Core | Azure SQL | ✅ identical |
| Auth | Microsoft Entra + RBA engine (migration 093) | Entra + role-based | ✅ identical |
| Multi-tenancy | Multi-campus (Stop Six, McCart) | Multi-district | ⚠️ campus ≠ tenant, but architecturally close |
| Real-time | SignalR configured | SignalR | ✅ identical |
| PowerSchool integration | Read plugin v2.4.4 + Write-back plugin v2.0.0 | External LMS/SIS integration | ✅ mature |
| PEIMS path | Via PowerSchool | Native Ed-Fi/TEDS | ❌ major gap |
| Student-facing UI | None (all staff-facing) | Mobile-first PWA | ❌ major gap |

### Feature Reality

**60+ Blazor pages already shipped** across these functional areas:

**Advising & Case Management** — AdvisorDashboard, AdvisorHome, AdvisorLeaderboard, AdvisorPerformance, Caseload, Interactions, AssignStudents, WeeklyRecap, Alerts, AlertHistoryReport
**Student Records** — StudentProfile, StudentDocuments, StudentSchedulePrint, Transcripts, TranscriptAnalyzer, TranscriptShare
**Grad Planning** — GradPlanPrint, CourseEquivalencyManager, TCC/Certification Program Tracking, TranscriptAnalyzer for prior credit
**Reports** — ReportBuilder (self-service ad-hoc), HonorRoll, PerfectAttendance, CreditHolds, ClassRoster, StaarEligibility, RepeatingPassedCourse, TermPersistence, CampusComparison, PowerBI integration
**Special Programs** — SpecialProgramsDashboard, SpedDocuments
**Admissions** — Admissions module, NewStudents workflow, ingest pipelines, email system (July 2026)
**College & Career** — CC Console, TCC integration, Certification tracking
**Hardware / ID Cards** — IdCard/ module, IdPhotoCapture, HardwareLoans, HardwareAgreement, DocumentCameraCapture (in-flight ID Suite absorption)
**Admin Console** — 14 admin pages: access roles, campuses, certifications, course/term availability, backup status, drive sync, email console, reference data, student doc requests
**Workflows** — Workflows engine, DirectorDashboard, RegistrarDashboard

### Data Reality

- **1,176 active students** (662 Stop Six + 514 McCart)
- **213 staff** with campus assignments
- **1,174 active advisor assignments** with audit log
- **28 database tables** across Core (7) + Advising (12) + PowerSchool Cache (5) + Dashboard (1) + Legacy (3)
- **PowerSchool custom extension tables** (`U_DEF_EXT_STUDENTS0`, `U_STU_MILESTONES`) already deployed
- **Bidirectional sync** operational with X-PS-AuditUser audit compliance

### Active Development (June-July 2026)

Three parallel tracks running right now:
1. **IDSuite3 → CMS unification** (Phase 0-5 plan documented, Phases 1-2 in progress)
2. **TimeClock absorption** (holding pattern on TimeClock dev, cutover to CMS planned)
3. **Admissions Console + Email System** (July 9, 2026 handoff)

---

## 2. Feature Map: CMS vs. Phoenix SIS Vision

### ✅ Already Built (or Substantially Built)

| Phoenix SIS Feature (from v1.0) | CMS Reality |
|---|---|
| Multi-campus/district architecture | Campus filtering with RLS-ready design; 2 campuses live |
| Student demographics + enrollment | `Core_Students` with 22 fields; PowerSchool synced every 6 hours |
| Staff/roster management | `Core_Staff`, `StaffCampusAssignments`, `AdvisorCapacity`, `AdvisorSpecialization` |
| Case management (the differentiator!) | `Advising_Cases`, `Advising_Interactions`, `Advising_Alerts`, `Advising_Milestones` — production tables with 6 interaction types, 12 milestone types |
| Wraparound services tracking | `Advising_StudentServices` — TCC readiness, bus pass, childcare, pathway |
| Transcript evaluation | `TranscriptAnalyzer` + `CourseEquivalencyManager` — already exists as advanced feature |
| Attendance tracking + risk alerts | `StudentAttendanceSummary`, auto-calculated risk flags, `Advising_Alerts` |
| State test tracking | `StudentTestScores` (STAAR/EOC/SAT/ACT), `StaarEligibilityReport` |
| Reporting engine | `ReportBuilder`, `ReportViewer`, 10+ production reports, PowerBI ready |
| Admin console + reference data | 14 admin pages, `AdminAccessRoles`, `AdminReferenceData`, `AdminCampuses` |
| Alert/early warning system | `Advising_Alerts`, `AlertHistoryReport` |
| Audit trail / FERPA defensibility | `Core_AuditLog`, `AssignmentAuditLog`, `Core_SyncLog` |
| Multi-disciplinary team workflows | Advisor caseload with campus scoping + role-based views (Director/Advisor/Registrar) |
| Real-time updates | SignalR configured and used |
| PowerSchool write-back | v2.0.0 plugin — bidirectional sync with 22 tables including LOG, StudentCounselor, ATTENDANCE |
| Photo management + ID cards | Absorbing from IDSuite3 (Phase 1 of card path) |

### ⚠️ Needs Adult-Ed Specific Extension

| Feature | CMS Today | Extension Needed |
|---|---|---|
| Enrollment model | Single continuous enrollment (K-12 pattern) | Rolling / multi-episode enrollment |
| Program tracking | Single program (New Heights HS) | Multi-program (ESL / ABE / ASE / HSE simultaneous enrollment) |
| Barrier tracking | Attendance risk flag, basic alerts | Structured `StudentBarrier` table (homelessness, disability, ELL, justice-involved, etc.) |
| Goals | Implicit in case notes | Explicit `StudentGoal` with SMART framework |
| Prior Learning | `TranscriptAnalyzer` handles course credit | Add PLA workflow (life/work experience credit) |
| Community partners | Local service tracking (TCC, bus, childcare) | Service marketplace with external agency directory |

### ❌ Needs New Build

| Feature | Status | Why It Matters |
|---|---|---|
| **Student-facing portal** | Doesn't exist — everything is staff-facing | THE most critical gap. Adult learners must self-serve. Without this, no product. |
| **Competency-Based Education** | Not modeled | The Phoenix SIS differentiator per v1.0. Needs `Competency`, `CompetencyDemonstration`, `CredentialAward` tables |
| **Native PEIMS/TSDS** | Uses PowerSchool as the PEIMS pipeline | To sell to other districts, must generate PEIMS directly (Ed-Fi native) |
| **WIOA Title II / NRS reporting** | Doesn't exist | Federal adult ed reporting — funding depends on it |
| **CASAS/TABE test tracking** | STAAR only | Adult ed uses different assessments |
| **Multi-tenant SaaS scaffolding** | Single-tenant (New Heights) | Serving multiple districts requires tenant isolation, config, white-labeling |
| **Native mobile app** | Web only | Adult learners are phone-first |
| **In-app messaging** | Email-based communication only | Modern student expectation |

---

## 3. The Four Transformations to Get from CMS → Phoenix SIS

Each is meaningful in size, but none is a rewrite. All build on what exists.

### Transformation 1: Add the Student-Facing Portal

**What it is:** A parallel Blazor Server (or Next.js) surface for students to log in, check schedule, see grades, review credits, receive messages, submit forms, request services.

**What already helps:**
- Data model is complete (StudentProfile has everything a student needs to see)
- Auth is Entra-based (student accounts already provisioned via PowerSchool → district M365)
- PhotoService, PersonPhoto, transcripts, schedules — all readable from existing services
- The existing StudentProfile page could be adapted as the "back office" view; a mirror student-facing view reuses services

**What's new:**
- Student-appropriate UX (no advisor caseload framing)
- Mobile-first design (existing UI is desk-centric)
- Self-service actions: absence requests, appointment booking, service requests, document uploads
- Messaging (in-app chat with teacher/advisor)
- Notification preferences (SMS/email/in-app)

**Rough effort:** 4-6 months for MVP student portal (single dev), or 3-4 months with focused team

### Transformation 2: Adult Ed Data Model Extensions

**What it is:** Add tables and services for enrollment episodes, barriers, goals, PLA, multi-program tracking, competency framework.

**What already helps:**
- EF Core migration pattern proven (022+ migrations)
- Modular architecture (`Modules/Advising.Domain/Data/UI`) — new module: `Modules/AdultEd.Domain/Data/UI`
- Interaction and milestone framework generalizes (add `BarrierIntervention`, `GoalReview` types)
- StudentServices already tracks wraparound

**What's new:**
- New tables: `StudentEnrollmentEpisode`, `StudentBarrier`, `StudentGoal`, `PriorLearningAssessment`, `Competency`, `CompetencyDemonstration`, `CredentialAward`
- New UI: barrier tracker, goal manager, PLA workflow, competency framework builder
- Reporting adjustments (WIOA/NRS friendly)

**Rough effort:** 4-6 months (can parallelize with portal)

### Transformation 3: Native PEIMS/TSDS (Ed-Fi Foundation)

**What it is:** Reduce dependence on PowerSchool as the PEIMS pipeline. Build an Ed-Fi-native data view over existing tables (or as a new schema) that submits PEIMS directly to TSDS.

**Two paths — decision required:**

**Path A: Keep PowerSchool, add direct Ed-Fi submission for adult ed only**
- Adult ed data flows through PowerSchool's PEIMS pipeline (current)
- Adult-ed-specific data (barriers, PLA, competencies) submits directly to TEA via Ed-Fi
- Lower risk, keeps existing workflow, but doesn't help future districts leave PowerSchool
- ~3-4 months

**Path B: Full Ed-Fi ODS/API integration, CMS becomes PEIMS-native**
- Deploy Ed-Fi ODS/API alongside CMS
- CMS data model maps to Ed-Fi resources natively (Ed-Fi Alliance provides this mapping)
- PEIMS submissions generated by CMS directly (bypass PowerSchool for adult ed)
- Higher effort, but unlocks multi-district selling ($$$)
- ~8-12 months

**Recommendation:** Start with Path A. If you decide to commercialize the Phoenix SIS (Question A from v1.0), migrate to Path B in Phase 4 of the broader plan.

### Transformation 4: Multi-Tenant SaaS Hardening

**What it is:** Convert from "runs for New Heights" to "runs for any district that signs up."

**What already helps:**
- Campus filtering is the seed of tenant filtering (same concept, different scope)
- Config-driven reference data (interaction types, milestone types, campuses) already isolated per install
- Azure SQL supports elastic pools / per-tenant databases
- RBA engine allows per-tenant role definitions

**What's new:**
- Tenant model layer (`TenantID` on every table OR per-tenant database)
- Tenant configuration framework (branding, feature flags, reference data)
- Tenant onboarding workflow (self-service or admin-managed)
- Data migration tooling (import from PowerSchool, Ascender, LACES, etc.)
- Billing / subscription management

**Rough effort:** 4-6 months for basic multi-tenant, 6-9 months for market-ready SaaS

**Critical decision:** This transformation is only worth doing if the answer to "who is Phoenix SIS for?" includes districts other than New Heights. If it's an internal tool, skip this transformation entirely and save 6+ months.

---

## 4. Revised Roadmap (with CMS as Foundation)

### Original v1.0 Roadmap (Blank Slate)

| Phase | Duration | Goal |
|---|---|---|
| 0 | 3 mo | Discovery |
| 1 | 9 mo | MVP - Core SIS + PEIMS |
| 2 | 6 mo | Case Management + Wraparound |
| 3 | 6 mo | Competency-Based Education |
| 4 | 6 mo | Multi-District SaaS Scale |
| **Total** | **30 mo** | Market-ready |

### Revised Roadmap (CMS as Foundation)

| Phase | Duration | Goal | What Actually Happens |
|---|---|---|---|
| **0. Discovery** | 2 mo | Validate market, decide business model | Run existing stakeholder questionnaires with focus on gaps vs. CMS today |
| **1. Adult Ed Extensions** | 4-6 mo | Extend CMS with adult-learner data model | New `Modules/AdultEd.*` — barriers, goals, PLA, multi-program, enrollment episodes |
| **2. Student Portal** | 4-6 mo *parallel* | Deliver student-facing surface | Mobile-first web + PWA. Reuses existing data services. Runs in parallel with Phase 1. |
| **3. CBE Foundation** | 4-6 mo | Competency-based education | Framework builder, assessments, demonstrations, credentials |
| **4. Native PEIMS (Ed-Fi)** | 6-9 mo | Reduce PowerSchool dependency | Ed-Fi ODS/API integration; adult ed data submits directly |
| **5. Multi-Tenant Hardening** | 4-6 mo | Convert to SaaS platform | ONLY if commercializing. Tenant isolation, config, onboarding, billing. |
| **6. Market Entry** | 4-6 mo | First external customer(s) | Migration tooling, sales/onboarding motion, support infra |

**If internal-only tool:** Phases 0-3 = **12-18 months**
**If Texas-wide SaaS:** All phases = **24-30 months** (parallel execution possible)
**If open source + services:** Add ~3 months for community infrastructure, docs, governance

---

## 5. Revised Cost & Team Estimates

### Original v1.0 Estimate (Blank Slate)

- **Cost:** $5-15M for 2-3 years to market-ready
- **Team:** 8-15 engineers + PM + designer + compliance/QA lead

### Revised Estimate (CMS as Foundation)

**Internal tool track (New Heights primary customer):**
- **Cost:** $1.5-2.5M over 18 months
- **Team:** 2-3 engineers + Patrick (product/architect) + part-time UX designer + part-time compliance consultant
- **Assumes:** Continue current CMS pace; add student portal and adult ed extensions as parallel workstreams

**SaaS product track (multi-district):**
- **Cost:** $3-5M over 24-30 months
- **Team:** 4-6 engineers + PM + full-time designer + compliance/QA lead + DevOps + sales/CS
- **Assumes:** Same as above plus multi-tenant hardening + market entry investment

**Open source + services track:**
- **Cost:** $2-3M over 24 months + ongoing operations
- **Team:** 3-4 core engineers + community manager + services team
- **Revenue model:** Hosting, support, custom implementation, training

### Why the Reduction

The v1.0 estimate assumed we were building the SIS from scratch. In reality:
- **PowerSchool integration** (~3,100 LOC + write-back plugin v2.0.0): Done. Would have been 6-9 months of work.
- **Case management** (the differentiator): Done. Would have been 6+ months.
- **Multi-campus architecture with RBA**: Done. Would have been 3-4 months.
- **Attendance, testing, reporting infrastructure**: Done. Would have been 4-6 months.
- **Admin console + reference data**: Done. Would have been 2-3 months.

**Roughly 18-24 months of engineering already exists in CMS.**

---

## 6. Critical Strategic Questions (Updated)

The original v1.0 questions still apply, but with new nuance given the CMS discovery:

### Q-A. Funding & Business Model (unchanged but reframed)

- **Internal tool** = cheapest, fastest, lowest risk. CMS keeps evolving for New Heights. Phoenix SIS is a natural extension.
- **VC-backed SaaS** = 3-5x the cost, but the CMS foundation is a genuine unfair advantage — most competitors are starting from zero
- **State partnership** = interesting given TEA's TSDS/Ed-Fi direction. Design partnership + grant funding could accelerate Path B (native Ed-Fi)
- **Open source + services** = leverages the CMS foundation without VC pressure. Could attract other districts organically.

### Q-B. Design Partner (updated)

Previously: "Do you have a design partner school?"

Now: **New Heights IS your design partner already.** The CMS runs their operations daily. Every extension gets battle-tested immediately. This is a huge strategic advantage over any greenfield competitor.

Additional design partners still valuable, but not blocking Phase 0.

### Q-C. Your Role (needs revisiting)

The CMS work shows you're already operating as:
- Product owner / architect (driving IDSuite3 unification, RBA design, TimeClock absorption)
- Product manager (roadmap decisions in every handoff)
- Technical lead (approving migrations, choosing tech directions)

The question is whether the Phoenix SIS effort:
1. **Continues under your solo direction** (like current CMS) — limits scale
2. **Adds a co-founder/CTO** to split product from engineering leadership
3. **Becomes a small team you manage** with you as CEO/product

### Q-D. IDSuite3 Relationship (RESOLVED)

Not an open question anymore — the June 23, 2026 handoff shows this is decided:
- ✅ CMS is the platform of record
- ✅ IDSuite3 is being absorbed as a dashboard (`/id-card`)
- ✅ TimeClock is being absorbed as feature area
- ✅ Legacy IDCardPrinterDB tables migrating to CaseManagementDB under `Card_*` and `TC_*` prefixes

**The same pattern applies to Phoenix SIS:** it's another feature area of the CMS platform, not a separate product. The "Phoenix SIS" branding might be:
- A marketing name for the productized version of CMS
- A tenant-facing brand for external districts (multi-tenant)
- An internal codename for the adult-ed extensions

### Q-E. Timeline Pressure (unchanged - still need answer)

Given the CMS foundation, what's the pressure driving Phoenix SIS?
- New Heights operational need (already being served)
- Grant deadline (TEA, TEACH grants, federal)
- Competitive threat (PowerSchool acquiring adult ed vendor, etc.)
- Personal timeline (career move, funding cycle)

### NEW Q-F. Path A vs. Path B for PEIMS

Do we:
- **Path A:** Keep PowerSchool as PEIMS pipeline, add direct Ed-Fi only for adult-ed extensions (cheaper, faster)
- **Path B:** Full Ed-Fi ODS/API, CMS becomes PEIMS-native (unlocks multi-district selling)

This decision doesn't have to be made now, but it shapes Transformation 3 significantly.

### NEW Q-G. Product Positioning

The CMS is called "Case Management System" internally. Phoenix SIS is a new brand. Are they:
1. **Same product, marketing rebrand** — "CMS is the internal name, Phoenix SIS is the customer name"
2. **Same codebase, different SKUs** — CMS-basic for K-12 districts, Phoenix SIS-adult for adult ed
3. **CMS is New Heights internal, Phoenix SIS is the public product** — divergent forks over time

---

## 7. What This Means for Our Original Design Work

### The Stakeholder Questionnaires (v1.0)

**Still valuable, but reprioritize:**
- Adult Student (#1) — MORE important now. Portal is greenfield; we need student voice to design it.
- Case Manager (#3) — LESS critical for gap discovery (already built), but still valuable for extension validation
- PEIMS Coordinator (#7) — critical if going Path B (Ed-Fi native)
- District Admin (#6) — critical if commercializing
- Community Partner (#10) — critical for service marketplace extension
- IT Director (#9) — less urgent (New Heights IT already engaged)

### The Design Conversation (v1.0)

Section 5 (Architecture Recommendations) needs a rewrite. The proposed Next.js frontend was based on greenfield; the CMS is Blazor Server and that's fine — probably keep Blazor Server for staff surfaces, add Next.js *only* if the student portal specifically benefits from it (mobile PWA, native app path).

Section 7 (Phased Roadmap) is superseded by Section 4 of THIS document.

Section 8 (Strategic Questions) is superseded by Section 6 of THIS document.

The rest of v1.0 (market gap analysis, adult learner realities, feature taxonomy) is still valid and useful.

---

## 8. Recommended Next Steps

### This Week
1. **You answer Question Q-A** (funding/business model) — this shapes the next 12 months
2. **You answer Question Q-E** (timeline pressure) — this affects prioritization
3. **You answer Question Q-G** (product positioning) — this affects everything, including team, branding, and legal structure

### This Month
1. **Reprioritized stakeholder discovery** — 15-20 targeted interviews focused on the four transformations, not the whole SIS scope
2. **Student portal prototype** — clickable Figma or actual Blazor prototype to validate the Transformation 1 approach
3. **Adult ed extension architecture** — technical spec for the new `Modules/AdultEd.*` layout
4. **Path A vs Path B decision** (Q-F) — this affects Transformation 3 significantly

### This Quarter
1. **Complete Phase 0 (Discovery)** with the reduced scope
2. **Begin Transformation 1 (Student Portal)** or Transformation 2 (Adult Ed Extensions) — parallel possible
3. **Continue current CMS work** (Admissions Console, IDSuite3 absorption) — these still create foundation value

---

## 9. Risks Specific to This Path

Different risk profile than a greenfield build:

1. **Coupling risk** — every Phoenix SIS decision that changes CMS internals could disrupt New Heights daily operations. Need clean module boundaries.

2. **Naming/branding confusion** — "CMS" internally, "Phoenix SIS" externally, both are the same codebase. Team, stakeholders, sales will get confused.

3. **PowerSchool dependency** — CMS currently depends on PowerSchool for PEIMS. External districts on Ascender/Infinite Campus can't use CMS without abstracting this. Transformation 3 becomes MANDATORY for commercial viability.

4. **Product debt from single-tenant assumptions** — some code patterns (campus-hardcoded logic, New Heights-specific reference data, embedded credentials) will need refactoring for multi-tenant. Cheaper to do this proactively than retroactively.

5. **Feature scope creep** — the CMS grows features constantly (Admissions, ID cards, TimeClock, Reports). Every added feature is another surface to maintain across tenants. Ruthless feature governance required for SaaS path.

6. **Attribution / IP question** — if the CMS was built as work-for-hire for New Heights, commercializing it may have contract implications. Legal review needed before commercial path.

---

## 10. Bottom Line

**The Phoenix SIS project moved from "greenfield 2-3 year build" to "extend a substantially-built platform in four defined transformations."** This is enormously good news for feasibility, cost, and time-to-value.

The strategic decisions in front of you are:
- **How much of Phoenix SIS is for New Heights vs. external customers?** (funding, positioning, tenant model)
- **How much do we lean into Ed-Fi to escape PowerSchool dependency?** (transformation 3, market size)
- **Which transformation runs first — student portal or adult ed data extensions?** (both matter; sequencing affects perceived progress)

Discovery is still worth doing, but the questions to ask have narrowed. The stakeholder questionnaires you now have are still the right tool — we just apply them to specific gap areas rather than the whole SIS space.

---

*This document should be treated as the current strategic frame. The v1.0 Design Conversation document remains useful as a reference for market context, adult learner realities, and feature taxonomy, but its phased roadmap and cost estimates are superseded by Section 4 and Section 5 above.*
