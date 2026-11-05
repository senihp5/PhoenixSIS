# 2026-09-24 17-11 CST - Phoenix SIS Conversation - Session Handoff

**Purpose:** re-establish full context and working relationship for any future conversation. This distills the complete 2026-07-17 → 2026-07-22 working session (project realignment → Sprints 1-2 shipped → Sprint 3 partial → project pause and Azure teardown). Companion documents: `2026-07-22 14-00 CST - Phoenix SIS Project Pause - Session Handoff.md` (detailed pause state, asset inventory, teardown record, resume checklist) and the repo's `docs\` tree. **Project status as of this writing: PAUSED since 2026-07-22.**

---

## 1. Original problem/question and how it evolved

The conversation opened as housekeeping: the Phoenix SIS Claude project had drifted into CMS/Roll Call work, and the first task was separating the two — exporting 10 CMS docs out of this project (zip → CMS project) so Phoenix could restart clean from the 2026-07-14 handoff. From there it evolved through five distinct phases:

1. **Restart Sprint 1 (PHX-1.2)** — which immediately pivoted when Patrick decided on a **fresh clean-room repo** (`F:\Github\PhoenixSIS`, GitHub `senihp5/PhoenixSIS`) instead of continuing inside CaseManagementSystem.
2. **Sprint 1 completion** — scaffold, feature flags, flag service, Entra auth in a brand-new dedicated tenant (`phoenixsis.onmicrosoft.com`), admin dashboard. Plus a .NET 8 → **.NET 10 LTS** upgrade (8/9 EOL 2026-11-10).
3. **Plan repair** — Patrick flagged that the fresh repo "broke" the v1.0 Solo-Dev Sprint Plan; analysis showed it actually *removed* ~2 sprints of CMS-protection work. Produced **Sprint Plan Amendment v1.1** and renumbered the roadmap.
4. **Sprint 2 completion** — IModule/bootstrapper, module template + ADR-001, first test project (NetArchTest boundary rules), Phoenix-Dev/Phoenix-Demo configs via `PHOENIX_DEPLOYMENT` overlay, environment banner.
5. **Sprint 3 partial, then pause** — Ed-Fi platform decision (ODS/API 7.3.x, not week-old v8.0), infrastructure pivot to NH-hosted pilot (App Services + SQL VM), PHX-3.2 provisioning done, PHX-3.3 Phase A (four EdFi databases deployed after five documented walls), then **Patrick paused the project** — full documentation written, all Azure resources torn down.

## 2. Key insights, recommendations, and solutions developed

- **Clean-room + separate identity as IP posture:** fresh repo with zero CMS code, dedicated Entra tenant, policy that concepts carry over but code never does. This turned a legal risk into an architectural principle (recorded in ADR-001).
- **The fresh repo shortened the plan:** original Sprints 2-3 existed mostly to protect CMS inside a shared codebase; without CMS present, ~40 hours evaporated. Lesson: re-derive the plan from the premise change instead of assuming breakage.
- **Research-before-build repeatedly paid for itself:** Ed-Fi API v8.0 had shipped *one week* before the platform decision (chose stable 7.3.x, queued a v8 spike for 8.1); Ed-Fi's SQL Server Docker path turned out to be experimental (drove binary-deploy/Windows App Service choice); the current 7.3.20297 packages require the .NET 10 SDK and Data Standard 6.1 — none of which the July planning docs knew.
- **`PHOENIX_DEPLOYMENT` overlay pattern:** named deployment configs selected by a custom variable rather than ASPNETCORE_ENVIRONMENT, because user-secrets (Entra client secret) only load in Development. Documented in `docs\deployment\configurations.md`.
- **Ed-Fi install walls (all five fixed + documented in `docs\edfi\local-setup.md`):** .NET 10 SDK requirement; real error prints above the ToolsHelper throw; `Encrypt=false` needed on every connection string; SQL-auth login required (Windows/AzureAD identities failed; created `edfi_app`), including the undocumented `EdFi_Ods` template string (`database=EdFi_{0}` — keep the `{0}`); a missing JSON comma makes the tooling silently fall back to defaults. Re-provisioning is ~20 minutes with this list.
- **Teardown discipline:** delete only Phoenix-specific resources (two App Services, two DNS records, four databases, one login, temp files with passwords); never touch shared assets (plan-nh-cus, wildcard cert, VNet, DNS zone, Key Vault). Honest accounting: the App Services rode the existing S1 plan, so removal recovered $0/month — it was hygiene plus VM disk.

## 3. User working style, preferences, communication patterns observed

- 25-year Director of IT, certified SCRUM master, solo dev building with Claude. Fort Worth TX, CST.
- **Terse go/no-go cadence:** "go 3.2", "confirmed", "push success go 2.2". A short reply means proceed, not disengagement. He pastes full console output rather than summarizing — read it all; the diagnosis is usually in there.
- **He runs everything with side effects himself:** git (one combined block at the end), builds/publishes, Azure portal/CLI, RDP, SQL. Claude writes files (verify after every write — the bridge drops during desktop-app updates), gives single-command PowerShell blocks, exact FIND/REPLACE, and "Test the fix" expected outcomes.
- **Decides fast and pivots without ceremony:** fresh repo over the locked modular-monolith location; reversed his own tenant choice mid-task (NH → new Phoenix tenant); dropped local SQL for the NH VM when his machine lacked headroom; paused the whole project cleanly rather than letting it drift.
- **Wants honest pushback exactly once:** the NH-hosting/IP-entanglement concern was raised, he chose "proceed, IP later," and the right move was to record it on the risk register and move on — no re-nagging.
- **Documentation is a first-class deliverable to him:** handoffs at every transition, retros, tags per sprint, decision records. He asked for paste-ready GitHub issue bodies repeatedly — provide titles + fenced markdown bodies, with Sprint/estimate/label metadata called out.

