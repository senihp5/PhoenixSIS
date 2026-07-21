# 2026-07-14 15:30 CST - Phoenix SIS Project - Session Handoff

## Purpose of This Document

This handoff document captures the complete context of the Phoenix SIS design and setup conversation to enable seamless continuation in a new Cowork session. Read this document first before continuing work.

---

## Project Snapshot

**Project name:** Phoenix SIS
**Type:** Purpose-built Student Information System for Adult High School programs
**Developer:** Patrick Hines (solo, foreseeable future)
**Location:** Fort Worth, TX
**Codebase:** F:\Github\CaseManagementSystem (existing CMS being evolved into Phoenix)
**GitHub username:** senihp5
**GitHub Project:** Phoenix SIS - Solo Dev (at github.com/users/senihp5/projects/2)

---

## Who Patrick Is (Critical Context)

- **25-year Director of IT** with deep infrastructure experience
- **Certified SCRUM Master** — thinks in stories, sprints, ceremonies
- **NOT a traditional developer** — has been building CMS with Claude's help
- **Windows / .NET 8 / Visual Studio 2022 / SQL Server** native
- **Sole developer** for foreseeable future
- Working on Phoenix in parallel with day job and other projects
- Prototype-first strategy: build working prototype → then seek capital/personnel
- Timeline: ship-when-ready, no fixed deadlines

**Communication style Patrick prefers:**
- Direct and concise, focused on solving the problem
- Extremely explicit step-by-step instructions
- Full file paths, exact line numbers when possible
- FIND/REPLACE code blocks (before/after)
- 4 spaces indent, no tabs
- PowerShell for command-line
- Visual Studio MSBuild (not dotnet CLI) - though dotnet CLI OK for phoenix-dev branch work
- Screenshot-driven debugging when UIs get in the way
- Acknowledge uncertainty; don't make things up

**Communication style Claude should AVOID:**
- Explaining basic infrastructure concepts
- Recommending managed services over self-hosted when self-hosted is fine
- Suggesting consultants for things Patrick can do himself
- Adding "when you're ready" caveats to normal ops work
- Estimating hours based on a new-developer velocity

---

## Locked Strategic Decisions (Do Not Re-Litigate)

| # | Decision | Rationale |
|---|---|---|
| 1 | **Hybrid architecture** — Ed-Fi ODS core + CMS-native extensions for differentiation | Best balance of national TAM, PEIMS compliance, and differentiation |
| 2 | **Self-host Ed-Fi ODS on Azure** | Matches existing Patrick infrastructure |
| 3 | **Contribute adult ed extensions back to Ed-Fi Alliance** | National standard, credibility, ecosystem effects |
| 4 | **Modular monolith** — single codebase, feature-flagged modules | NOT a fork. One repo, three deployment configs. |
| 5 | **Team learns Ed-Fi from scratch** | No consultant hire. Saves $20-60K. Adds 2-3 months. |
| 6 | **Watch Ed-Fi Summit on YouTube** (not attend in person) | Free, efficient |
| 7 | **Reuse existing Azure SQL dev server** for Ed-Fi ODS | Full-Text Search confirmed available (FulltextInstalled = 1) |
| 8 | **Skip on-prem server build for now** | Use dev machine + Azure SQL. Revisit if multi-machine access needed. |
| 9 | **Use GitHub Projects for backlog, Loom for sprint demos** | Free, integrated, sufficient for solo dev |
| 10 | **Ed-Fi v7.3 with Data Standard 5.2** | Latest as of July 2026, verified via web search |

---

## Documents Produced (Complete Set)

All documents are saved to /mnt/user-data/outputs/ during the conversation:

| # | Document | Purpose |
|---|---|---|
| 1 | Adult-HS-SIS-Design-Conversation-v1.md | Market analysis, feature taxonomy, adult learner reality |
| 2 | Adult-HS-SIS-Stakeholder-Questionnaires.docx | 10 stakeholder interview guides (Students, Teachers, Case Managers, Counselors, Principals, District Admins, PEIMS Coordinators, State Reporters, IT Directors, Community Partners) - targets 50-70 total interviews |
| 3 | Phoenix-SIS-CMS-Fit-Assessment-v1.md | CMS-to-Phoenix mapping analysis |
| 4 | Phoenix-SIS-Hybrid-Architecture-Design.docx | Two-database architecture, table-by-table mapping of all 28 CMS tables, universal StudentUniqueId concept, 5-phase migration path |
| 5 | Phoenix-SIS-Solo-Dev-Sprint-Plan.docx | 21-sprint plan (~360-500 hours) across 6 epics for solo dev |
| 6 | Phoenix-SIS-EdFi-ODS-Deployment-Guide.docx | Ed-Fi ODS/API v7.3 setup guide for Windows dev machine + SQL Server (Sprint 5 reference) |
| 7 | Phoenix-SIS-Sprints-1-4-Story-Cards.docx | Detailed 17 stories for Epic 1 (Modular Refactor) |
| 8 | Phoenix-SIS-Setup-and-Sprint-1-Issues-v2.docx | CURRENT setup guide + Sprint 1 issue content (v2 supersedes v1 which had outdated GitHub UI instructions) |

