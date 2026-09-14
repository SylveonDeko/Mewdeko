using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     One dashboard request, reported by the dashboard's beacon.
/// </summary>
[Table("AnalyticsPageView")]
public class AnalyticsPageView
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public long Id { get; set; }

    /// <summary>
    ///     When the request finished, UTC.
    /// </summary>
    [Column("At")]
    public DateTime At { get; set; }

    /// <summary>
    ///     The route template, never the concrete path.
    /// </summary>
    [Column("Route")]
    public string Route { get; set; } = string.Empty;

    /// <summary>
    ///     The HTTP method.
    /// </summary>
    [Column("Method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    ///     The response status code.
    /// </summary>
    [Column("Status")]
    public short Status { get; set; }

    /// <summary>
    ///     Server time to respond in milliseconds.
    /// </summary>
    [Column("DurationMs")]
    public int DurationMs { get; set; }

    /// <summary>
    ///     Daily salted hash identifying a visitor without identifying a user.
    /// </summary>
    [Column("VisitorHash")]
    public string? VisitorHash { get; set; }

    /// <summary>
    ///     The visitor's locale.
    /// </summary>
    [Column("Locale")]
    public string? Locale { get; set; }

    /// <summary>
    ///     desktop, mobile, tablet or bot.
    /// </summary>
    [Column("Device")]
    public string? Device { get; set; }

    /// <summary>
    ///     Member count of the guild being viewed, when any.
    /// </summary>
    [Column("GuildSize")]
    public int? GuildSize { get; set; }

    /// <summary>
    ///     Whether the visitor is a bot owner.
    /// </summary>
    [Column("IsOwner")]
    public bool IsOwner { get; set; }
}