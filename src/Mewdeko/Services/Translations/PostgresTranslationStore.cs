using System.Text;
using System.Threading;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Database.Impl;

namespace Mewdeko.Services.Translations;

/// <summary>
///     Reads and writes the <c>dash_tr_*</c> tables over Postgres. The tables are created by the dashboard's migration,
///     never by the bot.
/// </summary>
public class PostgresTranslationStore : ITranslationStore, INService
{
    /// <summary>
    ///     Rows per statement. Three parameters per row keeps a statement far below Postgres' limit of 65535.
    /// </summary>
    public const int BatchSize = 1000;

    private readonly IBotCredentials creds;
    private readonly IDataConnectionFactory dbFactory;
    private readonly object factoryLock = new();
    private IDataConnectionFactory? translationsFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PostgresTranslationStore" /> class.
    /// </summary>
    /// <param name="dbFactory">The bot's own database connections.</param>
    /// <param name="creds">The bot credentials, for <c>TranslationsDbConnection</c>.</param>
    public PostgresTranslationStore(IDataConnectionFactory dbFactory, IBotCredentials creds)
    {
        this.dbFactory = dbFactory;
        this.creds = creds;
    }

    /// <inheritdoc />
    public async Task<string?> GetBuildHashAsync(string catalog, CancellationToken cancellationToken = default)
    {
        await using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.QueryToListAsync<string>(
            "SELECT build_hash FROM dash_tr_catalogs WHERE catalog = @catalog",
            new DataParameter("catalog", catalog, DataType.Text)).ConfigureAwait(false);

        return rows.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task ReplaceCatalogAsync(string catalog, CatalogSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        await using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await db.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var existing = await db.QueryToListAsync<string>(
            "SELECT \"key\" FROM dash_tr_strings WHERE catalog = @catalog",
            new DataParameter("catalog", catalog, DataType.Text)).ConfigureAwait(false);

        var removed = RemovedKeys(existing, snapshot.Strings);

        foreach (var batch in snapshot.Strings.Chunk(BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (sql, parameters) = BuildStringsUpsert(catalog, batch);
            await db.ExecuteAsync(sql, parameters).ConfigureAwait(false);
        }

        foreach (var batch in removed.Chunk(BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (sql, parameters) = BuildStringsDelete(catalog, batch);
            await db.ExecuteAsync(sql, parameters).ConfigureAwait(false);
        }

        await db.ExecuteAsync("DELETE FROM dash_tr_baselines WHERE catalog = @catalog",
            new DataParameter("catalog", catalog, DataType.Text)).ConfigureAwait(false);

        foreach (var batch in snapshot.Baselines.Chunk(BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (sql, parameters) = BuildBaselinesInsert(catalog, batch);
            await db.ExecuteAsync(sql, parameters).ConfigureAwait(false);
        }

        await db.ExecuteAsync(
            """
            INSERT INTO dash_tr_catalogs (catalog, build_hash) VALUES (@catalog, @hash)
            ON CONFLICT (catalog) DO UPDATE SET build_hash = EXCLUDED.build_hash, registered_at = now()
            """,
            new DataParameter("catalog", catalog, DataType.Text),
            new DataParameter("hash", snapshot.Hash, DataType.Text)).ConfigureAwait(false);

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long?> GetVersionAsync(string catalog, CancellationToken cancellationToken = default)
    {
        await using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.QueryToListAsync<long>(
            "SELECT version FROM dash_tr_catalogs WHERE catalog = @catalog",
            new DataParameter("catalog", catalog, DataType.Text)).ConfigureAwait(false);

        return rows.Count == 0 ? null : rows[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TranslationOverride>> GetOverridesAsync(string catalog,
        CancellationToken cancellationToken = default)
    {
        await using var db = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.QueryToListAsync<OverrideRow>(
            "SELECT locale AS \"Locale\", \"key\" AS \"Key\", \"text\" AS \"Text\" FROM dash_tr_overrides WHERE catalog = @catalog",
            new DataParameter("catalog", catalog, DataType.Text)).ConfigureAwait(false);

        return rows.Select(x => new TranslationOverride(x.Locale, x.Key, x.Text)).ToList();
    }

    /// <summary>
    ///     Lists the stored keys that are not in the new catalog any more.
    /// </summary>
    /// <param name="existing">The keys stored now.</param>
    /// <param name="strings">The strings of the new catalog.</param>
    /// <returns>The keys to delete.</returns>
    public static List<string> RemovedKeys(IEnumerable<string> existing, IReadOnlyList<CatalogString> strings)
    {
        var current = strings.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        return existing.Where(x => !current.Contains(x)).ToList();
    }

    /// <summary>
    ///     Builds the statement that upserts a batch of strings. The source text and group are always written;
    ///     <c>updated_at</c> moves only when the source text differs from the stored one.
    /// </summary>
    /// <param name="catalog">The catalog name.</param>
    /// <param name="batch">The strings to write.</param>
    /// <returns>The statement and its parameters.</returns>
    public static (string Sql, DataParameter[] Parameters) BuildStringsUpsert(string catalog,
        IReadOnlyList<CatalogString> batch)
    {
        var sql = new StringBuilder("INSERT INTO dash_tr_strings (catalog, \"key\", grp, source_text, updated_at) VALUES ");
        var parameters = new List<DataParameter> { new("catalog", catalog, DataType.Text) };

        for (var i = 0; i < batch.Count; i++)
        {
            if (i > 0)
                sql.Append(", ");

            sql.Append($"(@catalog, @k{i}, @g{i}, @s{i}, now())");
            parameters.Add(new DataParameter($"k{i}", batch[i].Key, DataType.Text));
            parameters.Add(new DataParameter($"g{i}", batch[i].Group, DataType.Text));
            parameters.Add(new DataParameter($"s{i}", batch[i].SourceText, DataType.Text));
        }

        sql.Append(
            " ON CONFLICT (catalog, \"key\") DO UPDATE SET grp = EXCLUDED.grp, source_text = EXCLUDED.source_text, " +
            "updated_at = CASE WHEN dash_tr_strings.source_text IS DISTINCT FROM EXCLUDED.source_text " +
            "THEN now() ELSE dash_tr_strings.updated_at END");

        return (sql.ToString(), parameters.ToArray());
    }

    /// <summary>
    ///     Builds the statement that deletes a batch of strings by key.
    /// </summary>
    /// <param name="catalog">The catalog name.</param>
    /// <param name="keys">The keys to delete.</param>
    /// <returns>The statement and its parameters.</returns>
    public static (string Sql, DataParameter[] Parameters) BuildStringsDelete(string catalog,
        IReadOnlyList<string> keys)
    {
        var parameters = new List<DataParameter> { new("catalog", catalog, DataType.Text) };
        var names = new List<string>();

        for (var i = 0; i < keys.Count; i++)
        {
            names.Add($"@k{i}");
            parameters.Add(new DataParameter($"k{i}", keys[i], DataType.Text));
        }

        return ($"DELETE FROM dash_tr_strings WHERE catalog = @catalog AND \"key\" IN ({string.Join(", ", names)})",
            parameters.ToArray());
    }

    /// <summary>
    ///     Builds the statement that inserts a batch of baselines.
    /// </summary>
    /// <param name="catalog">The catalog name.</param>
    /// <param name="batch">The baselines to insert.</param>
    /// <returns>The statement and its parameters.</returns>
    public static (string Sql, DataParameter[] Parameters) BuildBaselinesInsert(string catalog,
        IReadOnlyList<CatalogBaseline> batch)
    {
        var sql = new StringBuilder("INSERT INTO dash_tr_baselines (catalog, \"key\", locale, \"text\") VALUES ");
        var parameters = new List<DataParameter> { new("catalog", catalog, DataType.Text) };

        for (var i = 0; i < batch.Count; i++)
        {
            if (i > 0)
                sql.Append(", ");

            sql.Append($"(@catalog, @k{i}, @l{i}, @t{i})");
            parameters.Add(new DataParameter($"k{i}", batch[i].Key, DataType.Text));
            parameters.Add(new DataParameter($"l{i}", batch[i].Locale, DataType.Text));
            parameters.Add(new DataParameter($"t{i}", batch[i].Text, DataType.Text));
        }

        return (sql.ToString(), parameters.ToArray());
    }

    private Task<MewdekoDb> OpenAsync(CancellationToken cancellationToken)
    {
        return ResolveFactory().CreateConnectionAsync(cancellationToken);
    }

    private IDataConnectionFactory ResolveFactory()
    {
        if (string.IsNullOrWhiteSpace(creds.TranslationsDbConnection))
            return dbFactory;

        lock (factoryLock)
        {
            return translationsFactory ??= new PostgreSqlConnectionFactory(creds.TranslationsDbConnection);
        }
    }

    private sealed class OverrideRow
    {
        public string Locale { get; set; } = "";
        public string Key { get; set; } = "";
        public string Text { get; set; } = "";
    }
}
