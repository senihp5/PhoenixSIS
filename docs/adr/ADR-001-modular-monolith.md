# ADR-001: Modular Monolith in a Clean-Room Repository

**Status:** Accepted
**Date:** 2026-07-21 (architecture decided 2026-07-13; repo decision 2026-07-17)
**Deciders:** Patrick Hines

## Context

Phoenix SIS is a purpose-built SIS for adult high school programs, developed solo alongside other commitments. Three architectures were considered: microservices, a fork of the existing CMS, and a modular monolith. The original plan (v1.0) placed the modular monolith *inside* the CaseManagementSystem repo, serving both CMS-Prod and Phoenix from one codebase behind feature flags.

Two realities changed that: the shared `phoenix-dev` branch became CMS's staging branch (the two products were colliding in one repo), and CMS was built as work-for-hire for an employer — growing Phoenix inside it deepened a derivative-work IP exposure.

## Decision

1. **Modular monolith** — one solution, one process, feature-flagged modules (`IModule` + `ModuleBootstrapper`), named deployment configurations. Not microservices: a solo developer cannot operate a distributed system, and the domain doesn't need one. Not a fork: there is nothing to fork (see 2).
2. **Clean-room repository** — Phoenix lives in `PhoenixSIS`, started empty on 2026-07-17. **No CMS source code is ever copied in.** Concepts, domain knowledge, and UX lessons carry over; code does not. CMS-parity features (advising, reports, ID cards) are rebuilt fresh if and when Phoenix needs them.
3. **Dedicated identity** — Phoenix runs in its own Entra tenant with its own app registration; secrets live in user-secrets, never in committed config.
4. **Module boundaries are enforced, not aspirational** — NetArchTest rules in `tests\Phoenix.ArchitectureTests` fail the build when a module reaches into another module's internals.

## Consequences

**Positive:** clean IP provenance for every line; modules keep a solo codebase navigable after gaps; feature flags let unfinished work ship dormant (main always builds); deployment configs (Phoenix-Dev, Phoenix-Demo) are reproducible flag sets; single process = trivial ops.

**Negative:** CMS's mature features are not available for free — parity is future work; module discipline costs a little ceremony per module (IModule, template, tests); a monolith means one runtime scaling unit, acceptable far beyond prototype scale.

**Neutral/notes:** Ed-Fi ODS runs as its own separately-deployed stack reached over REST — its runtime and ours are independent. If Phoenix ever needs to split a module into a service (e.g., Portal at real scale), the enforced boundaries are the seam.

## References

- `docs\04-Planning\Phoenix-SIS-Solo-Dev-Sprint-Plan.docx` (v1.0 strategy)
- `docs\04-Planning\Sprint-Plan-Amendment-v1.1.md` (fresh-repo realignment)
- `docs\features\modules\MODULE_TEMPLATE.md` (the pattern this ADR mandates)
