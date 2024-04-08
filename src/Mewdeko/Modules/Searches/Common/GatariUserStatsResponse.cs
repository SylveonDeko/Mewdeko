using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches.Common
{
    /// <summary>
    /// Represents user statistics retrieved from the Gatari API.
    /// </summary>
    public class UserStats
    {
        /// <summary>
        /// Gets or sets the count of A ranks.
        /// </summary>
        [JsonProperty("a_count")]
        public int ACount { get; set; }

        /// <summary>
        /// Gets or sets the average accuracy.
        /// </summary>
        [JsonProperty("avg_accuracy")]
        public double AvgAccuracy { get; set; }

        /// <summary>
        /// Gets or sets the average hits per play.
        /// </summary>
        [JsonProperty("avg_hits_play")]
        public double AvgHitsPlay { get; set; }

        /// <summary>
        /// Gets or sets the country rank.
        /// </summary>
        [JsonProperty("country_rank")]
        public int CountryRank { get; set; }

        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the user level.
        /// </summary>
        [JsonProperty("level")]
        public int Level { get; set; }

        /// <summary>
        /// Gets or sets the level progress.
        /// </summary>
        [JsonProperty("level_progress")]
        public int LevelProgress { get; set; }

        /// <summary>
        /// Gets or sets the maximum combo achieved.
        /// </summary>
        [JsonProperty("max_combo")]
        public int MaxCombo { get; set; }

        /// <summary>
        /// Gets or sets the total play count.
        /// </summary>
        [JsonProperty("playcount")]
        public int Playcount { get; set; }

        /// <summary>
        /// Gets or sets the total playtime.
        /// </summary>
        [JsonProperty("playtime")]
        public int Playtime { get; set; }

        /// <summary>
        /// Gets or sets the performance points (PP).
        /// </summary>
        [JsonProperty("pp")]
        public int Pp { get; set; }

        /// <summary>
        /// Gets or sets the overall rank.
        /// </summary>
        [JsonProperty("rank")]
        public int Rank { get; set; }

        /// <summary>
        /// Gets or sets the ranked score.
        /// </summary>
        [JsonProperty("ranked_score")]
        public int RankedScore { get; set; }

        /// <summary>
        /// Gets or sets the count of replays watched.
        /// </summary>
        [JsonProperty("replays_watched")]
        public int ReplaysWatched { get; set; }

        /// <summary>
        /// Gets or sets the count of S ranks.
        /// </summary>
        [JsonProperty("s_count")]
        public int SCount { get; set; }

        /// <summary>
        /// Gets or sets the count of SH ranks.
        /// </summary>
        [JsonProperty("sh_count")]
        public int ShCount { get; set; }

        /// <summary>
        /// Gets or sets the total hits.
        /// </summary>
        [JsonProperty("total_hits")]
        public int TotalHits { get; set; }

        /// <summary>
        /// Gets or sets the total score.
        /// </summary>
        [JsonProperty("total_score")]
        public long TotalScore { get; set; }

        /// <summary>
        /// Gets or sets the count of X ranks.
        /// </summary>
        [JsonProperty("x_count")]
        public int XCount { get; set; }

        /// <summary>
        /// Gets or sets the count of XH ranks.
        /// </summary>
        [JsonProperty("xh_count")]
        public int XhCount { get; set; }
    }

    /// <summary>
    /// Represents the response structure for user statistics from the Gatari API.
    /// </summary>
    public class GatariUserStatsResponse
    {
        /// <summary>
        /// Gets or sets the response code.
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Gets or sets the user statistics.
        /// </summary>
        [JsonProperty("stats")]
        public UserStats Stats { get; set; }
    }
}