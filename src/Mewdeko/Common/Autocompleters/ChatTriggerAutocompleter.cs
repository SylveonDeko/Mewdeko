using System.Threading.Tasks;
using Discord.Interactions;
using Mewdeko.Modules.Chat_Triggers.Services;

namespace Mewdeko.Common.Autocompleters;

public class ChatTriggerAutocompleter : AutocompleteHandler
{
    public ChatTriggersService Triggers { get; set; }
    private IBotCredentials Credentials { get; set; }

    public ChatTriggerAutocompleter(ChatTriggersService triggers, IBotCredentials credentials)
    {
        Triggers = triggers;
        Credentials = credentials;
    }

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        if ((autocompleteInteraction.User is IGuildUser user
             && user.Guild.OwnerId != user.Id
             && !user.GuildPermissions.Has(GuildPermission.Administrator))
            || (autocompleteInteraction.User is not IGuildUser && !Credentials.IsOwner(autocompleteInteraction.User)))
            return Task.FromResult(AutocompletionResult.FromError(InteractionCommandError.Unsuccessful,
                "You don't have permission to view chat triggers!"));

        var inpt = autocompleteInteraction.Data.Current.Value as string;


        return Task.FromResult(AutocompletionResult.FromSuccess(Triggers.GetChatTriggersFor(context.Guild?.Id)
            .Where(x => (x.Trigger + x.RealName + x.Response).Contains(inpt, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Trigger.StartsWith(inpt, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(x => new AutocompleteResult($"{x.RealName} ({x.Trigger})".TrimTo(100), x.Id))));
    }
}