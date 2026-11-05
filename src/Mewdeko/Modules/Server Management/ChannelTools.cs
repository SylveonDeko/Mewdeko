using DataModel;
using Discord.Commands;
using Discord.Net;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Server_Management.Services;

namespace Mewdeko.Modules.Server_Management;

public partial class ServerManagement
{
    /// <summary>
    ///     Commands that lock or hide a channel, optionally for a set time.
    /// </summary>
    [Group]
    public class ChannelTimedCommands : MewdekoSubmodule<ChannelTimedActionService>
    {
        private static bool InRange(StoopidTime? time) =>
            time is null || (time.Time >= ChannelTimedActionService.MinDuration &&
                             time.Time <= ChannelTimedActionService.MaxDuration);

        private async Task ApplyAsync(SocketGuildChannel? channel, StoopidTime? time, IRole[] roles,
            ChannelTimedActionKind kind)
        {
            channel ??= ctx.Channel as SocketGuildChannel;
            if (channel is null)
                return;

            if (!InRange(time))
            {
                await ReplyErrorAsync(Strings.ChantoolsInvalidTime(ctx.Guild.Id));
                return;
            }

            try
            {
                var updated = await Service.ApplyAsync(channel, kind, roles, time?.Time, ctx.User.Id);
                var lockKind = kind == ChannelTimedActionKind.Lock;
                var guildId = ctx.Guild.Id;
                var text = (lockKind, updated, time is not null) switch
                {
                    (true, true, _) => Strings.ChantoolsLockUpdated(guildId, $"<#{channel.Id}>"),
                    (false, true, _) => Strings.ChantoolsHideUpdated(guildId, $"<#{channel.Id}>"),
                    (true, false, true) => Strings.ChantoolsLockedFor(guildId, $"<#{channel.Id}>",
                        JoinRoleTimeoutService.FormatMinutes((int)time!.Time.TotalMinutes)),
                    (false, false, true) => Strings.ChantoolsHiddenFor(guildId, $"<#{channel.Id}>",
                        JoinRoleTimeoutService.FormatMinutes((int)time!.Time.TotalMinutes)),
                    (true, false, false) => Strings.ChantoolsLocked(guildId, $"<#{channel.Id}>"),
                    _ => Strings.ChantoolsHidden(guildId, $"<#{channel.Id}>")
                };
                await ReplyConfirmAsync(text);
            }
            catch (HttpException ex)
            {
                await ReplyErrorAsync(Strings.ChantoolsFailed(ctx.Guild.Id, ex.Reason ?? ex.Message));
            }
        }

        private async Task UndoAsync(SocketGuildChannel? channel, ChannelTimedActionKind kind)
        {
            channel ??= ctx.Channel as SocketGuildChannel;
            if (channel is null)
                return;

            var guildId = ctx.Guild.Id;
            var mention = $"<#{channel.Id}>";
            var lockKind = kind == ChannelTimedActionKind.Lock;
            if (!await Service.UndoAsync(channel.Id, kind))
            {
                await ReplyErrorAsync(lockKind
                    ? Strings.ChantoolsNotLocked(guildId, mention)
                    : Strings.ChantoolsNotHidden(guildId, mention));
                return;
            }

            await ReplyConfirmAsync(lockKind
                ? Strings.ChantoolsUnlocked(guildId, mention)
                : Strings.ChantoolsUnhidden(guildId, mention));
        }

        /// <summary>
        ///     Locks a channel by denying Send Messages, optionally for a set time.
        /// </summary>
        /// <param name="channel">The channel, or the current one.</param>
        /// <param name="time">How long the lock lasts, or null until unlocked.</param>
        /// <param name="roles">The roles to lock out, or none for @everyone.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageRoles)]
        public Task ChanLock(SocketGuildChannel? channel = null, StoopidTime? time = null, params IRole[] roles) =>
            ApplyAsync(channel, time, roles, ChannelTimedActionKind.Lock);

        /// <summary>
        ///     Undoes a lock and puts the overwrites back.
        /// </summary>
        /// <param name="channel">The channel, or the current one.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageRoles)]
        public Task ChanUnlock(SocketGuildChannel? channel = null) => UndoAsync(channel, ChannelTimedActionKind.Lock);

