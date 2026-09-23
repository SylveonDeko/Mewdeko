namespace Mewdeko.Controllers.Common.WordOfTheDay;

/// <summary>
///     Request model for updating a guild's Word of the Day configuration. Null fields are left unchanged.
/// </summary>
public class WordOfTheDayConfigRequest
{
    /// <summary>
    ///     Channel to post daily words in. Zero clears the channel.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     Whether scheduled posting is on.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    ///     Local hour (0 to 23) to post at.
    /// </summary>
    public int? PostHour { get; set; }

    /// <summary>
    ///     IANA timezone name.
    /// </summary>
    public string? Timezone { get; set; }

    /// <summary>
    ///     Role to ping with each post. Zero clears the role.
    /// </summary>
    public ulong? PingRoleId { get; set; }

    /// <summary>
    ///     Custom message template. Empty string clears it.
    /// </summary>
    public string? MessageTemplate { get; set; }

    /// <summary>
    ///     Topic hint for dictionary words. Empty string clears it.
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    ///     Part of speech filter: 0 any, 1 noun, 2 verb, 3 adjective, 4 adverb.
    /// </summary>
    public int? PartOfSpeech { get; set; }

    /// <summary>
    ///     Difficulty filter: 0 any, 1 common, 2 moderate, 3 rare.
    /// </summary>
    public int? Difficulty { get; set; }

    /// <summary>
    ///     Word source: 0 dictionary, 1 custom, 2 mixed.
    /// </summary>
    public int? SourceMode { get; set; }
}

/// <summary>
///     Request model for creating or updating a weekday or month rule. Null fields leave an existing rule unchanged.
/// </summary>
public class WordOfTheDayScheduleRequest
{
    /// <summary>
    ///     0 for a weekday rule, 1 for a month rule.
    /// </summary>
    public int RuleType { get; set; }

    /// <summary>
    ///     Day of week value (0 Sunday to 6 Saturday) or month number (1 to 12).
    /// </summary>
    public int RuleKey { get; set; }

    /// <summary>
    ///     Topic override. Empty string clears the topic on the rule.
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    ///     Part of speech override: 0 inherits, 1 noun, 2 verb, 3 adjective, 4 adverb.
    /// </summary>
    public int? PartOfSpeech { get; set; }

    /// <summary>
    ///     Difficulty override: 0 inherits, 1 common, 2 moderate, 3 rare.
    /// </summary>
    public int? Difficulty { get; set; }
}

/// <summary>
///     Request model for adding a custom word.
/// </summary>
public class WordOfTheDayAddWordRequest
{
    /// <summary>
    ///     The word to add.
    /// </summary>
    public string Word { get; set; } = null!;

    /// <summary>
    ///     Optional definition. Looked up automatically when omitted.
    /// </summary>
    public string? Definition { get; set; }

    /// <summary>
    ///     The dashboard user adding the word.
    /// </summary>
    public ulong AddedBy { get; set; }
}
