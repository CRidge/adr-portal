using AdrPortal.Core.Workflows;
using AdrPortal.Infrastructure.Workflows;

namespace AdrPortal.Infrastructure.Tests;

public class GitHubRemoteUrlParserTests
{
    [Test]
    public async Task Parse_HttpsRemote_ReturnsOwnerAndRepository()
    {
        var parsed = GitHubRemoteUrlParser.Parse("https://github.com/contoso/adr-portal.git");

        await Assert.That(parsed.Owner).IsEqualTo("contoso");
        await Assert.That(parsed.Repository).IsEqualTo("adr-portal");
    }

    [Test]
    public async Task Parse_SshRemote_ReturnsOwnerAndRepository()
    {
        var parsed = GitHubRemoteUrlParser.Parse("git@github.com:contoso/adr-portal.git");

        await Assert.That(parsed.Owner).IsEqualTo("contoso");
        await Assert.That(parsed.Repository).IsEqualTo("adr-portal");
    }

    [Test]
    public async Task Parse_ThrowsWhenHostIsNotGitHub()
    {
        Exception? exception = null;
        try
        {
            _ = GitHubRemoteUrlParser.Parse("https://example.com/contoso/adr-portal.git");
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is GitPrWorkflowException).IsTrue();
        await Assert.That(exception!.Message.Contains("not a GitHub host", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task Parse_ThrowsWhenUrlMissingOwnerAndRepository()
    {
        Exception? exception = null;
        try
        {
            _ = GitHubRemoteUrlParser.Parse("https://github.com");
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception is GitPrWorkflowException).IsTrue();
        await Assert.That(exception!.Message.Contains("owner and repository", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }
}
