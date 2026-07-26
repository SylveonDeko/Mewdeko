namespace Mewdeko.Controllers.Common.Embeds;

/// <summary>
///     Response model for a saved embed template.
/// </summary>
public class EmbedResponse
{
    /// <summary>
    ///     The database ID of the embed template.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The name of the embed template.
    /// </summary>
    public string? EmbedName { get; set; }

    /// <summary>
    ///     The JSON representation of the embed.
    /// </summary>
    public string JsonCode { get; set; } = null!;

    /// <summary>
    ///     The ID of the user who created the embed template.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     When the embed template was created.
    /// </summary>
    public DateTime? DateAdded { get; set; }

    /// <summary>
    ///     The ID of the guild the embed template is shared with, if any.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    ///     Whether this embed template is shared with the guild.
    /// </summary>
    public bool IsGuildShared { get; set; }
}