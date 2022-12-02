namespace Mewdeko.Modules.Utility.Common.Patreon;

public class DiscordConnection
{
    public string UserId { get; set; }
}

public class SocialConnections
{
    public object Deviantart { get; set; }
    public DiscordConnection Discord { get; set; }
    public object Facebook { get; set; }
    public object Spotify { get; set; }
    public object Twitch { get; set; }
    public object Twitter { get; set; }
    public object Youtube { get; set; }
}

public class UserAttributes
{
    public string About { get; set; }
    public string Created { get; set; }
    public object DiscordId { get; set; }
    public string Email { get; set; }
    public object Facebook { get; set; }
    public object FacebookId { get; set; }
    public string FirstName { get; set; }
    public string FullName { get; set; }
    public int Gender { get; set; }
    public bool HasPassword { get; set; }
    public string ImageUrl { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsNuked { get; set; }
    public bool IsSuspended { get; set; }
    public string LastName { get; set; }
    public SocialConnections SocialConnections { get; set; }
    public int Status { get; set; }
    public string ThumbUrl { get; set; }
    public object Twitch { get; set; }
    public string Twitter { get; set; }
    public string Url { get; set; }
    public string Vanity { get; set; }
    public object Youtube { get; set; }
}

public class Campaign
{
    public Data Data { get; set; }
    public Links Links { get; set; }
}

public class UserRelationships
{
    public Campaign Campaign { get; set; }
}

public class PatreonUser
{
    public UserAttributes Attributes { get; set; }
    public string Id { get; set; }
    public UserRelationships Relationships { get; set; }
    public string Type { get; set; }
}