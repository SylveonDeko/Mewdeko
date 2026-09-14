using DataModel;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Common;
using Mewdeko.Modules.Moderation.Services;
using Swan;
using PermValue = Discord.PermValue;
using UserExtensions = Mewdeko.Extensions.UserExtensions;

namespace Mewdeko.Modules.Moderation;

public partial class SlashPunishCommands
{
    /// <summary>
    ///     The ban actions whose message purge can be configured, plus an option covering every action.
    /// </summary>
    public enum BanPruneActionChoice
    {
        /// <summary>
        ///     Every ban action.
        /// </summary>
        All,

        /// <summary>
        ///     A regular ban issued through the ban command.
        /// </summary>
        Ban,

        /// <summary>
        ///     A ban by user id, where the target need not be in the server.
        /// </summary>
        HackBan,

        /// <summary>
        ///     A ban that lifts itself after a set duration.
        /// </summary>
        TempBan,

        /// <summary>
        ///     A ban immediately followed by an unban.
        /// </summary>
        SoftBan,

        /// <summary>
        ///     A bulk ban issued through the mass ban command.
        /// </summary>
        MassBan,

        /// <summary>
        ///     Banning everyone sharing an avatar hash.
        /// </summary>
        BanByHash,

        /// <summary>
        ///     Banning every member holding a role.
        /// </summary>
        BanInRole,

        /// <summary>
        ///     Banning every account created or joined under an age threshold.
        /// </summary>
        BanUnder,

        /// <summary>
        ///     A ban handed out automatically once a member passes the warn threshold.
        /// </summary>
        WarnPunish,

        /// <summary>
        ///     A ban triggered by a member gaining an auto-ban role.
        /// </summary>
        AutoBanRole,

        /// <summary>
        ///     A ban triggered by the role monitor catching a permission violation.
        /// </summary>
        RoleMonitor,

        /// <summary>
        ///     A ban triggered by a word, link, or invite filter.
        /// </summary>
        Filter,

        /// <summary>
        ///     A ban issued while the server is locked down.
        /// </summary>
        Lockdown,

        /// <summary>
        ///     A ban issued from the dashboard or the mobile app.
        /// </summary>
        Dashboard
    }

