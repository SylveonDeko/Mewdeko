using System.Threading;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Impl;
using Mewdeko.Services.strings;
using StackExchange.Redis;

namespace Mewdeko.Services.Translations;

/// <summary>
///     Registers the bot's strings with the translation tables and keeps approved translations applied.
/// </summary>
public class TranslationService : INService, IReadyExecutor, IDisposable
{
    /// <summary>
    ///     The Redis channel the dashboard publishes on after it changes overrides. It has no key prefix.
    /// </summary>
    public const string ReloadChannel = "mewdeko:translations:reload";

    /// <summary>
    ///     The message that asks for the bot catalog to be reloaded.
    /// </summary>
    public const string ReloadMessage = "bot";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(10);

    private readonly CancellationTokenSource cts = new();
    private readonly BotCredentials creds;
    private readonly ILogger<TranslationService> logger;
    private readonly MissingTablesNotice missingTables;
    private readonly ConnectionMultiplexer redis;
    private readonly TranslationCatalogRegistrar registrar;
    private readonly SemaphoreSlim reloadLock = new(1, 1);
    private readonly IStringsSource source;
    private readonly ITranslationStore store;
    private readonly IBotStringsProvider stringsProvider;
    private long appliedVersion = -1;
    private int disposed;
    private int pendingReload;
    private int started;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TranslationService" /> class.
    /// </summary>
    /// <param name="store">The translation tables.</param>
    /// <param name="source">The response string files.</param>
    /// <param name="stringsProvider">The provider that holds the loaded strings.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="redis">The Redis connection.</param>
    /// <param name="logger">The logger.</param>
    public TranslationService(ITranslationStore store, IStringsSource source, IBotStringsProvider stringsProvider,
        BotCredentials creds, ConnectionMultiplexer redis, ILogger<TranslationService> logger)
    {
        this.store = store;
        this.source = source;
        this.stringsProvider = stringsProvider;
        this.creds = creds;
        this.redis = redis;
        this.logger = logger;
        missingTables = new MissingTablesNotice(logger);
        registrar = new TranslationCatalogRegistrar(store, logger);
    }

    /// <summary>
    ///     Stops the periodic check and the subscription.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 1)
            return;

        cts.Cancel();
        cts.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Starts the translation work in the background so it never delays the other ready handlers.
    /// </summary>
    /// <returns>A completed task.</returns>
    public Task OnReadyAsync()
    {
        if (Interlocked.Exchange(ref started, 1) == 0)
            _ = Task.Run(StartAsync);

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Reloads the strings from the files and applies the overrides read from the database. When the overrides cannot
    ///     be read the strings are still reloaded if <paramref name="reloadFilesAnyway" /> is set, keeping the overrides
    ///     applied last. Only one reload runs at a time and it never throws.
    /// </summary>
    /// <param name="reloadFilesAnyway">Reload the files even when the overrides cannot be read.</param>
    public async Task ReloadAsync(bool reloadFilesAnyway = false)
    {
        try
        {
            await reloadLock.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            Interlocked.Exchange(ref pendingReload, 0);

            long? version = null;
            IReadOnlyList<TranslationOverride>? overrides = null;

            try
            {
                version = await store.GetVersionAsync(TranslationCatalog.Name, cts.Token).ConfigureAwait(false);
                overrides = await store.GetOverridesAsync(TranslationCatalog.Name, cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (TranslationCatalogRegistrar.IsMissingTables(ex))
            {
                missingTables.Report();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Reading the translation overrides failed");
            }

            if (overrides is not null)
            {
                stringsProvider.ReloadWithOverrides(overrides);
                Volatile.Write(ref appliedVersion, version ?? -1);
                logger.LogInformation("Applied {Count} translation overrides at version {Version}", overrides.Count,
                    version);
            }
            else if (reloadFilesAnyway)
            {
                stringsProvider.Reload();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Reloading the strings with translation overrides failed");
        }
        finally
        {
            reloadLock.Release();
        }
    }

    private async Task StartAsync()
    {
        try
        {
            if (creds.IsMasterInstance)
            {
                var result = await registrar.RegisterAsync(source.GetResponseFiles(), cts.Token).ConfigureAwait(false);
                if (result == RegistrationResult.MissingTables)
                    missingTables.Report();
            }

            await ReloadAsync().ConfigureAwait(false);

            redis.GetSubscriber().Subscribe(RedisChannel.Literal(ReloadChannel), OnReloadMessage,
                CommandFlags.FireAndForget);

            _ = Task.Run(CheckVersionLoop);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Starting the translation service failed");
        }
    }

    private void OnReloadMessage(RedisChannel channel, RedisValue message)
    {
        if (!string.Equals(message.ToString().Trim(), ReloadMessage, StringComparison.Ordinal))
            return;

        if (Interlocked.Exchange(ref pendingReload, 1) == 1)
            return;

        _ = Task.Run(() => ReloadAsync());
    }

    private async Task CheckVersionLoop()
    {
        using var timer = new PeriodicTimer(CheckInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cts.Token).ConfigureAwait(false))
            {
                try
                {
                    var current = await store.GetVersionAsync(TranslationCatalog.Name, cts.Token)
                        .ConfigureAwait(false);

                    var applied = Volatile.Read(ref appliedVersion);

                    if (TranslationOverrides.VersionChanged(applied < 0 ? null : applied, current))
                        await ReloadAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (TranslationCatalogRegistrar.IsMissingTables(ex))
                {
                    missingTables.Report();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Checking the translation version failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
