namespace Mewdeko.Controllers.Common.Minecraft;

/// <summary>
///     Response model representing a registered Minecraft server.
/// </summary>
public class MinecraftServerResponse
{
    /// <summary>
    ///     The database ID of the server.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The server label.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     The server address.
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    ///     The server port.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    ///     The server type. 0 = Java, 1 = Bedrock.
    /// </summary>
    public int ServerType { get; set; }

    /// <summary>
    ///     The query protocol port.
    /// </summary>
    public int QueryPort { get; set; }

    /// <summary>
    ///     Whether this is the default server.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    ///     The watch channel ID, if watching is enabled.
    /// </summary>
    public ulong? WatchChannelId { get; set; }

    /// <summary>
    ///     The watch message ID for the live status embed.
    /// </summary>
    public ulong? WatchMessageId { get; set; }

    /// <summary>
    ///     The watch interval in minutes.
    /// </summary>
    public int WatchInterval { get; set; }

    /// <summary>
    ///     The watch mode. 0 = Embed, 1 = ChannelTopic, 2 = Both.
    /// </summary>
    public int WatchMode { get; set; }

    /// <summary>
    ///     The custom embed template JSON, if set.
    /// </summary>
    public string? CustomEmbedTemplate { get; set; }

    /// <summary>
    ///     The last known online state.
    /// </summary>
    public bool? LastOnline { get; set; }

    /// <summary>
    ///     The channel for chat bridge messages.
    /// </summary>
    public ulong? ChatChannelId { get; set; }

    /// <summary>
    ///     The channel for join/leave events.
    /// </summary>
    public ulong? JoinLeaveChannelId { get; set; }

    /// <summary>
    ///     The channel for death messages.
    /// </summary>
    public ulong? DeathChannelId { get; set; }

    /// <summary>
    ///     The channel for advancement messages.
    /// </summary>
    public ulong? AdvancementChannelId { get; set; }

    /// <summary>
    ///     The custom online alert message template.
    /// </summary>
    public string? CustomOnlineMessage { get; set; }

    /// <summary>
    ///     The custom offline alert message template.
    /// </summary>
    public string? CustomOfflineMessage { get; set; }

    /// <summary>
    ///     Whether RCON is enabled.
    /// </summary>
    public bool RconEnabled { get; set; }

    /// <summary>
    ///     The RCON port.
    /// </summary>
    public int RconPort { get; set; }

    /// <summary>
    ///     Whether a RCON password is configured (never exposes the actual password).
    /// </summary>
    public bool HasRconPassword { get; set; }

    /// <summary>
    ///     The event templates JSON for customizing bridge event messages.
    /// </summary>
    public string? EventTemplates { get; set; }

    /// <summary>
    ///     Whether a plugin API key is configured.
    /// </summary>
    public bool HasPluginKey { get; set; }

    /// <summary>
    ///     When the server was added.
    /// </summary>
    public DateTime? DateAdded { get; set; }
}