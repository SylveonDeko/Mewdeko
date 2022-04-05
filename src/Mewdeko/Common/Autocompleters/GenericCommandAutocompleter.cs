using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Mewdeko;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Common.Attributes;

public class GenericCommandAutocompleter : AutocompleteHandler
{
    private CommandService _commands { get; set; }
    private GlobalPermissionService _perms { get; set; }
    public GenericCommandAutocompleter(CommandService commands, GlobalPermissionService perms)
    {
        _commands = commands;
        _perms = perms;
    }
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    => Task.FromResult(AutocompletionResult
            .FromSuccess(_commands.Commands
            .Where(c => !_perms.BlockedCommands.Contains(c.Aliases[0].ToLowerInvariant()))
            .Select(x => x.Name)
            .Concat(_commands.Commands.SelectMany(x => x.Aliases))
            .Where(x => x.Contains((string)autocompleteInteraction.Data.Current.Value, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.StartsWith((string)autocompleteInteraction.Data.Current.Value, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Take(20)
            .Select(x => new AutocompleteResult(x, x))));
}
