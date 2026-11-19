using System.Security.Cryptography;
using System.Text;
using Mewdeko.Services.strings;

namespace Mewdeko.Services.Translations;

/// <summary>
///     One string of the catalog: the source language text and the group it is listed under.
/// </summary>
/// <param name="Key">The string key, exactly as the bot looks it up.</param>
/// <param name="Group">The file name without extension the key was read from.</param>
/// <param name="SourceText">The source language text.</param>
public sealed record CatalogString(string Key, string Group, string SourceText);

/// <summary>
///     The text a locale other than the source language ships with for a key.
/// </summary>
/// <param name="Key">The string key.</param>
/// <param name="Locale">The locale.</param>
/// <param name="Text">The text from the locale's files.</param>
public sealed record CatalogBaseline(string Key, string Locale, string Text);

/// <summary>
///     Everything registered for the bot catalog, with the hash that identifies it.
/// </summary>
/// <param name="Strings">The strings, ordered by key.</param>
/// <param name="Baselines">The baselines, ordered by locale then key.</param>
/// <param name="Hash">The sha256 of the strings and baselines as lowercase hex.</param>
public sealed record CatalogSnapshot(
    IReadOnlyList<CatalogString> Strings,
    IReadOnlyList<CatalogBaseline> Baselines,
    string Hash);

/// <summary>
///     Builds the bot's translation catalog from its response files.
/// </summary>
public static class TranslationCatalog
{
    /// <summary>
    ///     The name of the bot's catalog.
    /// </summary>
    public const string Name = "bot";

    /// <summary>
    ///     The source language. It is never translated in the tool.
    /// </summary>
    public const string SourceLocale = "en-US";

    /// <summary>
    ///     Builds the catalog. Keys and groups come from the source language files. A key that appears in more than one
    ///     file of a locale takes its text, and for the source language its group, from the file whose name sorts last
    ///     by ordinal comparison, which is the file the bot's own loader lets win.
    /// </summary>
    /// <param name="files">The response files, in any order.</param>
    /// <returns>The strings, the baselines and their hash.</returns>
    public static CatalogSnapshot Build(IEnumerable<ResponseFile> files)
    {
        var resolved = new Dictionary<string, Dictionary<string, (string Group, string Text)>>(StringComparer.Ordinal);

        foreach (var file in files.OrderBy(x => x.Locale, StringComparer.Ordinal)
                     .ThenBy(x => x.Group, StringComparer.Ordinal))
        {
            if (!resolved.TryGetValue(file.Locale, out var locale))
            {
                locale = new Dictionary<string, (string Group, string Text)>(StringComparer.Ordinal);
                resolved[file.Locale] = locale;
            }

            foreach (var (key, text) in file.Strings)
            {
                if (text is not null)
                    locale[key] = (file.Group, text);
            }
        }

        if (!resolved.TryGetValue(SourceLocale, out var source))
            return new CatalogSnapshot([], [], ComputeHash([], []));

        var strings = source
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new CatalogString(x.Key, x.Value.Group, x.Value.Text))
            .ToList();

        var baselines = new List<CatalogBaseline>();
        foreach (var (locale, entries) in resolved.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (locale == SourceLocale)
                continue;

            baselines.AddRange(entries
                .Where(x => source.ContainsKey(x.Key))
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new CatalogBaseline(x.Key, locale, x.Value.Text)));
        }

        return new CatalogSnapshot(strings, baselines, ComputeHash(strings, baselines));
    }

    /// <summary>
    ///     Computes the sha256 over the strings and baselines in the order given. Every field is length prefixed so no
    ///     two different catalogs produce the same input.
    /// </summary>
    /// <param name="strings">The strings, ordered by key.</param>
    /// <param name="baselines">The baselines, ordered by locale then key.</param>
    /// <returns>The hash as lowercase hex.</returns>
    public static string ComputeHash(IReadOnlyList<CatalogString> strings, IReadOnlyList<CatalogBaseline> baselines)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "mewdeko-translations-v1");
        Append(hash, strings.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));

        foreach (var item in strings)
        {
            Append(hash, item.Key);
            Append(hash, item.Group);
            Append(hash, item.SourceText);
        }

        Append(hash, baselines.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));

        foreach (var item in baselines)
        {
            Append(hash, item.Locale);
            Append(hash, item.Key);
            Append(hash, item.Text);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
