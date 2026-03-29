namespace Mewdeko.Controllers.Common.Minecraft;

/// <summary>
///     Request model for configuring RCON on a Minecraft server.
/// </summary>
public class SetRconConfigRequest
{
    /// <summary>
    ///     Whether RCON is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The RCON port. 0 means use default (25575).
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    ///     The RCON password.
    /// </summary>
    public string? Password { get; set; }
}