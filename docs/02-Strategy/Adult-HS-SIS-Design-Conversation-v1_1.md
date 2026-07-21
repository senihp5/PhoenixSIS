# Adult High School SIS - Foundational Design Conversation

**Document Version:** 1.0
**Date:** May 5, 2026 (CST)
**Status:** Discovery / Design Phase - For Discussion
**Prepared for:** Patrick Hines

---

## Executive Summary

You are proposing to build something that **does not exist in the market today**: a purpose-built, web-based, multi-tenant Student Information System designed for the unique realities of Adult High School students, with native PEIMS/TSDS compatibility and integrated wraparound case management.

The good news: the existing market is fragmented, dated, and ill-suited for adult learners. The opportunity is real.

The hard truth: this is a 2-3 year build to reach market-ready scope, even with a focused team. We need to be strategic about phasing.

This document captures research findings, key architectural decisions, recommended technology stack, and a phased roadmap to validate before we write a single line of code.

---

## 1. The Market Gap You're Solving

### Current State of Adult Ed SIS Tools

| System | Strengths | Weaknesses for Adult HS |
|---|---|---|
| **PowerSchool / Infinite Campus** | Mature K-12 features, parent portals, mobile apps | Built for traditional 8am-3pm seat-time model. Adult ed is bolted on. Parent-centric (assumes minor children). |
| **LACES** (LiteracyPro) | Strong NRS/WIOA federal reporting, used by ~1/3 of states | Data-entry tool, not a true SIS. No student portal. No real attendance/curriculum management. Reporting-focused. |
| **TOPSpro Enterprise** (CASAS) | NRS reports, CASAS test integration, California adult ed standard | Same limitations - reporting tool, not operational SIS. Aging interface. |
| **ASAP** | Registration, payments, CASAS integration | Continuing education focus, not HS diploma path. |
| **Ascender / Frontline** | PEIMS-native, Texas-built | Traditional K-12. Adult ed is afterthought. |
| **Higher Ed (Anthology, Workday Student)** | Modern UX, true CBE support | Far too expensive, too complex, not PEIMS-aware. |

### The Gap

**No system today combines all of these:**
- Modern, mobile-first web UX (think Robinhood-simple, not 2008 portal)
- Adult-learner pedagogy (CBE, rolling enrollment, prior-learning credit)
- True case management (wraparound services, social workers, court systems)
- PEIMS/TSDS native compatibility (Texas-mandatory)
- WIOA/NRS Title II reporting (federal adult ed funding)
- Multi-tenant SaaS for districts/state-level deployment

**This is your wedge.**

---

## 2. The Adult Learner is a Different Animal

Before we design anything, we need to internalize who we're serving. This drives every UX decision.

### Demographics & Reality

- **Average age:** 19-45+, often single parents, often working 1-2 jobs
- **Prior schooling:** Fragmented - may have credits from 3+ districts, GED partial completions, military training, foreign transcripts
- **Digital literacy:** Bimodal - some are smartphone-natives, others are using a computer for the first time
- **Schedule:** Cannot attend 8am-3pm. Need evening, weekend, online, hybrid options
- **Life barriers:** Childcare, transportation, food insecurity, unstable housing, immigration status, probation/parole, mental health
- **Motivation:** Extrinsic and immediate - this credential unlocks a job, a promotion, military enlistment, college access

### Implications for the SIS

| Traditional K-12 SIS Assumes | Adult HS Reality |
|---|---|
| Students attend a fixed schedule | Rolling enrollment, irregular attendance |
| Parents manage everything | Students ARE the responsible adults |
| Seat-time = credit | Competency = credit |
| One school per student | Lifetime of fragmented prior credits |
| Behavior tracking, discipline | Case notes, wraparound, barrier tracking |
| Email/web is fine | Many students are SMS-first or phone-first |
| English primary | Spanish-primary very common in TX adult ed |
| Annual schedule | Open entry/open exit, multiple cohort starts |

---

## 3. Best-in-Class Features to Aggregate

