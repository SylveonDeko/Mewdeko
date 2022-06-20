using Discord.Interactions;
using Mewdeko.Common.Attributes.SlashCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Suggestions.Services;
using System.Threading.Tasks;

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
            await Service.SetSuggestionChannelId(ctx.Guild, 0);
            await ctx.Interaction.SendConfirmAsync("Suggestions Disabled!");
        }
        else
        {
            await Service.SetSuggestionChannelId(ctx.Guild, channel.Id);
            var chn2 = await ctx.Guild.GetTextChannelAsync(Service.GetSuggestionChannel(ctx.Guild.Id));
            await ctx.Interaction.SendConfirmAsync($"Your Suggestion channel has been set to {chn2.Mention}");
        }
    }

    [SlashCommand("suggest", "Sends a suggestion to the suggestion channel, if there is one set.", true),
     RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Suggest() => await ctx.Interaction.RespondWithModalAsync<SuggestionModal>("suggest.sendsuggestion",
        null,
        x => x.UpdateTextInput("suggestion",
            s => s.WithMaxLength(Math.Min(4000, Service.GetMaxLength(ctx.Guild?.Id)))
                  .WithMinLength(Math.Min(Service.GetMinLength(ctx.Guild?.Id), 4000))))
                                      .ConfigureAwait(false);

    [ComponentInteraction("accept:*", true), RequireContext(ContextType.Guild), CheckPermissions, SlashUserPerm(ChannelPermission.ManageMessages)]
    public async Task Accept(string suggestId)
        => await ctx.Interaction.RespondWithModalAsync<SuggestStateModal>($"suggeststate:accept.{suggestId}");

    [ComponentInteraction("deny:*", true), RequireContext(ContextType.Guild), CheckPermissions, SlashUserPerm(ChannelPermission.ManageMessages)]
    public async Task Deny(string suggestId)
        => await ctx.Interaction.RespondWithModalAsync<SuggestStateModal>($"suggeststate:deny.{suggestId}");
    [ComponentInteraction("consider:*", true), RequireContext(ContextType.Guild), CheckPermissions, SlashUserPerm(ChannelPermission.ManageMessages)]
    public async Task Consider(string suggestId)
        => await ctx.Interaction.RespondWithModalAsync<SuggestStateModal>($"suggeststate:consider.{suggestId}");
    [ComponentInteraction("implement:*", true), RequireContext(ContextType.Guild), CheckPermissions, SlashUserPerm(ChannelPermission.ManageMessages)]
    public async Task Implemented(string suggestId)
        => await ctx.Interaction.RespondWithModalAsync<SuggestStateModal>($"suggeststate:implement.{suggestId}");

    [ModalInteraction("suggeststate:*.*", true), CheckPermissions, RequireContext(ContextType.Guild)]
    public async Task HandleStateModal(string state, string suggestId, SuggestStateModal modal)
    {
        ulong.TryParse(suggestId, out var sugId);
        switch (state)
        {
            case "accept":
                await Accept(sugId, modal.Reason.EscapeQuotes());
                break;
            case "deny":
                await Deny(sugId, modal.Reason.EscapeQuotes());
                break;
            case "consider":
                await Consider(sugId, modal.Reason.EscapeQuotes());
                break;
            case "implement":
                await Implemented(sugId, modal.Reason.EscapeQuotes());
                break;
        }
    }

    [ModalInteraction("suggest.sendsuggestion", true), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task HandleSuggestion(SuggestionModal modal)
    {
        modal.Suggestion = modal.Suggestion.EscapeQuotes();
        if (Service.GetSuggestionChannel(ctx.Guild.Id) is 0)
        {
            await EphemeralReplyErrorLocalizedAsync("suggest_channel_not_set");
        }
        await DeferAsync(true);
        if (modal.Suggestion.Length > Service.GetMaxLength(ctx.Guild.Id))
        {
            await ctx.Interaction.SendEphemeralFollowupConfirmAsync(
                $"Cannot send this suggestion as its over the max length (`{Service.GetMaxLength(ctx.Guild.Id)}`) set in this server!");
            return;
        }
        if (modal.Suggestion.Length < Service.GetMinLength(ctx.Guild.Id))
        {
            await ctx.Interaction.SendEphemeralFollowupErrorAsync(
                $"Cannot send this suggestion as its under the minimum length (`{Service.GetMinLength(ctx.Guild.Id)}`) set in this server!");
            return;
        }

        await Service.SendSuggestion(ctx.Guild, ctx.User as IGuildUser, ctx.Client as DiscordSocketClient,
            modal.Suggestion, ctx.Channel as ITextChannel, ctx.Interaction);
    }
    [SlashCommand("clear", "Clears all suggestions. Cannot be undone.")]
    public async Task SuggestClear()
    {
        await DeferAsync();
        var suggests = Service.Suggestions(ctx.Guild.Id);
        if (suggests.Count == 0)
        {
            await ctx.Interaction.SendErrorFollowupAsync("There are no suggestions to clear.");
            return;
        }
        if (await PromptUserConfirmAsync("Are you sure you want to clear all suggestions? ***This cannot be undone.***", ctx.User.Id))
        {
            await Service.SuggestReset(ctx.Guild);
            await ctx.Interaction.SendConfirmFollowupAsync("Suggestions cleared.");
        }
    }
    [SlashCommand("deny", "Denies a suggestion"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task Deny([Summary(description: "The number of the suggestion.")][Autocomplete(typeof(SuggestionAutocompleter))] ulong suggestid, string? reason = null) =>
        await Service.SendDenyEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason.EscapeQuotes(), ctx.Interaction);

    [SlashCommand("accept", "Accepts a suggestion"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task Accept([Summary(description: "The number of the suggestion.")][Autocomplete(typeof(SuggestionAutocompleter))] ulong suggestid, string? reason = null) =>
        await Service.SendAcceptEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason.EscapeQuotes(), ctx.Interaction);

    [SlashCommand("implement", "Sets a suggestion as implemented"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task Implemented([Summary(description: "The number of the suggestion.")][Autocomplete(typeof(SuggestionAutocompleter))] ulong suggestid, string? reason = null) =>
        await Service.SendImplementEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason.EscapeQuotes(), ctx.Interaction);

    [SlashCommand("consider", "Sets a suggestion as considered"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task Consider([Summary(description: "The number of the suggestion.")][Autocomplete(typeof(SuggestionAutocompleter))] ulong suggestid, string? reason = null) =>
        await Service.SendConsiderEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason.EscapeQuotes(), ctx.Interaction);
}