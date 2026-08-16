using DataModel;
using Mewdeko.Controllers.Common.ChannelAccess;
using Mewdeko.Modules.ChannelAccess.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Manages the channel access system from the dashboard: the gates on locked channels, their
///     application forms, the applications themselves and the applicant blacklist.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class ChannelAccessController(
    DiscordShardedClient client,
    ChannelAccessService service,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Gets every access gate in a guild, with its questions and open application count.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <returns>The guild's gates.</returns>
    [HttpGet("gates")]
    public async Task<IActionResult> GetGates(ulong guildId)
    {
        var configs = await service.GetConfigsAsync(guildId);
        var pending = await service.GetPendingApplicationsAsync(guildId);

        var result = new List<ChannelAccessGateResponse>();
        foreach (var config in configs)
        {
            var questions = await service.GetQuestionsAsync(config.Id);
            result.Add(ToGateResponse(config, questions, pending.Count(x => x.ConfigId == config.Id)));
        }

        return Ok(result);
    }

    /// <summary>
    ///     Opens applications for a locked channel.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <param name="request">The channel to gate and the role approved applicants receive.</param>
    /// <returns>The newly created gate.</returns>
    [HttpPost("gates")]
    public async Task<IActionResult> CreateGate(ulong guildId, [FromBody] CreateChannelAccessGateRequest request)
    {
        if (await service.GetConfigAsync(guildId, request.ChannelId) is not null)
            return Conflict("That channel already has an access gate.");

        var config = await service.CreateConfigAsync(guildId, request.ChannelId, request.AccessRoleId, request.UserId);
        auditContext.RecordAfter(config);

        return Ok(ToGateResponse(config, [], 0));
    }

    /// <summary>
    ///     Changes a gate's settings. Fields left out of the request are not touched, and passing zero for
    ///     an optional channel or role clears it.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <param name="configId">The gate id.</param>
    /// <param name="request">The settings to change.</param>
    /// <returns>The updated gate.</returns>
    [HttpPut("gates/{configId:int}")]
    public async Task<IActionResult> UpdateGate(ulong guildId, int configId,
        [FromBody] UpdateChannelAccessGateRequest request)
    {
        var config = await service.GetConfigByIdAsync(configId);
        if (config is null || config.GuildId != guildId)
            return NotFound("Gate not found");

        auditContext.RecordBefore(config);

        if (request.AccessRoleId is { } accessRole)
        {
            config.AccessRoleId = accessRole == 0 ? null : accessRole;
            config.GrantMode = (int)(accessRole == 0 ? AccessGrantMode.ChannelPermission : AccessGrantMode.Role);
        }

        if (request.ReviewChannelId is { } review) config.ReviewChannelId = review == 0 ? null : review;
        if (request.LogChannelId is { } log) config.LogChannelId = log == 0 ? null : log;
        if (request.VoterRoleId is { } voter) config.VoterRoleId = voter == 0 ? null : voter;
        if (request.PingRoleId is { } ping) config.PingRoleId = ping == 0 ? null : ping;
        if (request.Enabled is { } enabled) config.Enabled = enabled;
        if (request.RequiredApprovals is { } approvals) config.RequiredApprovals = Math.Max(0, approvals);
        if (request.RequiredDenials is { } denials) config.RequiredDenials = Math.Max(0, denials);
        if (request.VoteDurationHours is { } voteHours) config.VoteDurationHours = Math.Max(0, voteHours);
        if (request.OnExpiry is { } onExpiry && Enum.IsDefined((AccessExpiryBehavior)onExpiry))
            config.OnExpiry = onExpiry;
        if (request.AllowAbstain is { } allowAbstain) config.AllowAbstain = allowAbstain;
        if (request.AnonymousVotes is { } anonymousVotes) config.AnonymousVotes = anonymousVotes;
        if (request.AnonymousApplicant is { } anonymousApplicant) config.AnonymousApplicant = anonymousApplicant;
        if (request.MinAccountAgeDays is { } accountAge) config.MinAccountAgeDays = Math.Max(0, accountAge);
        if (request.MinServerAgeDays is { } serverAge) config.MinServerAgeDays = Math.Max(0, serverAge);
        if (request.ReapplyCooldownHours is { } cooldown) config.ReapplyCooldownHours = Math.Max(0, cooldown);
        if (request.DmOnDecision is { } dmOnDecision) config.DmOnDecision = dmOnDecision;

        await service.UpdateConfigAsync(config);
        auditContext.RecordAfter(config);

        var questions = await service.GetQuestionsAsync(config.Id);
        var pending = await service.GetPendingApplicationsAsync(guildId, config.Id);
        return Ok(ToGateResponse(config, questions, pending.Count));
    }

    /// <summary>
    ///     Removes a gate along with its questions, applications and votes.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <param name="configId">The gate id.</param>
    /// <returns>No content when the gate is gone.</returns>
    [HttpDelete("gates/{configId:int}")]
    public async Task<IActionResult> DeleteGate(ulong guildId, int configId)
    {
        var config = await service.GetConfigByIdAsync(configId);
        if (config is null || config.GuildId != guildId)
            return NotFound("Gate not found");

        auditContext.RecordBefore(config);
        await service.DeleteConfigAsync(configId);
        return NoContent();
    }

    /// <summary>
    ///     Posts the gate's apply panel in a channel.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <param name="configId">The gate id.</param>
    /// <param name="request">The channel to post the panel in.</param>
    /// <returns>The panel message id.</returns>
    [HttpPost("gates/{configId:int}/panel")]
    public async Task<IActionResult> PostPanel(ulong guildId, int configId,
        [FromBody] PostChannelAccessPanelRequest request)
    {
        var config = await service.GetConfigByIdAsync(configId);
        if (config is null || config.GuildId != guildId)
            return NotFound("Gate not found");

        var messageId = await service.PostPanelAsync(config, request.ChannelId);
        if (messageId is null)
            return BadRequest("Could not post the panel in that channel.");

        return Ok(new
        {
            messageId
        });
    }

    /// <summary>
    ///     Gets a gate's application questions in display order.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <param name="configId">The gate id.</param>
    /// <returns>The gate's questions.</returns>
    [HttpGet("gates/{configId:int}/questions")]
    public async Task<IActionResult> GetQuestions(ulong guildId, int configId)
    {
        var config = await service.GetConfigByIdAsync(configId);
        if (config is null || config.GuildId != guildId)
            return NotFound("Gate not found");

        var questions = await service.GetQuestionsAsync(configId);
        return Ok(questions.Select(ToQuestionResponse));
    }

    /// <summary>
    ///     Adds a question to a gate's application form.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <param name="configId">The gate id.</param>
    /// <param name="request">The question to add.</param>
    /// <returns>The gate's questions after the addition.</returns>
    [HttpPost("gates/{configId:int}/questions")]
    public async Task<IActionResult> AddQuestion(ulong guildId, int configId,
        [FromBody] CreateChannelAccessQuestionRequest request)
    {
        var config = await service.GetConfigByIdAsync(configId);
        if (config is null || config.GuildId != guildId)
            return NotFound("Gate not found");

        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("The question cannot be empty.");

        if (!await service.AddQuestionAsync(configId, request.Question, request.Required, request.Paragraph,
                request.Placeholder))
            return BadRequest($"A gate can only have {ChannelAccessService.MaxQuestions} questions.");

        var questions = await service.GetQuestionsAsync(configId);
        return Ok(questions.Select(ToQuestionResponse));
    }

    /// <summary>
    ///     Removes a question from a gate's application form and closes the gap behind it.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <param name="configId">The gate id.</param>
    /// <param name="position">The one-based display position.</param>
    /// <returns>The gate's questions after the removal.</returns>
    [HttpDelete("gates/{configId:int}/questions/{position:int}")]
    public async Task<IActionResult> RemoveQuestion(ulong guildId, int configId, int position)
    {
        var config = await service.GetConfigByIdAsync(configId);
        if (config is null || config.GuildId != guildId)
            return NotFound("Gate not found");

        if (!await service.RemoveQuestionAsync(configId, position))
            return NotFound("There is no question at that position.");

        var questions = await service.GetQuestionsAsync(configId);
        return Ok(questions.Select(ToQuestionResponse));
    }

    /// <summary>
    ///     Gets applications in a guild, newest first, optionally filtered by gate and status.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <param name="configId">An optional gate filter.</param>
    /// <param name="status">An optional status filter. 0 pending, 1 approved, 2 denied, 3 withdrawn, 4 expired.</param>
    /// <param name="limit">The maximum number of applications to return.</param>
    /// <returns>The matching applications with their answers and tallies.</returns>
    [HttpGet("applications")]
    public async Task<IActionResult> GetApplications(ulong guildId, [FromQuery] int? configId = null,
        [FromQuery] int? status = null, [FromQuery] int limit = 100)
    {
        var statusFilter = status is null ? (AccessApplicationStatus?)null : (AccessApplicationStatus)status.Value;
        var applications =
            await service.GetApplicationsAsync(guildId, configId, statusFilter, Math.Clamp(limit, 1, 200));

        var result = new List<ChannelAccessApplicationResponse>();
        foreach (var application in applications)
            result.Add(await ToApplicationResponseAsync(application));

        return Ok(result);
    }

    /// <summary>
    ///     Gets a single application with its answers and, when the gate allows it, its individual votes.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <param name="applicationId">The application id.</param>
    /// <returns>The application.</returns>
    [HttpGet("applications/{applicationId:int}")]
    public async Task<IActionResult> GetApplication(ulong guildId, int applicationId)
    {
        var application = await service.GetApplicationAsync(applicationId);
        if (application is null || application.GuildId != guildId)
            return NotFound("Application not found");

        return Ok(await ToApplicationResponseAsync(application, true));
    }

    /// <summary>
    ///     Closes an application from the dashboard, overriding the vote count.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <param name="applicationId">The application id.</param>
    /// <param name="request">The outcome to record.</param>
    /// <returns>The closed application.</returns>
    [HttpPost("applications/{applicationId:int}/resolve")]
    public async Task<IActionResult> ResolveApplication(ulong guildId, int applicationId,
        [FromBody] ResolveChannelAccessApplicationRequest request)
    {
        var application = await service.GetApplicationAsync(applicationId);
        if (application is null || application.GuildId != guildId)
            return NotFound("Application not found");

        if (application.Status != (int)AccessApplicationStatus.Pending)
            return BadRequest("That application is already closed.");

        var status = (AccessApplicationStatus)request.Status;
        if (status is not (AccessApplicationStatus.Approved or AccessApplicationStatus.Denied))
            return BadRequest("An application can only be approved or denied.");

        auditContext.RecordBefore(application);
        await service.ResolveApplicationAsync(application, status, request.UserId, request.Reason);
        auditContext.RecordAfter(application);

        return Ok(await ToApplicationResponseAsync(application, true));
    }

    /// <summary>
    ///     Gets everyone barred from applying in a guild.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <returns>The blacklist entries.</returns>
    [HttpGet("blacklist")]
    public async Task<IActionResult> GetBlacklist(ulong guildId)
    {
        var entries = await service.GetBlacklistAsync(guildId);
        var guild = client.GetGuild(guildId);

        return Ok(entries.Select(entry => new ChannelAccessBlacklistResponse
        {
            Id = entry.Id,
            UserId = entry.UserId,
            Username = guild?.GetUser(entry.UserId)?.Username,
            ConfigId = entry.ConfigId,
            Reason = entry.Reason,
            AddedBy = entry.AddedBy,
            AddedAt = entry.DateAdded
        }));
    }

    /// <summary>
    ///     Bars a user from applying, either for one gate or for every gate in the guild.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <param name="request">The user to bar and the scope of the bar.</param>
    /// <returns>No content once the entry exists.</returns>
    [HttpPost("blacklist")]
    public async Task<IActionResult> AddBlacklist(ulong guildId,
        [FromBody] CreateChannelAccessBlacklistRequest request)
    {
        if (request.ConfigId is { } configId)
        {
            var config = await service.GetConfigByIdAsync(configId);
            if (config is null || config.GuildId != guildId)
                return NotFound("Gate not found");
        }

        await service.AddBlacklistAsync(guildId, request.ConfigId, request.UserId, request.AddedBy, request.Reason);
        return NoContent();
    }

    /// <summary>
    ///     Lifts a bar so a user can apply again.
    /// </summary>
    /// <param name="guildId">The guild id.</param>
    /// <param name="userId">The barred user.</param>
    /// <param name="configId">The gate the bar was set on, or null for the guild wide entry.</param>
    /// <returns>No content once the entry is gone.</returns>
    [HttpDelete("blacklist/{userId}")]
    public async Task<IActionResult> RemoveBlacklist(ulong guildId, ulong userId, [FromQuery] int? configId = null)
    {
        if (!await service.RemoveBlacklistAsync(guildId, configId, userId))
            return NotFound("That user is not blocked from applying.");

        return NoContent();
    }

    private static ChannelAccessGateResponse ToGateResponse(ChannelAccessConfig config,
        IEnumerable<ChannelAccessQuestion> questions, int pendingApplications)
    {
        return new ChannelAccessGateResponse
        {
            Id = config.Id,
            ChannelId = config.ChannelId,
            AccessRoleId = config.AccessRoleId,
            GrantMode = config.GrantMode,
            ReviewChannelId = config.ReviewChannelId,
            LogChannelId = config.LogChannelId,
            PanelChannelId = config.PanelChannelId,
            PanelMessageId = config.PanelMessageId,
            VoterRoleId = config.VoterRoleId,
            PingRoleId = config.PingRoleId,
            Enabled = config.Enabled,
            RequiredApprovals = config.RequiredApprovals,
            RequiredDenials = config.RequiredDenials,
            VoteDurationHours = config.VoteDurationHours,
            OnExpiry = config.OnExpiry,
            AllowAbstain = config.AllowAbstain,
            AnonymousVotes = config.AnonymousVotes,
            AnonymousApplicant = config.AnonymousApplicant,
            MinAccountAgeDays = config.MinAccountAgeDays,
            MinServerAgeDays = config.MinServerAgeDays,
            ReapplyCooldownHours = config.ReapplyCooldownHours,
            DmOnDecision = config.DmOnDecision,
            PendingApplications = pendingApplications,
            Questions = questions.Select(ToQuestionResponse).ToList()
        };
    }

    private static ChannelAccessQuestionResponse ToQuestionResponse(ChannelAccessQuestion question)
    {
        return new ChannelAccessQuestionResponse
        {
            Id = question.Id,
            Position = question.Position,
            Question = question.Question,
            Placeholder = question.Placeholder,
            Required = question.Required,
            Paragraph = question.Paragraph
        };
    }

    private async Task<ChannelAccessApplicationResponse> ToApplicationResponseAsync(
        ChannelAccessApplication application, bool includeVotes = false)
    {
        var config = await service.GetConfigByIdAsync(application.ConfigId);
        var answers = await service.GetAnswersAsync(application.Id);
        var votes = await service.GetVotesAsync(application.Id);
        var guild = client.GetGuild(application.GuildId);
        var applicant = guild?.GetUser(application.UserId);

        var hideApplicant = config?.AnonymousApplicant == true &&
                            application.Status == (int)AccessApplicationStatus.Pending;

        return new ChannelAccessApplicationResponse
        {
            Id = application.Id,
            ConfigId = application.ConfigId,
            ChannelId = config?.ChannelId ?? 0,
            UserId = application.UserId,
            Username = hideApplicant ? null : applicant?.Username,
            AvatarUrl = hideApplicant ? null : applicant?.GetAvatarUrl() ?? applicant?.GetDefaultAvatarUrl(),
            Status = application.Status,
            ExpiresAt = application.ExpiresAt,
            ResolvedAt = application.ResolvedAt,
            ResolvedBy = application.ResolvedBy,
            ResolutionReason = application.ResolutionReason,
            CreatedAt = application.DateAdded,
            Approvals = votes.Count(x => x.Vote == 1),
            Denials = votes.Count(x => x.Vote == -1),
            Abstains = votes.Count(x => x.Vote == 0),
            Answers = answers.Select(answer => new ChannelAccessAnswerResponse
            {
                Question = answer.Question, Answer = answer.Answer
            }).ToList(),
            Votes = includeVotes && config?.AnonymousVotes != true
                ? votes.Select(vote => new ChannelAccessVoteResponse
                {
                    UserId = vote.UserId,
                    Username = guild?.GetUser(vote.UserId)?.Username,
                    Vote = vote.Vote,
                    VotedAt = vote.DateAdded
                }).ToList()
                : []
        };
    }
}