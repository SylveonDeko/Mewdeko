using Discord;
using Discord.Commands;
using Discord.Interactions;
using Mewdeko.Extensions;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.strings;
using Serilog;

namespace Mewdeko.Common.Autocompleters;

public class GenericCommandAutocompleter : AutocompleteHandler
{
    private CommandService Commands { get; }
    private CommandHandler CommandHandler { get; }
    private GlobalPermissionService Perms { get; }
    private IBotStrings Strings { get; }
    public GenericCommandAutocompleter(CommandService commands, GlobalPermissionService perms, IBotStrings strings, CommandHandler commandHandler)
    {
        Commands = commands;
        Perms = perms;
        Strings = strings;
        CommandHandler = commandHandler;
    }

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services) =>
        Task.FromResult(AutocompletionResult.FromSuccess(Commands.Commands.Where(c => !Perms.BlockedCommands.Contains(c.Aliases[0].ToLowerInvariant()))
                                                                  .Select(x => $"{x.Name} : {x.RealSummary(Strings, context.Guild?.Id, CommandHandler.GetPrefix(context.Guild?.Id))}")
                                                                  .Where(x => x.Contains((string)autocompleteInteraction.Data.Current.Value, StringComparison.OrdinalIgnoreCase))
                                                                  .OrderByDescending(x => x.StartsWith((string)autocompleteInteraction.Data.Current.Value, StringComparison.OrdinalIgnoreCase)).Distinct()
                                                                  .Take(20).Select(x => new AutocompleteResult(x.Length >= 100 ? x[..97] + "..." : x, x.Split(':')[0].Trim()))));
}
