using Discord.Interactions;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
/// Autocompleter for highlights.
/// </summary>
public class HighlightAutocompleter : AutocompleteHandler
{
    /// <summary>
    /// Initializes a new instance of the HighlightAutocompleter class.
    /// </summary>
    /// <param name="cache">The data cache.</param>
    public HighlightAutocompleter(IDataCache cache)
    {
        this.cache = cache;
    }

    /// <summary>
    /// Gets the data cache.
    /// </summary>
    private readonly IDataCache cache;

    /// <summary>
    /// Generates suggestions for autocomplete.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="interaction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the autocomplete result.</returns>
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction interaction, IParameterInfo parameter,
        IServiceProvider services)
    {
        var content = (string)interaction.Data.Current.Value;
        var highlights = cache.GetHighlightsForGuild(context.Guild.Id);

        return Task.FromResult(AutocompletionResult.FromSuccess(highlights
            .Where(x => x.UserId == context.User.Id && x.GuildId == context.Guild.Id)
            .Select(x => x.Word = x.Word.ToLower().Replace("\n", " | "))
            .Where(x => x.Contains(content.ToLower().Replace("\n", " | ")))
            .Take(20)
            .Select(x => new AutocompleteResult(x, x.Replace(" | ", "\n")))));
    }
}