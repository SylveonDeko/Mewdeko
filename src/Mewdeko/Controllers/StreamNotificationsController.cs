using LinqToDB.Async;
using Mewdeko.Controllers.Common.StreamNotifications;
using Mewdeko.Modules.Searches.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API Controller for managing stream notifications (Twitch, YouTube, etc.).
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class StreamNotificationsController(
    StreamNotificationService service,
    DiscordShardedClient client,
    IDataConnectionFactory dbFactory)
    : Controller
{
    /// <summary>
    ///     Gets all followed streams for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>List of followed streams</returns>
    [HttpGet]
    public async Task<IActionResult> GetFollowedStreams(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var streams = await db.FollowedStreams
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Username)
            .ToListAsync();

        var streamData = streams.Select((s, index) => new
        {
            index,
            s.Id,
            s.ChannelId,
            s.Username,
            s.Type,
            typeName = GetStreamTypeName(s.Type),
            s.OnlineMessage,
            s.OfflineMessage,
            s.DateAdded,
            channelName = client.GetGuild(guildId)?.GetTextChannel(s.ChannelId)?.Name ?? "Unknown"
        });

        return Ok(streamData);
    }

    /// <summary>
    ///     Follows a new stream
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="request">The follow stream request</param>
    /// <returns>Success or error response</returns>
    [HttpPost]
    public async Task<IActionResult> FollowStream(ulong guildId, [FromBody] FollowStreamRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest("Stream URL cannot be empty");

        var result = await service.FollowStream(guildId, request.ChannelId, request.Url);

        if (result == null)
            return BadRequest("Failed to follow stream. URL may be invalid or stream already followed.");

        return Ok(new
        {
            platform = result.StreamType.ToString(), username = result.Name, streamUrl = result.StreamUrl
        });
    }

    /// <summary>
    ///     Unfollows a stream by index
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="index">The index of the stream to unfollow</param>
    /// <returns>Success or error response</returns>
    [HttpDelete("{index:int}")]
    public async Task<IActionResult> UnfollowStream(ulong guildId, int index)
    {
        var result = await service.UnfollowStreamAsync(guildId, index);

        if (result == null)
            return NotFound("Stream not found at the specified index");

        return Ok();
    }

    /// <summary>
    ///     Clears all followed streams for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>Number of streams removed</returns>
    [HttpDelete]
    public async Task<IActionResult> ClearAllStreams(ulong guildId)
    {
        var count = await service.ClearAllStreams(guildId);
        return Ok(new
        {
            removedCount = count
        });
    }

    /// <summary>
    ///     Sets the online message for a specific stream
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="index">The index of the stream</param>
    /// <param name="message">The custom online message</param>
    /// <returns>Success or error response</returns>
    [HttpPut("{index:int}/onlineMessage")]
    public async Task<IActionResult> SetStreamOnlineMessage(ulong guildId, int index, [FromBody] string message)
    {
        var (success, _) = await service.SetStreamMessage(guildId, index, message);

        if (!success)
            return NotFound("Stream not found at the specified index");

        return Ok();
    }

    /// <summary>
    ///     Sets the offline message for a specific stream
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="index">The index of the stream</param>
    /// <param name="message">The custom offline message</param>
    /// <returns>Success or error response</returns>
    [HttpPut("{index:int}/offlineMessage")]
    public async Task<IActionResult> SetStreamOfflineMessage(ulong guildId, int index, [FromBody] string message)
    {
        var (success, _) = await service.SetStreamOfflineMessage(guildId, index, message);

        if (!success)
            return NotFound("Stream not found at the specified index");

        return Ok();
    }

    /// <summary>
    ///     Gets the global custom stream message for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The custom stream message</returns>
    [HttpGet("customMessage")]
    public async Task<IActionResult> GetCustomStreamMessage(ulong guildId)
    {
        var message = await service.GetCustomStreamMessageAsync(guildId);
        return Ok(message ?? "");
    }

    /// <summary>
    ///     Sets the global custom stream message for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="message">The custom message template</param>
    /// <returns>Success response</returns>
    [HttpPost("customMessage")]
    public async Task<IActionResult> SetCustomStreamMessage(ulong guildId, [FromBody] string message)
    {
        await service.SetCustomStreamMessageAsync(guildId, message);
        return Ok();
    }

    /// <summary>
    ///     Gets the offline notification setting for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>Whether offline notifications are enabled</returns>
    [HttpGet("offlineNotifications")]
    public async Task<IActionResult> GetOfflineNotificationSetting(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var config = await db.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
        return Ok(config?.NotifyStreamOffline ?? false);
    }

    /// <summary>
    ///     Toggles offline notifications for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>New offline notification status</returns>
    [HttpPost("offlineNotifications/toggle")]
    public async Task<IActionResult> ToggleOfflineNotifications(ulong guildId)
    {
        var newStatus = await service.ToggleStreamOffline(guildId);
        return Ok(new
        {
            offlineNotificationsEnabled = newStatus
        });
    }

    /// <summary>
    ///     Gets stream statistics for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>Stream statistics</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStreamStats(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var totalStreams = await db.FollowedStreams
            .CountAsync(x => x.GuildId == guildId);

        var streamsByType = await db.FollowedStreams
            .Where(x => x.GuildId == guildId)
            .GroupBy(x => x.Type)
            .Select(g => new
            {
                type = g.Key, typeName = GetStreamTypeName(g.Key), count = g.Count()
            })
            .ToListAsync();

        var streamsByChannel = await db.FollowedStreams
            .Where(x => x.GuildId == guildId)
            .GroupBy(x => x.ChannelId)
            .Select(g => new
            {
                channelId = g.Key, count = g.Count()
            })
            .ToListAsync();

        var oldestStream = await db.FollowedStreams
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.DateAdded)
            .Select(s => new
            {
                s.Username, s.Type, typeName = GetStreamTypeName(s.Type), s.DateAdded
            })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            totalStreams, streamsByType, streamsByChannel, oldestStream
        });
    }

    /// <summary>
    ///     Gets unique streamers being followed
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>List of unique streamers</returns>
    [HttpGet("streamers")]
    public async Task<IActionResult> GetUniqueStreamers(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var uniqueStreamers = await db.FollowedStreams
            .Where(x => x.GuildId == guildId)
            .GroupBy(x => new
            {
                x.Username, x.Type
            })
            .Select(g => new
            {
                username = g.Key.Username,
                type = g.Key.Type,
                typeName = GetStreamTypeName(g.Key.Type),
                followCount = g.Count()
            })
            .ToListAsync();

        return Ok(uniqueStreamers);
    }

    /// <summary>
    ///     Helper method to get stream type name from type integer
    /// </summary>
    private static string GetStreamTypeName(int type)
    {
        return type switch
        {
            0 => "Twitch",
            1 => "YouTube",
            2 => "Trovo",
            3 => "Facebook",
            _ => "Unknown"
        };
    }
}