using DataModel;
using Mewdeko.AuthHandlers;
using Mewdeko.Controllers.Common.LeaveFeedback;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mewdeko.Controllers;

/// <summary>
///     Read API for the feedback servers owners give after removing the bot. Bot wide, not
///     guild scoped, so the dashboard only exposes it to bot owners.
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class LeaveFeedbackController(GuildLeaveFeedbackService service, BotCredentials creds) : Controller
{
    /// <summary>
    ///     Rejects anyone who is not a bot owner. Controllers authorize with the shared API key,
    ///     which the dashboard proxy attaches to every request, so the dashboard user identity has
    ///     to be read out of the dashboard JWT explicitly and checked here. Without this the
    ///     client-side owner check would be the only thing standing between any logged in
    ///     dashboard user and every server owner's feedback.
    /// </summary>
    /// <param name="context">The action context.</param>
    /// <param name="next">The next action in the pipeline.</param>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var authResult = await HttpContext.AuthenticateAsync(DashJwtConstants.SchemeName);
        var userIdClaim = authResult.Principal?.FindFirst(DashJwtConstants.UserIdClaim)?.Value;

        if (!ulong.TryParse(userIdClaim, out var userId) || !creds.IsOwner(userId))
        {
            context.Result = Forbid();
            return;
        }

        await next();
    }

    /// <summary>
    ///     Returns a page of leave feedback records, newest first.
    /// </summary>
    /// <param name="reason">Optional reason key filter.</param>
    /// <param name="status">Optional status filter: answered, dismissed, pending, or commented.</param>
    /// <param name="search">Optional search across guild name and comment.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The page size (max 200).</param>
    [HttpGet]
    public async Task<IActionResult> GetFeedback(
        [FromQuery] string? reason = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var (items, total) = await service.GetPageAsync(reason, status, search, page, pageSize);

        return Ok(new LeaveFeedbackPageResponse
        {
            Items = items.Select(Map).ToList(),
            Total = total,
            Page = page < 1 ? 1 : page,
            PageSize = Math.Clamp(pageSize, 1, 200)
        });
    }

    /// <summary>
    ///     Returns the bot wide leave feedback settings, with the effective channel resolved.
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        return Ok(await BuildSettings());
    }

    /// <summary>
    ///     Updates the bot wide leave feedback settings.
    /// </summary>
    /// <param name="request">The new settings.</param>
    [HttpPost("settings")]
    public async Task<IActionResult> SetSettings([FromBody] LeaveFeedbackSettingsRequest request)
    {
        if (request.ChannelId is not 0)
        {
            var (_, _, _, reachable) = await service.DescribeChannelAsync(request.ChannelId);
            if (!reachable)
                return BadRequest("That channel does not exist, or the bot cannot see it.");
        }

        service.UpdateSettings(request.Enabled, request.ChannelId);
        return Ok(await BuildSettings());
    }

    /// <summary>
    ///     Returns aggregate counts by status and by reason.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var (total, answered, dismissed, pending, withComment, byReason) = await service.GetStatsAsync();

        return Ok(new LeaveFeedbackStatsResponse
        {
            Total = total,
            Answered = answered,
            Dismissed = dismissed,
            Pending = pending,
            WithComment = withComment,
            Reasons = GuildLeaveFeedbackService.ReasonKeys.Select(key => new LeaveFeedbackReasonResponse
            {
                Key = key, Label = service.GetReasonLabel(key, null), Count = byReason.GetValueOrDefault(key)
            }).ToList()
        });
    }

    /// <summary>
    ///     Deletes a feedback record.
    /// </summary>
    /// <param name="id">The record id to delete.</param>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteFeedback(int id)
    {
        return await service.DeleteAsync(id) ? Ok() : NotFound();
    }

    private async Task<LeaveFeedbackSettingsResponse> BuildSettings()
    {
        var effective = service.ReportChannelId;
        var (channelName, guildId, guildName, reachable) = await service.DescribeChannelAsync(effective);

        return new LeaveFeedbackSettingsResponse
        {
            Enabled = service.Enabled,
            ChannelId = service.ConfiguredChannelId,
            EffectiveChannelId = effective,
            UsingFallback = service.ConfiguredChannelId is 0,
            ChannelName = channelName,
            GuildId = guildId,
            GuildName = guildName,
            Reachable = reachable
        };
    }

    private LeaveFeedbackEntryResponse Map(GuildLeaveFeedback entry)
    {
        return new LeaveFeedbackEntryResponse
        {
            Id = entry.Id,
            GuildId = entry.GuildId,
            GuildName = entry.GuildName,
            MemberCount = entry.MemberCount,
            OwnerId = entry.OwnerId,
            JoinedAt = AsUtc(entry.JoinedAt),
            Reason = entry.Reason,
            ReasonLabel = entry.Reason is null ? null : service.GetReasonLabel(entry.Reason, null),
            Comment = entry.Comment,
            Dismissed = entry.Dismissed,
            AnsweredAt = AsUtc(entry.AnsweredAt),
            DateAdded = AsUtc(entry.DateAdded)
        };
    }

    private static DateTime? AsUtc(DateTime? value)
    {
        return value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
    }
}