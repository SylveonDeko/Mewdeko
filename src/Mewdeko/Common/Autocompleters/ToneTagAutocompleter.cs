using System.Threading.Tasks;
using Discord.Interactions;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Common.Autocompleters;

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
                    StringComparison.InvariantCultureIgnoreCase)).Take(25)
            .Select(x => new AutocompleteResult(x, x))));
}