### Borrow from PowerSchool / Infinite Campus
- Real-time gradebook with push notifications
- Mobile-first parent/student portals (theirs are the gold standard for adoption)
- District/multi-school architecture
- Customizable reports and ad-hoc query builders
- API-first ecosystem with third-party integrations (Canvas, Schoology, etc.)

### Borrow from LACES / TOPSpro
- NRS/WIOA Title II report generators (out of the box)
- Pre/post-test tracking with gain calculations (CASAS, TABE, BEST Plus)
- Multi-program enrollment (ESL, ABE, ASE, HSE - student can be in several at once)
- Federal/state reporting period management

### Borrow from Modern Higher Ed (Anthology, Workday Student)
- Competency-based academic plans
- Prior Learning Assessment (PLA) workflow
- Stackable credentials, micro-credentials, badging
- Self-service degree audit / progress dashboards
- Open/rolling registration

### Borrow from Case Management Tools (Casebook, CaseWorthy, Apricot)
- Multi-disciplinary team case notes with role-based redaction
- Service referrals and external agency tracking (DSHS, food banks, childcare)
- Goal tracking with SMART goal templates
- Outcome measurement aligned with funding requirements
- Strict FERPA + HIPAA compliance with field-level access control

### Borrow from Modern Consumer Apps (this is the differentiator)
- **Robinhood-clean interfaces** - hide complexity behind progressive disclosure
- **Duolingo-style streaks and progress** - gamified attendance and competency
- **Intercom-style messaging** - in-app chat with case managers and instructors
- **Calendly-style scheduling** - student books their own appointments
- **Stripe-quality dashboards** - if a student wants to know "how close am I to graduating?" it should be one screen, no clicks

---

## 4. Proposed Feature Taxonomy by Role

### Student Portal (Mobile-First)
- **Home Dashboard:** "Where am I?" — credits earned, credits remaining, projected graduation, current GPA equivalent
- **My Schedule:** Today, this week, drop-in lab times available
- **My Coursework:** Current competencies in progress, upcoming assessments
- **Attendance:** Self check-in (geo-fenced or QR), absence requests, hours toward funding requirements
- **Messages:** Threaded conversations with teachers, case managers, counselors
- **Documents:** Transcripts (current and from prior schools), enrollment letters, certificates
- **Resources:** Wraparound services hub - "I need childcare," "I need food," "I need to talk to someone"
- **Goals:** Personal academic and life goals tracked over time
- **Tasks:** What do I need to do today/this week to stay on track?

### Teacher Interface
- **Roster:** Quick attendance with bulk actions, multi-class roster view
- **Gradebook:** Competency-based AND traditional grading (toggleable per course)
- **Assessments:** Build, deliver, score (integrated with assessment standards)
- **Communications:** Class-wide and individual messaging
- **Concerns:** Quick "flag this student" workflow that routes to case manager
- **Lesson Planning:** Curriculum mapping to TEKS / state standards

### Case Manager Interface
- **My Caseload:** All assigned students with status indicators and last-contact dates
- **Case Files:** Notes, attachments, service plans, multi-agency contacts
- **Service Catalog:** Internal services + community resource directory
- **Referrals:** Outbound and inbound, with status tracking
- **Goal Tracking:** Student goals, milestones, outcomes for funding reports
- **Risk Indicators:** Early warning system (attendance dips, grade drops, missed appointments)

### Counselor / Registrar Interface
- **Transcript Evaluation:** Workflow to receive, evaluate, and award credit for prior learning
- **Course Planning:** Build student academic plans, project graduation timelines
- **PLA Assessments:** Manage prior learning assessment requests
- **Graduation Audit:** State requirements + district overlay
- **State Testing:** STAAR-EOC, TSI, HSE/GED tracking

### Admin / Principal Interface
- **Dashboards:** Enrollment trends, attendance, performance, funding metrics
- **Compliance:** PEIMS/TSDS submission status, error reports
- **Staff Management:** Caseload distribution, workload balancing
- **Configuration:** Calendars, courses, programs, grading scales

### State / District Reporter
- **PEIMS/TSDS Submissions:** Native Ed-Fi compliance, all four annual + new fall enrollment + six-week attendance
- **WIOA Title II / NRS:** Tables 1-12 with NRS calculations baked in
- **Custom Reports:** Self-service ad-hoc query builder with saved templates
- **Audit Trails:** Who saw what, when, for FERPA defensibility

