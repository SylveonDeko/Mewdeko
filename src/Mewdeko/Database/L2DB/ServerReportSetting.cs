using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Where and how often a guild receives its growth and activity digest.
/// </summary>
[Table("ServerReportSettings")]
public class ServerReportSetting
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("ChannelId")]
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     The <see cref="Mewdeko.Modules.ServerStats.Common.ReportFrequency" />.
    /// </summary>
    [Column("Frequency")]
    public int Frequency { get; set; } = 1;

    [Column("Enabled")]
    public bool Enabled { get; set; }

    [Column("LastSentAt")]
    public DateTime? LastSentAt { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}