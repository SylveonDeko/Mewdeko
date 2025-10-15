namespace Mewdeko.Controllers.Common.ClientOperations;

/// <summary>
///     Information about guild emojis for the emoji picker
/// </summary>
public class GuildEmojiInfo
{
    /// <summary>
    ///     The guild these emojis belong to
    /// </summary>
    public GuildInfo Guild { get; set; } = new();

    /// <summary>
    ///     List of emojis in this guild
    /// </summary>
    public List<EmojiInfo> Emojis { get; set; } = new();
}

/// <summary>
///     Basic guild information
/// </summary>
public class GuildInfo
{
    /// <summary>
    ///     The guild ID
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     The guild name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     The guild icon URL
    /// </summary>
    public string? IconUrl { get; set; }
}

/// <summary>
///     Information about a guild emoji
/// </summary>
public class EmojiInfo
{
    /// <summary>
    ///     The emoji ID
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     The emoji name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Whether the emoji is animated
    /// </summary>
    public bool Animated { get; set; }

    /// <summary>
    ///     Whether the emoji is available (not disabled)
    /// </summary>
    public bool? IsAvailable { get; set; }

    /// <summary>
    ///     Role IDs that are allowed to use this emoji
    /// </summary>
    public List<ulong> RoleIds { get; set; } = new();

    /// <summary>
    ///     Whether the emoji requires colons
    /// </summary>
    public bool RequireColons { get; set; }

    /// <summary>
    ///     The CDN URL for the emoji
    /// </summary>
    public string Url { get; set; } = string.Empty;
}