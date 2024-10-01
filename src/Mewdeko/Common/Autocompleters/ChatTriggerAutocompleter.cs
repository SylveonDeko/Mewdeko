using Discord.Interactions;
using Mewdeko.Modules.Chat_Triggers.Services;

namespace Mewdeko.Common.Autocompleters;

/// <summary>
///     Autocompleter for chat triggers.
/// </summary>
public class ChatTriggerAutocompleter : AutocompleteHandler
{
    private const int MaxSuggestions = 25;
    private const int MaxDescriptionLength = 100;

    /// <summary>
    ///     Initializes a new instance of the ChatTriggerAutocompleter class.
    /// </summary>
    /// <param name="triggers">The ChatTriggersService.</param>
    /// <param name="credentials">The bot credentials.</param>
    public ChatTriggerAutocompleter(ChatTriggersService triggers, IBotCredentials credentials)
    {
        Triggers = triggers;
        Credentials = credentials;
    }

    /// <summary>
    ///     Gets or sets the ChatTriggersService.
    /// </summary>
    private ChatTriggersService Triggers { get; }

    /// <summary>
    ///     Gets or sets the bot credentials.
    /// </summary>
    private IBotCredentials Credentials { get; }

    /// <summary>
    ///     Generates suggestions for autocomplete.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <param name="parameter">The parameter info.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the autocomplete result.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        if (!HasPermission(autocompleteInteraction))
            return AutocompletionResult.FromError(InteractionCommandError.Unsuccessful,
                "You don't have permission to view chat triggers!");

        var input = autocompleteInteraction.Data.Current.Value as string;

        var suggestions = (await Triggers.GetChatTriggersFor(context.Guild?.Id))
            .Where(x => (x.Trigger + x.RealName + x.Response).Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Trigger.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSuggestions)
            .Select(x => new AutocompleteResult($"{x.RealName} ({x.Trigger})".TrimTo(MaxDescriptionLength), x.Id));

        return AutocompletionResult.FromSuccess(suggestions);
    }

    /// <summary>
    ///     Checks if the user has permission to view chat triggers.
    /// </summary>
    /// <param name="autocompleteInteraction">The autocomplete interaction.</param>
    /// <returns>A boolean indicating whether the user has permission to view chat triggers.</returns>
    private bool HasPermission(IDiscordInteraction autocompleteInteraction)
    {
        if (autocompleteInteraction.User is IGuildUser user)
        {
            return user.Guild.OwnerId == user.Id || user.GuildPermissions.Has(GuildPermission.Administrator);
        }

        return Credentials.IsOwner(autocompleteInteraction.User);
    }
}