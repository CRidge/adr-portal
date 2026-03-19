---
status: "accepted"
date: 2026-03-19
decision-makers: [ADR Portal Project Team]
consulted: []
informed: []
---
# Global ADR Versioned Library for Cross-Repo Reuse and Sync

## Context and Problem Statement

When the portal manages multiple repositories simultaneously, the same architectural decision (e.g., "use PostgreSQL for relational data") may be accepted independently in several repos. Without a mechanism to link these instances, the same decision diverges silently over time and there is no way to propagate an update to the decision across all repos that adopted it. A global ADR library is needed that tracks accepted ADRs as versioned templates and records which repos hold instances of each template.

## Decision Drivers

* Multiple repos may independently adopt the same architectural decision
* When a decision evolves in one repo, other repos that adopted it should be notified
* Changes must never be automatically applied to repos — every update requires human review
* ADR content remains in each repo's own markdown files; the library is a reference catalogue, not a replacement
* The library must survive container restarts (persisted in SQLite — see ADR-0007)

## Considered Options

* Versioned library in SQLite with `global-id` / `global-version` fields embedded in ADR front matter
* Git submodule or shared repository of "canonical" ADRs
* No cross-repo linkage (repos remain fully independent)

## Decision Outcome

Chosen option: "Versioned library in SQLite with `global-id` / `global-version` front matter fields", because it keeps each repo ADR fully independent (owned by its repo), provides a clear audit trail of template versions, and enables explicit human-reviewed sync in both directions without coupling repo files to a shared git repository.

### Consequences

* Good, because repo ADRs remain individually owned — they can diverge from the template intentionally.
* Good, because version history is immutable; every accepted template change is a new `GlobalAdrVersion` row, never an edit.
* Good, because sync is always explicit and human-reviewed — no automated file changes.
* Good, because `global-id` in the front matter makes linkage visible in the markdown file itself.
* Bad, because adds complexity: two directions of change (repo → library, library → repos) must both be surfaced and managed.
* Bad, because the library only covers the portal's managed repositories; repos not known to the portal cannot participate.

### Data model summary

```
GlobalAdr           — one entry per unique ADR topic
  └─ GlobalAdrVersion  — immutable snapshot per accepted version (v1, v2, ...)
  └─ GlobalAdrInstance — one per repo holding this ADR; tracks BaseTemplateVersion, HasLocalChanges, UpdateAvailable
```

### Two-direction workflow

**Repo → Library**: When a repo ADR diverges from its base template version, a "Propose library update" action creates a pending `GlobalAdrVersion`. Any user can accept or discard the proposal. On accept, all other instances are flagged `UpdateAvailable = true`.

**Library → Repo**: When a new library version exists, each repo instance can Review (diff view), then Apply (Git/PR workflow), Customise (edit before committing), or Dismiss (keep repo ADR as-is).

### Front matter extensions

```yaml
global-id: "550e8400-e29b-41d4-a716-446655440000"  # links to GlobalAdr.GlobalId
global-version: 2                                   # GlobalAdrVersion.Version last reviewed against
```

## Pros and Cons of the Options

### Versioned library in SQLite with global-id front matter

* Good, because repo files remain independent and git-native.
* Good, because version history is immutable and auditable.
* Good, because sync is always explicit.
* Bad, because adds a two-direction sync workflow that users must understand.

### Git submodule of canonical ADRs

* Good, because git-native versioning.
* Bad, because submodule workflows are complex and error-prone.
* Bad, because all repos sharing the submodule are coupled to its history.

### No cross-repo linkage

* Good, because maximum simplicity — each repo is fully independent.
* Bad, because the same decision evolves separately in every repo with no visibility into divergence.
* Bad, because there is no mechanism to propagate an improved decision to all adopters.

## More Information

* [ADR-0005: File-Based ADR Storage](adr-0005-file-based-adr-storage.md)
* [ADR-0006: MADR 4.0.0 Format](adr-0006-madr-format.md)
* [ADR-0007: SQLite with EF Core](adr-0007-sqlite-ef-core-persistence.md)
