using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Mewdeko;
using Mewdeko.Common.Attributes;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Modules.Chat_Triggers.Services;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Common.Attributes;

public class ChatTriggerAutocompleter : AutocompleteHandler
{
    public ChatTriggersService _triggers { get; set; }

    public ChatTriggerAutocompleter(ChatTriggersService triggers)
    {
        _triggers = triggers;
    }

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var inpt = autocompleteInteraction.Data.Current.Value as string;
        if (autocompleteInteraction.User is not IGuildUser user) return Task.FromResult(AutocompletionResult.FromError(InteractionCommandError.Unsuccessful, "You must be in a guild to use this autocompelter!"));

        if (user.Guild.OwnerId != user.Id && !user.GuildPermissions.Has(GuildPermission.Administrator))
            return Task.FromResult(AutocompletionResult.FromError(InteractionCommandError.Unsuccessful, "You must be an administrator to veiw chat triggers!"));
        return Task.FromResult(AutocompletionResult.FromSuccess(_triggers.GetChatTriggersFor(user.GuildId)
            .Where(x => (x.Trigger + x.Response).Contains(inpt, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Trigger.StartsWith(inpt, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .Select(x => new AutocompleteResult(x.Trigger, x.Id))));
    }
}
