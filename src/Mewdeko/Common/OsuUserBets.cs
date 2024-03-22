using Newtonsoft.Json;

namespace Mewdeko.Common
{
    /// <summary>
    /// Represents a user's best performance on an Osu! map.
    /// </summary>
    public class OsuUserBests
    {
        /// <summary>
        /// Gets or sets the beatmap ID.
        /// </summary>
        [JsonProperty("beatmap_id")]
        public string BeatmapId { get; set; }

        /// <summary>
        /// Gets or sets the score ID.
        /// </summary>
        [JsonProperty("score_id")]
        public string ScoreId { get; set; }

        /// <summary>
        /// Gets or sets the score achieved.
        /// </summary>
        [JsonProperty("score")]
        public string Score { get; set; }

        /// <summary>
        /// Gets or sets the maximum combo achieved.
        /// </summary>
        [JsonProperty("maxcombo")]
        public string Maxcombo { get; set; }

        /// <summary>
        /// Gets or sets the number of 50s.
        /// </summary>
        [JsonProperty("count50")]
        public double Count50 { get; set; }

        /// <summary>
        /// Gets or sets the number of 100s.
        /// </summary>
        [JsonProperty("count100")]
        public double Count100 { get; set; }

        /// <summary>
        /// Gets or sets the number of 300s.
        /// </summary>
        [JsonProperty("count300")]
        public double Count300 { get; set; }

        /// <summary>
        /// Gets or sets the number of misses.
        /// </summary>
        [JsonProperty("countmiss")]
        public int Countmiss { get; set; }

        /// <summary>
        /// Gets or sets the number of katus.
        /// </summary>
        [JsonProperty("countkatu")]
        public double Countkatu { get; set; }

        /// <summary>
        /// Gets or sets the number of gekis.
        /// </summary>
        [JsonProperty("countgeki")]
        public double Countgeki { get; set; }

        /// <summary>
        /// Gets or sets whether the performance was perfect.
        /// </summary>
        [JsonProperty("perfect")]
        public string Perfect { get; set; }

        /// <summary>
        /// Gets or sets the enabled mods.
        /// </summary>
        [JsonProperty("enabled_mods")]
        public int EnabledMods { get; set; }

        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        [JsonProperty("user_id")]
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets the date of the performance.
        /// </summary>
        [JsonProperty("date")]
        public string Date { get; set; }

        /// <summary>
        /// Gets or sets the rank achieved.
        /// </summary>
        [JsonProperty("rank")]
        public string Rank { get; set; }

        /// <summary>
        /// Gets or sets the performance points (pp) earned.
        /// </summary>
        [JsonProperty("pp")]
        public double Pp { get; set; }

        /// <summary>
        /// Gets or sets whether the replay is available.
        /// </summary>
        [JsonProperty("replay_available")]
        public string ReplayAvailable { get; set; }
    }
}