using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("AchievementIconUploads")]
public class AchievementIconUpload
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("Kind")]
    public short Kind { get; set; }

    [Column("Data")]
    public byte[] Data { get; set; } = [];

    [Column("PublicUrl")]
    public string? PublicUrl { get; set; }

    [Column("UploadedBy")]
    public ulong UploadedBy { get; set; }

    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
