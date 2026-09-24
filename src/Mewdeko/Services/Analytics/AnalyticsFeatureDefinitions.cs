using System.Globalization;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Modules.Xp.Models;

namespace Mewdeko.Services.Analytics;

/// <summary>
///     What the nightly census counts: for every feature, which guilds have it configured and which have it
///     switched on, and for the most telling settings, how many guilds use each value.
/// </summary>
public static class AnalyticsFeatureDefinitions
{
    /// <summary>
    ///     Feature keys with the queries that yield the guild ids having the feature configured and enabled.
    /// </summary>
    public static readonly IReadOnlyList<FeatureDefinition> Features =
    [
        new("xp",
            db => db.GuildXpSettings.Select(x => x.GuildId),
            db => db.GuildXpSettings.Where(x => !x.XpGainDisabled).Select(x => x.GuildId)),
        new("xp_voice",
            db => db.GuildXpSettings.Where(x => x.VoiceXpPerMinute > 0).Select(x => x.GuildId),
            db => db.GuildXpSettings.Where(x => x.VoiceXpPerMinute > 0 && !x.XpGainDisabled)
                .Select(x => x.GuildId)),
        new("starboard",
            db => db.Starboards.Select(x => x.GuildId),
            db => db.Starboards.Where(x => x.StarboardChannelId != 0).Select(x => x.GuildId)),
        new("automod_spam",
            db => db.AntiSpamSettings.Where(x => x.GuildId != null).Select(x => x.GuildId!.Value),
            db => db.AntiSpamSettings.Where(x => x.GuildId != null).Select(x => x.GuildId!.Value)),
        new("automod_mention",
            db => db.AntiMassMentionSettings.Where(x => x.GuildId != null).Select(x => x.GuildId!.Value),
            db => db.AntiMassMentionSettings.Where(x => x.GuildId != null).Select(x => x.GuildId!.Value)),
        new("automod_post",
            db => db.AntiMassPostSettings.Select(x => x.GuildId),
            db => db.AntiMassPostSettings.Select(x => x.GuildId)),
        new("automod_image",
            db => db.AntiImageHashSettings.Select(x => x.GuildId),
            db => db.AntiImageHashSettings.Select(x => x.GuildId)),
        new("automod_raid",
            db => db.AntiRaidSettings.Where(x => x.GuildId != null).Select(x => x.GuildId!.Value),
            db => db.AntiRaidSettings.Where(x => x.GuildId != null).Select(x => x.GuildId!.Value)),
        new("automod_alt",
            db => db.AntiAltSettings.Where(x => x.GuildId != null).Select(x => x.GuildId!.Value),
            db => db.AntiAltSettings.Where(x => x.GuildId != null).Select(x => x.GuildId!.Value)),
        new("logging",
            db => db.LoggingV2.Select(x => x.GuildId),
            db => db.LoggingV2.Where(x => x.LogOtherId != null || x.MessageDeletedId != null
                                                               || x.MessageUpdatedId != null
                                                               || x.UserJoinedId != null || x.UserLeftId != null
                                                               || x.UserBannedId != null
                                                               || x.UserUpdatedId != null
                                                               || x.ChannelCreatedId != null
                                                               || x.RoleUpdatedId != null)
                .Select(x => x.GuildId)),
        new("suggestion",
            db => db.GuildConfigs.Where(x => x.Sugchan != 0).Select(x => x.GuildId),
            db => db.GuildConfigs.Where(x => x.Sugchan != 0).Select(x => x.GuildId)),
        new("ticket",
            db => db.TicketPanels.Select(x => x.GuildId).Concat(db.GuildTicketSettings.Select(x => x.GuildId)),
            db => db.TicketPanels.Select(x => x.GuildId)),
        new("giveaway",
            db => db.Giveaways.Select(x => x.ServerId),
            db => db.Giveaways.Where(x => x.Ended == 0).Select(x => x.ServerId)),
        new("ai_chat",
            db => db.GuildAiConfigs.Select(x => x.GuildId),
            db => db.GuildAiConfigs.Where(x => x.Enabled && x.ChannelId != 0).Select(x => x.GuildId)),
        new("chat_trigger",
            db => db.ChatTriggers.Where(x => x.GuildId != null).Select(x => x.GuildId!.Value),
            db => db.ChatTriggers.Where(x => x.GuildId != null && !x.IsDisabled).Select(x => x.GuildId!.Value)),
        new("form",
            db => db.Forms.Select(x => x.GuildId),
            db => db.Forms.Where(x => x.IsActive && !x.IsDraft).Select(x => x.GuildId)),
        new("counting",
            db => db.CountingChannels.Select(x => x.GuildId),
            db => db.CountingChannels.Where(x => x.IsActive).Select(x => x.GuildId)),
        new("message_count",
            db => db.GuildConfigs.Where(x => x.UseMessageCount).Select(x => x.GuildId),
            db => db.GuildConfigs.Where(x => x.UseMessageCount).Select(x => x.GuildId)),
        new("invite",
            db => db.InviteCountSettings.Select(x => x.GuildId),
            db => db.InviteCountSettings.Where(x => x.IsEnabled).Select(x => x.GuildId)),
        new("afk",
            db => db.Afks.Select(x => x.GuildId),
            db => db.Afks.Select(x => x.GuildId)),
        new("custom_voice",
            db => db.CustomVoiceConfigs.Select(x => x.GuildId),
            db => db.CustomVoiceConfigs.Where(x => x.HubVoiceChannelId != 0).Select(x => x.GuildId)),
        new("reputation",
            db => db.RepConfigs.Select(x => x.GuildId),
            db => db.RepConfigs.Where(x => x.Enabled).Select(x => x.GuildId)),
        new("repeater",
            db => db.GuildRepeaters.Select(x => x.GuildId),
            db => db.GuildRepeaters.Where(x => x.IsEnabled).Select(x => x.GuildId)),
        new("greet",
            db => db.MultiGreets.Select(x => x.GuildId)
                .Concat(db.GuildConfigs.Where(x => x.SendDmGreetMessage).Select(x => x.GuildId)),
            db => db.MultiGreets.Where(x => !x.Disabled).Select(x => x.GuildId)
                .Concat(db.GuildConfigs.Where(x => x.SendDmGreetMessage).Select(x => x.GuildId))),
        new("bye",
            db => db.GuildConfigs.Where(x => x.ByeMessageChannelId != 0).Select(x => x.GuildId),
            db => db.GuildConfigs.Where(x => x.ByeMessageChannelId != 0 && x.SendChannelByeMessage)
                .Select(x => x.GuildId)),
        new("role_state",
            db => db.RoleStateSettings.Select(x => x.GuildId),
            db => db.RoleStateSettings.Where(x => x.Enabled).Select(x => x.GuildId)),
        new("highlight",
            db => db.Highlights.Select(x => x.GuildId),
            db => db.HighlightSettings.Where(x => x.HighlightsOn).Select(x => x.GuildId)),
        new("poll",
            db => db.Polls.Select(x => x.GuildId),
            db => db.Polls.Where(x => x.IsActive).Select(x => x.GuildId)),
        new("confession",
            db => db.GuildConfigs.Where(x => x.ConfessionChannel != 0).Select(x => x.GuildId),
            db => db.GuildConfigs.Where(x => x.ConfessionChannel != 0).Select(x => x.GuildId)),
        new("birthday",
            db => db.BirthdayConfigs.Select(x => x.GuildId),
            db => db.BirthdayConfigs.Where(x => x.BirthdayChannelId != null && x.EnabledFeatures != 0)
                .Select(x => x.GuildId)),
        new("stat_channel",
            db => db.StatChannels.Select(x => x.GuildId),
            db => db.StatChannels.Select(x => x.GuildId)),
        new("patreon",
            db => db.GuildConfigs.Where(x => x.PatreonCampaignId != null).Select(x => x.GuildId),
            db => db.GuildConfigs.Where(x => x.PatreonCampaignId != null && x.PatreonEnabled)
                .Select(x => x.GuildId)),
        new("word_of_the_day",
            db => db.WordOfTheDayConfigs.Select(x => x.GuildId),
            db => db.WordOfTheDayConfigs.Where(x => x.Enabled && x.ChannelId != null).Select(x => x.GuildId)),
        new("role_menus",
            db => db.RoleMenus.Select(x => x.GuildId),
            db => db.RoleMenus.Where(x => x.Enabled && x.MessageId != null).Select(x => x.GuildId))
    ];

