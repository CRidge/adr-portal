# ADR Portal — Implementation Plan

## Problem Statement

Build a locally-hosted Blazor Server web portal (.NET 10) for managing Architectural Decision Records (MADR 4.0.0 format) across multiple git repositories simultaneously. The portal automates the full ADR lifecycle: inbox monitoring, state transitions via Git PR workflow, AI-assisted evaluation, and cross-repo relevance analysis.

---

## Confirmed Decisions

| Decision           | Choice                                                                             |
| ------------------ | ---------------------------------------------------------------------------------- |
| Framework          | .NET 10 Blazor Web App (Interactive Server)                                        |
| Testing            | **TUnit exclusively** — never xUnit/NUnit/MSTest                                   |
| Package management | NuGet Central Package Management (`Directory.Packages.props`)                      |
| ADR format         | **MADR 4.0.0**                                                                     |
| ADR states         | proposed → accepted / rejected (→ `/docs/adr/rejected/`) / superseded / deprecated |
| Folder monitoring  | Configurable per-repo inbox folder; auto-import on file creation                   |
| AI provider        | `Microsoft.Extensions.AI` abstraction; GitHub Copilot SDK as default               |
| Git integration    | PR workflow — proposed ADRs create branch+PR; accept/reject merges/closes PR       |
| Credentials        | `GITHUB_TOKEN` first, then ambient git config (local fallback); error if unavailable |
| Multi-repo         | Workspace model — multiple repos open simultaneously                               |
| Source→target      | AI-only relevance determination                                                    |
| Affected ADRs      | AI analysis                                                                        |
| Persistence        | SQLite via EF Core (file path configurable; Docker volume-mounted)                 |
| Orchestration      | Aspire 13.1.3 for local dev                                                        |
| Deployment         | HTTP only; no in-app auth; Docker image later                                      |
| UI design          | No default Blazor theme; clean, minimalistic, modern; **no gradients**             |

---

## Solution Structure

```
AdrPortal.slnx
├── src/
│   ├── AdrPortal.AppHost/               # Aspire AppHost (Aspire 13.1.3)
│   ├── AdrPortal.ServiceDefaults/       # Aspire service defaults (OTEL, health checks)
│   ├── AdrPortal.Web/                   # Blazor Server (Interactive Server, no default theme)
│   ├── AdrPortal.Core/                  # Domain models, interfaces, business logic, MADR parser
│   └── AdrPortal.Infrastructure/        # EF Core/SQLite, File I/O, Git, AI, FSW
├── tests/
│   ├── AdrPortal.Core.Tests/            # TUnit — domain + MADR parser tests
│   ├── AdrPortal.Infrastructure.Tests/  # TUnit — file repo, git, AI service tests
│   └── AdrPortal.Web.Tests/             # TUnit — Blazor component/integration tests
├── Directory.Packages.props             # Central Package Management
├── Directory.Build.props                # Shared build settings
└── global.json                          # .NET 10 SDK pinning
```

---

## Key Domain Types (AdrPortal.Core)

### Adr

```csharp
public sealed record Adr
{
    public int Number { get; init; }                  // e.g. 7
    public string Slug { get; init; }                 // e.g. "use-postgresql"
    public string RepoRelativePath { get; init; }     // e.g. docs/adr/adr-0007-use-postgresql.md
    public string Title { get; init; }
    public AdrStatus Status { get; init; }
    public DateOnly Date { get; init; }
    public Guid? GlobalId { get; init; }              // null until ADR is accepted; embedded in front matter
    public IReadOnlyList<string> DecisionMakers { get; init; }
    public IReadOnlyList<string> Consulted { get; init; }
    public IReadOnlyList<string> Informed { get; init; }
    public int? SupersededByNumber { get; init; }
    public string RawMarkdown { get; init; }          // full file content
}

public enum AdrStatus { Proposed, Accepted, Rejected, Superseded, Deprecated }
```

### Interfaces

