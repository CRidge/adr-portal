---
title: "ADR-0004: Copilot SDK for AI-Assisted ADR Evaluation"
status: "Proposed"
date: "2026-03-19"
authors: "ADR Portal Project Team"
tags: ["architecture", "decision", "ai", "copilot-sdk", "llm"]
supersedes: ""
superseded_by: ""
---

# ADR-0004: Copilot SDK for AI-Assisted ADR Evaluation

## Status

**Proposed**

## Context

A core differentiator of the ADR Portal is AI assistance in two areas:

1. **Alternative discovery**: When a user is authoring a proposed ADR, the system should suggest alternatives they may not have considered.
2. **Option evaluation and recommendation**: Given a proposed ADR's context and the corpus of existing accepted/superseded ADRs in the repository, the AI should evaluate options and recommend one with rationale.

The system must ground AI responses in the existing ADR corpus — recommendations must be consistent with prior decisions and flag potential conflicts or superseding relationships.

The project specification suggests the **Copilot SDK** as the AI integration mechanism. This SDK allows embedding GitHub Copilot AI capabilities directly into .NET applications, providing access to large language models (LLMs) with tool-calling, context management, and streaming response support.

Key considerations:
- The AI must have access to existing ADRs as context (RAG pattern or in-context injection)
- Responses must be streamed for perceived responsiveness in the Blazor UI
- The integration must be extensible if the underlying model or provider changes
- Authentication and token management must be handled securely

## Decision

The portal will integrate AI capabilities via the **GitHub Copilot SDK** (using `Microsoft.Extensions.AI` abstractions where available) to provide alternative discovery and option recommendation features. AI calls will be made server-side (within Blazor Server or API components) to keep credentials off the client.

## Consequences

### Positive

- **POS-001**: Direct integration with GitHub Copilot models, which are familiar to the developer audience using this portal.
- **POS-002**: The Copilot SDK supports tool-calling, enabling the AI to query existing ADRs programmatically as grounding context.
- **POS-003**: Streaming response support allows the Blazor UI to display AI output incrementally, improving perceived responsiveness.
- **POS-004**: `Microsoft.Extensions.AI` abstractions decouple the application from the specific AI provider, making it possible to swap to Azure OpenAI or other providers with minimal code changes.
- **POS-005**: Server-side execution ensures API tokens and credentials never reach the browser.

### Negative

- **NEG-001**: The Copilot SDK is relatively new and may have breaking API changes in early versions, requiring periodic updates.
- **NEG-002**: AI responses are non-deterministic; the same ADR context may produce different recommendations on successive calls, requiring UI design that sets appropriate user expectations.
- **NEG-003**: Token cost and latency are non-trivial for large ADR corpora; context window limits may require chunking or summarization strategies.
- **NEG-004**: GitHub Copilot access requires a paid subscription, which may not be available to all users of the portal.

## Alternatives Considered

### Azure OpenAI Service via Semantic Kernel

- **ALT-001**: **Description**: Use Microsoft Semantic Kernel with an Azure OpenAI backend (GPT-4o) for orchestration and AI features.
- **ALT-002**: **Rejection Reason**: Requires provisioning and managing an Azure OpenAI resource with associated costs. Copilot SDK is more naturally aligned with GitHub-hosted ADR workflows and the developer tooling context of this project. Copilot SDK can be switched in behind `Microsoft.Extensions.AI` if needed.

### Ollama (local LLM)

- **ALT-003**: **Description**: Run a local open-source LLM (e.g., Llama 3, Mistral) via Ollama for fully offline, zero-cost AI evaluation.
- **ALT-004**: **Rejection Reason**: Model quality for nuanced architectural reasoning is significantly lower than frontier models. Requires local GPU resources that may not be available in all deployment environments.

### No AI integration (manual evaluation only)

- **ALT-005**: **Description**: Omit AI features; users manually research alternatives and evaluation criteria.
- **ALT-006**: **Rejection Reason**: AI-assisted evaluation is a core use-case requirement stated in `concept.md`. This alternative would fail to meet the stated product goals.

## Implementation Notes

- **IMP-001**: Implement an `IAdrAiService` interface in `ADRPortal.Core` with methods `SuggestAlternativesAsync` and `EvaluateAndRecommendAsync`. Bind to the Copilot SDK implementation in `ADRPortal.Infrastructure`.
- **IMP-002**: Before each AI call, inject existing ADRs from the active repository as context. For large repositories, use a sliding-window strategy: always include all `Accepted` ADRs plus any ADRs whose tags overlap with the proposed decision's tags.
- **IMP-003**: Stream AI responses to the Blazor component using `IAsyncEnumerable<string>` and render incrementally via a Blazor streaming component pattern.
- **IMP-004**: Store the Copilot SDK credentials/tokens in .NET's `IConfiguration` / user secrets — never in source control.
- **IMP-005**: Design the UI to clearly label AI-generated content and include a disclaimer that recommendations require human review before acceptance.

## References

- **REF-001**: [ADR-0001: Blazor on .NET 10 as Web Application Framework](./adr-0001-blazor-dotnet10-framework.md)
- **REF-002**: [ADR-0005: File-Based ADR Storage and Folder Structure](./adr-0005-file-based-adr-storage.md)
- **REF-003**: [GitHub Copilot SDK documentation](https://docs.github.com/en/copilot/building-copilot-extensions/building-a-copilot-agent-for-your-copilot-extension/using-copilots-llm-for-your-agent)
- **REF-004**: [Microsoft.Extensions.AI abstractions](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
- **REF-005**: [concept.md — Use cases section](../../concept.md)
