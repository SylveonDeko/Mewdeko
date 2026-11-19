using System.Threading;
using Mewdeko.Services.strings;
using Npgsql;

namespace Mewdeko.Services.Translations;

/// <summary>
///     What a registration attempt did.
/// </summary>
public enum RegistrationResult
{
    /// <summary>
    ///     The stored hash already matched, or there was nothing to register.
    /// </summary>
    Skipped,

    /// <summary>
    ///     The catalog was written.
    /// </summary>
    Registered,

    /// <summary>
    ///     The translation tables do not exist yet.
    /// </summary>
    MissingTables,

    /// <summary>
    ///     Registration failed for another reason. The error was logged.
    /// </summary>
    Failed
}

/// <summary>
///     Logs a missing-tables condition once, however many times it is hit.
/// </summary>
/// <param name="logger">The logger.</param>
public sealed class MissingTablesNotice(ILogger logger)
{
    private int reported;

    /// <summary>
    ///     Logs at information level the first time it is called.
    /// </summary>
    public void Report()
    {
        if (Interlocked.Exchange(ref reported, 1) == 0)
        {
            logger.LogInformation(
                "The translation tables do not exist yet. Translations are skipped until the dashboard has migrated");
        }
    }
}

/// <summary>
///     Registers the bot catalog in the translation tables.
/// </summary>
/// <param name="store">The translation tables.</param>
/// <param name="logger">The logger.</param>
public sealed class TranslationCatalogRegistrar(ITranslationStore store, ILogger logger)
{
    /// <summary>
    ///     Tells whether an exception means the translation tables are missing: Postgres error 42P01, possibly wrapped.
    /// </summary>
    /// <param name="exception">The exception to look at.</param>
    /// <returns>True when a table does not exist.</returns>
    public static bool IsMissingTables(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable })
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Builds the catalog from the files and writes it unless the stored hash already matches. Never throws.
    /// </summary>
    /// <param name="files">The response files.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>What happened.</returns>
    public async Task<RegistrationResult> RegisterAsync(IEnumerable<ResponseFile> files,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = TranslationCatalog.Build(files);
            if (snapshot.Strings.Count == 0)
            {
                logger.LogWarning("No {SourceLocale} response strings found, not registering the {Catalog} catalog",
                    TranslationCatalog.SourceLocale, TranslationCatalog.Name);
                return RegistrationResult.Skipped;
            }

            var stored = await store.GetBuildHashAsync(TranslationCatalog.Name, cancellationToken)
                .ConfigureAwait(false);
            if (string.Equals(stored, snapshot.Hash, StringComparison.Ordinal))
                return RegistrationResult.Skipped;

            await store.ReplaceCatalogAsync(TranslationCatalog.Name, snapshot, cancellationToken)
                .ConfigureAwait(false);
            logger.LogInformation("Registered the {Catalog} translation catalog: {Strings} strings, {Baselines} baselines",
                TranslationCatalog.Name, snapshot.Strings.Count, snapshot.Baselines.Count);
            return RegistrationResult.Registered;
        }
        catch (Exception ex) when (IsMissingTables(ex))
        {
            return RegistrationResult.MissingTables;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Registering the {Catalog} translation catalog failed", TranslationCatalog.Name);
            return RegistrationResult.Failed;
        }
    }
}
