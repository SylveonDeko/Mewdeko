using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Confessions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API Controller for managing anonymous confessions.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class ConfessionsController(
    ConfessionService service,
    GuildSettingsService guildSettings,
    DiscordShardedClient client,
    IDataConnectionFactory dbFactory)
    : Controller
{
    /// <summary>
    ///     Gets all confessions for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>List of confessions</returns>
    [HttpGet]
    public async Task<IActionResult> GetConfessions(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var confessions = await db.Confessions
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.ConfessNumber)
            .Select(c => new
            {
                c.Id,
                c.ConfessNumber,
                c.Confession1,
                c.DateAdded,
                c.MessageId,
                c.ChannelId
            })
            .ToListAsync();

        return Ok(confessions);
    }

    /// <summary>
    ///     Gets a specific confession by number
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="confessionNumber">The confession number</param>
    /// <returns>The confession details</returns>
    [HttpGet("{confessionNumber}")]
    public async Task<IActionResult> GetConfession(ulong guildId, ulong confessionNumber)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var confession = await db.Confessions
            .Where(x => x.GuildId == guildId && x.ConfessNumber == confessionNumber)
            .Select(c => new
            {
                c.Id,
                c.ConfessNumber,
                c.Confession1,
                c.DateAdded,
                c.MessageId,
                c.ChannelId,
                c.UserId
            })
            .FirstOrDefaultAsync();

        if (confession == null)
            return NotFound("Confession not found");

        return Ok(confession);
    }

    /// <summary>
    ///     Gets the confession channel for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The ID of the confession channel</returns>
    [HttpGet("channel")]
    public async Task<IActionResult> GetConfessionChannel(ulong guildId)
    {
        var config = await guildSettings.GetGuildConfig(guildId);
        return Ok(config.ConfessionChannel);
    }

    /// <summary>
    ///     Sets the confession channel for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The ID of the confession channel</param>
    /// <returns>Success response</returns>
    [HttpPost("channel")]
    public async Task<IActionResult> SetConfessionChannel(ulong guildId, [FromBody] ulong channelId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        await service.SetConfessionChannel(guild, channelId);
        return Ok();
    }

    /// <summary>
    ///     Gets the confession log channel for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The ID of the confession log channel</returns>
    [HttpGet("logChannel")]
    public async Task<IActionResult> GetConfessionLogChannel(ulong guildId)
    {
        var config = await guildSettings.GetGuildConfig(guildId);
        return Ok(config.ConfessionLogChannel);
    }

    /// <summary>
    ///     Sets the confession log channel for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The ID of the confession log channel</param>
    /// <returns>Success response</returns>
    [HttpPost("logChannel")]
    public async Task<IActionResult> SetConfessionLogChannel(ulong guildId, [FromBody] ulong channelId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        await service.SetConfessionLogChannel(guild, channelId);
        return Ok();
    }

    /// <summary>
    ///     Gets the confession blacklist for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>List of blacklisted role IDs</returns>
    [HttpGet("blacklist")]
    public async Task<IActionResult> GetConfessionBlacklist(ulong guildId)
    {
        var config = await guildSettings.GetGuildConfig(guildId);
        var blacklist = config.GetConfessionBlacklists();
        return Ok(blacklist);
    }

    /// <summary>
    ///     Toggles a role in the confession blacklist
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="roleId">The ID of the role to toggle</param>
    /// <returns>Success response with updated blacklist status</returns>
    [HttpPost("blacklist/{roleId}")]
    public async Task<IActionResult> ToggleConfessionBlacklist(ulong guildId, ulong roleId)
    {
        await service.ToggleUserBlacklistAsync(guildId, roleId);

        var config = await guildSettings.GetGuildConfig(guildId);
        var blacklist = config.GetConfessionBlacklists();
        var isBlacklisted = blacklist.Contains(roleId);

        return Ok(new
        {
            roleId, isBlacklisted
        });
    }

    /// <summary>
    ///     Gets confession statistics for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>Confession statistics</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetConfessionStats(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var totalConfessions = await db.Confessions
            .CountAsync(x => x.GuildId == guildId);

        var confessionsThisMonth = await db.Confessions
            .CountAsync(x => x.GuildId == guildId &&
                             x.DateAdded.Value.Month == DateTime.UtcNow.Month &&
                             x.DateAdded.Value.Year == DateTime.UtcNow.Year);

        var confessionsToday = await db.Confessions
            .CountAsync(x => x.GuildId == guildId &&
                             x.DateAdded.Value.Date == DateTime.UtcNow.Date);

        var lastConfession = await db.Confessions
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.ConfessNumber)
            .Select(c => new
            {
                c.ConfessNumber, c.DateAdded
            })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            totalConfessions,
            confessionsThisMonth,
            confessionsToday,
            lastConfessionNumber = lastConfession?.ConfessNumber ?? 0,
            lastConfessionDate = lastConfession?.DateAdded
        });
    }

    /// <summary>
    ///     Deletes a confession by number
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="confessionNumber">The confession number to delete</param>
    /// <returns>Success response</returns>
    [HttpDelete("{confessionNumber}")]
    public async Task<IActionResult> DeleteConfession(ulong guildId, ulong confessionNumber)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var confession = await db.Confessions
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.ConfessNumber == confessionNumber);

        if (confession == null)
            return NotFound("Confession not found");

        await db.DeleteAsync(confession);

        // Try to delete the message if it still exists
        var guild = client.GetGuild(guildId);
        if (guild != null)
        {
            var channel = guild.GetTextChannel(confession.ChannelId);
            if (channel != null)
            {
                try
                {
                    var message = await channel.GetMessageAsync(confession.MessageId);
                    if (message != null)
                        await message.DeleteAsync();
                }
                catch
                {
                    // Message already deleted or inaccessible
                }
            }
        }

        return Ok();
    }
}