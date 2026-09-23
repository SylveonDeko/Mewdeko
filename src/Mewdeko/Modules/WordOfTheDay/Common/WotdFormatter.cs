using System.Text;
using DataModel;
using Mewdeko.Modules.WordOfTheDay.Services;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.WordOfTheDay.Common;

/// <summary>
///     Shared rendering helpers used by both the text and slash Word of the Day modules.
/// </summary>
public static class WotdFormatter
{
    /// <summary>
    ///     Maps a posting failure key to a localized message.
    /// </summary>
    /// <param name="strings">Localized strings.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <param name="failure">The failure key returned by the service.</param>
    /// <returns>The localized error text.</returns>
    public static string FailureMessage(GeneratedBotStrings strings, ulong guildId, string? failure)
    {
        return failure switch
        {
            "channel" => strings.WotdPostFailedChannel(guildId),
            "send" => strings.WotdPostFailedSend(guildId),
            _ => strings.WotdNoWordFound(guildId)
        };
    }

    /// <summary>
    ///     Renders a page of custom words.
    /// </summary>
    /// <param name="words">The words on the page.</param>
    /// <returns>Markdown lines.</returns>
    public static string FormatWordPage(IEnumerable<WordOfTheDayWord> words)
    {
        var sb = new StringBuilder();
        foreach (var w in words)
        {
            var pos = string.IsNullOrWhiteSpace(w.PartOfSpeech) ? "" : $" *({w.PartOfSpeech})*";
            var def = string.IsNullOrWhiteSpace(w.Definition)
                ? ""
                : $": {WordOfTheDayService.Clamp(w.Definition, 80)}";
            sb.AppendLine($"**{w.Word}**{pos}{def}  `×{w.TimesUsed}`");
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Renders recent history rows.
    /// </summary>
    /// <param name="history">Rows, newest first.</param>
    /// <returns>Markdown lines.</returns>
    public static string FormatHistory(IEnumerable<WordOfTheDayHistory> history)
    {
        var sb = new StringBuilder();
        foreach (var h in history)
        {
            var pos = string.IsNullOrWhiteSpace(h.PartOfSpeech) ? "" : $" *({h.PartOfSpeech})*";
            sb.AppendLine(
                $"`{h.PostedOn:yyyy-MM-dd}` **{h.Word}**{pos}: {WordOfTheDayService.Clamp(h.Definition, 90)}");
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Describes which fields a rule overrides.
    /// </summary>
    /// <param name="strings">Localized strings.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="rule">The rule.</param>
    /// <returns>A short comma separated summary.</returns>
    public static string DescribeRule(GeneratedBotStrings strings, ulong guildId, WordOfTheDaySchedule rule)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(rule.Topic))
            parts.Add($"{strings.WotdConfigTopic(guildId)}: **{rule.Topic}**");
        if (rule.PartOfSpeech.HasValue)
            parts.Add($"{strings.WotdConfigPos(guildId)}: **{(WordPartOfSpeech)rule.PartOfSpeech.Value}**");
        if (rule.Difficulty.HasValue)
            parts.Add($"{strings.WotdConfigDifficulty(guildId)}: **{(WordDifficulty)rule.Difficulty.Value}**");
        return parts.Count == 0 ? strings.WotdRuleInherit(guildId) : string.Join(", ", parts);
    }

    /// <summary>
    ///     Describes the effective filters for a day.
    /// </summary>
    /// <param name="strings">Localized strings.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="filters">The resolved filters.</param>
    /// <returns>A short summary including which rule supplied it.</returns>
    public static string DescribeFilters(GeneratedBotStrings strings, ulong guildId, WordFilters filters)
    {
        var topic = filters.Topic ?? strings.WotdScheduleNone(guildId);
        var source = filters.SourceRule is null
            ? strings.WotdScheduleBase(guildId)
            : strings.WotdScheduleFromRule(guildId, filters.SourceRule);
        return $"{strings.WotdConfigTopic(guildId)}: **{topic}**, " +
               $"{strings.WotdConfigPos(guildId)}: **{filters.PartOfSpeech}**, " +
               $"{strings.WotdConfigDifficulty(guildId)}: **{filters.Difficulty}** ({source})";
    }

    /// <summary>
    ///     Names a rule for display, such as "Monday" or "October".
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The display name.</returns>
    public static string RuleName(WordOfTheDaySchedule rule)
    {
        return rule.RuleType == (int)ScheduleRuleType.DayOfWeek
            ? ((DayOfWeek)rule.RuleKey).ToString()
            : WordOfTheDayService.MonthName(rule.RuleKey);
    }

    /// <summary>
    ///     Builds the schedule overview embed.
    /// </summary>
    /// <param name="strings">Localized strings.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="rules">All rules for the guild.</param>
    /// <param name="today">The filters in effect today.</param>
    /// <returns>The embed builder.</returns>
    public static EmbedBuilder BuildScheduleEmbed(GeneratedBotStrings strings, ulong guildId,
        List<WordOfTheDaySchedule> rules, WordFilters today)
    {
        var none = strings.WotdScheduleNone(guildId);
        var days = rules.Where(r => r.RuleType == (int)ScheduleRuleType.DayOfWeek).ToList();
        var months = rules.Where(r => r.RuleType == (int)ScheduleRuleType.Month).ToList();

        var dayText = days.Count == 0
            ? none
            : string.Join("\n", days.Select(r => $"**{RuleName(r)}**: {DescribeRule(strings, guildId, r)}"));
        var monthText = months.Count == 0
            ? none
            : string.Join("\n", months.Select(r => $"**{RuleName(r)}**: {DescribeRule(strings, guildId, r)}"));
        var todayText = DescribeFilters(strings, guildId, today);
        var title = strings.WotdScheduleTitle(guildId);

        return new EmbedBuilder()
            .WithOkColor()
            .WithTitle(title)
            .AddField(strings.WotdScheduleToday(guildId), todayText)
            .AddField(strings.WotdScheduleWeekdays(guildId), dayText)
            .AddField(strings.WotdScheduleMonths(guildId), monthText);
    }

    /// <summary>
    ///     Builds the configuration overview embed.
    /// </summary>
    /// <param name="strings">Localized strings.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="customCount">Number of custom words.</param>
    /// <param name="ruleCount">Number of schedule rules.</param>
    /// <returns>The embed builder.</returns>
    public static EmbedBuilder BuildConfigEmbed(GeneratedBotStrings strings, ulong guildId,
        WordOfTheDayConfig config, int customCount, int ruleCount)
    {
        var none = strings.WotdConfigNone(guildId);
        var channel = config.ChannelId.HasValue ? $"<#{config.ChannelId.Value}>" : none;
        var ping = config.PingRoleId.HasValue ? $"<@&{config.PingRoleId.Value}>" : none;
        var status = config.Enabled ? strings.WotdEnabledStatus(guildId) : strings.WotdDisabledStatus(guildId);
        var template = string.IsNullOrWhiteSpace(config.MessageTemplate)
            ? strings.WotdConfigDefault(guildId)
            : WordOfTheDayService.Clamp(config.MessageTemplate, 200);
        var lastPosted = config.LastPostedDate?.ToString("yyyy-MM-dd") ?? none;
        var topic = string.IsNullOrWhiteSpace(config.Topic) ? none : config.Topic;
        var source = $"{(WordSourceMode)config.SourceMode} ({strings.WotdConfigCustomCount(guildId, customCount)})";
        var time = $"{config.PostHour:00}:00 {config.Timezone}";
        var pos = ((WordPartOfSpeech)config.PartOfSpeech).ToString();
        var difficulty = ((WordDifficulty)config.Difficulty).ToString();
        var title = strings.WotdConfigTitle(guildId);

        return new EmbedBuilder()
            .WithOkColor()
            .WithTitle(title)
            .AddField(strings.WotdConfigStatus(guildId), status, true)
            .AddField(strings.WotdConfigChannel(guildId), channel, true)
            .AddField(strings.WotdConfigTime(guildId), time, true)
            .AddField(strings.WotdConfigPing(guildId), ping, true)
            .AddField(strings.WotdConfigSource(guildId), source, true)
            .AddField(strings.WotdConfigTopic(guildId), topic, true)
            .AddField(strings.WotdConfigPos(guildId), pos, true)
            .AddField(strings.WotdConfigDifficulty(guildId), difficulty, true)
            .AddField(strings.WotdConfigLast(guildId), lastPosted, true)
            .AddField(strings.WotdConfigRules(guildId), ruleCount.ToString(), true)
            .AddField(strings.WotdConfigTemplate(guildId), template);
    }
}
