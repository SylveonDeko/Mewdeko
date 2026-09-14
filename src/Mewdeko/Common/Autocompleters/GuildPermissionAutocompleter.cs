using Discord.Interactions;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Provides autocomplete suggestions for Discord guild permission names.
/// </summary>
public class GuildPermissionAutocompleter : AutocompleteHandler
{
    /// <inheritdoc />
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var current = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;
        var results = Enum.GetNames<GuildPermission>()
            .Where(x => x.Contains(current, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.StartsWith(current, StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x)
            .Take(25)
            .Select(x => new AutocompleteResult(x, x));
        return Task.FromResult(AutocompletionResult.FromSuccess(results));
    }
}