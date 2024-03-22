using Discord.Interactions;
using Mewdeko.Services.Settings;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
/// Autocompleter for settings service names.
/// </summary>
public class SettingsServiceNameAutoCompleter : AutocompleteHandler
{
    /// <summary>
    /// Gets the collection of setting services.
    /// </summary>
    private readonly IEnumerable<IConfigService> settingServices;

    /// <summary>
    /// Initializes a new instance of the SettingsServiceNameAutoCompleter class.
    /// </summary>
    /// <param name="settingServices">The collection of setting services.</param>
    public SettingsServiceNameAutoCompleter(IEnumerable<IConfigService> settingServices)
    {
        this.settingServices = settingServices;
    }

    /// <summary>
    /// Generates suggestions for autocomplete.
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
        return Task.FromResult(AutocompletionResult.FromSuccess(settingServices
            .Where(x => x.Name.Contains((string)autocompleteInteraction.Data.Current.Value))
            .Select(x => new AutocompleteResult(x.Name, x.Name))));
    }
}