```csharp
// File system
public interface IAdrFileRepository
{
    Task<IReadOnlyList<Adr>> GetAllAsync(CancellationToken ct);
    Task<Adr?> GetByNumberAsync(int number, CancellationToken ct);
    Task<Adr> WriteAsync(Adr adr, CancellationToken ct);
    Task MoveToRejectedAsync(int number, CancellationToken ct);
    Task<int> GetNextNumberAsync(CancellationToken ct);
}

// Git / PR
public interface IGitService
{
    Task<string> CreateProposalBranchAsync(Adr adr, CancellationToken ct);
    Task<Uri> CreatePullRequestAsync(Adr adr, string branchName, CancellationToken ct);
    Task MergePullRequestAsync(Uri prUrl, CancellationToken ct);
    Task ClosePullRequestAsync(Uri prUrl, CancellationToken ct);
}

// Global ADR Library
public interface IGlobalAdrRegistry
{
    // Register a newly accepted ADR as library entry v1 (first time only)
    Task<Guid> RegisterAsync(Adr adr, int repositoryId, CancellationToken ct);

    // Scan repo on add — link any .md files with global-id/global-version to existing library entries
    Task ReconcileRepoAsync(int repositoryId, CancellationToken ct);

    // Repo ADR was edited — detect divergence from base template version; set HasLocalChanges
    Task DetectLocalChangesAsync(Guid globalId, int repositoryId, string currentMarkdown, CancellationToken ct);

    // User proposes a library update from a changed repo ADR (creates pending proposal)
    Task<GlobalAdrUpdateProposal> ProposeTemplateUpdateAsync(
        Guid globalId, string newMarkdown, string changeNotes, int fromRepositoryId, CancellationToken ct);

    // Accept pending proposal — creates a new GlobalAdrVersion and bumps CurrentVersion
    Task<GlobalAdrVersion> AcceptTemplateUpdateAsync(Guid globalId, int proposalId, CancellationToken ct);

    // Discard pending proposal with no new version created
    Task DiscardTemplateUpdateAsync(Guid globalId, int proposalId, CancellationToken ct);

    // User reviewed the template update for their repo and chose to apply it (triggers Git/PR workflow)
    Task ApplyUpdateToRepoAsync(Guid globalId, int repositoryId, string resolvedMarkdown, CancellationToken ct);

    // User dismissed the update for their repo (keeps repo ADR as-is; clears UpdateAvailable for this instance)
    Task DismissUpdateForRepoAsync(Guid globalId, int repositoryId, CancellationToken ct);

    Task<GlobalAdr?> GetByIdAsync(Guid globalId, CancellationToken ct);
    Task<IReadOnlyList<GlobalAdr>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<GlobalAdrInstance>> GetInstancesAsync(Guid globalId, CancellationToken ct);
    Task<GlobalAdrVersion> GetVersionAsync(Guid globalId, int version, CancellationToken ct);
}

// AI
public interface IAiService
{
    Task<IReadOnlyList<string>> SuggestAlternativesAsync(
        string problemStatement, IReadOnlyList<Adr> existingAdrs, CancellationToken ct);

    Task<AdrRecommendation> EvaluateAndRecommendAsync(
        string adrDraft, IReadOnlyList<Adr> existingAdrs, CancellationToken ct);

    Task<IReadOnlyList<AffectedAdrResult>> FindAffectedAdrsAsync(
        Adr newAdr, IReadOnlyList<Adr> existingAdrs, CancellationToken ct);

    Task<IReadOnlyList<RelevanceResult>> EvaluateSourceRelevanceAsync(
        IReadOnlyList<Adr> sourceAdrs, IReadOnlyList<Adr> targetAdrs, CancellationToken ct);

    // Scan a codebase with no existing ADRs and suggest a set of proposed ADRs
    Task<IReadOnlyList<AdrDraft>> BootstrapAdrsFromCodebaseAsync(
        string repoRootPath, CancellationToken ct);
}

// Inbox
public interface IInboxWatcherService
{
    void Start(string inboxPath);
    void Stop();
    event EventHandler<Adr> AdrImported;
}
```

