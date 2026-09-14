using System.Globalization;
using Discord.Interactions;
using Mewdeko.Services.strings;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for the locales the bot has string data for.
/// </summary>
/// <param name="stringsProvider">The strings provider used to look up available locales.</param>
public class CultureAutocompleter(IBotStringsProvider stringsProvider) : AutocompleteHandler
{
    /// <summary>
    ///     Generates suggestions for autocomplete.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="interaction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the autocomplete result.</returns>
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter, IServiceProvider services)
    {
        var val = interaction.Data.Current.Value?.ToString() ?? "";
        var locales = stringsProvider.GetAvailableLocales()
            .Select(x =>
            {
                string display;
                try
                {
                    display = $"{x} ({new CultureInfo(x).NativeName})";
                }
                catch (CultureNotFoundException)
                {
                    display = x;
                }

                return (Name: x, Display: display);
            })
            .Prepend((Name: "default", Display: "default"))
            .Where(x => x.Display.Contains(val, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Name.StartsWith(val, StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x.Name)
            .Take(25)
            .Select(x => new AutocompleteResult(x.Display.Length > 100 ? x.Display[..97] + "..." : x.Display,
                x.Name));
        return Task.FromResult(AutocompletionResult.FromSuccess(locales));
    }
}