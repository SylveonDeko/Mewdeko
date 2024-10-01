using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches.Common;

/// <summary>
///     Represents user data from Osu.
/// </summary>
public class OsuUserData
{
    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    [JsonProperty("user_id")]
    public string UserId { get; set; }

    /// <summary>
    ///     Gets or sets the username.
    /// </summary>
    [JsonProperty("username")]
    public string Username { get; set; }

    /// <summary>
    ///     Gets or sets the join date.
    /// </summary>
    [JsonProperty("join_date")]
    public string JoinDate { get; set; }

    /// <summary>
    ///     Gets or sets the count of 300s.
    /// </summary>
    [JsonProperty("count300")]
    public string Count300 { get; set; }

    /// <summary>
    ///     Gets or sets the count of 100s.
    /// </summary>
    [JsonProperty("count100")]
    public string Count100 { get; set; }

    /// <summary>
    ///     Gets or sets the count of 50s.
    /// </summary>
    [JsonProperty("count50")]
    public string Count50 { get; set; }

    /// <summary>
    ///     Gets or sets the play count.
    /// </summary>
    [JsonProperty("playcount")]
    public string Playcount { get; set; }

    /// <summary>
    ///     Gets or sets the ranked score.
    /// </summary>
    [JsonProperty("ranked_score")]
    public string RankedScore { get; set; }

    /// <summary>
    ///     Gets or sets the total score.
    /// </summary>
    [JsonProperty("total_score")]
    public string TotalScore { get; set; }

    /// <summary>
    ///     Gets or sets the PP rank.
    /// </summary>
    [JsonProperty("pp_rank")]
    public string PpRank { get; set; }

    /// <summary>
    ///     Gets or sets the level.
    /// </summary>
    [JsonProperty("level")]
    public double Level { get; set; }

    /// <summary>
    ///     Gets or sets the raw PP.
    /// </summary>
    [JsonProperty("pp_raw")]
    public double PpRaw { get; set; }

    /// <summary>
    ///     Gets or sets the accuracy.
    /// </summary>
    [JsonProperty("accuracy")]
    public double Accuracy { get; set; }

    /// <summary>
    ///     Gets or sets the count of SS ranks.
    /// </summary>
    [JsonProperty("count_rank_ss")]
    public string CountRankSs { get; set; }

    /// <summary>
    ///     Gets or sets the count of SSH ranks.
    /// </summary>
    [JsonProperty("count_rank_ssh")]
    public string CountRankSsh { get; set; }

    /// <summary>
    ///     Gets or sets the count of S ranks.
    /// </summary>
    [JsonProperty("count_rank_s")]
    public string CountRankS { get; set; }

    /// <summary>
    ///     Gets or sets the count of SH ranks.
    /// </summary>
    [JsonProperty("count_rank_sh")]
    public string CountRankSh { get; set; }

    /// <summary>
    ///     Gets or sets the count of A ranks.
    /// </summary>
    [JsonProperty("count_rank_a")]
    public string CountRankA { get; set; }

    /// <summary>
    ///     Gets or sets the country.
    /// </summary>
    [JsonProperty("country")]
    public string Country { get; set; }

    /// <summary>
    ///     Gets or sets the total seconds played.
    /// </summary>
    [JsonProperty("total_seconds_played")]
    public string TotalSecondsPlayed { get; set; }

    /// <summary>
    ///     Gets or sets the PP country rank.
    /// </summary>
    [JsonProperty("pp_country_rank")]
    public string PpCountryRank { get; set; }
}