### Parent / Guardian (Optional - many adult students have minor children also enrolling)
- Limited-scope view appropriate for adult learners (FERPA implications)
- Notably: many adult students DO NOT want their parents to have access — design must respect adult agency

---

## 5. Architecture Recommendations

### The Critical Discovery: Build on Ed-Fi

**TEDS (Texas Education Data Standards) is built on Ed-Fi.** This is the foundation of PEIMS/TSDS submissions.

**Ed-Fi ODS/API is open source, built in C# / ASP.NET Core / SQL Server.**

This means we can:
1. Use the Ed-Fi data model as our **operational database** (not just a reporting export)
2. Build custom extensions for adult ed (CBE, case management, wraparound) using Ed-Fi's extension framework
3. Generate PEIMS/TSDS submissions natively because the data already lives in TEDS-compliant structures
4. Inherit OAuth 2.0, claims-based authorization, and FERPA-grade security from the Ed-Fi platform
5. Be portable to other states (every state with Ed-Fi adoption becomes a potential market)

**This single decision saves us 18+ months of compliance work.**

### Recommended Technology Stack

```
┌─────────────────────────────────────────────────────────┐
│ STUDENT/STAFF EXPERIENCE LAYER                          │
│ - Next.js 14 (React) - server-rendered web app          │
│ - Tailwind + shadcn/ui - design system                  │
│ - PWA (Progressive Web App) - installable, offline-capable │
│ - Native iOS/Android (later phase, via React Native)    │
└─────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────┐
│ APPLICATION SERVICES LAYER (.NET 8 / C#)                │
│ - ASP.NET Core Web API                                  │
│ - MediatR (CQRS pattern)                                │
│ - SignalR (real-time messaging, attendance, alerts)     │
│ - Hangfire (background jobs - reports, submissions)     │
│ - Identity Server / Duende (auth, SSO, MFA)             │
└─────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────┐
│ INTEGRATION & DATA LAYER                                │
│ - Ed-Fi ODS/API v7+ (operational data store)            │
│ - SQL Server (primary RDBMS)                            │
│ - Azure Blob Storage (documents, transcripts)           │
│ - Azure Service Bus (event-driven integrations)         │
│ - Redis (caching, session)                              │
└─────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────┐
│ INTEGRATIONS                                            │
│ - TSDS PEIMS submissions (Ed-Fi native)                 │
│ - Canvas / Schoology / Google Classroom (LMS)           │
│ - CASAS / TABE / BEST Plus (assessment APIs)            │
│ - GED / HiSET (testing partner APIs)                    │
│ - Twilio (SMS), SendGrid (email)                        │
│ - DocuSign (forms, releases)                            │
│ - State workforce systems (per-state, varies)           │
└─────────────────────────────────────────────────────────┘
```

### Why This Stack
- **C#/.NET 8** matches your IDSuite3 ecosystem - team can move between projects
- **Ed-Fi is C#/.NET native** - no impedance mismatch
- **Next.js** is the modern web standard, runs on Azure App Service
- **Azure** aligns with Texas school district preferences (most are Microsoft 365 shops)
- **Multi-tenant from day one** - each district/school is a tenant with isolated data

### Hosting Model
- **Azure (preferred)** - SOC 2, FERPA-aligned, regional data residency
- **Multi-region active-active** for high availability
- **Per-tenant database (silo model)** for largest customers - SQL Server elastic pools for smaller ones

---

## 6. The "Adult Ed Specific" Data Model Extensions

Standard Ed-Fi covers K-12. We need extensions for:

### Student Lifecycle Extensions
- `StudentBarrier` - tracked life barriers (homelessness, disability, ELL status, justice-involved, etc.)
- `StudentGoal` - personal/academic goals with SMART framework
- `StudentPriorLearning` - PLA assessments, military credits, work experience credits
- `StudentEnrollmentEpisode` - rolling enrollment is multi-episode, not one continuous stretch

