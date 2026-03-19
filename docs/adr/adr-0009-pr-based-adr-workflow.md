---
status: "accepted"
date: 2026-03-19
decision-makers: [ADR Portal Project Team]
consulted: []
informed: []
---
# PR-Based Workflow for ADR State Transitions

## Context and Problem Statement

When an ADR moves from `proposed` to `accepted` or `rejected`, the change must be reflected in the repository's git history so that the decision and its review can be traced. A decision is needed on how the portal commits and reviews ADR state transitions — whether transitions are committed directly to the default branch or go through a pull request review process.

## Decision Drivers

* ADR state changes are significant architectural decisions that benefit from peer review
* Git history must capture who reviewed and approved the decision, not just who authored it
* The portal operates against repositories that may have branch protection rules on `main`/`master`
* Credential management must work both locally (ambient git config) and in Docker (environment variables)

## Considered Options

* PR-based workflow — proposed ADRs create a branch+PR; accept/reject merges/closes the PR
* Direct commit to default branch on state transition
* Tag-based workflow — state stored in git tags, no branch required

## Decision Outcome

Chosen option: "PR-based workflow", because it enables peer review of architectural decisions, works with branch protection rules, and produces a clear git history showing who reviewed and approved each ADR.

### Consequences

* Good, because each proposed ADR gets a dedicated branch (`proposed/adr-{NNNN}-{slug}`) and PR, enabling review comments and approval workflows.
* Good, because accepting an ADR merges the PR — the merge commit records the reviewer, not just the author.
* Good, because rejecting an ADR closes the PR and moves the file to `docs/adr/rejected/` without polluting `main`.
* Good, because compatible with repository branch protection rules that require PR reviews before merging.
* Bad, because requires the portal to have write access to the repository and PR creation permissions via a token.
* Bad, because adds latency to the accept/reject flow compared to a direct commit.

### Branch and PR naming conventions

* Branch: `proposed/adr-{NNNN}-{slug}`
* PR title: `ADR-{NNNN}: {title}`
* On accept: PR is merged; ADR file updated with `status: accepted` and `global-id` assigned
* On reject: PR is closed; ADR file moved to `docs/adr/rejected/`

### Credential resolution

1. `GITHUB_TOKEN` environment variable (covers both local dev and Docker deployment)
2. Ambient git credential helper (local dev fallback)
3. Explicit error shown in UI if neither is available

## Pros and Cons of the Options

### PR-based workflow

* Good, because enables peer review of architectural decisions.
* Good, because compatible with branch protection rules.
* Good, because produces auditable git history.
* Bad, because requires token with PR creation permissions.

### Direct commit to default branch

* Good, because simpler implementation with no PR API calls.
* Bad, because bypasses branch protection rules.
* Bad, because no review step; decisions are committed without peer sign-off.

### Tag-based workflow

* Good, because no branch management required.
* Bad, because state is stored in tags rather than file content — inconsistent with the MADR front matter `status` field.
* Bad, because poor IDE and GitHub UI discoverability.

## More Information

* [ADR-0010: LibGit2Sharp and Octokit.NET for Git Operations](adr-0010-libgit2sharp-octokit-git-operations.md)
* [ADR-0005: File-Based ADR Storage](adr-0005-file-based-adr-storage.md)
