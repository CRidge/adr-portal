---
status: "accepted"
date: 2026-03-19
decision-makers: [ADR Portal Project Team]
consulted: []
informed: []
---
# Blazor on .NET 10 as Web Application Framework

## Context and Problem Statement

The ADR Portal requires a modern web framework for a rich interactive UI managing ADR lifecycles, AI-assisted evaluation, multi-repo comparison, and real-time folder monitoring. The team has existing .NET expertise and wants a unified technology stack to share domain models across UI and server without maintaining two codebases in different languages.

## Decision Drivers

* Team expertise in the .NET ecosystem
* Need for real-time interactivity (folder monitoring, AI streaming)
* Desire to share domain models between UI and server without duplication
* Long-term supportability aligned with Microsoft's recommended practices
* `System.IO` and AI SDK access required server-side

## Considered Options

* Blazor Web App on .NET 10 (Interactive Server)
* React (TypeScript) + ASP.NET Core API
* Angular + ASP.NET Core API
* Razor Pages (server-rendered MPA)

## Decision Outcome

Chosen option: "Blazor Web App on .NET 10 (Interactive Server)", because it provides a single C# stack, shares domain models with zero duplication, supports real-time SignalR-backed interactivity, and gives full access to .NET libraries needed for file system monitoring and AI integration.

### Consequences

* Good, because a single language (C#) across the entire stack eliminates context switching.
* Good, because shared domain models and validation logic prevent drift between UI and server.
* Good, because .NET 10 provides the latest Blazor rendering improvements including streaming rendering and auto render mode.
* Good, because the full .NET ecosystem (`FileSystemWatcher`, AI SDKs, LibGit2Sharp) is directly accessible.
* Bad, because Blazor has a smaller community and fewer third-party UI component libraries than React or Angular.
* Bad, because Blazor Server requires persistent SignalR connections which adds infrastructure considerations at scale.

## Pros and Cons of the Options

### Blazor Web App on .NET 10 (Interactive Server)

* Good, because single codebase in C# covering UI and server.
* Good, because domain models, validation, and business logic can be shared directly.
* Good, because `FileSystemWatcher` and AI SDKs are natively accessible server-side.
* Bad, because the Blazor ecosystem is smaller than React/Angular.

### React (TypeScript) + ASP.NET Core API

* Good, because React has the largest frontend ecosystem and community.
* Bad, because requires maintaining two codebases in different languages.
* Bad, because domain models must be duplicated or generated as TypeScript types.
* Bad, because CORS, API versioning, and authentication must be managed across the split.

### Angular + ASP.NET Core API

* Good, because Angular has strong enterprise adoption and opinionated structure.
* Bad, because same split-stack concerns as React.
* Bad, because Angular's complexity adds overhead without benefit given .NET expertise.

### Razor Pages (server-rendered MPA)

* Good, because simpler programming model with no client-side state.
* Bad, because insufficient interactivity for real-time features such as live ADR status updates, folder monitoring feedback, and AI streaming.

## More Information

* [ADR-0002: TUnit as Testing Framework](adr-0002-tunit-testing-framework.md)
* [ADR-0003: NuGet Central Package Management](adr-0003-nuget-central-package-management.md)
* [Blazor documentation — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/blazor/)