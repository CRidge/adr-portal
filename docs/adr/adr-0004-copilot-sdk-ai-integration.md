---
status: "accepted"
date: 2026-03-19
decision-makers: [ADR Portal Project Team]
consulted: []
informed: []
---
# Microsoft.Extensions.AI Abstraction with GitHub Copilot SDK as Default Provider

## Context and Problem Statement

The ADR Portal requires AI capabilities for alternative discovery, option evaluation, affected-ADR analysis, cross-repo relevance scoring, and bootstrapping ADRs from a codebase. The AI integration must be provider-agnostic so that the underlying model can be swapped without application changes, while GitHub Copilot is the natural default for the developer audience using this portal.

## Decision Drivers

* Provider-agnostic design to avoid lock-in to a single LLM vendor
* GitHub Copilot is the natural default for developer tooling contexts
* All AI calls must be server-side to keep credentials off the browser
* Responses for some features must stream incrementally to the Blazor UI
* AI calls must be grounded in the existing ADR corpus to produce relevant recommendations

## Considered Options

* `Microsoft.Extensions.AI` abstraction + GitHub Copilot SDK as default implementation
* Azure OpenAI Service via Semantic Kernel
* Ollama (local LLM)
* No AI integration

## Decision Outcome

Chosen option: "`Microsoft.Extensions.AI` abstraction + GitHub Copilot SDK as default implementation", because the abstraction layer provides provider independence, the Copilot SDK is the natural fit for developer tooling, and streaming + tool-calling support meet the portal's UI requirements.

### Consequences

* Good, because `IChatClient` abstraction decouples the application from the AI provider — Azure OpenAI, Ollama, or any other provider can be substituted by swapping the DI registration.
* Good, because the Copilot SDK is familiar to the developer audience and integrates naturally with GitHub-hosted repositories.
* Good, because server-side execution ensures API credentials never reach the browser.
* Good, because streaming responses allow Blazor UI to display AI output incrementally.
* Bad, because GitHub Copilot requires a paid subscription, which may not be available to all users.
* Bad, because AI responses are non-deterministic; the same input may produce different outputs on successive calls.
* Bad, because large ADR corpora may exceed context window limits, requiring chunking strategies.

## Pros and Cons of the Options

### Microsoft.Extensions.AI + GitHub Copilot SDK

* Good, because provider-agnostic `IChatClient` interface makes swapping models trivial.
* Good, because Copilot SDK supports tool-calling and streaming natively.
* Good, because aligns with the developer tooling context of the portal.
* Bad, because requires a Copilot subscription.

### Azure OpenAI via Semantic Kernel

* Good, because Semantic Kernel provides rich orchestration primitives.
* Bad, because requires provisioning an Azure OpenAI resource with associated infrastructure costs.
* Bad, because Semantic Kernel adds significant abstraction overhead for the portal's focused use cases.

### Ollama (local LLM)

* Good, because fully offline and zero ongoing cost.
* Bad, because model quality for nuanced architectural reasoning is significantly below frontier models.
* Bad, because requires local GPU resources not available in all environments.

### No AI integration

* Good, because eliminates LLM dependency and cost entirely.
* Bad, because AI-assisted evaluation is a core use-case requirement and omitting it fails the product goals.

## More Information

* [ADR-0011: Microsoft.Extensions.AI as AI Provider Abstraction](adr-0011-microsoft-extensions-ai-abstraction.md)
* [ADR-0001: Blazor on .NET 10](adr-0001-blazor-dotnet10-framework.md)
* [Microsoft.Extensions.AI abstractions](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)