**Deprecated (do not use):**
- Phoenix-SIS-Setup-and-Sprint-1-Issues.docx (v1) — outdated GitHub UI instructions
- Phoenix-SIS-GitHub-Projects-Setup-v2.docx — interim fix, absorbed into v2

---

## Current State of Execution

### Setup Phase - COMPLETE ✅

- ✅ Phoenix SIS - Solo Dev GitHub Project created
- ✅ Four columns configured: Backlog / Sprint / In Progress / Done
- ✅ Three custom fields created: Sprint (Single select), Actual Hours (Number), Loom URL (Text)
- ✅ Repository linked to project (CaseManagementSystem)
- ✅ All 4 Sprint 1 issues created with full body content (PHX-1.1 through PHX-1.4)
- ✅ Definition of Done checklists added to all 4 issues

### Sprint 1 Progress - 1 of 4 stories complete

- ✅ **PHX-1.1: Feature Flag Configuration Schema** — DONE
  - Added `Features` section to appsettings.json in CaseManagement.Web
  - 8 modules with Enabled and Description fields
  - Defaults preserve current CMS behavior
  - Committed to phoenix-dev branch
  - CMS still runs identically

- ⬜ **PHX-1.2: IFeatureFlagService Interface and Implementation** — READY TO START
- ⬜ **PHX-1.3: Admin Feature Flag Dashboard Page** — Not started
- ⬜ **PHX-1.4: Documentation and Sprint 1 Demo** — Not started

### Pending Environmental Setup

- ⬜ Loom account (~15 min task, can defer until end of Sprint 1)

---

## Solution Structure (Verified)

Read directly from F:\Github\CaseManagementSystem\CaseManagementSystem.slnx:

```
Solution: CaseManagementSystem.slnx
├── src/
│   ├── CaseManagement.Core/          .NET 8, nullable enabled
│   ├── CaseManagement.Data/          EF Core context
│   ├── CaseManagement.Infrastructure/ Services, external integrations
│   ├── CaseManagement.ReconcileCli/  CLI reconciliation tool
│   └── CaseManagement.Web/           Blazor Server main app
├── src/Modules/
│   ├── Advising.Data/
│   ├── Advising.Domain/
│   └── Advising.UI/
└── src/Shared/
    └── Shared.PowerSchool/           Shared PowerSchool integration
```

**Existing conventions observed:**
- Config classes in `CaseManagement.Core/Configuration/` (e.g., PowerSchoolSettings)
- Services in `CaseManagement.Infrastructure/Services/`
- DI registration in `CaseManagement.Web/Program.cs`
- Blazor pages in `CaseManagement.Web/Components/Pages/`
- Authentication via Entra AD with policy-based authorization

**Test project:** NONE currently. This is a decision point for Sprint 1.

---

## The Next Story: PHX-1.2

### What PHX-1.2 Does

Creates the `IFeatureFlagService` interface, its implementation, and registers it in dependency injection. This is the first real C# code work of Phoenix — PHX-1.1 was pure configuration.

### Files to Create

1. `src/CaseManagement.Core/Features/IFeatureFlagService.cs`
   - Contains the interface definition
   - Contains `FeatureFlagState` record
   - Contains `FeatureNotEnabledException` class

2. `src/CaseManagement.Infrastructure/Features/FeatureFlagService.cs`
   - Contains the concrete implementation
   - Uses IConfiguration to read the Features section
   - Uses ILogger for startup logging
   - Caches flags in a Dictionary

### File to Modify

- `src/CaseManagement.Web/Program.cs`
  - Add `using CaseManagement.Infrastructure.Features;`
  - Add DI registration: `builder.Services.AddSingleton<IFeatureFlagService, FeatureFlagService>();`

### Complete Code Ready to Paste

**IFeatureFlagService.cs:**