## 4. Effective collaboration approaches identified

- **Division of labor:** Claude researches (web + rendering JS docs through his Chrome when WebFetch can't), writes code/docs directly to `F:\Github` via the device bridge with read-back verification, and drafts all planning artifacts; Patrick executes, tests, and reports output. Live troubleshooting off pasted console output resolved every wall same-session.
- **Checkpoint before configuring:** on unfamiliar territory (Ed-Fi WebApi), inspect the real artifact (its appsettings.json) before prescribing settings — don't configure from memory of how a package "should" look.
- **Ask structured either/or questions** (AskUserQuestion) for genuine decisions — platform choice, install path, repo strategy — with a recommended option and honest trade-offs. He answers immediately and they stick.
- **Write durable state to three places:** the repo (`docs\`), the Claude project knowledge, and (for sprint tracking) the GitHub Project board — so any future session can cold-start from one read.

## 5. Clarifications or corrections that changed direction

- "We should create a new repo" → **fresh empty repo** (not a clone) — reset the whole Epic 1 plan.
- Tenant: initially chose New Heights, then "Scratch that, I think I want to create a new tenant" → dedicated Phoenix tenant. When later choosing NH *hosting*, auth stayed on the Phoenix tenant.
- "I don't have the processing power to run sql locally" → killed the local-Docker Ed-Fi plan mid-story; PHX-3.2/3.3 were rewritten for App Service + SQL VM (issue bodies re-issued for the board).
- "Skip the backups for the moment, make a note" → deferrals get a tracked task + doc checklist entry, not silence.
- "I am abandoning this project for now" → full pause documentation + complete teardown runbook, distinguishing what survives (repo, tenant, docs) from what was removed.

## 6. Specific project context (identifiers a future session needs)

- **Repo:** `F:\Github\PhoenixSIS` / github.com/senihp5/PhoenixSIS (private), branch `main`, tags `phoenix-sprint-01`, `phoenix-sprint-02`, `phoenix-paused-2026-07-22`. Solution: Phoenix.Core / Phoenix.Infrastructure / Phoenix.Web (net10.0) + tests\Phoenix.ArchitectureTests.
- **Entra (KEPT, free):** tenant `phoenixsis.onmicrosoft.com` = `b9221ae6-0702-49b0-9d96-3e7a0a3e33aa`; app reg PhoenixSIS-Dev client `1fdd8ec2-b221-46ef-9208-dfc8405d57ae`; group PhoenixSIS.Admin `cf1f69d0-dee9-4a0e-94e4-8a1eb0a00add`; secret was in dotnet user-secrets, **expires ~2027-01 — likely needs regeneration on resume**.
- **Removed at teardown:** nh-phoenix-cus, nh-edfi-cus, phoenix.newheightsed.com DNS records, EdFi_* databases and `edfi_app` login on vm-decisioned-data.
- **GitHub Project:** "Phoenix SIS - Solo Dev" (users/senihp5/projects/2), epic labels only (`epic:*`).
- Ed-Fi specifics: ODS/API 7.3.20297, Data Standard 6.1, packages from `pkgs.dev.azure.com/ed-fi-alliance/...EdFi` feed; Sandbox install type; SwaggerUI/SandboxAdmin were planned as App Service virtual applications (never built).

## 7. Templates, frameworks, and processes established

- Sprint cadence: hours-boxed sprints, PHX-N.N stories, paste-ready issue bodies, 5-question retro (`docs\retros\sprint-NN.md`), 3-minute Loom, `git tag phoenix-sprint-NN`.
- Module pattern: `IModule` + `ModuleBootstrapper` registry, flag-guard patterns for services/pages/nav/jobs, boundaries enforced by NetArchTest (`docs\features\modules\MODULE_TEMPLATE.md`, `docs\adr\ADR-001`).
- Docs taxonomy in-repo: `01-Session-Handoffs` … `05-Execution`, `adr`, `deployment`, `edfi`, `features`, `retros`.
- Handoff convention: this format, filename `YYYY-MM-DD HH-MM CST - Topic - Session Handoff.md` (hyphens in time), written to both repo and project knowledge.

## 8. Next steps / follow-up areas (when the project wakes)

1. Read the 2026-07-22 pause handoff §7 resume checklist first — it is the authoritative re-entry path (verify build/tests, regenerate the Entra secret if past ~2027-01, re-decide hosting, re-check whether Ed-Fi v8.1+ has matured before recommitting to 7.3).
2. Standing items that survived the pause: written IP clarification with New Heights (Very High on the risk register, deferred by decision); rotate the `decisioned-admin` SQL password if not already done; PHX-1.4/2.5 Loom recordings if the demo trail matters for the investor story.
3. Sprint 3 resumes at re-provisioning (PHX-3.2/3.3 — every command and wall is documented) or gets re-planned against the then-current Ed-Fi landscape.

*End of handoff.*
