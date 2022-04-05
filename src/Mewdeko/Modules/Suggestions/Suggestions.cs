using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Suggestions.Services;

namespace Mewdeko.Modules.Suggestions;
public partial class Suggestions : MewdekoModuleBase<SuggestionsService>
{
    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageChannels)]
    public async Task SetSuggestChannel(ITextChannel? channel = null)
    {
        if (channel == null)
        {
            await Service.SetSuggestionChannelId(ctx.Guild, 0);
            await ctx.Channel.SendConfirmAsync("Suggestions Disabled!");
        }
        else
        {
            await Service.SetSuggestionChannelId(ctx.Guild, channel.Id);
            var chn2 = await ctx.Guild.GetTextChannelAsync(SuggestChannel);
            await ctx.Channel.SendConfirmAsync($"Your Suggestion channel has been set to {chn2.Mention}");
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Suggest([Remainder] string suggestion)
    {
        try
        {
            await ctx.Message.DeleteAsync();
        }
        catch
        {
            //ignored
        }
        if (suggestion.Length > Service.GetMaxLength(ctx.Guild.Id))
        {
            var msg = await ctx.Channel.SendErrorAsync(
                $"Cannot send this suggestion as its over the max length (`{Service.GetMaxLength(ctx.Guild.Id)}`) set in this server!");
            msg.DeleteAfter(5);
            return;
        }
        if (suggestion.Length < Service.GetMinLength(ctx.Guild.Id))
        {
            var message = await ctx.Channel.SendErrorAsync(
                $"Cannot send this suggestion as its under the minimum length (`{Service.GetMinLength(ctx.Guild.Id)}`) set in this server!");
            message.DeleteAfter(5);
            return;
        }
        
        await Service.SendSuggestion(ctx.Guild, ctx.User as IGuildUser, ctx.Client as DiscordSocketClient,
            suggestion, ctx.Channel as ITextChannel);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageMessages)]
    public async Task Deny(ulong sid, [Remainder] string? reason = null) =>
        await Service.SendDenyEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid,
            ctx.Channel as ITextChannel, reason);

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageMessages)]
    public async Task Accept(ulong sid, [Remainder] string? reason = null) =>
        await Service.SendAcceptEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid,
            ctx.Channel as ITextChannel, reason);

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageMessages)]
    public async Task Implemented(ulong sid, [Remainder] string? reason = null) =>
        await Service.SendImplementEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid,
            ctx.Channel as ITextChannel, reason);

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageMessages)]
    public async Task Consider(ulong sid, [Remainder] string? reason = null) =>
        await Service.SendConsiderEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid,
            ctx.Channel as ITextChannel, reason);
}