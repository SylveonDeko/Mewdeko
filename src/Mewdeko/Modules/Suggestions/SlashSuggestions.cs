using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Suggestions.Services;

namespace Mewdeko.Modules.Suggestions;

[Group("suggestions", "Send or manage suggestions!")]
public class SlashSuggestions : MewdekoSlashModuleBase<SuggestionsService>
{
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

    [SlashCommand("suggest", "Sends a suggestion to the suggestion channel, if there is one set.", true),
     RequireContext(ContextType.Guild), CheckPermissions]
    public Task Suggest() => ctx.Interaction.RespondWithModalAsync<SuggestionModal>("suggest.sendsuggestion",
        null,
        x => x.UpdateTextInput("suggestion", async s => s
            .WithMaxLength(Math.Min(4000, await Service.GetMaxLength(ctx.Guild.Id)))
            .WithMinLength(Math.Min(await Service.GetMinLength(ctx.Guild.Id), 4000))));

    [ComponentInteraction("accept:*", true), RequireContext(ContextType.Guild), CheckPermissions,
     SlashUserPerm(ChannelPermission.ManageMessages)]
    public Task Accept(string suggestId)
        => ctx.Interaction.RespondWithModalAsync<SuggestStateModal>($"suggeststate:accept.{suggestId}");

    [ComponentInteraction("deny:*", true), RequireContext(ContextType.Guild), CheckPermissions,
     SlashUserPerm(ChannelPermission.ManageMessages)]
    public Task Deny(string suggestId)
        => ctx.Interaction.RespondWithModalAsync<SuggestStateModal>($"suggeststate:deny.{suggestId}");

    [ComponentInteraction("consider:*", true), RequireContext(ContextType.Guild), CheckPermissions,
     SlashUserPerm(ChannelPermission.ManageMessages)]
    public Task Consider(string suggestId)
        => ctx.Interaction.RespondWithModalAsync<SuggestStateModal>($"suggeststate:consider.{suggestId}");

    [ComponentInteraction("implement:*", true), RequireContext(ContextType.Guild), CheckPermissions,
     SlashUserPerm(ChannelPermission.ManageMessages)]
    public Task Implemented(string suggestId)
        => ctx.Interaction.RespondWithModalAsync<SuggestStateModal>($"suggeststate:implement.{suggestId}");

    [ModalInteraction("suggeststate:*.*", true), CheckPermissions, RequireContext(ContextType.Guild)]
    public Task HandleStateModal(string state, string suggestId, SuggestStateModal modal)
    {
        ulong.TryParse(suggestId, out var sugId);
        switch (state)
        {
            case "accept":
                return Accept(sugId, modal.Reason.EscapeWeirdStuff());
                break;
            case "deny":
                return Deny(sugId, modal.Reason.EscapeWeirdStuff());
                break;
            case "consider":
                return Consider(sugId, modal.Reason.EscapeWeirdStuff());
                break;
            case "implement":
                return Implemented(sugId, modal.Reason.EscapeWeirdStuff());
                break;
        }

        return Task.CompletedTask;
    }

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
                    $"Cannot send this suggestion as its under the minimum length (`{await Service.GetMinLength(ctx.Guild.Id)}`) set in this server!")
                .ConfigureAwait(false);
            return;
        }

        await Service.SendSuggestion(ctx.Guild, ctx.User as IGuildUser, ctx.Client as DiscordSocketClient,
            modal.Suggestion, ctx.Channel as ITextChannel, ctx.Interaction).ConfigureAwait(false);
    }

    [SlashCommand("clear", "Clears all suggestions. Cannot be undone.")]
    public async Task SuggestClear()
    {
        await DeferAsync().ConfigureAwait(false);
        var suggests = Service.Suggestions(ctx.Guild.Id);
        if (suggests.Count == 0)
        {
            await ctx.Interaction.SendErrorFollowupAsync("There are no suggestions to clear.").ConfigureAwait(false);
            return;
        }

        if (await PromptUserConfirmAsync("Are you sure you want to clear all suggestions? ***This cannot be undone.***",
                ctx.User.Id).ConfigureAwait(false))
        {
            await Service.SuggestReset(ctx.Guild).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmFollowupAsync("Suggestions cleared.").ConfigureAwait(false);
        }
    }

    [SlashCommand("deny", "Denies a suggestion"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public Task Deny(
        [Summary(description: "The number of the suggestion.")] [Autocomplete(typeof(SuggestionAutocompleter))]
        ulong suggestid, string? reason = null) =>
        Service.SendDenyEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason.EscapeWeirdStuff(), ctx.Interaction);

    [SlashCommand("accept", "Accepts a suggestion"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public Task Accept(
        [Summary(description: "The number of the suggestion.")] [Autocomplete(typeof(SuggestionAutocompleter))]
        ulong suggestid, string? reason = null) =>
        Service.SendAcceptEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason.EscapeWeirdStuff(), ctx.Interaction);

    [SlashCommand("implement", "Sets a suggestion as implemented"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public Task Implemented(
        [Summary(description: "The number of the suggestion.")] [Autocomplete(typeof(SuggestionAutocompleter))]
        ulong suggestid, string? reason = null) =>
        Service.SendImplementEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason.EscapeWeirdStuff(), ctx.Interaction);

    [SlashCommand("consider", "Sets a suggestion as considered"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public Task Consider(
        [Summary(description: "The number of the suggestion.")] [Autocomplete(typeof(SuggestionAutocompleter))]
        ulong suggestid, string? reason = null) =>
        Service.SendConsiderEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason.EscapeWeirdStuff(), ctx.Interaction);
}