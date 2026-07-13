using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.Protection;

/// <summary>
///     Request model for anti-image-hash configuration
/// </summary>
public class AntiImageHashConfigRequest
{
    /// <summary>
    ///     Whether anti-image-hash protection should be enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The default punishment action, which individual blocked images may override
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    ///     The duration of the punishment in minutes
    /// </summary>
    public int PunishDuration { get; set; }

    /// <summary>
    ///     The ID of the role to be added as punishment, if applicable
    /// </summary>
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     How many of the 256 PDQ hash bits may differ for an image to still count as a match, from 0 to 64. PDQ's standard
    ///     "same image" threshold is 31
    /// </summary>
    public int HashThreshold { get; set; } = 31;

    /// <summary>
    ///     Whether to delete the offending message
    /// </summary>
    public bool DeleteMessages { get; set; } = true;

    /// <summary>
    ///     Whether to notify the user via DM
    /// </summary>
    public bool NotifyUser { get; set; } = true;

    /// <summary>
    ///     Whether to ignore bot messages
    /// </summary>
    public bool IgnoreBots { get; set; } = true;

    /// <summary>
    ///     Whether to check images inside embeds as well as attachments
    /// </summary>
    public bool CheckEmbeds { get; set; } = true;

    /// <summary>
    ///     Whether to strip a solid border from posted images before matching, which catches a blocked image that has been
    ///     wrapped in a frame to disguise it
    /// </summary>
    public bool CheckBorders { get; set; } = true;

    /// <summary>
    ///     Whether to also block the known scam images that ship with the bot
    /// </summary>
    public bool UsePresetList { get; set; }

    /// <summary>
    ///     The maximum image size to download and hash, in megabytes
    /// </summary>
    public int MaxImageSizeMb { get; set; } = 8;
}