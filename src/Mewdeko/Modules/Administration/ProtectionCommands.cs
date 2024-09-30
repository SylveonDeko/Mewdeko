using Discord.Commands;
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
    public class ProtectionCommands : MewdekoSubmodule<ProtectionService>
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
                await ReplyConfirmLocalizedAsync("prot_disable", "Anti-Alt").ConfigureAwait(false);
                return;
            }

            await ReplyErrorLocalizedAsync("protection_not_running", "Anti-Alt").ConfigureAwait(false);
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
                    await ReplyErrorLocalizedAsync("timeout_length_too_long").ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime.Time == TimeSpan.Zero:
                    await ReplyErrorLocalizedAsync("timeout_needs_time").ConfigureAwait(false);
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
                await ReplyConfirmLocalizedAsync("prot_disable", "Anti-Raid");
            else
                await ReplyErrorLocalizedAsync("protection_not_running", "Anti-Raid");
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
                    await ReplyErrorLocalizedAsync("timeout_length_too_long").ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime.Time == TimeSpan.Zero:
                    await ReplyErrorLocalizedAsync("timeout_needs_time").ConfigureAwait(false);
                    return;
            }

            if (action == PunishmentAction.AddRole)
            {
                await ReplyErrorLocalizedAsync("punishment_unsupported", action).ConfigureAwait(false);
                return;
            }

            if (userThreshold is < 2 or > 30)
            {
                await ReplyErrorLocalizedAsync("raid_cnt", 2, 30).ConfigureAwait(false);
                return;
            }

            if (seconds is < 2 or > 300)
            {
                await ReplyErrorLocalizedAsync("raid_time", 2, 300).ConfigureAwait(false);
                return;
            }

            if (punishTime is not null)
            {
                if (!ProtectionService.IsDurationAllowed(action))
                    await ReplyErrorLocalizedAsync("prot_cant_use_time").ConfigureAwait(false);
            }

            var time = (int?)punishTime?.Time.TotalMinutes ?? 0;
            if (time is < 0 or > 60 * 24)
                return;

            var stats = await Service.StartAntiRaidAsync(ctx.Guild.Id, userThreshold, seconds,
                action, time).ConfigureAwait(false);

            if (stats == null) return;

            await ctx.Channel.SendConfirmAsync(GetText("prot_enable", "Anti-Raid"),
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
                await ReplyConfirmLocalizedAsync("prot_disable", "Anti-Spam");
            else
                await ReplyErrorLocalizedAsync("protection_not_running", "Anti-Spam");
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
                    await ReplyErrorLocalizedAsync("prot_cant_use_time").ConfigureAwait(false);
                    return;
                }
            }

            var time = (int?)timeData?.Time.TotalMinutes ?? 0;
            if (time is < 0 or > 60 * 24)
                return;

            switch (action)
            {
                case PunishmentAction.Timeout when timeData.Time.Days > 28:
                    await ReplyErrorLocalizedAsync("timeout_length_too_long").ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when timeData.Time == TimeSpan.Zero:
                    await ReplyErrorLocalizedAsync("timeout_needs_time").ConfigureAwait(false);
                    return;
            }

            var stats = await Service.StartAntiSpamAsync(ctx.Guild.Id, messageCount, action, time, role?.Id)
                .ConfigureAwait(false);

            await ctx.Channel.SendConfirmAsync(GetText("prot_enable", "Anti-Spam"),
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
                await ReplyErrorLocalizedAsync("protection_not_running", "Anti-Spam").ConfigureAwait(false);
                return;
            }

            await ReplyConfirmLocalizedAsync(added.Value ? "spam_ignore" : "spam_not_ignore", "Anti-Spam")
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
                await ReplyConfirmLocalizedAsync("prot_disable", "Anti-Mass-Mention").ConfigureAwait(false);
                return;
            }

            await ReplyErrorLocalizedAsync("protection_not_running", "Anti-Mass-Mention").ConfigureAwait(false);
        }

        /// <summary>
        ///     Configures the Anti-Mass-Mention protection for the guild, setting the mention threshold for a single message,
        ///     the time window for mention tracking, the maximum allowed mentions in the time window, and the punishment action.
        /// </summary>
        /// <param name="mentionThreshold">The number of mentions allowed in a single message before triggering protection.</param>
        /// <param name="timeWindowSeconds">The time window (in seconds) to observe mentions.</param>
        /// <param name="maxMentionsInTimeWindow">The maximum allowed mentions in the specified time window.</param>
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
        public async Task AntiMassMention(int mentionThreshold, int timeWindowSeconds, int maxMentionsInTimeWindow, bool ignoreBots,
            PunishmentAction action, [Remainder] StoopidTime? punishTime = null)
        {
            var punishTimeMinutes = (int?)punishTime?.Time.TotalMinutes ?? 0;

            if (punishTimeMinutes < 0 || mentionThreshold < 1 || timeWindowSeconds < 1 || maxMentionsInTimeWindow < 1)
                return;

            switch (action)
            {
                case PunishmentAction.Timeout when punishTime.Time.Days > 28:
                    await ReplyErrorLocalizedAsync("timeout_length_too_long").ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime.Time == TimeSpan.Zero:
                    await ReplyErrorLocalizedAsync("timeout_needs_time").ConfigureAwait(false);
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
        public async Task AntiMassMention(int mentionThreshold, int timeWindowSeconds, int maxMentionsInTimeWindow, bool ignoreBots,
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
            var (spam, raid, alt, massMention) = Service.GetAntiStats(ctx.Guild.Id);

            if (spam is null && raid is null && alt is null && massMention is null)
            {
                await ReplyConfirmLocalizedAsync("prot_none").ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(GetText("prot_active"));

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

            return GetText("mass_mention_stats",
                Format.Bold(settings.MentionThreshold.ToString()),
                Format.Bold(settings.MaxMentionsInTimeWindow.ToString()),
                Format.Bold(settings.TimeWindowSeconds.ToString()),
                Format.Bold(settings.Action + add),
                Format.Bold(ignoreBots));
        }


        private string? GetAntiAltString(AntiAltStats alt)
        {
            return GetText("anti_alt_status",
                Format.Bold(TimeSpan.Parse(alt.MinAge).ToString(@"dd\d\ hh\h\ mm\m\ ")),
                Format.Bold(alt.Action.ToString()),
                Format.Bold(alt.Counter.ToString()));
        }

        private string? GetAntiSpamString(AntiSpamStats stats)
        {
            var settings = stats.AntiSpamSettings;
            var ignoredString = string.Join(", ", settings.IgnoredChannels.Select(c => $"<#{c.ChannelId}>"));

            if (string.IsNullOrWhiteSpace(ignoredString))
                ignoredString = "none";

            var add = "";
            if (settings.MuteTime > 0) add = $" ({TimeSpan.FromMinutes(settings.MuteTime).Humanize()})";

            return GetText("spam_stats",
                Format.Bold(settings.MessageThreshold.ToString()),
                Format.Bold(settings.Action + add),
                ignoredString);
        }

        private string? GetAntiRaidString(AntiRaidStats stats)
        {
            var actionString = Format.Bold(stats.AntiRaidSettings.Action.ToString());

            if (stats.AntiRaidSettings.PunishDuration > 0)
                actionString += $" **({TimeSpan.FromMinutes(stats.AntiRaidSettings.PunishDuration).Humanize()})**";

            return GetText("raid_stats",
                Format.Bold(stats.AntiRaidSettings.UserThreshold.ToString()),
                Format.Bold(stats.AntiRaidSettings.Seconds.ToString()),
                actionString);
        }
    }
}