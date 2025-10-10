using Mewdeko.Modules.Utility.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing invite tracking and statistics
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class InviteTrackingController : Controller
{
    private readonly DiscordShardedClient client;
    private readonly InviteCountService inviteService;

    /// <summary>
    ///     Initializes a new instance of the InviteTrackingController
    /// </summary>
    /// <param name="inviteService">The inviteservice service.</param>
    /// <param name="client">The Discord client instance.</param>
    public InviteTrackingController(InviteCountService inviteService, DiscordShardedClient client)
    {
        this.inviteService = inviteService;
        this.client = client;
    }

    /// <summary>
    ///     Gets invite tracking settings for a guild
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(ulong guildId)
    {
        var settings = await inviteService.GetInviteCountSettingsAsync(guildId);
        return Ok(settings);
    }

    /// <summary>
    ///     Enables or disables invite tracking for a guild
    /// </summary>
    [HttpPost("toggle")]
    public async Task<IActionResult> ToggleInviteTracking(ulong guildId, [FromBody] bool enabled)
    {
        var result = await inviteService.SetInviteTrackingEnabledAsync(guildId, enabled);
        return Ok(result);
    }

    /// <summary>
    ///     Sets whether invites should be removed when users leave
    /// </summary>
    [HttpPost("remove-on-leave")]
    public async Task<IActionResult> SetRemoveOnLeave(ulong guildId, [FromBody] bool removeOnLeave)
    {
        var result = await inviteService.SetRemoveInviteOnLeaveAsync(guildId, removeOnLeave);
        return Ok(result);
    }

    /// <summary>
    ///     Sets minimum account age for invite counting
    /// </summary>
    [HttpPost("min-age")]
    public async Task<IActionResult> SetMinAccountAge(ulong guildId, [FromBody] string minAge)
    {
        var timeSpan = TimeSpan.Parse(minAge);
        var result = await inviteService.SetMinAccountAgeAsync(guildId, timeSpan);
        return Ok(result.ToString());
    }

    /// <summary>
    ///     Gets invites for a specific user
    /// </summary>
    [HttpGet("count/{userId}")]
    public async Task<IActionResult> GetInviteCount(ulong guildId, ulong userId)
    {
        var count = await inviteService.GetInviteCount(userId, guildId);
        return Ok(count);
    }

    /// <summary>
    ///     Gets who invited a specific user
    /// </summary>
    [HttpGet("inviter/{userId}")]
    public async Task<IActionResult> GetInviter(ulong guildId, ulong userId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var inviter = await inviteService.GetInviter(userId, guild);
        if (inviter == null)
            return NotFound("Inviter not found");

        return Ok(new
        {
            inviter.Id, inviter.Username, inviter.Discriminator, AvatarUrl = inviter.GetAvatarUrl()
        });
    }

    /// <summary>
    ///     Gets all users invited by a specific user
    /// </summary>
    [HttpGet("invited/{userId}")]
    public async Task<IActionResult> GetInvitedUsers(ulong guildId, ulong userId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var invitedUsers = await inviteService.GetInvitedUsers(userId, guild);
        var result = invitedUsers.Select(user => new
        {
            user.Id, user.Username, user.Discriminator, AvatarUrl = user.GetAvatarUrl()
        });

        return Ok(result);
    }

    /// <summary>
    ///     Gets the invite leaderboard for a guild
    /// </summary>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard(ulong guildId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var leaderboard = await inviteService.GetInviteLeaderboardAsync(guild, page, pageSize);
        return Ok(leaderboard.Select(entry => new
        {
            entry.UserId, entry.Username, entry.InviteCount
        }));
    }
}