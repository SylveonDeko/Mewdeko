using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Patreon.Common;

/// <summary>
///     The Patreon user, which can be both patron and creator
/// </summary>
public class User : PatreonResource
{
    /// <summary>
    ///     User attributes containing all the user's properties
    /// </summary>
    [JsonPropertyName("attributes")]
    public UserAttributes Attributes { get; set; } = new();

    /// <summary>
    ///     Relationships to other resources
    /// </summary>
    [JsonPropertyName("relationships")]
    public UserRelationships? Relationships { get; set; }
}

/// <summary>
///     All attributes for a Patreon user
/// </summary>
public class UserAttributes
{
    /// <summary>
    ///     The user's about text, which appears on their profile. Can be null.
    /// </summary>
    [JsonPropertyName("about")]
    public string? About { get; set; }

    /// <summary>
    ///     True if this user can view nsfw content. Can be null.
    /// </summary>
    [JsonPropertyName("can_see_nsfw")]
    public bool? CanSeeNsfw { get; set; }

    /// <summary>
    ///     Datetime of this user's account creation
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime? Created { get; set; }

    /// <summary>
    ///     The user's email address. Users may restrict the sharing of their email address. Requires certain scopes to access.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>
    ///     First name. Can be null.
    /// </summary>
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    /// <summary>
    ///     Last name. Can be null.
    /// </summary>
    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    /// <summary>
    ///     Combined first and last name
    /// </summary>
    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }

    /// <summary>
    ///     True if the user has chosen to keep private which creators they pledge to. Can be null.
    /// </summary>
    [JsonPropertyName("hide_pledges")]
    public bool? HidePledges { get; set; }

    /// <summary>
    ///     The user's profile picture URL, scaled to width 400px
    /// </summary>
    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    /// <summary>
    ///     True if the user has confirmed their email
    /// </summary>
    [JsonPropertyName("is_email_verified")]
    public bool? IsEmailVerified { get; set; }

    /// <summary>
    ///     True if this user has an active campaign
    /// </summary>
    [JsonPropertyName("is_creator")]
    public bool? IsCreator { get; set; }

    /// <summary>
    ///     How many posts this user has liked
    /// </summary>
    [JsonPropertyName("like_count")]
    public int? LikeCount { get; set; }

    /// <summary>
    ///     Mapping from user's connected app names to external user id on the respective app
    /// </summary>
    [JsonPropertyName("social_connections")]
    public SocialConnections? SocialConnections { get; set; }

    /// <summary>
    ///     The user's profile picture URL, scaled to a square of size 100x100px
    /// </summary>
    [JsonPropertyName("thumb_url")]
    public string? ThumbUrl { get; set; }

    /// <summary>
    ///     URL of this user's creator or patron profile
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

/// <summary>
///     Social media connections for a user
/// </summary>
public class SocialConnections
{
    /// <summary>
    ///     DeviantArt connection
    /// </summary>
    [JsonPropertyName("deviantart")]
    public SocialConnection? DeviantArt { get; set; }

    /// <summary>
    ///     Discord connection
    /// </summary>
    [JsonPropertyName("discord")]
    public SocialConnection? Discord { get; set; }

    /// <summary>
    ///     Facebook connection
    /// </summary>
    [JsonPropertyName("facebook")]
    public SocialConnection? Facebook { get; set; }

    /// <summary>
    ///     Reddit connection
    /// </summary>
    [JsonPropertyName("reddit")]
    public SocialConnection? Reddit { get; set; }

    /// <summary>
    ///     Spotify connection
    /// </summary>
    [JsonPropertyName("spotify")]
    public SocialConnection? Spotify { get; set; }

    /// <summary>
    ///     Twitch connection
    /// </summary>
    [JsonPropertyName("twitch")]
    public SocialConnection? Twitch { get; set; }

    /// <summary>
    ///     Twitter connection
    /// </summary>
    [JsonPropertyName("twitter")]
    public SocialConnection? Twitter { get; set; }

    /// <summary>
    ///     YouTube connection
    /// </summary>
    [JsonPropertyName("youtube")]
    public SocialConnection? Youtube { get; set; }
}

/// <summary>
///     Represents a connection to a social media platform.
///     In Patreon API v2, this is an object with user_id. Can be null if no connection exists.
/// </summary>
[JsonConverter(typeof(SocialConnectionConverter))]
public class SocialConnection
{
    /// <summary>
    ///     The user ID on the connected platform
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    /// <summary>
    ///     The URL to the user's profile on the connected platform. Can be null.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    ///     Gets the user ID as a string (for convenience)
    /// </summary>
    public override string? ToString()
    {
        return UserId;
    }
}

/// <summary>
///     User relationships to other resources
/// </summary>
public class UserRelationships
{
    /// <summary>
    ///     The user's campaign (if they are a creator)
    /// </summary>
    [JsonPropertyName("campaign")]
    public SingleRelationship? Campaign { get; set; }

    /// <summary>
    ///     Usually a zero or one-element array with the user's membership to the token creator's campaign, if they are a
    ///     member.
    ///     With the identity.memberships scope, this returns memberships to ALL campaigns the user is a member of.
    /// </summary>
    [JsonPropertyName("memberships")]
    public MultipleRelationship? Memberships { get; set; }
}