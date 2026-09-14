using Discord.Interactions;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for the economy settings that can be changed with the currency admin set command.
/// </summary>
public class EconomySettingAutocompleter : AutocompleteHandler
{
    private const int MaxSuggestions = 25;

    /// <summary>
    ///     Every setting key the economy set command understands.
    /// </summary>
    public static readonly string[] Keys =
    [
        "gambling", "minbet", "maxbet", "payoutmultiplier", "gamecooldown", "losslimit",
        "pay", "paytax", "paycooldown", "payminimum",
        "bank", "bankcapacity", "bankinterest", "bankinteresthours",
        "work", "workmin", "workmax", "workcooldown",
        "crime", "crimemin", "crimemax", "crimechance", "crimefinemin", "crimefinemax", "crimecooldown",
        "rob", "robchance", "robmaxsteal", "robfine", "robminimum", "robcooldown",
        "dailystreak", "dailystreakbonus", "dailystreakmax"
    ];

    /// <summary>
    ///     Generates setting key suggestions filtered by the current input.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>The matching setting keys.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        await Task.CompletedTask;
        var input = (autocompleteInteraction.Data.Current.Value as string ?? "").Trim();

        var matches = Keys
            .Where(k => input.Length == 0 || k.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSuggestions)
            .Select(k => new AutocompleteResult(k, k));

        return AutocompletionResult.FromSuccess(matches);
    }
}