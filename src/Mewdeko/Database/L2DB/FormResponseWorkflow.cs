using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Represents the approval/rejection workflow for a form response
/// </summary>
[Table("form_response_workflows")]
public class FormResponseWorkflow
{
    [Column("id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("response_id", CanBeNull = false)]
    public int ResponseId { get; set; }

    [Column("status", CanBeNull = false)]
    public int Status { get; set; }

    [Column("reviewed_by")]
    public ulong? ReviewedBy { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("review_notes")]
    public string? ReviewNotes { get; set; }

    [Column("action_taken", CanBeNull = false)]
    public int ActionTaken { get; set; }

    [Column("invite_code")]
    public string? InviteCode { get; set; }

    [Column("invite_expires_at")]
    public DateTime? InviteExpiresAt { get; set; }

    [Column("status_check_token", CanBeNull = false)]
    public string StatusCheckToken { get; set; } = null!;

    [Column("created_at", CanBeNull = false)]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", CanBeNull = false)]
    public DateTime UpdatedAt { get; set; }
}