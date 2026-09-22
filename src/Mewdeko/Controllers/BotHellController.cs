using Mewdeko.AuthHandlers;
using Mewdeko.Controllers.Common.BotHell;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mewdeko.Controllers;

/// <summary>
///     Owner only API for finding servers littered with bots and leaving them in bulk. Bot wide, not guild scoped.
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class BotHellController(BotHellService service, DiscordShardedClient client, BotCredentials creds)
    : Controller
{
    /// <summary>
    ///     Rejects anyone who is not a bot owner. The shared API key authorizes the dashboard proxy, so the dashboard
    ///     user has to be read out of the dashboard JWT and checked here.
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
    ///     Returns every server on this instance evaluated against the thresholds, flagged ones first.
    /// </summary>
    /// <param name="flaggedOnly">Whether to return only flagged servers.</param>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool flaggedOnly = false)
    {
        var all = service.ListAll();
        var items = (flaggedOnly ? all.Where(v => v.IsBotHell) : all).Select(Map).ToList();

        return Ok(new BotHellListResponse
        {
            Items = items, Flagged = all.Count(v => v.IsBotHell), Settings = await BuildSettings()
        });
    }

    /// <summary>
    ///     Re-evaluates one server after downloading its full member list.
    /// </summary>
    /// <param name="guildId">The server to check.</param>
    [HttpPost("{guildId}/check")]
    public async Task<IActionResult> Check(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound();

        return Ok(Map(await service.EvaluateFullAsync(guild)));
    }

    /// <summary>
    ///     Leaves the given servers.
    /// </summary>
    /// <param name="request">The server ids to leave.</param>
    [HttpPost("leave")]
    public async Task<IActionResult> Leave([FromBody] BotHellBulkRequest request)
    {
        if (request.GuildIds.Count == 0)
            return BadRequest("No servers were given.");

        var (left, failed) = await service.LeaveAsync(request.GuildIds);
        return Ok(new BotHellLeaveResponse
        {
            Left = left, Failed = failed
        });
    }

    /// <summary>
    ///     Returns the bot wide settings, with the effective report channel resolved.
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        return Ok(await BuildSettings());
    }

    /// <summary>
    ///     Updates the bot wide settings.
    /// </summary>
    /// <param name="request">The new settings.</param>
    [HttpPost("settings")]
    public async Task<IActionResult> SetSettings([FromBody] BotHellSettingsRequest request)
    {
        if (request.ChannelId is not 0)
        {
            var (_, _, _, reachable) = await service.DescribeChannelAsync(request.ChannelId);
            if (!reachable)
                return BadRequest("That channel does not exist, or the bot cannot see it.");
        }

        service.UpdateSettings(request.MinMembers, request.BotCount, request.BotPercent, request.AutoLeave,
            request.ChannelId);
        return Ok(await BuildSettings());
    }

    private async Task<BotHellSettingsResponse> BuildSettings()
    {
        var effective = service.ReportChannelId;
        var (channelName, guildId, guildName, reachable) = await service.DescribeChannelAsync(effective);

        return new BotHellSettingsResponse
        {
            MinMembers = service.MinMembers,
            BotCount = service.BotCountLimit,
            BotPercent = service.BotPercentLimit,
            AutoLeave = service.AutoLeave,
            ChannelId = service.ConfiguredChannelId,
            EffectiveChannelId = effective,
            UsingFallback = service.ConfiguredChannelId is 0,
            ChannelName = channelName,
            GuildId = guildId,
            GuildName = guildName,
            Reachable = reachable
        };
    }

    private BotHellEntryResponse Map(BotHellVerdict verdict)
    {
        var guild = client.GetGuild(verdict.GuildId);
        return new BotHellEntryResponse
        {
            GuildId = verdict.GuildId,
            GuildName = verdict.GuildName,
            IconUrl = guild?.IconUrl,
            OwnerId = guild?.OwnerId ?? 0,
            Total = verdict.Total,
            Humans = verdict.Humans,
            Bots = verdict.Bots,
            Percent = verdict.Percent,
            IsBotHell = verdict.IsBotHell,
            ByCount = verdict.ByCount,
            ByPercent = verdict.ByPercent,
            Complete = verdict.Complete,
            JoinedAt = guild?.CurrentUser?.JoinedAt?.UtcDateTime
        };
    }
}
