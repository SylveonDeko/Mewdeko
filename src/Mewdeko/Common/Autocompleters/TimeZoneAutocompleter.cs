using Discord.Interactions;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
/// Autocompleter for time zones.
/// </summary>
public class TimeZoneAutocompleter : AutocompleteHandler
{
    /// <summary>
    /// Generates suggestions for autocomplete.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="interaction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the autocomplete result.</returns>
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter, IServiceProvider services)
    {
        var val = interaction.Data.Current.Value.ToString() ?? "";
        var timezones = TimeZoneInfo.GetSystemTimeZones()
            .Select(x => x.Id)
            .Where(x => x
                .Contains(val, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x
                .StartsWith(val, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(x => new AutocompleteResult(x, x));
        return Task.FromResult(AutocompletionResult.FromSuccess(timezones));
    }
}