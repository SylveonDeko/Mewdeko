namespace Mewdeko.Services.strings.impl;

/// <summary>
///     Represents a provider for local bot strings.
/// </summary>
public class LocalBotStringsProvider : IBotStringsProvider
{
    private readonly IStringsSource source;
    private IReadOnlyDictionary<string, Dictionary<string, CommandStrings>> commandStrings;
    private IReadOnlyDictionary<string, Dictionary<string, string>> responseStrings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LocalBotStringsProvider" /> class.
    /// </summary>
    /// <param name="source">The source for bot strings.</param>
    public LocalBotStringsProvider(IStringsSource source)
    {
        this.source = source;
        Reload();
    }

    /// <summary>
    ///     Gets the text corresponding to the provided key and locale name.
    /// </summary>
    /// <param name="localeName">The name of the locale.</param>
    /// <param name="key">The key for the desired text.</param>
    /// <returns>The text corresponding to the key and locale name.</returns>
    public string? GetText(string localeName, string? key)
    {
        if (responseStrings.TryGetValue(localeName, out var langStrings)
            && langStrings.TryGetValue(key, out var text))
        {
            return text;
        }

        return null;
    }

    /// <summary>
    ///     Reloads the bot strings from the source.
    /// </summary>
    public void Reload()
    {
        responseStrings = source.GetResponseStrings();
        commandStrings = source.GetCommandStrings();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetAvailableLocales()
    {
        return responseStrings.Keys.ToArray();
    }

    /// <summary>
    ///     Gets the command strings for the specified locale and command name.
    /// </summary>
    /// <param name="localeName">The name of the locale.</param>
    /// <param name="commandName">The name of the command.</param>
    /// <returns>The command strings for the specified locale and command name.</returns>
    public CommandStrings? GetCommandStrings(string localeName, string commandName)
    {
        if (commandStrings.TryGetValue(localeName, out var langStrings)
            && langStrings.TryGetValue(commandName, out var strings))
        {
            return strings;
        }

        return null;
    }

    /// <summary>
    ///     Gets overloaded versions of a command for the specified locale and command name.
    /// </summary>
    /// <param name="localeName">The name of the locale.</param>
    /// <param name="commandName">The base name of the command.</param>
    /// <returns>A list of overloaded versions of the command.</returns>
    public List<CommandOverload> GetCommandOverloads(string localeName, string commandName)
    {
        var overloads = new List<CommandOverload>();

        if (!commandStrings.TryGetValue(localeName, out var langStrings))
            return overloads;

        var baseCommandKey = commandName.ToLowerInvariant();
        var index = 0;

        while (true)
        {
            var overloadKey = $"{baseCommandKey}_overload_{index}";
            if (!langStrings.TryGetValue(overloadKey, out var overloadString))
                break;

            // Convert CommandStrings to CommandOverload
            overloads.Add(new CommandOverload
            {
                Desc = overloadString.Desc,
                Args = overloadString.Args,
                Parameters = overloadString.Parameters?.ToList(),
                Signature = overloadString.Signature
            });

            index++;
        }

        return overloads;
    }
}