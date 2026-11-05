# Sprint Plan Amendment v1.1 — Fresh Repo Realignment

**Date:** 2026-07-21 (CST)
**Amends:** Phoenix-SIS-Solo-Dev-Sprint-Plan.docx (v1.0, 2026-07-13)
**Trigger:** 2026-07-17 decision to move Phoenix into its own clean-room repo (`PhoenixSIS`) instead of evolving inside CaseManagementSystem.

---

## 1. What changed and why

The v1.0 plan's premise was **one repo, two products**: CMS-Prod keeps running while Phoenix grows inside the same solution behind feature flags. On 2026-07-17 Phoenix moved to its own fresh repo because (a) the shared `phoenix-dev` branch had become CMS's staging branch, and (b) a clean-room repo keeps new Phoenix code out of the CMS-derivative IP question.

**The destination (investor-ready prototype, Epics 2-6) is unchanged.** What changed is Epic 1's remaining scope — most of it existed only to protect CMS while sharing a codebase, and that concern no longer exists.

## 2. Disposition of the original sprints

| Original | Disposition |
|---|---|
| Sprint 1 (Feature Flag Foundation) | ✅ DONE — delivered in PhoenixSIS as PHX-1.0 through PHX-1.5 (scaffold, flags, service, Entra auth in dedicated tenant, admin dashboard, docs). .NET 10 LTS. |
| Sprint 2 (Wrap PowerSchool in flags) | **DROPPED — moot.** No PowerSchool code exists in this repo, and CMS-Prod is untouched in its own repo, which was this sprint's entire goal. PowerSchool becomes a possible future integration module for customers who run it. ~20-25 hrs removed. |
| Sprint 3 (Extract Advising module) | **REPLACED.** The Advising *code* stays in CMS, but the *module pattern* it was meant to establish (IModule, template, boundary enforcement) is what Epics 3-5 depend on. That pattern is now new Sprint 2, built greenfield. ~10-12 hrs removed net. |
| Sprint 4 (Deployment configs) | **REDUCED and merged into new Sprint 2.** Two configs instead of three — CMS-Prod config is moot. Startup banner / config indicator retained. |
| Sprints 5-21 (Epics 2-6) | **UNCHANGED** except renumbering (each shifts down ~2) and one edit: old Sprint 9's student sync sources from **seeded Phoenix test data**, not the CMS Dev DB. |

**Net effect: the plan shrinks by roughly 2 sprints / ~40 hours.** New total: ~19-25 sprints, ~320-460 hours.

## 3. What was genuinely lost (and the policy on it)

Advising, Reports, IdCard, and TimeClock were "existing" modules in v1.0 — free inventory from CMS. In the clean-room repo they are feature flags with no code behind them. The investor prototype does not need them (Epics 2-6 are all new code: EdFi, AdultEd, Portal, polish).

- They move to a **post-prototype epic: "CMS-parity modules (clean-room)"** — built fresh if and when Phoenix needs them.
- **Policy: do NOT copy CMS code into this repo.** Porting would re-create the derivative-work exposure the fresh repo exists to avoid. Concepts and lessons carry over; code does not.

## 4. Amended rules and configs

- **Deployment configs (2):** `Phoenix-Dev` (local dev: EdFi/AdultEd/Portal ON as built, local SQL Server Dev Edition) and `Phoenix-Demo` (Azure, polished demo data, deferred until Epic 5-6 per v1.0 infrastructure plan). `Deployment:Name` in config + visible banner so environments are never confused.
- **Branch model:** `main` is the only long-lived branch; optional `feature/*` branches for larger stories. The v1.0 "never merge phoenix-dev to main" safety rule is retired — its replacement is: **incomplete features stay behind flags; main always builds and runs.**
- **Infrastructure:** unchanged from v1.0 — everything local through Epic 4 (SQL Server 2022 Developer Edition, local Ed-Fi ODS via Docker or direct install), minimal Azure for Phoenix-Demo later. This confirms local SQL Dev Edition over the old "reuse New Heights Azure SQL dev server" note, consistent with the separation posture.
- **Cadence unchanged:** hours-boxed sprints, Loom per sprint, 5-question retro, `phoenix-sprint-NN` tags, docs as deliverables.

## 5. Renumbered sprint map (forward)

