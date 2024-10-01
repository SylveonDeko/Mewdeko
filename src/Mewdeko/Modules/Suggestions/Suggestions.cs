using Discord.Commands;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Suggestions.Services;

namespace Mewdeko.Modules.Suggestions;

/// <summary>
///     Commands for managing and interacting with suggestions.
/// </summary>
public partial class Suggestions : MewdekoModuleBase<SuggestionsService>
{
    /// <summary>
    ///     Sets or disables the suggestion channel for the server.
    /// </summary>
    /// <param name="channel">The text channel to set as the suggestion channel. If null, disables suggestions.</param>
    /// <remarks>
    ///     Requires Manage Channels permission. When a channel is set, all future suggestions will be sent to that channel.
    ///     If no channel is provided, the suggestion feature is disabled.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
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
            var chn2 = await ctx.Guild.GetTextChannelAsync(await Service.GetSuggestionChannel(ctx.Guild.Id))
                .ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Your Suggestion channel has been set to {chn2.Mention}")
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Provides detailed information about a specific suggestion.
    /// </summary>
    /// <param name="num">The unique number (ID) of the suggestion to retrieve information for.</param>
    /// <remarks>
    ///     Displays information such as the content of the suggestion, who suggested it, current status, and reaction counts.
    ///     Requires Manage Messages permission.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task SuggestInfo(ulong num)
    {
        var suggest = (await Service.Suggestions(ctx.Guild.Id, num)).FirstOrDefault();
        if (suggest is null)
        {
            await ctx.Channel.SendErrorAsync("That suggestion wasn't found! Please double check the number.", Config)
                .ConfigureAwait(false);
            return;
        }

        var emoteCount = new List<string>();
        var emotes = await Service.GetEmotes(ctx.Guild.Id);
        var count = 0;
        if (emotes is not null and not "disable ")
        {
            foreach (var i in emotes.Split(","))
            {
                emoteCount.Add(
                    $"{i.ToIEmote()} `{await Service.GetCurrentCount(suggest.MessageId, ++count).ConfigureAwait(false)}`");
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
            .AddField("Last Changed By",
                suggest.StateChangeUser == 0 ? "Nobody" : $"<@{suggest.StateChangeUser}> `{suggest.StateChangeUser}`")
            .AddField("State Change Count", suggest.StateChangeCount)
            .AddField("Emote Count", string.Join("\n", emoteCount));
        await ctx.Channel.SendMessageAsync(embed: eb.Build(), components: components.Build()).ConfigureAwait(false);
    }


    /// <summary>
    ///     Clears all suggestions from the server.
    /// </summary>
    /// <remarks>
    ///     Requires Administrator permission. This action cannot be undone, and all suggestions will be permanently removed.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task SuggestClear()
    {
        var suggests = await Service.Suggestions(ctx.Guild.Id);
        if (suggests.Count == 0)
        {
            await ctx.Channel.SendErrorAsync("There are no suggestions to clear.", Config).ConfigureAwait(false);
            return;
        }

        if (await PromptUserConfirmAsync("Are you sure you want to clear all suggestions? ***This cannot be undone.***",
                ctx.User.Id).ConfigureAwait(false))
        {
            await Service.SuggestReset(ctx.Guild).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("Suggestions cleared.").ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Submits a new suggestion to the designated suggestion channel.
    /// </summary>
    /// <param name="suggestion">The content of the suggestion to be submitted.</param>
    /// <remarks>
    ///     The suggestion must meet the minimum and maximum length requirements set for the server.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
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
                    $"Cannot send this suggestion as its over the max length (`{await Service.GetMaxLength(ctx.Guild.Id)}`) set in this server!",
                    Config)
                .ConfigureAwait(false);
            msg.DeleteAfter(5);
            return;
        }

        if (suggestion.Length < await Service.GetMinLength(ctx.Guild.Id))
        {
            var message = await ctx.Channel.SendErrorAsync(
                    $"Cannot send this suggestion as its under the minimum length (`{await Service.GetMinLength(ctx.Guild.Id)}`) set in this server!",
                    Config)
                .ConfigureAwait(false);
            message.DeleteAfter(5);
            return;
        }

        await Service.SendSuggestion(ctx.Guild, ctx.User as IGuildUser, ctx.Client as DiscordShardedClient,
            suggestion, ctx.Channel as ITextChannel).ConfigureAwait(false);
    }

    /// <summary>
    ///     Denies a suggestion, marking it as rejected.
    /// </summary>
    /// <param name="sid">The unique number (ID) of the suggestion to deny.</param>
    /// <param name="reason">Optional. The reason for denying the suggestion.</param>
    /// <remarks>
    ///     Requires Manage Messages permission. The reason for denial is communicated to the suggester if provided.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public Task Deny(ulong sid, [Remainder] string? reason = null)
    {
        return Service.SendDenyEmbed(ctx.Guild, ctx.User, sid,
            ctx.Channel as ITextChannel, reason.EscapeWeirdStuff());
    }

    /// <summary>
    ///     Accepts a suggestion, marking it as approved.
    /// </summary>
    /// <param name="sid">The unique number (ID) of the suggestion to accept.</param>
    /// <param name="reason">Optional. The reason for accepting the suggestion.</param>
    /// <remarks>
    ///     Requires Manage Messages permission. The reason for acceptance is communicated to the suggester if provided.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public Task Accept(ulong sid, [Remainder] string? reason = null)
    {
        return Service.SendAcceptEmbed(ctx.Guild, ctx.User, sid,
            ctx.Channel as ITextChannel, reason.EscapeWeirdStuff());
    }

    /// <summary>
    ///     Marks a suggestion as implemented.
    /// </summary>
    /// <param name="sid">The unique number (ID) of the suggestion to mark as implemented.</param>
    /// <param name="reason">Optional. The reason or details regarding the implementation.</param>
    /// <remarks>
    ///     Requires Manage Messages permission. This status indicates that the suggestion has been put into effect.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public Task Implemented(ulong sid, [Remainder] string? reason = null)
    {
        return Service.SendImplementEmbed(ctx.Guild, ctx.User, sid,
            ctx.Channel as ITextChannel, reason.EscapeWeirdStuff());
    }

    /// <summary>
    ///     Marks a suggestion as being considered.
    /// </summary>
    /// <param name="sid">The unique number (ID) of the suggestion to mark as considered.</param>
    /// <param name="reason">Optional. Comments or reasoning behind considering the suggestion.</param>
    /// <remarks>
    ///     Requires Manage Messages permission. This status indicates that the suggestion is under review.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public Task Consider(ulong sid, [Remainder] string? reason = null)
    {
        return Service.SendConsiderEmbed(ctx.Guild, ctx.User, sid,
            ctx.Channel as ITextChannel, reason.EscapeWeirdStuff());
    }
}