---

## EF Core / SQLite (AdrPortal.Infrastructure)

Two concerns are stored in the database: portal configuration (repos) and the **global ADR library** (versioned templates). ADR *content* continues to live in markdown files on disk — the library does not replace repo ADRs, it is a reference catalogue they can be linked to.

```csharp
public class ManagedRepository  // EF Core entity
{
    public int Id { get; set; }
    public string DisplayName { get; set; }
    public string RootPath { get; set; }            // absolute path on disk
    public string AdrFolder { get; set; }           // default: "docs/adr"
    public string? InboxFolder { get; set; }        // null = inbox monitoring disabled
    public string? GitRemoteUrl { get; set; }       // e.g. https://github.com/org/repo
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ── Global ADR Library ────────────────────────────────────────────────────────

public class GlobalAdr          // one entry per unique ADR topic in the library
{
    public Guid GlobalId { get; set; }              // PK — embedded in .md front matter as global-id
    public string Title { get; set; }               // canonical title (tracks latest version)
    public int CurrentVersion { get; set; }         // version number of latest accepted template
    public DateTime RegisteredAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public ICollection<GlobalAdrVersion> Versions { get; set; }
    public ICollection<GlobalAdrUpdateProposal> PendingProposals { get; set; }
    public ICollection<GlobalAdrInstance> Instances { get; set; }
}

public class GlobalAdrVersion   // immutable snapshot — one row per accepted library version
{
    public int Id { get; set; }
    public Guid GlobalId { get; set; }              // FK → GlobalAdr
    public int Version { get; set; }                // 1, 2, 3 ...
    public string Markdown { get; set; }            // full template .md at this version
    public string? ChangeNotes { get; set; }        // what changed vs previous version
    public DateTime CreatedAt { get; set; }
    public int? SourceRepositoryId { get; set; }    // which repo ADR triggered this version (nullable)
}

public class GlobalAdrUpdateProposal  // pending repo→library update awaiting accept/discard
{
    public int Id { get; set; }
    public Guid GlobalId { get; set; }              // FK → GlobalAdr
    public int ProposedFromVersion { get; set; }    // base version proposal was created from
    public string Markdown { get; set; }            // proposed next template markdown
    public string ChangeNotes { get; set; }         // rationale for proposed update
    public int FromRepositoryId { get; set; }       // origin repository
    public DateTime ProposedAt { get; set; }
}

public class GlobalAdrInstance  // one row per repo that holds an ADR linked to this template
{
    public int Id { get; set; }
    public Guid GlobalId { get; set; }              // FK → GlobalAdr
    public int RepositoryId { get; set; }           // FK → ManagedRepository
    public int LocalAdrNumber { get; set; }         // local ID in that repo (may differ)
    public string RepoRelativeFilePath { get; set; }
    public AdrStatus LastKnownStatus { get; set; }
    public int BaseTemplateVersion { get; set; }    // last library version this instance was reviewed against
    public bool HasLocalChanges { get; set; }       // repo ADR content diverges from its base template version
    public bool UpdateAvailable { get; set; }       // library has newer version than BaseTemplateVersion
    public DateTime LastReviewedAt { get; set; }
}
```

### Mental model

```
Library (SQLite)                       Repos (disk .md files)
─────────────────────                  ──────────────────────
GlobalAdr (topic)                      Repo A: adr-0007  ──┐
  └─ Version 1  ◄── base template ────────── global-id: X  │
  └─ Proposal #17 ◄── submitted from Repo A (pending review)
  └─ Version 2  ◄── accepted from Proposal #17
  └─ Version 3                         Repo B: adr-0003  ──┘
                                            global-id: X
```

- Library templates are reference documents — **not authoritative over repo ADRs**
- Each repo ADR is **individually owned** by its repo; it may diverge from the template intentionally
- Version bumps on the library template **never automatically change repo files**

