using Discord.Commands;
using Discord.Interactions;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for modules.
/// </summary>
public class ModuleAutoCompleter : AutocompleteHandler
{
    /// <summary>
    ///     Initializes a new instance of the ModuleAutoCompleter class.
    /// </summary>
    /// <param name="commands">The CommandService.</param>
    /// <param name="perms">The GlobalPermissionService.</param>
    public ModuleAutoCompleter(CommandService commands, GlobalPermissionService perms)
    {
        Commands = commands;
        Perms = perms;
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
        return Task.FromResult(AutocompletionResult.FromSuccess(Commands.Modules
            .Where(c => !Perms.BlockedModules.Contains(c.Aliases[0].ToLowerInvariant()))
            .Where(x => !x.IsSubmodule)
            .Select(x => $"{x.Name}")
            .Where(x => x.Contains((string)autocompleteInteraction.Data.Current.Value,
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x =>
                x.StartsWith((string)autocompleteInteraction.Data.Current.Value, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Take(20)
            .Select(x => new AutocompleteResult(x.Length >= 100 ? x[..97] + "..." : x, x.Split(':')[0].Trim()))));
    }
}