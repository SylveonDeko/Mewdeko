using Mewdeko.Modules.Utility.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for message count statistics and analytics
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class MessageCountController(
    DiscordShardedClient client,
    MessageCountService messageCountService,
    IDashboardAuditContext auditContext,
    IMemoryCache cache) : Controller
{
    private static string StatsCacheKey(ulong guildId)
    {
        return $"messagecount:dashboard:stats:{guildId}";
    }

    /// <summary>
    ///     Gets daily message statistics for the guild
    /// </summary>
    [HttpGet("daily")]
    public async Task<IActionResult> GetDailyMessageStats(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null) return NotFound("Guild not found");

        var (counts, enabled) = await messageCountService.GetAllCountsForEntity(
            MessageCountService.CountQueryType.Guild, guildId, guildId);

        if (!enabled)
            return Ok(new
            {
                enabled = false, dailyMessages = 0, totalMessages = 0
            });

        // Calculate messages from the last 24 hours
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var dailyMessages = counts.Where(c => c.DateAdded >= yesterday).Sum(c => (long)c.Count);
        var totalMessages = counts.Sum(c => (long)c.Count);

        return Ok(new
        {
            enabled = true, dailyMessages, totalMessages, lastUpdated = DateTime.UtcNow
        });
    }

    /// <summary>
    ///     Gets message count statistics for a specific channel
    /// </summary>
    [HttpGet("channel/{channelId}")]
    public async Task<IActionResult> GetChannelMessageStats(ulong guildId, ulong channelId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null) return NotFound("Guild not found");

        var channel = guild.GetTextChannel(channelId);
        if (channel == null) return NotFound("Channel not found");

        var (counts, enabled) = await messageCountService.GetAllCountsForEntity(
            MessageCountService.CountQueryType.Channel, channelId, guildId);

        if (!enabled)
            return Ok(new
            {
                enabled = false, totalMessages = 0
            });

        var totalMessages = counts.Sum(c => (long)c.Count);
        var dailyMessages = counts.Where(c => c.DateAdded >= DateTime.UtcNow.AddDays(-1)).Sum(c => (long)c.Count);

        return Ok(new
        {
            enabled = true,
            channelId = channelId.ToString(),
            channelName = channel.Name,
            totalMessages,
            dailyMessages,
            lastUpdated = DateTime.UtcNow
        });
    }

    /// <summary>
    ///     Gets message count statistics for a specific user
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserMessageStats(ulong guildId, ulong userId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null) return NotFound("Guild not found");

        var (counts, enabled) = await messageCountService.GetAllCountsForEntity(
            MessageCountService.CountQueryType.User, userId, guildId);

        if (!enabled)
            return Ok(new
            {
                enabled = false, totalMessages = 0
            });

        var totalMessages = counts.Sum(c => (long)c.Count);
        var dailyMessages = counts.Where(c => c.DateAdded >= DateTime.UtcNow.AddDays(-1)).Sum(c => (long)c.Count);

        return Ok(new
        {
            enabled = true,
            userId = userId.ToString(),
            totalMessages,
            dailyMessages,
            lastUpdated = DateTime.UtcNow
        });
    }

    /// <summary>
    ///     Gets top users by message count
    /// </summary>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetMessageLeaderboard(ulong guildId, [FromQuery] int limit = 10)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null) return NotFound("Guild not found");

        var (counts, enabled) = await messageCountService.GetAllCountsForEntity(
            MessageCountService.CountQueryType.Guild, guildId, guildId);

        if (!enabled)
            return Ok(new
            {
                enabled = false, leaderboard = Array.Empty<object>()
            });

        var userGroups = counts.GroupBy(c => c.UserId)
            .Select(g => new
            {
                UserId = g.Key.ToString(),
                TotalMessages = g.Sum(x => (long)x.Count),
                DailyMessages = g.Where(x => x.DateAdded >= DateTime.UtcNow.AddDays(-1)).Sum(x => (long)x.Count)
            })
            .OrderByDescending(u => u.TotalMessages)
            .Take(limit)
            .ToList();

        return Ok(new
        {
            enabled = true, leaderboard = userGroups, lastUpdated = DateTime.UtcNow
        });
    }

    /// <summary>
    ///     Gets comprehensive message statistics for the guild
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetMessageStats(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null) return NotFound("Guild not found");

        if (cache.TryGetValue(StatsCacheKey(guildId), out var cachedStats))
            return Ok(cachedStats);

        var (counts, enabled) = await messageCountService.GetAllCountsForEntity(
            MessageCountService.CountQueryType.Guild, guildId, guildId);

        if (!enabled)
        {
            var disabledStats = new
            {
                enabled = false,
                topUsers = Array.Empty<object>(),
                topChannels = Array.Empty<object>(),
                leastActiveUser = (object?)null,
                leastActiveChannel = (object?)null,
                busiestHours = Array.Empty<object>(),
                busiestDays = Array.Empty<object>(),
                dailyMessages = 0,
                totalMessages = 0
            };

            cache.Set(StatsCacheKey(guildId), disabledStats, TimeSpan.FromSeconds(15));
            return Ok(disabledStats);
        }

        var totalMessages = counts.Sum(c => (long)c.Count);
        var dayCutoff = DateTime.UtcNow.AddDays(-1);

        // Get top users
        var topUsers = counts.GroupBy(c => c.UserId)
            .Select(g => new
            {
                userId = g.Key.ToString(),
                totalMessages = g.Sum(x => (long)x.Count),
                dailyMessages = g.Where(x => x.DateAdded >= dayCutoff).Sum(x => (long)x.Count),
                percentage = totalMessages > 0 ? Math.Round(g.Sum(x => (long)x.Count) * 100.0 / totalMessages, 2) : 0
            })
            .OrderByDescending(u => u.totalMessages)
            .ToArray();

        // Get top channels
        var topChannels = counts.GroupBy(c => c.ChannelId)
            .Where(g => g.Key != 0) // Filter out entries without channel
            .Select(g => new
            {
                channelId = g.Key.ToString(),
                channelName = guild.GetTextChannel(g.Key)?.Name ?? "Unknown Channel",
                totalMessages = g.Sum(x => (long)x.Count),
                dailyMessages = g.Where(x => x.DateAdded >= dayCutoff).Sum(x => (long)x.Count),
                percentage = totalMessages > 0 ? Math.Round(g.Sum(x => (long)x.Count) * 100.0 / totalMessages, 2) : 0
            })
            .OrderByDescending(c => c.totalMessages)
            .ToArray();

        var busiestHours = (await messageCountService.GetBusiestHours(guildId))
            .OrderBy(h => h.Hour)
            .Select(h => new
            {
                hour = h.Hour, messageCount = h.Count
            })
            .ToArray();

        var busiestDays = (await messageCountService.GetBusiestDays(guildId))
            .OrderBy(d => (int)d.Day)
            .Select(d => new
            {
                day = d.Day.ToString(), messageCount = d.Count
            })
            .ToArray();

        var dailyMessages = counts.Where(c => c.DateAdded >= dayCutoff).Sum(c => (long)c.Count);

        var stats = new
        {
            enabled = true,
            topUsers = topUsers.Take(10).ToArray(),
            topChannels = topChannels.Take(10).ToArray(),
            leastActiveUser = topUsers.LastOrDefault(),
            leastActiveChannel = topChannels.LastOrDefault(),
            busiestHours,
            busiestDays,
            dailyMessages,
            totalMessages,
            lastUpdated = DateTime.UtcNow
        };

        cache.Set(StatsCacheKey(guildId), stats, TimeSpan.FromSeconds(15));
        return Ok(stats);
    }

    /// <summary>
    ///     Gets message count status for the guild
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetMessageCountStatus(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null) return NotFound("Guild not found");

        var (_, enabled) = await messageCountService.GetAllCountsForEntity(
            MessageCountService.CountQueryType.Guild, guildId, guildId);

        return Ok(new
        {
            enabled
        });
    }

    /// <summary>
    ///     Toggles message counting for the guild
    /// </summary>
    [HttpPost("toggle")]
    public async Task<IActionResult> ToggleMessageCount(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null) return NotFound("Guild not found");

        try
        {
            var enabled = await messageCountService.ToggleGuildMessageCount(guildId);
            cache.Remove(StatsCacheKey(guildId));
            auditContext.RecordBefore(!enabled);
            auditContext.RecordAfter(enabled);
            return Ok(new
            {
                enabled, message = enabled ? "Message counting enabled" : "Message counting disabled"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                error = ex.Message
            });
        }
    }

    /// <summary>
    ///     Resets message counts for the guild
    /// </summary>
    [HttpPost("reset")]
    public async Task<IActionResult> ResetMessageCounts(ulong guildId, [FromQuery] ulong? userId = null,
        [FromQuery] ulong? channelId = null)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null) return NotFound("Guild not found");

        try
        {
            var removedAny = await messageCountService.ResetCount(guildId, userId, channelId);
            cache.Remove(StatsCacheKey(guildId));

            var message = (userId, channelId) switch
            {
                (not null, not null) => $"Reset message counts for user in channel",
                (not null, null) => $"Reset message counts for user",
                (null, not null) => $"Reset message counts for channel",
                _ => "Reset all message counts for guild"
            };

            return Ok(new
            {
                message, removedAny
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                error = ex.Message
            });
        }
    }
}