### Global ID in MADR front matter

`global-id` and `global-version` are MADR 4.0.0 extension fields (the spec allows additional fields):

```yaml
---
status: accepted
date: 2025-01-15
global-id: "550e8400-e29b-41d4-a716-446655440000"
global-version: 2        # which library template version this was last reviewed against
---
```

Proposed ADRs do **not** have a `global-id` yet — it is assigned at the moment of acceptance.

### Two-direction review workflow

**Direction 1 — Repo ADR → Library** (repo changes, library may need updating)

1. An accepted ADR in a repo is edited and saved
2. Portal detects content diverges from the library template version it was based on
3. Badge shown on ADR detail: *"This ADR has local changes vs library template v{N}"*
4. Action: **"Propose library update"** → user writes change notes → creates a pending `GlobalAdrUpdateProposal`
5. Any user can **accept** or **discard** the proposal on `/global/{globalId}`
6. On accept: a new `GlobalAdrVersion` row is created from the proposal; all other instances get `UpdateAvailable = true`

**Direction 2 — Library → Repos** (template updated, repos may need reviewing)

1. Library template version bumped
2. Portal finds all `GlobalAdrInstance` rows with `BaseTemplateVersion < CurrentVersion`; sets `UpdateAvailable = true`
3. Notification badge on `/repos/{id}` and the global dashboard: *"N ADRs have library updates available"*
4. On `/global/{globalId}`: shows diff between old and new template version
5. Per-repo action: **"Review for [Repo A]"** → opens a diff view (template change vs repo ADR) → user chooses:
   - **Apply** (write updated content to repo .md via Git/PR workflow, bump `BaseTemplateVersion`)
   - **Customise** (edit merged result manually before committing)
   - **Dismiss** (keep repo ADR as-is; `UpdateAvailable` cleared for this instance only)

### Out-of-sync detection

On portal load and on FSW change events, each accepted ADR's `.md` content hash is compared against the corresponding `GlobalAdrVersion.Markdown` hash at `BaseTemplateVersion`. Divergence sets `HasLocalChanges = true` in the DB.

SQLite file path: configurable via `ConnectionStrings:AdrPortal` in `appsettings.json` / env var.

---

## MADR 4.0.0 Parser (AdrPortal.Core)

- Split on first and second `---` fence to extract YAML front matter
- Parse YAML with **YamlDotNet**
- Parse body sections with **Markdig**
- Writer: regenerate front matter from `Adr` record + preserve body
- Validation: ensure required fields present; surface errors as domain exceptions

---

## Blazor Pages & Components (AdrPortal.Web)

| Route                           | Purpose                                                                      |
| ------------------------------- | ---------------------------------------------------------------------------- |
| `/`                             | Dashboard: repos summary, recent activity, global out-of-sync badge          |
| `/settings/repos`               | Add / edit / remove managed repositories                                     |
| `/repos/{id}`                   | ADR list for repo (filterable by status, searchable) + drag-drop inbox zone  |
| `/repos/{id}/adr/{number}`      | ADR detail: rendered MADR + action buttons + sync status badge               |
| `/repos/{id}/adr/new`           | Create ADR: template form + AI assist panel                                  |
| `/repos/{id}/adr/{number}/edit` | Edit ADR: raw form                                                           |
| `/compare`                      | Source → target relevance analysis                                           |
| `/global`                       | Global ADR library: all registered ADRs with per-repo sync status overview   |
| `/global/{globalId}`            | Single global ADR: version history, diff viewer, all instances, sync actions |

### ADR state actions (detail page)

| Current State                      | Available Actions                                             |
| ---------------------------------- | ------------------------------------------------------------- |
| Proposed                           | Accept → PR merged \| Reject → PR closed + move to /rejected/ |
| Accepted                           | Supersede (link to new ADR) \| Deprecate                      |
| Rejected / Deprecated / Superseded | Read-only                                                     |

---

