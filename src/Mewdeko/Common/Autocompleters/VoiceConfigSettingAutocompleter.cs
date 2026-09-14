using Discord.Interactions;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for the custom voice settings that can be changed with the voice admin config command.
/// </summary>
public class VoiceConfigSettingAutocompleter : AutocompleteHandler
{
    private const int MaxSuggestions = 25;

    /// <summary>
    ///     Every setting key the voice config command understands.
    /// </summary>
    public static readonly string[] Keys =
    [
        "nameformat", "userlimit", "bitrate", "deleteempty", "emptytimeout", "multiplechannels",
        "namechange", "limitchange", "bitratechange", "locking", "usermanagement",
        "maxuserlimit", "maxbitrate", "persistpreferences", "autopermission", "adminrole"
    ];

    /// <summary>
    ///     Generates setting key suggestions filtered by the current input.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>The matching setting keys.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        await Task.CompletedTask;
        var input = (autocompleteInteraction.Data.Current.Value as string ?? "").Trim();

        var matches = Keys
            .Where(k => input.Length == 0 || k.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSuggestions)
            .Select(k => new AutocompleteResult(k, k));

        return AutocompletionResult.FromSuccess(matches);
    }
}