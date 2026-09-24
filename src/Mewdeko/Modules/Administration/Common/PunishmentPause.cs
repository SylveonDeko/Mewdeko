namespace Mewdeko.Modules.Administration.Common;

/// <summary>
///     Records that automod punishments are paused for a guild because the bot could not apply one.
/// </summary>
public class PunishmentPause
{
    /// <summary>
    ///     Why the punishment could not be applied, in plain words.
    /// </summary>
    public string Reason { get; set; } = "";

    /// <summary>
    ///     The punishment that failed.
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    ///     The protection that triggered the failed punishment.
    /// </summary>
    public ProtectionType Type { get; set; }

    /// <summary>
    ///     When the pause began.
    /// </summary>
    public DateTime Since { get; set; }

    /// <summary>
    ///     When the next trigger may re-check permissions and resume.
    /// </summary>
    public DateTime RetryAfter { get; set; }
}
