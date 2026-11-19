namespace Mewdeko.Services.Translations;

/// <summary>
///     An approved translation that replaces the file text of one string in one locale.
/// </summary>
/// <param name="Locale">The locale the translation is for.</param>
/// <param name="Key">The string key.</param>
/// <param name="Text">The translated text.</param>
public sealed record TranslationOverride(string Locale, string Key, string Text);
