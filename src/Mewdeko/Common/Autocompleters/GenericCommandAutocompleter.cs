using Discord;
using Discord.Commands;
using Discord.Interactions;
using Mewdeko.Extensions;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.strings;

namespace Mewdeko.Common.Autocompleters;

public class GenericCommandAutocompleter : AutocompleteHandler
{
    private CommandService _commands { get; set; }
    private CommandHandler _commandHandler { get; set; }
    private GlobalPermissionService _perms { get; set; }
    private IBotStrings _strings { get; set; }
    public GenericCommandAutocompleter(CommandService commands, GlobalPermissionService perms, IBotStrings strings, CommandHandler commandHandler)
    {
        _commands = commands;
        _perms = perms;
        _strings = strings;
        _commandHandler = commandHandler;
    }

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    => Task.FromResult(AutocompletionResult
            .FromSuccess(_commands.Commands
            .Where(c => !_perms.BlockedCommands.Contains(c.Aliases[0].ToLowerInvariant()))
            .Select(x => $"{x.Name} : {x.RealSummary(_strings, context.Guild?.Id, _commandHandler.GetPrefix(context.Guild?.Id))}")
            .Concat(_commands.Commands
                .SelectMany(x => x.Aliases.Select(a => $"{a} : {x.RealSummary(_strings, context.Guild?.Id, _commandHandler.GetPrefix(context.Guild?.Id))}")))
            .Where(x => x.Contains((string)autocompleteInteraction.Data.Current.Value, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.StartsWith((string)autocompleteInteraction.Data.Current.Value, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Take(20)
            .Select(x => new AutocompleteResult(x.Length >= 100 ? x[..97] + "..." : x, x.Split(':')[0].Trim()))));
}
