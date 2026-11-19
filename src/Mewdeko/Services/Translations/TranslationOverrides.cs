namespace Mewdeko.Services.Translations;

/// <summary>
///     Pure steps for applying approved translations over the strings loaded from files.
/// </summary>
public static class TranslationOverrides
{
    /// <summary>
    ///     Picks the overrides that can be applied to the given strings. A row is dropped when its locale is the source
    ///     language or is not loaded, when its key is not a source language key any more, or when its text is blank.
    /// </summary>
    /// <param name="loaded">The strings loaded from files, by locale and key.</param>
    /// <param name="rows">The approved translations.</param>
    /// <returns>The texts to write, by locale and key.</returns>
    public static Dictionary<string, Dictionary<string, string>> Resolve(
        IReadOnlyDictionary<string, Dictionary<string, string>> loaded,
        IEnumerable<TranslationOverride> rows)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        if (!loaded.TryGetValue(TranslationCatalog.SourceLocale, out var source))
            return result;

        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(row.Locale) || string.IsNullOrEmpty(row.Key) || string.IsNullOrWhiteSpace(row.Text))
                continue;

            if (row.Locale == TranslationCatalog.SourceLocale || !loaded.ContainsKey(row.Locale)
                                                              || !source.ContainsKey(row.Key))
                continue;

            if (!result.TryGetValue(row.Locale, out var locale))
            {
                locale = new Dictionary<string, string>(StringComparer.Ordinal);
                result[row.Locale] = locale;
            }

            locale[row.Key] = row.Text;
        }

        return result;
    }

    /// <summary>
    ///     Writes the applicable overrides into the loaded strings.
    /// </summary>
    /// <param name="loaded">The strings loaded from files, changed in place.</param>
    /// <param name="rows">The approved translations.</param>
    /// <returns>How many strings were replaced.</returns>
    public static int Apply(Dictionary<string, Dictionary<string, string>> loaded, IEnumerable<TranslationOverride> rows)
    {
        var count = 0;

        foreach (var (locale, entries) in Resolve(loaded, rows))
        {
            foreach (var (key, text) in entries)
            {
                loaded[locale][key] = text;
                count++;
            }
        }

        return count;
    }

    /// <summary>
    ///     Decides whether the overrides have to be applied again.
    /// </summary>
    /// <param name="applied">The version applied last, or null when none was.</param>
    /// <param name="current">The version stored now, or null when it could not be read.</param>
    /// <returns>True when the stored version is readable and differs from the applied one.</returns>
    public static bool VersionChanged(long? applied, long? current)
    {
        if (current is null)
            return false;

        return applied is null || applied.Value != current.Value;
    }
}
