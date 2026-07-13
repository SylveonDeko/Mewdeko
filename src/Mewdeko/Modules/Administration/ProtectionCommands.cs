using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Commands for managing the Anti-Alt, Anti-Raid, and Anti-Spam protection settings.
    /// </summary>
    [Group]
    public class ProtectionCommands(InteractiveService serv, ImageHashingService imageHashing)
        : MewdekoSubmodule<ProtectionService>
    {
        /// <summary>
        ///     Disables the Anti-Alt protection for the guild.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiAlt()
        {
            if (await Service.TryStopAntiAlt(ctx.Guild.Id).ConfigureAwait(false))
            {
                await ReplyConfirmAsync(Strings.ProtDisable(ctx.Guild.Id, "Anti-Alt")).ConfigureAwait(false);
                return;
            }

            await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Alt")).ConfigureAwait(false);
        }


        /// <summary>
        ///     Configures the Anti-Alt protection for the guild, setting the minimum account age and punishment action.
        /// </summary>
        /// <param name="minAge">The minimum age (in minutes) for accounts to be considered as alts.</param>
        /// <param name="action">The punishment action to be taken against detected alts. <see cref="PunishmentAction" /></param>
        /// <param name="punishTime">Optional: The duration of the punishment, if applicable.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiAlt(StoopidTime minAge, PunishmentAction action,
            [Remainder] StoopidTime? punishTime = null)
        {
            var minAgeMinutes = (int)minAge.Time.TotalMinutes;
            var punishTimeMinutes = (int?)punishTime?.Time.TotalMinutes ?? 0;

            if (minAgeMinutes < 1 || punishTimeMinutes < 0)
                return;
            switch (action)
            {
                case PunishmentAction.Timeout when punishTime.Time.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime.Time == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            await Service.StartAntiAltAsync(ctx.Guild.Id, minAgeMinutes, action, punishTimeMinutes)
                .ConfigureAwait(false);

            await ctx.OkAsync().ConfigureAwait(false);
        }


        /// <summary>
        ///     Configures the Anti-Alt protection for the guild, setting the minimum account age and punishment action with a
        ///     role-based punishment.
        /// </summary>
        /// <param name="minAge">The minimum age (in minutes) for accounts to be considered as alts.</param>
        /// <param name="action">The punishment action to be taken against detected alts. <see cref="PunishmentAction" /></param>
        /// <param name="role">The role to be assigned to detected alts as punishment.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiAlt(StoopidTime minAge, PunishmentAction action, [Remainder] IRole role)
        {
            var minAgeMinutes = (int)minAge.Time.TotalMinutes;

            if (minAgeMinutes < 1)
                return;

            await Service.StartAntiAltAsync(ctx.Guild.Id, minAgeMinutes, action, roleId: role.Id).ConfigureAwait(false);

            await ctx.OkAsync().ConfigureAwait(false);
        }


        /// <summary>
        ///     Disables the Anti-Raid protection for the guild.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiRaid()
        {
            if (await Service.TryStopAntiRaid(ctx.Guild.Id))
                await ReplyConfirmAsync(Strings.ProtDisable(ctx.Guild.Id, "Anti-Raid"));
            else
                await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Raid"));
        }

        /// <summary>
        ///     Configures the Anti-Raid protection for the guild, setting the user threshold, detection time window, punishment
        ///     action, and optional punishment duration.
        /// </summary>
        /// <param name="userThreshold">The threshold of users that triggers the detection of a raid.</param>
        /// <param name="seconds">The time window (in seconds) to observe user joins.</param>
        /// <param name="action">The punishment action to be taken against detected raids. <see cref="PunishmentAction" /></param>
        /// <param name="punishTime">The duration of punishment for the raiders (optional).</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(1)]
        public Task AntiRaid(int userThreshold, int seconds, PunishmentAction action,
            [Remainder] StoopidTime punishTime)
        {
            return InternalAntiRaid(userThreshold, seconds, action, punishTime);
        }

        /// <summary>
        ///     Configures the Anti-Raid protection for the guild, setting the user threshold, detection time window, and
        ///     punishment action.
        /// </summary>
        /// <param name="userThreshold">The threshold of users that triggers the detection of a raid.</param>
        /// <param name="seconds">The time window (in seconds) to observe user joins.</param>
        /// <param name="action">The punishment action to be taken against detected raids. <see cref="PunishmentAction" /></param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(2)]
        public Task AntiRaid(int userThreshold, int seconds, PunishmentAction action)
        {
            return InternalAntiRaid(userThreshold, seconds, action);
        }


        private async Task InternalAntiRaid(int userThreshold, int seconds = 10,
            PunishmentAction action = PunishmentAction.Mute, StoopidTime? punishTime = null)
        {
            switch (action)
            {
                case PunishmentAction.Timeout when punishTime.Time.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime.Time == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            if (action == PunishmentAction.AddRole)
            {
                await ReplyErrorAsync(Strings.PunishmentUnsupported(ctx.Guild.Id, action)).ConfigureAwait(false);
                return;
            }

            if (userThreshold is < 2 or > 30)
            {
                await ReplyErrorAsync(Strings.RaidCnt(ctx.Guild.Id, 2, 30)).ConfigureAwait(false);
                return;
            }

            if (seconds is < 2 or > 300)
            {
                await ReplyErrorAsync(Strings.RaidTime(ctx.Guild.Id, 2, 300)).ConfigureAwait(false);
                return;
            }

            if (punishTime is not null)
            {
                if (!ProtectionService.IsDurationAllowed(action))
                    await ReplyErrorAsync(Strings.ProtCantUseTime(ctx.Guild.Id)).ConfigureAwait(false);
            }

            var time = (int?)punishTime?.Time.TotalMinutes ?? 0;
            if (time is < 0 or > 60 * 24)
                return;

            var stats = await Service.StartAntiRaidAsync(ctx.Guild.Id, userThreshold, seconds,
                action, time).ConfigureAwait(false);

            if (stats == null) return;

            await ctx.Channel.SendConfirmAsync(Strings.ProtEnable(ctx.Guild.Id, "Anti-Raid"),
                    $"{ctx.User.Mention} {GetAntiRaidString(stats)}")
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Disables the Anti-Spam protection for the guild.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiSpam()
        {
            if (await Service.TryStopAntiSpam(ctx.Guild.Id))
                await ReplyConfirmAsync(Strings.ProtDisable(ctx.Guild.Id, "Anti-Spam"));
            else
                await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Spam"));
        }

        /// <summary>
        ///     Configures the Anti-Spam protection for the guild, setting the message count threshold, punishment action, and
        ///     optional punishment duration.
        /// </summary>
        /// <param name="messageCount">The threshold of messages that triggers the detection of spam.</param>
        /// <param name="action">
        ///     The punishment action to be taken against detected spammers. <see cref="PunishmentAction" />
        /// </param>
        /// <param name="punishTime">The duration of punishment for the spammers (optional).</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(1)]
        public Task AntiSpam(int messageCount, PunishmentAction action, [Remainder] StoopidTime punishTime)
        {
            return InternalAntiSpam(messageCount, action, punishTime);
        }

        /// <summary>
        ///     Configures the Anti-Spam protection for the guild, setting the message count threshold, punishment action, and the
        ///     role to add to spammers.
        /// </summary>
        /// <param name="messageCount">The threshold of messages that triggers the detection of spam.</param>
        /// <param name="action">
        ///     The punishment action to be taken against detected spammers. <see cref="PunishmentAction" />
        /// </param>
        /// <param name="role">The role to add to the spammers.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(0)]
        public Task AntiSpam(int messageCount, PunishmentAction action, [Remainder] IRole role)
        {
            if (action != PunishmentAction.AddRole)
                return Task.CompletedTask;

            return InternalAntiSpam(messageCount, action, null, role);
        }

        /// <summary>
        ///     Configures the Anti-Spam protection for the guild, setting the message count threshold and punishment action.
        /// </summary>
        /// <param name="messageCount">The threshold of messages that triggers the detection of spam.</param>
        /// <param name="action">
        ///     The punishment action to be taken against detected spammers. <see cref="PunishmentAction" />
        /// </param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(2)]
        public Task AntiSpam(int messageCount, PunishmentAction action)
        {
            return InternalAntiSpam(messageCount, action);
        }


        /// <summary>
        ///     Configures the Anti-Spam protection for the guild, setting the message count threshold, punishment action, and
        ///     optional punishment duration.
        /// </summary>
        /// <param name="messageCount">The threshold of messages that triggers the detection of spam.</param>
        /// <param name="action">The punishment action to be taken against detected spammers.</param>
        /// <param name="timeData">The duration of punishment for the spammers (optional).</param>
        /// <param name="role">The role to add to the spammers (optional).</param>
        /// <remarks>
        ///     This method is internally used by the AntiSpam command and is restricted to users with Administrator permissions.
        /// </remarks>
        private async Task InternalAntiSpam(int messageCount, PunishmentAction action,
            StoopidTime? timeData = null, IRole? role = null)
        {
            if (messageCount is < 2 or > 10)
                return;

            if (timeData is not null)
            {
                if (!ProtectionService.IsDurationAllowed(action))
                {
                    await ReplyErrorAsync(Strings.ProtCantUseTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }
            }

            var time = (int?)timeData?.Time.TotalMinutes ?? 0;
            if (time is < 0 or > 60 * 24)
                return;

            switch (action)
            {
                case PunishmentAction.Timeout when timeData.Time.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when timeData.Time == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            var stats = await Service.StartAntiSpamAsync(ctx.Guild.Id, messageCount, action, time, role?.Id)
                .ConfigureAwait(false);

            await ctx.Channel.SendConfirmAsync(Strings.ProtEnable(ctx.Guild.Id, "Anti-Spam"),
                $"{ctx.User.Mention} {GetAntiSpamString(stats)}").ConfigureAwait(false);
        }


        /// <summary>
        ///     Ignores the current text channel from Anti-Spam protection.
        /// </summary>
        /// <remarks>
        ///     This command adds the current text channel to the list of ignored channels for Anti-Spam protection.
        ///     It is restricted to users with Administrator permissions and is used to exclude specific channels from Anti-Spam
        ///     checks.
        /// </remarks>
        public async Task AntispamIgnore()
        {
            var added = await Service.AntiSpamIgnoreAsync(ctx.Guild.Id, ctx.Channel.Id).ConfigureAwait(false);

            if (added is null)
            {
                await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Spam")).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(added.Value
                    ? Strings.SpamIgnore(ctx.Guild.Id, "Anti-Spam")
                    : Strings.SpamNotIgnore(ctx.Guild.Id, "Anti-Spam"))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Disables the Anti-Mass-Mention protection for the guild.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiMassMention()
        {
            if (await Service.TryStopAntiMassMention(ctx.Guild.Id).ConfigureAwait(false))
            {
                await ReplyConfirmAsync(Strings.ProtDisable(ctx.Guild.Id, "Anti-Mass-Mention")).ConfigureAwait(false);
                return;
            }

            await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Mass-Mention"))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Configures the Anti-Mass-Mention protection for the guild, setting the mention threshold for a single message,
        ///     the time window for mention tracking, the maximum allowed mentions in the time window, and the punishment action.
        /// </summary>
        /// <param name="mentionThreshold">The number of mentions allowed in a single message before triggering protection.</param>
        /// <param name="timeWindowSeconds">The time window (in seconds) to observe mentions.</param>
        /// <param name="maxMentionsInTimeWindow">The maximum allowed mentions in the specified time window.</param>
        /// <param name="ignoreBots">Whether to ignore bot accounts when tracking mentions.</param>
        /// <param name="action">
        ///     The punishment action to be taken against users who exceed the mention limits.
        ///     <see cref="PunishmentAction" />
        /// </param>
        /// <param name="punishTime">Optional: The duration of the punishment (if applicable).</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiMassMention(int mentionThreshold, int timeWindowSeconds, int maxMentionsInTimeWindow,
            bool ignoreBots,
            PunishmentAction action, [Remainder] StoopidTime? punishTime = null)
        {
            var punishTimeMinutes = (int?)punishTime?.Time.TotalMinutes ?? 0;

            if (punishTimeMinutes < 0 || mentionThreshold < 1 || timeWindowSeconds < 1 || maxMentionsInTimeWindow < 1)
                return;

            switch (action)
            {
                case PunishmentAction.Timeout when punishTime.Time.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime.Time == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            await Service.StartAntiMassMentionAsync(ctx.Guild.Id, mentionThreshold, timeWindowSeconds,
                maxMentionsInTimeWindow, ignoreBots, action, punishTimeMinutes, null).ConfigureAwait(false);

            await ctx.OkAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///     Configures the Anti-Mass-Mention protection for the guild, setting the mention threshold for a single message,
        ///     the time window for mention tracking, the maximum allowed mentions in the time window, and the punishment action
        ///     with a role-based punishment.
        /// </summary>
        /// <param name="mentionThreshold">The number of mentions allowed in a single message before triggering protection.</param>
        /// <param name="timeWindowSeconds">The time window (in seconds) to observe mentions.</param>
        /// <param name="maxMentionsInTimeWindow">The maximum allowed mentions in the specified time window.</param>
        /// <param name="ignoreBots">Whether to ignore bot accounts when tracking mentions.</param>
        /// <param name="action">
        ///     The punishment action to be taken against users who exceed the mention limits.
        ///     <see cref="PunishmentAction" />
        /// </param>
        /// <param name="role">The role to be assigned to punished users as punishment.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiMassMention(int mentionThreshold, int timeWindowSeconds, int maxMentionsInTimeWindow,
            bool ignoreBots,
            PunishmentAction action, [Remainder] IRole role)
        {
            if (mentionThreshold < 1 || timeWindowSeconds < 1 || maxMentionsInTimeWindow < 1)
                return;

            await Service.StartAntiMassMentionAsync(ctx.Guild.Id, mentionThreshold, timeWindowSeconds,
                maxMentionsInTimeWindow, ignoreBots, action, 0, role.Id).ConfigureAwait(false);

            await ctx.OkAsync().ConfigureAwait(false);
        }


        /// <summary>
        ///     Displays the current status of anti-protection settings, including Anti-Spam, Anti-Raid, Anti-Alt, and
        ///     Anti-Mass-Mention.
        /// </summary>
        /// <remarks>
        ///     This command provides information about the active anti-protection settings in the server, including Anti-Spam,
        ///     Anti-Raid, Anti-Alt, and Anti-Mass-Mention.
        ///     It does not require any specific permissions to use.
        /// </remarks>
        public async Task AntiList()
        {
            var (spam, raid, alt, massMention, pattern, massPost, postChannel) = Service.GetAntiStats(ctx.Guild.Id);
            var imageHash = Service.GetAntiImageHashStats(ctx.Guild.Id);

            if (spam is null && raid is null && alt is null && massMention is null && pattern is null &&
                massPost is null && postChannel is null && imageHash is null)
            {
                await ReplyConfirmAsync(Strings.ProtNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.ProtActive(ctx.Guild.Id));

            if (spam != null)
            {
                embed.AddField(efb => efb.WithName("Anti-Spam")
                    .WithValue(GetAntiSpamString(spam).TrimTo(1024))
                    .WithIsInline(true));
            }

            if (raid != null)
            {
                embed.AddField(efb => efb.WithName("Anti-Raid")
                    .WithValue(GetAntiRaidString(raid).TrimTo(1024))
                    .WithIsInline(true));
            }

            if (alt is not null)
                embed.AddField("Anti-Alt", GetAntiAltString(alt), true);

            if (massMention != null)
            {
                embed.AddField("Anti-Mass-Mention", GetAntiMassMentionString(massMention).TrimTo(1024), true);
            }

            if (pattern != null)
            {
                embed.AddField("Anti-Pattern", GetAntiPatternString(pattern).TrimTo(1024), true);
            }

            if (massPost != null)
            {
                embed.AddField("Anti-Mass-Post", GetAntiMassPostString(massPost).TrimTo(1024), true);
            }

            if (postChannel != null)
            {
                embed.AddField("Anti-Post-Channel", GetAntiPostChannelString(postChannel).TrimTo(1024), true);
            }

            if (imageHash != null)
            {
                embed.AddField("Anti-Image-Hash", GetAntiImageHashString(imageHash).TrimTo(1024), true);
            }

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Builds the string for the Anti-Mass-Mention settings display.
        /// </summary>
        /// <param name="stats">The AntiMassMentionStats object.</param>
        /// <returns>A formatted string showing the current Anti-Mass-Mention settings.</returns>
        private string GetAntiMassMentionString(AntiMassMentionStats stats)
        {
            var settings = stats.AntiMassMentionSettings;

            var ignoreBots = settings.IgnoreBots ? "Yes" : "No";
            var add = "";
            if (settings.MuteTime > 0)
                add = $" ({TimeSpan.FromMinutes(settings.MuteTime).Humanize()})";

            return Strings.MassMentionStats(ctx.Guild.Id,
                Format.Bold(settings.MentionThreshold.ToString()),
                Format.Bold(settings.MaxMentionsInTimeWindow.ToString()),
                Format.Bold(settings.TimeWindowSeconds.ToString()),
                Format.Bold(settings.Action + add),
                Format.Bold(ignoreBots));
        }


        private string? GetAntiAltString(AntiAltStats alt)
        {
            return Strings.AntiAltStatus(ctx.Guild.Id,
                Format.Bold(TimeSpan.Parse(alt.MinAge).ToString(@"dd\d\ hh\h\ mm\m\ ")),
                Format.Bold(alt.Action.ToString()),
                Format.Bold(alt.Counter.ToString()));
        }

        private string? GetAntiSpamString(AntiSpamStats stats)
        {
            var settings = stats.AntiSpamSettings;
            var ignoredString = string.Join(", ", settings.AntiSpamIgnores.Select(c => $"<#{c.ChannelId}>"));

            if (string.IsNullOrWhiteSpace(ignoredString))
                ignoredString = "none";

            var add = "";
            if (settings.MuteTime > 0) add = $" ({TimeSpan.FromMinutes(settings.MuteTime).Humanize()})";

            return Strings.SpamStats(ctx.Guild.Id,
                Format.Bold(settings.MessageThreshold.ToString()),
                Format.Bold(settings.Action + add),
                ignoredString);
        }

        private string? GetAntiRaidString(AntiRaidStats stats)
        {
            var actionString = Format.Bold(stats.AntiRaidSettings.Action.ToString());

            if (stats.AntiRaidSettings.PunishDuration > 0)
                actionString += $" **({TimeSpan.FromMinutes(stats.AntiRaidSettings.PunishDuration).Humanize()})**";

            return Strings.RaidStats(ctx.Guild.Id,
                Format.Bold(stats.AntiRaidSettings.UserThreshold.ToString()),
                Format.Bold(stats.AntiRaidSettings.Seconds.ToString()),
                actionString);
        }

        private string? GetAntiPatternString(AntiPatternStats stats)
        {
            var settings = stats.AntiPatternSettings;
            var patterns = settings.AntiPatternPatterns?.ToList();
            var patternCount = patterns?.Count ?? 0;

            var add = "";
            if (settings.PunishDuration > 0)
                add = $" ({TimeSpan.FromMinutes(settings.PunishDuration).Humanize()})";

            return Strings.AntiPatternStats(ctx.Guild.Id,
                Format.Bold(settings.Action + add),
                Format.Bold(patternCount.ToString()),
                Format.Bold(stats.Counter.ToString()));
        }

        /// <summary>
        ///     Disables the Anti-Pattern protection for the guild.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiPattern()
        {
            if (await Service.TryStopAntiPattern(ctx.Guild.Id).ConfigureAwait(false))
            {
                await ReplyConfirmAsync(Strings.AntiPatternDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyErrorAsync(Strings.AntiPatternNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Configures the Anti-Pattern protection for the guild, setting the punishment action and optional duration.
        /// </summary>
        /// <param name="action">The punishment action to be taken against detected pattern matches.</param>
        /// <param name="punishTime">Optional: The duration of the punishment, if applicable.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiPattern(PunishmentAction action, [Remainder] StoopidTime? punishTime = null)
        {
            var punishTimeMinutes = (int?)punishTime?.Time.TotalMinutes ?? 0;

            if (punishTimeMinutes < 0)
                return;

            switch (action)
            {
                case PunishmentAction.Timeout when punishTime?.Time.Days > 28:
                    await ReplyErrorAsync("Timeout length cannot be longer than 28 days.").ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime?.Time == TimeSpan.Zero:
                    await ReplyErrorAsync("Timeout punishment requires a duration.").ConfigureAwait(false);
                    return;
            }

            var stats = await Service.StartAntiPatternAsync(ctx.Guild.Id, action, punishTimeMinutes)
                .ConfigureAwait(false);

            if (stats == null)
            {
                await ReplyErrorAsync(Strings.AntiPatternFailedStart(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var durationText = punishTimeMinutes > 0
                ? $" for **{TimeSpan.FromMinutes(punishTimeMinutes).Humanize()}**"
                : "";
            await ReplyConfirmAsync(Strings.AntiPatternEnabled(ctx.Guild.Id, action.ToString(), durationText))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Configures the Anti-Pattern protection for the guild, setting the punishment action with a role-based punishment.
        /// </summary>
        /// <param name="action">The punishment action to be taken against detected pattern matches.</param>
        /// <param name="role">The role to be assigned to users who match patterns.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiPattern(PunishmentAction action, [Remainder] IRole role)
        {
            var stats = await Service.StartAntiPatternAsync(ctx.Guild.Id, action, roleId: role.Id)
                .ConfigureAwait(false);

            if (stats == null)
            {
                await ReplyErrorAsync(Strings.AntiPatternFailedStart(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.AntiPatternEnabledRole(ctx.Guild.Id, action.ToString(), role.Mention))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a regex pattern to the Anti-Pattern protection.
        /// </summary>
        /// <param name="pattern">The regex pattern to match against usernames/display names.</param>
        /// <param name="name">Optional name for the pattern.</param>
        /// <param name="checkUsername">Whether to check usernames against this pattern (default: true).</param>
        /// <param name="checkDisplayName">Whether to check display names against this pattern (default: true).</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task PatternAdd(string pattern, string? name = null, bool checkUsername = true,
            bool checkDisplayName = true)
        {
            if (await Service.AddPatternAsync(ctx.Guild.Id, pattern, name, checkUsername, checkDisplayName)
                    .ConfigureAwait(false))
            {
                await ReplyConfirmAsync(Strings.PatternAdded(ctx.Guild.Id, name ?? "Unnamed", pattern, checkUsername,
                    checkDisplayName)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.PatternAddFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes a pattern from the Anti-Pattern protection.
        /// </summary>
        /// <param name="patternId">The ID of the pattern to remove.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task PatternRemove(int patternId)
        {
            if (await Service.RemovePatternAsync(ctx.Guild.Id, patternId).ConfigureAwait(false))
            {
                await ReplyConfirmAsync(Strings.PatternRemoved(ctx.Guild.Id, patternId)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.PatternRemoveFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Lists all patterns configured for the Anti-Pattern protection.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task PatternList()
        {
            var (_, _, _, _, patternStats, _, _) = Service.GetAntiStats(ctx.Guild.Id);

            if (patternStats == null)
            {
                await ReplyErrorAsync(Strings.AntiPatternNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var patterns = patternStats.AntiPatternSettings.AntiPatternPatterns?.ToList();
            if (patterns == null || patterns.Count == 0)
            {
                await ReplyConfirmAsync(Strings.PatternListEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.PatternListTitle(ctx.Guild.Id))
                .WithDescription(Strings.PatternListDesc(ctx.Guild.Id, patternStats.Action, patternStats.Counter));

            foreach (var pattern in patterns.Take(10)) // Limit to 10 patterns to avoid embed limits
            {
                var fieldName = $"ID: {pattern.Id} - {pattern.Name ?? "Unnamed"}";
                var fieldValue = $"**Pattern:** `{pattern.Pattern}`\n" +
                                 $"**Username:** {(pattern.CheckUsername ? "✅" : "❌")}\n" +
                                 $"**Display Name:** {(pattern.CheckDisplayName ? "✅" : "❌")}";
                embed.AddField(fieldName, fieldValue, true);
            }

            if (patterns.Count > 10)
            {
                embed.WithFooter(Strings.PatternListFooter(ctx.Guild.Id, patterns.Count));
            }

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Configures advanced anti-pattern settings.
        /// </summary>
        /// <param name="setting">The setting to configure.</param>
        /// <param name="value">The value to set.</param>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task PatternConfig(string setting, string value)
        {
            var (_, _, _, _, patternStats, _, _) = Service.GetAntiStats(ctx.Guild.Id);

            if (patternStats == null)
            {
                await ReplyErrorAsync(Strings.AntiPatternNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var success = false;
            var settingLower = setting.ToLower();

            switch (settingLower)
            {
                case "accountage":
                    if (bool.TryParse(value, out var checkAccountAge))
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            checkAccountAge);
                    }

                    break;
                case "maxaccountage":
                    if (int.TryParse(value, out var maxAccountAgeMonths) && maxAccountAgeMonths > 0)
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            maxAccountAgeMonths: maxAccountAgeMonths);
                    }

                    break;
                case "jointiming":
                    if (bool.TryParse(value, out var checkJoinTiming))
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            checkJoinTiming: checkJoinTiming);
                    }

                    break;
                case "maxjoinhours":
                    if (double.TryParse(value, out var maxJoinHours) && maxJoinHours > 0)
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id, maxJoinHours: maxJoinHours);
                    }

                    break;
                case "batchcreation":
                    if (bool.TryParse(value, out var checkBatchCreation))
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            checkBatchCreation: checkBatchCreation);
                    }

                    break;
                case "offlinestatus":
                    if (bool.TryParse(value, out var checkOfflineStatus))
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            checkOfflineStatus: checkOfflineStatus);
                    }

                    break;
                case "newaccounts":
                    if (bool.TryParse(value, out var checkNewAccounts))
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            checkNewAccounts: checkNewAccounts);
                    }

                    break;
                case "newaccountdays":
                    if (int.TryParse(value, out var newAccountDays) && newAccountDays > 0)
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            newAccountDays: newAccountDays);
                    }

                    break;
                case "minimumscore":
                    if (int.TryParse(value, out var minimumScore) && minimumScore > 0)
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id, minimumScore: minimumScore);
                    }

                    break;
                default:
                    await ReplyErrorAsync(Strings.PatternConfigUnknownSetting(ctx.Guild.Id, setting))
                        .ConfigureAwait(false);
                    return;
            }

            if (success)
            {
                await ReplyConfirmAsync(Strings.PatternConfigUpdated(ctx.Guild.Id, setting, value))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.PatternConfigUpdateFailed(ctx.Guild.Id, setting)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Shows the current anti-pattern configuration.
        /// </summary>
        /// <remarks>
        ///     This command is restricted to users with Administrator permissions.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task PatternConfig()
        {
            var (_, _, _, _, patternStats, _, _) = Service.GetAntiStats(ctx.Guild.Id);

            if (patternStats == null)
            {
                await ReplyErrorAsync(Strings.AntiPatternNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var settings = patternStats.AntiPatternSettings;
            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.PatternConfigTitle(ctx.Guild.Id))
                .WithDescription(Strings.PatternConfigDesc(ctx.Guild.Id, settings.Action, settings.MinimumScore))
                .AddField("Account Age Check",
                    $"**Enabled:** {settings.CheckAccountAge}\n**Max Age:** {settings.MaxAccountAgeMonths} months",
                    true)
                .AddField("Join Timing Check",
                    $"**Enabled:** {settings.CheckJoinTiming}\n**Max Hours:** {settings.MaxJoinHours}h", true)
                .AddField("Batch Creation Check", $"**Enabled:** {settings.CheckBatchCreation}", true)
                .AddField("Offline Status Check", $"**Enabled:** {settings.CheckOfflineStatus}", true)
                .AddField("New Account Check",
                    $"**Enabled:** {settings.CheckNewAccounts}\n**Days:** {settings.NewAccountDays}", true)
                .AddField("Statistics",
                    $"**Patterns:** {settings.AntiPatternPatterns?.Count() ?? 0}\n**Triggered:** {patternStats.Counter} times",
                    true);

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Builds the string for the Anti-Mass-Post settings display.
        /// </summary>
        private string GetAntiMassPostString(AntiMassPostStats stats)
        {
            var settings = stats.AntiMassPostSettings;
            var add = "";
            if (settings.PunishDuration > 0)
                add = $" ({TimeSpan.FromMinutes(settings.PunishDuration).Humanize()})";

            return Strings.AntiMassPostStats(ctx.Guild.Id,
                Format.Bold(settings.Action.ToString()),
                add,
                Format.Bold(settings.ChannelThreshold.ToString()),
                Format.Bold(settings.TimeWindowSeconds.ToString()),
                Format.Bold(settings.CheckLinksOnly.ToString()),
                Format.Bold(stats.Counter.ToString()));
        }

        /// <summary>
        ///     Builds the string for the Anti-Post-Channel settings display.
        /// </summary>
        private string GetAntiPostChannelString(AntiPostChannelStats stats)
        {
            var settings = stats.AntiPostChannelSettings;
            var add = "";
            if (settings.PunishDuration > 0)
                add = $" ({TimeSpan.FromMinutes(settings.PunishDuration).Humanize()})";

            var channelCount = settings.AntiPostChannelChannels?.Count() ?? 0;
            return Strings.AntiPostChannelStats(ctx.Guild.Id,
                Format.Bold(settings.Action.ToString()),
                add,
                Format.Bold(channelCount.ToString()),
                Format.Bold(stats.Counter.ToString()));
        }

        /// <summary>
        ///     Disables the Anti-Mass-Post protection for the guild.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiMassPost()
        {
            if (await Service.TryStopAntiMassPost(ctx.Guild.Id).ConfigureAwait(false))
            {
                await ReplyConfirmAsync(Strings.AntiMassPostDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyErrorAsync(Strings.AntiMassPostNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Configures the Anti-Mass-Post protection for the guild.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiMassPost(int channelThreshold, int seconds, PunishmentAction action,
            [Remainder] StoopidTime? punishTime = null)
        {
            if (channelThreshold is < 2 or > 20)
            {
                await ReplyErrorAsync("Channel threshold must be between 2 and 20.").ConfigureAwait(false);
                return;
            }

            if (seconds is < 10 or > 600)
            {
                await ReplyErrorAsync("Time window must be between 10 and 600 seconds.").ConfigureAwait(false);
                return;
            }

            var punishDuration = (int?)punishTime?.Time.TotalMinutes ?? 0;

            switch (action)
            {
                case PunishmentAction.Timeout when punishTime?.Time.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime?.Time == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            var result = await Service.StartAntiMassPostAsync(
                ctx.Guild.Id,
                channelThreshold,
                seconds,
                0.8, // contentSimilarityThreshold
                20, // minContentLength
                true, // checkLinksOnly
                true, // checkDuplicateContent
                false, // requireIdenticalContent
                false, // caseSensitive
                true, // deleteMessages
                true, // notifyUser
                action,
                punishDuration,
                null, // roleId
                true, // ignoreBots
                50 // maxMessagesTracked
            ).ConfigureAwait(false);

            if (result != null)
            {
                var durationText = punishDuration > 0 ? $" for {TimeSpan.FromMinutes(punishDuration).Humanize()}" : "";
                await ReplyConfirmAsync(
                    Strings.AntiMassPostEnabled(ctx.Guild.Id, channelThreshold, seconds, action.ToString(),
                        durationText)
                ).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.AntiMassPostFailedStart(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Disables the Anti-Post-Channel protection for the guild.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiPostChannel()
        {
            if (await Service.TryStopAntiPostChannel(ctx.Guild.Id).ConfigureAwait(false))
            {
                await ReplyConfirmAsync(Strings.AntiPostChannelDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyErrorAsync(Strings.AntiPostChannelNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Configures the Anti-Post-Channel protection for the guild.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiPostChannel(PunishmentAction action, [Remainder] StoopidTime? punishTime = null)
        {
            var punishDuration = (int?)punishTime?.Time.TotalMinutes ?? 0;

            switch (action)
            {
                case PunishmentAction.Timeout when punishTime?.Time.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime?.Time == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            var result = await Service.StartAntiPostChannelAsync(
                ctx.Guild.Id,
                action,
                punishDuration,
                null, // roleId
                true, // deleteMessages
                true, // notifyUser
                true, // ignoreBots
                ctx.Channel.Id
            ).ConfigureAwait(false);

            if (result != null)
            {
                var durationText = punishDuration > 0 ? $" for {TimeSpan.FromMinutes(punishDuration).Humanize()}" : "";
                await ReplyConfirmAsync(
                    Strings.AntiPostChannelEnabled(ctx.Guild.Id, action.ToString(), durationText)
                ).ConfigureAwait(false);
                await Service.UpdateAntiPostChannelStatusEmbedAsync(ctx.Guild.Id).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.AntiPostChannelFailedStart(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Adds a honeypot channel to the Anti-Post-Channel protection.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiPostChannelAdd(ITextChannel channel)
        {
            if (await Service.AddAntiPostChannelAsync(ctx.Guild.Id, channel.Id).ConfigureAwait(false))
            {
                await ReplyConfirmAsync(Strings.AntiPostChannelAdded(ctx.Guild.Id, channel.Mention))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.AntiPostChannelAddFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes a honeypot channel from the Anti-Post-Channel protection.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiPostChannelRemove(ITextChannel channel)
        {
            if (await Service.RemoveAntiPostChannelAsync(ctx.Guild.Id, channel.Id).ConfigureAwait(false))
            {
                await ReplyConfirmAsync(Strings.AntiPostChannelRemoved(ctx.Guild.Id, channel.Mention))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.AntiPostChannelRemoveFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Disables the Anti-Image-Hash protection for the guild. The blocked image list is kept.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiImageHash()
        {
            if (await Service.TryStopAntiImageHash(ctx.Guild.Id).ConfigureAwait(false))
            {
                await ReplyConfirmAsync(Strings.AntiImageHashDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyErrorAsync(Strings.AntiImageHashNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Enables the Anti-Image-Hash protection for the guild, choosing the action taken when someone posts an image that
        ///     matches the blocklist.
        /// </summary>
        /// <param name="action">The action taken against the poster. Individual blocked images may override it.</param>
        /// <param name="tolerance">
        ///     How many of the 256 PDQ hash bits may differ for an image to still count as a match. The default of 31 is PDQ's
        ///     standard "same image" threshold; values above about 48 start producing false positives.
        /// </param>
        /// <param name="punishTime">The punishment duration, for actions that support one.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiImageHash(PunishmentAction action, int tolerance = 31,
            [Remainder] StoopidTime? punishTime = null)
        {
            var punishDuration = (int?)punishTime?.Time.TotalMinutes ?? 0;

            switch (action)
            {
                case PunishmentAction.Timeout when punishTime?.Time.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime?.Time == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            var result = await Service.StartAntiImageHashAsync(
                ctx.Guild.Id,
                action,
                punishDuration,
                null,
                tolerance,
                true, // deleteMessages
                true, // notifyUser
                true, // ignoreBots
                true, // checkEmbeds
                true, // checkBorders
                true, // usePresetList
                8 // maxImageSizeMb
            ).ConfigureAwait(false);

            if (result is null)
            {
                await ReplyErrorAsync(Strings.AntiImageHashFailedStart(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var durationText = punishDuration > 0 ? $" for {TimeSpan.FromMinutes(punishDuration).Humanize()}" : "";
            await ReplyConfirmAsync(Strings.AntiImageHashEnabled(ctx.Guild.Id, action.ToString(), durationText,
                result.AntiImageHashSettings.HashThreshold)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Blocks an image, using the guild default action. The image is taken from an attachment on this message, from the
        ///     message being replied to, or from a URL given as the first word.
        /// </summary>
        /// <param name="name">An optional label for the blocked image, or the image URL followed by a label.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(0)]
        public Task BlockImage([Remainder] string? name = null)
        {
            return BlockImageInternal(null, name);
        }

        /// <summary>
        ///     Blocks an image with an action that overrides the guild default, so one image can simply be deleted while another
        ///     gets the poster banned.
        /// </summary>
        /// <param name="action">The action taken against anyone posting this specific image.</param>
        /// <param name="name">An optional label for the blocked image, or the image URL followed by a label.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(1)]
        public Task BlockImage(PunishmentAction action, [Remainder] string? name = null)
        {
            return BlockImageInternal(action, name);
        }

        private async Task BlockImageInternal(PunishmentAction? action, string? name)
        {
            if (Service.GetAntiImageHashStats(ctx.Guild.Id) is null)
            {
                await ReplyErrorAsync(Strings.AntiImageHashNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var url = ResolveImageUrl(ref name);
            if (url is null)
            {
                await ReplyErrorAsync(Strings.ImageHashNoImage(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var hashSet = await imageHashing.ComputeHashSetFromUrlAsync(url).ConfigureAwait(false);
            if (hashSet is null)
            {
                await ReplyErrorAsync(Strings.ImageHashUnreadable(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (hashSet.Quality < ImageHashingService.MinReliableQuality)
            {
                await ReplyErrorAsync(Strings.ImageHashLowQuality(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var entry = await Service
                .AddBannedImageHashAsync(ctx.Guild.Id, hashSet, name, url, ctx.User.Id, action)
                .ConfigureAwait(false);

            if (entry is null)
            {
                await ReplyErrorAsync(Strings.ImageHashExists(ctx.Guild.Id, hashSet.Hash)).ConfigureAwait(false);
                return;
            }

            var stats = Service.GetAntiImageHashStats(ctx.Guild.Id);
            var effectiveAction = (PunishmentAction)(entry.Action ?? stats?.Action ?? (int)PunishmentAction.Ban);
            var label = string.IsNullOrWhiteSpace(entry.Name) ? "" : $" as **{entry.Name}**";

            await ReplyConfirmAsync(Strings.ImageHashAdded(ctx.Guild.Id, entry.Hash, label, effectiveAction.ToString()))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes an image from the blocklist by its ID, as shown by the blocked image list.
        /// </summary>
        /// <param name="hashId">The ID of the blocked image.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task UnblockImage(int hashId)
        {
            if (await Service.RemoveBannedImageHashAsync(ctx.Guild.Id, hashId).ConfigureAwait(false))
                await ReplyConfirmAsync(Strings.ImageHashRemoved(ctx.Guild.Id, hashId)).ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.ImageHashRemoveFailed(ctx.Guild.Id, hashId)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows the perceptual hash of an image without blocking it, so it can be copied into the dashboard or another
        ///     server.
        /// </summary>
        /// <param name="url">An optional image URL. Ignored if the message has an attachment or replies to an image.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ImageHash([Remainder] string? url = null)
        {
            var resolved = ResolveImageUrl(ref url);
            if (resolved is null)
            {
                await ReplyErrorAsync(Strings.ImageHashNoImage(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var hashSet = await imageHashing.ComputeHashSetFromUrlAsync(resolved).ConfigureAwait(false);
            if (hashSet is null)
            {
                await ReplyErrorAsync(Strings.ImageHashUnreadable(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.ImageHashComputed(ctx.Guild.Id, hashSet.Hash)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists the blocked images for the guild along with how many times each one has been caught.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task BlockedImages()
        {
            var hashes = await Service.GetBannedImageHashesAsync(ctx.Guild.Id).ConfigureAwait(false);

            if (hashes.Count == 0)
            {
                await ReplyErrorAsync(Strings.ImageHashListEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var defaultAction = Service.GetAntiImageHashStats(ctx.Guild.Id)?.Action ?? (int)PunishmentAction.Ban;

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex((hashes.Count - 1) / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);

                var entries = hashes.Skip(page * 10).Take(10).Select(h =>
                {
                    var action = (PunishmentAction)(h.Action ?? defaultAction);
                    var last = h.LastTriggeredAt.HasValue
                        ? Strings.ImageHashListLast(ctx.Guild.Id,
                            new DateTimeOffset(h.LastTriggeredAt.Value, TimeSpan.Zero).ToUnixTimeSeconds())
                        : Strings.ImageHashListNever(ctx.Guild.Id);

                    return Strings.ImageHashListEntry(ctx.Guild.Id, h.Id,
                        string.IsNullOrWhiteSpace(h.Name) ? "Unnamed" : h.Name, h.Hash, action.ToString(), h.HitCount,
                        last);
                });

                return new PageBuilder()
                    .WithTitle(Strings.ImageHashListTitle(ctx.Guild.Id))
                    .WithDescription(string.Join("\n\n", entries))
                    .WithOkColor();
            }
        }

        /// <summary>
        ///     Toggles the list of known scam images that ships with the bot, so the guild blocks the images every server is
        ///     seeing without having to collect them first.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiImageHashPreset()
        {
            var stats = Service.GetAntiImageHashStats(ctx.Guild.Id);
            if (stats is null)
            {
                await ReplyErrorAsync(Strings.AntiImageHashNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var enabled = !stats.AntiImageHashSettings.UsePresetList;
            await Service.SetPresetScamImagesAsync(ctx.Guild.Id, enabled).ConfigureAwait(false);

            await ReplyConfirmAsync(enabled
                    ? Strings.ImageHashPresetEnabled(ctx.Guild.Id, Service.PresetScamImageCount)
                    : Strings.ImageHashPresetDisabled(ctx.Guild.Id))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles a role as exempt from Anti-Image-Hash protection.
        /// </summary>
        /// <param name="role">The role to toggle.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiImageHashIgnore([Remainder] IRole role)
        {
            var added = await Service.ToggleAntiImageHashIgnoredRoleAsync(ctx.Guild.Id, role.Id).ConfigureAwait(false);

            await ReplyConfirmAsync(added
                    ? Strings.ImageHashIgnoredRoleAdded(ctx.Guild.Id, role.Mention)
                    : Strings.ImageHashIgnoredRoleRemoved(ctx.Guild.Id, role.Mention))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles a channel as exempt from Anti-Image-Hash protection.
        /// </summary>
        /// <param name="channel">The channel to toggle.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AntiImageHashIgnore([Remainder] ITextChannel channel)
        {
            var added = await Service.ToggleAntiImageHashIgnoredChannelAsync(ctx.Guild.Id, channel.Id)
                .ConfigureAwait(false);

            await ReplyConfirmAsync(added
                    ? Strings.ImageHashIgnoredChannelAdded(ctx.Guild.Id, channel.Mention)
                    : Strings.ImageHashIgnoredChannelRemoved(ctx.Guild.Id, channel.Mention))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Finds the image to hash: an attachment on the invoking message, an image on the message being replied to, or a URL
        ///     given as the first word of the remaining text. When the first word is a URL it is stripped from the label.
        /// </summary>
        private string? ResolveImageUrl(ref string? text)
        {
            var attachment = ctx.Message.Attachments.FirstOrDefault(a =>
                a.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true);

            if (attachment is not null)
                return attachment.Url;

            if (ctx.Message.ReferencedMessage is { } reply)
            {
                var replyAttachment = reply.Attachments.FirstOrDefault(a =>
                    a.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true);

                if (replyAttachment is not null)
                    return replyAttachment.Url;

                var embedImage = reply.Embeds
                    .Select(e => e.Image?.Url ?? e.Thumbnail?.Url)
                    .FirstOrDefault(u => u is not null);

                if (embedImage is not null)
                    return embedImage;
            }

            if (string.IsNullOrWhiteSpace(text))
                return null;

            var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (!Uri.TryCreate(parts[0], UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return null;

            text = parts.Length > 1 ? parts[1] : null;
            return uri.ToString();
        }

        /// <summary>
        ///     Builds the string for the Anti-Image-Hash settings display.
        /// </summary>
        private string GetAntiImageHashString(AntiImageHashStats stats)
        {
            var settings = stats.AntiImageHashSettings;
            var add = "";
            if (settings.PunishDuration > 0)
                add = $" ({TimeSpan.FromMinutes(settings.PunishDuration).Humanize()})";

            return Strings.AntiImageHashStats(ctx.Guild.Id,
                Format.Bold(((PunishmentAction)settings.Action).ToString()),
                add,
                Format.Bold(stats.Hashes.Count.ToString()),
                Format.Bold(settings.HashThreshold.ToString()),
                Format.Bold(stats.Counter.ToString()));
        }
    }
}