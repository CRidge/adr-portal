---
title: "ADR-0001: Blazor on .NET 10 as Web Application Framework"
status: "Accepted"
date: "2026-03-19"
authors: "ADR Portal Project Team"
tags: ["architecture", "decision", "frontend", "dotnet", "blazor"]
supersedes: ""
superseded_by: ""
---

# ADR-0001: Blazor on .NET 10 as Web Application Framework

## Status

**Accepted**

## Context

The ADR Portal requires a modern web application framework for building a UI that manages Architectural Decision Records. The portal must support complex interactions including ADR lifecycle management (propose, accept, reject, supersede, retire), AI-assisted evaluation, multi-repo comparison, and real-time folder monitoring.

The team has existing expertise in the .NET ecosystem. A unified technology stack reduces context switching, allows code sharing between frontend and backend, and simplifies build and deployment pipelines. .NET 10 is the latest LTS-aligned release offering the most current language features, performance improvements, and Blazor capabilities.

Key requirements driving this decision:
- Rich interactive UI for ADR authoring and review workflows
- Integration with .NET libraries for file system monitoring and AI (Copilot SDK)
- Ability to share domain models (ADR entities, states) across client and server
- Long-term supportability and alignment with Microsoft's recommended practices

## Decision

The portal will be built using **Blazor** (Blazor Server or Blazor WebAssembly with .NET 10), targeting the latest stable .NET 10 SDK. All server-side logic, AI integration, and file system access will be implemented in C# 14 within the same solution.

## Consequences

### Positive

- **POS-001**: Single language (C#) across the entire stack eliminates context switching between frontend and backend languages.
- **POS-002**: Shared domain models and validation logic between UI and server prevent duplication and drift.
- **POS-003**: .NET 10 provides the latest Blazor rendering improvements, including enhanced static SSR, streaming rendering, and auto render mode.
- **POS-004**: Full access to the .NET ecosystem, including `System.IO.FileSystemWatcher` for folder monitoring and the Copilot SDK for AI features.
- **POS-005**: Simplified CI/CD with a single `dotnet build` / `dotnet publish` pipeline.

### Negative

- **NEG-001**: Blazor has a smaller community and fewer third-party UI component libraries compared to React or Angular ecosystems.
- **NEG-002**: .NET 10 is cutting-edge; some tooling and packages may lag slightly behind the runtime release.
- **NEG-003**: Blazor WebAssembly has initial load size considerations; Blazor Server requires persistent SignalR connections which adds infrastructure considerations.
- **NEG-004**: Developers unfamiliar with Blazor may face a learning curve, particularly around component lifecycle and render modes.

## Alternatives Considered

### React (TypeScript) + ASP.NET Core API

- **ALT-001**: **Description**: Separate React SPA for the frontend communicating with an ASP.NET Core Web API backend.
- **ALT-002**: **Rejection Reason**: Requires maintaining two codebases in different languages, duplicating domain models, and managing CORS and API versioning. Adds significant complexity without benefit given the team's .NET expertise.

### Angular + ASP.NET Core API

- **ALT-003**: **Description**: Angular frontend with an ASP.NET Core backend API.
- **ALT-004**: **Rejection Reason**: Same split-stack concerns as React. Angular's opinionated structure adds overhead for a team not already invested in it.

### Razor Pages (MPA only)

- **ALT-005**: **Description**: Traditional server-rendered multi-page application using Razor Pages.
- **ALT-006**: **Rejection Reason**: Insufficient interactivity for real-time features like live ADR status updates, folder monitoring feedback, and AI streaming responses. Blazor supersedes this for interactive scenarios.

## Implementation Notes

- **IMP-001**: Use the **Blazor Web App** project template (introduced in .NET 8+) which supports all render modes (Static SSR, Interactive Server, Interactive WebAssembly, Interactive Auto) per component.
- **IMP-002**: Adopt **Interactive Server** render mode by default for components requiring real-time updates (folder monitoring, AI streaming); evaluate WebAssembly for offline-capable components.
- **IMP-003**: Organize the solution with a `src/` layout: `ADRPortal.Web` (Blazor host), `ADRPortal.Core` (domain/business logic), `ADRPortal.Infrastructure` (file system, AI integration).
- **IMP-004**: Pin the .NET SDK version in `global.json` to ensure reproducible builds across developer machines and CI.

## References

- **REF-001**: [ADR-0002: TUnit as Testing Framework](./adr-0002-tunit-testing-framework.md)
- **REF-002**: [ADR-0003: NuGet Central Package Management](./adr-0003-nuget-central-package-management.md)
- **REF-003**: [Blazor documentation — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
- **REF-004**: [concept.md — Tech choices section](../../concept.md)
