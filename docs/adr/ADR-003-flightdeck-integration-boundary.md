# ADR-003: Flightdeck (CMS) Integration Boundary

**Status:** Accepted (principles) - contract and Phase-2 scope open (see Open Items)
**Date:** 2026-11-12
**Deciders:** Patrick Hines

## Context

Flightdeck is the New Heights Case Management System (CMS), rebranded. Patrick wants
to integrate Flightdeck with Phoenix SIS. Two standing decisions constrain how:

- **ADR-001 (clean-room code):** no CMS/Flightdeck source is ever copied into Phoenix.
  Concepts, UX and lessons carry over; code does not. The repos, Entra tenants and
  databases are separate.
- **IP independence:** Phoenix is Patrick's independent, third-party product (the
  framing just established for the TSDS source-system-vendor inquiry). Flightdeck is
  New Heights work product. Connecting them is a *new* entanglement beyond "is Phoenix
  a derivative," and the data that crosses the boundary is New Heights' student data.

The pre-clean-room Hybrid Architecture doc already mapped CMS <-> Phoenix crossover
(SIS/Ed-Fi data vs CMS-native case management, joined on a universal student id). Under
clean-room that map is a **boundary between two products**, not a merge.

## Decision

1. **Integration is a boundary, never a merge.** Flightdeck and Phoenix stay separate
   products, codebases, repos and tenants. They connect only over explicit contracts
   (identity mapping, data APIs, SSO, deep links) - the anti-corruption layer of
   ADR-002. No Flightdeck code enters the Phoenix repo.

2. **Shared canonical identity is the connective tissue and the first build.**
   Flightdeck is onboarded as a mapped source/peer through `ISisSourceConnector` (the
   same pattern as the PowerSchool and sample sources). Adding it is rows plus a
   connector, never a schema change (ADR-002). This is no-regrets under every end state
   below.

3. **Phased arc (not an either/or):**
   - **Phase 1 - Unified.** Flightdeck as a mapped peer: shared student identity, data
     exchange over APIs, and SSO + deep links so users move between the two apps. Both
     products remain in use.
   - **Phase 2 - Rebuild.** Clean-room rebuild of the case-management surface into
     Phoenix as the CMS-parity epic (Amendment v1.1 section 3) - concept and UX carry
     over, no code copied; Flightdeck data migrates in through the same identity
     boundary.
   Identity-first makes the Phase-1 -> Phase-2 move reversible and keeps no work wasted.

4. **Real New Heights student data is gated by THREE independent conditions.** Design and
   vetting proceed on **sample data only** until all three hold:
   - **(a) On-site hardening complete** - the "sample data only until hardening" rule
     (in progress now).
   - **(b) New Heights' written data-sharing / processing authorization** - NH is the
     FERPA data owner; this unlocks using NH data inside Phoenix and is **NH's call, not
     TEA's**.
   - **(c) TEA source-system vendor approval** - required only to *submit (push) to
     TSDS*, not to hold NH data in Phoenix.

5. **Cross-tenant identity is real work.** Phoenix (phoenixsis.onmicrosoft.com) and
   Flightdeck (New Heights tenant) are separate Entra tenants; SSO and unified
   navigation are a cross-tenant (B2B / federation) design, scoped in Phase 1.

## Consequences

**Positive:** clean IP provenance is preserved and the boundary is auditable; Phoenix
stays independent; identity-first is reversible and non-wasted; sample-data discipline
holds FERPA and hardening risk at zero until authorized.

**Negative:** every Flightdeck data surface needs an explicit mapped contract (no "just
point EF at their database"); cross-tenant SSO is genuine work; the dual role (NH
employee and independent vendor) means the NH data authorization and the ownership of
the integration must be in writing before real data flows (the long-open IP item, Very
High).

**Neutral:** this is the product-boundary counterpart to ADR-001 (code) and ADR-002
(schema) - ADR-001 keeps foreign code out, ADR-002 keeps foreign schema out, ADR-003
keeps a foreign *product* at arm's length behind a contract.

## Crossover findings (2026-11-12, from reading the Flightdeck interface)

From the Flightdeck `Student` entity interface (read, not copied):

- Flightdeck's canonical student key is `Student.StudentDCID` (PowerSchool DCID). It
  also carries `StudentNumber`, `PowerSchoolStudentID`, and - importantly -
  `StateStudentNumber`, the 10-digit **TSDS Unique ID**. These map directly to Phoenix
  `StudentIdentifier` rows (ADR-002): Flightdeck is already the holder of New Heights'
  real TSDS Unique IDs.
- Implication for Sprint 7: the Phoenix-generated `PHX-NNNNNN` id is correct for the
  sandbox demo, but a real NH -> TSDS submission should carry each student's existing
  `StateStudentNumber` as the `TSDS`/`Ed-Fi` identifier rather than invent one.
- The current `SourceStudentRecord` carries a single `ExternalId` + name + birthdate.
  Mapping Flightdeck's multiple identifiers (DCID, StudentNumber, StateStudentNumber)
  requires extending `SourceStudentRecord` to carry more than one identifier - a
  deliberate next increment, not folded into the first connector scaffold.
- A first `FlightdeckSourceConnector` scaffold (sample data, `SourceSystem` =
  "Flightdeck") is registered and appears in the migration wizard, proving the boundary.

## Open items (resolve before the connector and Phase-1 build)

- **Flightdeck integration contract:** its student identifier scheme, auth/tenant model,
  data API surface, and deep-link URL scheme - read from the Flightdeck repo interface
  or described, never guessed (verify-against-actual rule).
- **"Push data back" target:** TSDS, or back into Flightdeck.
- **NH data-sharing authorization:** whether one exists yet (gate 4b).

## References

- ADR-001 (clean-room code), ADR-002 (clean-room identity / anti-corruption layer)
- `docs\03-Architecture\Phoenix-SIS-Hybrid-Architecture-Design.docx` (CMS <-> Phoenix crossover map, pre-clean-room)
- `docs\04-Planning\Sprint-Plan-Amendment-v1.1.md` (CMS-parity modules backlog) and Amendment v1.4
- `docs\integrations\texas-tsds-requirements.md`, `teds-gap-analysis.md` (the TEA / TSDS gate)
- Project status doc - on-site hardening list (gate 4a)
