using AdrPortal.Core.Workflows;
using LibGit2Sharp;
using Microsoft.Extensions.Options;

namespace AdrPortal.Infrastructure.Workflows;

/// <summary>
/// Performs deterministic repository branch and commit operations using LibGit2Sharp.
/// </summary>
public sealed class LibGit2SharpRepositoryService(IOptions<GitHubOptions> options) : IGitRepositoryService
{
    /// <inheritdoc />
    public Task<string> CommitAdrChangeAsync(
        string repositoryRootPath,
        string repoRelativeFilePath,
        string baseBranchName,
        string branchName,
        string commitMessage,
        string? gitUserName,
        string? gitPassword,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureRequired(repositoryRootPath, nameof(repositoryRootPath));
        EnsureRequired(repoRelativeFilePath, nameof(repoRelativeFilePath));
        EnsureRequired(baseBranchName, nameof(baseBranchName));
        EnsureRequired(branchName, nameof(branchName));
        EnsureRequired(commitMessage, nameof(commitMessage));

        var normalizedRoot = Path.GetFullPath(repositoryRootPath);
        if (!Directory.Exists(normalizedRoot))
        {
            throw new GitPrWorkflowException($"Repository root path '{normalizedRoot}' does not exist.");
        }

        var absoluteAdrPath = ResolveRepositoryPath(normalizedRoot, repoRelativeFilePath);
        if (!File.Exists(absoluteAdrPath))
        {
            throw new GitPrWorkflowException(
                $"ADR file '{repoRelativeFilePath}' was not found in repository '{normalizedRoot}'.");
        }

        return Task.FromResult(CommitInternal(
            normalizedRoot,
            absoluteAdrPath,
            repoRelativeFilePath,
            baseBranchName.Trim(),
            branchName.Trim(),
            commitMessage.Trim(),
            gitUserName,
            gitPassword));
    }

    private string CommitInternal(
        string repositoryRootPath,
        string absoluteAdrPath,
        string repoRelativeFilePath,
        string baseBranchName,
        string branchName,
        string commitMessage,
        string? gitUserName,
        string? gitPassword)
    {
        try
        {
            using var repository = new Repository(repositoryRootPath);
            var baseBranch = ResolveBranch(repository, baseBranchName);
            var targetBranch = ResolveOrCreateBranch(repository, branchName, baseBranch);
            Commands.Checkout(repository, targetBranch);

            var relativePathForGit = NormalizeToGitPath(repoRelativeFilePath);
            Commands.Stage(repository, relativePathForGit);
            var fileStatus = repository.RetrieveStatus(relativePathForGit);
            if (!HasCommitCandidate(fileStatus))
            {
                throw new GitPrWorkflowException(
                    $"No tracked changes were found for '{repoRelativeFilePath}' when staging the branch '{branchName}'.");
            }

            var signature = BuildSignature();
            var commit = repository.Commit(commitMessage, signature, signature);
            var pushOptions = BuildPushOptions(gitUserName, gitPassword);
            repository.Network.Push(targetBranch, pushOptions);
            return commit.Sha;
        }
        catch (RepositoryNotFoundException exception)
        {
            throw new GitPrWorkflowException(
                $"Path '{repositoryRootPath}' is not a Git repository.", exception);
        }
        catch (EmptyCommitException exception)
        {
            throw new GitPrWorkflowException(
                $"No content changes were detected for '{repoRelativeFilePath}' to commit.", exception);
        }
        catch (LibGit2SharpException exception)
        {
            throw new GitPrWorkflowException(
                $"Git operation failed for branch '{branchName}': {exception.Message}",
                exception);
        }
    }

    private static string ResolveRepositoryPath(string repositoryRootPath, string repoRelativeFilePath)
    {
        var normalizedRelative = NormalizeToGitPath(repoRelativeFilePath).Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.GetFullPath(Path.Combine(repositoryRootPath, normalizedRelative));
        var rootWithSeparator = repositoryRootPath.EndsWith(Path.DirectorySeparatorChar)
            ? repositoryRootPath
            : repositoryRootPath + Path.DirectorySeparatorChar;
        if (!absolutePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(absolutePath, repositoryRootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new GitPrWorkflowException(
                $"ADR path '{repoRelativeFilePath}' escapes repository root '{repositoryRootPath}'.");
        }

        return absolutePath;
    }

    private static string NormalizeToGitPath(string path)
    {
        var segments = path
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length is 0 || segments.Any(segment => segment is "." or ".."))
        {
            throw new GitPrWorkflowException($"ADR path '{path}' is not a valid repository-relative file path.");
        }

        return string.Join('/', segments);
    }

    private static Branch ResolveBranch(Repository repository, string branchName)
    {
        var branch = repository.Branches[branchName];
        if (branch is not null)
        {
            return branch;
        }

        var canonical = $"origin/{branchName}";
        var remoteBranch = repository.Branches[canonical];
        if (remoteBranch is null)
        {
            throw new GitPrWorkflowException($"Base branch '{branchName}' was not found in repository '{repository.Info.WorkingDirectory}'.");
        }

        return repository.CreateBranch(branchName, remoteBranch.Tip);
    }

    private static Branch ResolveOrCreateBranch(Repository repository, string branchName, Branch baseBranch)
    {
        var existing = repository.Branches[branchName];
        if (existing is not null)
        {
            return existing;
        }

        return repository.CreateBranch(branchName, baseBranch.Tip);
    }

    private Signature BuildSignature()
    {
        var now = DateTimeOffset.UtcNow;
        return new Signature(options.Value.CommitAuthorName, options.Value.CommitAuthorEmail, now);
    }

    private static PushOptions BuildPushOptions(string? gitUserName, string? gitPassword)
    {
        if (string.IsNullOrWhiteSpace(gitUserName) || string.IsNullOrWhiteSpace(gitPassword))
        {
            return new PushOptions();
        }

        return new PushOptions
        {
            CredentialsProvider = (_, _, _) => new UsernamePasswordCredentials
            {
                Username = gitUserName.Trim(),
                Password = gitPassword.Trim()
            }
        };
    }

    private static bool HasCommitCandidate(FileStatus fileStatus)
    {
        return fileStatus is not FileStatus.Unaltered
            and not FileStatus.Nonexistent
            and not FileStatus.Ignored;
    }

    private static void EnsureRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }
    }
}