## Inbox Watcher Service (AdrPortal.Infrastructure)

1. `FileSystemWatcher` on configured `InboxFolder` — watch `*.md` Created events
2. On event: read file → parse as MADR (best-effort; missing front matter gets defaults)
3. Assign next sequential ID from the repo's ADR folder
4. Write to `{AdrFolder}/adr-{NNNN}-{slug}.md` with `status: proposed`
5. Delete source file from inbox
6. Fire `AdrImported` event → Blazor component reloads list via `InvokeAsync`
7. If parsing fails: leave file in inbox, write error to `{inboxFolder}/.errors/`

---

## Inbox Drag-Drop Upload (AdrPortal.Web)

A drag-drop zone on `/repos/{id}` allows users to upload `.md` files directly to the repo's inbox without touching the filesystem manually.

- Rendered as a styled drop zone on the ADR list page (visible only when `InboxFolder` is configured)
- If inbox is not configured: show a disabled drop zone with a message linking to repo settings
- Uses Blazor `InputFile` component with drag-and-drop CSS overlay (HTML5 `dragover`/`drop` events via JS interop)
- Accepts **only `.md` files**; rejects others with inline error message
- Supports **multiple files** dropped simultaneously
- On drop: read file bytes via `IBrowserFile.OpenReadStream()` → write to `{InboxFolder}/{filename}` on disk
- `InboxWatcherService` then picks up the file automatically (no separate import path needed)
- UI feedback: per-file progress indicator → success/error state per file
- Filename collision: append `-{timestamp}` suffix before writing to inbox

---

## AI Codebase Bootstrap

When a repo is added with no existing ADRs, a **"Bootstrap ADRs with AI"** button is shown.

- AI scans the repo's source code (file tree + key files: solution files, `README`, config files, dependency manifests)
- Returns a list of `AdrDraft` suggestions: title + problem statement + initial options
- Each draft is shown as a card — user selects which to keep
- Selected drafts are written to the ADR folder with `status: proposed` and queued for the Git/PR workflow
- Uses `IAiService.BootstrapAdrsFromCodebaseAsync`

---

## Git / PR Service (AdrPortal.Infrastructure)

- **LibGit2Sharp** for local operations (init, stage, commit, create branch, push)
- **Octokit.NET** for GitHub PR creation/merge/close
- Credential resolution:
  - Local: `GITHUB_TOKEN` env var → ambient git config credential helper → error
  - Deployed: `GITHUB_TOKEN` env var (required, explicit error if missing)
- Branch naming: `proposed/adr-{NNNN}-{slug}`
- On Accept: `IGitService.MergePullRequestAsync` (PR contains `status: accepted` and metadata updates)
- On Reject: `IGitService.ClosePullRequestAsync` → `IAdrFileRepository.MoveToRejectedAsync`
- Optional: `IGitService.ClosePullRequestAsync` for withdrawn proposals before a decision
- Note: GitLab support via `IGitService` second implementation (out of scope for v1; flag in ADR)

---

## AI Service (AdrPortal.Infrastructure)

- Configure `IChatClient` from `Microsoft.Extensions.AI`
- Default: GitHub Copilot provider via configured `Microsoft.Extensions.AI` adapter registration
- All AI calls are grounded: inject relevant existing ADRs as context in the system prompt
- Features:
  - **Suggest Alternatives**: given problem statement → list of alternative approaches
  - **Evaluate & Recommend**: given draft ADR + existing ADRs → structured recommendation (preferred option + rationale)
  - **Find Affected ADRs**: given new/edited ADR → list of existing ADRs affected + why
  - **Source Relevance**: given source ADR list + target ADR list → relevance scores + reasoning
  - **Bootstrap**: given repo file tree + key files → list of `AdrDraft` suggestions
- All responses typed as structured C# records deserialized from AI JSON output

---

## Source → Target Comparison Flow

