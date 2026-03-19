---
status: "accepted"
date: 2026-03-19
decision-makers: [ADR Portal Project Team]
consulted: []
informed: []
---
# NuGet Central Package Management

## Context and Problem Statement

The ADR Portal solution contains multiple projects (web host, core domain, infrastructure, test projects). Without centralised version management, each project independently declares NuGet versions in its `.csproj`, leading to version drift, tedious multi-file upgrade PRs, and inconsistent transitive dependency resolution. The project specification explicitly references Central Package Management (CPM) as the required approach.

## Decision Drivers

* Multi-project solution requiring consistent package versions across all projects
* Simplifying security patch rollout (one file to update)
* Project specification explicitly requires CPM
* Compatibility with Dependabot for automated dependency updates

## Considered Options

* NuGet Central Package Management (`Directory.Packages.props`)
* Per-project version declarations (no CPM)
* Paket
* MSBuild property variables in `Directory.Build.props`

## Decision Outcome

Chosen option: "NuGet Central Package Management (`Directory.Packages.props`)", because it is the officially supported mechanism, is mandated by the project specification, and provides a single source of truth for all package versions with full IDE and tooling support.

### Consequences

* Good, because a single file controls all package versions — upgrades require editing exactly one place.
* Good, because version conflicts between projects in the same solution are eliminated.
* Good, because Dependabot raises a single PR per package update rather than one per project.
* Bad, because projects cannot independently use different package versions without an explicit `VersionOverride`, which must be used sparingly.
* Bad, because developers unfamiliar with CPM may be confused when `.csproj` references have no `Version` attribute.

## Pros and Cons of the Options

### NuGet Central Package Management

* Good, because official NuGet/MSBuild mechanism with full IDE support.
* Good, because single source of truth for all versions.
* Good, because compatible with Dependabot and `dotnet list package --vulnerable`.
* Bad, because requires all projects to align on a single version per package.

### Per-project version declarations

* Good, because each project can independently choose its version.
* Bad, because version drift across projects is inevitable and introduces transitive conflicts.
* Bad, because security patches require updating multiple `.csproj` files.

### Paket

* Good, because strict dependency locking and reproducible resolution.
* Bad, because introduces a non-standard toolchain not part of the official NuGet/MSBuild ecosystem.

### MSBuild property variables

* Good, because works with any MSBuild version without requiring CPM support.
* Bad, because a manual workaround that predates CPM; no IDE auto-complete or tooling support.

## More Information

* [Central Package Management — Microsoft Learn](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
* [ADR-0001: .NET 10 Runtime](adr-0001-dotnet10-runtime.md)
* [ADR-0013: Blazor Interactive Server](adr-0013-blazor-interactive-server.md)