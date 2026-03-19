using AdrPortal.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdrPortal.Web.Services;

/// <summary>
/// Manages file system watchers for managed repository inbox folders.
/// </summary>
public sealed class InboxWatcherCoordinator(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<InboxWatcherCoordinator> logger)
    : IInboxWatcherCoordinator, IHostedService, IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<int, WatchRegistration> registrations = [];
    private readonly CancellationTokenSource stoppingSource = new();
    private bool disposed;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await RefreshWatchersAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        stoppingSource.Cancel();
        DisposeAllRegistrations();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task RefreshWatchersAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var managedRepositoryStore = scope.ServiceProvider.GetRequiredService<IManagedRepositoryStore>();
        var repositories = await managedRepositoryStore.GetAllAsync(ct);
        var desiredRegistrations = new Dictionary<int, string>();
        foreach (var repository in repositories.Where(item => item.IsActive && !string.IsNullOrWhiteSpace(item.InboxFolder)))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var inboxPath = RepositoryPathSecurity.ResolveInboxPath(repository);
                Directory.CreateDirectory(inboxPath);
                desiredRegistrations[repository.Id] = inboxPath;
            }
            catch (InvalidOperationException exception)
            {
                logger.LogError(
                    exception,
                    "Skipping inbox watcher for repository {RepositoryId} because inbox configuration is invalid.",
                    repository.Id);
            }
            catch (IOException exception)
            {
                logger.LogError(
                    exception,
                    "Skipping inbox watcher for repository {RepositoryId} because inbox directory could not be prepared.",
                    repository.Id);
            }
            catch (UnauthorizedAccessException exception)
            {
                logger.LogError(
                    exception,
                    "Skipping inbox watcher for repository {RepositoryId} due to inbox directory access denial.",
                    repository.Id);
            }
        }

        List<WatchRegistration> toDispose = [];
        List<(int RepositoryId, string InboxPath)> toCreate = [];

        lock (gate)
        {
            foreach (var existing in registrations)
            {
                if (!desiredRegistrations.TryGetValue(existing.Key, out var desiredPath)
                    || !string.Equals(desiredPath, existing.Value.InboxPath, StringComparison.OrdinalIgnoreCase))
                {
                    toDispose.Add(existing.Value);
                }
            }

            foreach (var disposeTarget in toDispose)
            {
                _ = registrations.Remove(disposeTarget.RepositoryId);
            }

            foreach (var desired in desiredRegistrations)
            {
                if (!registrations.ContainsKey(desired.Key))
                {
                    toCreate.Add((desired.Key, desired.Value));
                }
            }
        }

        foreach (var disposeTarget in toDispose)
        {
            disposeTarget.Dispose();
            logger.LogInformation(
                "Stopped inbox watcher for repository {RepositoryId} at '{InboxPath}'.",
                disposeTarget.RepositoryId,
                disposeTarget.InboxPath);
        }

        foreach (var createTarget in toCreate)
        {
            var registration = CreateRegistration(createTarget.RepositoryId, createTarget.InboxPath);
            lock (gate)
            {
                registrations[registration.RepositoryId] = registration;
            }

            logger.LogInformation(
                "Started inbox watcher for repository {RepositoryId} at '{InboxPath}'.",
                registration.RepositoryId,
                registration.InboxPath);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        stoppingSource.Cancel();
        DisposeAllRegistrations();
        stoppingSource.Dispose();
    }

    private WatchRegistration CreateRegistration(int repositoryId, string inboxPath)
    {
        var watcher = new FileSystemWatcher(inboxPath)
        {
            Filter = "*.md",
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime
        };

        var registration = new WatchRegistration(repositoryId, inboxPath, watcher);
        watcher.Created += HandleCreated;
        watcher.Renamed += HandleRenamed;
        watcher.Error += HandleError;
        watcher.EnableRaisingEvents = true;
        return registration;
    }

    private void HandleCreated(object sender, FileSystemEventArgs eventArgs)
    {
        _ = ProcessInboxFileEventAsync(eventArgs.FullPath);
    }

    private void HandleRenamed(object sender, RenamedEventArgs eventArgs)
    {
        if (!string.Equals(Path.GetExtension(eventArgs.FullPath), ".md", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _ = ProcessInboxFileEventAsync(eventArgs.FullPath);
    }

    private void HandleError(object sender, ErrorEventArgs eventArgs)
    {
        logger.LogError(
            eventArgs.GetException(),
            "Inbox watcher reported an error and may need a refresh.");
    }

    private async Task ProcessInboxFileEventAsync(string fullPath)
    {
        try
        {
            if (stoppingSource.IsCancellationRequested)
            {
                return;
            }

            if (!string.Equals(Path.GetExtension(fullPath), ".md", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var registration = TryFindRegistrationForPath(fullPath);
            if (registration is null)
            {
                logger.LogWarning("Received inbox file event for unregistered path '{InboxFilePath}'.", fullPath);
                return;
            }

            await registration.SerialGate.WaitAsync(stoppingSource.Token);
            try
            {
                await ImportWithRetriesAsync(registration.RepositoryId, fullPath, stoppingSource.Token);
            }
            finally
            {
                registration.SerialGate.Release();
            }
        }
        catch (OperationCanceledException) when (stoppingSource.IsCancellationRequested)
        {
            logger.LogDebug("Inbox file processing was canceled during shutdown.");
        }
    }

    private WatchRegistration? TryFindRegistrationForPath(string fullPath)
    {
        var resolvedPath = Path.GetFullPath(fullPath);
        lock (gate)
        {
            foreach (var registration in registrations.Values)
            {
                var rootWithSeparator = registration.InboxPath.EndsWith(Path.DirectorySeparatorChar)
                    ? registration.InboxPath
                    : registration.InboxPath + Path.DirectorySeparatorChar;

                if (resolvedPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    return registration;
                }
            }
        }

        return null;
    }

    private async Task ImportWithRetriesAsync(int repositoryId, string filePath, CancellationToken ct)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var inboxImportService = scope.ServiceProvider.GetRequiredService<IInboxImportService>();
                var result = await inboxImportService.ImportInboxFileAsync(repositoryId, filePath, ct);
                if (result is null)
                {
                    logger.LogWarning(
                        "Inbox watcher import skipped because repository {RepositoryId} no longer exists for '{InboxFilePath}'.",
                        repositoryId,
                        filePath);
                }
                else
                {
                    logger.LogInformation(
                        "Inbox watcher imported '{InboxFilePath}' as ADR-{AdrNumber:0000} for repository {RepositoryId}.",
                        filePath,
                        result.ImportedAdr.Number,
                        repositoryId);
                }

                return;
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (DirectoryNotFoundException exception)
            {
                logger.LogError(
                    exception,
                    "Inbox watcher could not import '{InboxFilePath}' because the directory was not found.",
                    filePath);
                return;
            }
            catch (FormatException exception)
            {
                logger.LogError(
                    exception,
                    "Inbox watcher could not parse '{InboxFilePath}' as markdown/MADR content.",
                    filePath);
                return;
            }
            catch (ArgumentException exception)
            {
                logger.LogError(
                    exception,
                    "Inbox watcher rejected '{InboxFilePath}' due to invalid input.",
                    filePath);
                return;
            }
            catch (InvalidOperationException exception)
            {
                logger.LogError(
                    exception,
                    "Inbox watcher could not import '{InboxFilePath}' due to invalid repository or content state.",
                    filePath);
                return;
            }
            catch (IOException exception)
            {
                if (attempt == maxAttempts)
                {
                    logger.LogError(
                        exception,
                        "Inbox watcher failed to import '{InboxFilePath}' after {AttemptCount} attempts.",
                        filePath,
                        maxAttempts);
                    return;
                }

                logger.LogWarning(
                    exception,
                    "Inbox watcher import attempt {AttemptNumber}/{AttemptCount} for '{InboxFilePath}' failed due to I/O. Retrying.",
                    attempt,
                    maxAttempts,
                    filePath);
                await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), ct);
            }
            catch (UnauthorizedAccessException exception)
            {
                if (attempt == maxAttempts)
                {
                    logger.LogError(
                        exception,
                        "Inbox watcher failed to import '{InboxFilePath}' after {AttemptCount} attempts due to access denial.",
                        filePath,
                        maxAttempts);
                    return;
                }

                logger.LogWarning(
                    exception,
                    "Inbox watcher import attempt {AttemptNumber}/{AttemptCount} for '{InboxFilePath}' was denied. Retrying.",
                    attempt,
                    maxAttempts,
                    filePath);
                await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), ct);
            }
        }
    }

    private void DisposeAllRegistrations()
    {
        List<WatchRegistration> snapshot;
        lock (gate)
        {
            snapshot = [.. registrations.Values];
            registrations.Clear();
        }

        foreach (var registration in snapshot)
        {
            registration.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(InboxWatcherCoordinator));
        }
    }

    private sealed class WatchRegistration(int repositoryId, string inboxPath, FileSystemWatcher watcher) : IDisposable
    {
        public int RepositoryId { get; } = repositoryId;

        public string InboxPath { get; } = inboxPath;

        public FileSystemWatcher Watcher { get; } = watcher;

        public SemaphoreSlim SerialGate { get; } = new(1, 1);

        public void Dispose()
        {
            Watcher.EnableRaisingEvents = false;
            Watcher.Dispose();
            SerialGate.Dispose();
        }
    }
}
