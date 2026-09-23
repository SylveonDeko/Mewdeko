namespace Mewdeko.Modules.WordOfTheDay.Common;

/// <summary>
///     The filters in effect for a given day after schedule rules are applied over the base config.
/// </summary>
public class WordFilters
{
    /// <summary>
    ///     Topic hint for Datamuse, or null for general vocabulary.
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    ///     Part of speech restriction.
    /// </summary>
    public WordPartOfSpeech PartOfSpeech { get; set; }

    /// <summary>
    ///     Frequency based difficulty restriction.
    /// </summary>
    public WordDifficulty Difficulty { get; set; }

    /// <summary>
    ///     Which rule supplied the overrides, or null when only the base config applied.
    /// </summary>
    public string? SourceRule { get; set; }
}