    /// <summary>
    ///     Settings whose value distribution is counted, one row per distinct value or range.
    /// </summary>
    public static readonly IReadOnlyList<SettingDefinition> Settings =
    [
        new("GuildXpSetting", "XpPerMessage", db => db.GuildXpSettings.Select(x => (double)x.XpPerMessage),
            v => Range(v, 1, 3, 5, 10, 25, 50, 100)),
        new("GuildXpSetting", "MessageXpCooldown",
            db => db.GuildXpSettings.Select(x => (double)x.MessageXpCooldown),
            v => Range(v, 1, 30, 60, 120, 300, 600)),
        new("GuildXpSetting", "XpMultiplier", db => db.GuildXpSettings.Select(x => x.XpMultiplier),
            v => Range(v, 0.5, 1, 1.5, 2, 3, 5)),
        new("GuildXpSetting", "VoiceXpPerMinute", db => db.GuildXpSettings.Select(x => (double)x.VoiceXpPerMinute),
            v => Range(v, 1, 3, 5, 10, 25)),
        new("GuildXpSetting", "XpCurveType", db => db.GuildXpSettings.Select(x => (double)x.XpCurveType),
            v => Enum<XpCurveType>(v)),
        new("GuildXpSetting", "EnableXpDecay", db => db.GuildXpSettings.Select(x => x.EnableXpDecay ? 1d : 0d),
            Bool),
        new("GuildXpSetting", "ExclusiveRoleRewards",
            db => db.GuildXpSettings.Select(x => x.ExclusiveRoleRewards ? 1d : 0d), Bool),
        new("Starboard", "Threshold", db => db.Starboards.Select(x => (double)x.Threshold),
            v => Range(v, 1, 2, 3, 5, 10, 25)),
        new("Starboard", "AllowBots", db => db.Starboards.Select(x => x.AllowBots ? 1d : 0d), Bool),
        new("Starboard", "UseBlacklist", db => db.Starboards.Select(x => x.UseBlacklist ? 1d : 0d), Bool),
        new("Starboard", "RemoveOnDelete", db => db.Starboards.Select(x => x.RemoveOnDelete ? 1d : 0d), Bool),
        new("AntiSpamSetting", "Action", db => db.AntiSpamSettings.Select(x => (double)x.Action),
            v => Enum<PunishmentAction>(v)),
        new("AntiSpamSetting", "MessageThreshold",
            db => db.AntiSpamSettings.Select(x => (double)x.MessageThreshold), v => Range(v, 3, 5, 8, 10, 20)),
        new("AntiSpamSetting", "MuteTime", db => db.AntiSpamSettings.Select(x => (double)x.MuteTime),
            v => Range(v, 1, 5, 15, 60, 1440)),
        new("AntiRaidSetting", "Action", db => db.AntiRaidSettings.Select(x => (double)x.Action),
            v => Enum<PunishmentAction>(v)),
        new("AntiRaidSetting", "UserThreshold", db => db.AntiRaidSettings.Select(x => (double)x.UserThreshold),
            v => Range(v, 3, 5, 10, 20, 50)),
        new("AntiRaidSetting", "Seconds", db => db.AntiRaidSettings.Select(x => (double)x.Seconds),
            v => Range(v, 5, 10, 30, 60, 300)),
        new("AntiAltSetting", "Action", db => db.AntiAltSettings.Select(x => (double)x.Action),
            v => Enum<PunishmentAction>(v)),
        new("AntiMassMentionSetting", "Action", db => db.AntiMassMentionSettings.Select(x => (double)x.Action),
            v => Enum<PunishmentAction>(v)),
        new("AntiMassMentionSetting", "MentionThreshold",
            db => db.AntiMassMentionSettings.Select(x => (double)x.MentionThreshold),
            v => Range(v, 3, 5, 10, 20)),
        new("AntiMassPostSetting", "Action", db => db.AntiMassPostSettings.Select(x => (double)x.Action),
            v => Enum<PunishmentAction>(v)),
        new("AntiMassPostSetting", "ChannelThreshold",
            db => db.AntiMassPostSettings.Select(x => (double)x.ChannelThreshold), v => Range(v, 2, 3, 5, 10)),
        new("AntiImageHashSetting", "Action", db => db.AntiImageHashSettings.Select(x => (double)x.Action),
            v => Enum<PunishmentAction>(v)),
        new("AntiImageHashSetting", "UsePresetList",
            db => db.AntiImageHashSettings.Select(x => x.UsePresetList ? 1d : 0d), Bool),
        new("GuildConfig", "SuggestionThreadType",
            db => db.GuildConfigs.Where(x => x.Sugchan != 0).Select(x => (double)x.SuggestionThreadType), Raw),
        new("GuildConfig", "SuggestCommandsType",
            db => db.GuildConfigs.Where(x => x.Sugchan != 0).Select(x => (double)x.SuggestCommandsType), Raw),
        new("GuildConfig", "EmoteMode",
            db => db.GuildConfigs.Where(x => x.Sugchan != 0).Select(x => (double)x.EmoteMode), Raw),
        new("GuildConfig", "ArchiveOnDeny",
            db => db.GuildConfigs.Where(x => x.Sugchan != 0).Select(x => x.ArchiveOnDeny ? 1d : 0d), Bool),
        new("GuildConfig", "ArchiveOnAccept",
            db => db.GuildConfigs.Where(x => x.Sugchan != 0).Select(x => x.ArchiveOnAccept ? 1d : 0d), Bool),
        new("GuildConfig", "AfkType",
            db => db.GuildConfigs.Where(x => x.AfkType != 0).Select(x => (double)x.AfkType), Raw),
        new("GuildConfig", "AfkTimeout",
            db => db.GuildConfigs.Where(x => x.AfkTimeout != 0).Select(x => (double)x.AfkTimeout),
            v => Range(v, 60, 300, 900, 3600)),
        new("GuildConfig", "MultiGreetType", db => db.GuildConfigs.Select(x => (double)x.MultiGreetType), Raw),
        new("GuildConfig", "DeleteMessageOnCommand",
            db => db.GuildConfigs.Select(x => x.DeleteMessageOnCommand ? 1d : 0d), Bool),
        new("GuildConfig", "StatsOptOut", db => db.GuildConfigs.Select(x => x.StatsOptOut ? 1d : 0d), Bool),
        new("GuildConfig", "FilterInvites", db => db.GuildConfigs.Select(x => x.FilterInvites ? 1d : 0d), Bool),
        new("GuildConfig", "FilterLinks", db => db.GuildConfigs.Select(x => x.FilterLinks ? 1d : 0d), Bool),
        new("GuildConfig", "FilterWords", db => db.GuildConfigs.Select(x => x.FilterWords ? 1d : 0d), Bool),
        new("GuildConfig", "WarnExpireHours",
            db => db.GuildConfigs.Where(x => x.WarnExpireHours != 0).Select(x => (double)x.WarnExpireHours),
            v => Range(v, 24, 72, 168, 720)),
        new("GuildConfig", "WarnExpireAction",
            db => db.GuildConfigs.Where(x => x.WarnExpireHours != 0).Select(x => (double)x.WarnExpireAction), Raw),
        new("GuildConfig", "PreviewLinks", db => db.GuildConfigs.Select(x => (double)x.PreviewLinks), Raw),
        new("GuildConfig", "PatreonRoleSync",
            db => db.GuildConfigs.Where(x => x.PatreonCampaignId != null).Select(x => x.PatreonRoleSync ? 1d : 0d),
            Bool),
        new("GuildTicketSetting", "DefaultMaxTickets",
            db => db.GuildTicketSettings.Select(x => (double)x.DefaultMaxTickets), v => Range(v, 1, 2, 3, 5, 10)),
        new("GuildTicketSetting", "EnableStaffPings",
            db => db.GuildTicketSettings.Select(x => x.EnableStaffPings ? 1d : 0d), Bool),
        new("GuildTicketSetting", "EnableDmNotifications",
            db => db.GuildTicketSettings.Select(x => x.EnableDmNotifications ? 1d : 0d), Bool),
        new("GuildTicketSetting", "DeleteTicketsOnClose",
            db => db.GuildTicketSettings.Select(x => x.DeleteTicketsOnClose ? 1d : 0d), Bool),
        new("GuildTicketSetting", "AutoArchiveOnClose",
            db => db.GuildTicketSettings.Select(x => x.AutoArchiveOnClose ? 1d : 0d), Bool),
        new("GuildTicketSetting", "TranscriptChannelSet",
            db => db.GuildTicketSettings.Select(x => x.TranscriptChannelId != null ? 1d : 0d), Bool),
        new("GuildAiConfig", "Provider", db => db.GuildAiConfigs.Select(x => (double)x.Provider),
            v => Enum<AiService.AiProvider>(v)),
        new("GuildAiConfig", "WebSearchEnabled",
            db => db.GuildAiConfigs.Select(x => x.WebSearchEnabled ? 1d : 0d), Bool),
        new("GuildAiConfig", "CustomApiKey",
            db => db.GuildAiConfigs.Select(x => x.ApiKey != null && x.ApiKey != "" ? 1d : 0d), Bool),
        new("Giveaway", "UseButton", db => db.Giveaways.Select(x => x.UseButton ? 1d : 0d), Bool),
        new("Giveaway", "UseCaptcha", db => db.Giveaways.Select(x => x.UseCaptcha ? 1d : 0d), Bool),
        new("Giveaway", "Winners", db => db.Giveaways.Select(x => (double)x.Winners), v => Range(v, 1, 2, 3, 5, 10)),
        new("ChatTrigger", "IsRegex",
            db => db.ChatTriggers.Where(x => x.GuildId != null).Select(x => x.IsRegex ? 1d : 0d), Bool),
        new("ChatTrigger", "ContainsAnywhere",
            db => db.ChatTriggers.Where(x => x.GuildId != null).Select(x => x.ContainsAnywhere ? 1d : 0d), Bool),
        new("ChatTrigger", "ResponseMode",
            db => db.ChatTriggers.Where(x => x.GuildId != null).Select(x => (double)x.ResponseMode), Raw),
        new("ChatTrigger", "PrefixType",
            db => db.ChatTriggers.Where(x => x.GuildId != null).Select(x => (double)x.PrefixType), Raw),
        new("Form", "FormType", db => db.Forms.Select(x => (double)x.FormType), Raw),
        new("Form", "RequireApproval", db => db.Forms.Select(x => x.RequireApproval ? 1d : 0d), Bool),
        new("Form", "RequireCaptcha", db => db.Forms.Select(x => x.RequireCaptcha ? 1d : 0d), Bool),
        new("Form", "AllowAnonymous", db => db.Forms.Select(x => x.AllowAnonymous ? 1d : 0d), Bool),
        new("CountingChannelConfig", "Pattern", db => db.CountingChannelConfigs.Select(x => (double)x.Pattern), Raw),
        new("CountingChannelConfig", "NumberBase",
            db => db.CountingChannelConfigs.Select(x => (double)x.NumberBase), Raw),
        new("CountingChannelConfig", "ResetOnError",
            db => db.CountingChannelConfigs.Select(x => x.ResetOnError ? 1d : 0d), Bool),
        new("CountingChannelConfig", "AllowRepeatedUsers",
            db => db.CountingChannelConfigs.Select(x => x.AllowRepeatedUsers ? 1d : 0d), Bool),
        new("InviteCountSetting", "RemoveInviteOnLeave",
            db => db.InviteCountSettings.Select(x => x.RemoveInviteOnLeave ? 1d : 0d), Bool),
        new("CustomVoiceConfig", "DefaultUserLimit",
            db => db.CustomVoiceConfigs.Select(x => (double)x.DefaultUserLimit), v => Range(v, 1, 2, 5, 10, 25)),
        new("CustomVoiceConfig", "DeleteWhenEmpty",
            db => db.CustomVoiceConfigs.Select(x => x.DeleteWhenEmpty ? 1d : 0d), Bool),
        new("CustomVoiceConfig", "AllowLocking",
            db => db.CustomVoiceConfigs.Select(x => x.AllowLocking ? 1d : 0d), Bool),
        new("RepConfig", "DailyLimit", db => db.RepConfigs.Select(x => (double)x.DailyLimit),
            v => Range(v, 1, 3, 5, 10, 25)),
        new("RepConfig", "DefaultCooldownMinutes",
            db => db.RepConfigs.Select(x => (double)x.DefaultCooldownMinutes), v => Range(v, 1, 30, 60, 360, 1440)),
        new("RepConfig", "EnableNegativeRep", db => db.RepConfigs.Select(x => x.EnableNegativeRep ? 1d : 0d), Bool),
        new("RepConfig", "EnableAnonymous", db => db.RepConfigs.Select(x => x.EnableAnonymous ? 1d : 0d), Bool),
        new("RepConfig", "EnableDecay", db => db.RepConfigs.Select(x => x.EnableDecay ? 1d : 0d), Bool),
        new("GuildRepeater", "TriggerMode", db => db.GuildRepeaters.Select(x => (double)x.TriggerMode), Raw),
        new("GuildRepeater", "NoRedundant", db => db.GuildRepeaters.Select(x => x.NoRedundant ? 1d : 0d), Bool),
        new("GuildRepeater", "ThreadOnlyMode", db => db.GuildRepeaters.Select(x => x.ThreadOnlyMode ? 1d : 0d),
            Bool),
        new("MultiGreet", "GreetBots", db => db.MultiGreets.Select(x => x.GreetBots ? 1d : 0d), Bool),
        new("MultiGreet", "DeleteTime", db => db.MultiGreets.Select(x => (double)x.DeleteTime),
            v => Range(v, 1, 10, 30, 60, 300)),
        new("MultiGreet", "UsesWebhook",
            db => db.MultiGreets.Select(x => x.WebhookUrl != null && x.WebhookUrl != "" ? 1d : 0d), Bool),
        new("RoleStateSetting", "ClearOnBan", db => db.RoleStateSettings.Select(x => x.ClearOnBan ? 1d : 0d), Bool),
        new("RoleStateSetting", "IgnoreBots", db => db.RoleStateSettings.Select(x => x.IgnoreBots ? 1d : 0d), Bool),
        new("Poll", "Type", db => db.Polls.Select(x => (double)x.Type), Raw),
        new("BirthdayConfig", "EnabledFeatures", db => db.BirthdayConfigs.Select(x => (double)x.EnabledFeatures),
            Raw),
        new("BirthdayConfig", "BirthdayReminderDays",
            db => db.BirthdayConfigs.Select(x => (double)x.BirthdayReminderDays), v => Range(v, 1, 3, 7, 14)),
        new("StatChannel", "StatType", db => db.StatChannels.Select(x => (double)x.StatType), Raw),
        new("StatChannel", "UpdateMechanism", db => db.StatChannels.Select(x => (double)x.UpdateMechanism), Raw),
        new("StatChannel", "DisplayStyle", db => db.StatChannels.Select(x => (double)x.DisplayStyle), Raw)
    ];

