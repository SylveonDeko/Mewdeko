using Discord.Commands;
using Discord.Interactions;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.strings;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for generic commands.
/// </summary>
public class GenericCommandAutocompleter : AutocompleteHandler
{
    /// <summary>
    ///     Gets the GuildSettingsService.
    /// </summary>
    private readonly GuildSettingsService guildSettings;

    /// <summary>
    ///     Initializes a new instance of the GenericCommandAutocompleter class.
    /// </summary>
    /// <param name="commands">The CommandService.</param>
    /// <param name="perms">The GlobalPermissionService.</param>
    /// <param name="strings">The IBotStrings.</param>
    /// <param name="guildSettings">The GuildSettingsService.</param>
    public GenericCommandAutocompleter(CommandService commands, GlobalPermissionService perms, IBotStrings strings,
        GuildSettingsService guildSettings)
    {
        Commands = commands;
        Perms = perms;
        Strings = strings;
        this.guildSettings = guildSettings;
    }

    /// <summary>
    ///     Gets the CommandService.
    /// </summary>
    private CommandService Commands { get; }

    /// <summary>
    ///     Gets the GlobalPermissionService.
    /// </summary>
    private GlobalPermissionService Perms { get; }

    /// <summary>
    ///     Gets the IBotStrings.
    /// </summary>
    private IBotStrings Strings { get; }

    /// <summary>
    ///     Generates suggestions for autocomplete.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the autocomplete result.</returns>
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter,
        IServiceProvider services)
    {
        return Task.FromResult(AutocompletionResult.FromSuccess(Commands.Commands
            .Where(c => !Perms.BlockedCommands.Contains(c.Aliases[0].ToLowerInvariant()))
            .Select(x =>
                $"{x.Name} : {x.RealSummary(Strings, context.Guild?.Id, guildSettings.GetPrefix(context.Guild.Id).GetAwaiter().GetResult())}")
            .Where(x => x.Contains((string)autocompleteInteraction.Data.Current.Value,
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x =>
                x.StartsWith((string)autocompleteInteraction.Data.Current.Value, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Take(20)
            .Select(x => new AutocompleteResult(x.Length >= 100 ? x[..97] + "..." : x, x.Split(':')[0].Trim()))));
    }
}