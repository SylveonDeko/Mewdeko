using System.Text.Json.Serialization;

namespace Mewdeko.Modules.WordOfTheDay.Common;

/// <summary>
///     A resolved word ready to be posted, with its definition and optional extras.
/// </summary>
public class WordEntry
{
    /// <summary>
    ///     The word itself.
    /// </summary>
    public string Word { get; set; } = null!;

    /// <summary>
    ///     Human readable part of speech, such as "noun".
    /// </summary>
    public string? PartOfSpeech { get; set; }

    /// <summary>
    ///     The primary definition.
    /// </summary>
    public string Definition { get; set; } = null!;

    /// <summary>
    ///     An example sentence, when one is available.
    /// </summary>
    public string? Example { get; set; }

    /// <summary>
    ///     IPA style pronunciation, when one is available.
    /// </summary>
    public string? Phonetic { get; set; }

    /// <summary>
    ///     Whether the word came from the guild's custom pool rather than Datamuse.
    /// </summary>
    public bool IsCustom { get; set; }

    /// <summary>
    ///     Identifier of the custom pool row this entry was drawn from, if any.
    /// </summary>
    public int? CustomWordId { get; set; }
}

/// <summary>
///     A single result row from the Datamuse words endpoint.
/// </summary>
public class DatamuseWord
{
    /// <summary>
    ///     The word text.
    /// </summary>
    [JsonPropertyName("word")]
    public string Word { get; set; } = null!;

    /// <summary>
    ///     Relevance score assigned by Datamuse.
    /// </summary>
    [JsonPropertyName("score")]
    public long Score { get; set; }

    /// <summary>
    ///     Metadata tags such as part of speech codes and a frequency marker.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    /// <summary>
    ///     Definitions prefixed with a part of speech code and a tab.
    /// </summary>
    [JsonPropertyName("defs")]
    public List<string>? Defs { get; set; }
}

/// <summary>
///     A single entry from the Free Dictionary API.
/// </summary>
public class FreeDictionaryEntry
{
    /// <summary>
    ///     IPA pronunciation of the headword.
    /// </summary>
    [JsonPropertyName("phonetic")]
    public string? Phonetic { get; set; }

    /// <summary>
    ///     Meanings grouped by part of speech.
    /// </summary>
    [JsonPropertyName("meanings")]
    public List<FreeDictionaryMeaning>? Meanings { get; set; }
}

/// <summary>
///     A group of definitions sharing a part of speech.
/// </summary>
public class FreeDictionaryMeaning
{
    /// <summary>
    ///     The part of speech for this meaning group.
    /// </summary>
    [JsonPropertyName("partOfSpeech")]
    public string? PartOfSpeech { get; set; }

    /// <summary>
    ///     The definitions in this group.
    /// </summary>
    [JsonPropertyName("definitions")]
    public List<FreeDictionaryDefinition>? Definitions { get; set; }
}

/// <summary>
///     A definition with an optional usage example.
/// </summary>
public class FreeDictionaryDefinition
{
    /// <summary>
    ///     The definition text.
    /// </summary>
    [JsonPropertyName("definition")]
    public string? Definition { get; set; }

    /// <summary>
    ///     An example sentence using the word.
    /// </summary>
    [JsonPropertyName("example")]
    public string? Example { get; set; }
}
