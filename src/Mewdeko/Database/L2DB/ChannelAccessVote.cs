using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("ChannelAccessVotes")]
public class ChannelAccessVote
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; } // integer

    [Column("ApplicationId")]
    public int ApplicationId { get; set; } // integer

    [Column("UserId")]
    public ulong UserId { get; set; } // numeric(20,0)

    [Column("Vote")]
    public int Vote { get; set; } // integer

    [Column("Comment")]
    public string? Comment { get; set; } // text

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; } // timestamp (6) without time zone
}