using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Represents a shareable link for a form with instance routing
/// </summary>
[Table("form_share_links")]
public class FormShareLink
{
    [Column("id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("share_code", CanBeNull = false)]
    public string ShareCode { get; set; } = null!;

    [Column("form_id", CanBeNull = false)]
    public int FormId { get; set; }

    [Column("instance_identifier", CanBeNull = false)]
    public string InstanceIdentifier { get; set; } = null!;

    [Column("created_at", CanBeNull = false)]
    public DateTime CreatedAt { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("is_active", CanBeNull = false)]
    public bool IsActive { get; set; }
}