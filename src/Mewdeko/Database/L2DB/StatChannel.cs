using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("StatChannels")]
public class StatChannel
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    [Column("StatType")]
    public int StatType { get; set; }

    [Column("Template")]
    public string Template { get; set; } = "{count}";

    [Column("RoleId")]
    public ulong? RoleId { get; set; }

    [Column("CountdownDate")]
    public DateTime? CountdownDate { get; set; }

    [Column("GoalTarget")]
    public int GoalTarget { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }

    /// <summary>
    ///     The <see cref="Mewdeko.Modules.StatChannels.Common.StatChannelDisplayStyle" /> applied to the resolved number.
    /// </summary>
    [Column("DisplayStyle")]
    public int DisplayStyle { get; set; } = 1;

    /// <summary>
    ///     JSON serialized style options (bar width, glyphs, decimals, boolean text).
    /// </summary>
    [Column("StyleOptions")]
    public string? StyleOptions { get; set; }

    /// <summary>
    ///     The <see cref="Mewdeko.Modules.StatChannels.Common.StatChannelUpdateMechanism" /> used to push updates.
    /// </summary>
    [Column("UpdateMechanism")]
    public int UpdateMechanism { get; set; } = 2;

    /// <summary>
    ///     How often this channel should refresh, in minutes.
    /// </summary>
    [Column("UpdateIntervalMinutes")]
    public int UpdateIntervalMinutes { get; set; } = 5;

    /// <summary>
    ///     The parent category, persisted so recreate mode can restore placement.
    /// </summary>
    [Column("CategoryId")]
    public ulong? CategoryId { get; set; }

    /// <summary>
    ///     The channel position, persisted so recreate mode can restore ordering.
    /// </summary>
    [Column("Position")]
    public int? Position { get; set; }

    /// <summary>
    ///     JSON serialized permission overwrites, persisted so recreate mode does not drop them.
    /// </summary>
    [Column("PermissionOverwrites")]
    public string? PermissionOverwrites { get; set; }

    /// <summary>
    ///     A generic snowflake target (counting channel, Minecraft server, starboard) for types that need one.
    /// </summary>
    [Column("TargetId")]
    public ulong? TargetId { get; set; }

    /// <summary>
    ///     A generic named target (Twitch counter name, Minecraft server name) for types that need one.
    /// </summary>
    [Column("TargetName")]
    public string? TargetName { get; set; }

    /// <summary>
    ///     The last rendered name, used to skip no-op updates across restarts.
    /// </summary>
    [Column("LastValue")]
    public string? LastValue { get; set; }

    /// <summary>
    ///     When this channel was last successfully updated.
    /// </summary>
    [Column("LastUpdateAt")]
    public DateTime? LastUpdateAt { get; set; }
}