        /// <summary>
        ///     Hides a channel by denying View Channel, optionally for a set time.
        /// </summary>
        /// <param name="channel">The channel, or the current one.</param>
        /// <param name="time">How long the channel stays hidden, or null until unhidden.</param>
        /// <param name="roles">The roles to hide it from, or none for @everyone.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageRoles)]
        public Task Hide(SocketGuildChannel? channel = null, StoopidTime? time = null, params IRole[] roles) =>
            ApplyAsync(channel, time, roles, ChannelTimedActionKind.Hide);

        /// <summary>
        ///     Undoes a hide and puts the overwrites back.
        /// </summary>
        /// <param name="channel">The channel, or the current one.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageRoles)]
        public Task Unhide(SocketGuildChannel? channel = null) => UndoAsync(channel, ChannelTimedActionKind.Hide);

        /// <summary>
        ///     Lists the channels that are locked or hidden by these commands.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task ChanLocks()
        {
            var actions = await Service.ListAsync(ctx.Guild.Id);
            if (actions.Count == 0)
            {
                await ReplyErrorAsync(Strings.ChantoolsListEmpty(ctx.Guild.Id));
                return;
            }

            var embed = new EmbedBuilder().WithOkColor().WithTitle(Strings.ChantoolsListTitle(ctx.Guild.Id))
                .WithDescription(string.Join("\n", actions.Take(40).Select(Service.Line)));
            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }
    }

    /// <summary>
    ///     Commands that link a voice channel to a text channel.
    /// </summary>
    [Group]
    public class VoiceTextCommands : MewdekoSubmodule<VoiceTextLinkService>
    {
        /// <summary>
        ///     Links a voice channel to a text channel that members can use while they are in the voice channel.
        /// </summary>
        /// <param name="voice">The voice channel.</param>
        /// <param name="text">The text channel.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task VoiceTextLink(IVoiceChannel voice, ITextChannel text)
        {
            try
            {
                await Service.LinkAsync(ctx.Guild.Id, voice.Id, text.Id);
                await ReplyConfirmAsync(Strings.VtlLinked(ctx.Guild.Id, voice.Mention, text.Mention));
            }
            catch (HttpException ex)
            {
                await ReplyErrorAsync(Strings.ChantoolsFailed(ctx.Guild.Id, ex.Reason ?? ex.Message));
            }
        }

        /// <summary>
        ///     Removes the link of a voice channel.
        /// </summary>
        /// <param name="voice">The voice channel.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task VoiceTextUnlink(IVoiceChannel voice)
        {
            if (!await Service.UnlinkAsync(voice.Id))
            {
                await ReplyErrorAsync(Strings.VtlNotLinked(ctx.Guild.Id, voice.Mention));
                return;
            }

            await ReplyConfirmAsync(Strings.VtlUnlinked(ctx.Guild.Id, voice.Mention));
        }

        /// <summary>
        ///     Lists the voice channels that are linked to text channels.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task VoiceTextLinks()
        {
            var links = Service.GetLinks(ctx.Guild.Id);
            if (links.Count == 0)
            {
                await ReplyErrorAsync(Strings.VtlListEmpty(ctx.Guild.Id));
                return;
            }

            var embed = new EmbedBuilder().WithOkColor().WithTitle(Strings.VtlListTitle(ctx.Guild.Id))
                .WithDescription(string.Join("\n",
                    links.Take(40).Select(x => Strings.VtlListLine(ctx.Guild.Id, $"<#{x.VoiceChannelId}>",
                        $"<#{x.TextChannelId}>"))));
            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }
    }

    /// <summary>
    ///     Commands that delete old messages from a channel on a schedule.
    /// </summary>
    [Group]
    public class AutoPurgeCommands : MewdekoSubmodule<AutoPurgeService>
    {
        /// <summary>
        ///     Turns on auto-purge for a channel or changes it.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="maxAge">How old a message must be to be deleted, like 1d.</param>
        /// <param name="filter">All, Humans, Bots, Links or Attachments.</param>
        /// <param name="keepPinned">Whether pinned messages are kept.</param>
        /// <param name="interval">How often the channel is purged, 10 minutes by default.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [BotPerm(GuildPermission.ManageMessages)]
        public async Task AutoPurge(ITextChannel channel, StoopidTime maxAge, AutoPurgeFilter filter = AutoPurgeFilter.All,
            bool keepPinned = true, StoopidTime? interval = null)
        {
            var (error, setting) = await Service.SetAsync(ctx.Guild.Id, channel.Id, (int)maxAge.Time.TotalMinutes,
                interval is null ? 10 : (int)interval.Time.TotalMinutes, filter, keepPinned);
            switch (error)
            {
                case AutoPurgeError.InvalidAge:
                    await ReplyErrorAsync(Strings.ApInvalidAge(ctx.Guild.Id));
                    return;
                case AutoPurgeError.InvalidInterval:
                    await ReplyErrorAsync(Strings.ApInvalidInterval(ctx.Guild.Id));
                    return;
            }

            await ReplyConfirmAsync(Service.Summary(setting!));
        }

