using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A saved "send as" identity for the embed builder: a display name and avatar a message can be
///     delivered under via webhook. Personal when GuildId is null, otherwise shared with the guild.
/// </summary>
[Table("EmbedWebhookPersonas")]
public class EmbedWebhookPersona
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; } // integer

    [Column("GuildId")]
    public ulong? GuildId { get; set; } // numeric(20,0)

    [Column("UserId")]
    public ulong UserId { get; set; } // numeric(20,0)

    [Column("Name", CanBeNull = false)]
    public string Name { get; set; } = null!; // text

    [Column("AvatarUrl")]
    public string? AvatarUrl { get; set; } // text

    [Column("AvatarData")]
    public byte[]? AvatarData { get; set; } // bytea

    [Column("AvatarVersion")]
    public int AvatarVersion { get; set; } // integer

    [Column("IsGuildShared")]
    public bool IsGuildShared { get; set; } // boolean

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; } // timestamp (6) without time zone
}