using LinqToDB.Async;
using Mewdeko.Modules.Votes.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API Controller for managing vote system configuration and data.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class VotesController(
    VoteService service,
    DiscordShardedClient client,
    IDataConnectionFactory dbFactory)
    : Controller
{
    /// <summary>
    ///     Gets all vote roles configured for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>List of vote roles</returns>
    [HttpGet("roles")]
    public async Task<IActionResult> GetVoteRoles(ulong guildId)
    {
        var voteRoles = await service.GetVoteRoles(guildId);
        return Ok(voteRoles);
    }

    /// <summary>
    ///     Adds a vote role to the guild configuration
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="roleId">The ID of the role to add</param>
    /// <param name="seconds">Duration in seconds for automatic removal (0 for indefinite)</param>
    /// <returns>Success or error message</returns>
    [HttpPost("roles/{roleId}")]
    public async Task<IActionResult> AddVoteRole(ulong guildId, ulong roleId, [FromBody] int seconds = 0)
    {
        var (success, error) = await service.AddVoteRole(guildId, roleId, seconds);
        if (!success)
            return BadRequest(error);
        return Ok();
    }

    /// <summary>
    ///     Removes a vote role from the guild configuration
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="roleId">The ID of the role to remove</param>
    /// <returns>Success or error message</returns>
    [HttpDelete("roles/{roleId}")]
    public async Task<IActionResult> RemoveVoteRole(ulong guildId, ulong roleId)
    {
        var (success, error) = await service.RemoveVoteRole(guildId, roleId);
        if (!success)
            return BadRequest(error);
        return Ok();
    }

    /// <summary>
    ///     Updates the timer for a vote role
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="roleId">The ID of the role</param>
    /// <param name="seconds">New duration in seconds</param>
    /// <returns>Success or error message</returns>
    [HttpPatch("roles/{roleId}")]
    public async Task<IActionResult> UpdateVoteRoleTimer(ulong guildId, ulong roleId, [FromBody] int seconds)
    {
        var (success, error) = await service.UpdateTimer(guildId, roleId, seconds);
        if (!success)
            return BadRequest(error);
        return Ok();
    }

    /// <summary>
    ///     Clears all vote roles for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>Success or error message</returns>
    [HttpDelete("roles")]
    public async Task<IActionResult> ClearVoteRoles(ulong guildId)
    {
        var (success, error) = await service.ClearVoteRoles(guildId);
        if (!success)
            return BadRequest(error);
        return Ok();
    }

    /// <summary>
    ///     Gets the custom vote message for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The custom vote message</returns>
    [HttpGet("message")]
    public async Task<IActionResult> GetVoteMessage(ulong guildId)
    {
        var message = await service.GetVoteMessage(guildId);
        return Ok(message ?? "");
    }

    /// <summary>
    ///     Sets the custom vote message for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="message">The new custom vote message</param>
    /// <returns>Success response</returns>
    [HttpPost("message")]
    public async Task<IActionResult> SetVoteMessage(ulong guildId, [FromBody] string message)
    {
        await service.SetVoteMessage(guildId, message);
        return Ok();
    }

    /// <summary>
    ///     Gets the vote password for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The vote password</returns>
    [HttpGet("password")]
    public async Task<IActionResult> GetVotePassword(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var config = await db.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
        return Ok(config?.VotesPassword ?? "");
    }

    /// <summary>
    ///     Sets the vote password for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="password">The new vote password</param>
    /// <returns>Success response</returns>
    [HttpPost("password")]
    public async Task<IActionResult> SetVotePassword(ulong guildId, [FromBody] string password)
    {
        await service.SetVotePassword(guildId, password);
        return Ok();
    }

    /// <summary>
    ///     Gets the vote channel for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The ID of the vote channel</returns>
    [HttpGet("channel")]
    public async Task<IActionResult> GetVoteChannel(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var config = await db.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
        return Ok(config?.VotesChannel ?? 0);
    }

    /// <summary>
    ///     Sets the vote channel for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The ID of the vote channel</param>
    /// <returns>Success response</returns>
    [HttpPost("channel")]
    public async Task<IActionResult> SetVoteChannel(ulong guildId, [FromBody] ulong channelId)
    {
        await service.SetVoteChannel(guildId, channelId);
        return Ok();
    }

    /// <summary>
    ///     Gets all votes for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>List of votes</returns>
    [HttpGet("votes")]
    public async Task<IActionResult> GetGuildVotes(ulong guildId)
    {
        var votes = await service.GetVotes(guildId);
        return Ok(votes);
    }

    /// <summary>
    ///     Gets votes for a specific user in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="userId">The ID of the user</param>
    /// <returns>List of votes for the user</returns>
    [HttpGet("votes/{userId}")]
    public async Task<IActionResult> GetUserVotes(ulong guildId, ulong userId)
    {
        var votes = await service.GetVotes(guildId, userId);
        return Ok(votes);
    }

    /// <summary>
    ///     Gets vote statistics for a user
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="userId">The ID of the user</param>
    /// <returns>Vote statistics embed data</returns>
    [HttpGet("stats/{userId}")]
    public async Task<IActionResult> GetUserVoteStats(ulong guildId, ulong userId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var user = client.GetUser(userId);
        if (user == null)
            return NotFound("User not found");

        await using var db = await dbFactory.CreateConnectionAsync();

        var thisMonth = await db.Votes
            .CountAsync(x => x.DateAdded.Value.Month == DateTime.UtcNow.Month &&
                             x.UserId == userId &&
                             x.GuildId == guildId);

        var total = await db.Votes
            .CountAsync(x => x.GuildId == guildId && x.UserId == userId);

        return Ok(new
        {
            userId, username = user.Username, votesThisMonth = thisMonth, totalVotes = total
        });
    }

    /// <summary>
    ///     Gets vote leaderboard for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <returns>Leaderboard data</returns>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetVoteLeaderboard(ulong guildId, [FromQuery] int limit = 10)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var leaderboard = await db.Votes
            .Where(x => x.GuildId == guildId)
            .GroupBy(x => x.UserId)
            .Select(g => new
            {
                userId = g.Key, voteCount = g.Count()
            })
            .OrderByDescending(x => x.voteCount)
            .Take(limit)
            .ToListAsync();

        return Ok(leaderboard);
    }
}