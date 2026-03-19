---
status: "accepted"
date: 2026-03-19
decision-makers: [ADR Portal Project Team]
consulted: []
informed: []
---
# No In-App Authentication for v1; Security via Local Deployment Boundary

## Context and Problem Statement

The portal is designed as a local developer tool that operates against repositories on disk and is intentionally hosted over HTTP without internet exposure by default. The implementation plan states "HTTP only; no in-app auth" but this security posture was not captured as an explicit ADR. A clear decision is needed so future contributors understand whether user authentication/authorization is intentionally omitted in v1 or accidentally missing.

## Decision Drivers

* Product scope is local-first single-user/small-team usage on trusted machines
* Fast delivery of core ADR workflows is prioritized over multi-tenant security features
* Existing decisions assume direct filesystem and git credential access from the server process
* Avoid introducing identity provider setup and auth UX complexity in v1

## Considered Options

* No in-app authentication/authorization in v1; rely on local deployment boundary controls
* Add built-in username/password authentication in v1
* Integrate external identity provider (OIDC/Entra ID/GitHub OAuth) in v1

## Decision Outcome

Chosen option: "No in-app authentication/authorization in v1; rely on local deployment boundary controls", because the portal is scoped as a locally hosted engineering tool and this approach minimizes complexity while preserving the shortest path to delivering the core ADR management capabilities.

### Consequences

* Good, because delivery focus remains on ADR workflow functionality instead of auth infrastructure.
* Good, because local development and setup stay simple with no identity-provider dependencies.
* Good, because behavior aligns with the current deployment assumption (local, trusted network boundary).
* Bad, because if deployed to a shared or internet-reachable environment without additional controls, any reachable user could access the portal.
* Bad, because per-user audit identity inside the app is unavailable (actions are tracked through git/PR identity, not portal login identity).
* Bad, because future migration to authenticated multi-user hosting will require additional architecture work.

### Security boundary requirements for this decision

This decision is only valid when the portal is run in a trusted local environment with one or more of the following controls:

* Bind to loopback-only interfaces by default.
* Restrict host/network exposure via firewall, reverse proxy, or private network controls.
* Keep git/AI tokens in environment variables or local credential helpers; never in source control.

If these controls cannot be guaranteed, this ADR must be revisited and replaced with an authenticated deployment model.

## Pros and Cons of the Options

### No in-app authentication in v1

* Good, because simplest operational model for local developer usage.
* Good, because avoids identity setup and maintenance burden.
* Bad, because unsuitable for untrusted network exposure.

### Built-in username/password auth

* Good, because provides immediate access control inside the app.
* Bad, because introduces credential storage and security hardening responsibilities.
* Bad, because still limited for enterprise SSO requirements.

### External identity provider (OIDC)

* Good, because best long-term model for shared/multi-user deployments.
* Bad, because highest integration and configuration complexity for v1.
* Bad, because conflicts with the local-first, low-friction initial scope.

## More Information

* [ADR-0013: Blazor Interactive Server as the Web UI Framework](adr-0013-blazor-interactive-server.md)
* [ADR-0009: PR-Based Workflow for ADR State Transitions](adr-0009-pr-based-adr-workflow.md)
* [ADR-0010: LibGit2Sharp and Octokit.NET for Git Operations](adr-0010-libgit2sharp-octokit-git-operations.md)
