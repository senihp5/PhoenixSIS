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
