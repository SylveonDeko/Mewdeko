using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Mewdeko.Modules.Searches.Services;

public class ToneTagAutocompleter : AutocompleteHandler
{
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
                                                                 StringComparison.InvariantCultureIgnoreCase)).Take(20)
                                                         .Select(x => new AutocompleteResult(x, x))));
}