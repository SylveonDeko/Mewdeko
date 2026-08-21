using Mewdeko.Modules.Moderation.Common;
using Mewdeko.Modules.Moderation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Request body for writing a ban purge setting.
/// </summary>
public class BanPruneSettingRequest
{
    /// <summary>
    ///     0 for the guild default, 1 for a category override, 2 for a channel override.
    /// </summary>
    public int ScopeType { get; set; }

    /// <summary>
    ///     The category or channel the setting applies to. Ignored for the guild default.
    /// </summary>
    public ulong ScopeId { get; set; }

    /// <summary>
    ///     The action key, or null to cover every action in the scope.
    /// </summary>
    public string? ActionKey { get; set; }

    /// <summary>
    ///     The purge in days, 0 through 7.
    /// </summary>
    public int PruneDays { get; set; }
}

/// <summary>
///     Controller for reading and writing how many days of messages each ban action purges.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class BanPruneController(BanPruneService banPruneService, DiscordShardedClient client) : Controller
{
    /// <summary>
    ///     Lists the actions that can carry a purge setting, with their built in defaults.
    /// </summary>
    /// <param name="guildId">The guild being configured.</param>
    /// <returns>Every configurable action.</returns>
    [HttpGet("actions")]
    public IActionResult GetActions(ulong guildId)
    {
        return Ok(BanPruneAction.All.Select(x => new
        {
            key = x.Key, displayName = x.DisplayName, defaultDays = x.DefaultDays
        }));
    }

    /// <summary>
    ///     Gets every purge setting configured in a guild.
    /// </summary>
    /// <param name="guildId">The guild to read.</param>
    /// <returns>The stored settings.</returns>
    [HttpGet]
    public async Task<IActionResult> GetSettings(ulong guildId)
    {
        var settings = await banPruneService.GetSettingListAsync(guildId);

        return Ok(settings.Select(x => new
        {
            id = x.Id,
            scopeType = x.ScopeType,
            scopeId = x.ScopeId.ToString(),
            actionKey = x.ActionKey ?? string.Empty,
            pruneDays = x.PruneDays
        }));
    }

    /// <summary>
    ///     Gets the purge a ban would use right now for one action.
    /// </summary>
    /// <param name="guildId">The guild to read.</param>
    /// <param name="actionKey">The action to resolve.</param>
    /// <param name="channelId">The channel the ban would come from, if any.</param>
    /// <returns>The resolved purge in days.</returns>
    [HttpGet("effective/{actionKey}")]
    public async Task<IActionResult> GetEffective(ulong guildId, string actionKey, [FromQuery] ulong? channelId = null)
    {
        var action = BanPruneAction.FromKey(actionKey);
        if (action is null)
            return NotFound("Unknown ban action");

        IChannel? channel = channelId is null
            ? null
            : client.GetGuild(guildId)?.GetChannel(channelId.Value);
        var days = await banPruneService.GetPruneDaysAsync(guildId, action, channel);

        return Ok(new
        {
            actionKey = action.Key, pruneDays = days
        });
    }

    /// <summary>
    ///     Creates or updates one purge setting.
    /// </summary>
    /// <param name="guildId">The guild to write to.</param>
    /// <param name="request">The setting to store.</param>
    /// <returns>No content on success.</returns>
    [HttpPost]
    public async Task<IActionResult> SetSetting(ulong guildId, [FromBody] BanPruneSettingRequest request)
    {
        if (!Enum.IsDefined(typeof(BanPruneScope), request.ScopeType))
            return BadRequest("Unknown scope type");

        if (request.PruneDays is < 0 or > BanPruneService.MaxPruneDays)
            return BadRequest($"Purge must be between 0 and {BanPruneService.MaxPruneDays} days");

        BanPruneAction? action = null;
        if (!string.IsNullOrWhiteSpace(request.ActionKey))
        {
            action = BanPruneAction.FromKey(request.ActionKey);
            if (action is null)
                return BadRequest("Unknown ban action");
        }

        var scope = (BanPruneScope)request.ScopeType;
        if (scope != BanPruneScope.Guild && request.ScopeId == 0)
            return BadRequest("Overrides need a channel or category id");

        await banPruneService.SetAsync(guildId, scope, request.ScopeId, action, request.PruneDays);
        return NoContent();
    }

    /// <summary>
    ///     Removes one purge setting.
    /// </summary>
    /// <param name="guildId">The guild to write to.</param>
    /// <param name="scopeType">The scope the setting is on.</param>
    /// <param name="scopeId">The category or channel id, or 0 for the guild default.</param>
    /// <param name="actionKey">The action to clear, or null for the setting covering every action.</param>
    /// <returns>No content when a setting was removed.</returns>
    [HttpDelete]
    public async Task<IActionResult> ClearSetting(
        ulong guildId,
        [FromQuery] int scopeType,
        [FromQuery] ulong scopeId = 0,
        [FromQuery] string? actionKey = null)
    {
        if (!Enum.IsDefined(typeof(BanPruneScope), scopeType))
            return BadRequest("Unknown scope type");

        BanPruneAction? action = null;
        if (!string.IsNullOrWhiteSpace(actionKey))
        {
            action = BanPruneAction.FromKey(actionKey);
            if (action is null)
                return BadRequest("Unknown ban action");
        }

        var removed = await banPruneService.ClearAsync(guildId, (BanPruneScope)scopeType, scopeId, action);
        return removed ? NoContent() : NotFound("No such setting");
    }

    /// <summary>
    ///     Removes every purge setting in a guild.
    /// </summary>
    /// <param name="guildId">The guild to reset.</param>
    /// <returns>The number of settings removed.</returns>
    [HttpDelete("all")]
    public async Task<IActionResult> Reset(ulong guildId)
    {
        var removed = await banPruneService.ResetAsync(guildId);
        return Ok(new
        {
            removed
        });
    }
}