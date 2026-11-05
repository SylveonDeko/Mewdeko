namespace Mewdeko.Controllers.Common.ChatTriggers;

/// <summary>
///     Request model for changing whether members may manage their own chat triggers.
/// </summary>
public class MemberCreateRequest
{
    /// <summary>
    ///     Whether members without staff permissions may create and edit their own triggers.
    /// </summary>
    public bool Enabled { get; set; }
}
