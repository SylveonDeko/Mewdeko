using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Suggestions.Services;

namespace Mewdeko.Modules.Suggestions;

[Group("suggestions", "Send and manage suggestions.")]
public class SlashSuggestions : MewdekoSlashModuleBase<SuggestionsService>
{
    [SlashCommand("setchannel", "Sets the suggestion channel."), RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageChannels)]
    public async Task SetSuggestChannel(ITextChannel channel = null)
    {
        if (channel == null)
        {
            await Service.SetSuggestionChannelId(ctx.Guild, 0);
            await ctx.Interaction.SendConfirmAsync("Suggestions Disabled!");
        }
        else
        {
            await Service.SetSuggestionChannelId(ctx.Guild, channel.Id);
            var chn2 = await ctx.Guild.GetTextChannelAsync(SuggestChannel);
            await ctx.Interaction.SendConfirmAsync($"Your Suggestion channel has been set to {chn2.Mention}");
        }
    }

    [SlashCommand("suggest", "Sends a suggestion to the suggestion channel, if there is one set."), RequireContext(ContextType.Guild)]
    public async Task Suggest(string suggestion)
    {
        if (suggestion.Length > Service.GetMaxLength(ctx.Guild.Id))
        {
            await ctx.Interaction.SendEphemeralConfirmAsync(
                $"Cannot send this suggestion as its over the max length (`{Service.GetMaxLength(ctx.Guild.Id)}`) set in this server!");
            return;
        }
        if (suggestion.Length < Service.GetMinLength(ctx.Guild.Id))
        {
            await ctx.Interaction.SendEphemeralErrorAsync(
                $"Cannot send this suggestion as its under the minimum length (`{Service.GetMinLength(ctx.Guild.Id)}`) set in this server!");
            return;
        }

        await Service.SendSuggestion(ctx.Guild, ctx.User as IGuildUser, ctx.Client as DiscordSocketClient,
            suggestion, ctx.Channel as ITextChannel, ctx.Interaction);
    }

    [SlashCommand("deny", "Denies a suggestion"), RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageMessages)]
    public async Task Deny([Summary(description:"The number of the suggestion.")]ulong suggestid, string reason = null) =>
        await Service.SendDenyEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason, ctx.Interaction);

    [SlashCommand("accept", "Accepts a suggestion"),RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageMessages)]
    public async Task Accept([Summary(description:"The number of the suggestion.")]ulong suggestid, string reason = null) =>
        await Service.SendAcceptEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason, ctx.Interaction);

    [SlashCommand("implement", "Sets a suggestion as implemented"), RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageMessages)]
    public async Task Implemented([Summary(description:"The number of the suggestion.")]ulong suggestid, string reason = null) =>
        await Service.SendImplementEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason, ctx.Interaction);

    [SlashCommand("consider", "Sets a suggestion as considered"), RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageMessages)]
    public async Task Consider([Summary(description:"The number of the suggestion.")]ulong suggestid, string reason = null) =>
        await Service.SendConsiderEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, suggestid,
            ctx.Channel as ITextChannel, reason, ctx.Interaction);
}