using Discord.Commands;
using Discord.Interactions;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.strings;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for command names. Offers both text command names and slash command paths; a slash path
///     resolves to the text command identity the permission system is keyed on, so either form can be used.
/// </summary>
/// <param name="commands">The text command service.</param>
/// <param name="interactions">The interaction service holding the registered slash commands.</param>
/// <param name="identity">The resolver mapping slash commands to text command identities.</param>
/// <param name="perms">The global permission service.</param>
/// <param name="strings">The bot strings.</param>
/// <param name="guildSettings">The guild settings service.</param>
public class GenericCommandAutocompleter(
    CommandService commands,
    InteractionService interactions,
    SlashCommandIdentityService identity,
    GlobalPermissionService perms,
    IBotStrings strings,
    GuildSettingsService guildSettings) : AutocompleteHandler
{
    /// <summary>
    ///     Generates suggestions for autocomplete.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>The autocomplete result.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter,
        IServiceProvider services)
    {
        var prefix = await guildSettings.GetPrefix(context.Guild).ConfigureAwait(false);
        var input = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        var textEntries = commands.Commands
            .Where(c => !perms.BlockedCommands.Contains(c.Aliases[0].ToLowerInvariant()))
            .Select(x => (Display: $"{x.Name} : {x.RealSummary(strings, context.Guild?.Id, prefix)}",
                Value: x.Name));

        var slashEntries = interactions.SlashCommands
            .Select(x => (Path: "/" + string.Join(' ', SlashCommandIdentityService.GetPath(x)),
                Identity: identity.Resolve(x)))
            .Where(x => !perms.BlockedCommands.Contains(x.Identity.Alias))
            .Select(x => (Display: $"{x.Path} : {x.Identity.Alias}", Value: x.Identity.Alias));

        var results = textEntries.Concat(slashEntries)
            .Where(x => x.Display.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Display.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.Display.StartsWith("/" + input, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(x => x.Display)
            .Take(20)
            .Select(x => new AutocompleteResult(
                x.Display.Length >= 100 ? x.Display[..97] + "..." : x.Display, x.Value));

        return AutocompletionResult.FromSuccess(results);
    }
}