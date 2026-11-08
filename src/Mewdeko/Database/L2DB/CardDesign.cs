using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     A card design a community offers for the rank card or the achievement card.
/// </summary>
[Table("CardDesigns")]
public class CardDesign
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The guild this design belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Display name, unique per guild case-insensitively. Used by the equip commands.
    /// </summary>
    [Column("Name")]
    public string Name { get; set; } = null!;

    /// <summary>
    ///     The card it is drawn on. See <see cref="Mewdeko.Database.Enums.CardDesignTarget" />.
    /// </summary>
    [Column("Target")]
    public short Target { get; set; }

    /// <summary>
    ///     The design as JSON: a background reference for rank cards, a card template for achievement cards.
    /// </summary>
    [Column("Data")]
    public string Data { get; set; } = "{}";

    /// <summary>
    ///     How members get it. See <see cref="Mewdeko.Database.Enums.CardDesignAvailability" />.
    /// </summary>
    [Column("Availability")]
    public short Availability { get; set; }

    /// <summary>
    ///     What grants it when <see cref="Availability" /> is unlock. See
    ///     <see cref="Mewdeko.Database.Enums.CardDesignUnlockType" />.
    /// </summary>
    [Column("UnlockType")]
    public short? UnlockType { get; set; }

    /// <summary>
    ///     The level that grants it.
    /// </summary>
    [Column("UnlockLevel")]
    public int? UnlockLevel { get; set; }

    /// <summary>
    ///     The achievement key that grants it.
    /// </summary>
    [Column("UnlockAchievementKey")]
    public string? UnlockAchievementKey { get; set; }

    /// <summary>
    ///     The role that grants it.
    /// </summary>
    [Column("UnlockRoleId")]
    public ulong? UnlockRoleId { get; set; }

    /// <summary>
    ///     Whether it is the community default for its target.
    /// </summary>
    [Column("IsDefault")]
    public bool IsDefault { get; set; }

    /// <summary>
    ///     When it was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }

    /// <summary>
    ///     When it last changed.
    /// </summary>
    [Column("DateUpdated")]
    public DateTime DateUpdated { get; set; }

    /// <summary>
    ///     The member who made it, or null for a design the community's staff made.
    /// </summary>
    [Column("OwnerUserId")]
    public ulong? OwnerUserId { get; set; }

    /// <summary>
    ///     Where it stands with the moderators. See <see cref="Mewdeko.Database.Enums.CardDesignReview" />.
    /// </summary>
    [Column("ReviewStatus")]
    public short ReviewStatus { get; set; }

    /// <summary>
    ///     The member's uploaded background, kept until approved. Null when the design uses presets only.
    /// </summary>
    [Column("UploadId")]
    public int? UploadId { get; set; }

    /// <summary>
    ///     The reason a moderator gave when turning the upload down.
    /// </summary>
    [Column("ReviewNote")]
    public string? ReviewNote { get; set; }
}

/// <summary>
///     A community's rules for designs its members make.
/// </summary>
[Table("CardDesignSettings")]
public class CardDesignSetting
{
    /// <summary>
    ///     The guild.
    /// </summary>
    [Column("GuildId", IsPrimaryKey = true)]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Whether members can make designs at all.
    /// </summary>
    [Column("MemberEnabled")]
    public bool MemberEnabled { get; set; }

    /// <summary>
    ///     Who may. See <see cref="Mewdeko.Database.Enums.MemberDesignWho" />.
    /// </summary>
    [Column("MemberWho")]
    public short MemberWho { get; set; }

    /// <summary>
    ///     Comma separated role IDs, for the roles option.
    /// </summary>
    [Column("MemberRoleIds")]
    public string MemberRoleIds { get; set; } = "";

    /// <summary>
    ///     The level members need, for the level option.
    /// </summary>
    [Column("MemberMinLevel")]
    public int MemberMinLevel { get; set; } = 1;

    /// <summary>
    ///     Whether members may upload a background instead of picking a preset.
    /// </summary>
    [Column("MemberUploads")]
    public bool MemberUploads { get; set; }

    /// <summary>
    ///     Whether an uploaded background waits for a moderator.
    /// </summary>
    [Column("MemberReview")]
    public bool MemberReview { get; set; } = true;

    /// <summary>
    ///     What creating or updating a design costs, or 0 for free.
    /// </summary>
    [Column("MemberCost")]
    public long MemberCost { get; set; }
}

/// <summary>
///     A member owning a <see cref="CardDesign" />.
/// </summary>
[Table("CardDesignOwners")]
public class CardDesignOwner
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The guild.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The owning member.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     The design owned.
    /// </summary>
    [Column("DesignId")]
    public int DesignId { get; set; }

    /// <summary>
    ///     How they got it. See <see cref="Mewdeko.Database.Enums.CardDesignSource" />.
    /// </summary>
    [Column("Source")]
    public short Source { get; set; }

    /// <summary>
    ///     When they got it.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     The design a member has equipped for one target.
/// </summary>
[Table("EquippedCardDesigns")]
public class EquippedCardDesign
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The guild.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The member.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     The target the design is equipped for.
    /// </summary>
    [Column("Target")]
    public short Target { get; set; }

    /// <summary>
    ///     The equipped design.
    /// </summary>
    [Column("DesignId")]
    public int DesignId { get; set; }
}
