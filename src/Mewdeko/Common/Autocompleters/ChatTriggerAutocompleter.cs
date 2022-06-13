using Discord.Interactions;
using Mewdeko.Modules.Chat_Triggers.Services;
using System.Threading.Tasks;

namespace Mewdeko.Common.Autocompleters;

public class ChatTriggerAutocompleter : AutocompleteHandler
{
    public ChatTriggersService _triggers { get; set; }
    private IBotCredentials _credentials { get; set; }

    public ChatTriggerAutocompleter(ChatTriggersService triggers, IBotCredentials credentials)
    {
        _triggers = triggers;
        _credentials = credentials;
    }

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {

        if ((autocompleteInteraction.User is IGuildUser user
             && user.Guild.OwnerId != user.Id
             && !user.GuildPermissions.Has(GuildPermission.Administrator))
            || (autocompleteInteraction.User is not IGuildUser && !_credentials.IsOwner(autocompleteInteraction.User)))
            return Task.FromResult(AutocompletionResult.FromError(InteractionCommandError.Unsuccessful,
                "You don't have permission to view chat triggers!"));

        var inpt = autocompleteInteraction.Data.Current.Value as string;


        return Task.FromResult(AutocompletionResult.FromSuccess(_triggers.GetChatTriggersFor(context.Guild?.Id)
            .Where(x => (x.Trigger + x.RealName + x.Response).Contains(inpt, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Trigger.StartsWith(inpt, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(x => new AutocompleteResult($"{x.RealName} ({x.Trigger})".TrimTo(100), x.Id))));
    }
}
