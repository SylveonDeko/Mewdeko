using DataModel;
using LinqToDB;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Afk.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing AFK (Away From Keyboard) statuses and settings via API.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class AfkController : Controller
{
    private readonly AfkService afk;
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<AfkController> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AfkController" /> class.
    /// </summary>
    /// <param name="afk">The AFK service instance.</param>
    /// <param name="client">The Discord sharded client instance.</param>
    /// <param name="dbFactory">The factory for creating database connections.</param>
    /// <param name="logger">Logger for this class.</param>
    public AfkController(AfkService afk, DiscordShardedClient client, IDataConnectionFactory dbFactory,
        ILogger<AfkController> logger)
    {
        this.afk = afk;
        this.client = client;
        this.dbFactory = dbFactory;
        this.logger = logger;
    }

    /// <summary>
    ///     Gets a specific user's AFK status in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to check within.</param>
    /// <param name="userId">The ID of the user whose AFK status is requested.</param>
    /// <returns>
    ///     An <see cref="IActionResult" /> containing the <see cref="Afk" /> status if found, otherwise
    ///     <see cref="NotFoundResult" />.
    /// </returns>
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetAfkStatus(ulong guildId, ulong userId)
    {
        var afkStatus = await afk.GetAfk(guildId, userId);

        if (afkStatus == null)
            return NotFound();

        return Ok(afkStatus);
    }

    /// <summary>
    ///     Sets a user's AFK status in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="message">The AFK message to set.</param>
    /// <returns>An <see cref="OkResult" /> indicating success.</returns>
    [HttpPost("{userId}")]
    public async Task<IActionResult> SetAfkStatus(ulong guildId, ulong userId, [FromBody] string message)
    {
        await afk.AfkSet(guildId, userId, message);
        return Ok();
    }

    /// <summary>
    ///     Removes a user's AFK status by setting an empty message.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>An <see cref="OkResult" /> indicating success.</returns>
    [HttpDelete("{userId}")]
    public async Task<IActionResult> DeleteAfkStatus(ulong guildId, ulong userId)
    {
        await afk.AfkSet(guildId, userId, ""); // Setting empty message clears AFK
        return Ok();
    }

    /// <summary>
    ///     Retrieves the latest AFK status for all users currently in the specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve statuses for.</param>
    /// <returns>
    ///     An <see cref="IActionResult" /> containing a list of users with their latest AFK status, or
    ///     <see cref="NotFoundResult" /> if the guild is not found.
    /// </returns>
    [HttpGet]
    public async Task<IActionResult> GetAllAfkStatus(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        // Fetch users first to avoid holding DB connection longer than needed
        var users = guild.Users;
        if (users is null || !users.Any())
            return Ok(Enumerable.Empty<object>()); // Return empty list if no users

        List<Afk> latestAfks;
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var allAfksInGuild = await db.Afks
                .Where(x => x.GuildId == guildId)
                .ToListAsync();

            // Group in memory to get the latest AFK per user
            latestAfks = allAfksInGuild
                .GroupBy(x => x.UserId)
                .Select(g => g.OrderByDescending(x => x.DateAdded).First())
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve AFK statuses for guild {GuildId}", guildId);
            return StatusCode(500, "Failed to retrieve AFK data."); // Internal Server Error
        }


        var result = users.Select(user => new
        {
            UserId = user.Id,
            user.Username,
            user.Nickname,
            AvatarUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl(),
            // Find the matching latest AFK status for this user
            AfkStatus = latestAfks.FirstOrDefault(a => a != null && a.UserId == user.Id)
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    ///     Gets the auto-deletion time for AFK messages in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get the deletion time for.</param>
    /// <returns>An <see cref="IActionResult" /> containing the deletion time in seconds.</returns>
    [HttpGet("deletion")]
    public async Task<IActionResult> GetAfkDel(ulong guildId)
    {
        var deletionTime = await afk.GetAfkDel(guildId);
        return Ok(deletionTime);
    }

    /// <summary>
    ///     Sets the auto-deletion time for AFK messages in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to set the deletion time for.</param>
    /// <param name="time">The deletion time in seconds.</param>
    /// <returns>An <see cref="OkResult" /> indicating success.</returns>
    [HttpPost("deletion")]
    public async Task<IActionResult> AfkDelSet(ulong guildId, [FromBody] int time)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null) return NotFound("Guild not found.");
        await afk.AfkDelSet(guild, time);
        return Ok();
    }

    /// <summary>
    ///     Gets the maximum length for AFK messages in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get the max length for.</param>
    /// <returns>An <see cref="IActionResult" /> containing the maximum length.</returns>
    [HttpGet("length")]
    public async Task<IActionResult> GetAfkLength(ulong guildId)
    {
        var maxLength = await afk.GetAfkLength(guildId);
        return Ok(maxLength);
    }

    /// <summary>
    ///     Sets the maximum length for AFK messages in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to set the max length for.</param>
    /// <param name="length">The maximum length.</param>
    /// <returns>An <see cref="OkResult" /> indicating success.</returns>
    [HttpPost("length")]
    public async Task<IActionResult> AfkLengthSet(ulong guildId, [FromBody] int length)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null) return NotFound("Guild not found.");
        await afk.AfkLengthSet(guild, length);
        return Ok();
    }

    /// <summary>
    ///     Gets the AFK type setting for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get the AFK type for.</param>
    /// <returns>An <see cref="IActionResult" /> containing the AFK type.</returns>
    [HttpGet("type")]
    public async Task<IActionResult> GetAfkType(ulong guildId)
    {
        var afkType = await afk.GetAfkType(guildId);
        return Ok(afkType);
    }

    /// <summary>
    ///     Sets the AFK type setting for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to set the AFK type for.</param>
    /// <param name="type">The AFK type to set.</param>
    /// <returns>An <see cref="OkResult" /> indicating success.</returns>
    [HttpPost("type")]
    public async Task<IActionResult> AfkTypeSet(ulong guildId, [FromBody] int type)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null) return NotFound("Guild not found.");
        await afk.AfkTypeSet(guild, type);
        return Ok();
    }

    /// <summary>
    ///     Gets the AFK timeout setting for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get the AFK timeout for.</param>
    /// <returns>An <see cref="IActionResult" /> containing the AFK timeout in seconds.</returns>
    [HttpGet("timeout")]
    public async Task<IActionResult> GetAfkTimeout(ulong guildId)
    {
        var timeout = await afk.GetAfkTimeout(guildId);
        return Ok(timeout);
    }

    /// <summary>
    ///     Sets the AFK timeout setting for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to set the AFK timeout for.</param>
    /// <param name="timeout">The AFK timeout as a string (e.g., "5m", "1h").</param>
    /// <returns>An <see cref="OkResult" /> indicating success.</returns>
    [HttpPost("timeout")]
    public async Task<IActionResult> AfkTimeoutSet(ulong guildId, [FromBody] string timeout)
    {
        var stoopidTime = StoopidTime.FromInput(timeout); // Assuming StoopidTime is accessible
        var timeoutSeconds = (int)stoopidTime.Time.TotalSeconds;
        var guild = client.GetGuild(guildId);
        if (guild is null) return NotFound("Guild not found.");
        await afk.AfkTimeoutSet(guild, timeoutSeconds);
        return Ok();
    }

    /// <summary>
    ///     Gets the disabled AFK channels for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get the disabled channels for.</param>
    /// <returns>An <see cref="IActionResult" /> containing a comma-separated string of disabled channel IDs.</returns>
    [HttpGet("disabled-channels")]
    public async Task<IActionResult> GetDisabledAfkChannels(ulong guildId)
    {
        var disabledChannels = await afk.GetDisabledAfkChannels(guildId);
        return Ok(disabledChannels);
    }

    /// <summary>
    ///     Sets the disabled AFK channels for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to set the disabled channels for.</param>
    /// <param name="channels">A comma-separated string of channel IDs to disable.</param>
    /// <returns>An <see cref="OkResult" /> indicating success.</returns>
    [HttpPost("disabled-channels")]
    public async Task<IActionResult> AfkDisabledSet(ulong guildId, [FromBody] string channels)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null) return NotFound("Guild not found.");
        await afk.AfkDisabledSet(guild, channels);
        return Ok();
    }

    /// <summary>
    ///     Gets the custom AFK message format for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get the custom message for.</param>
    /// <returns>An <see cref="IActionResult" /> containing the custom AFK message format.</returns>
    [HttpGet("custom-message")]
    public async Task<IActionResult> GetCustomAfkMessage(ulong guildId)
    {
        var customMessage = await afk.GetCustomAfkMessage(guildId);
        return Ok(customMessage == "-" ? "" : customMessage);
    }

    /// <summary>
    ///     Sets the custom AFK message format for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to set the custom message for.</param>
    /// <param name="message">The custom AFK message format to set.</param>
    /// <returns>An <see cref="OkResult" /> indicating success.</returns>
    [HttpPost("custom-message")]
    public async Task<IActionResult> SetCustomAfkMessage(ulong guildId, [FromBody] string message)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null) return NotFound("Guild not found.");
        await afk.SetCustomAfkMessage(guild, message);
        return Ok();
    }
}