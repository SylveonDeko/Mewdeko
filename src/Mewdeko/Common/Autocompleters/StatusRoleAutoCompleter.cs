using Discord.Interactions;

namespace Mewdeko.Common.Autocompleters;

public class StatusRoleAutocompleter : AutocompleteHandler
{
    public StatusRoleAutocompleter(IDataCache cache) => this.cache = cache;

    private readonly IDataCache cache;

    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction interaction, IParameterInfo parameter,
        IServiceProvider services)
    {
        var content = (string)interaction.Data.Current.Value;
        var statusRoles = await cache.GetStatusRoleCache();

        if (statusRoles == null)
            return AutocompletionResult.FromSuccess(Enumerable.Empty<AutocompleteResult>());

        return AutocompletionResult.FromSuccess(statusRoles
            .Where(x => x.GuildId == context.Guild.Id)
            .Select(x => x.Status)
            .Where(x => x.Contains(content))
            .Take(20)
            .Select(x => new AutocompleteResult(x, x)));
    }
}