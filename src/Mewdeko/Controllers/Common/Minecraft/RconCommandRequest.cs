namespace Mewdeko.Controllers.Common.Minecraft;

/// <summary>
///     Request model for sending an RCON command.
/// </summary>
public class RconCommandRequest
{
    /// <summary>
    ///     The command to execute.
    /// </summary>
    public string Command { get; set; } = string.Empty;
}