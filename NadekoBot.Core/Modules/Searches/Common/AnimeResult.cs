using NadekoBot.Extensions;
using Newtonsoft.Json;

namespace NadekoBot.Modules.Searches.Common
{
    public class AnimeResult
    {
        public int Id { get; set; }
        public string AiringStatus => AiringStatusParsed.ToTitleCase();
        [JsonProperty("airing_status")]
        public string AiringStatusParsed { get; set; }
        [JsonProperty("title_english")]
        public string TitleEnglish { get; set; }
        [JsonProperty("total_episodes")]
        public int TotalEpisodes { get; set; }
        public string Description { get; set; }
        [JsonProperty("image_url_lge")]
        public string ImageUrlLarge { get; set; }
        public string[] Genres { get; set; }
        [JsonProperty("average_score")]
        public string AverageScore { get; set; }

        public string Link => "http://anilist.co/anime/" + Id;
        public string Synopsis => Description?.Substring(0, Description.Length > 500 ? 500 : Description.Length) + "...";
    }
}