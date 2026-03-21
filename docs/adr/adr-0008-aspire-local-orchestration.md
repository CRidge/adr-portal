---
status: "accepted"
date: 2026-03-19
decision-makers: [ADR Portal Project Team]
consulted: []
informed: []
---
# .NET Aspire for Local Development Orchestration

## Context and Problem Statement

The ADR Portal is a multi-project solution (Blazor Web, Core, Infrastructure) with external concerns including a SQLite database (file path), environment-specific configuration, and OpenTelemetry observability. Local development requires starting multiple projects together, wiring environment variables, and having visibility into logs and traces without a full production observability stack. A decision is needed on how to orchestrate local development and whether to adopt .NET Aspire.

## Decision Drivers

* Multi-project solution benefits from coordinated startup
* Developers need structured logs and traces during local development without deploying a full observability stack
* Configuration (e.g., SQLite connection string) should be injected consistently across environments
* Future Docker packaging should align with the local orchestration model
* Aspire 13.1.3 is available and provides a dashboard, OTEL support, and service defaults out of the box

## Considered Options

* .NET Aspire 13.1.3 (AppHost + ServiceDefaults)
* Manual `launchSettings.json` + individual project startup
* Docker Compose for local dev
* Tye (deprecated Microsoft project)

## Decision Outcome

Chosen option: ".NET Aspire 13.1.3", because it provides a coordinated AppHost with a built-in developer dashboard, structured logging, distributed tracing via OpenTelemetry, and health check wiring — all without requiring a separately managed infrastructure stack during local development.

### Consequences

* Good, because the Aspire dashboard provides logs, traces, and resource health in a single UI during development.
* Good, because `ServiceDefaults` wires OpenTelemetry, health checks, and service discovery with a single method call.
* Good, because configuration (e.g., `ConnectionStrings:AdrPortal`) can be injected centrally from the AppHost without duplicating `appsettings.json` entries.
* Good, because the AppHost model maps naturally to Docker Compose for production packaging.
* Bad, because Aspire adds a project to the solution and a startup dependency; developers must run the AppHost rather than individual projects directly.
* Bad, because Aspire 13.x is a recent release; some tooling and documentation may lag.

## Pros and Cons of the Options

### .NET Aspire 13.1.3

* Good, because built-in developer dashboard with logs, traces, and metrics.
* Good, because `ServiceDefaults` provides OTEL, health checks, and service discovery in one package.
* Good, because natural fit for future Docker packaging.
* Bad, because adds AppHost project and requires developers to change how they start the solution.

### Manual launchSettings.json

* Good, because zero additional tooling.
* Bad, because no structured observability; developers must configure logging manually per project.
* Bad, because environment variable wiring must be duplicated across project launch profiles.

### Docker Compose for local dev

* Good, because matches production packaging exactly.
* Bad, because requires Docker to be running and adds significant startup overhead for inner-loop development.
* Bad, because hot reload and debugging are more complex inside containers.

### Tye

* Good, because similar orchestration model to Aspire.
* Bad, because deprecated by Microsoft; Aspire is its successor.

## More Information

* [ADR-0007: SQLite with EF Core](adr-0007-sqlite-ef-core-persistence.md)
* [.NET Aspire documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)

## Container Run Instructions (Phase 14)

For production-like local execution without AppHost, use Docker Compose from the repository root:

1. Build and start:
   * `docker compose up --build -d`
2. Open the portal:
   * `http://localhost:8080`
3. Stop:
   * `docker compose down`

Persistence and runtime notes:

* SQLite data is persisted in the named volume `adrportal-data` (`/app/data/adrportal.db` in the container).
* Repository working folders are mounted from `./repos` on the host to `/repos` in the container.
* Optional integration tokens can be injected via environment variables before `docker compose up`:
  * `GITHUB_TOKEN`
  * `COPILOT_TOKEN`

## Durable Persistence Injection (Phase 16)

The AppHost injects `Persistence__DatabaseRootPath` for the web project so SQLite files are anchored to a stable OS-backed path rather than the working directory. This improves restart resilience for local Aspire runs and avoids accidental database relocation between launches.
