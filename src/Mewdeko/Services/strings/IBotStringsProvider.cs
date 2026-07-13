using Mewdeko.Services.strings.impl;

namespace Mewdeko.Services.strings;

/// <summary>
///     Implemented by classes which provide localized strings in their own ways
/// </summary>
public interface IBotStringsProvider
{
    /// <summary>
    ///     Gets localized string
    /// </summary>
    /// <param name="localeName">Language name</param>
    /// <param name="key">String key</param>
    /// <returns>Localized string</returns>
    public string? GetText(string localeName, string? key);

    /// <summary>
    ///     Reloads string cache
    /// </summary>
    public void Reload();

    /// <summary>
    ///     Gets command arg examples and description
    /// </summary>
    /// <param name="localeName">Language name</param>
    /// <param name="commandName">Command name</param>
    public CommandStrings? GetCommandStrings(string localeName, string commandName);

    /// <summary>
    ///     Gets overloads for commands, if any.
    /// </summary>
    /// <param name="lang"></param>
    /// <param name="commandName"></param>
    /// <returns></returns>
    public List<CommandOverload> GetCommandOverloads(string lang, string commandName);

    /// <summary>
    ///     Gets the set of locale names that have response strings loaded (e.g. "en-US", "ar").
    /// </summary>
    public IReadOnlyCollection<string> GetAvailableLocales();
}