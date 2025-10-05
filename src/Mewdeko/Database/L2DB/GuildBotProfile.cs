using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Stores bot's guild-specific profile customizations (avatar, banner, bio)
/// </summary>
[Table("GuildBotProfiles")]
public class GuildBotProfile
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("AvatarUrl")]
    public string? AvatarUrl { get; set; }

    [Column("BannerUrl")]
    public string? BannerUrl { get; set; }

    [Column("Bio")]
    public string? Bio { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }

    [Column("DateUpdated")]
    public DateTime? DateUpdated { get; set; }
}