namespace Mewdeko.Controllers.Common.WordOfTheDay;

/// <summary>
///     A guild's Word of the Day configuration.
/// </summary>
public class WordOfTheDayConfigResponse
{
    /// <summary>
    ///     Channel daily words are posted in.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     Whether scheduled posting is on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Local hour (0 to 23) posts happen at.
    /// </summary>
    public int PostHour { get; set; }

    /// <summary>
    ///     IANA timezone name.
    /// </summary>
    public string Timezone { get; set; } = "UTC";

    /// <summary>
    ///     Role pinged with each post.
    /// </summary>
    public ulong? PingRoleId { get; set; }

    /// <summary>
    ///     Custom message template, or null for the default embed.
    /// </summary>
    public string? MessageTemplate { get; set; }

    /// <summary>
    ///     Topic hint for dictionary words.
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    ///     Part of speech filter: 0 any, 1 noun, 2 verb, 3 adjective, 4 adverb.
    /// </summary>
    public int PartOfSpeech { get; set; }

    /// <summary>
    ///     Difficulty filter: 0 any, 1 common, 2 moderate, 3 rare.
    /// </summary>
    public int Difficulty { get; set; }

    /// <summary>
    ///     Word source: 0 dictionary, 1 custom, 2 mixed.
    /// </summary>
    public int SourceMode { get; set; }

    /// <summary>
    ///     Guild-local date of the last post.
    /// </summary>
    public DateTime? LastPostedDate { get; set; }

    /// <summary>
    ///     Number of words in the custom pool.
    /// </summary>
    public int CustomWordCount { get; set; }

    /// <summary>
    ///     Whether a public discussion thread is created under each post.
    /// </summary>
    public bool CreateThread { get; set; }

    /// <summary>
    ///     Thread name template, or null for the default.
    /// </summary>
    public string? ThreadName { get; set; }

    /// <summary>
    ///     Auto-archive duration in minutes: 60, 1440, 4320, or 10080.
    /// </summary>
    public int ThreadAutoArchiveMinutes { get; set; }
}

/// <summary>
///     A custom word in a guild's pool.
/// </summary>
public class WordOfTheDayWordResponse
{
    /// <summary>
    ///     Row identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The word.
    /// </summary>
    public string Word { get; set; } = null!;

    /// <summary>
    ///     Part of speech, when known.
    /// </summary>
    public string? PartOfSpeech { get; set; }

    /// <summary>
    ///     Definition, when known.
    /// </summary>
    public string? Definition { get; set; }

    /// <summary>
    ///     Example sentence, when known.
    /// </summary>
    public string? Example { get; set; }

    /// <summary>
    ///     User who added the word.
    /// </summary>
    public ulong AddedBy { get; set; }

    /// <summary>
    ///     How many times the word has been posted.
    /// </summary>
    public int TimesUsed { get; set; }

    /// <summary>
    ///     When the word was last posted.
    /// </summary>
    public DateTime? LastUsed { get; set; }

    /// <summary>
    ///     When the word was added.
    /// </summary>
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     A weekday or month rule that overrides the base filters.
/// </summary>
public class WordOfTheDayScheduleResponse
{
    /// <summary>
    ///     Row identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     0 for a weekday rule, 1 for a month rule.
    /// </summary>
    public int RuleType { get; set; }

    /// <summary>
    ///     Day of week value (0 Sunday to 6 Saturday) or month number (1 to 12).
    /// </summary>
    public int RuleKey { get; set; }

    /// <summary>
    ///     Topic override, or null to inherit.
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    ///     Part of speech override, or null to inherit.
    /// </summary>
    public int? PartOfSpeech { get; set; }

    /// <summary>
    ///     Difficulty override, or null to inherit.
    /// </summary>
    public int? Difficulty { get; set; }
}

/// <summary>
///     A previously posted word.
/// </summary>
public class WordOfTheDayHistoryResponse
{
    /// <summary>
    ///     Row identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The word.
    /// </summary>
    public string Word { get; set; } = null!;

    /// <summary>
    ///     Part of speech, when known.
    /// </summary>
    public string? PartOfSpeech { get; set; }

    /// <summary>
    ///     The definition that was posted.
    /// </summary>
    public string Definition { get; set; } = null!;

    /// <summary>
    ///     Example sentence, when one was posted.
    /// </summary>
    public string? Example { get; set; }

    /// <summary>
    ///     Pronunciation, when one was posted.
    /// </summary>
    public string? Phonetic { get; set; }

    /// <summary>
    ///     Guild-local date the word was posted on.
    /// </summary>
    public DateTime PostedOn { get; set; }
}
