---
title: "ADR-0005: File-Based ADR Storage with Standard Folder Structure"
status: "Accepted"
date: "2026-03-19"
authors: "ADR Portal Project Team"
tags: ["architecture", "decision", "storage", "file-system", "adr-format"]
supersedes: ""
superseded_by: ""
---

# ADR-0005: File-Based ADR Storage with Standard Folder Structure

## Status

**Accepted**

## Context

The ADR Portal manages ADRs for software repositories. A fundamental design choice is where and how ADR data is persisted. The options range from a dedicated database to keeping ADRs as plain markdown files co-located within the repositories they document.

The project requirements are explicit:

- The portal operates against **a given repo on disk** and reads/writes ADRs from that repository.
- ADRs in all lifecycle states (proposed, accepted, superseded, retired, rejected) must be discoverable via a **predictable, standard folder structure**.
- The system must be able to **monitor a folder** and treat newly added `.md` files as proposed ADRs.
- A **source-to-target repo evaluation** workflow must be supported, implying ADRs are portable artifacts that live within repos.

These requirements strongly favour file-system-based storage over a separate database, keeping ADRs as version-controlled markdown files inside repositories — consistent with established ADR tooling (adr-tools, Log4brains, etc.).

## Decision

ADRs will be stored as **markdown files** within the repository they document, under a standard folder structure rooted at `docs/adr/`. The portal reads from and writes to this folder on disk. No separate database is used for ADR content. Metadata required for portal operation (e.g., watch state, cross-repo mappings) may be stored in a lightweight sidecar file (`.adr-portal.json`) at the repo root.

### Standard Folder Structure

```
{repo-root}/
└── docs/
    └── adr/
        ├── adr-0001-<title-slug>.md     # Accepted
        ├── adr-0002-<title-slug>.md     # Superseded
        ├── adr-0003-<title-slug>.md     # Proposed
        └── ...
```

ADR state (proposed, accepted, rejected, superseded, retired) is stored in the **YAML front matter** of each markdown file (`status` field), not in the folder hierarchy. This keeps all ADRs in a single scannable directory while still allowing filtering by status.

## Consequences

### Positive

- **POS-001**: ADRs are version-controlled alongside the code they govern — developers can see ADR history in `git log`, review ADR changes in pull requests, and trace decisions to commits.
- **POS-002**: No external database dependency; the portal has zero persistence infrastructure requirements beyond a file system.
- **POS-003**: ADRs are human-readable and editable outside the portal — any text editor or GitHub web UI can view and propose changes.
- **POS-004**: Portability between repositories is trivial — ADRs are just files that can be copied and ID-remapped.
- **POS-005**: `FileSystemWatcher` integration is straightforward for the folder-monitoring use case.
- **POS-006**: Consistent with established ADR community conventions (adr-tools, MADR, Log4brains all use this pattern).

### Negative

- **NEG-001**: Querying across large ADR corpora (e.g., finding all ADRs with a given tag) requires scanning all files, which is slower than a database query. Acceptable for typical ADR corpus sizes (<500 ADRs).
- **NEG-002**: Concurrent writes from multiple portal users to the same repo directory require a locking or conflict-detection strategy.
- **NEG-003**: Front matter parsing introduces a dependency on a YAML parser and contract with the markdown schema; malformed files can cause parse errors.
- **NEG-004**: No referential integrity — an ADR can reference a superseded-by ADR that doesn't exist. The portal must implement its own consistency validation.

## Alternatives Considered

### SQLite database per repository

- **ALT-001**: **Description**: Store ADR content and metadata in a SQLite database file (`.adr-portal.db`) at the repo root.
- **ALT-002**: **Rejection Reason**: ADRs become binary/opaque to git — they lose version history granularity, are not reviewable in PRs, and cannot be edited outside the portal. Breaks the core principle that ADRs are first-class repository documents.

### Central PostgreSQL / SQL Server database

- **ALT-003**: **Description**: A shared relational database storing ADRs for all managed repositories.
- **ALT-004**: **Rejection Reason**: Introduces infrastructure dependency (database server), connection management, and data synchronisation concerns. ADRs for a given repo would no longer live in that repo. Contradicts the requirement to "work on a given repo on disk".

### GitHub Issues / GitHub Discussions as ADR storage

- **ALT-005**: **Description**: Store proposed and accepted ADRs as GitHub Issues with structured templates.
- **ALT-006**: **Rejection Reason**: Creates a dependency on GitHub API availability, authentication, and rate limits. Folder-monitoring requirement cannot be met. ADRs are not co-located with the codebase in a portable way.

## Implementation Notes

- **IMP-001**: Implement an `IAdrRepository` interface in `ADRPortal.Core` with methods for `GetAll`, `GetByStatus`, `GetById`, `Save`, and `Delete`. Bind to a `FileSystemAdrRepository` in `ADRPortal.Infrastructure` that reads/writes the `docs/adr/` directory.
- **IMP-002**: Use [YamlDotNet](https://github.com/aaubry/YamlDotNet) or [Markdig](https://github.com/xoofx/markdig) with a YAML front matter extension to parse ADR markdown files. Define a canonical `AdrFrontMatter` model with required fields: `title`, `status`, `date`, `authors`, `tags`, `supersedes`, `superseded_by`.
- **IMP-003**: Register a `FileSystemWatcher` service that monitors the configured `docs/adr/` path. On `.md` file creation, parse the file and surface it as a proposed ADR if no `status` front matter is present, defaulting status to `Proposed`.
- **IMP-004**: When assigning ADR IDs in a target repository during cross-repo import, scan the existing `docs/adr/` directory for the highest numeric ID and assign the next sequential value, preserving the original ID in a `source_id` front matter field.
- **IMP-005**: Implement an `AdrConsistencyValidator` that checks: referenced `supersedes`/`superseded_by` IDs exist, linked ADRs have consistent mutual references, and no two ADRs share the same ID.

## References

- **REF-001**: [ADR-0004: Copilot SDK for AI-Assisted ADR Evaluation](./adr-0004-copilot-sdk-ai-integration.md)
- **REF-002**: [adr.github.io — ADR tooling and formats](https://adr.github.io/)
- **REF-003**: [MADR — Markdown Architectural Decision Records](https://adr.github.io/madr/)
- **REF-004**: [Maintain an ADR — Microsoft Azure Well-Architected Framework](https://learn.microsoft.com/en-us/azure/well-architected/architect-role/architecture-decision-record)
- **REF-005**: [concept.md — Use cases section](../../concept.md)
