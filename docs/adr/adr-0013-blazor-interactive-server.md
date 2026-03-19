---
status: "accepted"
date: 2026-03-19
decision-makers: [ADR Portal Project Team]
consulted: []
informed: []
---
# Blazor Interactive Server as the Web UI Framework

## Context and Problem Statement

The ADR Portal requires a rich interactive web UI for ADR lifecycle management, AI-assisted evaluation, real-time folder monitoring feedback, and multi-repo comparison. The runtime is .NET 10 (see ADR-0001). A decision is needed on which web UI framework to use and which Blazor render mode to adopt.

## Decision Drivers

* Need for real-time interactivity: live folder monitoring events, AI response streaming, ADR list updates
* Desire to share domain models and business logic between UI and server without duplication
* Team has existing .NET/C# expertise; introducing a separate JavaScript framework adds context-switching overhead
* Server-side access to `System.IO`, LibGit2Sharp, and AI SDKs is required
* No offline/disconnected use cases — the portal is always run locally alongside the repositories it manages

## Considered Options

* Blazor Web App — Interactive Server render mode
* Blazor Web App — Interactive WebAssembly render mode
* Blazor Web App — Auto render mode (Server then WASM)
* React (TypeScript) + ASP.NET Core API
* Angular + ASP.NET Core API
* Razor Pages (server-rendered MPA)

## Decision Outcome

Chosen option: "Blazor Web App — Interactive Server render mode", because it keeps all component logic server-side (giving direct access to file system, git, and AI services), supports real-time SignalR-backed updates needed for folder monitoring and AI streaming, and eliminates the need to expose internal services as HTTP APIs.

### Consequences

* Good, because a single C# codebase covers UI and server — no TypeScript, no API layer, no CORS.
* Good, because domain models, validation, and business logic are shared directly with no duplication.
* Good, because `FileSystemWatcher`, LibGit2Sharp, and AI SDKs are directly accessible within Blazor components via DI.
* Good, because SignalR keeps the UI in sync with server-side events (inbox file drops, AI streaming).
* Bad, because each user session requires a persistent SignalR connection to the server; not suitable for high-concurrency public deployments (acceptable for a locally-hosted developer tool).
* Bad, because Blazor has a smaller community and fewer third-party UI component libraries than React or Angular.
* Bad, because UI responsiveness depends on server latency — a slow server means a slow UI.

## Pros and Cons of the Options

### Blazor Interactive Server

* Good, because full .NET ecosystem access server-side within components.
* Good, because real-time updates via SignalR with no extra infrastructure.
* Good, because no separate API layer required.
* Bad, because persistent SignalR connection per session limits horizontal scalability.

### Blazor Interactive WebAssembly

* Good, because UI runs entirely in the browser; no server connection required after initial load.
* Bad, because all server-side services (file system, git, AI) would need to be exposed as HTTP APIs, adding significant complexity.
* Bad, because WASM payload size and startup time add friction for a developer tool.

### Blazor Auto (Server then WASM)

* Good, because fast initial load (server) with eventual offline capability (WASM).
* Bad, because the portal has no offline use case; added complexity provides no benefit.
* Bad, because same API-exposure problem as WASM for server-side services.

### React + ASP.NET Core API

* Good, because React has the largest frontend ecosystem.
* Bad, because requires maintaining two codebases in different languages.
* Bad, because domain models must be duplicated or code-generated as TypeScript types.
* Bad, because CORS, API versioning, and authentication must be managed across the split.

### Angular + ASP.NET Core API

* Good, because opinionated structure suits large teams.
* Bad, because same split-stack concerns as React with additional complexity.

### Razor Pages (MPA)

* Good, because simple server-rendered model with no client state.
* Bad, because no real-time capability; folder monitoring feedback and AI streaming are not achievable without significant JavaScript workarounds.

## More Information

* [ADR-0001: .NET 10 as Target Runtime](adr-0001-dotnet10-runtime.md)
* [ADR-0008: .NET Aspire for Local Orchestration](adr-0008-aspire-local-orchestration.md)
* [Blazor documentation — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
