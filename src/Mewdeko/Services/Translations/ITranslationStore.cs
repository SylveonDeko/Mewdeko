using System.Threading;

namespace Mewdeko.Services.Translations;

/// <summary>
///     Reads and writes the translation tables the dashboard owns. The bot never creates or alters them.
/// </summary>
public interface ITranslationStore
{
    /// <summary>
    ///     Gets the hash stored for a catalog.
    /// </summary>
    /// <param name="catalog">The catalog name.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>The stored hash, or null when the catalog is not registered.</returns>
    public Task<string?> GetBuildHashAsync(string catalog, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Replaces a catalog's strings and baselines and stores its hash, in one transaction.
    /// </summary>
    /// <param name="catalog">The catalog name.</param>
    /// <param name="snapshot">What to store.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    public Task ReplaceCatalogAsync(string catalog, CatalogSnapshot snapshot,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the version of a catalog's overrides.
    /// </summary>
    /// <param name="catalog">The catalog name.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>The version, or null when the catalog is not registered.</returns>
    public Task<long?> GetVersionAsync(string catalog, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets every override of a catalog.
    /// </summary>
    /// <param name="catalog">The catalog name.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>The overrides.</returns>
    public Task<IReadOnlyList<TranslationOverride>> GetOverridesAsync(string catalog,
        CancellationToken cancellationToken = default);
}
