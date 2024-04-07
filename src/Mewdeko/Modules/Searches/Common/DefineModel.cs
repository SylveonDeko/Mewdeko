using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches.Common;

/// <summary>
/// Represents an audio resource.
/// </summary>
public class Audio
{
    /// <summary>
    /// Gets or sets the URL of the audio.
    /// </summary>
    public string Url { get; set; }
}

/// <summary>
/// Represents an example with text and associated audio.
/// </summary>
public class Example
{
    /// <summary>
    /// Gets or sets the list of audio resources for the example.
    /// </summary>
    public List<Audio> Audio { get; set; }

    /// <summary>
    /// Gets or sets the example text.
    /// </summary>
    public string Text { get; set; }
}

/// <summary>
/// Represents grammatical information for a word or phrase.
/// </summary>
public class GramaticalInfo
{
    /// <summary>
    /// Gets or sets the type of grammatical information.
    /// </summary>
    public string Type { get; set; }
}

/// <summary>
/// Represents a sense (meaning) of a word or phrase.
/// </summary>
public class Sens
{
    /// <summary>
    /// Gets or sets the definition of the sense.
    /// </summary>
    public object? Definition { get; set; }

    /// <summary>
    /// Gets or sets a list of examples for the sense.
    /// </summary>
    [JsonProperty("gramatical_info")]
    public GramaticalInfo GramaticalInfo { get; set; }
}

/// <summary>
/// Represents a result from a word or phrase definition query.
/// </summary>
public class Result
{
    /// <summary>
    /// Gets or sets the part of speech for the word or phrase.
    /// </summary>
    [JsonProperty("part_of_speech")]
    public string PartOfSpeech { get; set; }

    /// <summary>
    /// Gets or sets the list of senses (meanings) for the word or phrase.
    /// </summary>
    public List<Sens>? Senses { get; set; }

    /// <summary>
    /// Gets or sets the URL for more information about the word or phrase.
    /// </summary>
    public string Url { get; set; }
}

/// <summary>
/// Represents the response model for a definition query.
/// </summary>
public class DefineModel
{
    /// <summary>
    /// Gets or sets the list of results from the definition query.
    /// </summary>
    public List<Result?>? Results { get; set; }
}