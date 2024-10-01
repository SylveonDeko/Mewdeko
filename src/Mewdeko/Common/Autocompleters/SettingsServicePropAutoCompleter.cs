using Discord.Interactions;
using Mewdeko.Services.Settings;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for settings service properties.
/// </summary>
public class SettingsServicePropAutoCompleter : AutocompleteHandler
{
    /// <summary>
    ///     Gets the collection of setting services.
    /// </summary>
    private readonly IEnumerable<IConfigService> settingServices;

    /// <summary>
    ///     Initializes a new instance of the SettingsServicePropAutoCompleter class.
    /// </summary>
    /// <param name="settingServices">The collection of setting services.</param>
    public SettingsServicePropAutoCompleter(IEnumerable<IConfigService> settingServices)
    {
        this.settingServices = settingServices;
    }

    /// <summary>
    ///     Generates suggestions for autocomplete.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the autocomplete result.</returns>
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter,
        IServiceProvider services)
    {
        var firstOption = autocompleteInteraction.Data.Options.FirstOrDefault(x => x.Name == "name");
        var setting = settingServices.FirstOrDefault(x => x.Name == (string)firstOption.Value);
        if (setting is null)
            return Task.FromResult(AutocompletionResult.FromSuccess([]));
        {
            var propNames = GetPropsAndValuesString(setting, setting.GetSettableProps());
            var dict = setting.GetSettableProps().Zip(propNames).ToDictionary(x => x.First, x => x.Second);
            var results = dict.Where(x => x.Key.Contains((string)autocompleteInteraction.Data.Current.Value))
                .Select(x => new AutocompleteResult($"{x.Key} | {x.Value}".TrimTo(100), x.Key));
            return Task.FromResult(AutocompletionResult.FromSuccess(results));
        }
    }

    /// <summary>
    ///     Gets the properties and their values as strings.
    /// </summary>
    /// <param name="config">The configuration service.</param>
    /// <param name="names">The names of the properties.</param>
    /// <returns>The properties and their values as strings.</returns>
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