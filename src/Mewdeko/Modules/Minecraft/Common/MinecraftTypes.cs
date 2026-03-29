using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Minecraft.Common;

/// <summary>
///     Represents the type of Minecraft server.
/// </summary>
public enum McServerType
{
    /// <summary>
    ///     Java Edition server.
    /// </summary>
    Java = 0,

    /// <summary>
    ///     Bedrock Edition server.
    /// </summary>
    Bedrock = 1
}

/// <summary>
///     Represents how server status is displayed in the watch channel.
/// </summary>
public enum McWatchMode
{
    /// <summary>
    ///     Posts and edits an embed in the watch channel.
    /// </summary>
    Embed = 0,

    /// <summary>
    ///     Updates the watch channel's topic with status info.
    /// </summary>
    ChannelTopic = 1,

    /// <summary>
    ///     Both edits an embed and updates the channel topic.
    /// </summary>
    Both = 2
}

/// <summary>
///     Represents the status of a queried Minecraft server.
/// </summary>
public class McServerStatus
{
    /// <summary>
    ///     Gets or sets whether the server is online.
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    ///     Gets or sets the server's Message of the Day.
    /// </summary>
    public string Motd { get; set; } = "";

    /// <summary>
    ///     Gets or sets the number of players currently online.
    /// </summary>
    public int PlayersOnline { get; set; }

    /// <summary>
    ///     Gets or sets the maximum number of players.
    /// </summary>
    public int PlayersMax { get; set; }

    /// <summary>
    ///     Gets or sets the list of online player names.
    /// </summary>
    public List<string> PlayerList { get; set; } = [];

    /// <summary>
    ///     Gets or sets the server version string.
    /// </summary>
    public string Version { get; set; } = "";

    /// <summary>
    ///     Gets or sets the server's favicon as a base64 data URI.
    /// </summary>
    public string? Favicon { get; set; }

    /// <summary>
    ///     Gets or sets the ping latency in milliseconds.
    /// </summary>
    public int Latency { get; set; }

    /// <summary>
    ///     Gets or sets the map name (available via Query protocol).
    /// </summary>
    public string? Map { get; set; }

    /// <summary>
    ///     Gets or sets the game mode (available via Query protocol).
    /// </summary>
    public string? GameMode { get; set; }

    /// <summary>
    ///     Gets or sets the server software (available via Query protocol).
    /// </summary>
    public string? Software { get; set; }

    /// <summary>
    ///     Gets or sets the list of plugins (available via Query protocol).
    /// </summary>
    public List<string> Plugins { get; set; } = [];

    /// <summary>
    ///     Gets or sets whether the data was retrieved via the Query protocol.
    /// </summary>
    public bool IsQueryResponse { get; set; }
}

/// <summary>
///     Represents a Minecraft player profile from the Mojang API.
/// </summary>
public class McPlayerProfile
{
    /// <summary>
    ///     Gets or sets the player's username.
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    ///     Gets or sets the player's UUID.
    /// </summary>
    public string Uuid { get; set; } = "";

    /// <summary>
    ///     Gets or sets the URL to the player's full body skin render.
    /// </summary>
    public string SkinUrl { get; set; } = "";

    /// <summary>
    ///     Gets or sets the URL to the player's head avatar.
    /// </summary>
    public string AvatarUrl { get; set; } = "";
}

/// <summary>
///     Represents a Mojang API profile response.
/// </summary>
public class MojangProfile
{
    /// <summary>
    ///     Gets or sets the player's UUID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    ///     Gets or sets the player's name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

/// <summary>
///     Represents the JSON response from a Java Edition server status ping.
/// </summary>
public class McJavaResponse
{
    /// <summary>
    ///     Gets or sets the server description/MOTD.
    /// </summary>
    [JsonPropertyName("description")]
    public McJavaDescription? Description { get; set; }

    /// <summary>
    ///     Gets or sets the player information.
    /// </summary>
    [JsonPropertyName("players")]
    public McJavaPlayers? Players { get; set; }

    /// <summary>
    ///     Gets or sets the version information.
    /// </summary>
    [JsonPropertyName("version")]
    public McJavaVersion? Version { get; set; }

    /// <summary>
    ///     Gets or sets the server favicon as a base64 data URI.
    /// </summary>
    [JsonPropertyName("favicon")]
    public string? Favicon { get; set; }
}

/// <summary>
///     Represents the description/MOTD in a Java server response.
/// </summary>
[JsonConverter(typeof(McJavaDescriptionConverter))]
public class McJavaDescription
{
    /// <summary>
    ///     Gets or sets the text content of the MOTD.
    /// </summary>
    public string? Text { get; set; }
}

/// <summary>
///     Represents player information in a Java server response.
/// </summary>
public class McJavaPlayers
{
    /// <summary>
    ///     Gets or sets the maximum number of players.
    /// </summary>
    [JsonPropertyName("max")]
    public int Max { get; set; }

    /// <summary>
    ///     Gets or sets the number of online players.
    /// </summary>
    [JsonPropertyName("online")]
    public int Online { get; set; }

    /// <summary>
    ///     Gets or sets the sample list of online players.
    /// </summary>
    [JsonPropertyName("sample")]
    public List<McJavaPlayerSample>? Sample { get; set; }
}

/// <summary>
///     Represents a single player entry in the sample player list.
/// </summary>
public class McJavaPlayerSample
{
    /// <summary>
    ///     Gets or sets the player's display name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the player's UUID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}

/// <summary>
///     Represents version information in a Java server response.
/// </summary>
public class McJavaVersion
{
    /// <summary>
    ///     Gets or sets the version name string.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the protocol version number.
    /// </summary>
    [JsonPropertyName("protocol")]
    public int Protocol { get; set; }
}

/// <summary>
///     JSON converter that handles the MOTD description field being either a string or an object.
/// </summary>
public class McJavaDescriptionConverter : JsonConverter<McJavaDescription>
{
    /// <summary>
    ///     Reads and converts JSON to a <see cref="McJavaDescription" />.
    /// </summary>
    public override McJavaDescription? Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new McJavaDescription
            {
                Text = reader.GetString()
            };

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var text = doc.RootElement.TryGetProperty("text", out var textProp) ? textProp.GetString() : null;
            return new McJavaDescription
            {
                Text = text
            };
        }

        reader.Skip();
        return new McJavaDescription();
    }

    /// <summary>
    ///     Writes a <see cref="McJavaDescription" /> as JSON.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, McJavaDescription value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Text);
    }
}