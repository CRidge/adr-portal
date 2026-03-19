---
title: "ADR-0002: TUnit as the Automated Testing Framework"
status: "Accepted"
date: "2026-03-19"
authors: "ADR Portal Project Team"
tags: ["architecture", "decision", "testing", "tunit"]
supersedes: ""
superseded_by: ""
---

# ADR-0002: TUnit as the Automated Testing Framework

## Status

**Accepted**

## Context

The ADR Portal requires an automated testing strategy covering unit tests, integration tests, and potentially component tests for Blazor UI logic. The project specification explicitly mandates TUnit as the testing framework, ruling out xUnit, NUnit, and MSTest.

TUnit is a modern, source-generator-based test framework for .NET that offers significant advantages in performance and expressiveness over traditional frameworks. It is designed from the ground up for .NET 8+ and supports parallelism, data-driven tests, and async-first patterns natively.

The choice of testing framework affects:
- Developer ergonomics for writing and reading tests
- CI execution speed (parallelism support)
- Integration with `dotnet test` and GitHub Actions
- Compatibility with assertion and mocking libraries

## Decision

All automated tests across the solution will use **TUnit** as the test framework. Test projects will follow the naming convention `[ProjectName].Tests` and be structured alongside the projects they test.

## Consequences

### Positive

- **POS-001**: TUnit's source-generator-based architecture eliminates reflection overhead at runtime, resulting in faster test discovery and execution.
- **POS-002**: Native async/await support throughout — test methods, setup, and teardown are all async-first with no workarounds required.
- **POS-003**: Rich built-in data-driven test support via `[Arguments]`, `[MethodDataSource]`, and `[ClassDataSource]` attributes, equivalent to xUnit's `[Theory]`.
- **POS-004**: Fine-grained parallelism control at the assembly, class, and method level, enabling faster CI pipelines.
- **POS-005**: Compatible with `dotnet test`, existing CI/CD tooling, and standard .NET assertion libraries (e.g., FluentAssertions, Shouldly).

### Negative

- **NEG-001**: TUnit is newer and has a smaller ecosystem and community compared to xUnit or NUnit; fewer Stack Overflow answers and third-party tutorials exist.
- **NEG-002**: Some IDE integrations (e.g., ReSharper, Rider test runner) may have less mature support compared to xUnit.
- **NEG-003**: Team members familiar with xUnit or NUnit will need to learn TUnit's attribute model and assertion patterns.
- **NEG-004**: Mocking libraries (Moq, NSubstitute) are compatible but integration patterns may differ slightly from xUnit-specific documentation.

## Alternatives Considered

### xUnit

- **ALT-001**: **Description**: The de facto standard .NET test framework, widely used in the .NET open-source ecosystem including ASP.NET Core itself.
- **ALT-002**: **Rejection Reason**: Explicitly excluded by the project specification (`concept.md`). Additionally, xUnit lacks native async setup/teardown and has less performant test discovery compared to TUnit.

### NUnit

- **ALT-003**: **Description**: Mature test framework with rich parameterization support via `[TestCase]` and `[TestCaseSource]`.
- **ALT-004**: **Rejection Reason**: Explicitly excluded by the project specification. NUnit's threading model and attribute verbosity are considered inferior to TUnit's modern design.

### MSTest

- **ALT-005**: **Description**: Microsoft's official test framework, deeply integrated with Visual Studio.
- **ALT-006**: **Rejection Reason**: Excluded by the project specification. MSTest v3 improves on the classic model, but TUnit's performance and async-first design are preferred.

## Implementation Notes

- **IMP-001**: Add TUnit NuGet packages via Central Package Management (see ADR-0003): `TUnit`, `TUnit.Assertions`, and `TUnit.Engine`.
- **IMP-002**: Structure test projects with a mirrored folder layout matching the source project (e.g., `src/ADRPortal.Core/Services/AdrService.cs` → `tests/ADRPortal.Core.Tests/Services/AdrServiceTests.cs`).
- **IMP-003**: Use `TUnit.Assertions` for fluent assertions; optionally supplement with FluentAssertions for complex assertion scenarios.
- **IMP-004**: Configure parallelism in test projects via `[assembly: Parallelism(ParallelismOptions.All)]` to maximize CI throughput.
- **IMP-005**: Use NSubstitute or Moq for dependency mocking — both are compatible with TUnit.

## References

- **REF-001**: [ADR-0001: Blazor on .NET 10 as Web Application Framework](./adr-0001-blazor-dotnet10-framework.md)
- **REF-002**: [ADR-0003: NuGet Central Package Management](./adr-0003-nuget-central-package-management.md)
- **REF-003**: [TUnit documentation](https://tunit.dev)
- **REF-004**: [concept.md — Tech choices section](../../concept.md)
