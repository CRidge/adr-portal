namespace AdrPortal.Web.Tests;

public class HomePageContentTests
{
    [Test]
    public async Task HomePage_IncludesPhaseOneSignals()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var homePagePath = Path.Combine(repositoryRoot, "src", "AdrPortal.Web", "Components", "Pages", "Home.razor");
        var normalizedPath = Path.GetFullPath(homePagePath);

        var homeMarkup = await File.ReadAllTextAsync(normalizedPath);

        await Assert.That(homeMarkup.Contains(".NET 10", StringComparison.Ordinal)).IsTrue();
        await Assert.That(homeMarkup.Contains("EF Core + SQLite", StringComparison.Ordinal)).IsTrue();
        await Assert.That(homeMarkup.Contains("Aspire", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task AppShell_DoesNotReferenceBootstrapStylesheet()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var appComponentPath = Path.Combine(repositoryRoot, "src", "AdrPortal.Web", "Components", "App.razor");
        var appComponent = await File.ReadAllTextAsync(appComponentPath);

        await Assert.That(appComponent.Contains("bootstrap", StringComparison.OrdinalIgnoreCase)).IsFalse();
    }

    private static string ResolveRepositoryRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "AdrPortal.slnx");
            if (File.Exists(solutionPath))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root from test execution directory.");
    }
}
