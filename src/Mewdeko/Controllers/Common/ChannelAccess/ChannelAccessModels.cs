namespace Mewdeko.Controllers.Common.ChannelAccess;

/// <summary>
///     A configured access gate on a locked channel.
/// </summary>
public class ChannelAccessGateResponse
{
    /// <summary>The gate id.</summary>
    public int Id { get; set; }

    /// <summary>The gated channel.</summary>
    public ulong ChannelId { get; set; }

    /// <summary>The role granted when an application passes, or null on a direct access gate.</summary>
    public ulong? AccessRoleId { get; set; }

    /// <summary>How approved applicants get in. 0 grants a role, 1 adds them to the channel directly.</summary>
    public int GrantMode { get; set; }

    /// <summary>Where applications get posted for voting, or null for the gated channel itself.</summary>
    public ulong? ReviewChannelId { get; set; }

    /// <summary>Where decisions get logged, if anywhere.</summary>
    public ulong? LogChannelId { get; set; }

    /// <summary>Where the apply panel was last posted, if anywhere.</summary>
    public ulong? PanelChannelId { get; set; }

    /// <summary>The apply panel message, if one was posted.</summary>
    public ulong? PanelMessageId { get; set; }

    /// <summary>Role allowed to vote, instead of everyone holding the access role.</summary>
    public ulong? VoterRoleId { get; set; }

    /// <summary>Role pinged when a new application arrives.</summary>
    public ulong? PingRoleId { get; set; }

    /// <summary>Whether applications are being accepted.</summary>
    public bool Enabled { get; set; }

    /// <summary>Approving votes needed to let someone in.</summary>
    public int RequiredApprovals { get; set; }

    /// <summary>Denying votes needed to turn someone away.</summary>
    public int RequiredDenials { get; set; }

    /// <summary>How long a vote stays open, in hours. Zero means no limit.</summary>
    public int VoteDurationHours { get; set; }

    /// <summary>What happens when the voting window closes. 0 deny, 1 majority, 2 stay open.</summary>
    public int OnExpiry { get; set; }

    /// <summary>Whether an abstain button is offered.</summary>
    public bool AllowAbstain { get; set; }

    /// <summary>Whether who voted which way is hidden from voters.</summary>
    public bool AnonymousVotes { get; set; }

    /// <summary>Whether the applicant's identity is hidden until the vote closes.</summary>
    public bool AnonymousApplicant { get; set; }

    /// <summary>Minimum Discord account age, in days, to apply.</summary>
    public int MinAccountAgeDays { get; set; }

    /// <summary>Minimum time in the server, in days, to apply.</summary>
    public int MinServerAgeDays { get; set; }

    /// <summary>How long a rejected applicant waits before trying again, in hours.</summary>
    public int ReapplyCooldownHours { get; set; }

    /// <summary>Whether the applicant is DMed when a decision lands.</summary>
    public bool DmOnDecision { get; set; }

    /// <summary>The number of applications still open on this gate.</summary>
    public int PendingApplications { get; set; }

    /// <summary>The gate's application questions, in display order.</summary>
    public List<ChannelAccessQuestionResponse> Questions { get; set; } = [];
}

/// <summary>
///     A question on a gate's application form.
/// </summary>
public class ChannelAccessQuestionResponse
{
    /// <summary>The question id.</summary>
    public int Id { get; set; }

    /// <summary>The zero-based display order.</summary>
    public int Position { get; set; }

    /// <summary>The question text.</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>Optional grey hint text inside the answer box.</summary>
    public string? Placeholder { get; set; }

    /// <summary>Whether the applicant must answer.</summary>
    public bool Required { get; set; }

    /// <summary>Whether the answer box is multi-line.</summary>
    public bool Paragraph { get; set; }
}

/// <summary>
///     An application to join a gated channel.
/// </summary>
public class ChannelAccessApplicationResponse
{
    /// <summary>The application id.</summary>
    public int Id { get; set; }

    /// <summary>The gate applied to.</summary>
    public int ConfigId { get; set; }

    /// <summary>The gated channel the gate covers.</summary>
    public ulong ChannelId { get; set; }

    /// <summary>The applicant.</summary>
    public ulong UserId { get; set; }

    /// <summary>The applicant's display name, if they are still in the server.</summary>
    public string? Username { get; set; }

    /// <summary>The applicant's avatar url, if they are still in the server.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>0 pending, 1 approved, 2 denied, 3 withdrawn, 4 expired.</summary>
    public int Status { get; set; }

    /// <summary>When the voting window closes, if it is limited.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>When the application was closed, if it has been.</summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>Who closed the application, if a person did.</summary>
    public ulong? ResolvedBy { get; set; }

    /// <summary>The note recorded when the application was closed.</summary>
    public string? ResolutionReason { get; set; }

    /// <summary>When the application was opened.</summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>How many approving votes it has.</summary>
    public int Approvals { get; set; }

    /// <summary>How many denying votes it has.</summary>
    public int Denials { get; set; }

    /// <summary>How many abstentions it has.</summary>
    public int Abstains { get; set; }

    /// <summary>The applicant's answers, in question order.</summary>
    public List<ChannelAccessAnswerResponse> Answers { get; set; } = [];

    /// <summary>The votes cast, empty unless the gate shows vote details.</summary>
    public List<ChannelAccessVoteResponse> Votes { get; set; } = [];
}

/// <summary>
///     One answer on an application.
/// </summary>
public class ChannelAccessAnswerResponse
{
    /// <summary>The question as it was worded when the application was made.</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>The applicant's answer.</summary>
    public string Answer { get; set; } = string.Empty;
}

