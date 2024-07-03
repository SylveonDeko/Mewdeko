using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Suggestions.Services;

namespace Mewdeko.Modules.Suggestions;

/// <summary>
/// Slash commands for sending and managing suggestions.
/// </summary>
[Group("suggestions", "Send or manage suggestions!")]
public partial class SlashSuggestions : MewdekoSlashModuleBase<SuggestionsService>
{
    /// <summary>
    /// Sets the suggestion channel for the guild.
    /// </summary>
    /// <param name="channel">The text channel to be set as the suggestion channel. If null, suggestions will be disabled.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This command allows guild administrators to designate a specific text channel for suggestions.
    /// Setting the channel to null will disable the suggestion feature.
    /// </remarks>
    [SlashCommand("setchannel", "Sets the suggestion channel."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageChannels), CheckPermissions]
    public async Task SetSuggestChannel(ITextChannel? channel = null)
    {
        if (channel == null)
        {
            await Service.SetSuggestionChannelId(ctx.Guild, 0).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync("Suggestions Disabled!").ConfigureAwait(false);
        }
        else
        {
            await Service.SetSuggestionChannelId(ctx.Guild, channel.Id).ConfigureAwait(false);
            var chn2 = await ctx.Guild.GetTextChannelAsync(await Service.GetSuggestionChannel(ctx.Guild.Id))
                .ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync($"Your Suggestion channel has been set to {chn2.Mention}")
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Initiates the process for a user to send a suggestion via a modal interaction.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This command opens a modal for the user to enter their suggestion, ensuring it adheres to the set character limits.
    /// </remarks>
    [SlashCommand("suggest", "Sends a suggestion to the suggestion channel, if there is one set.", true),
     RequireContext(ContextType.Guild), CheckPermissions]
    public Task Suggest() => ctx.Interaction.RespondWithModalAsync<SuggestionModal>("suggest.sendsuggestion",
        null,
        x => x.UpdateTextInput("suggestion", async s => s
            .WithMaxLength(Math.Min(4000, await Service.GetMaxLength(ctx.Guild.Id)))
            .WithMinLength(Math.Min(await Service.GetMinLength(ctx.Guild.Id), 4000))));

    /// <summary>
    /// Accepts a suggestion.
    /// </summary>
    /// <param name="suggestId">The unique identifier of the suggestion to be accepted.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This command changes the state of a suggestion to accepted, triggering any configured response or action.
    /// </remarks>
    [ComponentInteraction("accept:*", true), RequireContext(ContextType.Guild), CheckPermissions,
     SlashUserPerm(ChannelPermission.ManageMessages)]
    public Task Accept(string suggestId)
        => ctx.Interaction.RespondWithModalAsync<SuggestStateModal>($"suggeststate:accept.{suggestId}");

    /// <summary>
    /// Denies a suggestion.
    /// </summary>
    /// <param name="suggestId">The unique identifier of the suggestion to be denied.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This command changes the state of a suggestion to denied, triggering any configured response or action.
    /// </remarks>
    [ComponentInteraction("deny:*", true), RequireContext(ContextType.Guild), CheckPermissions,
     SlashUserPerm(ChannelPermission.ManageMessages)]
    public Task Deny(string suggestId)
        => ctx.Interaction.RespondWithModalAsync<SuggestStateModal>($"suggeststate:deny.{suggestId}");

    /// <summary>
    /// Marks a suggestion as considered.
    /// </summary>
    /// <param name="suggestId">The unique identifier of the suggestion to be considered.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This command changes the state of a suggestion to considered, triggering any configured response or action.
    /// </remarks>
    [ComponentInteraction("consider:*", true), RequireContext(ContextType.Guild), CheckPermissions,
     SlashUserPerm(ChannelPermission.ManageMessages)]
    public Task Consider(string suggestId)
        => ctx.Interaction.RespondWithModalAsync<SuggestStateModal>($"suggeststate:consider.{suggestId}");

    /// <summary>
    /// Marks a suggestion as implemented.
    /// </summary>
    /// <param name="suggestId">The unique identifier of the suggestion to be implemented.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This command changes the state of a suggestion to implemented, triggering any configured response or action.
    /// </remarks>
    [ComponentInteraction("implement:*", true), RequireContext(ContextType.Guild), CheckPermissions,
     SlashUserPerm(ChannelPermission.ManageMessages)]
    public Task Implemented(string suggestId)
        => ctx.Interaction.RespondWithModalAsync<SuggestStateModal>($"suggeststate:implement.{suggestId}");

    /// <summary>
    /// Handles the modal interaction for changing the state of a suggestion.
    /// </summary>
    /// <param name="state">The new state of the suggestion.</param>
    /// <param name="suggestId">The unique identifier of the suggestion.</param>
    /// <param name="modal">The modal containing the state change reason.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ModalInteraction("suggeststate:*.*", true), CheckPermissions, RequireContext(ContextType.Guild)]
    public Task HandleStateModal(string state, string suggestId, SuggestStateModal modal)
    {
        ulong.TryParse(suggestId, out var sugId);
        return state switch
        {
            "accept" => Accept(sugId, modal.Reason.EscapeWeirdStuff()),
            "deny" => Deny(sugId, modal.Reason.EscapeWeirdStuff()),
            "consider" => Consider(sugId, modal.Reason.EscapeWeirdStuff()),
            "implement" => Implemented(sugId, modal.Reason.EscapeWeirdStuff()),
            _ => Task.CompletedTask
        };
    }

    /// <summary>
    /// Handles the modal interaction for sending a suggestion.
    /// </summary>
    /// <param name="modal">The modal containing the suggestion.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// Validates the suggestion length against the configured limits before sending.
    /// </remarks>
    [ModalInteraction("suggest.sendsuggestion", true), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task HandleSuggestion(SuggestionModal modal)
    {
        modal.Suggestion = modal.Suggestion.EscapeWeirdStuff();
        if (await Service.GetSuggestionChannel(ctx.Guild.Id) is 0)
        {
            await EphemeralReplyErrorLocalizedAsync("suggest_channel_not_set").ConfigureAwait(false);
        }

        await DeferAsync(true).ConfigureAwait(false);
        if (modal.Suggestion.Length > await Service.GetMaxLength(ctx.Guild.Id))
        {
            await ctx.Interaction.SendEphemeralFollowupConfirmAsync(
                    $"Cannot send this suggestion as its over the max length (`{await Service.GetMaxLength(ctx.Guild.Id)}`) set in this server!")
                .ConfigureAwait(false);
            return;
        }

        if (modal.Suggestion.Length < await Service.GetMinLength(ctx.Guild.Id))
        {
            await ctx.Interaction.SendEphemeralFollowupErrorAsync(
                    $"Cannot send this suggestion as its under the minimum length (`{await Service.GetMinLength(ctx.Guild.Id)}`) set in this server!",
                    Config)
                .ConfigureAwait(false);
            return;
        }

        await Service.SendSuggestion(ctx.Guild, ctx.User as IGuildUser, ctx.Client as DiscordShardedClient,
            modal.Suggestion, ctx.Channel as ITextChannel, ctx.Interaction).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears all suggestions from the guild.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This action is irreversible. It deletes all suggestions and their associated data from the guild.
    /// </remarks>
    [SlashCommand("clear", "Clears all suggestions. Cannot be undone.")]
    public async Task SuggestClear()
    {
        await DeferAsync().ConfigureAwait(false);
        var suggests = await Service.Suggestions(ctx.Guild.Id);
        if (suggests.Count == 0)
        {
            await ctx.Interaction.SendErrorFollowupAsync("There are no suggestions to clear.", Config)
                .ConfigureAwait(false);
            return;
        }

        if (await PromptUserConfirmAsync("Are you sure you want to clear all suggestions? ***This cannot be undone.***",
                ctx.User.Id).ConfigureAwait(false))
        {
            await Service.SuggestReset(ctx.Guild).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmFollowupAsync("Suggestions cleared.").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Denies a specific suggestion.
    /// </summary>
    /// <param name="suggestid">The ID of the suggestion to be denied.</param>
    /// <param name="reason">The reason for denying the suggestion.</param>
    /// <returns>A task that represents the asynchronous operation of denying a suggestion.</returns>
    /// <remarks>
    /// This command changes the status of a suggestion to denied and notifies the suggestion's author.
    /// </remarks>
    [SlashCommand("deny", "Denies a suggestion"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public Task Deny(
        [Summary(description: "The number of the suggestion.")] [Autocomplete(typeof(SuggestionAutocompleter))]
        ulong suggestid, string? reason = null) =>
        Service.SendDenyEmbed(ctx.Guild, ctx.Client as DiscordShardedClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason.EscapeWeirdStuff(), ctx.Interaction);

    /// <summary>
    /// Accepts a specific suggestion.
    /// </summary>
    /// <param name="suggestid">The ID of the suggestion to be accepted.</param>
    /// <param name="reason">The reason for accepting the suggestion.</param>
    /// <returns>A task that represents the asynchronous operation of accepting a suggestion.</returns>
    /// <remarks>
    /// This command changes the status of a suggestion to accepted and can trigger additional configured actions or responses.
    /// </remarks>
    [SlashCommand("accept", "Accepts a suggestion"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public Task Accept(
        [Summary(description: "The number of the suggestion.")] [Autocomplete(typeof(SuggestionAutocompleter))]
        ulong suggestid, string? reason = null) =>
        Service.SendAcceptEmbed(ctx.Guild, ctx.Client as DiscordShardedClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason.EscapeWeirdStuff(), ctx.Interaction);

    /// <summary>
    /// Marks a specific suggestion as implemented.
    /// </summary>
    /// <param name="suggestid">The ID of the suggestion to be marked as implemented.</param>
    /// <param name="reason">The reason for marking the suggestion as implemented.</param>
    /// <returns>A task that represents the asynchronous operation of marking a suggestion as implemented.</returns>
    /// <remarks>
    /// This command changes the status of a suggestion to implemented, indicating that the suggestion has been or will be acted upon.
    /// </remarks>
    [SlashCommand("implement", "Sets a suggestion as implemented"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public Task Implemented(
        [Summary(description: "The number of the suggestion.")] [Autocomplete(typeof(SuggestionAutocompleter))]
        ulong suggestid, string? reason = null) =>
        Service.SendImplementEmbed(ctx.Guild, ctx.Client as DiscordShardedClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason.EscapeWeirdStuff(), ctx.Interaction);

    /// <summary>
    /// Marks a specific suggestion as considered.
    /// </summary>
    /// <param name="suggestid">The ID of the suggestion to be considered.</param>
    /// <param name="reason">The reason for considering the suggestion.</param>
    /// <returns>A task that represents the asynchronous operation of marking a suggestion as considered.</returns>
    /// <remarks>
    /// This command changes the status of a suggestion to considered, indicating that it is under review or discussion.
    /// </remarks>
    [SlashCommand("consider", "Sets a suggestion as considered"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public Task Consider(
        [Summary(description: "The number of the suggestion.")] [Autocomplete(typeof(SuggestionAutocompleter))]
        ulong suggestid, string? reason = null) =>
        Service.SendConsiderEmbed(ctx.Guild, ctx.Client as DiscordShardedClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason.EscapeWeirdStuff(), ctx.Interaction);
}