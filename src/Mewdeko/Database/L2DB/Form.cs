using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Represents a custom form created by a guild
/// </summary>
[Table("forms")]
public class Form
{
    [Column("id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("guild_id", CanBeNull = false)]
    public ulong GuildId { get; set; }

    [Column("name", CanBeNull = false)]
    public string Name { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("submit_channel_id")]
    public ulong? SubmitChannelId { get; set; }

    [Column("allow_multiple_submissions", CanBeNull = false)]
    public bool AllowMultipleSubmissions { get; set; }

    [Column("max_responses")]
    public int? MaxResponses { get; set; }

    [Column("require_captcha", CanBeNull = false)]
    public bool RequireCaptcha { get; set; }

    [Column("is_active", CanBeNull = false)]
    public bool IsActive { get; set; }

    [Column("is_draft", CanBeNull = false)]
    public bool IsDraft { get; set; }

    [Column("allow_anonymous", CanBeNull = false)]
    public bool AllowAnonymous { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("required_role_id")]
    public ulong? RequiredRoleId { get; set; }

    [Column("success_message")]
    public string? SuccessMessage { get; set; }

    [Column("form_type", CanBeNull = false)]
    public int FormType { get; set; }

    [Column("allow_external_users", CanBeNull = false)]
    public bool AllowExternalUsers { get; set; }

    [Column("auto_approve_role_ids")]
    public string? AutoApproveRoleIds { get; set; }

    /// <summary>
    ///     Whether form submissions require manual approval
    /// </summary>
    [Column("require_approval", CanBeNull = false)]
    public bool RequireApproval { get; set; }

    /// <summary>
    ///     Action type to perform when a submission is approved (0 = None, 1 = AddRole, 2 = RemoveRole)
    /// </summary>
    [Column("approval_action_type", CanBeNull = false)]
    public int ApprovalActionType { get; set; }

    /// <summary>
    ///     Comma-separated list of role IDs to add/remove when a submission is approved
    /// </summary>
    [Column("approval_role_ids")]
    public string? ApprovalRoleIds { get; set; }

    /// <summary>
    ///     Action type to perform when a submission is rejected (0 = None, 1 = AddRole, 2 = RemoveRole)
    /// </summary>
    [Column("rejection_action_type", CanBeNull = false)]
    public int RejectionActionType { get; set; }

    /// <summary>
    ///     Comma-separated list of role IDs to add/remove when a submission is rejected
    /// </summary>
    [Column("rejection_role_ids")]
    public string? RejectionRoleIds { get; set; }

    [Column("invite_max_uses")]
    public int? InviteMaxUses { get; set; }

    [Column("invite_max_age")]
    public int? InviteMaxAge { get; set; }

    [Column("notification_webhook_url")]
    public string? NotificationWebhookUrl { get; set; }

    [Column("created_by", CanBeNull = false)]
    public ulong CreatedBy { get; set; }

    [Column("created_at", CanBeNull = false)]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", CanBeNull = false)]
    public DateTime UpdatedAt { get; set; }
}