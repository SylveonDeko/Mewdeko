using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches.Common;

/// <summary>
/// Represents the result of an anime search query.
/// </summary>
public class AnimeResult
{
    /// <summary>
    /// Gets or sets the unique identifier for the anime.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets the airing status of the anime, converted to title case.
    /// </summary>
    public string AiringStatus => AiringStatusParsed.ToTitleCase();

    /// <summary>
    /// Gets or sets the airing status parsed from the JSON property.
    /// </summary>
    [JsonProperty("airing_status")]
    public string AiringStatusParsed { get; set; }

    /// <summary>
    /// Gets or sets the English title of the anime.
    /// </summary>
    [JsonProperty("title_english")]
    public string TitleEnglish { get; set; }

    /// <summary>
    /// Gets or sets the total number of episodes of the anime.
    /// </summary>
    [JsonProperty("total_episodes")]
    public int TotalEpisodes { get; set; }

    /// <summary>
    /// Gets or sets the description of the anime.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the URL to a large image of the anime.
    /// </summary>
    [JsonProperty("image_url_lge")]
    public string ImageUrlLarge { get; set; }

    /// <summary>
    /// Gets or sets the genres of the anime.
    /// </summary>
    public string[] Genres { get; set; }

    /// <summary>
    /// Gets or sets the average score of the anime.
    /// </summary>
    [JsonProperty("average_score")]
    public string AverageScore { get; set; }

    /// <summary>
    /// Gets the link to the anime's page on AniList.
    /// </summary>
    public string Link => $"https://anilist.co/anime/{Id}";

    /// <summary>
    /// Gets a shortened version of the anime's synopsis.
    /// </summary>
    public string Synopsis =>
        $"{Description[..(Description.Length > 500 ? 500 : Description.Length)]}...";
}