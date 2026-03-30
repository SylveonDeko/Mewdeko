using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Minecraft.Common;

/// <summary>
///     Base class for all WebSocket messages between the bot and the companion plugin.
/// </summary>
public class BridgeMessage
{
    /// <summary>
    ///     Gets or sets the message type identifier.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>
    ///     Gets or sets the UTC timestamp of the message.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
///     Sent by the plugin when a player joins the server.
/// </summary>
public class PlayerJoinMessage : BridgeMessage
{
    /// <summary>
    ///     Gets or sets the player's username.
    /// </summary>
    [JsonPropertyName("player")]
    public string Player { get; set; } = "";

    /// <summary>
    ///     Gets or sets the player's UUID.
    /// </summary>
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";
}

/// <summary>
///     Sent by the plugin when a player leaves the server.
/// </summary>
public class PlayerLeaveMessage : BridgeMessage
{
    /// <summary>
    ///     Gets or sets the player's username.
    /// </summary>
    [JsonPropertyName("player")]
    public string Player { get; set; } = "";

    /// <summary>
    ///     Gets or sets the player's UUID.
    /// </summary>
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";
}

/// <summary>
///     Sent by the plugin when a chat message is sent in-game.
/// </summary>
public class ChatMessage : BridgeMessage
{
    /// <summary>
    ///     Gets or sets the player's username.
    /// </summary>
    [JsonPropertyName("player")]
    public string Player { get; set; } = "";

    /// <summary>
    ///     Gets or sets the chat message content.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

/// <summary>
///     Sent by the plugin when a player dies.
/// </summary>
public class DeathMessage : BridgeMessage
{
    /// <summary>
    ///     Gets or sets the player's username.
    /// </summary>
    [JsonPropertyName("player")]
    public string Player { get; set; } = "";

    /// <summary>
    ///     Gets or sets the death message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

/// <summary>
///     Sent by the plugin when a player earns an advancement.
/// </summary>
public class AdvancementMessage : BridgeMessage
{
    /// <summary>
    ///     Gets or sets the player's username.
    /// </summary>
    [JsonPropertyName("player")]
    public string Player { get; set; } = "";

    /// <summary>
    ///     Gets or sets the advancement title.
    /// </summary>
    [JsonPropertyName("advancement")]
    public string Advancement { get; set; } = "";
}

/// <summary>
///     Sent by the plugin periodically with server performance metrics.
/// </summary>
public class ServerStatusMessage : BridgeMessage
{
    /// <summary>
    ///     Gets or sets the server TPS (ticks per second).
    /// </summary>
    [JsonPropertyName("tps")]
    public double Tps { get; set; }

    /// <summary>
    ///     Gets or sets the used memory in MB.
    /// </summary>
    [JsonPropertyName("usedMemory")]
    public long UsedMemory { get; set; }

    /// <summary>
    ///     Gets or sets the max memory in MB.
    /// </summary>
    [JsonPropertyName("maxMemory")]
    public long MaxMemory { get; set; }

    /// <summary>
    ///     Gets or sets the list of online players with their UUIDs.
    /// </summary>
    [JsonPropertyName("players")]
    public List<BridgePlayer> Players { get; set; } = [];

    /// <summary>
    ///     Gets or sets the server uptime in seconds.
    /// </summary>
    [JsonPropertyName("uptime")]
    public long Uptime { get; set; }
}

/// <summary>
///     Represents a player in a bridge status message.
/// </summary>
public class BridgePlayer
{
    /// <summary>
    ///     Gets or sets the player's username.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the player's UUID.
    /// </summary>
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";
}

/// <summary>
///     Sent by the plugin with console log lines.
/// </summary>
public class ConsoleLogMessage : BridgeMessage
{
    /// <summary>
    ///     Gets or sets the log line.
    /// </summary>
    [JsonPropertyName("line")]
    public string Line { get; set; } = "";
}

/// <summary>
///     Sent by the plugin in response to a command request.
/// </summary>
public class CommandResponseMessage : BridgeMessage
{
    /// <summary>
    ///     Gets or sets the request ID this response corresponds to.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    ///     Gets or sets the command response text.
    /// </summary>
    [JsonPropertyName("response")]
    public string Response { get; set; } = "";
}

/// <summary>
///     Sent by the bot to relay a Discord chat message to the MC server.
/// </summary>
public class DiscordChatMessage : BridgeMessage
{
    /// <summary>
    ///     Gets or sets the Discord user's display name.
    /// </summary>
    [JsonPropertyName("user")]
    public string User { get; set; } = "";

    /// <summary>
    ///     Gets or sets the message content.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    /// <summary>
    ///     Gets or sets the Discord channel name.
    /// </summary>
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "";
}

/// <summary>
///     Sent by the bot to execute a command on the MC server via the plugin.
/// </summary>
public class BridgeCommandMessage : BridgeMessage
{
    /// <summary>
    ///     Gets or sets the unique request ID for correlating responses.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    ///     Gets or sets the command to execute.
    /// </summary>
    [JsonPropertyName("command")]
    public string Command { get; set; } = "";
}

/// <summary>
///     Sent by the bot to broadcast a message to all players.
/// </summary>
public class BroadcastMessage : BridgeMessage
{
    /// <summary>
    ///     Gets or sets the message to broadcast.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}