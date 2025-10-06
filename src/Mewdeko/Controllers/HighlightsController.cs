using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Highlights.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API Controller for managing guild-level highlights configuration and statistics.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class HighlightsController(
    HighlightsService service,
    DiscordShardedClient client,
    IDataConnectionFactory dbFactory)
    : Controller
{
    /// <summary>
    ///     Gets all highlights configured in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>List of all highlights in the guild</returns>
    [HttpGet]
    public async Task<IActionResult> GetAllHighlights(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var highlights = await db.Highlights
            .Where(h => h.GuildId == guildId)
            .OrderBy(h => h.UserId)
            .ThenBy(h => h.Word)
            .Select(h => new
            {
                h.Id, h.UserId, h.Word, h.DateAdded
            })
            .ToListAsync();

        var guild = client.GetGuild(guildId);
        var enrichedHighlights = highlights.Select(h => new
        {
            h.Id,
            h.UserId,
            username = guild?.GetUser(h.UserId)?.Username ?? "Unknown",
            h.Word,
            h.DateAdded
        });

        return Ok(enrichedHighlights);
    }

    /// <summary>
    ///     Gets highlights for a specific user in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="userId">The ID of the user</param>
    /// <returns>List of user's highlights</returns>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserHighlights(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var highlights = await db.Highlights
            .Where(h => h.GuildId == guildId && h.UserId == userId)
            .Select(h => new
            {
                h.Id, h.Word, h.DateAdded
            })
            .ToListAsync();

        return Ok(highlights);
    }

    /// <summary>
    ///     Gets highlight settings for a specific user
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="userId">The ID of the user</param>
    /// <returns>User's highlight settings</returns>
    [HttpGet("user/{userId}/settings")]
    public async Task<IActionResult> GetUserHighlightSettings(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var settings = await db.HighlightSettings
            .FirstOrDefaultAsync(h => h.GuildId == guildId && h.UserId == userId);

        return Ok(new
        {
            highlightsEnabled = settings?.HighlightsOn ?? true,
            ignoredChannels = settings?.IgnoredChannels?.Split(' ').Where(c => c != "0").ToList() ?? new List<string>(),
            ignoredUsers = settings?.IgnoredUsers?.Split(' ').Where(u => u != "0").ToList() ?? new List<string>()
        });
    }

    /// <summary>
    ///     Gets highlight statistics for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>Highlight statistics</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetHighlightStats(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var totalHighlights = await db.Highlights
            .CountAsync(h => h.GuildId == guildId);

        var totalUsers = await db.Highlights
            .Where(h => h.GuildId == guildId)
            .Select(h => h.UserId)
            .Distinct()
            .CountAsync();

        var topHighlightedWords = await db.Highlights
            .Where(h => h.GuildId == guildId)
            .GroupBy(h => h.Word.ToLower())
            .Select(g => new
            {
                word = g.Key, count = g.Count()
            })
            .OrderByDescending(x => x.count)
            .Take(10)
            .ToListAsync();

        var topUsers = await db.Highlights
            .Where(h => h.GuildId == guildId)
            .GroupBy(h => h.UserId)
            .Select(g => new
            {
                userId = g.Key, highlightCount = g.Count()
            })
            .OrderByDescending(x => x.highlightCount)
            .Take(10)
            .ToListAsync();

        var recentHighlights = await db.Highlights
            .Where(h => h.GuildId == guildId)
            .OrderByDescending(h => h.DateAdded)
            .Take(10)
            .Select(h => new
            {
                h.UserId, h.Word, h.DateAdded
            })
            .ToListAsync();

        return Ok(new
        {
            totalHighlights,
            totalUsers,
            topHighlightedWords,
            topUsers,
            recentHighlights
        });
    }

    /// <summary>
    ///     Removes a specific highlight by ID
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="highlightId">The ID of the highlight to remove</param>
    /// <returns>Success response</returns>
    [HttpDelete("{highlightId:int}")]
    public async Task<IActionResult> RemoveHighlight(ulong guildId, int highlightId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var highlight = await db.Highlights
            .FirstOrDefaultAsync(h => h.Id == highlightId && h.GuildId == guildId);

        if (highlight == null)
            return NotFound("Highlight not found");

        await service.RemoveHighlight(highlight);
        return Ok();
    }

    /// <summary>
    ///     Removes all highlights for a specific user in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="userId">The ID of the user</param>
    /// <returns>Number of highlights removed</returns>
    [HttpDelete("user/{userId}")]
    public async Task<IActionResult> RemoveUserHighlights(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var count = await db.Highlights
            .Where(h => h.GuildId == guildId && h.UserId == userId)
            .DeleteAsync();

        return Ok(new
        {
            removedCount = count
        });
    }

    /// <summary>
    ///     Gets users with highlights disabled
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>List of users with highlights disabled</returns>
    [HttpGet("disabled")]
    public async Task<IActionResult> GetDisabledUsers(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var disabledUsers = await db.HighlightSettings
            .Where(h => h.GuildId == guildId && !h.HighlightsOn)
            .Select(h => new
            {
                h.UserId, h.IgnoredChannels, h.IgnoredUsers
            })
            .ToListAsync();

        var guild = client.GetGuild(guildId);
        var enrichedUsers = disabledUsers.Select(u => new
        {
            u.UserId,
            username = guild?.GetUser(u.UserId)?.Username ?? "Unknown",
            ignoredChannelsCount = u.IgnoredChannels?.Split(' ').Count(c => c != "0") ?? 0,
            ignoredUsersCount = u.IgnoredUsers?.Split(' ').Count(usr => usr != "0") ?? 0
        });

        return Ok(enrichedUsers);
    }

    /// <summary>
    ///     Searches for highlights matching a pattern
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="searchTerm">The term to search for</param>
    /// <returns>Matching highlights</returns>
    [HttpGet("search")]
    public async Task<IActionResult> SearchHighlights(ulong guildId, [FromQuery] string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return BadRequest("Search term cannot be empty");

        await using var db = await dbFactory.CreateConnectionAsync();

        var highlights = await db.Highlights
            .Where(h => h.GuildId == guildId && h.Word.ToLower().Contains(searchTerm.ToLower()))
            .Select(h => new
            {
                h.Id, h.UserId, h.Word, h.DateAdded
            })
            .ToListAsync();

        var guild = client.GetGuild(guildId);
        var enrichedHighlights = highlights.Select(h => new
        {
            h.Id,
            h.UserId,
            username = guild?.GetUser(h.UserId)?.Username ?? "Unknown",
            h.Word,
            h.DateAdded
        });

        return Ok(enrichedHighlights);
    }
}