```csharp
namespace CaseManagement.Core.Features;

public interface IFeatureFlagService
{
    bool IsEnabled(string featureName);
    IReadOnlyDictionary<string, FeatureFlagState> GetAll();
    void RequireEnabled(string featureName);
}

public record FeatureFlagState(
    string Name,
    bool IsEnabled,
    string? Description
);

public class FeatureNotEnabledException(string featureName)
    : InvalidOperationException(
        $"Feature '{featureName}' is not enabled in this deployment.");
```

Note: Uses C# 12 primary constructors. If they cause issues, use traditional constructor:

```csharp
public class FeatureNotEnabledException : InvalidOperationException
{
    public FeatureNotEnabledException(string featureName)
        : base($"Feature '{featureName}' is not enabled in this deployment.")
    { }
}
```

**FeatureFlagService.cs:**

```csharp
using CaseManagement.Core.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CaseManagement.Infrastructure.Features;

public class FeatureFlagService : IFeatureFlagService
{
    private readonly IConfiguration _config;
    private readonly ILogger<FeatureFlagService> _logger;
    private readonly Dictionary<string, FeatureFlagState> _cache;

    public FeatureFlagService(
        IConfiguration config,
        ILogger<FeatureFlagService> logger)
    {
        _config = config;
        _logger = logger;
        _cache = LoadFlags();
    }

    private Dictionary<string, FeatureFlagState> LoadFlags()
    {
        var flags = new Dictionary<string, FeatureFlagState>(
            StringComparer.OrdinalIgnoreCase);

        var section = _config.GetSection("Features");
        foreach (var child in section.GetChildren())
        {
            var enabled = child.GetValue<bool>("Enabled", false);
            var description = child.GetValue<string?>("Description");
            flags[child.Key] = new FeatureFlagState(
                child.Key, enabled, description);

            _logger.LogInformation(
                "Feature '{Name}' is {State}",
                child.Key,
                enabled ? "ENABLED" : "disabled");
        }

        return flags;
    }

    public bool IsEnabled(string featureName) =>
        _cache.TryGetValue(featureName, out var flag) && flag.IsEnabled;

    public IReadOnlyDictionary<string, FeatureFlagState> GetAll() => _cache;

    public void RequireEnabled(string featureName)
    {
        if (!IsEnabled(featureName))
            throw new FeatureNotEnabledException(featureName);
    }
}
```

**Program.cs additions:**

```csharp
// Add near top with other using statements:
using CaseManagement.Infrastructure.Features;

// Add near other builder.Services.Configure/AddSingleton lines:
// Phoenix SIS Feature Flag Service (PHX-1.2)
builder.Services.AddSingleton<
    CaseManagement.Core.Features.IFeatureFlagService,
    CaseManagement.Infrastructure.Features.FeatureFlagService>();
```

### Definition of Done Adjustments for Solo Dev

The original DoD said "Unit tests cover key scenarios." Since the solution has no test project currently, either:

- **Option A (recommended):** Update this DoD item to "Manual verification: startup logs show all 8 features and their state"
- **Option B:** Add a new test project — adds ~1 hour to sprint scope

Whichever chosen, keep Sprint 1 momentum. Add a test project in a later sprint when tests actually need to be written.

### Expected Result When PHX-1.2 Is Done

At CMS startup, the debug output shows 8 log lines:

```
Feature 'Advising' is ENABLED
Feature 'PowerSchool' is ENABLED
Feature 'EdFi' is disabled
Feature 'AdultEd' is disabled
Feature 'Portal' is disabled
Feature 'Reports' is ENABLED
Feature 'IdCard' is ENABLED
Feature 'TimeClock' is disabled
```

CMS continues to work identically. Nothing user-facing changes.

### Commit Message

```
PHX-1.2: Implement IFeatureFlagService with configuration binding
```

---

## Sprint 1 Full Story Sequence

For reference in this and subsequent sessions:

| Story | Estimate | Status | Description |
|---|---|---|---|
| PHX-1.1 | 2-3 hrs | ✅ DONE | Add Features section to appsettings.json |
| PHX-1.2 | 4-5 hrs | ⬜ NEXT | Create IFeatureFlagService + implementation + DI |
| PHX-1.3 | 4-5 hrs | ⬜ | Blazor admin dashboard at /admin/feature-flags |
| PHX-1.4 | 2-3 hrs | ⬜ | Docs + Loom sprint demo + retro |

Total Sprint 1: 15-20 hours estimated.

---

## Epic Roadmap (Beyond Sprint 1)