/// <summary>
///     One vote on an application.
/// </summary>
public class ChannelAccessVoteResponse
{
    /// <summary>The voter.</summary>
    public ulong UserId { get; set; }

    /// <summary>The voter's display name, if they are still in the server.</summary>
    public string? Username { get; set; }

    /// <summary>1 approve, -1 deny, 0 abstain.</summary>
    public int Vote { get; set; }

    /// <summary>When the vote was cast.</summary>
    public DateTime? VotedAt { get; set; }
}

/// <summary>
///     A user barred from applying.
/// </summary>
public class ChannelAccessBlacklistResponse
{
    /// <summary>The entry id.</summary>
    public int Id { get; set; }

    /// <summary>The barred user.</summary>
    public ulong UserId { get; set; }

    /// <summary>The user's display name, if they are still in the server.</summary>
    public string? Username { get; set; }

    /// <summary>The gate the bar applies to, or null for every gate in the guild.</summary>
    public int? ConfigId { get; set; }

    /// <summary>The reason recorded by staff.</summary>
    public string? Reason { get; set; }

    /// <summary>Who added the entry.</summary>
    public ulong AddedBy { get; set; }

    /// <summary>When the entry was added.</summary>
    public DateTime? AddedAt { get; set; }
}

/// <summary>
///     Request to open applications on a locked channel.
/// </summary>
public class CreateChannelAccessGateRequest
{
    /// <summary>The channel to gate.</summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The role granted when an application passes. Leave it null to add approved applicants to the
    ///     channel individually with a permission overwrite instead.
    /// </summary>
    public ulong? AccessRoleId { get; set; }

    /// <summary>The user setting the gate up.</summary>
    public ulong UserId { get; set; }
}

/// <summary>
///     Request to change a gate's settings. Omitted fields are left alone.
/// </summary>
public class UpdateChannelAccessGateRequest
{
    /// <summary>The role granted when an application passes. Zero switches the gate to direct access.</summary>
    public ulong? AccessRoleId { get; set; }

    /// <summary>Where applications get posted for voting. Zero clears it.</summary>
    public ulong? ReviewChannelId { get; set; }

    /// <summary>Where decisions get logged. Zero clears it.</summary>
    public ulong? LogChannelId { get; set; }

    /// <summary>Role allowed to vote. Zero clears it.</summary>
    public ulong? VoterRoleId { get; set; }

    /// <summary>Role pinged when a new application arrives. Zero clears it.</summary>
    public ulong? PingRoleId { get; set; }

    /// <summary>Whether applications are being accepted.</summary>
    public bool? Enabled { get; set; }

    /// <summary>Approving votes needed to let someone in.</summary>
    public int? RequiredApprovals { get; set; }

    /// <summary>Denying votes needed to turn someone away.</summary>
    public int? RequiredDenials { get; set; }

    /// <summary>How long a vote stays open, in hours. Zero means no limit.</summary>
    public int? VoteDurationHours { get; set; }

    /// <summary>What happens when the voting window closes. 0 deny, 1 majority, 2 stay open.</summary>
    public int? OnExpiry { get; set; }

    /// <summary>Whether an abstain button is offered.</summary>
    public bool? AllowAbstain { get; set; }

    /// <summary>Whether who voted which way is hidden from voters.</summary>
    public bool? AnonymousVotes { get; set; }

    /// <summary>Whether the applicant's identity is hidden until the vote closes.</summary>
    public bool? AnonymousApplicant { get; set; }

    /// <summary>Minimum Discord account age, in days, to apply.</summary>
    public int? MinAccountAgeDays { get; set; }

    /// <summary>Minimum time in the server, in days, to apply.</summary>
    public int? MinServerAgeDays { get; set; }

    /// <summary>How long a rejected applicant waits before trying again, in hours.</summary>
    public int? ReapplyCooldownHours { get; set; }

    /// <summary>Whether the applicant is DMed when a decision lands.</summary>
    public bool? DmOnDecision { get; set; }
}

/// <summary>
///     Request to add a question to a gate's application form.
/// </summary>
public class CreateChannelAccessQuestionRequest
{
    /// <summary>The question text, up to 45 characters.</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>Optional grey hint text inside the answer box.</summary>
    public string? Placeholder { get; set; }

    /// <summary>Whether the applicant must answer.</summary>
    public bool Required { get; set; } = true;

    /// <summary>Whether the answer box is multi-line.</summary>
    public bool Paragraph { get; set; } = true;
}

/// <summary>
///     Request to close an application from the dashboard.
/// </summary>
public class ResolveChannelAccessApplicationRequest
{
    /// <summary>1 to approve, 2 to deny.</summary>
    public int Status { get; set; }

    /// <summary>The staff member closing it.</summary>
    public ulong UserId { get; set; }

    /// <summary>An optional note shown on the closed application.</summary>
    public string? Reason { get; set; }
}

/// <summary>
///     Request to bar a user from applying.
/// </summary>
public class CreateChannelAccessBlacklistRequest
{
    /// <summary>The user to bar.</summary>
    public ulong UserId { get; set; }

    /// <summary>The gate to bar them from, or null for every gate in the guild.</summary>
    public int? ConfigId { get; set; }

    /// <summary>The staff member adding the entry.</summary>
    public ulong AddedBy { get; set; }

    /// <summary>An optional reason for other staff.</summary>
    public string? Reason { get; set; }
}

/// <summary>
///     Request to post a gate's apply panel in a channel.
/// </summary>
public class PostChannelAccessPanelRequest
{
    /// <summary>The channel to post the panel in.</summary>
    public ulong ChannelId { get; set; }
}