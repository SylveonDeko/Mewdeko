namespace Mewdeko.Modules.WordOfTheDay.Common;

/// <summary>
///     Where a guild's daily word is drawn from.
/// </summary>
public enum WordSourceMode
{
    /// <summary>
    ///     Words are fetched from Datamuse using the guild's topic, part of speech, and difficulty filters.
    /// </summary>
    Dictionary = 0,

    /// <summary>
    ///     Words are drawn only from the guild's custom word pool.
    /// </summary>
    Custom = 1,

    /// <summary>
    ///     Custom words are used when any remain unused, otherwise Datamuse is queried.
    /// </summary>
    Mixed = 2
}

/// <summary>
///     What calendar unit a schedule rule applies to.
/// </summary>
public enum ScheduleRuleType
{
    /// <summary>
    ///     Applies on a specific day of the week. Key is <see cref="DayOfWeek" />.
    /// </summary>
    DayOfWeek = 0,

    /// <summary>
    ///     Applies during a specific month. Key is 1 to 12.
    /// </summary>
    Month = 1
}

/// <summary>
///     Part of speech filter applied when fetching words from Datamuse.
/// </summary>
public enum WordPartOfSpeech
{
    /// <summary>
    ///     Any part of speech.
    /// </summary>
    Any = 0,

    /// <summary>
    ///     Nouns only.
    /// </summary>
    Noun = 1,

    /// <summary>
    ///     Verbs only.
    /// </summary>
    Verb = 2,

    /// <summary>
    ///     Adjectives only.
    /// </summary>
    Adjective = 3,

    /// <summary>
    ///     Adverbs only.
    /// </summary>
    Adverb = 4
}

/// <summary>
///     Difficulty filter based on how often a word appears per million words of English text.
/// </summary>
public enum WordDifficulty
{
    /// <summary>
    ///     No frequency filter.
    /// </summary>
    Any = 0,

    /// <summary>
    ///     Everyday words that appear frequently.
    /// </summary>
    Common = 1,

    /// <summary>
    ///     Words most adults know but do not use daily.
    /// </summary>
    Moderate = 2,

    /// <summary>
    ///     Uncommon or scholarly vocabulary.
    /// </summary>
    Rare = 3
}
