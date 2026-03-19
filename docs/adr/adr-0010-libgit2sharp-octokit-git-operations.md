---
status: "accepted"
date: 2026-03-19
decision-makers: [ADR Portal Project Team]
consulted: []
informed: []
---
# LibGit2Sharp and Octokit.NET for Git Operations

## Context and Problem Statement

The PR-based ADR workflow (ADR-0009) requires the portal to perform git operations (create branch, stage file, commit, push) and GitHub API operations (create PR, merge PR, close PR) programmatically from .NET. A decision is needed on which libraries handle these two concerns.

## Decision Drivers

* Local git operations (branch, commit, push) must not shell out to `git` CLI — the deployment environment may not have git installed
* GitHub API operations (PR lifecycle) require an authenticated REST or GraphQL client
* Libraries must be maintained, support .NET 10, and have NuGet packages
* GitLab support should be possible in a future iteration via the same abstraction

## Considered Options

* LibGit2Sharp (local git) + Octokit.NET (GitHub API)
* Shell out to `git` CLI + `gh` CLI
* NGit (Java port, unmaintained)
* Native `System.Diagnostics.Process` + GitHub REST API via `HttpClient`

## Decision Outcome

Chosen option: "LibGit2Sharp + Octokit.NET", because LibGit2Sharp provides a fully managed .NET wrapper around libgit2 with no CLI dependency, and Octokit.NET is the official GitHub .NET client with full PR lifecycle support.

### Consequences

* Good, because LibGit2Sharp performs all local git operations in-process without requiring `git` CLI in the deployment environment.
* Good, because Octokit.NET is the official GitHub .NET SDK with typed models for PRs, branches, and merge operations.
* Good, because both libraries are actively maintained and support .NET 10.
* Good, because the `IGitService` abstraction allows a future `GitLabGitService` implementation to be added without changing calling code.
* Bad, because LibGit2Sharp bundles a native libgit2 binary, adding a platform-specific dependency to the Docker image.
* Bad, because Octokit.NET covers GitHub only; GitLab support requires a separate client (out of scope for v1).

### Abstraction

All git and hosting-provider operations are accessed through `IGitService` in `AdrPortal.Core`. The `GitHubGitService` in `AdrPortal.Infrastructure` implements this interface using LibGit2Sharp and Octokit.NET. A future `GitLabGitService` can be registered without changes to the domain layer.

## Pros and Cons of the Options

### LibGit2Sharp + Octokit.NET

* Good, because no CLI dependency; fully in-process.
* Good, because typed .NET APIs for both local and remote operations.
* Good, because both actively maintained with .NET 10 support.
* Bad, because native libgit2 binary adds platform-specific Docker layer.

### Shell out to git + gh CLI

* Good, because trivially simple; leverages existing tools.
* Bad, because requires `git` and `gh` to be installed in the deployment environment.
* Bad, because brittle — output parsing is fragile and error handling is complex.

### Native HttpClient for GitHub API

* Good, because no third-party dependency.
* Bad, because requires manual serialisation of all GitHub API request/response types.
* Bad, because significantly more code to maintain than Octokit.NET.

## More Information

* [ADR-0009: PR-Based ADR Workflow](adr-0009-pr-based-adr-workflow.md)
* [LibGit2Sharp](https://github.com/libgit2/libgit2sharp)
* [Octokit.NET](https://github.com/octokit/octokit.net)
