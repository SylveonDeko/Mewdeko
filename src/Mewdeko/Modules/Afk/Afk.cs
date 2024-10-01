using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Afk.Services;

namespace Mewdeko.Modules.Afk;

/// <summary>
///     Module for managing AFK.
/// </summary>
/// <param name="serv">The interactivity service for doing embed pagination.</param>
/// <param name="client">The discord client.</param>
public class Afk(InteractiveService serv, DiscordShardedClient client) : MewdekoModuleBase<AfkService>
{
    /// <summary>
    ///     Enumerates different types of AFK (Away From Keyboard) settings.
    /// </summary>
    public enum AfkTypeEnum
    {
        /// <summary>
        ///     Indicates self-disable AFK.
        /// </summary>
        SelfDisable = 1,

        /// <summary>
        ///     Indicates AFK removed on receiving a message.
        /// </summary>
        OnMessage = 2,

        /// <summary>
        ///     Indicates AFK removed on typing.
        /// </summary>
        OnType = 3,

        /// <summary>
        ///     Indicates AFK removed either by receiving a message or typing.
        /// </summary>
        Either = 4
    }


    /// <summary>
    ///     Sets the user's AFK status with an optional message.
    /// </summary>
    /// <param name="message">The AFK message. If not provided, the user's AFK status will be toggled.</param>
    /// <example>.afk</example>
    /// <example>.afk I'm AFK</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    public async Task SetAfk([Remainder] string? message = null)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        if (message == null)
        {
            var afkmsg = await Service.GetAfk(ctx.Guild.Id, ctx.User.Id);
            if (string.IsNullOrEmpty(afkmsg?.Message))
            {
                await Service.AfkSet(ctx.Guild.Id, ctx.User.Id, "_ _").ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("afk_enabled_no_message").ConfigureAwait(false);
                try
                {
                    var user = await ctx.Guild.GetUserAsync(ctx.User.Id).ConfigureAwait(false);
                    var toset = user.Nickname is null
                        ? $"[AFK] {user.Username.TrimTo(26)}"
                        : $"[AFK] {user.Nickname.Replace("[AFK]", "").TrimTo(26)}";
                    await user.ModifyAsync(x => x.Nickname = toset).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }

                await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
                return;
            }

            await Service.AfkSet(ctx.Guild.Id, ctx.User.Id, "").ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("afk_disabled").ConfigureAwait(false);
            try
            {
                var user = await ctx.Guild.GetUserAsync(ctx.User.Id).ConfigureAwait(false);
                await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", "")).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
            return;
        }

        if (message.Length != 0 && message.Length > await Service.GetAfkLength(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("afk_message_too_long", Service.GetAfkLength(ctx.Guild.Id))
                .ConfigureAwait(false);
            return;
        }

        await Service.AfkSet(ctx.Guild.Id, ctx.User.Id, message.EscapeWeirdStuff()).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("afk_enabled", message).ConfigureAwait(false);
        try
        {
            var user1 = await ctx.Guild.GetUserAsync(ctx.User.Id).ConfigureAwait(false);
            var toset1 = user1.Nickname is null
                ? $"[AFK] {user1.Username.TrimTo(26)}"
                : $"[AFK] {user1.Nickname.Replace("[AFK]", "").TrimTo(26)}";
            await user1.ModifyAsync(x => x.Nickname = toset1).ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }

        await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
    }


    /// <summary>
    ///     Displays the current auto-deletion duration for AFK messages.
    /// </summary>
    /// <example>.afkdel</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AfkDel()
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        if (await Service.GetAfkDel(ctx.Guild.Id) == 0)
        {
            await ReplyConfirmLocalizedAsync("afk_messages_nodelete").ConfigureAwait(false);
            return;
        }

        await ReplyConfirmLocalizedAsync("afk_messages_delete",
                TimeSpan.FromSeconds(await Service.GetAfkDel(ctx.Guild.Id)).Humanize(maxUnit: TimeUnit.Minute))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the auto-deletion duration for AFK messages.
    /// </summary>
    /// <param name="num">The duration in seconds. Set to 0 to disable auto-deletion.</param>
    /// <example>.afkdel 60</example>
    [Cmd]
    [Aliases]
    [Priority(1)]
    public async Task AfkDel(int num)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        switch (num)
        {
            case < 0:
                return;
            case 0:
                await Service.AfkDelSet(ctx.Guild, 0).ConfigureAwait(false);
                await ConfirmLocalizedAsync("afk_deletion_disabled").ConfigureAwait(false);
                break;
            default:
                await Service.AfkDelSet(ctx.Guild, num).ConfigureAwait(false);
                await ConfirmLocalizedAsync("afk_deletion_set", num).ConfigureAwait(false);
                break;
        }
    }


