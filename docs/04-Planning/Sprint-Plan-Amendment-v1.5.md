# Sprint Plan Amendment v1.5 - Ed-Fi target: Data Standard 6.1 + ODS/API 7.3 (Texas SY 2027-28)

**Date:** 2026-11-20 (CST)
**Amends:** Amendment v1.4 and the Sprint 7 plan (`Sprint-7-Story-Cards.md`, commits ae7c868 / 9ad15af)
**Trigger:** TEA's TSDS Vendor Webinar (2026-10-29) and Ed-Fi's reply (Nathan Gandomi, 2026-11-20) - see `docs\integrations\texas-tsds-requirements.md` sections 4b and 4c.

---

## 1. What changed

On 2026-11-13 the TSDS Vendor SDK 2027.2.0 showed Texas state reporting on **Ed-Fi Data Standard 4.0** (ODS/API 7.1) + TX extension, and the plan was briefly re-cut to "two adapters, DS 4.0 first, TEA SDK hosted in Azure." That direction is **withdrawn**:

- **For SY 2027-28, TSDS moves to Ed-Fi ODS/API 7.3 (.NET 10) + Data Standard 6.1 + TX extension** - Phoenix's own stack.
- Preliminary **TEDS 2028.0.0: 2026-12-01**. **SDK 2028.0.0: target Jan/Feb 2027.**
- Ed-Fi: design to the SY 2027-28 state standard; a **DS v6 SIS certification is targeted for early 2027**; do not regress to DS 5.2. Certification covers core (unextended) Ed-Fi only - build to TSDS (TX) requirements first.

## 2. Decisions (Patrick, 2026-11-20)

1. **One Ed-Fi target: Data Standard 6.1 on ODS/API 7.3.** The DS 4.0 adapter is dropped (it would serve only SY 2026-27, before any realistic Phoenix submission).
2. **Sprint 7 starts now against nh-edfi-cus** (live: ODS/API 7.3.20432, data model Ed-Fi 6.1.0). Core DS 6.1 already exposes every resource Sprint 7 writes (verified 2026-11-20 from the live OpenAPI metadata).
3. **TX extension layer waits for SDK 2028.0.0** (Jan/Feb 2027). The sync is designed so the TX layer is an additive mapping, not a rewrite.
4. **Azure-hosted TEA SDK: deferred to SDK 2028.0.0.** The VM proposal (B2ms Ubuntu, private IP over VPN, nightly auto-shutdown, new Phoenix-only subnet in `vnet-decisioned` - shared networking, confirm first) stands but is not built for the 2027.x (DS 4.0) SDK.
5. **nh-edfi-cus is the right platform** - finish its on-site hardening (#3, #2, #1, #6).

## 3. What the DS 6.1 / ODS/API 7.3 target means for the code

- **Error handling:** ODS/API 7.3 returns RFC 7807 Problem Details (`type`, `title`, `status`, `detail`, `correlationId`, `errors[]`). The write client parses these into a typed result; HTTP status codes are unchanged.
- **Enrollment:** `studentSchoolAssociation` carries, beyond the required fields, `schoolYearTypeReference`, `calendarReference`, `primarySchool`, `enrollmentTypeDescriptor`, and `fullTimeEquivalency` - the SY 2026-27 Ed-Fi SIS certification scenario fields, and FTE aligns with the adult-HS ADA-by-enrollment funding model.
- **Identity:** `studentIdentificationCodes` (core DS 6.1) carries Phoenix's identifiers to Ed-Fi - the outbound counterpart of ADR-002's `StudentIdentifier`. SSN is never sent from Phoenix.
- **Not yet:** `studentDemographics`, `studentDirectories`, `contacts` / `studentContactAssociations` - Phoenix's model does not hold that data yet (intake/AdultEd work). They are in core DS 6.1 and slot into the same sync later.

## 4. Forward map

| Sprint | Content |
|---|---|
| **Sprint 7** (now) | First Phoenix -> Ed-Fi sync on DS 6.1 (cards re-cut: PHX-7.0 - 7.4) |
| Hardening (parallel) | #3 edfi_app least privilege -> #2 64-bit host -> #1 private-only -> #6 cleanup |
| Jan/Feb 2027 | SDK 2028.0.0 released -> Azure-hosted TEA SDK, TX extension layer, TEA vendor sandbox request |
| Early 2027 | Ed-Fi DS v6 SIS certification opens (needs a Phoenix legal entity; New Heights as first verification) |
| Later | Multi-identifier `SourceStudentRecord` + live Flightdeck read (gated by ADR-003); demographics/directory/contacts once the data exists in Phoenix |

## 5. Supersedes

- The 2026-11-13 "both adapters, DS 4.0 first" direction (project status doc).
- The original Sprint 7 cards' minimal enrollment (studentUniqueId + school + entry date + grade only).
