using Mewdeko.Controllers.Common.Starboard;
using Mewdeko.Modules.Starboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing starboard configurations in guilds
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class StarboardController : Controller
{
    private readonly DiscordShardedClient client;
    private readonly StarboardService starboardService;
    private readonly IDashboardAuditContext auditContext;

    /// <summary>
    ///     Initializes a new instance of the StarboardController
    /// </summary>
    /// <param name="starboardService">The starboardservice service.</param>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
    public StarboardController(
        StarboardService starboardService,
        DiscordShardedClient client,
        IDashboardAuditContext auditContext)
    {
        this.starboardService = starboardService;
        this.client = client;
        this.auditContext = auditContext;
    }

    /// <summary>
    ///     Gets all starboard configurations for a guild
    /// </summary>
    [HttpGet("all")]
    public IActionResult GetStarboards(ulong guildId)
    {
        var starboards = starboardService.GetStarboards(guildId);
        return Ok(starboards);
    }

    /// <summary>
    ///     Creates a new starboard for a guild
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateStarboard(ulong guildId, [FromBody] StarboardCreateRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        try
        {
            var starboardId =
                await starboardService.CreateStarboard(guild, request.ChannelId, request.Emote, request.Threshold);
            return Ok(starboardId);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already used"))
        {
            return BadRequest("Emote is already in use by another starboard configuration.");
        }
    }

    /// <summary>
    ///     Deletes a starboard configuration
    /// </summary>
    [HttpDelete("{starboardId}")]
    public async Task<IActionResult> DeleteStarboard(ulong guildId, int starboardId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        auditContext.RecordBefore(starboardService.GetStarboards(guildId).FirstOrDefault(s => s.Id == starboardId));
        var success = await starboardService.DeleteStarboard(guild, starboardId);
        if (!success)
            return NotFound("Starboard configuration not found");

        return Ok();
    }

    /// <summary>
    ///     Sets whether bots are allowed to be starred for a specific starboard
    /// </summary>
    [HttpPost("{starboardId}/allow-bots")]
    public async Task<IActionResult> SetAllowBots(ulong guildId, int starboardId, [FromBody] bool allowed)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        auditContext.RecordBefore(starboardService.GetStarboards(guildId).FirstOrDefault(s => s.Id == starboardId));
        var success = await starboardService.SetAllowBots(guild, starboardId, allowed);
        if (!success)
            return NotFound("Starboard configuration not found");

        return Ok(allowed);
    }

    /// <summary>
    ///     Sets whether to remove starred messages when the original is deleted
    /// </summary>
    [HttpPost("{starboardId}/remove-on-delete")]
    public async Task<IActionResult> SetRemoveOnDelete(ulong guildId, int starboardId, [FromBody] bool removeOnDelete)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        auditContext.RecordBefore(starboardService.GetStarboards(guildId).FirstOrDefault(s => s.Id == starboardId));
        var success = await starboardService.SetRemoveOnDelete(guild, starboardId, removeOnDelete);
        if (!success)
            return NotFound("Starboard configuration not found");

        return Ok(removeOnDelete);
    }

    /// <summary>
    ///     Sets whether to remove starred messages when reactions are cleared
    /// </summary>
    [HttpPost("{starboardId}/remove-on-clear")]
    public async Task<IActionResult> SetRemoveOnClear(ulong guildId, int starboardId, [FromBody] bool removeOnClear)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        auditContext.RecordBefore(starboardService.GetStarboards(guildId).FirstOrDefault(s => s.Id == starboardId));
        var success = await starboardService.SetRemoveOnClear(guild, starboardId, removeOnClear);
        if (!success)
            return NotFound("Starboard configuration not found");

        return Ok(removeOnClear);
    }

    /// <summary>
    ///     Sets whether to remove starred messages when they fall below threshold
    /// </summary>
    [HttpPost("{starboardId}/remove-below-threshold")]
    public async Task<IActionResult> SetRemoveBelowThreshold(ulong guildId, int starboardId,
        [FromBody] bool removeBelowThreshold)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        auditContext.RecordBefore(starboardService.GetStarboards(guildId).FirstOrDefault(s => s.Id == starboardId));
        var success = await starboardService.SetRemoveBelowThreshold(guild, starboardId, removeBelowThreshold);
        if (!success)
            return NotFound("Starboard configuration not found");

        return Ok(removeBelowThreshold);
    }

    /// <summary>
    ///     Sets the repost threshold for a starboard
    /// </summary>
    [HttpPost("{starboardId}/repost-threshold")]
    public async Task<IActionResult> SetRepostThreshold(ulong guildId, int starboardId, [FromBody] int threshold)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        auditContext.RecordBefore(starboardService.GetStarboards(guildId).FirstOrDefault(s => s.Id == starboardId));
        var success = await starboardService.SetRepostThreshold(guild, starboardId, threshold);
        if (!success)
            return NotFound("Starboard configuration not found");

        return Ok(threshold);
    }

    /// <summary>
    ///     Sets the star threshold for a starboard
    /// </summary>
    [HttpPost("{starboardId}/star-threshold")]
    public async Task<IActionResult> SetStarThreshold(ulong guildId, int starboardId, [FromBody] int threshold)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        auditContext.RecordBefore(starboardService.GetStarboards(guildId).FirstOrDefault(s => s.Id == starboardId));
        var success = await starboardService.SetStarThreshold(guild, starboardId, threshold);
        if (!success)
            return NotFound("Starboard configuration not found");

        return Ok(threshold);
    }

    /// <summary>
    ///     Sets whether to use blacklist mode for channel checking
    /// </summary>
    [HttpPost("{starboardId}/use-blacklist")]
    public async Task<IActionResult> SetUseBlacklist(ulong guildId, int starboardId, [FromBody] bool useBlacklist)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        auditContext.RecordBefore(starboardService.GetStarboards(guildId).FirstOrDefault(s => s.Id == starboardId));
        var success = await starboardService.SetUseBlacklist(guild, starboardId, useBlacklist);
        if (!success)
            return NotFound("Starboard configuration not found");

        return Ok(useBlacklist);
    }

    /// <summary>
    ///     Toggles a channel in the starboard's check list
    /// </summary>
    [HttpPost("{starboardId}/toggle-channel")]
    public async Task<IActionResult> ToggleChannel(ulong guildId, int starboardId, [FromBody] ulong channelId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        auditContext.RecordBefore(starboardService.GetStarboards(guildId).FirstOrDefault(s => s.Id == starboardId));
        var result = await starboardService.ToggleChannel(guild, starboardId, channelId.ToString());
        if (result.Config == null)
            return NotFound("Starboard configuration not found");

        return Ok(new
        {
            wasAdded = result.WasAdded, config = result.Config
        });
    }

    /// <summary>
    ///     Gets recent starboard highlights for a guild
    /// </summary>
    [HttpGet("highlights")]
    public async Task<IActionResult> GetStarboardHighlights(ulong guildId, [FromQuery] int limit = 5)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var highlights = await starboardService.GetRecentHighlights(guildId, limit);
        return Ok(highlights);
    }

    /// <summary>
    ///     Adds an emote to an existing starboard configuration
    /// </summary>
    [HttpPost("{starboardId}/add-emote")]
    public async Task<IActionResult> AddEmoteToStarboard(ulong guildId, int starboardId, [FromBody] string emote)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        try
        {
            auditContext.RecordBefore(
                starboardService.GetStarboards(guildId).FirstOrDefault(s => s.Id == starboardId));
            var success = await starboardService.AddEmoteToStarboard(guild, starboardId, emote);
            if (!success)
                return NotFound("Starboard configuration not found");

            return Ok(emote);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already used"))
        {
            return BadRequest("Emote is already in use by another starboard configuration.");
        }
    }

    /// <summary>
    ///     Removes an emote from an existing starboard configuration
    /// </summary>
    [HttpPost("{starboardId}/remove-emote")]
    public async Task<IActionResult> RemoveEmoteFromStarboard(ulong guildId, int starboardId, [FromBody] string emote)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        try
        {
            auditContext.RecordBefore(
                starboardService.GetStarboards(guildId).FirstOrDefault(s => s.Id == starboardId));
            var success = await starboardService.RemoveEmoteFromStarboard(guild, starboardId, emote);
            if (!success)
                return NotFound("Emote not found in starboard configuration");

            return Ok(emote);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot remove the last emote"))
        {
            return BadRequest("Cannot remove the last emote from a starboard. Delete the starboard instead.");
        }
    }

    /// <summary>
    ///     Gets starboard statistics for a guild
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStarboardStats(ulong guildId)
    {
        var stats = await starboardService.GetStarboardStats(guildId);
        if (stats == null)
            return NotFound("No starboard statistics available for this guild.");

        return Ok(stats);
    }
}