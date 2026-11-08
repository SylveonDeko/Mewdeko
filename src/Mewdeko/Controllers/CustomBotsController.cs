using Mewdeko.AuthHandlers;
using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Paid custom bots: the Ko-fi webhook, a user's slots and bots, and owner tools.
/// </summary>
[ApiController]
[Route("botapi/CustomBots")]
[Authorize("ApiKeyPolicy")]
[SkipDashboardAccess]
public class CustomBotsController(CustomBotService bots, BotCredentials creds, ILogger<CustomBotsController> logger)
    : Controller
{
    /// <summary>
    ///     Body of a claim request.
    /// </summary>
    /// <param name="Email">The purchase email.</param>
    /// <param name="TransactionId">The Ko-fi transaction id.</param>
    public sealed record ClaimRequest(string? Email, string? TransactionId);

    /// <summary>
    ///     Body of a create request.
    /// </summary>
    /// <param name="Token">The bot token.</param>
    /// <param name="SlotId">The slot to use.</param>
    public sealed record CreateRequest(string? Token, int SlotId);

    /// <summary>
    ///     Body of a token replacement.
    /// </summary>
    /// <param name="Token">The new token.</param>
    public sealed record TokenRequest(string? Token);

    /// <summary>
    ///     Body of a manual slot grant.
    /// </summary>
    /// <param name="Days">How many days, or null for never expiring.</param>
    public sealed record GrantRequest(int? Days);

    private async Task<ulong?> JwtUserAsync()
    {
        var auth = await HttpContext.AuthenticateAsync(DashJwtConstants.SchemeName);
        var claim = auth.Principal?.FindFirst(DashJwtConstants.UserIdClaim)?.Value;
        return ulong.TryParse(claim, out var id) ? id : null;
    }

    private async Task<IActionResult?> GuardUserAsync(ulong userId)
    {
        if (!bots.Enabled)
            return StatusCode(503, new { error = "custom bots are handled by the main instance" });

        var jwt = await JwtUserAsync();
        if (jwt is { } id && id != userId && !creds.IsOwner(id))
            return Forbid();

        return null;
    }

    private async Task<IActionResult?> GuardOwnerAsync()
    {
        if (!bots.Enabled)
            return StatusCode(503, new { error = "custom bots are handled by the main instance" });

        var jwt = await JwtUserAsync();
        return jwt is { } id && creds.IsOwner(id) ? null : Forbid();
    }

    private IActionResult Result(string? error)
    {
        if (error is null)
            return Ok(new { success = true });

        return error == "not found" ? NotFound(new { error }) : BadRequest(new { error });
    }

    /// <summary>
    ///     Receives Ko-fi webhook deliveries. Ko-fi posts a form whose <c>data</c> field holds the payment JSON.
    /// </summary>
    [HttpPost("kofi")]
    [AllowAnonymous]
    public async Task<IActionResult> Kofi([FromForm] string? data)
    {
        if (!bots.Enabled)
            return StatusCode(503);

        var result = await bots.HandleKofiAsync(data);
        switch (result)
        {
            case KofiResult.Rejected:
                logger.LogWarning("Rejected a Ko-fi webhook with a bad verification token");
                return Unauthorized();
            case KofiResult.Invalid:
                return BadRequest();
            default:
                return Ok();
        }
    }

    /// <summary>
    ///     Gets a user's slots and bots.
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUser(ulong userId)
    {
        if (await GuardUserAsync(userId) is { } denied)
            return denied;

        var (slots, list) = await bots.GetUserAsync(userId);
        return Ok(new
        {
            slots, bots = list
        });
    }

    /// <summary>
    ///     Links a Ko-fi purchase to a user.
    /// </summary>
    [HttpPost("user/{userId}/claim")]
    public async Task<IActionResult> Claim(ulong userId, [FromBody] ClaimRequest request)
    {
        if (await GuardUserAsync(userId) is { } denied)
            return denied;

        return Result(await bots.ClaimAsync(userId, request.Email, request.TransactionId));
    }

    /// <summary>
    ///     Creates a custom bot from a token.
    /// </summary>
    [HttpPost("user/{userId}/bots")]
    public async Task<IActionResult> Create(ulong userId, [FromBody] CreateRequest request)
    {
        if (await GuardUserAsync(userId) is { } denied)
            return denied;

        var (bot, error) = await bots.CreateBotAsync(userId, request.Token, request.SlotId);
        return bot is null ? BadRequest(new { error }) : Ok(bot);
    }

    /// <summary>
    ///     Starts, stops or restarts a bot.
    /// </summary>
    [HttpPost("user/{userId}/bots/{instanceId:int}/{action}")]
    public async Task<IActionResult> Control(ulong userId, int instanceId, string action)
    {
        if (await GuardUserAsync(userId) is { } denied)
            return denied;

        return Result(await bots.ControlAsync(userId, instanceId, action));
    }

    /// <summary>
    ///     Replaces a bot's token.
    /// </summary>
    [HttpPut("user/{userId}/bots/{instanceId:int}/token")]
    public async Task<IActionResult> ReplaceToken(ulong userId, int instanceId, [FromBody] TokenRequest request)
    {
        if (await GuardUserAsync(userId) is { } denied)
            return denied;

        return Result(await bots.ReplaceTokenAsync(userId, instanceId, request.Token));
    }

    /// <summary>
    ///     Removes a bot.
    /// </summary>
    [HttpDelete("user/{userId}/bots/{instanceId:int}")]
    public async Task<IActionResult> Remove(ulong userId, int instanceId)
    {
        if (await GuardUserAsync(userId) is { } denied)
            return denied;

        return Result(await bots.RemoveAsync(userId, instanceId));
    }

    /// <summary>
    ///     Gets recent log lines for a bot.
    /// </summary>
    [HttpGet("user/{userId}/bots/{instanceId:int}/logs")]
    public async Task<IActionResult> Logs(ulong userId, int instanceId, [FromQuery] int tail = 200)
    {
        if (await GuardUserAsync(userId) is { } denied)
            return denied;

        var lines = await bots.GetLogsAsync(userId, instanceId, tail);
        return lines is null ? NotFound(new { error = "not found" }) : Ok(new { lines });
    }

    /// <summary>
    ///     Gives a guild to a bot.
    /// </summary>
    [HttpPost("user/{userId}/bots/{instanceId:int}/guilds/{guildId}")]
    public async Task<IActionResult> AssignGuild(ulong userId, int instanceId, ulong guildId)
    {
        if (await GuardUserAsync(userId) is { } denied)
            return denied;

        return Result(await bots.AssignGuildAsync(userId, instanceId, guildId));
    }

    /// <summary>
    ///     Returns a guild to the main bot.
    /// </summary>
    [HttpDelete("user/{userId}/bots/{instanceId:int}/guilds/{guildId}")]
    public async Task<IActionResult> ReleaseGuild(ulong userId, int instanceId, ulong guildId)
    {
        if (await GuardUserAsync(userId) is { } denied)
            return denied;

        return Result(await bots.ReleaseGuildAsync(userId, instanceId, guildId));
    }

    /// <summary>
    ///     Says which custom bot runs a guild, for a dashboard banner.
    /// </summary>
    [HttpGet("guild/{guildId}")]
    public async Task<IActionResult> GetGuild(ulong guildId)
    {
        var (instanceId, name, avatar) = await bots.GetGuildBotAsync(guildId);
        return Ok(new GuildBotResponse(instanceId, name, avatar));
    }

    /// <summary>
    ///     Which custom bot runs a guild. Every field is null when the main bot does.
    /// </summary>
    /// <param name="InstanceId">The custom bot's instance id.</param>
    /// <param name="BotName">Its name.</param>
    /// <param name="BotAvatar">Its avatar URL.</param>
    public sealed record GuildBotResponse(
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
        int? InstanceId,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
        string? BotName,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
        string? BotAvatar);

    /// <summary>
    ///     Grants a manual slot. Owners only.
    /// </summary>
    [HttpPost("slots/{userId}")]
    public async Task<IActionResult> GrantSlot(ulong userId, [FromBody] GrantRequest? request)
    {
        if (await GuardOwnerAsync() is { } denied)
            return denied;

        if (request?.Days is <= 0)
            return BadRequest(new { error = "days must be positive" });

        return Ok(await bots.GrantManualSlotAsync(userId, request?.Days));
    }

    /// <summary>
    ///     Lists every slot and bot. Owners only.
    /// </summary>
    [HttpGet("slots")]
    public async Task<IActionResult> ListSlots()
    {
        if (await GuardOwnerAsync() is { } denied)
            return denied;

        var (slots, list) = await bots.GetAllAsync();
        return Ok(new
        {
            slots, bots = list
        });
    }
}
