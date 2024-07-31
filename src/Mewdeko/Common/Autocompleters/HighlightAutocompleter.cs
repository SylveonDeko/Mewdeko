using Discord.Interactions;
using ZiggyCreatures.Caching.Fusion;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
/// Autocompleter for highlights.
/// </summary>
public class HighlightAutocompleter : AutocompleteHandler
{
    /// <summary>
    /// Initializes a new instance of the HighlightAutocompleter class.
    /// </summary>
    /// <param name="cache">The FusionCache instance.</param>
    public HighlightAutocompleter(IFusionCache cache)
    {
        this.cache = cache;
    }

    /// <summary>
    /// Gets the FusionCache instance.
    /// </summary>
    private readonly IFusionCache cache;

    /// <summary>
    /// Generates suggestions for autocomplete.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="interaction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the autocomplete result.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction interaction, IParameterInfo parameter,
        IServiceProvider services)
    {
        var content = (string)interaction.Data.Current.Value;
        var highlights = await cache.GetOrSetAsync<List<Database.Models.Highlights>>($"highlights_{context.Guild.Id}",
            async _ => []);

        var results = highlights
            .Where(x => x.UserId == context.User.Id && x.GuildId == context.Guild.Id)
            .Select(x => x.Word.ToLower().Replace("\n", " | "))
            .Where(x => x.Contains(content.ToLower().Replace("\n", " | ")))
            .Take(20)
            .Select(x => new AutocompleteResult(x, x.Replace(" | ", "\n")))
            .ToList();

        return AutocompletionResult.FromSuccess(results);
    }
}