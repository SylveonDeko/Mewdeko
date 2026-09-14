using DataModel;
using Mewdeko.AuthHandlers;
using Mewdeko.Controllers.Common.FeatureRequests;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mewdeko.Controllers;

/// <summary>
///     Feature requests submitted from the dashboard. Any logged in dashboard user can submit,
///     list, and upvote. Status changes, deletion, stats, and settings are bot owner only.
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class FeatureRequestsController(FeatureRequestService service, BotCredentials creds) : Controller
{
    private ulong userId;

    /// <summary>
    ///     Requires a verified dashboard identity on every action, since submissions and votes are
    ///     attributed to the user in the dashboard JWT rather than to anything the client sends.
    /// </summary>
    /// <param name="context">The action context.</param>
    /// <param name="next">The next action in the pipeline.</param>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var authResult = await HttpContext.AuthenticateAsync(DashJwtConstants.SchemeName);
        var userIdClaim = authResult.Principal?.FindFirst(DashJwtConstants.UserIdClaim)?.Value;

        if (!authResult.Succeeded || !ulong.TryParse(userIdClaim, out userId))
        {
            context.Result = Forbid();
            return;
        }

        await next();
    }

    /// <summary>
    ///     Submits a new request.
    /// </summary>
    /// <param name="request">The request body.</param>
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] FeatureRequestSubmitRequest request)
    {
        var (created, error) =
            await service.SubmitAsync(userId, request.GuildId, request.Category, request.Title, request.Body);
        return created is null ? BadRequest(error) : Ok(Map(created, false));
    }

    /// <summary>
    ///     Returns a page of requests, most voted first by default.
    /// </summary>
    /// <param name="status">Optional status filter.</param>
    /// <param name="category">Optional category filter.</param>
    /// <param name="search">Optional search across title, body, and submitter.</param>
    /// <param name="sort">votes or newest.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The page size (max 200).</param>
    [HttpGet]
    public async Task<IActionResult> GetPage(
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var (items, total, voted) =
            await service.GetPageAsync(status, category, search, sort, page, pageSize, userId);

        return Ok(new FeatureRequestPageResponse
        {
            Items = items.Select(x => Map(x, voted.Contains(x.Id))).ToList(),
            Total = total,
            Page = page < 1 ? 1 : page,
            PageSize = Math.Clamp(pageSize, 1, 200)
        });
    }

    /// <summary>
    ///     Returns everything the current user has submitted.
    /// </summary>
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var items = await service.GetMineAsync(userId);
        return Ok(items.Select(x => Map(x, false)).ToList());
    }

    /// <summary>
    ///     Adds or removes the current user's upvote.
    /// </summary>
    /// <param name="id">The request id.</param>
    [HttpPost("{id:int}/vote")]
    public async Task<IActionResult> ToggleVote(int id)
    {
        var result = await service.ToggleVoteAsync(id, userId);
        if (result is null)
            return NotFound();

        return Ok(new FeatureRequestVoteResponse
        {
            Votes = result.Value.Votes, Voted = result.Value.Voted
        });
    }

    /// <summary>
    ///     Sets the status and note on a request. Bot owner only.
    /// </summary>
    /// <param name="id">The request id.</param>
    /// <param name="request">The new status and note.</param>
    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] FeatureRequestStatusRequest request)
    {
        if (!creds.IsOwner(userId))
            return Forbid();

        var (updated, error) = await service.SetStatusAsync(id, request.Status, request.Note);
        return updated is null ? BadRequest(error) : Ok(Map(updated, false));
    }

    /// <summary>
    ///     Deletes a request. Bot owner only.
    /// </summary>
    /// <param name="id">The request id.</param>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!creds.IsOwner(userId))
            return Forbid();

        return await service.DeleteAsync(id) ? Ok() : NotFound();
    }

    /// <summary>
    ///     Returns counts by status and category. Bot owner only.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        if (!creds.IsOwner(userId))
            return Forbid();

        var (total, byStatus, byCategory) = await service.GetStatsAsync();
        return Ok(new FeatureRequestStatsResponse
        {
            Total = total, ByStatus = byStatus, ByCategory = byCategory
        });
    }

    /// <summary>
    ///     Returns the report channel settings. Bot owner only.
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        if (!creds.IsOwner(userId))
            return Forbid();

        return Ok(await BuildSettings());
    }

    /// <summary>
    ///     Updates the report channel. Bot owner only.
    /// </summary>
    /// <param name="request">The new channel.</param>
    [HttpPost("settings")]
    public async Task<IActionResult> SetSettings([FromBody] FeatureRequestSettingsRequest request)
    {
        if (!creds.IsOwner(userId))
            return Forbid();

        if (request.ChannelId is not 0)
        {
            var (_, _, _, reachable) = await service.DescribeChannelAsync(request.ChannelId);
            if (!reachable)
                return BadRequest("That channel does not exist, or the bot cannot see it.");
        }

        service.UpdateSettings(request.ChannelId);
        return Ok(await BuildSettings());
    }

    private async Task<FeatureRequestSettingsResponse> BuildSettings()
    {
        var effective = service.ReportChannelId;
        var (channelName, guildId, guildName, reachable) = await service.DescribeChannelAsync(effective);

        return new FeatureRequestSettingsResponse
        {
            ChannelId = service.ConfiguredChannelId,
            EffectiveChannelId = effective,
            UsingFallback = service.ConfiguredChannelId is 0,
            ChannelName = channelName,
            GuildId = guildId,
            GuildName = guildName,
            Reachable = reachable
        };
    }

    private FeatureRequestEntryResponse Map(FeatureRequest entry, bool voted)
    {
        return new FeatureRequestEntryResponse
        {
            Id = entry.Id,
            UserId = entry.UserId,
            UserName = entry.UserName,
            GuildId = entry.GuildId,
            GuildName = entry.GuildName,
            Category = entry.Category,
            Title = entry.Title,
            Body = entry.Body,
            Status = entry.Status,
            OwnerNote = entry.OwnerNote,
            Votes = entry.Votes,
            Voted = voted,
            Mine = entry.UserId == userId,
            UpdatedAt = AsUtc(entry.UpdatedAt),
            DateAdded = AsUtc(entry.DateAdded)
        };
    }

    private static DateTime? AsUtc(DateTime? value)
    {
        return value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
    }
}