### Competency-Based Extensions
- `Competency` - granular learning outcomes
- `CompetencyAssessment` - multiple ways to demonstrate (essay, project, exam, portfolio)
- `CompetencyDemonstration` - student-level evidence of mastery
- `CredentialAward` - micro-credentials, certificates, badges

### Case Management Extensions
- `CaseFile` - root entity for a student's case
- `CaseNote` - timestamped, role-tagged, redactable notes
- `ServicePlan` - structured wraparound plan
- `ServiceReferral` - to internal/external services
- `ServiceProvider` - directory of community resources
- `CaseTeamMember` - multi-disciplinary teams with permission scoping

### Reporting Extensions
- `NRSEducationalGain` - WIOA Title II measurable skill gains
- `WIOAOutcome` - employment/credential/training outcomes for federal reporting
- `FundingSource` - track per-hour/per-outcome funding by source

---

## 7. Phased Roadmap (Recommended)

### Phase 0: Discovery & Validation (3 months)
**Goal:** Confirm the problem, validate the buyer, lock the design
- 20+ stakeholder interviews (adult HS principals, case managers, students, state admins)
- Compete-mapping with actual product demos of LACES, TOPSpro, Ascender
- Lock Phase 1 feature set with a design partner school (your current employer?)
- UX prototype testing with real students
- Legal/compliance review (FERPA, HIPAA, state-specific)

**Deliverable:** Signed Letter of Intent from at least one design partner

### Phase 1: MVP - Core SIS + PEIMS (9 months)
**Goal:** Replace the basic SIS functions for one design-partner school
- Student/staff identity, enrollment, demographics
- Course catalog, sections, schedules
- Attendance (multiple methods - QR, geo, manual)
- Gradebook (traditional + competency-based toggle)
- Student portal (web, mobile-responsive)
- Teacher portal
- PEIMS Fall, Mid-Year, Summer submissions (the big three)
- Basic state reports

**Success Metric:** One school running their full operations on it for one semester.

### Phase 2: Case Management + Wraparound (6 months)
**Goal:** Differentiate beyond what any K-12 SIS offers
- Case files, notes, service plans
- Multi-disciplinary team workflows
- Service catalog and referral tracking
- Risk indicators / early warning
- Goal tracking
- WIOA / NRS Title II reporting

**Success Metric:** Case managers stop using their separate tracking spreadsheets.

### Phase 3: Competency-Based Education (6 months)
**Goal:** Enable true CBE programs with prior learning credit
- Competency framework builder
- Competency assessments and demonstrations
- Prior Learning Assessment workflow
- Dual-transcript output (competencies + credit-hour equivalents)
- Stackable credentials and badging
- Personalized learning plans

**Success Metric:** Students earning credit faster through demonstrated mastery.

### Phase 4: Multi-District SaaS Scale (6 months)
**Goal:** Open to additional districts beyond design partner
- Multi-tenancy hardening
- Self-service tenant onboarding
- Tenant configuration framework
- White-labeling
- Marketplace for community resource integrations
- Data import tools (from PowerSchool, Ascender, etc.)

**Success Metric:** Five paying districts.

