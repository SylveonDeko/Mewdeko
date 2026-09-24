namespace Mewdeko.Controllers.Common.Guild;

/// <summary>
///     Essential guild information model for dashboard
/// </summary>
public class GuildInfoModel
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
    ///     The guild icon hash
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    ///     The full guild icon URL
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    ///     The guild banner hash
    /// </summary>
    public string? Banner { get; set; }

    /// <summary>
    ///     The full guild banner URL
    /// </summary>
    public string? BannerUrl { get; set; }

    /// <summary>
    ///     The invite splash image URL, usable as a banner fallback
    /// </summary>
    public string? SplashUrl { get; set; }

    /// <summary>
    ///     The discovery splash image URL, usable as a banner fallback
    /// </summary>
    public string? DiscoverySplashUrl { get; set; }

    /// <summary>
    ///     The guild description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Total member count
    /// </summary>
    public int MemberCount { get; set; }

    /// <summary>
    ///     Premium tier (boost level)
    /// </summary>
    public int PremiumTier { get; set; }

    /// <summary>
    ///     Guild owner ID
    /// </summary>
    public ulong OwnerId { get; set; }

    /// <summary>
    ///     When the guild was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}