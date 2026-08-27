using Discord.Interactions;
using Mewdeko.Modules.StatChannels.Common;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for stat channel types. There are far more stat types than the 25 choices a slash command option
///     allows, so they are surfaced by search instead.
/// </summary>
public class StatChannelTypeAutocompleter : AutocompleteHandler
{
    private const int MaxSuggestions = 25;

    /// <summary>
    ///     Generates stat type suggestions filtered by name, category or description.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>The matching stat types.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        await Task.CompletedTask;
        var input = (autocompleteInteraction.Data.Current.Value as string ?? "").Trim();

        var matches = StatChannelDefinitions.All
            .Where(d => input.Length == 0 ||
                        d.Name.Contains(input, StringComparison.OrdinalIgnoreCase) ||
                        d.Category.Contains(input, StringComparison.OrdinalIgnoreCase) ||
                        d.Description.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSuggestions)
            .Select(d => new AutocompleteResult($"{d.Category}: {d.Name}", (int)d.Type));

        return AutocompletionResult.FromSuccess(matches);
    }
}