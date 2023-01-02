using System.Threading.Tasks;
using Discord.Commands;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
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
            await Service.SetSuggestionChannelId(ctx.Guild, 0).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("Suggestions Disabled!").ConfigureAwait(false);
        }
        else
        {
            await Service.SetSuggestionChannelId(ctx.Guild, channel.Id).ConfigureAwait(false);
            var chn2 = await ctx.Guild.GetTextChannelAsync(await Service.GetSuggestionChannel(ctx.Guild.Id)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Your Suggestion channel has been set to {chn2.Mention}").ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
    public async Task SuggestInfo(ulong num)
    {
        var suggest = (await Service.Suggestions(ctx.Guild.Id, num)).FirstOrDefault();
        if (suggest is null)
        {
            await ctx.Channel.SendErrorAsync("That suggestion wasn't found! Please double check the number.").ConfigureAwait(false);
            return;
        }

        var emoteCount = new List<string>();
        var emotes = await Service.GetEmotes(ctx.Guild.Id);
        var count = 0;
        if (emotes is not null and not "disable")
        {
            foreach (var i in emotes.Split(","))
            {
                emoteCount.Add($"{i.ToIEmote()} `{await Service.GetCurrentCount(suggest.MessageId, ++count).ConfigureAwait(false)}`");
            }
        }
        else
        {
            emoteCount.Add($"👍 `{await Service.GetCurrentCount(suggest.MessageId, 1).ConfigureAwait(false)}`");
            emoteCount.Add($"👎 `{await Service.GetCurrentCount(suggest.MessageId, 2).ConfigureAwait(false)}`");
        }

        var components = new ComponentBuilder()
            .WithButton("Accept", $"accept:{suggest.SuggestionId}")
            .WithButton("Deny", $"deny:{suggest.SuggestionId}")
            .WithButton("Consider", $"consider:{suggest.SuggestionId}")
            .WithButton("Implement", $"implement:{suggest.SuggestionId}");
        var eb = new EmbedBuilder()
            .WithOkColor()
            .AddField("Suggestion",
                $"{suggest.Suggestion.Truncate(256)} \n[Jump To Suggestion](https://discord.com/channels/{ctx.Guild.Id}/{Service.GetSuggestionChannel(ctx.Guild.Id)}/{suggest.MessageId})")
            .AddField("Suggested By", $"<@{suggest.UserId}> `{suggest.UserId}`")
            .AddField("Curerent State", (SuggestionsService.SuggestState)suggest.CurrentState)
            .AddField("Last Changed By", suggest.StateChangeUser == 0 ? "Nobody" : $"<@{suggest.StateChangeUser}> `{suggest.StateChangeUser}`")
            .AddField("State Change Count", suggest.StateChangeCount)
            .AddField("Emote Count", string.Join("\n", emoteCount));
        await ctx.Channel.SendMessageAsync(embed: eb.Build(), components: components.Build()).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
    public async Task SuggestClear()
    {
        var suggests = Service.Suggestions(ctx.Guild.Id);
        if (suggests.Count == 0)
        {
            await ctx.Channel.SendErrorAsync("There are no suggestions to clear.").ConfigureAwait(false);
            return;
        }

        if (await PromptUserConfirmAsync("Are you sure you want to clear all suggestions? ***This cannot be undone.***", ctx.User.Id).ConfigureAwait(false))
        {
            await Service.SuggestReset(ctx.Guild).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("Suggestions cleared.").ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Suggest([Remainder] string suggestion)
    {
        try
        {
            await ctx.Message.DeleteAsync().ConfigureAwait(false);
        }
        catch
        {
            //ignored
        }

        suggestion = suggestion.EscapeWeirdStuff();
        if (suggestion.Length > await Service.GetMaxLength(ctx.Guild.Id))
        {
            var msg = await ctx.Channel.SendErrorAsync(
                $"Cannot send this suggestion as its over the max length (`{await Service.GetMaxLength(ctx.Guild.Id)}`) set in this server!").ConfigureAwait(false);
            msg.DeleteAfter(5);
            return;
        }

        if (suggestion.Length < await Service.GetMinLength(ctx.Guild.Id))
        {
            var message = await ctx.Channel.SendErrorAsync(
                $"Cannot send this suggestion as its under the minimum length (`{await Service.GetMinLength(ctx.Guild.Id)}`) set in this server!").ConfigureAwait(false);
            message.DeleteAfter(5);
            return;
        }

        await Service.SendSuggestion(ctx.Guild, ctx.User as IGuildUser, ctx.Client as DiscordSocketClient,
            suggestion, ctx.Channel as ITextChannel).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageMessages)]
    public async Task Deny(ulong sid, [Remainder] string? reason = null) =>
        await Service.SendDenyEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid,
            ctx.Channel as ITextChannel, reason.EscapeWeirdStuff()).ConfigureAwait(false);

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageMessages)]
    public async Task Accept(ulong sid, [Remainder] string? reason = null) =>
        await Service.SendAcceptEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid,
            ctx.Channel as ITextChannel, reason.EscapeWeirdStuff()).ConfigureAwait(false);

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageMessages)]
    public async Task Implemented(ulong sid, [Remainder] string? reason = null) =>
        await Service.SendImplementEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid,
            ctx.Channel as ITextChannel, reason.EscapeWeirdStuff()).ConfigureAwait(false);

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageMessages)]
    public async Task Consider(ulong sid, [Remainder] string? reason = null) =>
        await Service.SendConsiderEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid,
            ctx.Channel as ITextChannel, reason.EscapeWeirdStuff()).ConfigureAwait(false);
}