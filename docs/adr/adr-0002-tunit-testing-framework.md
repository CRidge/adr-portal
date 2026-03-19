---
status: "accepted"
date: 2026-03-19
decision-makers: [ADR Portal Project Team]
consulted: []
informed: []
---
# TUnit as the Automated Testing Framework

## Context and Problem Statement

The ADR Portal requires automated testing covering unit tests, integration tests, and Blazor component tests. The project specification explicitly mandates TUnit, ruling out xUnit, NUnit, and MSTest. A testing framework choice affects developer ergonomics, CI execution speed, async support, and IDE integration.

## Decision Drivers

* Project specification explicitly requires TUnit
* Need for async-first test patterns throughout (file I/O, AI calls, Git operations)
* CI speed via parallelism support
* Compatibility with `dotnet test` and standard mocking/assertion libraries

## Considered Options

* TUnit
* xUnit
* NUnit
* MSTest

## Decision Outcome

Chosen option: "TUnit", because it is mandated by the project specification and its source-generator-based architecture, async-first design, and fine-grained parallelism make it the best fit for a solution with heavy async I/O and AI integration.

### Consequences

* Good, because source-generator-based test discovery eliminates reflection overhead, resulting in faster test execution.
* Good, because native async/await support throughout — test methods, setup, and teardown require no workarounds.
* Good, because fine-grained parallelism control at assembly, class, and method level enables faster CI pipelines.
* Bad, because TUnit is newer with a smaller community; fewer Stack Overflow answers and tutorials exist.
* Bad, because some IDE integrations (ReSharper, Rider) may have less mature support than for xUnit.

## Pros and Cons of the Options

### TUnit

* Good, because source-generator-based — no reflection at runtime, fast discovery.
* Good, because async-first: `async` test methods, setup, and teardown work natively.
* Good, because rich data-driven test support via `[Arguments]`, `[MethodDataSource]`, `[ClassDataSource]`.
* Bad, because smaller ecosystem and fewer community resources compared to xUnit.

### xUnit

* Good, because de facto standard with the largest .NET test community.
* Good, because excellent IDE support across Visual Studio, Rider, and VS Code.
* Bad, because explicitly excluded by the project specification.
* Bad, because lacks native async setup/teardown; requires workarounds.

### NUnit

* Good, because mature with rich parameterization via `[TestCase]` and `[TestCaseSource]`.
* Bad, because explicitly excluded by the project specification.
* Bad, because threading model and attribute verbosity are considered inferior to TUnit's modern design.

### MSTest

* Good, because deeply integrated with Visual Studio.
* Bad, because explicitly excluded by the project specification.
* Bad, because historically verbose and less ergonomic than modern alternatives.

## More Information

* [ADR-0001: .NET 10 Runtime](adr-0001-dotnet10-runtime.md)
* [ADR-0013: Blazor Interactive Server](adr-0013-blazor-interactive-server.md)
* [ADR-0003: NuGet Central Package Management](adr-0003-nuget-central-package-management.md)
* [TUnit documentation](https://tunit.dev)