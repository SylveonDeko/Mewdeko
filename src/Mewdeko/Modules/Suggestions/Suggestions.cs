using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Humanizer;
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

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
    public async Task SuggestInfo(ulong num)
    {
        var suggest = Service.Suggestions(ctx.Guild.Id, num).FirstOrDefault();
        if (suggest is null)
        {
            await ctx.Channel.SendErrorAsync("That suggestion wasn't found! Please double check the number.");
            return;
        }

        var emoteCount = new List<string>();
        var emotes = Service.GetEmotes(ctx.Guild.Id);
        int count = 0;
        if (emotes is not null and not "disable")
        {
            foreach (var i in emotes.Split(","))
            {
                emoteCount.Add($"{i.ToIEmote()} `{await Service.GetCurrentCount(ctx.Guild, suggest.MessageId, ++count)}`");
            }
        }
        else
        {
            emoteCount.Add($"👍 `{await Service.GetCurrentCount(ctx.Guild, suggest.MessageId, 1)}`");
            emoteCount.Add($"👎 `{await Service.GetCurrentCount(ctx.Guild, suggest.MessageId, 2)}`");
        }

        var components = new ComponentBuilder()
                         .WithButton("Accept", $"accept:{suggest.SuggestionId}")
                         .WithButton("Deny", $"deny:{suggest.SuggestionId}")
                         .WithButton("Consider", $"consider:{suggest.SuggestionId}")
                         .WithButton("Implement", $"implement:{suggest.SuggestionId}");
        var eb = new EmbedBuilder()
            .WithOkColor()
            .AddField("Suggestion", $"{suggest.Suggestion.Truncate(256)} \n[Jump To Suggestion](https://discord.com/channels/{ctx.Guild.Id}/{Service.GetSuggestionChannel(ctx.Guild.Id)}/{suggest.MessageId})")
            .AddField("Suggested By", $"<@{suggest.UserId}> `{suggest.UserId}`")
            .AddField("Curerent State", (SuggestionsService.SuggestState)suggest.CurrentState)
            .AddField("Last Changed By", suggest.StateChangeUser == 0 ? "Nobody" : $"<@{suggest.StateChangeUser}> `{suggest.StateChangeUser}`")
            .AddField("State Change Count", suggest.StateChangeCount)
            .AddField("Emote Count", string.Join("\n", emoteCount));
        await ctx.Channel.SendMessageAsync(embed: eb.Build(), components: components.Build());
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
    public async Task SuggestClear()
    {
        var suggests = Service.Suggestions(ctx.Guild.Id);
        if (suggests.Count == 0)
        {
            await ctx.Channel.SendErrorAsync("There are no suggestions to clear.");
            return;
        }
        if (await PromptUserConfirmAsync("Are you sure you want to clear all suggestions? ***This cannot be undone.***", ctx.User.Id))
        {
            await Service.SuggestReset(ctx.Guild);
            await ctx.Channel.SendConfirmAsync("Suggestions cleared.");
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

        suggestion = suggestion.EscapeQuotes();
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
            ctx.Channel as ITextChannel, reason.EscapeQuotes());

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageMessages)]
    public async Task Accept(ulong sid, [Remainder] string? reason = null) =>
        await Service.SendAcceptEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid,
            ctx.Channel as ITextChannel, reason.EscapeQuotes());

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageMessages)]
    public async Task Implemented(ulong sid, [Remainder] string? reason = null) =>
        await Service.SendImplementEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid,
            ctx.Channel as ITextChannel, reason.EscapeQuotes());

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageMessages)]
    public async Task Consider(ulong sid, [Remainder] string? reason = null) =>
        await Service.SendConsiderEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid,
            ctx.Channel as ITextChannel, reason.EscapeQuotes());
}