    /// <summary>
    ///     Sets a timed AFK status with a custom message.
    /// </summary>
    /// <param name="time">The duration for the AFK status.</param>
    /// <param name="message">The custom message for the AFK status.</param>
    /// <example>.afk 1h I'm AFK</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    public async Task TimedAfk(StoopidTime time, [Remainder] string message)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
        }

        if (message.Length != 0 && message.Length > await Service.GetAfkLength(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("afk_message_too_long", Service.GetAfkLength(ctx.Guild.Id))
                .ConfigureAwait(false);
            return;
        }

        await Service.AfkSet(ctx.Guild.Id, ctx.User.Id, message, true, DateTime.UtcNow + time.Time);
        await ConfirmLocalizedAsync("afk_timed_set",
            TimestampTag.FromDateTimeOffset(DateTimeOffset.UtcNow + time.Time, TimestampTagStyles.Relative), message);
    }

    /// <summary>
    ///     Sets a custom AFK embed that will be displayed when a user is AFK. Use "-" to reset to the default embed. Check
    ///     https://eb.mewdeko.tech for the embed builder and http://mewdeko.tech/placeholders for placeholders.
    /// </summary>
    /// <param name="embedCode">The custom message to set. Use "-" to reset to the default message.</param>
    /// <example>.afkmsg -</example>
    /// <example>.afkmsg embedcode</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CustomAfkMessage([Remainder] string embedCode)
    {
        if (embedCode == "-")
        {
            await Service.SetCustomAfkMessage(ctx.Guild, "-").ConfigureAwait(false);
            await ConfirmLocalizedAsync("afk_message_default").ConfigureAwait(false);
            return;
        }

        await Service.SetCustomAfkMessage(ctx.Guild, embedCode).ConfigureAwait(false);
        await ConfirmLocalizedAsync("afk_message_updated").ConfigureAwait(false);
    }

    /// <summary>
    ///     Displays a list of active users who are currently AFK.
    /// </summary>
    /// <example>.afklist</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    public async Task GetActiveAfks()
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        var afks = await Service.GetAfkUsers(ctx.Guild).ConfigureAwait(false);
        if (afks.Count == 0)
        {
            await ErrorLocalizedAsync("afk_users_none").ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(afks.ToArray().Length / 20)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithOkColor()
                .WithTitle($"{Format.Bold(GetText("active_afks"))} - {afks.ToArray().Length}")
                .WithDescription(string.Join("\n", afks.ToArray().Skip(page * 20).Take(20)));
        }
    }

    /// <summary>
    ///     Displays the AFK status of a specific user.
    /// </summary>
    /// <param name="user">The user to check the AFK status for.</param>
    /// <example>.afkview @user</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task AfkView(IGuildUser user)
    {
        if (!await CheckRoleHierarchy(user))
            return;
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        if (!await Service.IsAfk(user.Guild.Id, user.Id))
        {
            await ErrorLocalizedAsync("afk_user_none").ConfigureAwait(false);
            return;
        }

        var msg = await Service.GetAfk(user.Guild.Id, user.Id);
        await ConfirmLocalizedAsync("afk_user", user, msg.Message);
    }


    /// <summary>
    ///     Lists the text channels where the AFK message doesnt display.
    /// </summary>
    /// <example>.afkdisabledlist</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task AfkDisabledList()
    {
        var mentions = new List<string>();
        var chans = await Service.GetDisabledAfkChannels(ctx.Guild.Id);
        if (string.IsNullOrEmpty(chans) || chans == "0")
        {
            await ErrorLocalizedAsync("afk_disabled_none").ConfigureAwait(false);
            return;
        }

        foreach (var i in chans.Split(","))
        {
            var role = await ctx.Guild.GetTextChannelAsync(Convert.ToUInt64(i)).ConfigureAwait(false);
            mentions.Add(role.Mention);
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(mentions.ToArray().Length / 20)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithOkColor()
                .WithTitle(
                    $"{Format.Bold(GetText("disabled_afk_channels"))} - {mentions.ToArray().Length}")
                .WithDescription(string.Join("\n", mentions.ToArray().Skip(page * 20).Take(20)));
        }
    }

    /// <summary>
    ///     Sets the maximum length of all AFK messages.
    /// </summary>
    /// <param name="num">The maximum length you want to set.</param>
    /// <example>.afklength 100</example>
    [Cmd]
    [Aliases]
    [Priority(0)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task AfkLength(int num)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        if (num > 4096)
        {
            await ErrorLocalizedAsync("afk_max_length_too_large").ConfigureAwait(false);
        }
        else
        {
            await Service.AfkLengthSet(ctx.Guild, num).ConfigureAwait(false);
            await ConfirmLocalizedAsync("afk_max_length_set", num).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets the type of AFK behavior for the guild.
    /// </summary>
    /// <param name="afkTypeEnum">The type of AFK behavior to set. <see cref="AfkTypeEnum" /></param>
    /// <example>.afktype 1</example>
    [Cmd]
    [Aliases]
    [Priority(1)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task AfkType(AfkTypeEnum afkTypeEnum)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        await Service.AfkTypeSet(ctx.Guild, (int)afkTypeEnum).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("afk_type_set", afkTypeEnum).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the timeout duration before a user is no longer considered afk. Triggers when a user sends a message or types
    ///     in a channel.
    /// </summary>
    /// <param name="time">The timeout duration for the AFK status.</param>
    /// <example>.afktimeout 1h</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task AfkTimeout(StoopidTime time)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        if (time.Time < TimeSpan.FromSeconds(1) || time.Time > TimeSpan.FromHours(2))
        {
            await ErrorLocalizedAsync("afk_timeout_invalid").ConfigureAwait(false);
            return;
        }

        await Service.AfkTimeoutSet(ctx.Guild, Convert.ToInt32(time.Time.TotalSeconds)).ConfigureAwait(false);
        await ConfirmLocalizedAsync("afk_timeout_set", time.Time.Humanize());
    }


    /// <summary>
    ///     Removes the specified channels from the afk message blacklist.
    /// </summary>
    /// <param name="chan">The text channels for which to remove from the afk message blacklist.</param>
    /// <example>.afkundisable #channel</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task AfkUndisable(params ITextChannel[] chan)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        var mentions = new List<string>();
        var toremove = new List<string>();
        var chans = await Service.GetDisabledAfkChannels(ctx.Guild.Id);
        if (string.IsNullOrWhiteSpace(chans) || chans == "0")
        {
            await ErrorLocalizedAsync("afk_disabled_channels_none").ConfigureAwait(false);
            return;
        }

        var e = chans.Split(",");
        var list = e.ToList();
        foreach (var i in chan)
        {
            if (e.Contains(i.Id.ToString()))
            {
                toremove.Add(i.Id.ToString());
                mentions.Add(i.Mention);
            }
        }

        if (mentions.Count == 0)
        {
            await ErrorLocalizedAsync("afk_disabled_channels_noneset").ConfigureAwait(false);
            return;
        }

        if (!list.Except(toremove).Any())
        {
            await Service.AfkDisabledSet(ctx.Guild, "0").ConfigureAwait(false);
            await ErrorLocalizedAsync("afk_ignoring_no_longer_disabled").ConfigureAwait(false);
            return;
        }

        await Service.AfkDisabledSet(ctx.Guild, string.Join(",", list.Except(toremove))).ConfigureAwait(false);
        await ConfirmLocalizedAsync("afk_disabled_channels_removed", string.Join(",", mentions));
    }

    /// <summary>
    ///     Sets the channels where the AFK message will not display.
    /// </summary>
    /// <param name="chan">Channels you want to add to the afk message blacklist.</param>
    /// <example>.afkdisable #channel</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task AfkDisable(params ITextChannel[] chan)
    {
        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        var list = new HashSet<string>();
        // ReSharper disable once CollectionNeverQueried.Local
        var newchans = new HashSet<string>();
        var mentions = new HashSet<string>();
        if (await Service.GetDisabledAfkChannels(ctx.Guild.Id) == "0" ||
            string.IsNullOrWhiteSpace(await Service.GetDisabledAfkChannels(ctx.Guild.Id)))
        {
            foreach (var i in chan)
            {
                list.Add(i.Id.ToString());
                mentions.Add(i.Mention);
            }

            await Service.AfkDisabledSet(ctx.Guild, string.Join(",", list)).ConfigureAwait(false);
            await ConfirmLocalizedAsync("afk_disabled_in", string.Join(",", mentions));
        }
        else
        {
            var e = await Service.GetDisabledAfkChannels(ctx.Guild.Id);
            var w = e.Split(",");
            foreach (var i in w)
                list.Add(i);

            foreach (var i in chan)
            {
                if (!w.Contains(i.Id.ToString()))
                {
                    list.Add(i.Id.ToString());
                    mentions.Add(i.Mention);
                }

                newchans.Add(i.Id.ToString());
            }

            if (mentions.Count > 0)
            {
                await ErrorLocalizedAsync("afk_already_in_list");
                return;
            }

            await Service.AfkDisabledSet(ctx.Guild, string.Join(",", list)).ConfigureAwait(false);
            await ConfirmLocalizedAsync("afk_channels_updated", string.Join(",", mentions));
        }
    }

    /// <summary>
    ///     Removes the AFK status for one or more users.
    /// </summary>
    /// <param name="user">The user(s) you want to remove afk from.</param>
    /// <example>.afkremove @user</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageMessages)]
    [Priority(0)]
    public async Task AfkRemove(params IGuildUser[] user)
    {
        foreach (var i in user)
        {
            if (!await CheckRoleHierarchy(i))
                return;
        }

        if (Environment.GetEnvironmentVariable("AFK_CACHED") != "1")
        {
            await ReplyErrorLocalizedAsync("afk_still_starting").ConfigureAwait(false);
            return;
        }

        var users = 0;
        var erroredusers = 0;
        foreach (var i in user)
        {
            var curafk = await Service.IsAfk(ctx.Guild.Id, i.Id);
            if (!curafk)
                continue;

            if (!await CheckRoleHierarchy(i, false).ConfigureAwait(false))
            {
                erroredusers++;
                continue;
            }

            await Service.AfkSet(ctx.Guild.Id, i.Id, "").ConfigureAwait(false);
            users++;
            try
            {
                await i.ModifyAsync(x => x.Nickname = i.Nickname.Replace("[AFK]", "")).ConfigureAwait(false);
            }
            catch
            {
                //ignored
            }
        }

        switch (users)
        {
            case > 0 when erroredusers == 0:
                await ReplyConfirmLocalizedAsync("afk_rm_multi_success", users).ConfigureAwait(false);
                break;
            case 0 when erroredusers == 0:
                await ReplyErrorLocalizedAsync("afk_rm_fail_noafk").ConfigureAwait(false);
                break;
            case > 0 when erroredusers > 0:
                await ReplyConfirmLocalizedAsync("afk_success_hierarchy", users, erroredusers).ConfigureAwait(false);
                break;
            case 0 when erroredusers >= 1:
                await ReplyErrorLocalizedAsync("afk_rm_fail_hierarchy").ConfigureAwait(false);
                break;
        }
    }
}