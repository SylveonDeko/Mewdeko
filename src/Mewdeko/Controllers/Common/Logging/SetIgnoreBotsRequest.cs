namespace Mewdeko.Controllers.Common.Logging;

/// <summary>
///     Request model for setting whether logs skip actions by bots
/// </summary>
public class SetIgnoreBotsRequest
{
    /// <summary>
    ///     Whether logs skip bots
    /// </summary>
    public bool IgnoreBots { get; set; } = true;
}
