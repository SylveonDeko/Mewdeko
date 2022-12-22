using System.Threading.Tasks;
using Discord.Interactions;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Common.Autocompleters;

public class TimeZoneAutocompleter : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction interaction,
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