    /// <summary>
    ///     Labels a value with the range it falls into: <c>lt{first}</c>, <c>{a}-{b}</c> for each pair of edges and
    ///     <c>gte{last}</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="edges">Ascending range edges.</param>
    public static string Range(double value, params double[] edges)
    {
        if (edges.Length == 0) return Raw(value);
        if (value < edges[0]) return "lt" + Num(edges[0]);
        for (var i = 0; i < edges.Length - 1; i++)
        {
            if (value >= edges[i] && value < edges[i + 1])
                return Num(edges[i]) + "-" + Num(edges[i + 1]);
        }

        return "gte" + Num(edges[^1]);
    }

    /// <summary>
    ///     Labels a numeric enum value with its name, falling back to the number.
    /// </summary>
    /// <typeparam name="T">The enum.</typeparam>
    /// <param name="value">The stored value.</param>
    public static string Enum<T>(double value) where T : struct, Enum
    {
        var number = (int)value;
        return System.Enum.IsDefined(typeof(T), number)
            ? System.Enum.GetName(typeof(T), number)!.ToLowerInvariant()
            : number.ToString();
    }

    /// <summary>
    ///     Labels a 0/1 value as <c>false</c> or <c>true</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    public static string Bool(double value)
    {
        return value != 0 ? "true" : "false";
    }

    /// <summary>
    ///     Labels a value with itself.
    /// </summary>
    /// <param name="value">The value.</param>
    public static string Raw(double value)
    {
        return Num(value);
    }

    private static string Num(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     A feature key with the queries selecting the guild ids that have it configured and enabled.
    /// </summary>
    /// <param name="Key">The feature key.</param>
    /// <param name="Configured">Guilds with the feature set up at all.</param>
    /// <param name="Enabled">Guilds with the feature set up and switched on.</param>
    public sealed record FeatureDefinition(
        string Key,
        Func<MewdekoDb, IQueryable<ulong>> Configured,
        Func<MewdekoDb, IQueryable<ulong>> Enabled);

    /// <summary>
    ///     A setting whose values are counted per guild row.
    /// </summary>
    /// <param name="Table">The table the setting lives in.</param>
    /// <param name="Column">The column, or a descriptive name for a derived value.</param>
    /// <param name="Values">The numeric value of the setting for every row.</param>
    /// <param name="Label">Turns a value into the label used in the metric name.</param>
    public sealed record SettingDefinition(
        string Table,
        string Column,
        Func<MewdekoDb, IQueryable<double>> Values,
        Func<double, string> Label);
}