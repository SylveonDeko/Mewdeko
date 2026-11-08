using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("AfkInbox")]
public class AfkInboxMessage
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("RecipientId")]
    public ulong RecipientId { get; set; }

    [Column("SenderId")]
    public ulong SenderId { get; set; }

    [Column("Content")]
    public string Content { get; set; } = "";

    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}

[Table("AfkInboxBlocks")]
public class AfkInboxBlock
{
    [Column("UserId", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public ulong UserId { get; set; }

    [Column("BlockedId", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public ulong BlockedId { get; set; }
}
