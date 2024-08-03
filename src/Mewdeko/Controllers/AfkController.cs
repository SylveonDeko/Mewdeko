using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Afk.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing afk stuff for the bot
/// </summary>
/// <param name="afk">The afk service</param>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
public class AfkController(AfkService afk, DiscordShardedClient client, DbContextProvider provider) : Controller
{
    /// <summary>
    ///     Gets a users afk status.
    /// </summary>
    /// <param name="guildId">The guildid to check the users afk in</param>
    /// <param name="userId">The user to check afk for</param>
    /// <returns>Returns a <see cref="Afk" /> or a 404 if the user has never had an afk in that guild before.</returns>
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetAfkStatus(ulong guildId, ulong userId)
    {
        var afkStatus = await afk.GetAfk(guildId, userId);

        if (afkStatus == null)
            return NotFound();

        return Ok(afkStatus);
    }

    /// <summary>
    ///     Sets a users afk status
    /// </summary>
    /// <param name="guildId">The guild to set the afk for</param>
    /// <param name="userId">The userid of the user to set the afk for</param>
    /// <param name="message">The afk message to set.</param>
    /// <returns>A 200 status code</returns>
    [HttpPost("{userId}")]
    public async Task<IActionResult> SetAfkStatus(ulong guildId, ulong userId, [FromBody] string message)
    {
        await afk.AfkSet(guildId, userId, message);

        return Ok();
    }

    /// <summary>
    ///     Removes a users afk status.
    /// </summary>
    /// <param name="guildId">The guild id to remove a users afk status for</param>
    /// <param name="userId">The userid of the user to remove the afk status for</param>
    /// <returns></returns>
    [HttpDelete("{userId}")]
    public async Task<IActionResult> DeleteAfkStatus(ulong guildId, ulong userId)
    {
        await afk.AfkSet(guildId, userId, "");

        return Ok();
    }

