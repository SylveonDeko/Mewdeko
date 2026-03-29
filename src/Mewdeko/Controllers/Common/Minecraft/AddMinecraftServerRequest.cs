namespace Mewdeko.Controllers.Common.Minecraft;

/// <summary>
///     Request model for adding a Minecraft server to a guild.
/// </summary>
public class AddMinecraftServerRequest
{
    /// <summary>
    ///     The label for the server (e.g. "survival", "creative").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     The server address (e.g. "play.example.com").
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    ///     The server port. Defaults to 25565 for Java, 19132 for Bedrock.
    /// </summary>
    public int Port { get; set; } = 25565;

    /// <summary>
    ///     The server type. 0 = Java, 1 = Bedrock.
    /// </summary>
    public int ServerType { get; set; }

    /// <summary>
    ///     The query protocol port. 0 means use the game port.
    /// </summary>
    public int QueryPort { get; set; }
}