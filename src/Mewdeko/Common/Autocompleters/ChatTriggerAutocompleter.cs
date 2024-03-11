using Discord.Interactions;
using Mewdeko.Modules.Chat_Triggers.Services;

namespace Mewdeko.Common.Autocompleters;

public class ChatTriggerAutocompleter : AutocompleteHandler
{
    private const int MaxSuggestions = 25;
    private const int MaxDescriptionLength = 100;

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
        if (!HasPermission(autocompleteInteraction))
            return Task.FromResult(AutocompletionResult.FromError(InteractionCommandError.Unsuccessful,
                "You don't have permission to view chat triggers!"));

        var input = autocompleteInteraction.Data.Current.Value as string;

        var suggestions = Triggers.GetChatTriggersFor(context.Guild?.Id)
            .Where(x => (x.Trigger + x.RealName + x.Response).Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Trigger.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSuggestions)
            .Select(x => new AutocompleteResult($"{x.RealName} ({x.Trigger})".TrimTo(MaxDescriptionLength), x.Id));

        return Task.FromResult(AutocompletionResult.FromSuccess(suggestions));
    }

    private bool HasPermission(IDiscordInteraction autocompleteInteraction)
    {
        if (autocompleteInteraction.User is IGuildUser user)
        {
            return user.Guild.OwnerId == user.Id || user.GuildPermissions.Has(GuildPermission.Administrator);
        }

        return Credentials.IsOwner(autocompleteInteraction.User);
    }
}