| Epic | Sprints | Est. Hours | Focus |
|---|---|---|---|
| 1. Modular Refactor | 1-4 | 70-90 | Feature flags, module boundaries, deployment configs |
| 2. Ed-Fi ODS Setup | 5-6 | 40 | Local Ed-Fi install, Texas descriptors |
| 3. EdFi Module + Sync | 7-10 | 80 | C# Ed-Fi client, student sync, data quality dashboard |
| 4. AdultEd Module | 11-13 | 60 | StudentBarrier extension, adult ed features |
| 5. Portal MVP | 14-19 | 120 | Student portal (schedule, progress, attendance, messaging) |
| 6. Investor Prototype | 20-21 | 40 | Polish, demo data, pitch materials |

**Total: 21 sprints, ~360-500 hours** to investor-ready prototype. Ship when ready.

---

## Key Working Patterns Established

### Git Workflow

- `main` branch: stays as CMS-Prod (untouched by Phoenix work)
- `phoenix-dev` branch: all Phoenix development happens here
- Commits reference story IDs: `PHX-1.2: Do the thing`
- Push with `-u origin phoenix-dev` first time, then just `git push`

### GitHub Project Workflow

- Move story from Backlog → Sprint → In Progress → Done
- Set Sprint field to Sprint 1 (or whatever current sprint)
- Set Estimate field (built-in) with estimated hours
- Fill Actual Hours field when done (track velocity)
- Fill Loom URL field with sprint demo recording
- Definition of Done checklists live in issue body

### Sprint Ceremony Cadence

- Sprint = ~20-40 hours of Phoenix work (not calendar days)
- Sprint retro = 5 questions answered in docs/retros/sprint-NN.md
- Sprint demo = 3-minute Loom
- Git tag at sprint end: `phoenix-sprint-NN`

---

## Environmental Setup Confirmed

- ✅ SQL Server: Azure SQL dev server (existing) — will reuse for Ed-Fi
- ✅ Full-Text Search: FulltextInstalled = 1 (verified)
- ✅ Visual Studio 2022 (existing)
- ✅ .NET 8 (existing)
- ✅ PowerShell (existing)
- ✅ Git (existing)
- ✅ GitHub Projects (setup complete)
- ⬜ Loom account (pending, low priority)
- ⬜ Ed-Fi ODS installation (Sprint 5, not yet)

---

## Business / Legal Context

**Warning previously issued (still applicable):**
IP/employment risk exists because CMS was built for New Heights. If Phoenix is a derivative of CMS, Patrick's employer may have claims. Recommendation: consult an employment lawyer before significant Phoenix work is done. Patrick should get written clarification on IP ownership of CMS and derivative works.

**Business model:** Not yet decided. Options include:
- VC-backed SaaS
- State partnership / grant-funded
- Open-source with services
- Bootstrap to profitability

Decision deferred until working prototype exists (Sprint 21).

---

## First Actions for New Cowork Session

When Patrick opens the new Cowork session:

1. Share this handoff document as first message
2. Say: "Ready to start PHX-1.2"
3. Claude should:
   - Acknowledge context received
   - Skip re-explanation of decisions already made
   - Move directly to walkthrough for PHX-1.2
   - Use Cowork's file access tools to read F:\Github\CaseManagementSystem project files as needed
   - Give explicit steps with file paths and code blocks

## Cowork Advantages for This Work

- Can read/write files directly on Patrick's machine
- Can run build commands and see results
- Can commit code changes with proper messages
- Can iterate faster without copy-paste friction

---

## Open Questions and Deferred Decisions

- **Test project:** Add now or defer? (Recommended: defer until Sprint 3-4)
- **Loom setup:** Complete before or after Sprint 1 demo? (Either works)
- **Business model:** Deferred until Sprint 21
- **IP/legal consultation:** Recommended before significant Phoenix commitment
- **Design partner school:** Not yet identified for Phase 0 discovery
- **Timeline pressures:** None currently, ship-when-ready

---

## Files Patrick Should Have Open in New Session

1. This handoff document (for context)
2. Phoenix-SIS-Setup-and-Sprint-1-Issues-v2.docx (Sprint 1 story details)
3. Phoenix-SIS-Solo-Dev-Sprint-Plan.docx (bigger picture)
4. GitHub Project (github.com/users/senihp5/projects/2)
5. Visual Studio 2022 with CaseManagementSystem solution
6. PowerShell/Terminal for git commands

---

## Success Metric for New Session

Complete PHX-1.2 including:
- IFeatureFlagService interface created in Core
- FeatureFlagService implementation created in Infrastructure
- Registered in DI in Program.cs
- Build succeeds
- CMS runs identically
- Startup logs show 8 feature states (or first request triggers logging)
- Commit pushed to phoenix-dev branch
- PHX-1.2 issue moved to Done in project

Estimated: 4-5 hours of work.

---

*End of handoff document. Ready to resume in Cowork.*
