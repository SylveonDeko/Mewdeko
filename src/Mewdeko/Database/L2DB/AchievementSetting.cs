using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("AchievementSettings")]
public class AchievementSetting
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("Enabled")]
    public bool Enabled { get; set; }

    [Column("AnnounceMode")]
    public int AnnounceMode { get; set; }

    [Column("LogChannelId")]
    public ulong? LogChannelId { get; set; }

    [Column("DmByDefault")]
    public bool DmByDefault { get; set; }

    [Column("MentionUsers")]
    public bool MentionUsers { get; set; } = true;

    [Column("UnlockMessage")]
    public string? UnlockMessage { get; set; }

    [Column("XpPerPoint")]
    public int XpPerPoint { get; set; }

    [Column("RevealHidden")]
    public bool RevealHidden { get; set; }

    [Column("UnlockImage")]
    public bool UnlockImage { get; set; } = true;

    [Column("DeleteAfter")]
    public int DeleteAfter { get; set; }

    [Column("DefaultCardId")]
    public int? DefaultCardId { get; set; }

    [Column("CardAssignments")]
    public string CardAssignments { get; set; } = "";

    [Column("DisabledCategories")]
    public string DisabledCategories { get; set; } = "";

    [Column("CategoryOrder")]
    public string CategoryOrder { get; set; } = "";

    [Column("ExcludedRoleIds")]
    public string ExcludedRoleIds { get; set; } = "";

    [Column("ExcludedChannelIds")]
    public string ExcludedChannelIds { get; set; } = "";

    [Column("BackfilledAt")]
    public DateTime? BackfilledAt { get; set; }

    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
