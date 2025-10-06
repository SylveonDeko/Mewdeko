using LinqToDB.Async;
using Mewdeko.Controllers.Common.Feeds;
using Mewdeko.Modules.Searches.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API Controller for managing RSS feed subscriptions.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class FeedsController(
    FeedsService service,
    DiscordShardedClient client,
    IDataConnectionFactory dbFactory)
    : Controller
{
    /// <summary>
    ///     Gets all RSS feed subscriptions for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>List of feed subscriptions</returns>
    [HttpGet]
    public async Task<IActionResult> GetFeeds(ulong guildId)
    {
        var feeds = await service.GetFeeds(guildId);

        var feedData = feeds.Select((f, index) => new
        {
            index,
            f.Id,
            f.ChannelId,
            f.Url,
            f.Message,
            f.DateAdded,
            channelName = client.GetGuild(guildId)?.GetTextChannel(f.ChannelId)?.Name ?? "Unknown"
        });

        return Ok(feedData);
    }

    /// <summary>
    ///     Adds a new RSS feed subscription
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="request">The feed subscription request</param>
    /// <returns>Success or error response</returns>
    [HttpPost]
    public async Task<IActionResult> AddFeed(ulong guildId, [FromBody] AddFeedRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest("Feed URL cannot be empty");

        var success = await service.AddFeed(guildId, request.ChannelId, request.Url);

        if (!success)
            return BadRequest("Failed to add feed. Feed may already exist or URL is invalid.");

        return Ok();
    }

    /// <summary>
    ///     Updates the custom message for a feed subscription
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="index">The index of the feed in the guild's feed list</param>
    /// <param name="message">The custom message to display when feed updates</param>
    /// <returns>Success or error response</returns>
    [HttpPut("{index:int}/message")]
    public async Task<IActionResult> SetFeedMessage(ulong guildId, int index, [FromBody] string message)
    {
        var success = await service.AddFeedMessage(guildId, index, message);

        if (!success)
            return NotFound("Feed not found at the specified index");

        return Ok();
    }

    /// <summary>
    ///     Removes a feed subscription
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="index">The index of the feed to remove</param>
    /// <returns>Success or error response</returns>
    [HttpDelete("{index:int}")]
    public async Task<IActionResult> RemoveFeed(ulong guildId, int index)
    {
        var success = await service.RemoveFeed(guildId, index);

        if (!success)
            return NotFound("Feed not found at the specified index");

        return Ok();
    }

    /// <summary>
    ///     Gets feed statistics for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>Feed statistics</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetFeedStats(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var totalFeeds = await db.FeedSubs
            .CountAsync(x => x.GuildId == guildId);

        var feedsByChannel = await db.FeedSubs
            .Where(x => x.GuildId == guildId)
            .GroupBy(x => x.ChannelId)
            .Select(g => new
            {
                channelId = g.Key, count = g.Count()
            })
            .ToListAsync();

        var oldestFeed = await db.FeedSubs
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.DateAdded)
            .Select(f => new
            {
                f.Url, f.DateAdded
            })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            totalFeeds, feedsByChannel, oldestFeed
        });
    }

    /// <summary>
    ///     Gets all unique feed URLs subscribed across the guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>List of unique feed URLs</returns>
    [HttpGet("urls")]
    public async Task<IActionResult> GetUniqueFeedUrls(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var uniqueUrls = await db.FeedSubs
            .Where(x => x.GuildId == guildId)
            .Select(x => x.Url)
            .Distinct()
            .ToListAsync();

        return Ok(uniqueUrls);
    }
}