    /// <summary>
    ///     Slash commands for muting and unmuting users.
    /// </summary>
    /// <param name="logger">The logger instance for structured logging.</param>
    [Group("mute", "Mute and unmute users")]
    public class ModerationMute(ILogger<ModerationMute> logger) : MewdekoSlashSubmodule<MuteService>
    {
        /// <summary>
        ///     Mutes a user from text and voice, optionally for a specified amount of time.
        /// </summary>
        /// <param name="user">The user to mute</param>
        /// <param name="time">The amount of time to mute the user for, between 1 minute and 90 days</param>
        /// <param name="reason">The reason for the mute</param>
        [SlashCommand("mute", "Mutes a user from text and voice, optionally for a set time")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles | GuildPermission.MuteMembers)]
        public async Task Mute([Summary("user", "The user to mute")] IGuildUser user,
            [Summary("time", "How long to mute for, for example 1h30m")]
            TimeSpan? time = null,
            [Summary("reason", "The reason for the mute")]
            string reason = "")
        {
            if (time is not null && (time.Value < TimeSpan.FromMinutes(1) || time.Value > TimeSpan.FromDays(90)))
                return;
            try
            {
                if (!await CheckRoleHierarchy(user))
                    return;

                if (time is null)
                {
                    await Service.MuteUser(user, ctx.User, reason: reason).ConfigureAwait(false);
                    await ReplyConfirmAsync(Strings.UserMuted(ctx.Guild.Id, Format.Bold(user.ToString())))
                        .ConfigureAwait(false);
                    return;
                }

                await Service.TimedMute(user, ctx.User, time.Value, reason: reason).ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.UserMutedTime(ctx.Guild.Id, Format.Bold(user.ToString()),
                    time.Value.Humanize())).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error in mute command");
                await ReplyErrorAsync(Strings.MuteError(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Unmutes a user.
        /// </summary>
        /// <param name="user">The user to unmute</param>
        /// <param name="reason">The reason for the unmute</param>
        [SlashCommand("unmute", "Unmutes a user from text and voice")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles | GuildPermission.MuteMembers)]
        public async Task Unmute([Summary("user", "The user to unmute")] IGuildUser user,
            [Summary("reason", "The reason for the unmute")]
            string reason = "")
        {
            try
            {
                if (!await CheckRoleHierarchy(user))
                    return;
                await Service.UnmuteUser(user.GuildId, user.Id, ctx.User, reason: reason).ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.UserUnmuted(ctx.Guild.Id, Format.Bold(user.ToString())))
                    .ConfigureAwait(false);
            }
            catch
            {
                await ReplyErrorAsync(Strings.MuteError(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Mutes a user in chat and not voice, optionally for a specified amount of time.
        /// </summary>
        /// <param name="user">The user to mute</param>
        /// <param name="time">The amount of time to mute the user for, between 1 minute and 49 days</param>
        /// <param name="reason">The reason for the mute</param>
        [SlashCommand("chat-mute", "Mutes a user in chat only, optionally for a set time")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task ChatMute([Summary("user", "The user to mute")] IGuildUser user,
            [Summary("time", "How long to mute for, for example 1h30m")]
            TimeSpan? time = null,
            [Summary("reason", "The reason for the mute")]
            string reason = "")
        {
            if (time is not null && (time.Value < TimeSpan.FromMinutes(1) || time.Value > TimeSpan.FromDays(49)))
                return;
            try
            {
                if (!await CheckRoleHierarchy(user))
                    return;

                if (time is null)
                {
                    await Service.MuteUser(user, ctx.User, MuteType.Chat, reason).ConfigureAwait(false);
                    await ReplyConfirmAsync(Strings.UserChatMute(ctx.Guild.Id, Format.Bold(user.ToString())))
                        .ConfigureAwait(false);
                    return;
                }

                await Service.TimedMute(user, ctx.User, time.Value, MuteType.Chat, reason).ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.UserChatMuteTime(ctx.Guild.Id, Format.Bold(user.ToString()),
                    time.Value.Humanize())).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex.ToString());
                await ReplyErrorAsync(Strings.MuteError(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Unmutes a user in chat.
        /// </summary>
        /// <param name="user">The user to unmute</param>
        /// <param name="reason">The reason for the unmute</param>
        [SlashCommand("chat-unmute", "Unmutes a user in chat")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task ChatUnmute([Summary("user", "The user to unmute")] IGuildUser user,
            [Summary("reason", "The reason for the unmute")]
            string reason = "")
        {
            try
            {
                if (!await CheckRoleHierarchy(user))
                    return;
                await Service.UnmuteUser(user.Guild.Id, user.Id, ctx.User, MuteType.Chat, reason)
                    .ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.UserChatUnmute(ctx.Guild.Id, Format.Bold(user.ToString())))
                    .ConfigureAwait(false);
            }
            catch
            {
                await ReplyErrorAsync(Strings.MuteError(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Mutes a user in voice and not chat, optionally for a specified amount of time.
        /// </summary>
        /// <param name="user">The user to mute</param>
        /// <param name="time">The amount of time to mute the user for, between 1 minute and 49 days</param>
        /// <param name="reason">The reason for the mute</param>
        [SlashCommand("voice-mute", "Mutes a user in voice only, optionally for a set time")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.MuteMembers)]
        public async Task VoiceMute([Summary("user", "The user to mute")] IGuildUser user,
            [Summary("time", "How long to mute for, for example 1h30m")]
            TimeSpan? time = null,
            [Summary("reason", "The reason for the mute")]
            string reason = "")
        {
            if (time is not null && (time.Value < TimeSpan.FromMinutes(1) || time.Value > TimeSpan.FromDays(49)))
                return;
            try
            {
                if (!await CheckRoleHierarchy(user))
                    return;

                if (time is null)
                {
                    await Service.MuteUser(user, ctx.User, MuteType.Voice, reason).ConfigureAwait(false);
                    await ReplyConfirmAsync(Strings.UserVoiceMute(ctx.Guild.Id, Format.Bold(user.ToString())))
                        .ConfigureAwait(false);
                    return;
                }

                await Service.TimedMute(user, ctx.User, time.Value, MuteType.Voice, reason).ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.UserVoiceMuteTime(ctx.Guild.Id, Format.Bold(user.ToString()),
                    time.Value.Humanize())).ConfigureAwait(false);
            }
            catch
            {
                await ReplyErrorAsync(Strings.MuteError(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Unmutes a user in voice.
        /// </summary>
        /// <param name="user">The user to unmute</param>
        /// <param name="reason">The reason for the unmute</param>
        [SlashCommand("voice-unmute", "Unmutes a user in voice")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.MuteMembers)]
        public async Task VoiceUnmute([Summary("user", "The user to unmute")] IGuildUser user,
            [Summary("reason", "The reason for the unmute")]
            string reason = "")
        {
            try
            {
                if (!await CheckRoleHierarchy(user))
                    return;
                await Service.UnmuteUser(user.GuildId, user.Id, ctx.User, MuteType.Voice, reason)
                    .ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.UserVoiceUnmute(ctx.Guild.Id, Format.Bold(user.ToString())))
                    .ConfigureAwait(false);
            }
            catch
            {
                await ReplyErrorAsync(Strings.MuteError(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Whats there not to understand? Shuts a user the fuck up in the current channel.
        /// </summary>
        /// <param name="user">The user to shut up</param>
        /// <param name="time">For how long to shut a user the fuck up for.</param>
        [SlashCommand("stfu", "Stops a user from sending messages in this channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.MuteMembers)]
        public async Task Stfu([Summary("user", "The user to shut up")] IGuildUser user,
            [Summary("time", "How long to shut them up for, for example 1h30m")]
            TimeSpan? time = null)
        {
            if (!await CheckRoleHierarchy(user))
                return;
            var channel = ctx.Channel as SocketGuildChannel;
            var currentPerms = channel.GetPermissionOverwrite(user) ?? new OverwritePermissions();
            await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(sendMessages: PermValue.Deny))
                .ConfigureAwait(false);
            if (time is null)
            {
                await ConfirmAsync(Strings.UserChannelMuted(ctx.Guild.Id, user)).ConfigureAwait(false);
                return;
            }

            await ConfirmAsync(Strings.UserChannelMutedTime(ctx.Guild.Id, user, time.Value.Humanize()))
                .ConfigureAwait(false);
            var delay = time.Value;
            _ = Task.Run(async () =>
            {
                await Task.Delay(delay).ConfigureAwait(false);
                try
                {
                    await channel.AddPermissionOverwriteAsync(user,
                        currentPerms.Modify(sendMessages: PermValue.Inherit)).ConfigureAwait(false);
                }
                catch
                {
                }
            });
        }

        /// <summary>
        ///     Unshuts a user up in the current channel.
        /// </summary>
        /// <param name="user">The user to unshut up</param>
        [SlashCommand("unstfu", "Lets a user send messages in this channel again")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.MuteMembers)]
        public async Task Unstfu([Summary("user", "The user to unshut up")] IGuildUser user)
        {
            if (!await CheckRoleHierarchy(user))
                return;
            var channel = ctx.Channel as SocketGuildChannel;
            var currentPerms = channel.GetPermissionOverwrite(user) ?? new OverwritePermissions();
            await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(sendMessages: PermValue.Inherit))
                .ConfigureAwait(false);
            await ConfirmAsync(Strings.UserChannelUnmuted(ctx.Guild.Id, user)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Unmutes all users in the guild. DANGEROUS!!!!!!!!!!!!!!!!
        /// </summary>
        /// <param name="reason">The reason you would want to commit such an atrocity!</param>
        [SlashCommand("unmute-all", "Unmutes every muted user in the server")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task UnmuteAll([Summary("reason", "The reason for the unmute")] string? reason = null)
        {
            await DeferAsync();
            var muteRole = await Service.GetMuteRole(ctx.Guild).ConfigureAwait(false);
            var users = (await ctx.Guild.GetUsersAsync().ConfigureAwait(false))
                .Where(x => x.RoleIds.Contains(muteRole.Id))
                .ToList();
            if (users.Count == 0)
            {
                await ErrorAsync(Strings.NoMutedUsers(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (!await PromptUserConfirmAsync(
                    new EmbedBuilder().WithOkColor()
                        .WithDescription(Strings.UnmuteAllConfirm(ctx.Guild.Id)),
                    ctx.User.Id).ConfigureAwait(false))
                return;

            if (reason is null && await PromptUserConfirmAsync(
                        new EmbedBuilder().WithOkColor()
                            .WithDescription(Strings.UnmuteAllReasonPrompt(ctx.Guild.Id)), ctx.User.Id)
                    .ConfigureAwait(false))
            {
                await ConfirmAsync(Strings.UnmuteAllReasonRequest(ctx.Guild.Id)).ConfigureAwait(false);
                reason = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
            }

            await ConfirmAsync(Strings.UnmuteAllProgress(ctx.Guild.Id, users.Count)).ConfigureAwait(false);
            foreach (var i in users)
            {
                try
                {
                    if (reason is null)
                        await Service.UnmuteUser(i.GuildId, i.Id, ctx.User).ConfigureAwait(false);
                    else
                        await Service.UnmuteUser(i.GuildId, i.Id, ctx.User, MuteType.All, reason)
                            .ConfigureAwait(false);
                }
                catch
                {
                }
            }

            await ConfirmAsync(Strings.UnmuteAllComplete(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the mute role for the guild, or shows the current one when no role is given.
        /// </summary>
        /// <param name="role">The role to use as the mute role</param>
        [SlashCommand("mute-role", "Sets or shows the mute role")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task MuteRole([Summary("role", "The role to use as the mute role")] IRole? role = null)
        {
            if (role is null)
            {
                var muteRole = await Service.GetMuteRole(ctx.Guild).ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.MuteRole(ctx.Guild.Id, muteRole.Mention)).ConfigureAwait(false);
                return;
            }

            if (ctx.User.Id != ctx.Guild.OwnerId &&
                role.Position >= ((SocketGuildUser)ctx.User).Roles.Max(x => x.Position))
            {
                await ReplyErrorAsync(Strings.InsufPermsU(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetMuteRoleAsync(ctx.Guild.Id, role.Name).ConfigureAwait(false);

            await ReplyConfirmAsync(Strings.MuteRoleSet(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles whether to remove roles on mute.
        /// </summary>
        /// <param name="enabled">Whether roles should be removed when a user is muted</param>
        [SlashCommand("remove-on-mute", "Sets whether roles are removed when a user is muted")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RemoveOnMute(
            [Summary("enabled", "Whether roles should be removed on mute")]
            bool enabled)
        {
            if (enabled)
            {
                await Service.Removeonmute(ctx.Guild, "y").ConfigureAwait(false);
                await ConfirmAsync(Strings.RemoveRolesMuteEnabled(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                await Service.Removeonmute(ctx.Guild, "n").ConfigureAwait(false);
                await ConfirmAsync(Strings.RemoveRolesMuteDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Slash commands for managing mini warnings.
    /// </summary>
    /// <param name="dbFactory">The db provider</param>
    /// <param name="interactivity">Fergun.Interactive paginator builder</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    [Group("mwarn", "Mini warnings")]
    public class ModerationMiniWarn(
        IDataConnectionFactory dbFactory,
        InteractiveService interactivity,
        ILogger<ModerationMiniWarn> logger) : MewdekoSlashSubmodule<UserPunishService2>
    {
        /// <summary>
        ///     Sets the mini warnlog channel.
        /// </summary>
        /// <param name="channel">The channel to set</param>
        [SlashCommand("set-channel", "Sets the channel mini warnings are logged to")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task SetMWarnChannel(
            [Summary("channel", "The channel to log mini warnings to")]
            ITextChannel channel)
        {
            if (string.IsNullOrWhiteSpace(channel.Name))
                return;
            var mWarnlogChannel = await Service.GetMWarnlogChannel(ctx.Guild.Id);
            if (mWarnlogChannel == channel.Id)
            {
                await ErrorAsync(Strings.MiniWarnlogChannelAlreadySet(ctx.Guild.Id));
                return;
            }

            if (mWarnlogChannel == 0)
            {
                await Service.SetMWarnlogChannelId(ctx.Guild, channel).ConfigureAwait(false);
                await ConfirmAsync(Strings.MiniWarnlogChannelSet(ctx.Guild.Id, channel.Mention));
                return;
            }

            var oldWarnChannel = await ctx.Guild.GetTextChannelAsync(mWarnlogChannel).ConfigureAwait(false);
            await Service.SetMWarnlogChannelId(ctx.Guild, channel).ConfigureAwait(false);
            await ConfirmAsync(Strings.MiniWarnlogChannelChanged(ctx.Guild.Id,
                oldWarnChannel?.Mention ?? mWarnlogChannel.ToString(), channel.Mention));
        }

        /// <summary>
        ///     Mini Warns a user.
        /// </summary>
        /// <param name="user">The user to warn</param>
        /// <param name="reason">The reason for the warning</param>
        [SlashCommand("warn", "Gives a user a mini warning")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.MuteMembers)]
        public async Task MWarn([Summary("user", "The user to warn")] IGuildUser user,
            [Summary("reason", "The reason for the warning")]
            string? reason = null)
        {
            if (ctx.User.Id != user.Guild.OwnerId
                && user.GetRoles().Select(r => r.Position).Max() >=
                ((IGuildUser)ctx.User).GetRoles().Select(r => r.Position).Max())
            {
                await ReplyErrorAsync(Strings.Hierarchy(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await DeferAsync();
            try
            {
                await (await user.CreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(new EmbedBuilder()
                        .WithDescription(Strings.MiniWarnedInGuild(ctx.Guild.Id, ctx.Guild))
                        .AddField(efb => efb.WithName(Strings.MiniWarnModerator(ctx.Guild.Id))
                            .WithValue(ctx.User.ToString()))
                        .AddField(efb => efb.WithName(Strings.MiniWarnReason(ctx.Guild.Id))
                            .WithValue(reason ?? "-")))
                    .ConfigureAwait(false);
            }
            catch
            {
            }

            WarningPunishment2? punishment;
            try
            {
                punishment = await Service.Warn(ctx.Guild, user.Id, ctx.User, reason).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex.Message);
                await ReplyErrorAsync(Strings.CantApplyPunishment(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(
                punishment == null
                    ? Strings.UserMiniWarned(ctx.Guild.Id, Format.Bold(user.ToString()))
                    : Strings.UserMiniWarnedAndPunished(ctx.Guild.Id, Format.Bold(user.ToString()),
                        Format.Bold(punishment.Punishment.ToString())));

            if (await Service.GetMWarnlogChannel(ctx.Guild.Id) != 0)
            {
                await using var dbContext = await dbFactory.CreateConnectionAsync();

                var warnings = await dbContext.Warnings2s
                    .Where(x => x.UserId == user.Id && x.GuildId == ctx.Guild.Id)
                    .CountAsync(w => !w.Forgiven && w.UserId == user.Id);
                var condition = punishment != null;
                var punishtime = condition ? TimeSpan.FromMinutes(punishment.Time).Humanize() : " ";
                var punishaction = condition ? punishment.Punishment.ToString() : "None";
                var channel = await ctx.Guild.GetTextChannelAsync(await Service.GetMWarnlogChannel(ctx.Guild.Id))
                    .ConfigureAwait(false);
                var originalResponse = await ctx.Interaction.GetOriginalResponseAsync().ConfigureAwait(false);
                await channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithThumbnailUrl(user.RealAvatarUrl().ToString())
                    .WithTitle(Strings.MiniWarnLogTitle(ctx.Guild.Id, ctx.User))
                    .WithDescription(Strings.MiniWarnLogDescription(
                        ctx.Guild.Id,
                        user.Username,
                        user.Discriminator,
                        user.Id,
                        warnings,
                        punishaction,
                        punishtime,
                        reason,
                        originalResponse.GetJumpUrl()
                    )));
            }
        }

        /// <summary>
        ///     Sets the mini warn expire time.
        /// </summary>
        /// <param name="days">The number of days until mini warnings expire, 0 to disable</param>
        /// <param name="action">The action to take when a warn expires</param>
        [SlashCommand("expire", "Sets when mini warnings expire in days")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task MWarnExpire(
            [Summary("days", "Days until mini warnings expire, 0 to disable")] [MinValue(0)] [MaxValue(366)]
            int days,
            [Summary("action", "Whether to clear or delete expired warnings")]
            WarnExpireAction action = WarnExpireAction.Clear)
        {
            if (days is < 0 or > 366)
                return;

            await DeferAsync();

            await Service.WarnExpireAsync(ctx.Guild.Id, days, action).ConfigureAwait(false);
            await ReplyConfirmAsync(
                days == 0
                    ? Strings.MiniWarnExpireReset(ctx.Guild.Id)
                    : action == WarnExpireAction.Delete
                        ? Strings.MiniWarnExpireSetDelete(ctx.Guild.Id, Format.Bold(days.ToString()))
                        : Strings.MiniWarnExpireSetClear(ctx.Guild.Id, Format.Bold(days.ToString()))
            );
        }

        /// <summary>
        ///     Gets the mini warnlog for a user. Viewing another user's log requires the mute members permission.
        /// </summary>
        /// <param name="user">The user to get the warnlog for. Defaults to yourself.</param>
        /// <param name="userId">The id of a user to get the warnlog for, used when the user is not in the server.</param>
        /// <param name="page">The page number</param>
        [SlashCommand("log", "Shows the mini warnings of a user")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task MWarnlog([Summary("user", "The user to show, defaults to you")] IGuildUser? user = null,
            [Summary("user-id", "The id of a user not in the server")]
            ulong? userId = null,
            [Summary("page", "The page to show")] [MinValue(1)]
            int page = 1)
        {
            var targetId = userId ?? user?.Id ?? ctx.User.Id;
            if (ctx.User.Id != targetId && !((IGuildUser)ctx.User).GuildPermissions.MuteMembers)
            {
                await EphemeralReplyErrorAsync(Strings.MissingPermissionsViewWarns(ctx.Guild.Id));
                return;
            }

            await InternalWarnlog(targetId, page - 1);
        }

        private async Task InternalWarnlog(ulong userId, int page)
        {
            if (page < 0)
                return;
            var warnings = await Service.UserWarnings(ctx.Guild.Id, userId);

            warnings = warnings.Skip(page * 9)
                .Take(9)
                .ToArray();

            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.MiniWarnlogFor(ctx.Guild.Id,
                    (ctx.Guild as SocketGuild)?.GetUser(userId)?.ToString() ?? userId.ToString()))
                .WithFooter(efb => efb.WithText(Strings.MiniWarnlogPage(ctx.Guild.Id, page + 1)));

            if (warnings.Length == 0)
            {
                embed.WithDescription(Strings.MiniWarningsNone(ctx.Guild.Id));
            }
            else
            {
                var i = page * 9;
                foreach (var w in warnings)
                {
                    i++;
                    var name = Strings.MiniWarnedOnBy(
                        ctx.Guild.Id,
                        $"<t:{w.DateAdded.Value.ToUnixEpochDate()}:D>",
                        $"<t:{w.DateAdded.Value.ToUnixEpochDate()}:T>",
                        w.Moderator
                    );
                    if (w.Forgiven)
                        name = $"{Format.Strikethrough(name)} {Strings.MiniWarnClearedBy(ctx.Guild.Id, w.ForgivenBy)}";

                    embed.AddField(x => x
                        .WithName($"#`{i}` {name}")
                        .WithValue(w.Reason.TrimTo(1020)));
                }
            }

            await RespondAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Gets the mini warnlog for all users.
        /// </summary>
        [SlashCommand("log-all", "Shows how many mini warnings every user has")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.MuteMembers)]
        public async Task MWarnlogAll()
        {
            var warnings = await Service.WarnlogAll(ctx.Guild.Id);

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(warnings.Length / 15)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var ws = warnings.Skip(page * 15)
                    .Take(15)
                    .ToArray()
                    .Select(x =>
                    {
                        var all = x.Count();
                        var forgiven = x.Count(y => y.Forgiven);
                        var total = all - forgiven;
                        var usr = ((SocketGuild)ctx.Guild).GetUser(x.Key);
                        return $"{usr?.ToString() ?? x.Key.ToString()} | {total} ({all} - {forgiven})";
                    });

                return new PageBuilder().WithOkColor()
                    .WithTitle(Strings.MiniWarningsList(ctx.Guild.Id))
                    .WithDescription(string.Join("\n", ws));
            }
        }

        /// <summary>
        ///     Clears a user's mini warnings. If index is specified, clears only that warning.
        /// </summary>
        /// <param name="user">The user to clear the warnings for</param>
        /// <param name="userId">The id of a user to clear the warnings for, used when the user is not in the server</param>
        /// <param name="index">The index of the warning to clear, 0 to clear all</param>
        [SlashCommand("clear", "Clears all or one mini warning of a user")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task MWarnclear([Summary("user", "The user to clear warnings for")] IGuildUser? user = null,
            [Summary("user-id", "The id of a user not in the server")]
            ulong? userId = null,
            [Summary("index", "The warning number to clear, 0 clears all")] [MinValue(0)]
            int index = 0)
        {
            var targetId = userId ?? user?.Id;
            if (targetId is null)
            {
                await ReplyErrorAsync(Strings.UserNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (index < 0)
                return;
            var success = await Service.WarnClearAsync(ctx.Guild.Id, targetId.Value, index, ctx.User.ToString())
                .ConfigureAwait(false);
            var userStr =
                Format.Bold((ctx.Guild as SocketGuild)?.GetUser(targetId.Value)?.ToString() ?? targetId.ToString());
            await ReplyConfirmAsync(
                index == 0
                    ? Strings.MiniWarningsCleared(ctx.Guild.Id, userStr)
                    : success
                        ? Strings.MiniWarningCleared(ctx.Guild.Id, Format.Bold(index.ToString()), userStr)
                        : Strings.MiniWarningClearFail(ctx.Guild.Id)
            );
        }

        /// <summary>
        ///     Sets the mini warn punishment for a number of warnings. Choose None to remove the punishment.
        /// </summary>
        /// <param name="number">The number of warnings</param>
        /// <param name="punish">The punishment to set, None removes it</param>
        /// <param name="time">The time to do the punishment for</param>
        /// <param name="role">The role to add, required for the AddRole punishment</param>
        [SlashCommand("punish", "Sets or removes the punishment for a number of mini warnings")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task MWarnPunish([Summary("count", "The number of mini warnings")] [MinValue(1)] int number,
            [Summary("punishment", "The punishment to apply, None removes it")]
            PunishmentAction punish = PunishmentAction.None,
            [Summary("time", "How long the punishment lasts, for example 1h30m")]
            TimeSpan? time = null,
            [Summary("role", "The role to add, used with AddRole")]
            IRole? role = null)
        {
            if (punish == PunishmentAction.None)
            {
                if (!await Service.WarnPunishRemove(ctx.Guild.Id, number)) return;

                await ReplyConfirmAsync(
                    Strings.MiniWarnPunishRemoved(ctx.Guild.Id, Format.Bold(number.ToString()))
                );
                return;
            }

            var stoopidTime = time is null
                ? null
                : new StoopidTime
                {
                    Input = time.Value.Humanize(), Time = time.Value
                };

            if (punish == PunishmentAction.AddRole)
            {
                if (role is null)
                {
                    await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                var roleSuccess = await Service.WarnPunish(ctx.Guild.Id, number, (int)punish, stoopidTime, role);

                if (!roleSuccess)
                    return;

                await ReplyConfirmAsync(
                    stoopidTime is null
                        ? Strings.MiniWarnPunishSet(ctx.Guild.Id, Format.Bold(punish.ToString()),
                            Format.Bold(number.ToString()))
                        : Strings.MiniWarnPunishSetTimed(ctx.Guild.Id, Format.Bold(punish.ToString()),
                            Format.Bold(number.ToString()), Format.Bold(stoopidTime.Input))
                );
                return;
            }

            var success = await Service.WarnPunish(ctx.Guild.Id, number, (int)punish, stoopidTime);

            if (!success)
                return;

            if (stoopidTime is null)
            {
                await ReplyConfirmAsync(Strings.WarnPunishSet(ctx.Guild.Id,
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString()))).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmAsync(Strings.WarnPunishSetTimed(ctx.Guild.Id,
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString()),
                    Format.Bold(stoopidTime.Input))).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Lists mini warn punishments.
        /// </summary>
        [SlashCommand("punish-list", "Lists the mini warning punishments")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task MWarnPunishList()
        {
            var ps = await Service.WarnPunishList(ctx.Guild.Id);

            await ctx.Interaction.SendConfirmAsync(
                Strings.MiniWarnPunishListTitle(ctx.Guild.Id),
                ps.Length > 0
                    ? string.Join("\n", ps.Select(x =>
                        $"{x.Count} -> {(PunishmentAction)x.Punishment} {(x.Punishment == (int)PunishmentAction.AddRole ? $"<@&{x.RoleId}>" : "")} {(x.Time <= 0 ? "" : $"{x.Time}m")} "))
                    : Strings.MiniWarnPunishListNone(ctx.Guild.Id)
            );
        }
    }

    /// <summary>
    ///     Slash commands for cleaning up nicknames and removing members in bulk.
    /// </summary>
    /// <param name="banPrune">The service resolving how many days of messages a ban purges</param>
    [Group("cleanup", "Nickname cleanup and bulk member removal")]
    public class ModerationCleanup(BanPruneService banPrune) : MewdekoSlashSubmodule<UserPunishService>
    {
        /// <summary>
        ///     Dehoists a user by replacing special characters in their nickname or username.
        /// </summary>
        /// <param name="user">The user to dehoist</param>
        [SlashCommand("dehoist", "Dehoists a user by replacing special characters in their name")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageNicknames)]
        [RequireBotPermission(GuildPermission.ManageNicknames)]
        public async Task Dehoist([Summary("user", "The user to dehoist")] IGuildUser user)
        {
            if (user.Nickname != null && user.Nickname[0] < 'A')
            {
                var newNickname = UserExtensions.ReplaceSpecialChars(user.Nickname);
                await user.ModifyAsync(u => u.Nickname = newNickname);
                await ConfirmAsync(Strings.UserDehoisted(ctx.Guild.Id, user.Mention, newNickname));
            }
            else
            {
                var newNickname = UserExtensions.ReplaceSpecialChars(user.Username);
                await user.ModifyAsync(u => u.Nickname = newNickname);
                await ConfirmAsync(Strings.UserDehoistedSuccess(ctx.Guild.Id, user.Mention, newNickname));
            }
        }

        /// <summary>
        ///     Dehoists all users in the guild by replacing special characters in their nicknames.
        /// </summary>
        /// <param name="onlyDehoistNicks">Whether to only dehoist nicknames</param>
        [SlashCommand("dehoist-all", "Dehoists every user in the server")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageNicknames)]
        [RequireBotPermission(GuildPermission.Administrator)]
        public async Task DehoistAll(
            [Summary("only-nicknames", "Whether to only dehoist users that have a nickname")]
            bool onlyDehoistNicks = false)
        {
            await DeferAsync();
            var dehoistedUsers = new ConcurrentDictionary<IGuildUser, string>();
            var users = (await ctx.Guild.GetUsersAsync()).ToList();
            Parallel.ForEach(users, user =>
            {
                var newNickname = onlyDehoistNicks
                    ? UserExtensions.ReplaceSpecialChars(user.Nickname)
                    : UserExtensions.ReplaceSpecialChars(user.Username);
                if (onlyDehoistNicks)
                {
                    if (newNickname != user.Nickname)
                        dehoistedUsers.TryAdd(user, newNickname);
                }
                else
                {
                    if (newNickname != user.Username && user.Nickname != newNickname)
                        dehoistedUsers.TryAdd(user, newNickname);
                }
            });

            if (!await PromptUserConfirmAsync(
                    Strings.MassNickConfirm(ctx.Guild.Id, dehoistedUsers.Count),
                    ctx.User.Id))
                return;

            if (dehoistedUsers.IsEmpty)
            {
                await ErrorAsync(Strings.NoUsersDehoist(ctx.Guild.Id));
                return;
            }

            if (Service.AddMassNick(ctx.Guild.Id, ctx.User, dehoistedUsers.Count, "Dehoist", out var massNick))
            {
                await ConfirmAsync(Strings.MassNickDehoisting(ctx.Guild.Id, dehoistedUsers.Count));
                foreach (var user in dehoistedUsers)
                {
                    massNick = Service.GetMassNick(ctx.Guild.Id);
                    if (massNick.Stopped)
                        continue;
                    try
                    {
                        await user.Key.ModifyAsync(u => u.Nickname = user.Value);
                        Service.UpdateMassNick(ctx.Guild.Id, false, true);
                    }
                    catch
                    {
                        Service.UpdateMassNick(ctx.Guild.Id, true, false);
                    }
                }

                if (massNick.Stopped)
                {
                    await ConfirmAsync(Strings.MassNickDehoistingStopped(ctx.Guild.Id,
                        massNick.Changed,
                        massNick.Failed));
                }
                else
                {
                    await ConfirmAsync(Strings.MassNickDehoistingCompleted(ctx.Guild.Id,
                        massNick.Changed,
                        massNick.Failed));
                }

                Service.RemoveMassNick(ctx.Guild.Id);
            }
            else
            {
                await ErrorAsync(Strings.MassNickAlreadyRunning(ctx.Guild.Id));
            }
        }

        /// <summary>
        ///     Sanitizes a user by replacing special characters in their username.
        /// </summary>
        /// <param name="user">The user to sanitize</param>
        [SlashCommand("sanitize", "Sanitizes a user's name by replacing special characters")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageNicknames)]
        [RequireBotPermission(GuildPermission.ManageNicknames)]
        public async Task Sanitize([Summary("user", "The user to sanitize")] IGuildUser user)
        {
            var newName = user.SanitizeUserName();
            await user.ModifyAsync(u => u.Nickname = newName);
            await ConfirmAsync(Strings.UserSanitizedSuccess(ctx.Guild.Id, user.Mention, newName));
        }

        /// <summary>
        ///     Sanitizes all users in the guild by replacing special characters in their usernames.
        /// </summary>
        [SlashCommand("sanitize-all", "Sanitizes every user's name in the server")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageNicknames)]
        [RequireBotPermission(GuildPermission.Administrator)]
        public async Task SanitizeAll()
        {
            await DeferAsync();
            var sanitizedUsers = new ConcurrentDictionary<IGuildUser, string>();
            var users = (await ctx.Guild.GetUsersAsync()).ToList();
            Parallel.ForEach(users, user =>
            {
                var newNickname = user.SanitizeUserName();
                if (newNickname != user.Username && user.Nickname != newNickname)
                    sanitizedUsers.TryAdd(user, newNickname);
            });
            if (!await PromptUserConfirmAsync(
                    Strings.MassNickConfirm(ctx.Guild.Id, sanitizedUsers.Count),
                    ctx.User.Id))
                return;
            if (sanitizedUsers.IsEmpty)
            {
                await ErrorAsync(Strings.NoUsersSanitize(ctx.Guild.Id));
                return;
            }

            if (Service.AddMassNick(ctx.Guild.Id, ctx.User, sanitizedUsers.Count, "Sanitize", out var massNick))
            {
                await ConfirmAsync(Strings.MassNickSanitizing(ctx.Guild.Id, sanitizedUsers.Count));
                foreach (var user in sanitizedUsers)
                {
                    massNick = Service.GetMassNick(ctx.Guild.Id);
                    if (massNick.Stopped)
                        continue;
                    try
                    {
                        await user.Key.ModifyAsync(u => u.Nickname = user.Value);
                        Service.UpdateMassNick(ctx.Guild.Id, false, true);
                    }
                    catch
                    {
                        Service.UpdateMassNick(ctx.Guild.Id, true, false);
                    }
                }

                if (massNick.Stopped)
                {
                    await ConfirmAsync(Strings.MassNickSanitizingStopped(ctx.Guild.Id,
                        massNick.Changed,
                        massNick.Failed));
                }
                else
                {
                    await ConfirmAsync(Strings.MassNickSanitizingCompleted(ctx.Guild.Id,
                        massNick.Changed,
                        massNick.Failed));
                }

                Service.RemoveMassNick(ctx.Guild.Id);
            }
            else
            {
                await ErrorAsync(Strings.MassNickAlreadyRunning(ctx.Guild.Id));
            }
        }

        /// <summary>
        ///     Gets progress for a mass nickname operation.
        /// </summary>
        [SlashCommand("mass-nick-progress", "Shows the progress of the running mass nickname operation")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task MassNickProgress()
        {
            var massNick = Service.GetMassNick(ctx.Guild.Id);
            if (massNick is null)
            {
                await ErrorAsync(Strings.MassNickNone(ctx.Guild.Id));
            }
            else
            {
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle(Strings.MassNickProgress(ctx.Guild.Id, massNick.OperationType))
                    .WithDescription(Strings.MassNickDesc(ctx.Guild.Id,
                        massNick.StartedBy.Mention,
                        massNick.StartedBy.Id,
                        massNick.Total,
                        massNick.Changed,
                        massNick.Failed,
                        TimestampTag.FromDateTime(massNick.StartedAt)));

                await RespondAsync(embed: eb.Build());
            }
        }

        /// <summary>
        ///     Stops a mass nickname operation.
        /// </summary>
        [SlashCommand("mass-nick-stop", "Stops the running mass nickname operation")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task MassNickStop()
        {
            var massNick = Service.GetMassNick(ctx.Guild.Id);
            if (massNick is null)
            {
                await ErrorAsync(Strings.MassNickNone(ctx.Guild.Id));
            }
            else
            {
                Service.UpdateMassNick(ctx.Guild.Id, false, false, true);
                await ConfirmAsync(Strings.MassNickStop(ctx.Guild.Id));
            }
        }

        /// <summary>
        ///     Kicks multiple users.
        /// </summary>
        /// <param name="usersUnp">The users to kick</param>
        [SlashCommand("mass-kick", "Kicks several users at once")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.KickMembers)]
        [RequireBotPermission(GuildPermission.KickMembers)]
        public async Task MassKick([Summary("users", "The users to kick, separated by spaces")] IUser[] usersUnp)
        {
            await DeferAsync();
            var users = usersUnp.OfType<IGuildUser>();
            List<ulong> succ = [], fail = [];

            var options = new RequestOptions
            {
                AuditLogReason = $"Masskick initiated by {ctx.User}"
            };

            foreach (var u in users)
                try
                {
                    await u.KickAsync(null, options);
                    succ.Add(u.Id);
                }
                catch
                {
                    fail.Add(u.Id);
                }

            var eb = new EmbedBuilder()
                .WithColor(fail.Count > 0 ? fail.Count > succ.Count ? Color.Red : Color.Orange : Color.Green)
                .WithDescription(Strings.MassKickedMembers(ctx.Guild.Id, succ.Count))
                .WithTitle(Strings.MassKick(ctx.Guild.Id));
            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Massbans users. Use this to ban multiple users at once. Blacklists them from the bot as well.
        /// </summary>
        /// <param name="people">The users to ban and blacklist</param>
        [SlashCommand("mass-kill", "Bans several users at once and blacklists them from the bot")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.BanMembers)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [SlashOwnerOnly]
        public async Task MassKill([Summary("users", "The users to ban, separated by spaces")] IUser[] people)
        {
            if (people.Length == 0)
                return;

            await DeferAsync();
            var (bans, missing) = Service.MassKill((SocketGuild)ctx.Guild,
                string.Join("\n", people.Select(x => x.Id)));

            var missStr = "-";

            var valueTuples = bans as (string Original, ulong? id, string Reason)[] ?? bans.ToArray();
            var banningMessage = await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder()
                .WithDescription(Strings.MassKillInProgress(ctx.Guild.Id, valueTuples.Length))
                .AddField(Strings.Invalid(ctx.Guild.Id, missing), missStr)
                .WithOkColor().Build());

            var massPruneDays = await banPrune
                .GetPruneDaysAsync(ctx.Guild.Id, BanPruneAction.MassBan, ctx.Channel).ConfigureAwait(false);
            await Task.WhenAll(valueTuples
                    .Where(x => x.id.HasValue)
                    .Select(x => ctx.Guild.AddBanAsync(x.id.Value, massPruneDays, "", new RequestOptions
                    {
                        RetryMode = RetryMode.AlwaysRetry, AuditLogReason = x.Reason
                    })))
                .ConfigureAwait(false);

            await banningMessage.ModifyAsync(x => x.Embed = new EmbedBuilder()
                .WithDescription(Strings.MassKillCompleted(ctx.Guild.Id, valueTuples.Length))
                .AddField(Strings.Invalid(ctx.Guild.Id, missing), missStr)
                .WithOkColor()
                .Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Slash commands for the ban dm message and how many days of messages each ban action purges.
    /// </summary>
    /// <param name="banPrune">The service holding the purge settings</param>
    [Group("ban-config", "Ban message and message purge settings")]
    public class ModerationBanConfig(BanPruneService banPrune) : MewdekoSlashSubmodule<UserPunishService>
    {
        /// <summary>
        ///     Shows the current ban dm message, or opens a modal to set the message that users get dmed with when they
        ///     are banned.
        /// </summary>
        /// <param name="show">Whether to show the current message instead of opening the modal.</param>
        [SlashCommand("message", "Shows or sets the message users are dmed with when banned")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.BanMembers)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        public async Task BanMessage(
            [Summary("show", "Show the current message instead of setting it")]
            bool show = false)
        {
            if (!show)
            {
                await RespondWithModalAsync<BanMessageModal>("moderation_ban_message");
                return;
            }

            var template = await Service.GetBanTemplate(ctx.Guild.Id);
            if (template is null)
            {
                await ReplyConfirmAsync(Strings.BanmsgDefault(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ConfirmAsync(template).ConfigureAwait(false);
        }

        /// <summary>
        ///     Handles the ban message modal submission and stores the template.
        /// </summary>
        /// <param name="modal">The submitted modal.</param>
        [ModalInteraction("moderation_ban_message", true)]
        public async Task BanMessageSubmitted(BanMessageModal modal)
        {
            await Service.SetBanTemplate(ctx.Guild.Id, modal.Message);
            await EphemeralReplyConfirmAsync(Strings.BanMessageSet(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Resets the ban message to the default message.
        /// </summary>
        [SlashCommand("message-reset", "Resets the ban dm message to the default")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.BanMembers)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        public async Task BanMsgReset()
        {
            await Service.SetBanTemplate(ctx.Guild.Id, null);
            await ReplyConfirmAsync(Strings.BanMessageReset(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Tests the ban message by dming it to you. Use it as a prank!
        /// </summary>
        /// <param name="user">The user to fill the placeholders with. Defaults to you.</param>
        /// <param name="reason">The reason for the ban</param>
        /// <param name="duration">The duration of the ban</param>
        [SlashCommand("message-test", "Dms you the ban message so you can test it")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.BanMembers)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        public async Task BanMessageTest(
            [Summary("user", "The user to fill placeholders with, defaults to you")]
            IGuildUser? user = null,
            [Summary("reason", "The reason to show in the message")]
            string? reason = null,
            [Summary("duration", "The ban duration to show, for example 1d")]
            TimeSpan? duration = null)
        {
            await DeferAsync();
            var dmChannel = await ctx.User.CreateDMChannelAsync().ConfigureAwait(false);
            var defaultMessage = Strings.Bandm(ctx.Guild.Id, Format.Bold(ctx.Guild.Name), reason);
            var crEmbed = await Service.GetBanUserDmEmbed(ctx,
                user ?? (IGuildUser)ctx.User,
                defaultMessage,
                reason,
                duration).ConfigureAwait(false);

            if (crEmbed.Item1 is null && crEmbed.Item2 is null)
            {
                await ConfirmAsync(Strings.BanDmDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    await dmChannel
                        .SendMessageAsync(crEmbed.Item2, embeds: crEmbed.Item1, components: crEmbed.Item3?.Build())
                        .ConfigureAwait(false);
                }
                catch (Exception)
                {
                    await ReplyErrorAsync(Strings.UnableToDmUser(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await EphemeralReplyConfirmAsync(Strings.BanMessageTestSent(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Shows the purge a ban issued in this channel would use, along with every configured setting.
        /// </summary>
        [SlashCommand("prune-view", "Shows how many days of messages each ban action purges")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.BanMembers)]
        public async Task BanPrune()
        {
            var effective = await banPrune
                .GetPruneDaysAsync(ctx.Guild.Id, BanPruneAction.Ban, ctx.Channel)
                .ConfigureAwait(false);

            var settings = await banPrune.GetSettingListAsync(ctx.Guild.Id).ConfigureAwait(false);

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.BanPruneListTitle(ctx.Guild.Id))
                .WithDescription(Strings.BanPruneEffective(ctx.Guild.Id, DescribeDays(effective)));

            if (settings.Count == 0)
            {
                eb.AddField(Strings.BanPruneDefaultsTitle(ctx.Guild.Id),
                    Strings.BanPruneListEmpty(ctx.Guild.Id));
            }
            else
            {
                var guildLines = settings
                    .Where(x => x.ScopeType == (int)BanPruneScope.Guild)
                    .Select(x => Strings.BanPruneCurrent(ctx.Guild.Id, DescribeAction(x.ActionKey),
                        DescribeDays(x.PruneDays)))
                    .ToList();

                var overrideLines = settings
                    .Where(x => x.ScopeType != (int)BanPruneScope.Guild)
                    .Select(x => Strings.BanPruneCurrent(ctx.Guild.Id,
                        $"{DescribeScope((BanPruneScope)x.ScopeType, x.ScopeId)} / {DescribeAction(x.ActionKey)}",
                        DescribeDays(x.PruneDays)))
                    .ToList();

                if (guildLines.Count > 0)
                    eb.AddField(Strings.BanPruneDefaultsTitle(ctx.Guild.Id), string.Join("\n", guildLines));

                if (overrideLines.Count > 0)
                    eb.AddField(Strings.BanPruneOverridesTitle(ctx.Guild.Id), string.Join("\n", overrideLines));
            }

            await RespondAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets how many days of messages one ban action purges.
        /// </summary>
        /// <param name="action">The action, or All to cover every action</param>
        /// <param name="days">The purge in days, 0 through 7</param>
        /// <param name="target">A channel or category to scope the setting to, or nothing for the server default</param>
        [SlashCommand("prune-set", "Sets how many days of messages a ban action purges")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task BanPruneSet([Summary("action", "The ban action, or All")] BanPruneActionChoice action,
            [Summary("days", "How many days of messages to purge, 0 to 7")] [MinValue(0)] [MaxValue(7)]
            int days,
            [Summary("target", "A channel or category to scope the setting to")]
            IGuildChannel? target = null)
        {
            var resolved = ResolveAction(action);
            var (scope, scopeId) = ResolveScope(target);
            await banPrune.SetAsync(ctx.Guild.Id, scope, scopeId, resolved, days).ConfigureAwait(false);

            await ConfirmAsync(Strings.BanPruneSet(ctx.Guild.Id,
                    DescribeAction(resolved?.Key),
                    DescribeDays(Math.Clamp(days, 0, BanPruneService.MaxPruneDays)),
                    DescribeScope(scope, scopeId)))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes a purge setting so the action falls back to a broader scope or its default.
        /// </summary>
        /// <param name="action">The action, or All for the setting covering every action</param>
        /// <param name="target">The channel or category the setting is on, or nothing for the server default</param>
        [SlashCommand("prune-clear", "Removes the purge setting for a ban action")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task BanPruneClear([Summary("action", "The ban action, or All")] BanPruneActionChoice action,
            [Summary("target", "The channel or category the setting is on")]
            IGuildChannel? target = null)
        {
            var resolved = ResolveAction(action);
            var (scope, scopeId) = ResolveScope(target);
            var removed = await banPrune.ClearAsync(ctx.Guild.Id, scope, scopeId, resolved).ConfigureAwait(false);

            var message = removed
                ? Strings.BanPruneCleared(ctx.Guild.Id, DescribeAction(resolved?.Key), DescribeScope(scope, scopeId))
                : Strings.BanPruneClearedNone(ctx.Guild.Id, DescribeAction(resolved?.Key),
                    DescribeScope(scope, scopeId));

            await ConfirmAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes every purge setting in the server.
        /// </summary>
        [SlashCommand("prune-reset", "Removes every ban purge setting in the server")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task BanPruneReset()
        {
            var removed = await banPrune.ResetAsync(ctx.Guild.Id).ConfigureAwait(false);
            await ConfirmAsync(Strings.BanPruneReset(ctx.Guild.Id, removed)).ConfigureAwait(false);
        }

        private static BanPruneAction? ResolveAction(BanPruneActionChoice choice)
        {
            return choice == BanPruneActionChoice.All ? null : BanPruneAction.FromKey(choice.ToString());
        }

        private static (BanPruneScope Scope, ulong ScopeId) ResolveScope(IGuildChannel? target)
        {
            return target switch
            {
                null => (BanPruneScope.Guild, 0UL),
                ICategoryChannel => (BanPruneScope.Category, target.Id),
                _ => (BanPruneScope.Channel, target.Id)
            };
        }

        private string DescribeAction(string? key)
        {
            return string.IsNullOrEmpty(key)
                ? Strings.BanPruneSetAllActions(ctx.Guild.Id)
                : BanPruneAction.FromKey(key)?.DisplayName ?? key;
        }

        private string DescribeDays(int days)
        {
            return days <= 0
                ? Strings.BanPruneDaysNone(ctx.Guild.Id)
                : Strings.BanPruneDays(ctx.Guild.Id, days);
        }

        private string DescribeScope(BanPruneScope scope, ulong scopeId)
        {
            return scope switch
            {
                BanPruneScope.Category => Strings.BanPruneScopeCategory(ctx.Guild.Id, $"<#{scopeId}>"),
                BanPruneScope.Channel => Strings.BanPruneScopeChannel(ctx.Guild.Id, $"<#{scopeId}>"),
                _ => Strings.BanPruneScopeGuild(ctx.Guild.Id)
            };
        }
    }

    /// <summary>
    ///     Slash commands for purging messages.
    /// </summary>
    [Group("purge", "Purge messages")]
    public class ModerationPurge : MewdekoSlashSubmodule<PurgeService>
    {
        private static readonly TimeSpan TwoWeeks = TimeSpan.FromDays(14);

        /// <summary>
        ///     Purges messages from the current channel with the given amount and filters. When a user or user id is given,
        ///     only that user's messages from the last two weeks are purged.
        /// </summary>
        /// <param name="count">The amount of messages to purge</param>
        /// <param name="user">Only purge messages from this user</param>
        /// <param name="userId">Only purge messages from the user with this id</param>
        /// <param name="safe">Skip pinned messages</param>
        /// <param name="noBots">Skip messages from bots</param>
        /// <param name="onlyBots">Only purge messages from bots</param>
        /// <param name="hasEmbed">Only purge messages that have an embed</param>
        /// <param name="noEmbed">Only purge messages that do not have an embed</param>
        /// <param name="contains">Only purge messages containing this text</param>
        /// <param name="before">Only purge messages sent before this date</param>
        /// <param name="after">Only purge messages sent after this date</param>
        [SlashCommand("messages", "Purges messages from this channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(ChannelPermission.ManageMessages)]
        [RequireBotPermission(ChannelPermission.ManageMessages)]
        public async Task Purge(
            [Summary("count", "How many messages to purge, 1 to 1000")] [MinValue(1)] [MaxValue(1000)]
            int count,
            [Summary("user", "Only purge messages from this user")]
            IGuildUser? user = null,
            [Summary("user-id", "Only purge messages from the user with this id")]
            ulong? userId = null,
            [Summary("safe", "Skip pinned messages")]
            bool safe = false,
            [Summary("no-bots", "Skip messages from bots")]
            bool noBots = false,
            [Summary("only-bots", "Only purge messages from bots")]
            bool onlyBots = false,
            [Summary("has-embed", "Only purge messages that have an embed")]
            bool hasEmbed = false,
            [Summary("no-embed", "Only purge messages without an embed")]
            bool noEmbed = false,
            [Summary("contains", "Only purge messages containing this text")]
            string? contains = null,
            [Summary("before", "Only purge messages sent before this date")]
            string? before = null,
            [Summary("after", "Only purge messages sent after this date")]
            string? after = null)
        {
            await DeferAsync(true);
            var channel = (ITextChannel)ctx.Channel;
            var targetId = userId ?? user?.Id;

            if (targetId is not null)
            {
                var id = targetId.Value;
                if (safe)
                {
                    await Service.PurgeWhere(channel, (ulong)count,
                        m => m.Author.Id == id && DateTime.UtcNow - m.CreatedAt < TwoWeeks && !m.IsPinned);
                }
                else
                {
                    await Service.PurgeWhere(channel, (ulong)count,
                        m => m.Author.Id == id && DateTime.UtcNow - m.CreatedAt < TwoWeeks);
                }

                await EphemeralReplyConfirmAsync(Strings.PurgeCompleted(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var options = new List<Func<IMessage, bool>>();

            if (safe)
                options.Add(x => !x.IsPinned);
            if (noBots)
                options.Add(x => !x.Author.IsBot);
            if (onlyBots)
                options.Add(x => x.Author.IsBot);
            if (hasEmbed)
                options.Add(x => x.Embeds.Count > 0);
            if (noEmbed)
                options.Add(x => x.Embeds.Count == 0);
            if (!string.IsNullOrEmpty(contains))
                options.Add(x => x.Content.Contains(contains, StringComparison.InvariantCultureIgnoreCase));
            if (!string.IsNullOrEmpty(before) && DateTimeOffset.TryParse(before, out var beforeDate))
                options.Add(x => x.Timestamp < beforeDate);
            if (!string.IsNullOrEmpty(after) && DateTimeOffset.TryParse(after, out var afterDate))
                options.Add(x => x.Timestamp > afterDate);

            if (options.Count == 0)
            {
                await Service.PurgeWhere(channel, (ulong)count, x => x.Channel.Id == ctx.Channel.Id);
                await EphemeralReplyConfirmAsync(Strings.PurgeCompleted(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.PurgeWhere(channel, (ulong)count, CombinedPredicate).ConfigureAwait(false);
            await EphemeralReplyConfirmAsync(Strings.PurgeCompleted(ctx.Guild.Id)).ConfigureAwait(false);
            return;

            bool CombinedPredicate(IMessage m)
            {
                return options.All(p => p(m));
            }
        }

        /// <summary>
        ///     Purges all messages from accessible channels for a user.
        /// </summary>
        /// <param name="user">The user whose messages to purge</param>
        /// <param name="messageCount">The count of messages to search in each channel</param>
        [SlashCommand("user", "Purges a user's messages from every channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageMessages)]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        public async Task PurgeUser([Summary("user", "The user whose messages to purge")] IUser user,
            [Summary("count", "How many messages to search in each channel, 1 to 1000")] [MinValue(1)] [MaxValue(1000)]
            int messageCount)
        {
            if (messageCount is > 1000 or < 1)
            {
                await ErrorAsync(Strings.PurgeInvalidAmount(ctx.Guild.Id));
                return;
            }

            await DeferAsync();
            await ConfirmAsync(Strings.PurgeUserSearching(ctx.Guild.Id, messageCount, user.Mention));

            var successCount = 0;
            var failCount = 0;
            var deletedMessageCount = 0;
            var channels = await ctx.Guild.GetTextChannelsAsync();
            foreach (var i in channels)
            {
                try
                {
                    var messages = await i.GetMessagesAsync(messageCount).FlattenAsync();
                    messages = messages.Where(x => x.Author.Id == user.Id).ToList();

                    if (!messages.Any())
                        continue;

                    await i.DeleteMessagesAsync(messages);
                    successCount++;
                    deletedMessageCount += messages.Count();
                }
                catch
                {
                    failCount++;
                }
            }

            switch (successCount)
            {
                case > 0 when failCount is 0:
                    await ConfirmAsync(
                        Strings.PurgeUserSuccess(ctx.Guild.Id, deletedMessageCount, user.Mention, successCount));
                    break;
                case > 0 when failCount > 0:
                    await ConfirmAsync(
                        Strings.PurgeUserPartial(ctx.Guild.Id, deletedMessageCount, user.Mention, successCount,
                            failCount));
                    break;
                case 0 when failCount > 0:
                    await ErrorAsync(Strings.PurgeUserFail(ctx.Guild.Id));
                    break;
                case 0 when failCount is 0:
                    await ConfirmAsync(Strings.PurgeUserNone(ctx.Guild.Id, user.Mention));
                    break;
            }
        }
    }
}