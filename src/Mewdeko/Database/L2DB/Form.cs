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

    /// <summary>
    ///     When the form begins accepting responses. Null means it opens the moment it is published.
    /// </summary>
    [Column("opens_at")]
    public DateTime? OpensAt { get; set; }

    /// <summary>
    ///     Channel the launch announcement is posted to once <see cref="OpensAt" /> passes.
    /// </summary>
    [Column("announce_channel_id")]
    public ulong? AnnounceChannelId { get; set; }

    /// <summary>
    ///     Role pinged by the launch announcement.
    /// </summary>
    [Column("announce_role_id")]
    public ulong? AnnounceRoleId { get; set; }

    /// <summary>
    ///     Body of the launch announcement.
    /// </summary>
    [Column("announce_message")]
    public string? AnnounceMessage { get; set; }

    /// <summary>
    ///     Stamped just before the launch announcement is sent, so a send that fails is not retried forever.
    /// </summary>
    [Column("announced_at")]
    public DateTime? AnnouncedAt { get; set; }

    /// <summary>
    ///     Role pinged when a response arrives in the submit channel.
    /// </summary>
    [Column("notify_role_id")]
    public ulong? NotifyRoleId { get; set; }

    /// <summary>
    ///     Comma separated roles granted the moment a response is submitted.
    /// </summary>
    [Column("submit_role_ids")]
    public string? SubmitRoleIds { get; set; }

    /// <summary>
    ///     Role held while a response awaits review, removed once it is decided.
    /// </summary>
    [Column("pending_role_id")]
    public ulong? PendingRoleId { get; set; }

    /// <summary>
    ///     Role permitted to decide this form's responses from the buttons posted in Discord. Null
    ///     falls back to requiring Manage Server, so the buttons are never open to everyone.
    /// </summary>
    [Column("reviewer_role_id")]
    public ulong? ReviewerRoleId { get; set; }

    /// <summary>
    ///     Emote on this form's approve button. Null falls back to the guild's default.
    /// </summary>
    [Column("approve_emote")]
    public string? ApproveEmote { get; set; }

    /// <summary>
    ///     Emote on this form's reject button. Null falls back to the guild's default.
    /// </summary>
    [Column("reject_emote")]
    public string? RejectEmote { get; set; }

    /// <summary>
    ///     Minimum age of the submitter's Discord account, in days.
    /// </summary>
    [Column("min_account_age_days")]
    public int? MinAccountAgeDays { get; set; }

    /// <summary>
    ///     Lets a rejected submitter try again without opening the form to unlimited submissions.
    /// </summary>
    [Column("allow_resubmit_after_rejection", CanBeNull = false)]
    public bool AllowResubmitAfterRejection { get; set; }

    /// <summary>
    ///     Whether a single rejection ends a submitter's ability to appeal.
    /// </summary>
    [Column("block_reappeal_after_rejection", CanBeNull = false)]
    public bool BlockReappealAfterRejection { get; set; }

    /// <summary>
    ///     How many rejected appeals a submitter may accumulate before being locked out.
    /// </summary>
    [Column("max_appeal_attempts")]
    public int? MaxAppealAttempts { get; set; }

    /// <summary>
    ///     Days a submitter must wait after a rejection before appealing again.
    /// </summary>
    [Column("reappeal_cooldown_days")]
    public int? ReappealCooldownDays { get; set; }

    /// <summary>
    ///     Days after the ban before a first appeal may be filed.
    /// </summary>
    [Column("appeal_delay_days")]
    public int? AppealDelayDays { get; set; }

    /// <summary>
    ///     Comma separated roles added on approval, applied alongside <see cref="ApprovalRemoveRoleIds" />.
    /// </summary>
    [Column("approval_add_role_ids")]
    public string? ApprovalAddRoleIds { get; set; }

    /// <summary>
    ///     Comma separated roles removed on approval.
    /// </summary>
    [Column("approval_remove_role_ids")]
    public string? ApprovalRemoveRoleIds { get; set; }

    /// <summary>
    ///     Comma separated roles added on rejection.
    /// </summary>
    [Column("rejection_add_role_ids")]
    public string? RejectionAddRoleIds { get; set; }

    /// <summary>
    ///     Comma separated roles removed on rejection.
    /// </summary>
    [Column("rejection_remove_role_ids")]
    public string? RejectionRemoveRoleIds { get; set; }

    [Column("created_by", CanBeNull = false)]
    public ulong CreatedBy { get; set; }

    [Column("created_at", CanBeNull = false)]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", CanBeNull = false)]
    public DateTime UpdatedAt { get; set; }
}