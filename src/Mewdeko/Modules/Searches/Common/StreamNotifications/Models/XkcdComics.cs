using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

/// <summary>
/// Represents an XKCD comic, including metadata and content.
/// </summary>
public class XkcdComic
{
    /// <summary>
    /// The unique identifier for the comic.
    /// </summary>
    public int Num { get; set; }

    /// <summary>
    /// The publication month of the comic.
    /// </summary>
    public string Month { get; set; }

    /// <summary>
    /// The publication year of the comic.
    /// </summary>
    public string Year { get; set; }

    /// <summary>
    /// The sanitized title of the comic.
    /// </summary>
    [JsonProperty("safe_title")]
    public string Title { get; set; }

    /// <summary>
    /// The direct URL to the comic's image.
    /// </summary>
    [JsonProperty("img")]
    public string ImageLink { get; set; }

    /// <summary>
    /// The alt text provided for the comic's image, often containing humor or additional context.
    /// </summary>
    public string Alt { get; set; }

    /// <summary>
    /// The day of the month on which the comic was published.
    /// </summary>
    public string Day { get; set; }

    /// <summary>
    /// A transcript or text contained within the comic, if available.
    /// </summary>
    [JsonProperty("transcript")]
    public string Transcript { get; set; }

    /// <summary>
    /// A link to a related comic or external content, if provided.
    /// </summary>
    [JsonProperty("link")]
    public string Link { get; set; }

    /// <summary>
    /// The news headline or message associated with the comic's publication, if available.
    /// </summary>
    [JsonProperty("news")]
    public string News { get; set; }
}