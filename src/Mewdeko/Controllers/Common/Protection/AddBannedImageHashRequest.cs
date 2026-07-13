using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.Protection;

/// <summary>
///     Request model for adding an image to the blocked image list. Supply exactly one source: an already computed hash,
///     an image URL, or the base64 bytes of an uploaded image.
/// </summary>
public class AddBannedImageHashRequest
{
    /// <summary>
    ///     A precomputed 16 character hex hash
    /// </summary>
    public string? Hash { get; set; }

    /// <summary>
    ///     A URL to hash. The bot downloads and hashes the image server side
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    ///     The raw bytes of an uploaded image, base64 encoded, optionally as a data URL
    /// </summary>
    public string? ImageBase64 { get; set; }

    /// <summary>
    ///     An optional label for the blocked image
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     The punishment action for this specific image, overriding the guild default when set
    /// </summary>
    public PunishmentAction? Action { get; set; }

    /// <summary>
    ///     The punishment duration in minutes for this specific image, overriding the guild default when set
    /// </summary>
    public int? PunishDuration { get; set; }

    /// <summary>
    ///     The role applied for this specific image when its action is AddRole
    /// </summary>
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     The Discord user ID of the dashboard user adding the hash
    /// </summary>
    public ulong? AddedBy { get; set; }
}