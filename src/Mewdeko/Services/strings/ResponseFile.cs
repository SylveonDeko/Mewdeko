namespace Mewdeko.Services.strings;

/// <summary>
///     The response strings of one source file, before the files of a locale are merged.
/// </summary>
/// <param name="Locale">The locale the file belongs to, for example en-US.</param>
/// <param name="Group">The file name without its extension. Empty for a legacy single-file locale.</param>
/// <param name="Strings">The strings in the file, by key.</param>
public sealed record ResponseFile(string Locale, string Group, IReadOnlyDictionary<string, string> Strings);
