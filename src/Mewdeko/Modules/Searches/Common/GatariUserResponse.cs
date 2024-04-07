using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches.Common;

/// <summary>
/// Represents user data from a Gatari API response.
/// </summary>
public class UserData
{
    /// <summary>
    /// Gets or sets the abbreviation for the user.
    /// </summary>
    [JsonProperty("abbr")]
    public object Abbr { get; set; }

    /// <summary>
    /// Gets or sets the clan ID for the user.
    /// </summary>
    [JsonProperty("clanid")]
    public object Clanid { get; set; }

    /// <summary>
    /// Gets or sets the country of the user.
    /// </summary>
    [JsonProperty("country")]
    public string Country { get; set; }

    /// <summary>
    /// Gets or sets the favorite game mode of the user.
    /// </summary>
    [JsonProperty("favourite_mode")]
    public int FavouriteMode { get; set; }

    /// <summary>
    /// Gets or sets the number of followers of the user.
    /// </summary>
    [JsonProperty("followers_count")]
    public int FollowersCount { get; set; }

    /// <summary>
    /// Gets or sets the unique ID of the user.
    /// </summary>
    [JsonProperty("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the latest activity timestamp of the user.
    /// </summary>
    [JsonProperty("latest_activity")]
    public int LatestActivity { get; set; }

    /// <summary>
    /// Gets or sets the play style of the user.
    /// </summary>
    [JsonProperty("play_style")]
    public int PlayStyle { get; set; }

    /// <summary>
    /// Gets or sets the privileges of the user.
    /// </summary>
    [JsonProperty("privileges")]
    public int Privileges { get; set; }

    /// <summary>
    /// Gets or sets the registration timestamp of the user.
    /// </summary>
    [JsonProperty("registered_on")]
    public int RegisteredOn { get; set; }

    /// <summary>
    /// Gets or sets the username of the user.
    /// </summary>
    [JsonProperty("username")]
    public string Username { get; set; }

    /// <summary>
    /// Gets or sets the alternative known as (aka) username of the user.
    /// </summary>
    [JsonProperty("username_aka")]
    public string UsernameAka { get; set; }
}

/// <summary>
/// Represents the response from a Gatari API query for users.
/// </summary>
public class GatariUserResponse
{
    /// <summary>
    /// Gets or sets the response code from the Gatari API.
    /// </summary>
    [JsonProperty("code")]
    public int Code { get; set; }

    /// <summary>
    /// Gets or sets the list of user data returned by the Gatari API.
    /// </summary>
    [JsonProperty("users")]
    public List<UserData> Users { get; set; }
}