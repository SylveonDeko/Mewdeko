using Discord.Interactions;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for permissions.
/// </summary>
public class PermissionAutoCompleter : AutocompleteHandler
{
    /// <summary>
    ///     Initializes a new instance of the PermissionAutoCompleter class.
    /// </summary>
    /// <param name="perms">The PermissionService.</param>
    public PermissionAutoCompleter(PermissionService perms)
    {
        Perms = perms;
    }

    /// <summary>
    ///     Gets the PermissionService.
    /// </summary>
    private PermissionService Perms { get; }

    /// <summary>
    ///     Generates suggestions for autocomplete.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the autocomplete result.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        var cache = await Perms.GetCacheFor(context.Guild.Id);
        var perms = cache.Permissions.Source;
        return AutocompletionResult.FromSuccess(perms
            .Select(x => $"{x.Index}: {x.GetCommand("/", (SocketGuild)context.Guild)}").Take(20)
            .Where(x => x.Contains((string)autocompleteInteraction.Data.Current.Value)).Select(x =>
                new AutocompleteResult(x.Length >= 100 ? x[..97] + "..." : x, x.Split(':')[0].Trim())));
    }
}