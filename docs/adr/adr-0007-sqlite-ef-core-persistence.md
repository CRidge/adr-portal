---
status: "accepted"
date: 2026-03-19
decision-makers: [ADR Portal Project Team]
consulted: []
informed: []
---
# SQLite with EF Core for Portal Configuration and Global Library Persistence

## Context and Problem Statement

The ADR Portal requires persistent storage for two concerns that cannot live in repository markdown files: (1) the list of managed repositories and their settings (inbox path, ADR folder, Git remote URL), and (2) the global ADR library with versioned templates and per-repo instance tracking. This storage must work locally without infrastructure, survive container restarts when mounted as a Docker volume, and be manageable via EF Core migrations.

## Decision Drivers

* No external infrastructure dependency for local developer use
* Must survive Docker container restarts when the database file is volume-mounted
* EF Core integration for schema migrations and strongly-typed queries
* Aspire dashboard integration for observability
* Distinct from ADR content storage (which lives in markdown files — see ADR-0005)

## Considered Options

* SQLite via EF Core
* PostgreSQL via EF Core
* JSON file (`appsettings.json` / custom config file)
* LiteDB (embedded document database)

## Decision Outcome

Chosen option: "SQLite via EF Core", because it requires no external server, integrates naturally with EF Core migrations, works as a Docker volume mount, and is sufficient for the portal's data volume (a small number of managed repositories and their ADR library entries).

### Consequences

* Good, because SQLite requires no server process — it is a single file that can be volume-mounted in Docker.
* Good, because EF Core migrations provide a controlled schema evolution path.
* Good, because strongly-typed LINQ queries over repositories, library entries, and sync status.
* Good, because the connection string is configurable via `ConnectionStrings:AdrPortal` in `appsettings.json` or environment variables.
* Bad, because SQLite does not support concurrent writes well at high load; acceptable given the portal's single-user / small-team usage pattern.
* Bad, because SQLite is not suitable if the portal is ever scaled horizontally to multiple instances.

### Entities stored in SQLite

| Entity | Purpose |
|---|---|
| `ManagedRepository` | Registered repos with paths, inbox folder, git remote, active flag |
| `GlobalAdr` | One entry per unique ADR topic in the global library |
| `GlobalAdrVersion` | Immutable versioned snapshots of a library template |
| `GlobalAdrInstance` | Per-repo tracking of which library version an ADR is based on and its sync status |

## Pros and Cons of the Options

### SQLite via EF Core

* Good, because zero infrastructure — a single file.
* Good, because Docker volume-mountable.
* Good, because EF Core migrations for schema evolution.
* Bad, because limited concurrent write support.

### PostgreSQL via EF Core

* Good, because production-grade with full concurrency support.
* Bad, because requires a running PostgreSQL server — adds infrastructure overhead for local dev.
* Bad, because over-engineered for a single-user portal managing a handful of repositories.

### JSON file

* Good, because trivially simple; no ORM needed.
* Bad, because no query capability — all filtering must be done in memory.
* Bad, because concurrent writes can corrupt the file.

### LiteDB

* Good, because embedded document database with no schema.
* Bad, because no EF Core integration; requires a separate query API.
* Bad, because less community support and tooling than SQLite.

## More Information

* [ADR-0005: File-Based ADR Storage](adr-0005-file-based-adr-storage.md)
* [ADR-0008: .NET Aspire for Local Orchestration](adr-0008-aspire-local-orchestration.md)
* [ADR-0012: Global ADR Versioned Library](adr-0012-global-adr-versioned-library.md)

## Persistence Path and Reset Safety (Phase 16)

To prevent accidental data loss and avoid runtime-path-dependent database placement, startup now applies explicit safety rules:

* Relative SQLite `Data Source` values are resolved into a durable root folder (default `%LOCALAPPDATA%\AdrPortal\data` on Windows when available, with user-profile fallback).
* Database deletion on startup is opt-in only via `Persistence:ResetDatabaseOnStartup=true`; the default is `false`.

Additional behavior:

* If a legacy database file exists at a previous relative location, it is copied into the durable root on first run (including optional SQLite `-wal` and `-shm` companion files) when the target does not already exist.
* Relative traversal outside the configured root is rejected at startup to prevent path escape from the persistence boundary.
