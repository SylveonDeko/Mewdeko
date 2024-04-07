using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

/// <summary>
/// Represents the response data for a channel on Picarto.
/// </summary>
public class PicartoChannelResponse
{
    /// <summary>
    /// The user ID of the channel.
    /// </summary>
    [JsonProperty("user_id")]
    public int UserId { get; set; }

    /// <summary>
    /// The name of the channel.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; }

    /// <summary>
    /// The URL to the channel's avatar image.
    /// </summary>
    [JsonProperty("avatar")]
    public string Avatar { get; set; }

    /// <summary>
    /// Indicates whether the channel is currently online.
    /// </summary>
    [JsonProperty("online")]
    public bool Online { get; set; }

    /// <summary>
    /// The current number of viewers on the channel.
    /// </summary>
    [JsonProperty("viewers")]
    public int Viewers { get; set; }

    /// <summary>
    /// The total number of viewers the channel has had.
    /// </summary>
    [JsonProperty("viewers_total")]
    public int ViewersTotal { get; set; }

    /// <summary>
    /// The thumbnails for the channel.
    /// </summary>
    [JsonProperty("thumbnails")]
    public Thumbnails Thumbnails { get; set; }

    /// <summary>
    /// The number of followers the channel has.
    /// </summary>
    [JsonProperty("followers")]
    public int Followers { get; set; }

    /// <summary>
    /// The number of subscribers the channel has.
    /// </summary>
    [JsonProperty("subscribers")]
    public int Subscribers { get; set; }

    /// <summary>
    /// Indicates whether the channel is marked as adult.
    /// </summary>
    [JsonProperty("adult")]
    public bool Adult { get; set; }

    /// <summary>
    /// The category of the channel.
    /// </summary>
    [JsonProperty("category")]
    public string Category { get; set; }

    /// <summary>
    /// The account type of the channel.
    /// </summary>
    [JsonProperty("account_type")]
    public string AccountType { get; set; }

    /// <summary>
    /// Indicates whether the channel offers commissions.
    /// </summary>
    [JsonProperty("commissions")]
    public bool Commissions { get; set; }

    /// <summary>
    /// Indicates whether the channel has recordings available.
    /// </summary>
    [JsonProperty("recordings")]
    public bool Recordings { get; set; }

    /// <summary>
    /// The title of the channel.
    /// </summary>
    [JsonProperty("title")]
    public string Title { get; set; }

    /// <summary>
    /// A list of description panels for the channel.
    /// </summary>
    [JsonProperty("description_panels")]
    public List<DescriptionPanel> DescriptionPanels { get; set; }

    /// <summary>
    /// Indicates whether the channel is private.
    /// </summary>
    [JsonProperty("private")]
    public bool Private { get; set; }

    /// <summary>
    /// The private message associated with the channel.
    /// </summary>
    [JsonProperty("private_message")]
    public string PrivateMessage { get; set; }

    /// <summary>
    /// Indicates whether the channel is related to gaming.
    /// </summary>
    [JsonProperty("gaming")]
    public bool Gaming { get; set; }

    /// <summary>
    /// The chat settings for the channel.
    /// </summary>
    [JsonProperty("chat_settings")]
    public ChatSettings ChatSettings { get; set; }

    /// <summary>
    /// The date and time when the channel was last live.
    /// </summary>
    [JsonProperty("last_live")]
    public DateTime LastLive { get; set; }

    /// <summary>
    /// A list of tags associated with the channel.
    /// </summary>
    [JsonProperty("tags")]
    public List<string> Tags { get; set; }

    /// <summary>
    /// A list of channels this channel is multistreaming with.
    /// </summary>
    [JsonProperty("multistream")]
    public List<Multistream> Multistream { get; set; }

    /// <summary>
    /// A list of languages the channel streams in.
    /// </summary>
    [JsonProperty("languages")]
    public List<Language> Languages { get; set; }

    /// <summary>
    /// Indicates whether the current user is following the channel.
    /// </summary>
    [JsonProperty("following")]
    public bool Following { get; set; }
}

/// <summary>
/// Represents the thumbnails associated with a Picarto channel.
/// </summary>
public class Thumbnails
{
    /// <summary>
    /// URL of the web-sized thumbnail.
    /// </summary>
    [JsonProperty("web")]
    public string Web { get; set; }

    /// <summary>
    /// URL of the large web-sized thumbnail.
    /// </summary>
    [JsonProperty("web_large")]
    public string WebLarge { get; set; }

    /// <summary>
    /// URL of the mobile-sized thumbnail.
    /// </summary>
    [JsonProperty("mobile")]
    public string Mobile { get; set; }

    /// <summary>
    /// URL of the tablet-sized thumbnail.
    /// </summary>
    [JsonProperty("tablet")]
    public string Tablet { get; set; }
}

/// <summary>
/// Represents a description panel within a Picarto channel's page.
/// </summary>
public class DescriptionPanel
{
    /// <summary>
    /// Title of the description panel.
    /// </summary>
    [JsonProperty("title")]
    public string Title { get; set; }

    /// <summary>
    /// Body text of the description panel.
    /// </summary>
    [JsonProperty("body")]
    public string Body { get; set; }

    /// <summary>
    /// URL of an image associated with the description panel.
    /// </summary>
    [JsonProperty("image")]
    public string Image { get; set; }

    /// <summary>
    /// URL that the image links to, if any.
    /// </summary>
    [JsonProperty("image_link")]
    public string ImageLink { get; set; }

    /// <summary>
    /// Text of the button within the panel, if any.
    /// </summary>
    [JsonProperty("button_text")]
    public string ButtonText { get; set; }

    /// <summary>
    /// URL that the button links to, if any.
    /// </summary>
    [JsonProperty("button_link")]
    public string ButtonLink { get; set; }

    /// <summary>
    /// Position of the panel among other panels.
    /// </summary>
    [JsonProperty("position")]
    public int Position { get; set; }
}

/// <summary>
/// Represents chat settings for a Picarto channel.
/// </summary>
public class ChatSettings
{
    /// <summary>
    /// Indicates whether guests can chat.
    /// </summary>
    [JsonProperty("guest_chat")]
    public bool GuestChat { get; set; }

    /// <summary>
    /// Indicates whether links are allowed in chat.
    /// </summary>
    [JsonProperty("links")]
    public bool Links { get; set; }

    /// <summary>
    /// The level of chat filtering.
    /// </summary>
    [JsonProperty("level")]
    public int Level { get; set; }
}

/// <summary>
/// Represents a channel that is part of a multistream with the current Picarto channel.
/// </summary>
public class Multistream
{
    /// <summary>
    /// The user ID of the multistreaming channel.
    /// </summary>
    [JsonProperty("user_id")]
    public int UserId { get; set; }

    /// <summary>
    /// The name of the multistreaming channel.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; }

    /// <summary>
    /// Indicates whether the multistreaming channel is online.
    /// </summary>
    [JsonProperty("online")]
    public bool Online { get; set; }

    /// <summary>
    /// Indicates whether the multistreaming channel is marked as adult.
    /// </summary>
    [JsonProperty("adult")]
    public bool Adult { get; set; }
}

/// <summary>
/// Represents a language that the Picarto channel streams in.
/// </summary>
public class Language
{
    /// <summary>
    /// The ID of the language.
    /// </summary>
    [JsonProperty("id")]
    public int Id { get; set; }

    /// <summary>
    /// The name of the language.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; }
}