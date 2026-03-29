namespace Mewdeko.Controllers.Common.Minecraft;

/// <summary>
///     Response model representing a live Minecraft server status query result.
/// </summary>
public class MinecraftStatusResponse
{
    /// <summary>
    ///     Whether the server is online.
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    ///     The server's Message of the Day.
    /// </summary>
    public string Motd { get; set; } = "";

    /// <summary>
    ///     The number of players currently online.
    /// </summary>
    public int PlayersOnline { get; set; }

    /// <summary>
    ///     The maximum number of players.
    /// </summary>
    public int PlayersMax { get; set; }

    /// <summary>
    ///     The list of online player names.
    /// </summary>
    public List<string> PlayerList { get; set; } = [];

    /// <summary>
    ///     Mapping of player names to UUIDs (when available).
    /// </summary>
    public Dictionary<string, string> PlayerUuids { get; set; } = new();

    /// <summary>
    ///     The server version string.
    /// </summary>
    public string Version { get; set; } = "";

    /// <summary>
    ///     The ping latency in milliseconds.
    /// </summary>
    public int Latency { get; set; }

    /// <summary>
    ///     The map name (available via Query protocol).
    /// </summary>
    public string? Map { get; set; }

    /// <summary>
    ///     The game mode (available via Query protocol).
    /// </summary>
    public string? GameMode { get; set; }

    /// <summary>
    ///     The server software (available via Query protocol).
    /// </summary>
    public string? Software { get; set; }

    /// <summary>
    ///     The list of plugins (available via Query protocol).
    /// </summary>
    public List<string> Plugins { get; set; } = [];

    /// <summary>
    ///     Whether the data was retrieved via the Query protocol.
    /// </summary>
    public bool IsQueryResponse { get; set; }
}