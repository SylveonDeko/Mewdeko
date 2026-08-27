namespace Mewdeko.Modules.StatChannels.Common;

/// <summary>
///     How a stat channel pushes a new name to Discord.
/// </summary>
public enum StatChannelUpdateMechanism
{
    /// <summary>
    ///     Edit the existing channel name. Discord allows two name edits per ten minutes per channel, so this is the
    ///     safest option but caps the effective refresh rate at once every five minutes.
    /// </summary>
    Rename = 0,

    /// <summary>
    ///     Delete the channel and recreate it with the new name. This uses the far more permissive guild channel bucket
    ///     so it can refresh much faster, at the cost of a changing channel ID and two audit log entries per update.
    /// </summary>
    Recreate = 1,

    /// <summary>
    ///     Rename while the per-channel rename budget allows it, and fall back to recreating only when an update is due
    ///     sooner than a rename can deliver it.
    /// </summary>
    Auto = 2
}