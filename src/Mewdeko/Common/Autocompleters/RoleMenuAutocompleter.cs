using Discord.Interactions;
using Mewdeko.Modules.RoleMenus.Services;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for role menus.
/// </summary>
public class RoleMenuAutocompleter : AutocompleteHandler
{
    /// <summary>
    ///     Most suggestions Discord shows.
    /// </summary>
    private const int MaxSuggestions = 25;

    /// <summary>
    ///     Longest suggestion name Discord accepts.
    /// </summary>
    private const int MaxNameLength = 100;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RoleMenuAutocompleter" /> class.
    /// </summary>
    /// <param name="service">The role menu service.</param>
    public RoleMenuAutocompleter(RoleMenuService service)
    {
        Service = service;
    }

    /// <summary>
    ///     Gets the role menu service.
    /// </summary>
    private RoleMenuService Service { get; }

    /// <summary>
    ///     Suggests this server's menus whose names contain the input.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>The suggestions.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        if (autocompleteInteraction.User is not IGuildUser user || context.Guild is null)
            return AutocompletionResult.FromSuccess();

        if (!HasPermission(user))
            return AutocompletionResult.FromError(InteractionCommandError.Unsuccessful,
                "You need Manage Roles to use role menus.");

        var input = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";
        var guild = context.Guild as SocketGuild;
        var menus = await Service.GetMenusAsync(context.Guild.Id);

        var suggestions = menus
            .Where(x => x.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x.Id)
            .Take(MaxSuggestions)
            .Select(x =>
            {
                var channelName = guild?.GetChannel(x.ChannelId)?.Name ?? x.ChannelId.ToString();
                return new AutocompleteResult($"{x.Name} (#{channelName})".TrimTo(MaxNameLength), x.Id);
            });

        return AutocompletionResult.FromSuccess(suggestions);
    }

    /// <summary>
    ///     Checks whether a member may manage role menus.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <returns>True for the owner and members with Manage Roles or Administrator.</returns>
    private static bool HasPermission(IGuildUser user)
    {
        return user.Guild.OwnerId == user.Id ||
               user.GuildPermissions.Has(GuildPermission.ManageRoles) ||
               user.GuildPermissions.Has(GuildPermission.Administrator);
    }
}