1. User selects source repo + target repo in `/compare`
2. System loads all ADRs from both repos
3. AI call: `EvaluateSourceRelevanceAsync` — returns `RelevanceResult[]` with scores + rationale
4. UI shows ranked list: relevant source ADRs → user checks which to import
5. Import: copy markdown, assign new sequential ID in target, set `status: proposed`, add `source-adr` metadata field
6. If imported ADR carries a `global-id`: create `GlobalAdrInstance` for target repo (preserve existing library link)
7. If ID conflict detected → surface to user in UI before import completes

---

## UI Design System

- **No default Blazor CSS** (remove `app.css` and bootstrap imports)
- Custom CSS file with CSS variables for theming
- Colors: neutral gray scale; single accent (slate/indigo)
- No gradients — flat, clean surfaces
- Typography: system font stack (`-apple-system, Segoe UI, sans-serif`)
- Status badges: amber=proposed, green=accepted, red=rejected, gray=deprecated, blue=superseded
- Dark/light mode via CSS `prefers-color-scheme` + manual toggle stored in `localStorage`
- Responsive: desktop-first (1200px+), readable down to tablet
- Sidebar: repo list + quick status filters; collapsible

---

## Testing Strategy (TUnit only)

- **Core.Tests**: MADR parser round-trips, state machine transitions, domain validation
- **Infrastructure.Tests**: file repo CRUD (temp directories), inbox watcher (file drop simulation), AI service (mocked `IChatClient`), global registry sync logic
- **Web.Tests**: Blazor component rendering (bUnit or `WebApplicationFactory`), route-level integration tests
- Test data: synthetic MADR `.md` fixtures in `tests/fixtures/`

---

## NuGet Central Package Management

`Directory.Packages.props` packages (initial):

```xml
<!-- Framework -->
Microsoft.AspNetCore.Components.Web
Microsoft.EntityFrameworkCore / .Sqlite / .Design
<!-- Parsing -->
YamlDotNet
Markdig
<!-- Git -->
LibGit2Sharp
Octokit
<!-- AI -->
Microsoft.Extensions.AI
Provider-specific Microsoft.Extensions.AI package (selected during integration)
<!-- Aspire -->
Aspire.Hosting (AppHost)
Microsoft.Extensions.ServiceDiscovery
OpenTelemetry.Exporter.OpenTelemetryProtocol
<!-- Testing (TUnit) -->
TUnit
TUnit.Assertions
TUnit.Core
Microsoft.AspNetCore.Mvc.Testing
bunit
```

---

## Docker Support (Phase 2)

- `Dockerfile` multi-stage: `sdk:10` build → `aspnet:10` runtime
- `ASPNETCORE_URLS=http://+:8080`
- Volumes:
  - `/data/adr-portal.db` — SQLite database
  - `/repos/{name}` — mounted git repositories (read-write)
- Env vars: `GITHUB_TOKEN`, `ConnectionStrings__AdrPortal`, `ASPNETCORE_ENVIRONMENT`
- `docker-compose.yml` for convenient local testing with volume mounts

---

## Aspire AppHost (Local Dev)

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var web = builder.AddProject<Projects.AdrPortal_Web>("web")
    .WithEnvironment("ConnectionStrings__AdrPortal", "Data Source=./adr-portal.db");

