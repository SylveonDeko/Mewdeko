using Discord.Interactions;
using Mewdeko.Modules.CardDesigns.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for the card designs a member can equip.
/// </summary>
public class CardDesignAutocompleter : AutocompleteHandler
{
    /// <summary>
    ///     Generates suggestions for autocomplete.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="interaction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>The suggestions.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction interaction, IParameterInfo parameter, IServiceProvider services)
    {
        if (context.Guild is null)
            return AutocompletionResult.FromSuccess([]);

        var content = interaction.Data.Current.Value?.ToString() ?? "";
        var available = await services.GetRequiredService<CardDesignService>()
            .GetAvailableAsync(context.Guild.Id, context.User.Id);

        return AutocompletionResult.FromSuccess(available
            .Select(a => a.Design.Name)
            .Where(name => name.Contains(content, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(name => new AutocompleteResult(name, name)));
    }
}
