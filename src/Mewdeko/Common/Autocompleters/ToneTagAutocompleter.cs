using Discord.Interactions;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
/// Autocompleter for tone tags.
/// </summary>
public class ToneTagAutocompleter : AutocompleteHandler
{
    /// <summary>
    /// Generates suggestions for autocomplete.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="inter">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the autocomplete result.</returns>
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction inter,
        IParameterInfo parameter,
        IServiceProvider services) =>
        Task.FromResult(AutocompletionResult.FromSuccess((services.GetService(typeof(ToneTagService)) as ToneTagService)
            .Tags.SelectMany(x => x.GetAllValues()).Select(x => '/' + x)
            .Where(x => x.Contains(inter.Data.Current.Value as string,
                StringComparison.InvariantCultureIgnoreCase))
            .OrderByDescending(x =>
                x.StartsWith(inter.Data.Current.Value as string,
                    StringComparison.InvariantCultureIgnoreCase)).Take(25)
            .Select(x => new AutocompleteResult(x, x))));
}