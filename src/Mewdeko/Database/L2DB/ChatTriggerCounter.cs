using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel
{
    [Table("ChatTriggerCounters")]
    public class ChatTriggerCounter
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
        public int Id { get; set; } // integer

        [Column("GuildId")]
        public ulong GuildId { get; set; } // numeric(20,0)

        [Column("Name")]
        public string Name { get; set; } = null!; // text

        [Column("UserId")]
        public ulong UserId { get; set; } // numeric(20,0)

        [Column("Value")]
        public long Value { get; set; } // bigint

        [Column("DateAdded")]
        public DateTime? DateAdded { get; set; } // timestamp (6) without time zone
    }
}