        /// <summary>
        ///     Turns off auto-purge for a channel.
        /// </summary>
        /// <param name="channel">The channel.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task AutoPurgeOff(ITextChannel channel)
        {
            if (!await Service.RemoveAsync(channel.Id))
            {
                await ReplyErrorAsync(Strings.ApNotSet(ctx.Guild.Id, channel.Mention));
                return;
            }

            await ReplyConfirmAsync(Strings.ApOff(ctx.Guild.Id, channel.Mention));
        }

        /// <summary>
        ///     Lists the channels that have auto-purge.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task AutoPurgeList()
        {
            var settings = await Service.GetAllAsync(ctx.Guild.Id);
            if (settings.Count == 0)
            {
                await ReplyErrorAsync(Strings.ApListEmpty(ctx.Guild.Id));
                return;
            }

            var embed = new EmbedBuilder().WithOkColor().WithTitle(Strings.ApListTitle(ctx.Guild.Id))
                .WithDescription(string.Join("\n", settings.Take(40).Select(Service.Line)));
            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Purges a channel now with its saved settings.
        /// </summary>
        /// <param name="channel">The channel.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [BotPerm(GuildPermission.ManageMessages)]
        public async Task AutoPurgeNow(ITextChannel channel)
        {
            var deleted = await Service.RunNowAsync(channel.Id);
            if (deleted is null)
            {
                await ReplyErrorAsync(Strings.ApRunFailed(ctx.Guild.Id, channel.Mention));
                return;
            }

            await ReplyConfirmAsync(Strings.ApRunDone(ctx.Guild.Id, deleted.Value, channel.Mention));
        }
    }

    /// <summary>
    ///     Commands that explain permissions and why the bot cannot assign a role.
    /// </summary>
    [Group]
    public class PermissionCheckCommands : MewdekoSubmodule<PermissionInspectorService>
    {
        /// <summary>
        ///     Shows the permissions a member has in a channel and the overwrites that apply.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="user">The member.</param>
        /// <param name="permission">One permission to trace, or none to list them all.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [Priority(1)]
        public Task PermCheck(SocketGuildChannel channel, IGuildUser user, ChannelPermission? permission = null) =>
            ctx.Channel.SendMessageAsync(embed: Service.Check(channel, user, null, permission).Build());

        /// <summary>
        ///     Shows the permissions a role has in a channel and the overwrites that apply.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="role">The role.</param>
        /// <param name="permission">One permission to trace, or none to list them all.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        [Priority(0)]
        public Task PermCheck(SocketGuildChannel channel, IRole role, ChannelPermission? permission = null) =>
            ctx.Channel.SendMessageAsync(embed: Service.Check(channel, null, role, permission).Build());

        /// <summary>
        ///     Shows the permissions the bot has in a channel and the overwrites that apply.
        /// </summary>
        /// <param name="channel">The channel, or the current one.</param>
        /// <param name="permission">One permission to trace, or none to list them all.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        public Task PermCheckBot(SocketGuildChannel? channel = null, ChannelPermission? permission = null)
        {
            channel ??= (SocketGuildChannel)ctx.Channel;
            return ctx.Channel.SendMessageAsync(embed: Service.Check(channel, channel.Guild.CurrentUser, null, permission)
                .Build());
        }

        /// <summary>
        ///     Explains why the bot can or cannot assign a role, or what it cannot assign at all.
        /// </summary>
        /// <param name="role">The role, or none for an overview.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageRoles)]
        public Task RoleDiagnose(IRole? role = null) =>
            ctx.Channel.SendMessageAsync(embed: Service.Diagnose((SocketGuild)ctx.Guild, role).Build());
    }
}
