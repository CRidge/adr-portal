namespace AdrPortal.Web.Tests;

public class HomePageContentTests
{
    [Test]
    public async Task HomePage_IncludesPhaseThirteenSignals()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var homePagePath = Path.Combine(repositoryRoot, "src", "AdrPortal.Web", "Components", "Pages", "Home.razor");
        var normalizedPath = Path.GetFullPath(homePagePath);

        var homeMarkup = await File.ReadAllTextAsync(normalizedPath);

        await Assert.That(homeMarkup.Contains("Phase 13", StringComparison.Ordinal)).IsTrue();
        await Assert.That(homeMarkup.Contains("Source-to-target ADR comparison", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(homeMarkup.Contains("/compare", StringComparison.Ordinal)).IsTrue();
        await Assert.That(homeMarkup.Contains("/global", StringComparison.Ordinal)).IsTrue();
        await Assert.That(homeMarkup.Contains("select import rows", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task AppShell_DoesNotReferenceBootstrapStylesheet()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var appComponentPath = Path.Combine(repositoryRoot, "src", "AdrPortal.Web", "Components", "App.razor");
        var appComponent = await File.ReadAllTextAsync(appComponentPath);

        await Assert.That(appComponent.Contains("bootstrap", StringComparison.OrdinalIgnoreCase)).IsFalse();
    }

    [Test]
    public async Task AppShell_ReferencesThemeScript()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var appComponentPath = Path.Combine(repositoryRoot, "src", "AdrPortal.Web", "Components", "App.razor");
        var appComponent = await File.ReadAllTextAsync(appComponentPath);

        await Assert.That(appComponent.Contains("@Assets[\"theme.js\"]", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task SettingsReposPage_ContainsRepositoryManagementControls()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var pagePath = Path.Combine(repositoryRoot, "src", "AdrPortal.Web", "Components", "Pages", "SettingsRepos.razor");
        var markup = await File.ReadAllTextAsync(pagePath);

        await Assert.That(markup.Contains("@page \"/settings/repos\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markup.Contains("Add repository", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markup.Contains("Edit", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markup.Contains("Remove", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markup.Contains("Git remote URL", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markup.Contains("Git remote URL (optional)", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task MainLayout_ContainsSidebarRepositorySection()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var layoutPath = Path.Combine(repositoryRoot, "src", "AdrPortal.Web", "Components", "Layout", "MainLayout.razor");
        var markup = await File.ReadAllTextAsync(layoutPath);

        await Assert.That(markup.Contains("Managed repositories", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markup.Contains("Global library", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markup.Contains("shell__nav-badge", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markup.Contains("/global", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markup.Contains("/compare", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markup.Contains("/settings/repos", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markup.Contains("shell__repo-list", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markup.Contains("/repos/{repository.Id}", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markup.Contains("shell__repo-indicator", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markup.Contains("Phase 13 source-to-target ADR comparison + import workflows", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markup.Contains("shell__theme-toggle", StringComparison.Ordinal)).IsTrue();
        await Assert.That(markup.Contains("ToggleThemeAsync", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task RepositoryComparePage_ContainsSourceTargetSelectionAndImportFlow()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var comparePagePath = Path.Combine(repositoryRoot, "src", "AdrPortal.Web", "Components", "Pages", "RepositoryCompare.razor");
        var compareMarkup = await File.ReadAllTextAsync(comparePagePath);

        await Assert.That(compareMarkup.Contains("@page \"/compare\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(compareMarkup.Contains("Source repository", StringComparison.Ordinal)).IsTrue();
        await Assert.That(compareMarkup.Contains("Target repository", StringComparison.Ordinal)).IsTrue();
        await Assert.That(compareMarkup.Contains("Compare repositories", StringComparison.Ordinal)).IsTrue();
        await Assert.That(compareMarkup.Contains("Ranked source ADR list", StringComparison.Ordinal)).IsTrue();
        await Assert.That(compareMarkup.Contains("Import selected ADRs as proposed", StringComparison.Ordinal)).IsTrue();
        await Assert.That(compareMarkup.Contains("global-id", StringComparison.Ordinal)).IsTrue();
        await Assert.That(compareMarkup.Contains("global-version", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task RepositoryRoutes_ContainListDetailAndEditorPages()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var listPath = Path.Combine(repositoryRoot, "src", "AdrPortal.Web", "Components", "Pages", "RepositoryAdrs.razor");
        var detailPath = Path.Combine(repositoryRoot, "src", "AdrPortal.Web", "Components", "Pages", "RepositoryAdrDetail.razor");
        var editorPath = Path.Combine(repositoryRoot, "src", "AdrPortal.Web", "Components", "Pages", "RepositoryAdrEditor.razor");
        var listMarkup = await File.ReadAllTextAsync(listPath);
        var detailMarkup = await File.ReadAllTextAsync(detailPath);
        var editorMarkup = await File.ReadAllTextAsync(editorPath);

        await Assert.That(listMarkup.Contains("@page \"/repos/{RepositoryId:int}\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(listMarkup.Contains("Status filters", StringComparison.Ordinal)).IsTrue();
        await Assert.That(listMarkup.Contains("Search ADRs", StringComparison.Ordinal)).IsTrue();
        await Assert.That(listMarkup.Contains("+ New ADR", StringComparison.Ordinal)).IsTrue();
        await Assert.That(listMarkup.Contains("Bootstrap ADRs with AI", StringComparison.Ordinal)).IsTrue();
        await Assert.That(listMarkup.Contains("Accept selected proposals", StringComparison.Ordinal)).IsTrue();
        await Assert.That(listMarkup.Contains("Queued", StringComparison.Ordinal)).IsTrue();
        await Assert.That(listMarkup.Contains("Git/PR workflow", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detailMarkup.Contains("@page \"/repos/{RepositoryId:int}/adr/{Number:int}\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detailMarkup.Contains("state-badge", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detailMarkup.Contains("Render(result.Value.Adr.RawMarkdown)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detailMarkup.Contains("Lifecycle actions", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detailMarkup.Contains("Accept ADR", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detailMarkup.Contains("Propose library update", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detailMarkup.Contains("/global/{syncStatus.GlobalId}", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detailMarkup.Contains("Mark superseded", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detailMarkup.Contains("Mark deprecated", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detailMarkup.Contains("Find Affected ADRs", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detailMarkup.Contains("Affected ADRs", StringComparison.Ordinal)).IsTrue();
        await Assert.That(editorMarkup.Contains("@page \"/repos/{RepositoryId:int}/adr/new\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(editorMarkup.Contains("@page \"/repos/{RepositoryId:int}/adr/{Number:int}/edit\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(editorMarkup.Contains("MADR markdown body", StringComparison.Ordinal)).IsTrue();
        await Assert.That(editorMarkup.Contains("Raw HTML is not accepted", StringComparison.Ordinal)).IsTrue();
        await Assert.That(editorMarkup.Contains("Evaluate & Recommend", StringComparison.Ordinal)).IsTrue();
        await Assert.That(editorMarkup.Contains("AI recommendation", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task GlobalLibraryRoutes_ContainOverviewAndDetailPages()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var overviewPath = Path.Combine(repositoryRoot, "src", "AdrPortal.Web", "Components", "Pages", "GlobalLibrary.razor");
        var detailPath = Path.Combine(repositoryRoot, "src", "AdrPortal.Web", "Components", "Pages", "GlobalAdrDetail.razor");
        var overviewMarkup = await File.ReadAllTextAsync(overviewPath);
        var detailMarkup = await File.ReadAllTextAsync(detailPath);

        await Assert.That(overviewMarkup.Contains("@page \"/global\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(overviewMarkup.Contains("Library overview", StringComparison.Ordinal)).IsTrue();
        await Assert.That(overviewMarkup.Contains("Current version", StringComparison.Ordinal)).IsTrue();
        await Assert.That(overviewMarkup.Contains("Update available", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detailMarkup.Contains("@page \"/global/{GlobalId:guid}\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detailMarkup.Contains("Version history", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detailMarkup.Contains("Baseline diff viewer", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detailMarkup.Contains("Repo → library proposals", StringComparison.Ordinal)).IsTrue();
        await Assert.That(detailMarkup.Contains("Library → repos instances", StringComparison.Ordinal)).IsTrue();
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
