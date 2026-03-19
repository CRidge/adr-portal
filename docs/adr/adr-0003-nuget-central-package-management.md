---
title: "ADR-0003: NuGet Central Package Management"
status: "Accepted"
date: "2026-03-19"
authors: "ADR Portal Project Team"
tags: ["architecture", "decision", "nuget", "package-management"]
supersedes: ""
superseded_by: ""
---

# ADR-0003: NuGet Central Package Management

## Status

**Accepted**

## Context

The ADR Portal solution will consist of multiple projects (web host, core domain, infrastructure, test projects). Without a centralized approach to NuGet package versioning, each project independently declares package versions in its `.csproj` file. This leads to:

- Version drift between projects (e.g., `Microsoft.Extensions.Logging` at 9.x in one project, 10.x in another)
- Tedious multi-file updates when upgrading shared dependencies
- Inconsistent transitive dependency resolution
- Increased security risk when patches are not uniformly applied

The project specification explicitly references [Central Package Management (CPM)](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management) as the required package management approach.

## Decision

All NuGet package **versions** will be declared exclusively in a root-level `Directory.Packages.props` file using Central Package Management. Individual project files (`.csproj`) reference packages by name only, without a version attribute. The `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` MSBuild property will be set in `Directory.Build.props`.

## Consequences

### Positive

- **POS-001**: Single source of truth for all package versions — upgrading a package requires editing exactly one file.
- **POS-002**: Eliminates version conflicts between projects in the same solution; all projects consume the same version of shared packages.
- **POS-003**: Simplifies security audits — `dotnet list package --vulnerable` reports against a unified version set.
- **POS-004**: Consistent with Microsoft's recommended practices for multi-project .NET solutions as of .NET 7+.
- **POS-005**: Compatible with Dependabot, which can raise a single PR to update a package version in `Directory.Packages.props` rather than multiple per-project PRs.

### Negative

- **NEG-001**: Projects cannot independently use different versions of the same package without an explicit `VersionOverride` attribute, which should be used sparingly and documented.
- **NEG-002**: Developers unfamiliar with CPM may be confused when adding a package reference without a version and seeing it resolved from the central props file.
- **NEG-003**: IDE tooling (e.g., Visual Studio NuGet Package Manager GUI) may display version information in a less intuitive way for CPM-managed solutions.

## Alternatives Considered

### Per-project version declarations (no CPM)

- **ALT-001**: **Description**: Each `.csproj` declares `<PackageReference Include="X" Version="Y" />` independently — the traditional approach.
- **ALT-002**: **Rejection Reason**: Does not scale to multi-project solutions. Version drift and inconsistency are inevitable. Explicitly rejected by the project specification.

### Paket

- **ALT-003**: **Description**: Alternative .NET package manager with its own lock file and dependency resolution model.
- **ALT-004**: **Rejection Reason**: Introduces a non-standard toolchain. CPM achieves the same goals within the official NuGet/MSBuild ecosystem, requiring no additional tooling.

### Directory.Build.props version variables

- **ALT-005**: **Description**: Define version strings as MSBuild properties in `Directory.Build.props` and reference them via `$(MyPackageVersion)` in project files.
- **ALT-006**: **Rejection Reason**: A manual workaround that predates CPM. CPM is the officially supported mechanism and provides better IDE and tooling integration.

## Implementation Notes

- **IMP-001**: Create `Directory.Packages.props` at the repository root with all `<PackageVersion>` entries. Example:
  ```xml
  <Project>
    <PropertyGroup>
      <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    </PropertyGroup>
    <ItemGroup>
      <PackageVersion Include="Microsoft.AspNetCore.Components.Web" Version="10.0.0" />
      <PackageVersion Include="TUnit" Version="x.y.z" />
    </ItemGroup>
  </Project>
  ```
- **IMP-002**: In `.csproj` files, reference packages without a `Version` attribute:
  ```xml
  <PackageReference Include="TUnit" />
  ```
- **IMP-003**: Use `VersionOverride` only when a specific project genuinely requires a different version, and document the reason in a comment within `Directory.Packages.props`.
- **IMP-004**: Configure Dependabot to target `Directory.Packages.props` for automated dependency updates.

## References

- **REF-001**: [ADR-0001: Blazor on .NET 10 as Web Application Framework](./adr-0001-blazor-dotnet10-framework.md)
- **REF-002**: [Central Package Management — Microsoft Learn](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
- **REF-003**: [concept.md — Tech choices section](../../concept.md)
