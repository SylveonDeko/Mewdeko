using Discord.Interactions;
using Mewdeko.Services.Settings;

namespace Mewdeko.Common.Autocompleters;

public class SettingsServiceNameAutoCompleter : AutocompleteHandler
{
    private readonly IEnumerable<IConfigService> settingServices;

    public SettingsServiceNameAutoCompleter(IEnumerable<IConfigService> settingServices)
    {
        this.settingServices = settingServices;
    }

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter,
        IServiceProvider services)
    {
        return Task.FromResult(AutocompletionResult.FromSuccess(settingServices.Where(x => x.Name.Contains((string)autocompleteInteraction.Data.Current.Value))
            .Select(x => new AutocompleteResult(x.Name, x.Name))));
    }
}