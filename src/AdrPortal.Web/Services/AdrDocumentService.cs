using AdrPortal.Core.Entities;
using AdrPortal.Core.Repositories;
using System.Text.RegularExpressions;

namespace AdrPortal.Web.Services;

/// <summary>
/// Resolves ADR data for managed repositories.
/// </summary>
public sealed class AdrDocumentService(IManagedRepositoryStore managedRepositoryStore, IMadrRepositoryFactory madrRepositoryFactory)
{
    private static readonly Regex SlugRegex = new(
        @"^[a-z0-9][a-z0-9-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HtmlTagRegex = new(
        @"<\s*/?\s*[a-zA-Z][a-zA-Z0-9-]*(?=[\s/>])[^>]*>|<!--[\s\S]*?-->",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Gets the repository and ADR list for the specified repository identifier.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>
    /// A tuple containing the resolved repository and ADR list, or <see langword="null"/> when the repository does not exist.
    /// </returns>
    public async Task<(ManagedRepository Repository, IReadOnlyList<Adr> Adrs)?> GetRepositoryWithAdrsAsync(int repositoryId, CancellationToken ct)
    {
        var resolvedRepository = await ResolveRepositoryAsync(repositoryId, ct);
        if (resolvedRepository is null)
        {
            return null;
        }

        var (repository, adrRepository) = resolvedRepository.Value;
        var adrs = await adrRepository.GetAllAsync(ct);
        return (repository, adrs);
    }

    /// <summary>
    /// Gets a single ADR with its repository context.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="number">ADR number.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>
    /// A tuple containing the repository and ADR when both are found; otherwise <see langword="null"/>.
    /// </returns>
    public async Task<(ManagedRepository Repository, Adr Adr)?> GetRepositoryAdrAsync(int repositoryId, int number, CancellationToken ct)
    {
        var resolvedRepository = await ResolveRepositoryAsync(repositoryId, ct);
        if (resolvedRepository is null)
        {
            return null;
        }

        var (repository, adrRepository) = resolvedRepository.Value;
        var adr = await adrRepository.GetByNumberAsync(number, ct);
        if (adr is null)
        {
            return null;
        }

        return (repository, adr);
    }

    /// <summary>
    /// Gets repository context and the next ADR number for create flows.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Repository context and next ADR number when repository exists; otherwise <see langword="null"/>.</returns>
    public async Task<(ManagedRepository Repository, int NextNumber)?> GetRepositoryForCreateAsync(int repositoryId, CancellationToken ct)
    {
        var resolvedRepository = await ResolveRepositoryAsync(repositoryId, ct);
        if (resolvedRepository is null)
        {
            return null;
        }

        var (repository, adrRepository) = resolvedRepository.Value;
        var nextNumber = await adrRepository.GetNextNumberAsync(ct);
        return (repository, nextNumber);
    }

    /// <summary>
    /// Creates and persists a new ADR markdown document.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="input">Normalized ADR editor input.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Persisted ADR result when repository exists; otherwise <see langword="null"/>.</returns>
    public async Task<AdrPersistResult?> CreateAdrAsync(int repositoryId, AdrEditorInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var resolvedRepository = await ResolveRepositoryAsync(repositoryId, ct);
        if (resolvedRepository is null)
        {
            return null;
        }

        var (repository, adrRepository) = resolvedRepository.Value;
        ValidateInput(input);

        var nextNumber = await adrRepository.GetNextNumberAsync(ct);
        var adrToWrite = BuildAdrForWrite(repository, existingAdr: null, nextNumber, input);
        var persistedAdr = await adrRepository.WriteAsync(adrToWrite, ct);

        return new AdrPersistResult
        {
            Repository = repository,
            Adr = persistedAdr
        };
    }

    /// <summary>
    /// Updates and persists an existing ADR markdown document.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="number">ADR number to update.</param>
    /// <param name="input">Normalized ADR editor input.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Persisted ADR result when repository and ADR exist; otherwise <see langword="null"/>.</returns>
    public async Task<AdrPersistResult?> UpdateAdrAsync(int repositoryId, int number, AdrEditorInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var resolvedRepository = await ResolveRepositoryAsync(repositoryId, ct);
        if (resolvedRepository is null)
        {
            return null;
        }

        var (repository, adrRepository) = resolvedRepository.Value;
        var existingAdr = await adrRepository.GetByNumberAsync(number, ct);
        if (existingAdr is null)
        {
            return null;
        }

        ValidateInput(input);

        var adrToWrite = BuildAdrForWrite(repository, existingAdr, number, input);
        var persistedAdr = await adrRepository.WriteAsync(adrToWrite, ct);

        return new AdrPersistResult
        {
            Repository = repository,
            Adr = persistedAdr
        };
    }

    /// <summary>
    /// Resolves repository metadata and a file-backed ADR repository for the requested identifier.
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Repository context and file repository when found; otherwise <see langword="null"/>.</returns>
    private async Task<(ManagedRepository Repository, IAdrFileRepository RepositoryFileStore)?> ResolveRepositoryAsync(int repositoryId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var repository = await managedRepositoryStore.GetByIdAsync(repositoryId, ct);
        if (repository is null)
        {
            return null;
        }

        var adrRepository = madrRepositoryFactory.Create(repository);
        return (repository, adrRepository);
    }

    /// <summary>
    /// Maps editor input and optional existing ADR metadata to a persisted ADR domain record.
    /// </summary>
    /// <param name="repository">Managed repository settings.</param>
    /// <param name="existingAdr">Existing ADR record for edit flows.</param>
    /// <param name="number">ADR number to persist.</param>
    /// <param name="input">Validated editor input.</param>
    /// <returns>ADR domain record ready for repository persistence.</returns>
    private static Adr BuildAdrForWrite(
        ManagedRepository repository,
        Adr? existingAdr,
        int number,
        AdrEditorInput input)
    {
        var normalizedSlug = input.Slug.Trim().ToLowerInvariant();
        var normalizedTitle = input.Title.Trim();
        var normalizedBody = input.BodyMarkdown.Trim();
        var normalizedAdrFolder = repository.AdrFolder.Replace('\\', '/').Trim('/');
        var defaultPath = $"{normalizedAdrFolder}/adr-{number:0000}-{normalizedSlug}.md";
        var relativePath = existingAdr?.RepoRelativePath ?? defaultPath;

        return new Adr
        {
            Number = number,
            Slug = normalizedSlug,
            RepoRelativePath = relativePath,
            Title = normalizedTitle,
            Status = input.Status,
            Date = input.Date,
            GlobalId = existingAdr?.GlobalId,
            GlobalVersion = existingAdr?.GlobalVersion,
            DecisionMakers = input.DecisionMakers,
            Consulted = input.Consulted,
            Informed = input.Informed,
            SupersededByNumber = input.SupersededByNumber,
            RawMarkdown = normalizedBody.StartsWith("# ", StringComparison.Ordinal)
                ? normalizedBody
                : $"# {normalizedTitle}\n\n{normalizedBody}"
        };
    }

    /// <summary>
    /// Validates editor input before creating or updating markdown files.
    /// </summary>
    /// <param name="input">Editor input to validate.</param>
    private static void ValidateInput(AdrEditorInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new ArgumentException("Title is required.", nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.Slug))
        {
            throw new ArgumentException("Slug is required.", nameof(input));
        }

        if (!SlugRegex.IsMatch(input.Slug))
        {
            throw new ArgumentException("Slug must contain lowercase letters, numbers, and hyphens only.", nameof(input));
        }

        if (input.DecisionMakers.Count is 0)
        {
            throw new ArgumentException("At least one decision maker is required.", nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.BodyMarkdown))
        {
            throw new ArgumentException("Markdown content is required.", nameof(input));
        }

        if (input.SupersededByNumber is <= 0)
        {
            throw new ArgumentException("Superseded-by ADR number must be greater than zero.", nameof(input));
        }

        EnsureTextContainsNoHtml(input.Title, "Title");
        EnsureTextContainsNoHtml(input.BodyMarkdown, "Markdown body");
        EnsureTextContainsNoHtml(string.Join('\n', input.DecisionMakers), "Decision makers");
        EnsureTextContainsNoHtml(string.Join('\n', input.Consulted), "Consulted");
        EnsureTextContainsNoHtml(string.Join('\n', input.Informed), "Informed");
    }

    /// <summary>
    /// Ensures a free-form text field does not include raw HTML markup.
    /// </summary>
    /// <param name="value">Text value to validate.</param>
    /// <param name="fieldName">Field display name for error messages.</param>
    private static void EnsureTextContainsNoHtml(string value, string fieldName)
    {
        if (!HtmlTagRegex.IsMatch(value))
        {
            return;
        }

        throw new ArgumentException($"{fieldName} cannot contain raw HTML markup.");
    }
}
