using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Guild wide defaults applied to newly created stat channels.
/// </summary>
[Table("StatChannelSettings")]
public class StatChannelSetting
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Default update mechanism for new stat channels.
    /// </summary>
    [Column("DefaultMechanism")]
    public int DefaultMechanism { get; set; } = 2;

    /// <summary>
    ///     Default refresh interval in minutes for new stat channels.
    /// </summary>
    [Column("DefaultIntervalMinutes")]
    public int DefaultIntervalMinutes { get; set; } = 5;

    /// <summary>
    ///     Default number display style for new stat channels.
    /// </summary>
    [Column("DefaultDisplayStyle")]
    public int DefaultDisplayStyle { get; set; } = 1;

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}