namespace Mewdeko.Controllers.Common.Embeds;

/// <summary>
///     Request model for creating a saved embed template.
/// </summary>
public class CreateEmbedRequest
{
    /// <summary>
    ///     The ID of the user creating the embed template.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The ID of the guild to share the embed with. Null for a personal embed.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    ///     The name of the embed template.
    /// </summary>
    public string EmbedName { get; set; } = null!;

    /// <summary>
    ///     The JSON representation of the embed.
    /// </summary>
    public string JsonCode { get; set; } = null!;

    /// <summary>
    ///     Whether this embed should be saved as a guild-shared template rather than a personal one.
    /// </summary>
    public bool IsGuildShared { get; set; }
}

/// <summary>
///     Request model for updating a saved embed template.
/// </summary>
public class UpdateEmbedRequest
{
    /// <summary>
    ///     The ID of the user requesting the update, used for ownership verification.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The new name for the embed template, or null to leave unchanged.
    /// </summary>
    public string? EmbedName { get; set; }

    /// <summary>
    ///     The new JSON representation of the embed, or null to leave unchanged.
    /// </summary>
    public string? JsonCode { get; set; }
}