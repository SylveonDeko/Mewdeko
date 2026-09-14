using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Administration;

/// <summary>
///     Slash commands for managing the Anti-Alt, Anti-Raid, Anti-Spam, Anti-Mass-Mention, Anti-Pattern, Anti-Mass-Post
///     and Anti-Image-Hash protection settings.
/// </summary>
[Group("protection", "Anti-alt, anti-raid, anti-spam and other server protections")]
public class SlashProtection : MewdekoSlashModuleBase<ProtectionService>
{
    /// <summary>
    ///     The advanced anti-pattern settings that can be changed with the pattern config command.
    /// </summary>
    public enum PatternConfigSetting
    {
        /// <summary>
        ///     Whether account age is checked.
        /// </summary>
        AccountAge,

        /// <summary>
        ///     The maximum account age, in months, that still counts as suspicious.
        /// </summary>
        MaxAccountAge,

        /// <summary>
        ///     Whether join timing is checked.
        /// </summary>
        JoinTiming,

        /// <summary>
        ///     The maximum number of hours between joins that still counts as suspicious.
        /// </summary>
        MaxJoinHours,

        /// <summary>
        ///     Whether batch account creation is checked.
        /// </summary>
        BatchCreation,

        /// <summary>
        ///     Whether offline status is checked.
        /// </summary>
        OfflineStatus,

        /// <summary>
        ///     Whether new accounts are checked.
        /// </summary>
        NewAccounts,

        /// <summary>
        ///     The number of days an account counts as new.
        /// </summary>
        NewAccountDays,

        /// <summary>
        ///     The minimum score required to trigger punishment.
        /// </summary>
        MinimumScore
    }

    /// <summary>
    ///     The action a protection command performs: show the current settings, enable the protection, or disable it.
    /// </summary>
    public enum ProtectionAction
    {
        /// <summary>
        ///     Shows the current settings.
        /// </summary>
        Status,

        /// <summary>
        ///     Enables the protection with the given settings.
        /// </summary>
        Enable,

        /// <summary>
        ///     Disables the protection.
        /// </summary>
        Disable
    }

