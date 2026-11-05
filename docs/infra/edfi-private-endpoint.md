# nh-edfi-cus Private Endpoint (VPN access)

**Date:** 2026-11-05
**Decision:** lock down the Ed-Fi API via private networking over the existing Azure
VPN, not IP allow-listing (product-owner choice 2026-11-05).
**Status:** Private path BUILT + VERIFIED. Public access intentionally LEFT ON for
now (Option B); disabling it is the final on-site step.

## What exists now

- **Private endpoint** `pe-nh-edfi-cus` (RG `IDCardOCR`), group `sites`, in new
  subnet **`subnet-pe` (10.10.7.0/27)** of `vnet-decisioned` (RG `rg-decisioned-prod`).
  Private IP **10.10.7.4** for both `nh-edfi-cus.azurewebsites.net` and its `.scm`.
- **Private DNS zone** `privatelink.azurewebsites.net` (RG `IDCardOCR`), linked to
  `vnet-decisioned` (link `link-vnet-decisioned`, registration off), with a DNS
  zone group `default` on the PE auto-maintaining the A records.
- Existing VPN: `vpngw-decisioned` (VpnGw1AZ, route-based) — the P2S VPN used for SQL.

## Verified

- From `nh-phoenix-cus` (VNet-integrated, DNS 168.63.129.16): `nh-edfi-cus.azurewebsites.net`
  resolves to **10.10.7.4**, tcpping :443 = 4/4, and `GET /` returns Ed-Fi version **7.3**
  — Phoenix -> Ed-Fi now travels the private endpoint.
- Public endpoint still reachable from the workstation (public access on) — version 7.3.
  So nothing is cut off; existing live smokes keep working.

## Blast radius (why this is safe with public on)

Only `nh-edfi-cus` has a private endpoint and records in the zone. Other VNet-integrated
apps (CMS, Forms, Check-in) have no PE, so their `*.azurewebsites.net` names are not in
the zone and continue to resolve publicly — unaffected. Public clients still resolve
`nh-edfi-cus` to its public VIP while public access remains enabled.

## Remaining on-site steps (finish the lockdown)

1. **Workstation private resolution over the VPN.** A P2S VPN client keeps its local
   DNS by default, so it still resolves `nh-edfi-cus` publicly. To resolve the private
   IP from the workstation, point the P2S VPN client's DNS at **168.63.129.16** (the
   VNet resolver, reachable over the tunnel) — update the gateway's P2S configuration
   and re-download the client, or add a temporary hosts entry
   `10.10.7.4 nh-edfi-cus.azurewebsites.net` for testing. Verify
   `nslookup nh-edfi-cus.azurewebsites.net` -> 10.10.7.4 on the VPN.
2. **Disable public access** (the actual lockdown), only after step 1 verifies:
   `az webapp update -g IDCardOCR -n nh-edfi-cus --set publicNetworkAccess=Disabled`.
   After this, nh-edfi-cus is reachable ONLY from the VNet / over the VPN — the EdFi
   live smoke and the Phoenix Ed-Fi pages then require the VPN. Load no real student
   data before this.

## Teardown (if ever reverting)

Delete in order: PE dns-zone-group `default`, private endpoint `pe-nh-edfi-cus`,
vnet link `link-vnet-decisioned`, zone `privatelink.azurewebsites.net`, subnet `subnet-pe`.
Re-enable public access if it was disabled.
