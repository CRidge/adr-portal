---
status: "accepted"
date: 2026-03-19
decision-makers: [ADR Portal Project Team]
consulted: []
informed: []
---
# File-Based ADR Storage with Standard Folder Structure

## Context and Problem Statement

The ADR Portal manages ADRs for software repositories. A core design choice is where ADR content is persisted. The portal must operate against repositories on disk, support folder monitoring for inbox files, enable cross-repo portability of ADR files, and keep ADRs version-controlled alongside the code they govern.

Note: this decision covers ADR *content* storage only. Portal configuration and the global ADR library are stored separately in SQLite — see ADR-0007.

## Decision Drivers

* ADRs must be version-controlled alongside the code they describe
* Folder-monitoring requirement demands files that can be dropped into a directory
* Cross-repo portability: ADRs must be copyable between repositories
* Consistency with established ADR tooling conventions (adr-tools, MADR, Log4brains)
* No external infrastructure dependency for ADR content

## Considered Options

* Markdown files in `docs/adr/` within each repository
* SQLite database file per repository
* Central relational database (PostgreSQL / SQL Server)
* GitHub Issues as ADR storage

## Decision Outcome

Chosen option: "Markdown files in `docs/adr/`", because it satisfies all requirements: ADRs are version-controlled, diffable in PRs, editable outside the portal, portable between repos, and directly compatible with `FileSystemWatcher` for inbox monitoring.

### Consequences

* Good, because ADRs are version-controlled alongside the code they govern — history is visible in `git log` and changes are reviewable in PRs.
* Good, because no external database dependency for ADR content.
* Good, because ADRs are human-readable and editable with any text editor or the GitHub web UI.
* Good, because cross-repo portability is trivial — ADRs are files that can be copied and ID-remapped.
* Bad, because querying large corpora (e.g., finding all ADRs with a given tag) requires scanning all files; acceptable for typical corpus sizes (<500 ADRs).
* Bad, because concurrent writes from multiple portal users to the same directory require conflict detection.
* Bad, because YAML front matter parsing introduces a dependency on a parser; malformed files can cause errors.

### Standard Folder Structure

```
{repo-root}/
└── docs/
    └── adr/
        ├── adr-0001-<slug>.md       # accepted / proposed / deprecated / superseded
        ├── adr-0002-<slug>.md
        └── rejected/
            └── adr-0003-<slug>.md   # rejected ADRs moved here
```

ADR state is stored in the YAML front matter `status` field. Rejected ADRs are moved to a `rejected/` subfolder to keep the main directory clean while preserving history.

## Pros and Cons of the Options

### Markdown files in `docs/adr/`

* Good, because git-native — history, blame, and PR review all work out of the box.
* Good, because zero infrastructure dependency.
* Good, because compatible with `FileSystemWatcher` for inbox monitoring.
* Bad, because no referential integrity — cross-ADR links can become stale.

### SQLite database file per repository

* Good, because efficient querying and referential integrity.
* Bad, because binary file — ADRs lose git history granularity and are not reviewable in PRs.
* Bad, because ADRs cannot be edited outside the portal.

### Central relational database

* Good, because powerful querying across all repos simultaneously.
* Bad, because introduces infrastructure dependency (database server).
* Bad, because ADRs are no longer co-located with the code they document.

### GitHub Issues as ADR storage

* Good, because built-in commenting and review workflows.
* Bad, because requires GitHub API access; inbox monitoring via file drop is not possible.
* Bad, because ADRs are not co-located with the codebase.

## More Information

* [ADR-0006: MADR 4.0.0 as ADR Format](adr-0006-madr-format.md)
* [ADR-0007: SQLite for Portal Configuration and Global Library](adr-0007-sqlite-ef-core-persistence.md)
* [MADR — Markdown Architectural Decision Records](https://adr.github.io/madr/)
* [adr.github.io — ADR tooling and formats](https://adr.github.io/)