    /// <summary>
    ///     Displays the current status of every protection, including Anti-Spam, Anti-Raid, Anti-Alt, Anti-Mass-Mention,
    ///     Anti-Pattern, Anti-Mass-Post, Anti-Post-Channel and Anti-Image-Hash.
    /// </summary>
    [SlashCommand("list", "Shows every active protection and its settings")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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
            embed.AddField("Anti-Spam", GetAntiSpamString(Strings, ctx.Guild.Id, spam).TrimTo(1024), true);

        if (raid != null)
            embed.AddField("Anti-Raid", GetAntiRaidString(Strings, ctx.Guild.Id, raid).TrimTo(1024), true);

        if (alt is not null)
            embed.AddField("Anti-Alt", GetAntiAltString(Strings, ctx.Guild.Id, alt), true);

        if (massMention != null)
        {
            embed.AddField("Anti-Mass-Mention",
                GetAntiMassMentionString(Strings, ctx.Guild.Id, massMention).TrimTo(1024), true);
        }

        if (pattern != null)
            embed.AddField("Anti-Pattern", GetAntiPatternString(Strings, ctx.Guild.Id, pattern).TrimTo(1024), true);

        if (massPost != null)
            embed.AddField("Anti-Mass-Post", GetAntiMassPostString(Strings, ctx.Guild.Id, massPost).TrimTo(1024), true);

        if (postChannel != null)
        {
            embed.AddField("Anti-Post-Channel",
                GetAntiPostChannelString(Strings, ctx.Guild.Id, postChannel).TrimTo(1024), true);
        }

        if (imageHash != null)
        {
            embed.AddField("Anti-Image-Hash", GetAntiImageHashString(Strings, ctx.Guild.Id, imageHash).TrimTo(1024),
                true);
        }

        await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Builds the string for the Anti-Mass-Mention settings display.
    /// </summary>
    /// <param name="strings">The bot strings.</param>
    /// <param name="guildId">The guild id.</param>
    /// <param name="stats">The AntiMassMentionStats object.</param>
    /// <returns>A formatted string showing the current Anti-Mass-Mention settings.</returns>
    internal static string GetAntiMassMentionString(GeneratedBotStrings strings, ulong guildId,
        AntiMassMentionStats stats)
    {
        var settings = stats.AntiMassMentionSettings;

        var ignoreBots = settings.IgnoreBots ? "Yes" : "No";
        var add = "";
        if (settings.MuteTime > 0)
            add = $" ({TimeSpan.FromMinutes(settings.MuteTime).Humanize()})";

        return strings.MassMentionStats(guildId,
            Format.Bold(settings.MentionThreshold.ToString()),
            Format.Bold(settings.MaxMentionsInTimeWindow.ToString()),
            Format.Bold(settings.TimeWindowSeconds.ToString()),
            Format.Bold(settings.Action + add),
            Format.Bold(ignoreBots));
    }

    /// <summary>
    ///     Builds the string for the Anti-Alt settings display.
    /// </summary>
    /// <param name="strings">The bot strings.</param>
    /// <param name="guildId">The guild id.</param>
    /// <param name="alt">The AntiAltStats object.</param>
    /// <returns>A formatted string showing the current Anti-Alt settings.</returns>
    internal static string GetAntiAltString(GeneratedBotStrings strings, ulong guildId, AntiAltStats alt)
    {
        return strings.AntiAltStatus(guildId,
            Format.Bold(TimeSpan.Parse(alt.MinAge).ToString(@"dd\d\ hh\h\ mm\m\ ")),
            Format.Bold(alt.Action.ToString()),
            Format.Bold(alt.Counter.ToString()));
    }

    /// <summary>
    ///     Builds the string for the Anti-Spam settings display.
    /// </summary>
    /// <param name="strings">The bot strings.</param>
    /// <param name="guildId">The guild id.</param>
    /// <param name="stats">The AntiSpamStats object.</param>
    /// <returns>A formatted string showing the current Anti-Spam settings.</returns>
    internal static string GetAntiSpamString(GeneratedBotStrings strings, ulong guildId, AntiSpamStats stats)
    {
        var settings = stats.AntiSpamSettings;
        var ignoredString = string.Join(", ", settings.AntiSpamIgnores.Select(c => $"<#{c.ChannelId}>"));

        if (string.IsNullOrWhiteSpace(ignoredString))
            ignoredString = "none";

        var add = "";
        if (settings.MuteTime > 0) add = $" ({TimeSpan.FromMinutes(settings.MuteTime).Humanize()})";

        return strings.SpamStats(guildId,
            Format.Bold(settings.MessageThreshold.ToString()),
            Format.Bold(settings.Action + add),
            ignoredString);
    }

    /// <summary>
    ///     Builds the string for the Anti-Raid settings display.
    /// </summary>
    /// <param name="strings">The bot strings.</param>
    /// <param name="guildId">The guild id.</param>
    /// <param name="stats">The AntiRaidStats object.</param>
    /// <returns>A formatted string showing the current Anti-Raid settings.</returns>
    internal static string GetAntiRaidString(GeneratedBotStrings strings, ulong guildId, AntiRaidStats stats)
    {
        var actionString = Format.Bold(stats.AntiRaidSettings.Action.ToString());

        if (stats.AntiRaidSettings.PunishDuration > 0)
            actionString += $" **({TimeSpan.FromMinutes(stats.AntiRaidSettings.PunishDuration).Humanize()})**";

        return strings.RaidStats(guildId,
            Format.Bold(stats.AntiRaidSettings.UserThreshold.ToString()),
            Format.Bold(stats.AntiRaidSettings.Seconds.ToString()),
            actionString);
    }

    /// <summary>
    ///     Builds the string for the Anti-Pattern settings display.
    /// </summary>
    /// <param name="strings">The bot strings.</param>
    /// <param name="guildId">The guild id.</param>
    /// <param name="stats">The AntiPatternStats object.</param>
    /// <returns>A formatted string showing the current Anti-Pattern settings.</returns>
    internal static string GetAntiPatternString(GeneratedBotStrings strings, ulong guildId, AntiPatternStats stats)
    {
        var settings = stats.AntiPatternSettings;
        var patterns = settings.AntiPatternPatterns?.ToList();
        var patternCount = patterns?.Count ?? 0;

        var add = "";
        if (settings.PunishDuration > 0)
            add = $" ({TimeSpan.FromMinutes(settings.PunishDuration).Humanize()})";

        return strings.AntiPatternStats(guildId,
            Format.Bold(settings.Action + add),
            Format.Bold(patternCount.ToString()),
            Format.Bold(stats.Counter.ToString()));
    }

    /// <summary>
    ///     Builds the string for the Anti-Mass-Post settings display.
    /// </summary>
    /// <param name="strings">The bot strings.</param>
    /// <param name="guildId">The guild id.</param>
    /// <param name="stats">The AntiMassPostStats object.</param>
    /// <returns>A formatted string showing the current Anti-Mass-Post settings.</returns>
    internal static string GetAntiMassPostString(GeneratedBotStrings strings, ulong guildId, AntiMassPostStats stats)
    {
        var settings = stats.AntiMassPostSettings;
        var add = "";
        if (settings.PunishDuration > 0)
            add = $" ({TimeSpan.FromMinutes(settings.PunishDuration).Humanize()})";

        return strings.AntiMassPostStats(guildId,
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
    /// <param name="strings">The bot strings.</param>
    /// <param name="guildId">The guild id.</param>
    /// <param name="stats">The AntiPostChannelStats object.</param>
    /// <returns>A formatted string showing the current Anti-Post-Channel settings.</returns>
    internal static string GetAntiPostChannelString(GeneratedBotStrings strings, ulong guildId,
        AntiPostChannelStats stats)
    {
        var settings = stats.AntiPostChannelSettings;
        var add = "";
        if (settings.PunishDuration > 0)
            add = $" ({TimeSpan.FromMinutes(settings.PunishDuration).Humanize()})";

        var channelCount = settings.AntiPostChannelChannels?.Count() ?? 0;
        return strings.AntiPostChannelStats(guildId,
            Format.Bold(settings.Action.ToString()),
            add,
            Format.Bold(channelCount.ToString()),
            Format.Bold(stats.Counter.ToString()));
    }

    /// <summary>
    ///     Builds the string for the Anti-Image-Hash settings display.
    /// </summary>
    /// <param name="strings">The bot strings.</param>
    /// <param name="guildId">The guild id.</param>
    /// <param name="stats">The AntiImageHashStats object.</param>
    /// <returns>A formatted string showing the current Anti-Image-Hash settings.</returns>
    internal static string GetAntiImageHashString(GeneratedBotStrings strings, ulong guildId,
        AntiImageHashStats stats)
    {
        var settings = stats.AntiImageHashSettings;
        var add = "";
        if (settings.PunishDuration > 0)
            add = $" ({TimeSpan.FromMinutes(settings.PunishDuration).Humanize()})";

        return strings.AntiImageHashStats(guildId,
            Format.Bold(((PunishmentAction)settings.Action).ToString()),
            add,
            Format.Bold(stats.Hashes.Count.ToString()),
            Format.Bold(settings.HashThreshold.ToString()),
            Format.Bold(stats.Counter.ToString()));
    }

    /// <summary>
    ///     Anti-Alt protection, which punishes accounts younger than a configured age when they join.
    /// </summary>
    [Group("alt", "Punish accounts younger than a given age when they join")]
    public class ProtectionAlt : MewdekoSlashSubmodule<ProtectionService>
    {
        /// <summary>
        ///     Shows, enables or disables the Anti-Alt protection. Enabling sets the minimum account age and punishment
        ///     action, with an optional punishment duration or role-based punishment.
        /// </summary>
        /// <param name="mode">Whether to show the settings, enable the protection, or disable it.</param>
        /// <param name="minAge">The minimum account age for accounts to not be considered alts. Required to enable.</param>
        /// <param name="action">The punishment action to be taken against detected alts. Required to enable.</param>
        /// <param name="punishTime">Optional: The duration of the punishment, if applicable.</param>
        /// <param name="role">Optional: The role to be assigned to detected alts as punishment.</param>
        [SlashCommand("set", "Shows, enables or disables Anti-Alt protection")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AntiAlt(
            [Summary("mode", "Show the settings, enable, or disable")]
            ProtectionAction mode,
            [Summary("min-age", "Minimum account age, for example 7d or 12h, required to enable")]
            TimeSpan? minAge = null,
            [Summary("action", "The punishment to apply, required to enable")]
            PunishmentAction? action = null,
            [Summary("punish-time", "Punishment duration, for example 1h30m")]
            TimeSpan? punishTime = null,
            [Summary("role", "The role to add when the action is AddRole")]
            IRole? role = null)
        {
            switch (mode)
            {
                case ProtectionAction.Status:
                {
                    var (_, _, alt, _, _, _, _) = Service.GetAntiStats(ctx.Guild.Id);
                    if (alt is null)
                    {
                        await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Alt"))
                            .ConfigureAwait(false);
                        return;
                    }

                    await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                        .WithTitle("Anti-Alt")
                        .WithDescription(GetAntiAltString(Strings, ctx.Guild.Id, alt))
                        .Build()).ConfigureAwait(false);
                    return;
                }
                case ProtectionAction.Disable:
                {
                    if (await Service.TryStopAntiAlt(ctx.Guild.Id).ConfigureAwait(false))
                    {
                        await ReplyConfirmAsync(Strings.ProtDisable(ctx.Guild.Id, "Anti-Alt")).ConfigureAwait(false);
                        return;
                    }

                    await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Alt"))
                        .ConfigureAwait(false);
                    return;
                }
            }

            if (minAge is null || action is null)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var minAgeMinutes = (int)minAge.Value.TotalMinutes;

            if (minAgeMinutes < 1)
                return;

            if (role is not null)
            {
                await Service.StartAntiAltAsync(ctx.Guild.Id, minAgeMinutes, action.Value, roleId: role.Id)
                    .ConfigureAwait(false);
                await ConfirmAsync(Strings.ProtEnable(ctx.Guild.Id, "Anti-Alt")).ConfigureAwait(false);
                return;
            }

            var punishTimeMinutes = (int?)punishTime?.TotalMinutes ?? 0;

            if (punishTimeMinutes < 0)
                return;

            switch (action.Value)
            {
                case PunishmentAction.Timeout when punishTime?.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime is null || punishTime == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            await Service.StartAntiAltAsync(ctx.Guild.Id, minAgeMinutes, action.Value, punishTimeMinutes)
                .ConfigureAwait(false);

            await ConfirmAsync(Strings.ProtEnable(ctx.Guild.Id, "Anti-Alt")).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Anti-Raid protection, which punishes users when too many join within a short time window.
    /// </summary>
    [Group("raid", "Punish users when too many join in a short time")]
    public class ProtectionRaid : MewdekoSlashSubmodule<ProtectionService>
    {
        /// <summary>
        ///     Shows, enables or disables the Anti-Raid protection. Enabling sets the user threshold, detection time
        ///     window, punishment action, and optional punishment duration.
        /// </summary>
        /// <param name="mode">Whether to show the settings, enable the protection, or disable it.</param>
        /// <param name="userThreshold">The threshold of users that triggers the detection of a raid. Required to enable.</param>
        /// <param name="seconds">The time window (in seconds) to observe user joins. Required to enable.</param>
        /// <param name="action">The punishment action to be taken against detected raids. Required to enable.</param>
        /// <param name="punishTime">The duration of punishment for the raiders (optional).</param>
        [SlashCommand("set", "Shows, enables or disables Anti-Raid protection")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AntiRaid(
            [Summary("mode", "Show the settings, enable, or disable")]
            ProtectionAction mode,
            [Summary("user-threshold", "How many users joining triggers the raid detection (2 to 30)")]
            int? userThreshold = null,
            [Summary("seconds", "The time window in seconds (2 to 300)")]
            int? seconds = null,
            [Summary("action", "The punishment to apply, required to enable")]
            PunishmentAction? action = null,
            [Summary("punish-time", "Punishment duration, for example 1h30m")]
            TimeSpan? punishTime = null)
        {
            switch (mode)
            {
                case ProtectionAction.Status:
                {
                    var (_, raid, _, _, _, _, _) = Service.GetAntiStats(ctx.Guild.Id);
                    if (raid is null)
                    {
                        await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Raid"))
                            .ConfigureAwait(false);
                        return;
                    }

                    await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                        .WithTitle("Anti-Raid")
                        .WithDescription(GetAntiRaidString(Strings, ctx.Guild.Id, raid))
                        .Build()).ConfigureAwait(false);
                    return;
                }
                case ProtectionAction.Disable:
                    if (await Service.TryStopAntiRaid(ctx.Guild.Id))
                        await ReplyConfirmAsync(Strings.ProtDisable(ctx.Guild.Id, "Anti-Raid"));
                    else
                        await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Raid"));
                    return;
            }

            if (userThreshold is null || seconds is null || action is null)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            switch (action.Value)
            {
                case PunishmentAction.Timeout when punishTime?.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime is null || punishTime == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            if (action == PunishmentAction.AddRole)
            {
                await ReplyErrorAsync(Strings.PunishmentUnsupported(ctx.Guild.Id, action.Value))
                    .ConfigureAwait(false);
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
                if (!ProtectionService.IsDurationAllowed(action.Value))
                {
                    await ReplyErrorAsync(Strings.ProtCantUseTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }
            }

            var time = (int?)punishTime?.TotalMinutes ?? 0;
            if (time is < 0 or > 60 * 24)
                return;

            var stats = await Service.StartAntiRaidAsync(ctx.Guild.Id, userThreshold.Value, seconds.Value,
                action.Value, time).ConfigureAwait(false);

            if (stats == null) return;

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.ProtEnable(ctx.Guild.Id, "Anti-Raid"))
                .WithDescription($"{ctx.User.Mention} {GetAntiRaidString(Strings, ctx.Guild.Id, stats)}")
                .Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Anti-Spam protection, which punishes users who send the same message repeatedly.
    /// </summary>
    [Group("spam", "Punish users who send the same message repeatedly")]
    public class ProtectionSpam : MewdekoSlashSubmodule<ProtectionService>
    {
        /// <summary>
        ///     Shows, enables or disables the Anti-Spam protection. Enabling sets the message count threshold, punishment
        ///     action, and an optional punishment duration or role to add to spammers.
        /// </summary>
        /// <param name="mode">Whether to show the settings, enable the protection, or disable it.</param>
        /// <param name="messageCount">The threshold of messages that triggers the detection of spam. Required to enable.</param>
        /// <param name="action">The punishment action to be taken against detected spammers. Required to enable.</param>
        /// <param name="punishTime">The duration of punishment for the spammers (optional).</param>
        /// <param name="role">The role to add to the spammers when the action is AddRole (optional).</param>
        [SlashCommand("set", "Shows, enables or disables Anti-Spam protection")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AntiSpam(
            [Summary("mode", "Show the settings, enable, or disable")]
            ProtectionAction mode,
            [Summary("message-count", "How many repeated messages count as spam (2 to 10)")]
            int? messageCount = null,
            [Summary("action", "The punishment to apply, required to enable")]
            PunishmentAction? action = null,
            [Summary("punish-time", "Punishment duration, for example 1h30m")]
            TimeSpan? punishTime = null,
            [Summary("role", "The role to add when the action is AddRole")]
            IRole? role = null)
        {
            switch (mode)
            {
                case ProtectionAction.Status:
                {
                    var (spam, _, _, _, _, _, _) = Service.GetAntiStats(ctx.Guild.Id);
                    if (spam is null)
                    {
                        await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Spam"))
                            .ConfigureAwait(false);
                        return;
                    }

                    await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                        .WithTitle("Anti-Spam")
                        .WithDescription(GetAntiSpamString(Strings, ctx.Guild.Id, spam))
                        .Build()).ConfigureAwait(false);
                    return;
                }
                case ProtectionAction.Disable:
                    if (await Service.TryStopAntiSpam(ctx.Guild.Id))
                        await ReplyConfirmAsync(Strings.ProtDisable(ctx.Guild.Id, "Anti-Spam"));
                    else
                        await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Spam"));
                    return;
            }

            if (messageCount is null || action is null)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (role is not null && action != PunishmentAction.AddRole)
                return;

            if (role is not null)
                punishTime = null;

            if (messageCount is < 2 or > 10)
                return;

            if (punishTime is not null)
            {
                if (!ProtectionService.IsDurationAllowed(action.Value))
                {
                    await ReplyErrorAsync(Strings.ProtCantUseTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }
            }

            var time = (int?)punishTime?.TotalMinutes ?? 0;
            if (time is < 0 or > 60 * 24)
                return;

            switch (action.Value)
            {
                case PunishmentAction.Timeout when punishTime?.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime is null || punishTime == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            var stats = await Service
                .StartAntiSpamAsync(ctx.Guild.Id, messageCount.Value, action.Value, time, role?.Id)
                .ConfigureAwait(false);

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.ProtEnable(ctx.Guild.Id, "Anti-Spam"))
                .WithDescription($"{ctx.User.Mention} {GetAntiSpamString(Strings, ctx.Guild.Id, stats)}")
                .Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles whether a text channel is ignored by the Anti-Spam protection.
        /// </summary>
        /// <param name="channel">The channel to toggle. Defaults to the current channel.</param>
        [SlashCommand("ignore", "Toggles whether a channel is ignored by Anti-Spam")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AntiSpamIgnore(
            [Summary("channel", "The channel to toggle, defaults to the current one")]
            ITextChannel? channel = null)
        {
            var channelId = channel?.Id ?? ctx.Channel.Id;
            var added = await Service.AntiSpamIgnoreAsync(ctx.Guild.Id, channelId).ConfigureAwait(false);

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
    }

    /// <summary>
    ///     Anti-Mass-Mention protection, which punishes users who mention too many people at once or over time.
    /// </summary>
    [Group("mass-mention", "Punish users who mention too many people")]
    public class ProtectionMassMention : MewdekoSlashSubmodule<ProtectionService>
    {
        /// <summary>
        ///     Shows, enables or disables the Anti-Mass-Mention protection. Enabling sets the mention threshold for a
        ///     single message, the time window for mention tracking, the maximum allowed mentions in the time window, the
        ///     punishment action and an optional punishment duration or role-based punishment.
        /// </summary>
        /// <param name="mode">Whether to show the settings, enable the protection, or disable it.</param>
        /// <param name="mentionThreshold">
        ///     The number of mentions allowed in a single message before triggering protection.
        ///     Required to enable.
        /// </param>
        /// <param name="timeWindowSeconds">The time window (in seconds) to observe mentions. Required to enable.</param>
        /// <param name="maxMentionsInTimeWindow">The maximum allowed mentions in the specified time window. Required to enable.</param>
        /// <param name="action">The punishment action to be taken against users who exceed the mention limits. Required to enable.</param>
        /// <param name="ignoreBots">Whether to ignore bot accounts when tracking mentions.</param>
        /// <param name="punishTime">Optional: The duration of the punishment (if applicable).</param>
        /// <param name="role">Optional: The role to be assigned to punished users as punishment.</param>
        [SlashCommand("set", "Shows, enables or disables Anti-Mass-Mention protection")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AntiMassMention(
            [Summary("mode", "Show the settings, enable, or disable")]
            ProtectionAction mode,
            [Summary("mention-threshold", "Mentions allowed in a single message")]
            int? mentionThreshold = null,
            [Summary("time-window", "The time window in seconds")]
            int? timeWindowSeconds = null,
            [Summary("max-mentions", "Mentions allowed within the time window")]
            int? maxMentionsInTimeWindow = null,
            [Summary("action", "The punishment to apply, required to enable")]
            PunishmentAction? action = null,
            [Summary("ignore-bots", "Whether bots are ignored")]
            bool ignoreBots = false,
            [Summary("punish-time", "Punishment duration, for example 1h30m")]
            TimeSpan? punishTime = null,
            [Summary("role", "The role to add when the action is AddRole")]
            IRole? role = null)
        {
            switch (mode)
            {
                case ProtectionAction.Status:
                {
                    var (_, _, _, massMention, _, _, _) = Service.GetAntiStats(ctx.Guild.Id);
                    if (massMention is null)
                    {
                        await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Mass-Mention"))
                            .ConfigureAwait(false);
                        return;
                    }

                    await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                        .WithTitle("Anti-Mass-Mention")
                        .WithDescription(GetAntiMassMentionString(Strings, ctx.Guild.Id, massMention))
                        .Build()).ConfigureAwait(false);
                    return;
                }
                case ProtectionAction.Disable:
                {
                    if (await Service.TryStopAntiMassMention(ctx.Guild.Id).ConfigureAwait(false))
                    {
                        await ReplyConfirmAsync(Strings.ProtDisable(ctx.Guild.Id, "Anti-Mass-Mention"))
                            .ConfigureAwait(false);
                        return;
                    }

                    await ReplyErrorAsync(Strings.ProtectionNotRunning(ctx.Guild.Id, "Anti-Mass-Mention"))
                        .ConfigureAwait(false);
                    return;
                }
            }

            if (mentionThreshold is null || timeWindowSeconds is null || maxMentionsInTimeWindow is null ||
                action is null)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (mentionThreshold < 1 || timeWindowSeconds < 1 || maxMentionsInTimeWindow < 1)
                return;

            if (role is not null)
            {
                await Service.StartAntiMassMentionAsync(ctx.Guild.Id, mentionThreshold.Value, timeWindowSeconds.Value,
                    maxMentionsInTimeWindow.Value, ignoreBots, action.Value, 0, role.Id).ConfigureAwait(false);

                await ConfirmAsync(Strings.ProtEnable(ctx.Guild.Id, "Anti-Mass-Mention")).ConfigureAwait(false);
                return;
            }

            var punishTimeMinutes = (int?)punishTime?.TotalMinutes ?? 0;

            if (punishTimeMinutes < 0)
                return;

            switch (action.Value)
            {
                case PunishmentAction.Timeout when punishTime?.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime is null || punishTime == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            await Service.StartAntiMassMentionAsync(ctx.Guild.Id, mentionThreshold.Value, timeWindowSeconds.Value,
                maxMentionsInTimeWindow.Value, ignoreBots, action.Value, punishTimeMinutes, null).ConfigureAwait(false);

            await ConfirmAsync(Strings.ProtEnable(ctx.Guild.Id, "Anti-Mass-Mention")).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Anti-Pattern protection, which punishes joining users whose names match configured regex patterns.
    /// </summary>
    [Group("pattern", "Punish joining users whose names match patterns")]
    public class ProtectionPattern : MewdekoSlashSubmodule<ProtectionService>
    {
        /// <summary>
        ///     Shows, enables or disables the Anti-Pattern protection. Enabling sets the punishment action and an optional
        ///     punishment duration or role-based punishment.
        /// </summary>
        /// <param name="mode">Whether to show the settings, enable the protection, or disable it.</param>
        /// <param name="action">The punishment action to be taken against detected pattern matches. Required to enable.</param>
        /// <param name="punishTime">Optional: The duration of the punishment, if applicable.</param>
        /// <param name="role">Optional: The role to be assigned to users who match patterns.</param>
        [SlashCommand("set", "Shows, enables or disables Anti-Pattern protection")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AntiPattern(
            [Summary("mode", "Show the settings, enable, or disable")]
            ProtectionAction mode,
            [Summary("action", "The punishment to apply, required to enable")]
            PunishmentAction? action = null,
            [Summary("punish-time", "Punishment duration, for example 1h30m")]
            TimeSpan? punishTime = null,
            [Summary("role", "The role to add when the action is AddRole")]
            IRole? role = null)
        {
            switch (mode)
            {
                case ProtectionAction.Status:
                {
                    var (_, _, _, _, pattern, _, _) = Service.GetAntiStats(ctx.Guild.Id);
                    if (pattern is null)
                    {
                        await ReplyErrorAsync(Strings.AntiPatternNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                        .WithTitle("Anti-Pattern")
                        .WithDescription(GetAntiPatternString(Strings, ctx.Guild.Id, pattern))
                        .Build()).ConfigureAwait(false);
                    return;
                }
                case ProtectionAction.Disable:
                {
                    if (await Service.TryStopAntiPattern(ctx.Guild.Id).ConfigureAwait(false))
                    {
                        await ReplyConfirmAsync(Strings.AntiPatternDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    await ReplyErrorAsync(Strings.AntiPatternNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }
            }

            if (action is null)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (role is not null)
            {
                var roleStats = await Service.StartAntiPatternAsync(ctx.Guild.Id, action.Value, roleId: role.Id)
                    .ConfigureAwait(false);

                if (roleStats == null)
                {
                    await ReplyErrorAsync(Strings.AntiPatternFailedStart(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await ReplyConfirmAsync(Strings.AntiPatternEnabledRole(ctx.Guild.Id, action.Value.ToString(),
                    role.Mention)).ConfigureAwait(false);
                return;
            }

            var punishTimeMinutes = (int?)punishTime?.TotalMinutes ?? 0;

            if (punishTimeMinutes < 0)
                return;

            switch (action.Value)
            {
                case PunishmentAction.Timeout when punishTime?.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime is null || punishTime == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            var stats = await Service.StartAntiPatternAsync(ctx.Guild.Id, action.Value, punishTimeMinutes)
                .ConfigureAwait(false);

            if (stats == null)
            {
                await ReplyErrorAsync(Strings.AntiPatternFailedStart(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var durationText = punishTimeMinutes > 0
                ? $" for **{TimeSpan.FromMinutes(punishTimeMinutes).Humanize()}**"
                : "";
            await ReplyConfirmAsync(Strings.AntiPatternEnabled(ctx.Guild.Id, action.Value.ToString(), durationText))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a regex pattern to the Anti-Pattern protection.
        /// </summary>
        /// <param name="pattern">The regex pattern to match against usernames and display names.</param>
        /// <param name="name">Optional name for the pattern.</param>
        /// <param name="checkUsername">Whether to check usernames against this pattern (default: true).</param>
        /// <param name="checkDisplayName">Whether to check display names against this pattern (default: true).</param>
        [SlashCommand("add", "Adds a regex pattern to Anti-Pattern")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task PatternAdd(
            [Summary("pattern", "The regex pattern")]
            string pattern,
            [Summary("name", "An optional label for the pattern")]
            string? name = null,
            [Summary("check-username", "Whether usernames are checked")]
            bool checkUsername = true,
            [Summary("check-display-name", "Whether display names are checked")]
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
        [SlashCommand("remove", "Removes a pattern from Anti-Pattern by its id")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task PatternRemove([Summary("pattern-id", "The id shown in the pattern list")] int patternId)
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
        [SlashCommand("list", "Lists the configured Anti-Pattern patterns")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
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

            foreach (var pattern in patterns.Take(10))
            {
                var fieldName = $"ID: {pattern.Id} - {pattern.Name ?? "Unnamed"}";
                var fieldValue = $"**Pattern:** `{pattern.Pattern}`\n" +
                                 $"**Username:** {(pattern.CheckUsername ? Strings.Yes(ctx.Guild.Id) : Strings.No(ctx.Guild.Id))}\n" +
                                 $"**Display Name:** {(pattern.CheckDisplayName ? Strings.Yes(ctx.Guild.Id) : Strings.No(ctx.Guild.Id))}";
                embed.AddField(fieldName, fieldValue, true);
            }

            if (patterns.Count > 10)
            {
                embed.WithFooter(Strings.PatternListFooter(ctx.Guild.Id, patterns.Count));
            }

            await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows or changes the advanced Anti-Pattern settings. When no setting is given the current configuration is
        ///     shown.
        /// </summary>
        /// <param name="setting">The setting to configure.</param>
        /// <param name="value">The value to set. Booleans take true or false, the others take a number.</param>
        [SlashCommand("config", "Shows or changes the advanced Anti-Pattern settings")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task PatternConfig(
            [Summary("setting", "The setting to change, omit to show the configuration")]
            PatternConfigSetting? setting = null,
            [Summary("value", "The new value, true/false or a number")]
            string? value = null)
        {
            var (_, _, _, _, patternStats, _, _) = Service.GetAntiStats(ctx.Guild.Id);

            if (patternStats == null)
            {
                await ReplyErrorAsync(Strings.AntiPatternNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (setting is null || value is null)
            {
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

                await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
                return;
            }

            var success = false;
            var settingName = setting.Value.ToString();

            switch (setting.Value)
            {
                case PatternConfigSetting.AccountAge:
                    if (bool.TryParse(value, out var checkAccountAge))
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            checkAccountAge);
                    }

                    break;
                case PatternConfigSetting.MaxAccountAge:
                    if (int.TryParse(value, out var maxAccountAgeMonths) && maxAccountAgeMonths > 0)
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            maxAccountAgeMonths: maxAccountAgeMonths);
                    }

                    break;
                case PatternConfigSetting.JoinTiming:
                    if (bool.TryParse(value, out var checkJoinTiming))
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            checkJoinTiming: checkJoinTiming);
                    }

                    break;
                case PatternConfigSetting.MaxJoinHours:
                    if (double.TryParse(value, out var maxJoinHours) && maxJoinHours > 0)
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id, maxJoinHours: maxJoinHours);
                    }

                    break;
                case PatternConfigSetting.BatchCreation:
                    if (bool.TryParse(value, out var checkBatchCreation))
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            checkBatchCreation: checkBatchCreation);
                    }

                    break;
                case PatternConfigSetting.OfflineStatus:
                    if (bool.TryParse(value, out var checkOfflineStatus))
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            checkOfflineStatus: checkOfflineStatus);
                    }

                    break;
                case PatternConfigSetting.NewAccounts:
                    if (bool.TryParse(value, out var checkNewAccounts))
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            checkNewAccounts: checkNewAccounts);
                    }

                    break;
                case PatternConfigSetting.NewAccountDays:
                    if (int.TryParse(value, out var newAccountDays) && newAccountDays > 0)
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id,
                            newAccountDays: newAccountDays);
                    }

                    break;
                case PatternConfigSetting.MinimumScore:
                    if (int.TryParse(value, out var minimumScore) && minimumScore > 0)
                    {
                        success = await Service.UpdateAntiPatternConfigAsync(ctx.Guild.Id, minimumScore: minimumScore);
                    }

                    break;
                default:
                    await ReplyErrorAsync(Strings.PatternConfigUnknownSetting(ctx.Guild.Id, settingName))
                        .ConfigureAwait(false);
                    return;
            }

            if (success)
            {
                await ReplyConfirmAsync(Strings.PatternConfigUpdated(ctx.Guild.Id, settingName, value))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.PatternConfigUpdateFailed(ctx.Guild.Id, settingName))
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Anti-Mass-Post protection, which punishes users posting the same content across many channels, plus the
    ///     honeypot channel protection that punishes anyone posting in designated channels.
    /// </summary>
    [Group("mass-post", "Punish cross-channel spam and posting in honeypot channels")]
    public class ProtectionMassPost : MewdekoSlashSubmodule<ProtectionService>
    {
        /// <summary>
        ///     Shows, enables or disables the Anti-Mass-Post protection. Showing the status also includes the
        ///     Anti-Post-Channel settings.
        /// </summary>
        /// <param name="mode">Whether to show the settings, enable the protection, or disable it.</param>
        /// <param name="channelThreshold">
        ///     How many channels the same content must appear in to trigger (2 to 20). Required to
        ///     enable.
        /// </param>
        /// <param name="seconds">The time window in seconds (10 to 600). Required to enable.</param>
        /// <param name="action">The punishment action to apply. Required to enable.</param>
        /// <param name="punishTime">Optional: The duration of the punishment, if applicable.</param>
        [SlashCommand("set", "Shows, enables or disables Anti-Mass-Post protection")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AntiMassPost(
            [Summary("mode", "Show the settings, enable, or disable")]
            ProtectionAction mode,
            [Summary("channel-threshold", "How many channels the same content must hit (2 to 20)")]
            int? channelThreshold = null,
            [Summary("seconds", "The time window in seconds (10 to 600)")]
            int? seconds = null,
            [Summary("action", "The punishment to apply, required to enable")]
            PunishmentAction? action = null,
            [Summary("punish-time", "Punishment duration, for example 1h30m")]
            TimeSpan? punishTime = null)
        {
            switch (mode)
            {
                case ProtectionAction.Status:
                {
                    var (_, _, _, _, _, massPost, postChannel) = Service.GetAntiStats(ctx.Guild.Id);
                    if (massPost is null && postChannel is null)
                    {
                        await ReplyErrorAsync(Strings.AntiMassPostNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    var embed = new EmbedBuilder().WithOkColor().WithTitle("Anti-Mass-Post");

                    if (massPost != null)
                    {
                        embed.AddField("Anti-Mass-Post",
                            GetAntiMassPostString(Strings, ctx.Guild.Id, massPost).TrimTo(1024));
                    }

                    if (postChannel != null)
                    {
                        embed.AddField("Anti-Post-Channel",
                            GetAntiPostChannelString(Strings, ctx.Guild.Id, postChannel).TrimTo(1024));
                    }

                    await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
                    return;
                }
                case ProtectionAction.Disable:
                {
                    if (await Service.TryStopAntiMassPost(ctx.Guild.Id).ConfigureAwait(false))
                    {
                        await ReplyConfirmAsync(Strings.AntiMassPostDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    await ReplyErrorAsync(Strings.AntiMassPostNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }
            }

            if (channelThreshold is null || seconds is null || action is null)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

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

            var punishDuration = (int?)punishTime?.TotalMinutes ?? 0;

            switch (action.Value)
            {
                case PunishmentAction.Timeout when punishTime?.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime is null || punishTime == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            var result = await Service.StartAntiMassPostAsync(
                ctx.Guild.Id,
                channelThreshold.Value,
                seconds.Value,
                0.8,
                20,
                true,
                true,
                false,
                false,
                true,
                true,
                action.Value,
                punishDuration,
                null,
                true,
                50
            ).ConfigureAwait(false);

            if (result != null)
            {
                var durationText = punishDuration > 0 ? $" for {TimeSpan.FromMinutes(punishDuration).Humanize()}" : "";
                await ReplyConfirmAsync(
                    Strings.AntiMassPostEnabled(ctx.Guild.Id, channelThreshold.Value, seconds.Value,
                        action.Value.ToString(), durationText)
                ).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.AntiMassPostFailedStart(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Shows, enables or disables the Anti-Post-Channel (honeypot) protection for the guild. When enabling, the
        ///     current channel receives the status embed.
        /// </summary>
        /// <param name="mode">Whether to show the settings, enable the protection, or disable it.</param>
        /// <param name="action">The punishment action to apply to anyone posting in a honeypot channel. Required to enable.</param>
        /// <param name="punishTime">Optional: The duration of the punishment, if applicable.</param>
        [SlashCommand("channel", "Shows, enables or disables the honeypot channel protection")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AntiPostChannel(
            [Summary("mode", "Show the settings, enable, or disable")]
            ProtectionAction mode,
            [Summary("action", "The punishment to apply, required to enable")]
            PunishmentAction? action = null,
            [Summary("punish-time", "Punishment duration, for example 1h30m")]
            TimeSpan? punishTime = null)
        {
            switch (mode)
            {
                case ProtectionAction.Status:
                {
                    var (_, _, _, _, _, _, postChannel) = Service.GetAntiStats(ctx.Guild.Id);
                    if (postChannel is null)
                    {
                        await ReplyErrorAsync(Strings.AntiPostChannelNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                        .WithTitle("Anti-Post-Channel")
                        .WithDescription(GetAntiPostChannelString(Strings, ctx.Guild.Id, postChannel))
                        .Build()).ConfigureAwait(false);
                    return;
                }
                case ProtectionAction.Disable:
                {
                    if (await Service.TryStopAntiPostChannel(ctx.Guild.Id).ConfigureAwait(false))
                    {
                        await ReplyConfirmAsync(Strings.AntiPostChannelDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    await ReplyErrorAsync(Strings.AntiPostChannelNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }
            }

            if (action is null)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var punishDuration = (int?)punishTime?.TotalMinutes ?? 0;

            switch (action.Value)
            {
                case PunishmentAction.Timeout when punishTime?.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime is null || punishTime == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            var result = await Service.StartAntiPostChannelAsync(
                ctx.Guild.Id,
                action.Value,
                punishDuration,
                null,
                true,
                true,
                true,
                ctx.Channel.Id
            ).ConfigureAwait(false);

            if (result != null)
            {
                var durationText = punishDuration > 0 ? $" for {TimeSpan.FromMinutes(punishDuration).Humanize()}" : "";
                await ReplyConfirmAsync(
                    Strings.AntiPostChannelEnabled(ctx.Guild.Id, action.Value.ToString(), durationText)
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
        /// <param name="channel">The channel to add.</param>
        [SlashCommand("channel-add", "Adds a honeypot channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AntiPostChannelAdd([Summary("channel", "The honeypot channel")] ITextChannel channel)
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
        /// <param name="channel">The channel to remove.</param>
        [SlashCommand("channel-remove", "Removes a honeypot channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AntiPostChannelRemove([Summary("channel", "The honeypot channel")] ITextChannel channel)
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
    }

    /// <summary>
    ///     Anti-Image-Hash protection, which punishes users who post images matching a perceptual hash blocklist.
    /// </summary>
    /// <param name="interactivity">The interactivity service used for paginated embeds.</param>
    /// <param name="imageHashing">The service computing perceptual image hashes.</param>
    [Group("image-hash", "Block images by perceptual hash")]
    public class ProtectionImageHash(InteractiveService interactivity, ImageHashingService imageHashing)
        : MewdekoSlashSubmodule<ProtectionService>
    {
        /// <summary>
        ///     Shows, enables or disables the Anti-Image-Hash protection. Enabling chooses the action taken when someone
        ///     posts an image that matches the blocklist. Disabling keeps the blocked image list.
        /// </summary>
        /// <param name="mode">Whether to show the settings, enable the protection, or disable it.</param>
        /// <param name="action">
        ///     The action taken against the poster. Individual blocked images may override it. Required to
        ///     enable.
        /// </param>
        /// <param name="tolerance">
        ///     How many of the 256 PDQ hash bits may differ for an image to still count as a match. The default of 31 is
        ///     PDQ's standard "same image" threshold; values above about 48 start producing false positives.
        /// </param>
        /// <param name="punishTime">The punishment duration, for actions that support one.</param>
        [SlashCommand("set", "Shows, enables or disables Anti-Image-Hash protection")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AntiImageHash(
            [Summary("mode", "Show the settings, enable, or disable")]
            ProtectionAction mode,
            [Summary("action", "The punishment to apply, required to enable")]
            PunishmentAction? action = null,
            [Summary("tolerance", "How many hash bits may differ, default 31")]
            int tolerance = 31,
            [Summary("punish-time", "Punishment duration, for example 1h30m")]
            TimeSpan? punishTime = null)
        {
            switch (mode)
            {
                case ProtectionAction.Status:
                {
                    var stats = Service.GetAntiImageHashStats(ctx.Guild.Id);
                    if (stats is null)
                    {
                        await ReplyErrorAsync(Strings.AntiImageHashNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                        .WithTitle("Anti-Image-Hash")
                        .WithDescription(GetAntiImageHashString(Strings, ctx.Guild.Id, stats))
                        .Build()).ConfigureAwait(false);
                    return;
                }
                case ProtectionAction.Disable:
                {
                    if (await Service.TryStopAntiImageHash(ctx.Guild.Id).ConfigureAwait(false))
                    {
                        await ReplyConfirmAsync(Strings.AntiImageHashDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    await ReplyErrorAsync(Strings.AntiImageHashNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }
            }

            if (action is null)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var punishDuration = (int?)punishTime?.TotalMinutes ?? 0;

            switch (action.Value)
            {
                case PunishmentAction.Timeout when punishTime?.Days > 28:
                    await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when punishTime is null || punishTime == TimeSpan.Zero:
                    await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
            }

            var result = await Service.StartAntiImageHashAsync(
                ctx.Guild.Id,
                action.Value,
                punishDuration,
                null,
                tolerance,
                true,
                true,
                true,
                true,
                true,
                true,
                8
            ).ConfigureAwait(false);

            if (result is null)
            {
                await ReplyErrorAsync(Strings.AntiImageHashFailedStart(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var durationText = punishDuration > 0 ? $" for {TimeSpan.FromMinutes(punishDuration).Humanize()}" : "";
            await ReplyConfirmAsync(Strings.AntiImageHashEnabled(ctx.Guild.Id, action.Value.ToString(), durationText,
                result.AntiImageHashSettings.HashThreshold)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles a role or a channel as exempt from Anti-Image-Hash protection. Exactly one of the two must be given.
        /// </summary>
        /// <param name="role">The role to toggle.</param>
        /// <param name="channel">The channel to toggle.</param>
        [SlashCommand("ignore", "Toggles a role or channel as exempt from Anti-Image-Hash")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AntiImageHashIgnore(
            [Summary("role", "The role to toggle")]
            IRole? role = null,
            [Summary("channel", "The channel to toggle")]
            IGuildChannel? channel = null)
        {
            if (role is null == channel is null)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (role is not null)
            {
                var roleAdded = await Service.ToggleAntiImageHashIgnoredRoleAsync(ctx.Guild.Id, role.Id)
                    .ConfigureAwait(false);

                await ReplyConfirmAsync(roleAdded
                        ? Strings.ImageHashIgnoredRoleAdded(ctx.Guild.Id, role.Mention)
                        : Strings.ImageHashIgnoredRoleRemoved(ctx.Guild.Id, role.Mention))
                    .ConfigureAwait(false);
                return;
            }

            var channelAdded = await Service.ToggleAntiImageHashIgnoredChannelAsync(ctx.Guild.Id, channel!.Id)
                .ConfigureAwait(false);

            await ReplyConfirmAsync(channelAdded
                    ? Strings.ImageHashIgnoredChannelAdded(ctx.Guild.Id, MentionUtils.MentionChannel(channel.Id))
                    : Strings.ImageHashIgnoredChannelRemoved(ctx.Guild.Id, MentionUtils.MentionChannel(channel.Id)))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Blocks an image, using the guild default action or an action that overrides it for this image only. The
        ///     image is taken from the attachment or from the URL.
        /// </summary>
        /// <param name="attachment">The image to block.</param>
        /// <param name="url">The URL of the image to block, used when no attachment is given.</param>
        /// <param name="name">An optional label for the blocked image.</param>
        /// <param name="action">An optional action taken against anyone posting this specific image.</param>
        [SlashCommand("block", "Blocks an image by attachment or url")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task BlockImage(
            [Summary("attachment", "The image to block")]
            IAttachment? attachment = null,
            [Summary("url", "The image url, used when no attachment is given")]
            string? url = null,
            [Summary("name", "An optional label for the image")]
            string? name = null,
            [Summary("action", "An action that overrides the guild default for this image")]
            PunishmentAction? action = null)
        {
            if (Service.GetAntiImageHashStats(ctx.Guild.Id) is null)
            {
                await ReplyErrorAsync(Strings.AntiImageHashNotEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var resolved = ResolveImageUrl(attachment, url);
            if (resolved is null)
            {
                await ReplyErrorAsync(Strings.ImageHashNoImage(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await DeferAsync().ConfigureAwait(false);

            var hashSet = await imageHashing.ComputeHashSetFromUrlAsync(resolved).ConfigureAwait(false);
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
                .AddBannedImageHashAsync(ctx.Guild.Id, hashSet, name, resolved, ctx.User.Id, action)
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
        [SlashCommand("unblock", "Removes an image from the blocklist by its id")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task UnblockImage([Summary("hash-id", "The id shown in the blocked list")] int hashId)
        {
            if (await Service.RemoveBannedImageHashAsync(ctx.Guild.Id, hashId).ConfigureAwait(false))
                await ReplyConfirmAsync(Strings.ImageHashRemoved(ctx.Guild.Id, hashId)).ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.ImageHashRemoveFailed(ctx.Guild.Id, hashId)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists the blocked images for the guild along with how many times each one has been caught.
        /// </summary>
        [SlashCommand("blocked-list", "Lists the blocked images")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
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

            await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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
        ///     Shows the perceptual hash of an image without blocking it, so it can be copied into the dashboard or another
        ///     server.
        /// </summary>
        /// <param name="attachment">The image to hash.</param>
        /// <param name="url">The URL of the image to hash, used when no attachment is given.</param>
        [SlashCommand("hash", "Shows the perceptual hash of an image without blocking it")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task ImageHash(
            [Summary("attachment", "The image to hash")]
            IAttachment? attachment = null,
            [Summary("url", "The image url, used when no attachment is given")]
            string? url = null)
        {
            var resolved = ResolveImageUrl(attachment, url);
            if (resolved is null)
            {
                await ReplyErrorAsync(Strings.ImageHashNoImage(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await DeferAsync().ConfigureAwait(false);

            var hashSet = await imageHashing.ComputeHashSetFromUrlAsync(resolved).ConfigureAwait(false);
            if (hashSet is null)
            {
                await ReplyErrorAsync(Strings.ImageHashUnreadable(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.ImageHashComputed(ctx.Guild.Id, hashSet.Hash)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles the list of known scam images that ships with the bot, so the guild blocks the images every server is
        ///     seeing without having to collect them first.
        /// </summary>
        [SlashCommand("preset", "Toggles the shipped list of known scam images")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
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
        ///     Finds the image to hash: the image attachment if one was given, otherwise an absolute http or https URL.
        /// </summary>
        /// <param name="attachment">The attachment, if any.</param>
        /// <param name="url">The URL, if any.</param>
        /// <returns>The resolved image URL, or null when neither is usable.</returns>
        private static string? ResolveImageUrl(IAttachment? attachment, string? url)
        {
            if (attachment is not null &&
                attachment.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
                return attachment.Url;

            if (string.IsNullOrWhiteSpace(url))
                return null;

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return null;

            return uri.ToString();
        }
    }
}