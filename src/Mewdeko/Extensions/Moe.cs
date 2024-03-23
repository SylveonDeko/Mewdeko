using Newtonsoft.Json;

namespace Mewdeko.Extensions;

/// <summary>
/// Represents a result from the Moe API.
/// </summary>
public class Result
{
    /// <summary>
    /// The Anilist ID.
    /// </summary>
    [JsonProperty("anilist")]
    public int Anilist { get; set; }

    /// <summary>
    /// The filename of the image.
    /// </summary>
    [JsonProperty("filename")]
    public string Filename { get; set; }

    /// <summary>
    /// The episode number.
    /// </summary>
    [JsonProperty("episode")]
    public double Episode { get; set; }

    /// <summary>
    /// The time the scene starts.
    /// </summary>
    [JsonProperty("from")]
    public double From { get; set; }

    /// <summary>
    /// The time the scene ends.
    /// </summary>
    [JsonProperty("to")]
    public double To { get; set; }

    /// <summary>
    /// The similarity of the scene in percentage.
    /// </summary>
    [JsonProperty("similarity")]
    public double Similarity { get; set; }

    /// <summary>
    /// The video URL.
    /// </summary>
    [JsonProperty("video")]
    public string Video { get; set; }

    /// <summary>
    /// The image URL.
    /// </summary>
    [JsonProperty("image")]
    public string Image { get; set; }
}

/// <summary>
/// Represents a response from the Moe API.
/// </summary>
public class MoeResponse
{
    /// <summary>
    /// The total number of frames.
    /// </summary>
    [JsonProperty("frameCount")]
    public int FrameCount { get; set; }

    /// <summary>
    /// The error message.
    /// </summary>
    [JsonProperty("error")]
    public string Error { get; set; }

    /// <summary>
    /// The results from the API.
    /// </summary>
    [JsonProperty("result")]
    public List<Result> Result { get; set; }
}