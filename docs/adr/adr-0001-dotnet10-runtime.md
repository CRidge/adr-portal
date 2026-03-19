---
status: "accepted"
date: 2026-03-19
decision-makers: [ADR Portal Project Team]
consulted: []
informed: []
---
# .NET 10 as the Target Runtime

## Context and Problem Statement

The ADR Portal is a new greenfield application. A target .NET runtime version must be chosen. This decision governs the SDK, language version (C#), BCL features, and long-term support timeline available to the project. The choice is independent of the web UI framework (see ADR-0013).

## Decision Drivers

* Access to the latest C# language features and runtime performance improvements
* Long-term support aligned with Microsoft's release cadence
* Compatibility with all planned dependencies (Aspire, EF Core, Microsoft.Extensions.AI)
* Team is starting fresh with no legacy version constraints

## Considered Options

* .NET 10
* .NET 9
* .NET 8 (LTS)

## Decision Outcome

Chosen option: ".NET 10", because the project starts in 2026 during the .NET 10 release window, giving access to the latest C# 14 features, runtime improvements, and the most current Aspire and Blazor capabilities — with no need to compromise for backwards compatibility.

### Consequences

* Good, because access to C# 14 and all .NET 10 runtime improvements.
* Good, because .NET 10 is the current release; all target dependencies (Aspire 13, EF Core 10, Microsoft.Extensions.AI) target it natively.
* Good, because the SDK version is pinned in `global.json` ensuring reproducible builds across machines and CI.
* Bad, because .NET 10 is cutting-edge; some third-party packages may lag slightly behind the runtime release at project start.

## Pros and Cons of the Options

### .NET 10

* Good, because latest C# language version and runtime performance.
* Good, because all planned first-party dependencies (Aspire, EF Core, MEA) target .NET 10.
* Bad, because some third-party packages may lag at initial release.

### .NET 9

* Good, because one version behind; slightly better third-party package availability at time of writing.
* Bad, because not an LTS release; short support window.
* Bad, because misses .NET 10 language and runtime improvements.

### .NET 8 (LTS)

* Good, because long-term support until November 2026.
* Bad, because already behind the current release; unnecessary version constraint for a greenfield project.
* Bad, because misses two generations of language and runtime improvements.

## More Information

* [ADR-0013: Blazor Interactive Server as Web UI Framework](adr-0013-blazor-interactive-server.md)
* [ADR-0002: TUnit as Testing Framework](adr-0002-tunit-testing-framework.md)
* [ADR-0003: NuGet Central Package Management](adr-0003-nuget-central-package-management.md)
* [.NET 10 release notes](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)