    /// <summary>
    ///     Retrieves all latest afks within a guild
    /// </summary>
    /// <param name="guildId">The guild to retrieve statuses for</param>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> GetAllAfkStatus(ulong guildId)
    {
        await using var db = await provider.GetContextAsync();
        var guild = client.GetGuild(guildId);

        var users = guild.Users;

        var latestAfks = await db.Afk
            .Where(x => x.GuildId == guildId)
            .GroupBy(x => x.UserId)
            .Select(g => g.OrderByDescending(x => x.DateAdded).FirstOrDefault())
            .ToListAsync();

        var result = users.Select(user => new
        {
            UserId = user.Id,
            user.Username,
            user.Nickname,
            AvatarUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl(),
            AfkStatus = latestAfks.FirstOrDefault(a => a.UserId == user.Id)
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    ///     Gets the auto-deletion time for AFK messages in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get the deletion time for</param>
    /// <returns>The deletion time in seconds</returns>
    [HttpGet("deletion")]
    public async Task<IActionResult> GetAfkDel(ulong guildId)
    {
        var deletionTime = await afk.GetAfkDel(guildId);
        return Ok(deletionTime);
    }

    /// <summary>
    ///     Sets the auto-deletion time for AFK messages in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to set the deletion time for</param>
    /// <param name="time">The deletion time in seconds</param>
    /// <returns>A 200 status code</returns>
    [HttpPost("deletion")]
    public async Task<IActionResult> AfkDelSet(ulong guildId, [FromBody] int time)
    {
        var guild = client.GetGuild(guildId);
        await afk.AfkDelSet(guild, time);
        return Ok();
    }

    /// <summary>
    ///     Gets the maximum length for AFK messages in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get the max length for</param>
    /// <returns>The maximum length for AFK messages</returns>
    [HttpGet("length")]
    public async Task<IActionResult> GetAfkLength(ulong guildId)
    {
        var maxLength = await afk.GetAfkLength(guildId);
        return Ok(maxLength);
    }

    /// <summary>
    ///     Sets the maximum length for AFK messages in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to set the max length for</param>
    /// <param name="length">The maximum length for AFK messages</param>
    /// <returns>A 200 status code</returns>
    [HttpPost("length")]
    public async Task<IActionResult> AfkLengthSet(ulong guildId, [FromBody] int length)
    {
        var guild = client.GetGuild(guildId);
        await afk.AfkLengthSet(guild, length);
        return Ok();
    }

    /// <summary>
    ///     Gets the AFK type for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get the AFK type for</param>
    /// <returns>The AFK type</returns>
    [HttpGet("type")]
    public async Task<IActionResult> GetAfkType(ulong guildId)
    {
        var afkType = await afk.GetAfkType(guildId);
        return Ok(afkType);
    }

    /// <summary>
    ///     Sets the AFK type for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to set the AFK type for</param>
    /// <param name="type">The AFK type to set</param>
    /// <returns>A 200 status code</returns>
    [HttpPost("type")]
    public async Task<IActionResult> AfkTypeSet(ulong guildId, [FromBody] int type)
    {
        var guild = client.GetGuild(guildId);
        await afk.AfkTypeSet(guild, type);
        return Ok();
    }

    /// <summary>
    ///     Gets the AFK timeout for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get the AFK timeout for</param>
    /// <returns>The AFK timeout in seconds</returns>
    [HttpGet("timeout")]
    public async Task<IActionResult> GetAfkTimeout(ulong guildId)
    {
        var timeout = await afk.GetAfkTimeout(guildId);
        return Ok(timeout);
    }

    /// <summary>
    ///     Sets the AFK timeout for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to set the AFK timeout for</param>
    /// <param name="timeout">The AFK timeout in seconds</param>
    /// <returns>A 200 status code</returns>
    [HttpPost("timeout")]
    public async Task<IActionResult> AfkTimeoutSet(ulong guildId, [FromBody] string timeout)
    {
        var stoopidTime = StoopidTime.FromInput(timeout);
        var timeoutSeconds = (int)stoopidTime.Time.TotalSeconds;
        var guild = client.GetGuild(guildId);
        await afk.AfkTimeoutSet(guild, timeoutSeconds);
        return Ok();
    }

    /// <summary>
    ///     Gets the disabled AFK channels for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get the disabled channels for</param>
    /// <returns>A comma-separated string of disabled channel IDs</returns>
    [HttpGet("disabled-channels")]
    public async Task<IActionResult> GetDisabledAfkChannels(ulong guildId)
    {
        var disabledChannels = await afk.GetDisabledAfkChannels(guildId);
        return Ok(disabledChannels);
    }

    /// <summary>
    ///     Sets the disabled AFK channels for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to set the disabled channels for</param>
    /// <param name="channels">A comma-separated string of channel IDs to disable</param>
    /// <returns>A 200 status code</returns>
    [HttpPost("disabled-channels")]
    public async Task<IActionResult> AfkDisabledSet(ulong guildId, [FromBody] string channels)
    {
        var guild = client.GetGuild(guildId);
        await afk.AfkDisabledSet(guild, channels);
        return Ok();
    }

    /// <summary>
    ///     Gets the custom AFK message for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get the custom message for</param>
    /// <returns>The custom AFK message</returns>
    [HttpGet("custom-message")]
    public async Task<IActionResult> GetCustomAfkMessage(ulong guildId)
    {
        var customMessage = await afk.GetCustomAfkMessage(guildId);
        return Ok(customMessage == "-" ? "" : customMessage);
    }

    /// <summary>
    ///     Sets the custom AFK message for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to set the custom message for</param>
    /// <param name="message">The custom AFK message to set</param>
    /// <returns>A 200 status code</returns>
    [HttpPost("custom-message")]
    public async Task<IActionResult> SetCustomAfkMessage(ulong guildId, [FromBody] string message)
    {
        var guild = client.GetGuild(guildId);
        await afk.SetCustomAfkMessage(guild, message);
        return Ok();
    }
}