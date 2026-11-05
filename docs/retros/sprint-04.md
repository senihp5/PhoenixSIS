# Sprint 4 Retrospective — EdFi Module Scaffold + API Client

**Sprint window:** 2026-11-05 (single session, autonomous run)
**Demo:** admin/edfi/health + admin/edfi/students reading live from the sandbox (needs an admin sign-in to view)

## Stories completed

| Story | Description | Status |
|---|---|---|
| PHX-4.1 | EdFiModule : IModule, flag-gated | ✅ |
| PHX-4.2 | Typed Ed-Fi API client + OAuth token caching | ✅ |
| PHX-4.3 | Ed-Fi health admin page | ✅ |
| PHX-4.4 | Student read-through to the page | ✅ |
| PHX-4.5 | Module docs + sprint close | ✅ |

## Context notes

First module built on the Sprint 2 framework, and the first time the Phoenix
app talks to Ed-Fi. Executed as an autonomous run: all builds, tests, commits,
and pushes done directly (device bridge), one commit per story. A new test
project `Phoenix.Infrastructure.Tests` joined the solution for client unit tests.

## 1. What went well?

- The module pattern paid off: EdFi slotted in as one `IModule` + one bootstrapper
  line, and the architecture boundary tests stayed green throughout — the client
  lives in Infrastructure, its contracts in Core, UI depends only on the interface.
- Headless verification went further than expected: unit tests (fake handler) for
  cache/parse/guard, an opt-in **live smoke** that hits the real sandbox, and
  startup-log checks for both flag states — so correctness is proven without a
  browser, which the auth-gated UI otherwise blocks.
- Token caching done right the first time (singleton + IHttpClientFactory), avoiding
  the transient-typed-client cache trap.

## 2. What was harder than expected?

- Verifying the Blazor pages themselves is gated by interactive Entra sign-in, which
  can't be driven headlessly. Mitigated by proving the data path (client) separately;
  the rendered pages still need a human eyeball once (noted below).
- The `DOTNET_Enable{ISA}` lesson from Sprint 3 stayed relevant: fine-grained runtime
  knobs are unreliable; prefer the proven mechanism.

## 3. What did I learn?

- `IHttpClientFactory` + singleton client is the clean way to keep a token cache
  without socket exhaustion.
- Ed-Fi's discovery endpoint is anonymous but data endpoints are not — so a health
  page should check reachability (discovery) and auth (token) as distinct signals.

## 4. What will I do differently next sprint?

- Stand up a lightweight way to smoke-test auth-gated pages (e.g. a test auth handler
  in a Development-only config) so UI verification isn't fully manual.

## 5. Open items / follow-ups

- **Human eyeball:** sign in as admin and confirm `admin/edfi/health` (green version
  card) and `admin/edfi/students` (sandbox list) render as intended.
- **Pkcs advisory** (task #12): transitive `System.Security.Cryptography.Pkcs 10.0.7`
  high-severity NU1903 — pin a patched version.
- **Hardening list** (task #5) still gates real data: IP lockdown, 64-bit worker,
  edfi_app downgrade, encryption-key backup, Entra secret rotation.
- Next: PHX-5 student identity model; Texas descriptors reslotted to Sprint 6
  (Amendment v1.2).
