using Discord.Interactions;
using Mewdeko.Modules.Suggestions.Services;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for suggestions.
/// </summary>
public class SuggestionAutocompleter : AutocompleteHandler
{
    /// <summary>
    ///     Gets the SuggestionsService.
    /// </summary>
    private readonly SuggestionsService suggest;

    /// <summary>
    ///     Initializes a new instance of the SuggestionAutocompleter class.
    /// </summary>
    /// <param name="suggest">The SuggestionsService.</param>
    public SuggestionAutocompleter(SuggestionsService suggest)
    {
        this.suggest = suggest;
    }

    /// <summary>
    ///     Generates suggestions for autocomplete.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="interaction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the autocomplete result.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction interaction, IParameterInfo parameter, IServiceProvider services)
    {
        var content = (string)interaction.Data.Current.Value;
        var suggestions = await suggest.Suggestions(context.Guild?.Id ?? 0);

        return AutocompletionResult.FromSuccess(suggestions
            .Where(x => x.Suggestion.Contains(content) || x.SuggestionId.ToString().Contains(content))
            .OrderByDescending(x => x.Suggestion.StartsWith(content))
            .ThenByDescending(x => x.SuggestionId.ToString().StartsWith(content))
            .Select(CreateAutocompleteResult));
    }

    /// <summary>
    ///     Creates an autocomplete result from a suggestion model.
    /// </summary>
    /// <param name="x">The suggestion model.</param>
    /// <returns>The autocomplete result.</returns>
    private static AutocompleteResult CreateAutocompleteResult(SuggestionsModel x)
    {
        var formattedResult = $"{x.SuggestionId} | {x.Suggestion}".TrimTo(100);
        return new AutocompleteResult(formattedResult, x.SuggestionId);
    }
}