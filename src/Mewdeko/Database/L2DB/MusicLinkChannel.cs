using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("MusicLinkChannels")]
public class MusicLinkChannel
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}