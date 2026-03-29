namespace Mewdeko.Controllers.Common.Minecraft;

/// <summary>
///     Request model for setting a custom embed template on a watched server.
/// </summary>
public class SetCustomEmbedRequest
{
    /// <summary>
    ///     The embed template JSON string. Null to reset to default.
    /// </summary>
    public string? Template { get; set; }
}