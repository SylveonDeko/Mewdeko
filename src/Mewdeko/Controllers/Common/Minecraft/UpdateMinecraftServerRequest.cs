namespace Mewdeko.Controllers.Common.Minecraft;

/// <summary>
///     Request model for updating a Minecraft server's configuration.
/// </summary>
public class UpdateMinecraftServerRequest
{
    /// <summary>
    ///     The server address.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    ///     The server port.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    ///     The server type. 0 = Java, 1 = Bedrock.
    /// </summary>
    public int? ServerType { get; set; }

    /// <summary>
    ///     The query protocol port. 0 means use the game port.
    /// </summary>
    public int? QueryPort { get; set; }

    /// <summary>
    ///     Whether this is the default server.
    /// </summary>
    public bool? IsDefault { get; set; }

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
}