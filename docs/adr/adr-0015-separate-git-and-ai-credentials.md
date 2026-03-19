---
status: "accepted"
date: 2026-03-19
decision-makers: [ADR Portal Project Team]
consulted: []
informed: []
---
# Separate Credentials for Git Operations and AI Provider Access

## Context and Problem Statement

The current documentation notes that `GITHUB_TOKEN` may be used both for git/PR operations and for AI provider access. Reusing one token for multiple concerns increases blast radius, makes least-privilege harder to enforce, and creates ambiguity in deployment configuration. A clear credential-separation decision is needed.

## Decision Drivers

* Principle of least privilege for all external integrations
* Reduced operational risk if one token is leaked or revoked
* Clear and explicit runtime configuration for deployment environments
* Compatibility with local development using ambient git credential helpers

## Considered Options

* Separate credentials: `GITHUB_TOKEN` for git/PR operations and `COPILOT_TOKEN` for AI provider access
* Single shared `GITHUB_TOKEN` for both git and AI
* Brokered credential service (vault-issued short-lived tokens)

## Decision Outcome

Chosen option: "Separate credentials for git and AI integrations", because it best enforces least privilege while keeping configuration straightforward for both local and deployed environments.

### Consequences

* Good, because token scope can be minimized independently for git and AI operations.
* Good, because credential rotation can happen per integration without coupling failures.
* Good, because compromise of one token does not automatically grant access to both capabilities.
* Bad, because deployment configuration has one extra environment variable to manage.
* Bad, because initial setup documentation must clearly explain fallback behavior.

### Configuration model

* Git/PR integration token: `GITHUB_TOKEN` (preferred), with ambient git credential helper fallback for local dev.
* AI provider token: `COPILOT_TOKEN` (required when Copilot is the selected AI provider).

If `COPILOT_TOKEN` is missing and Copilot is configured as provider, the portal should surface an explicit configuration error.

## Pros and Cons of the Options

### Separate credentials

* Good, because enforces separation of concerns and least privilege.
* Good, because failures and rotation are isolated per integration.
* Bad, because increases configuration surface slightly.

### Single shared token

* Good, because simplest initial setup.
* Bad, because broader token permissions are typically required.
* Bad, because increases blast radius and operational coupling.

### Brokered credential service

* Good, because strongest long-term security posture with short-lived credentials.
* Bad, because introduces infrastructure complexity beyond v1 scope.
* Bad, because conflicts with local-first minimal setup goals.

## More Information

* [ADR-0004: Microsoft.Extensions.AI Abstraction with GitHub Copilot SDK as Default Provider](adr-0004-copilot-sdk-ai-integration.md)
* [ADR-0009: PR-Based Workflow for ADR State Transitions](adr-0009-pr-based-adr-workflow.md)
* [ADR-0010: LibGit2Sharp and Octokit.NET for Git Operations](adr-0010-libgit2sharp-octokit-git-operations.md)
* [ADR-0014: No In-App Authentication for v1; Security via Local Deployment Boundary](adr-0014-no-in-app-auth-local-network-boundary.md)