| New # | Was | Content |
|---|---|---|
| Sprint 1 | Sprint 1 | ✅ DONE — flags + auth + dashboard |
| **Sprint 2** | Sprints 3+4 (reworked) | **Module architecture + deployment configs** (cards: PHX-2.1 – 2.5) |
| Sprint 3 | Sprint 5 | Ed-Fi ODS local deployment |
| Sprint 4 | Sprint 6 | Texas descriptors + sample data |
| Sprint 5 | Sprint 7 | EdFi module scaffold + API client |
| Sprint 6 | Sprint 8 | Student identity model |
| Sprint 7 | Sprint 9 | First student sync (seeded Phoenix data → Ed-Fi) |
| Sprint 8 | Sprint 10 | Data quality dashboard |
| Sprints 9-11 | Sprints 11-13 | AdultEd module (barriers, UI, Ed-Fi extension submission) |
| Sprints 12-17 | Sprints 14-19 | Portal MVP |
| Sprints 18-19 | Sprints 20-21 | Investor-ready prototype |
| Backlog | — | CMS-parity modules (clean-room): Advising, Reports, IdCard, TimeClock |

*Sprint 2 story cards: `Sprint-2-Story-Cards.md` in this folder.*

---

## Addendum v1.2 (2026-11-05) — executed-sprint reconciliation + Texas reslot

Executed work drifted from the v1.1 numbering because the executed **Sprint 3** overdelivered: it covered the amendment's Sprint 3 (Ed-Fi ODS deployment) **and** got the API running, a sample-loaded sandbox, OAuth, and student CRUD (PHX-3.1–3.6, tag `phoenix-sprint-03`). Reconciled map going forward:

| Executed sprint | Amendment rows covered | Content |
|---|---|---|
| Sprint 3 ✅ | S3 + most of S4 | Ed-Fi ODS/API on NH-pilot infra, sandbox, CRUD |
| **Sprint 4** (now) | S5 (pulled forward) | **EdFi module scaffold + API client** (PHX-4.1–4.5) |
| Sprint 5 (next) | S6 | Student identity model |
| Sprint 6 | **S4 reslotted** | **Texas descriptors + TEDS-shaped sample data** — moved here so it lands *before* the first real sync |
| Sprint 7 | S7 | First student sync (seeded Phoenix data → Ed-Fi) |
| … | S8+ | unchanged |

**Why reslot Texas descriptors:** product-owner decision 2026-11-05. The module scaffold (S5) is higher demo value and has no hardening dependency, so it goes first; Texas descriptors are invisible in a demo but must exist before a *real* sync carries TEDS-conformant data, so they sit immediately before Sprint 7. The national GrandBend sample already in the sandbox is enough to build the module against in the meantime. See `docs\integrations\texas-tsds-requirements.md`.

---

## Addendum v1.3 (2026-11-05) — identity/persistence decision + Data Migration epic

Product-owner direction when starting Sprint 5 reshaped the identity work and added a new epic:

- **Clean-room schema (ADR-002):** Phoenix uses Phoenix-native naming. No source SIS's structure (explicitly *not* PowerSchool's) infects Phoenix. Sources are translated at the boundary (anti-corruption layer).
- **Mapping-based identity:** one canonical Phoenix `Student`; external IDs (Ed-Fi, PowerSchool, TSDS, …) live in a `StudentIdentifier` mapping table. Adding a source = adding rows, not reshaping schema.
- **Persistence:** EF Core, tables on the **NH VM SQL server (10.10.2.4)**, new `PhoenixSIS` database (consistent with the Ed-Fi same-VM decision — no local SQL horsepower).
- **New Epic 4 — Data Migration & Clean-Room Identity (`epic:data-migration`):** a source-agnostic migration wizard that connects a source SIS to PhoenixSIS and maps it into the clean schema, built so other SIS's plug in later. Multi-sprint.

Reconciled forward map:

| Executed sprint | Content |
|---|---|
| Sprint 4 ✅ | EdFi module scaffold + API client |
| **Sprint 5** (now) | **Student identity model + migration foundation** (PHX-5.1–5.5): clean schema, resolution service, EF Core on VM, source-agnostic pipeline proven against a sample source. Live PowerSchool connector deferred. |
| Sprint 6 | Texas descriptors + TEDS-shaped sample data (reslotted S4) |
| Sprint 7 | First student sync (Phoenix → Ed-Fi) |
| Later, under epic:data-migration | Live PowerSchool connector; full wizard UX; additional source connectors |
| … | Portal, AdultEd, prototype — unchanged |

*Sprint 5 story cards: `Sprint-5-Story-Cards.md` in this folder.*
