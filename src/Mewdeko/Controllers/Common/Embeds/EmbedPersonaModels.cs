namespace Mewdeko.Controllers.Common.Embeds;

/// <summary>
///     Response model for a saved "send as" persona. The avatar bytes themselves are never returned;
///     <see cref="HasUploadedAvatar" /> reports whether one is stored.
/// </summary>
public class EmbedPersonaResponse
{
    /// <summary>
    ///     The database ID of the persona.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The display name messages are sent under.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    ///     The avatar URL, when the persona uses one.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    ///     Whether the persona has an uploaded avatar image stored.
    /// </summary>
    public bool HasUploadedAvatar { get; set; }

    /// <summary>
    ///     The ID of the user who created the persona, and the owner of a personal one.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The guild the persona is shared with, or null for a personal persona.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    ///     Whether the persona is shared with the guild rather than personal.
    /// </summary>
    public bool IsGuildShared { get; set; }

    /// <summary>
    ///     When the persona was created.
    /// </summary>
    public DateTime? DateAdded { get; set; }
}

/// <summary>
///     Request model for creating a "send as" persona.
/// </summary>
public class CreateEmbedPersonaRequest
{
    /// <summary>
    ///     The ID of the user creating the persona.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The guild to share the persona with. Null for a personal persona.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    ///     The display name messages are sent under.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    ///     An avatar URL. Ignored when <see cref="AvatarData" /> is supplied.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    ///     An uploaded avatar image, either raw base64 or a <c>data:image/...;base64,</c> URI.
    /// </summary>
    public string? AvatarData { get; set; }

    /// <summary>
    ///     Whether the persona should be shared with the guild rather than kept personal.
    /// </summary>
    public bool IsGuildShared { get; set; }
}

/// <summary>
///     Request model for updating a "send as" persona.
/// </summary>
public class UpdateEmbedPersonaRequest
{
    /// <summary>
    ///     The ID of the user requesting the update, used for ownership verification.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The new display name, or null to leave unchanged.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     A new avatar URL, or null to leave unchanged.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    ///     A new uploaded avatar, or null to leave unchanged. Takes precedence over <see cref="AvatarUrl" />.
    /// </summary>
    public string? AvatarData { get; set; }

    /// <summary>
    ///     Whether to remove the persona's avatar entirely, ignoring the two avatar fields.
    /// </summary>
    public bool ClearAvatar { get; set; }
}