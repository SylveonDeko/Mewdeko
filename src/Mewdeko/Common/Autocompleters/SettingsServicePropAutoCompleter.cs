using Discord.Interactions;
using Mewdeko.Services.Settings;

namespace Mewdeko.Common.Autocompleters;

public class SettingsServicePropAutoCompleter : AutocompleteHandler
{
    private readonly IEnumerable<IConfigService> settingServices;

    public SettingsServicePropAutoCompleter(IEnumerable<IConfigService> settingServices)
    {
        this.settingServices = settingServices;
    }

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter,
        IServiceProvider services)
    {
        var firstOption = autocompleteInteraction.Data.Options.FirstOrDefault(x => x.Name == "name");
        var setting = settingServices.FirstOrDefault(x => x.Name == (string)firstOption.Value);
        if (setting is null)
            return Task.FromResult(AutocompletionResult.FromSuccess(Enumerable.Empty<AutocompleteResult>()));
        {
            var propNames = GetPropsAndValuesString(setting, setting.GetSettableProps());
            var dict = setting.GetSettableProps().Zip(propNames).ToDictionary(x => x.First, x => x.Second);
            var results = dict.Where(x => x.Key.Contains((string)autocompleteInteraction.Data.Current.Value))
                .Select(x => new AutocompleteResult($"{x.Key} | {x.Value}".TrimTo(100), x.Key));
            return Task.FromResult(AutocompletionResult.FromSuccess(results));
        }
    }

    private static IEnumerable<string> GetPropsAndValuesString(IConfigService config, IEnumerable<string> names)
    {
        var propValues = names.Select(pr =>
        {
            var val = config.GetSetting(pr);
            if (pr != "currency.sign")
                val = val.TrimTo(40);
            return val?.Replace("\n", "") ?? "-";
        });
        return propValues;
    }
}