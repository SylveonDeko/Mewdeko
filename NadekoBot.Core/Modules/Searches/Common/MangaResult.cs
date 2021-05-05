using Newtonsoft.Json;

namespace NadekoBot.Modules.Searches.Common
{
    public class MangaResult
    {
        public int Id { get; set; }
        [JsonProperty("publishing_status")]
        public string PublishingStatus { get; set; }
        [JsonProperty("image_url_lge")]
        public string ImageUrlLge { get; set; }
        [JsonProperty("title_english")]
        public string TitleEnglish { get; set; }
        [JsonProperty("total_chapters")]
        public int TotalChapters { get; set; }
        [JsonProperty("total_volumes")]
        public int TotalVolumes { get; set; }
        public string Description { get; set; }
        public string[] Genres { get; set; }
        [JsonProperty("average_score")]
        public string AverageScore { get; set; }
        public string Link => "http://anilist.co/manga/" + Id;
        public string Synopsis => Description?.Substring(0, Description.Length > 500 ? 500 : Description.Length) + "...";
    }
}