# Texas-Shaped Sample Data

**Sprint:** 6 (PHX-6.2–6.3), 2026-11-06
**See also:** `docs\integrations\teds-gap-analysis.md` (what this is NOT — real TEDS),
`docs\integrations\texas-tsds-requirements.md`.

Sprint 6 makes the prototype read as a New Heights adult-high-school scenario on
both ends of the (future) Phoenix → Ed-Fi sync. **Sample data only.**

## Ed-Fi ODS reference data (the sync target)

`_tools\Seed-EdfiReference.ps1` — run from the workstation (uses the sandbox
key/secret in `C:\temp\edfi-sandbox.ps1`; the Ed-Fi API is still publicly reachable
behind OAuth until the on-site lockdown). Idempotent (Ed-Fi POST is upsert by
natural key; re-run returns HTTP 200, no duplicates).

Creates:

| Resource | Natural key | Shape |
|---|---|---|
| LocalEducationAgency | `localEducationAgencyId` **220822** | "New Heights", category **Charter**, Fort Worth / Tarrant |
| School | `schoolId` **220822001** | "New Heights Adult High School", category **Adult School**, grade level **Adult Education**, under LEA 220822 |

`220822` is New Heights' real TEA county-district number; `220822001` is a campus
under it. Descriptors referenced are the **national** Ed-Fi values already in the
ODS (`uri://ed-fi.org/...`). The seed deliberately does **not** create
`uri://tea.texas.gov` descriptors — Texas namespaces/extensions are a real-TEDS,
TEA-platform concern (gap doc §2).

Run:

```powershell
F:\Github\PhoenixSIS\_tools\Seed-EdfiReference.ps1
```

## Phoenix source data (the sync source)

`TexasSampleSourceConnector` (`ISisSourceConnector`, SourceSystem `TexasSample`) —
8 adult-HS students with realistic names/DOBs and `NH-`prefixed source IDs. Import
them through the Data Migration wizard (`admin/migration`): pick **Texas adult-HS
sample**, Preview, then Apply. Idempotent — re-apply matches instead of duplicating.
They land as canonical Phoenix students with `TexasSample` identifiers, ready for the
Sprint 7 sync.

## How this maps toward real TEDS

- **Matches TEDS in shape:** Ed-Fi education-organization hierarchy (LEA → School),
  Texas-style county-district/campus identifiers, a charter/adult-education scenario.
- **Differs from real TEDS:** national (`uri://ed-fi.org`) descriptors instead of
  Texas namespaces; no TEDS extensions; no PEIMS collections; our ODS is Ed-Fi
  7.3/DS 6.1, not TEA's Ed-Fi 3.x platform; no submission/validation. The honest
  delta — and the one hard blocker (in-house vendor registration) — is in
  `teds-gap-analysis.md`.

## Reset / re-run

Both seeders are idempotent and safe to re-run. If the Ed-Fi sandbox is rebuilt
(`_tools\Reset-EdfiSandbox.ps1`), re-run `Seed-EdfiReference.ps1` afterward to
recreate the org.
