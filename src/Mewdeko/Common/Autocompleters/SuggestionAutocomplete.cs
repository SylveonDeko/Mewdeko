using Discord.Interactions;
using Mewdeko.Modules.Suggestions.Services;

namespace Mewdeko.Common.Autocompleters;

public class SuggestionAutocompleter : AutocompleteHandler
{
    private readonly SuggestionsService suggest;

    public SuggestionAutocompleter(SuggestionsService suggest)
        => this.suggest = suggest;

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction interaction, IParameterInfo parameter, IServiceProvider services)
    {
        var content = (string)interaction.Data.Current.Value;
        var suggestions = suggest.Suggestions(context.Guild?.Id ?? 0);

        return Task.FromResult(AutocompletionResult.FromSuccess(suggestions
            .Where(x => x.Suggestion.Contains(content) || x.SuggestionId.ToString().Contains(content))
            .OrderByDescending(x => x.Suggestion.StartsWith(content))
            .ThenByDescending(x => x.SuggestionId.ToString().StartsWith(content))
            .Select(CreateAutocompleteResult)));
    }

    private static AutocompleteResult CreateAutocompleteResult(SuggestionsModel x)
    {
        var formattedResult = $"{x.SuggestionId} | {x.Suggestion}".TrimTo(100);
        return new AutocompleteResult(formattedResult, x.SuggestionId);
    }
}