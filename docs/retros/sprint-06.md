# Sprint 6 Retrospective — Texas-Shaped Sample Data + TEDS Gap Analysis

**Sprint window:** 2026-11-06 (single session, per-story go/no-go)
**Demo:** admin/migration → pick "Texas adult-HS sample" → Apply; New Heights LEA/campus in the Ed-Fi ODS

## Stories completed

| Story | Description | Status |
|---|---|---|
| PHX-6.1 | TEDS gap-analysis doc (+ TEA vendor-inquiry email draft) | ✅ |
| PHX-6.2 | Texas-shaped Ed-Fi reference seed (NH LEA + adult-HS campus) | ✅ |
| PHX-6.3 | Texas adult-HS sample connector + source-selectable wizard | ✅ |
| PHX-6.4 | Seeder docs + sprint close | ✅ |

## Context notes

Scope was a product-owner decision: Texas-*shaped* seed + honest gap doc, not a real
TEDS extension install (our ODS is Ed-Fi 7.3/DS 6.1; TEDS targets an older Ed-Fi
generation, and TSDS submission is blocked on the open TEA in-house-vendor question).
Both ends of the future sync are now Texas-shaped: New Heights org in the ODS, Texas
adult-HS students in Phoenix.

## 1. What went well?

- Probing the live API for the exact org JSON shape (and real descriptor values) before
  writing the seeder avoided guesswork — "Adult School" + "Adult Education" were already
  in the national set, a clean fit for the scenario.
- The source-agnostic design paid off immediately: adding a second connector was one
  class + one DI line, and the wizard became multi-source with a small UI change.
- The gap doc keeps the prototype's claims honest ("speaks Ed-Fi / Texas-shaped", not
  "TSDS-conformant") and turned the one real blocker into an actionable TEA email.

## 2. What was harder than expected?

- The Ed-Fi App Service **intermittently 404'd on cold start** right after the
  private-endpoint work — resources that exist returned 404 for a few seconds, then
  200. The seeder now retries transient 404/5xx. Worth watching (the app spins down idle).

## 3. What did I learn?

- Ed-Fi POST is upsert-by-natural-key, so idempotent seeding is free — no check-then-create.
- The national Ed-Fi descriptor set already covers an adult-HS scenario (Adult School,
  Adult Education), so Texas *shape* needs no custom descriptors; Texas *namespaces* are
  a separate real-TEDS concern.

## 4. What will I do differently next sprint?

- Warm the Ed-Fi API (token + a GET) before a batch of calls, given the cold-start 404s —
  the Sprint 7 sync should do this too.

## 5. Open items / follow-ups

- **Send the TEA email** (`docs\integrations\tea-vendor-inquiry-email.md`) — the one move
  that could unblock Phoenix ever submitting to TSDS.
- **Hardening (task #5)** still gates real data (private-endpoint lockdown finish, 32-bit
  worker, edfi_app downgrade, key backup, Entra rotation).
- **Pkcs advisory (task #12).**
- **Next:** Sprint 7 — first Phoenix → Ed-Fi sync (push the Texas sample students from
  Phoenix to the New Heights campus in the ODS, resolving identifiers). The demo payoff.
