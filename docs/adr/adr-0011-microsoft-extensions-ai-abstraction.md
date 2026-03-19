---
status: "accepted"
date: 2026-03-19
decision-makers: [ADR Portal Project Team]
consulted: []
informed: []
---
# Microsoft.Extensions.AI as the AI Provider Abstraction Layer

## Context and Problem Statement

The ADR Portal integrates AI for multiple features: suggesting alternatives, evaluating options, finding affected ADRs, cross-repo relevance scoring, and bootstrapping ADRs from a codebase. These features must not be tightly coupled to any single AI provider so that the default (GitHub Copilot SDK) can be swapped for Azure OpenAI, Ollama, or other providers by changing a DI registration without modifying application code.

## Decision Drivers

* Provider independence — the portal must not be locked to a single LLM vendor
* A standardised .NET abstraction for AI is preferable to building a custom one
* The abstraction must support both request/response and streaming patterns
* The default implementation must be the GitHub Copilot SDK (see ADR-0004)
* Future implementations (Azure OpenAI, Ollama) must be drop-in replacements

## Considered Options

* `Microsoft.Extensions.AI` (`IChatClient` abstraction)
* Custom `IAiProvider` interface in `AdrPortal.Core`
* Semantic Kernel as the abstraction layer
* Direct dependency on GitHub Copilot SDK throughout

## Decision Outcome

Chosen option: "`Microsoft.Extensions.AI` (`IChatClient`)", because it is a Microsoft-maintained standard abstraction for .NET AI integration that supports streaming, tool calling, and multiple provider implementations — making it the natural extension of the `Microsoft.Extensions.*` ecosystem the portal already depends on.

### Consequences

* Good, because `IChatClient` is a stable Microsoft-maintained abstraction, not a bespoke interface to maintain.
* Good, because provider implementations (Copilot SDK, Azure OpenAI, Ollama) are registered via DI — no application code changes required to swap providers.
* Good, because streaming and tool-calling are first-class in the abstraction.
* Good, because `AdrPortal.Core` depends only on `Microsoft.Extensions.AI`, not on any specific provider package.
* Bad, because `Microsoft.Extensions.AI` is a relatively new package; the API surface may evolve.

### DI registration pattern

```csharp
// Default provider registration (GitHub Copilot)
builder.Services.AddChatClient(
    _ => /* provider-specific Copilot adapter registration */);

// Alternative provider registration (Azure OpenAI)
builder.Services.AddChatClient(
    _ => /* provider-specific Azure OpenAI adapter registration */);
```

## Pros and Cons of the Options

### Microsoft.Extensions.AI

* Good, because Microsoft-maintained; aligns with the existing `Microsoft.Extensions.*` stack.
* Good, because provider-agnostic `IChatClient` interface.
* Good, because streaming and tool-calling supported.
* Bad, because relatively new; API may evolve.

### Custom `IAiProvider` interface

* Good, because tailored exactly to portal requirements.
* Bad, because requires designing, implementing, and maintaining a bespoke abstraction.
* Bad, because no community implementations — every new provider requires custom adapter code.

### Semantic Kernel

* Good, because rich orchestration, memory, and planning features.
* Bad, because significantly more complex than the portal requires; heavy abstraction for focused AI calls.
* Bad, because `IChatClient` from `Microsoft.Extensions.AI` is simpler and sufficient.

### Direct GitHub Copilot SDK dependency

* Good, because simplest integration with no abstraction layer.
* Bad, because tightly couples all AI code to a single provider.
* Bad, because swapping providers requires touching all AI call sites.

## More Information

* [ADR-0004: AI Provider Decision](adr-0004-copilot-sdk-ai-integration.md)
* [Microsoft.Extensions.AI documentation](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