builder.Build().Run();
```

Aspire provides: dashboard, OTEL traces/logs, health endpoint wiring.

---

## Implementation Phases

### Phase 1 — Solution Scaffold

- Create `.slnx`, all projects, `Directory.Packages.props`, `Directory.Build.props`, `global.json`
- Aspire AppHost + ServiceDefaults wired up
- EF Core migrations for `ManagedRepository`
- Minimal Blazor app with layout + custom CSS shell (no content yet)

### Phase 2 — MADR Parser + File Repository

- `MadrParser` and `MadrWriter` (round-trip tests)
- `AdrFileRepository` implementation
- Core domain tests pass
  
  

### Phase 3 — Visual design

- Create the general visual design of the web app
- It should be minimalistic, stylish and user friendly!
- Do not use gradient colors
- Create a single-file mockup in the repo (HTML)

### Phase 4 — Repository Management UI

- `/settings/repos` — add/edit/remove repos
- Repos persisted via EF Core
- Sidebar shows repo list

### Phase 5 — ADR Browse & View

- `/repos/{id}` — ADR list with status filter + search
- `/repos/{id}/adr/{number}` — rendered MADR detail page
- State badge display

### Phase 6 — ADR Create/Edit

- `/repos/{id}/adr/new` — MADR template form
- `/repos/{id}/adr/{number}/edit`
- File written on save

### Phase 7 — State Transitions + Library Registration

- Propose → Accept / Reject (without Git initially; Git wired after)
- Deprecate, Supersede (link to new ADR number)
- On Accept: generate `global-id` + `global-version: 1`, write to front matter, register in library (`IGlobalAdrRegistry.RegisterAsync`)
- On Accept of an already-linked ADR (imported from another repo): create `GlobalAdrInstance` only, preserve existing `global-id`

### Phase 8 — Global ADR Library UI + Sync Workflows

- EF Core migrations for `GlobalAdr`, `GlobalAdrVersion`, `GlobalAdrInstance`
- `ReconcileRepoAsync` on repo add — scan `.md` files for existing `global-id` fields
- `/global` — library overview: all registered ADRs, version number, count of instances, update-available count
- `/global/{globalId}` — template detail: version history, diff viewer between versions, list of all repo instances with status badges (`HasLocalChanges`, `UpdateAvailable`)
- **Direction 1** (repo → library): "Propose library update" button on ADR detail page when `HasLocalChanges = true`; review + accept/discard flow on `/global/{globalId}`
- **Direction 2** (library → repos): per-instance "Review update" action on `/global/{globalId}`; diff view (template change vs repo ADR); Apply / Customise / Dismiss actions
- `DetectLocalChangesAsync` wired to FSW change events and on-save hooks
- Dashboard badge: total count of ADRs with pending proposals or update-available instances

### Phase 9 — AI Codebase Bootstrap

- `IAiService.BootstrapAdrsFromCodebaseAsync` implementation
- "Bootstrap ADRs with AI" button on empty-repo view
- Card selection UI for reviewing and accepting/discarding AI-suggested drafts
- Accepted drafts written as proposed ADRs and queued for Git/PR workflow

### Phase 10 — Inbox Watcher + Drag-Drop

- `InboxWatcherService` background service
- Auto-import flow tested
- Per-repo enable/disable in settings
- Drag-drop zone on `/repos/{id}` (Blazor `InputFile` + JS interop)
- Filename collision handling

### Phase 11 — Git / PR Integration

- LibGit2Sharp branch creation + commit
- Octokit PR creation / merge / close
- Credential resolution (env var → git config → error UI)
- Wire library update "Apply" action through Git/PR workflow

### Phase 12 — AI Integration (remaining features)

- `Microsoft.Extensions.AI` + Copilot SDK registration
- Suggest Alternatives in create form
- Evaluate & Recommend in create/edit form
- Find Affected ADRs shown on ADR detail

### Phase 13 — Source → Target Comparison

- `/compare` UI
- AI relevance scoring
- Import workflow with ID remapping
- Preserve `global-id`/`global-version` when importing a library-linked ADR

### Phase 14 — Polish & Docker

- Dark/light mode toggle
- Docker image + compose file
- End-to-end testing pass

---

## Open Technical Notes

1. **GitLab support**: `IGitService` abstraction supports it; `GitHubGitService` is GitHub-only for v1. Document in ADR.
2. **Inbox path**: no default convention — must be explicitly configured per repo.
3. **Token separation**: use `GITHUB_TOKEN` for git operations and `COPILOT_TOKEN` for AI provider access; document fallback and error behavior in README.
4. **Cross-repo ID conflict**: surfaced as blocking UI dialog before import — user must confirm.