### Phase 5+: Beyond
- Native mobile apps
- AI-assisted advising ("based on your goals, here's what to do next")
- Predictive analytics for retention
- Inter-district transcript exchange (TREx integration)
- Multi-state expansion (each state's reporting layer)
- Higher ed dual enrollment workflows

---

## 8. Critical Strategic Questions for You

Before we proceed, I need your guidance on a few things that shape everything else:

### Question A: Funding & Business Model
This is a $5-15M build to do right. Options:
1. **Self-funded / bootstrap** (you fund it, sell to schools)
2. **State partnership** (Texas TEA grant, ESC partnership)
3. **Venture capital** (EdTech VCs are funding this category - see Frontline, PowerSchool exit comps)
4. **Open source + services** (build it open, monetize via hosting/support like Ed-Fi+)
5. **Internal tool first** (build for your school, productize later)

### Question B: Design Partner
Do you have a current adult HS that would be the first customer/co-designer? This is essential — building in a vacuum guarantees failure.

### Question C: Your Role
- Are you architect/CTO?
- Are you product/CEO?
- Are you the funder looking for a team to execute?
- Some hybrid?

### Question D: IDSuite3 Relationship
Is the current IDSuite3 codebase:
1. **The seed** - we extend it with SIS modules?
2. **A sibling** - separate product, shared identity infrastructure?
3. **Replaced** - the new SIS subsumes ID printing as one feature?

### Question E: Timeline Pressure
Is there an event driving urgency? (Grant deadline, contract loss, leadership mandate, school year start?)

---

## 9. Risks I Want You to See Clearly

1. **Multi-tenant SaaS at state-level scale is genuinely hard.** PowerSchool has 1,000+ engineers. We will need a focused team and sharp scope discipline.

2. **PEIMS compliance is not a feature, it's a compliance regime.** Errors = districts lose state funding. We need a dedicated compliance/QA function.

3. **K-12 buying cycles are slow.** 12-18 month sales cycles. Plan accordingly for runway.

4. **Data migration from incumbent SIS is brutal.** Every district has 10+ years of legacy data with quirks. Migration tooling is its own product.

5. **Trust takes years to build.** PowerSchool and Infinite Campus are trusted because they don't break. New entrants get one chance.

6. **Adult ed is a smaller market than K-12.** ~$300M TAM in the US. To make this work financially, we likely need to:
   - Charge premium per-student rates (justified by case mgmt + reporting savings)
   - Or expand into adjacent markets (alternative high schools, juvenile justice ed, corrections ed, workforce training)

7. **State reporting standards change every year.** This is ongoing engineering forever, not a one-time build.

---

## 10. My Recommendation for Next Steps

**This week:**
1. You answer Questions A-E above
2. We schedule deeper sessions on each major area (Case Mgmt, CBE, PEIMS, Portal UX)
3. We identify 3-5 stakeholders to interview for Phase 0

**This month:**
1. Lock business model and funding approach
2. Confirm design partner school
3. Draft Phase 0 stakeholder interview guide
4. Set up Ed-Fi ODS/API in a dev environment to validate technical assumptions

**This quarter (Q3 2026):**
1. Complete Phase 0 discovery
2. Build interactive UX prototype (Figma + clickable)
3. Lock Phase 1 scope
4. If go: stand up dev team and begin sprint zero

---

## Appendix A: Research Sources

- TSDS PEIMS documentation (TEA, texasstudentdatasystem.org)
- Ed-Fi Alliance technical docs (docs.ed-fi.org)
- Ed-Fi ODS/API v7.1 platform guides
- TEDS / TWEDS data standards
- LACES (LiteracyPro Systems / GeniusLearning)
- TOPSpro Enterprise (CASAS)
- ASAP (RevTrak)
- PowerSchool & Infinite Campus product documentation
- "A Buyer's Guide to Non-Traditional Student Information Systems" (Modern Campus)
- IES research on case management in high schools
- WIOA Title II / NRS reporting requirements
- Designing for Equity: Leveraging CBE (CompetencyWorks/iNACOL)

---

## Appendix B: Acronym Glossary

- **ABE** - Adult Basic Education
- **AEL** - Adult Education and Literacy
- **ASE** - Adult Secondary Education
- **CBE** - Competency-Based Education
- **CCMR** - College, Career, and Military Readiness
- **ESC** - Education Service Center (Texas regional)
- **ELL** - English Language Learner
- **ESL** - English as a Second Language
- **FERPA** - Family Educational Rights and Privacy Act
- **HSE** - High School Equivalency (GED, HiSET)
- **LEA** - Local Education Agency
- **NRS** - National Reporting System (federal adult ed)
- **PEIMS** - Public Education Information Management System (Texas)
- **PLA** - Prior Learning Assessment
- **SIS** - Student Information System
- **TEA** - Texas Education Agency
- **TEDS** - Texas Education Data Standards
- **TREx** - Texas Records Exchange
- **TSDS** - Texas Student Data System
- **WIOA** - Workforce Innovation and Opportunity Act

---

*End of v1.0 design conversation document. This is a living document — we'll iterate as decisions are made.*
