using AdrPortal.Core.Workflows;
using AdrPortal.Infrastructure.Workflows;
using Microsoft.Extensions.Options;

namespace AdrPortal.Infrastructure.Tests;

public class EnvironmentGitCredentialResolverTests
{
    private static readonly SemaphoreSlim EnvironmentGate = new(initialCount: 1, maxCount: 1);

    [Test]
    public async Task ResolveAsync_ReturnsTokenAndGitCredentialsWhenTokenPresent()
    {
        await EnvironmentGate.WaitAsync();
        var originalToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", " phase11-token ");
            var resolver = new EnvironmentGitCredentialResolver(
                Options.Create(
                    new GitHubOptions
                    {
                        TokenUserName = "x-access-token"
                    }));

            var credentials = await resolver.ResolveAsync(CancellationToken.None);

            await Assert.That(credentials.GitHubToken).IsEqualTo("phase11-token");
            await Assert.That(credentials.GitUserName).IsEqualTo("x-access-token");
            await Assert.That(credentials.GitPassword).IsEqualTo("phase11-token");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", originalToken);
            EnvironmentGate.Release();
        }
    }

    [Test]
    public async Task ResolveAsync_ThrowsWhenTokenMissing()
    {
        await EnvironmentGate.WaitAsync();
        var originalToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
            var resolver = new EnvironmentGitCredentialResolver(Options.Create(new GitHubOptions()));

            Exception? exception = null;
            try
            {
                _ = await resolver.ResolveAsync(CancellationToken.None);
            }
            catch (Exception caught)
            {
                exception = caught;
            }

            await Assert.That(exception is GitPrWorkflowException).IsTrue();
            await Assert.That(exception!.Message.Contains("GITHUB_TOKEN", StringComparison.OrdinalIgnoreCase)).IsTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", originalToken);
            EnvironmentGate.Release();
        }
    }
}
