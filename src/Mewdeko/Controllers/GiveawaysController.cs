using System.Net.Http;
using System.Text.Json;
using DataModel;
using LinqToDB.Async;
using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Controllers.Common.Giveaways;
using Mewdeko.Modules.Giveaways.Common;
using Mewdeko.Modules.Giveaways.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkiaSharp;

namespace Mewdeko.Controllers;

/// <summary>
///     API Controller for managing and interacting with Giveaways.
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class GiveawaysController : Controller
{
    private readonly IDashboardAuditContext auditContext;
    private readonly HttpClient client;
    private readonly BotCredentials creds;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<GiveawaysController> logger;
    private readonly GiveawayService service;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GiveawaysController" /> class.
    /// </summary>
    /// <param name="service">The giveaway service instance.</param>
    /// <param name="creds">The bot credentials instance.</param>
    /// <param name="client">The HTTP client instance.</param>
    /// <param name="dbFactory">The factory for creating database connections.</param>
    /// <param name="logger">Logger for this class.</param>
    /// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
    public GiveawaysController(
        GiveawayService service,
        BotCredentials creds,
        HttpClient client,
        IDataConnectionFactory dbFactory, ILogger<GiveawaysController> logger,
        IDashboardAuditContext auditContext)
    {
        this.service = service;
        this.creds = creds;
        this.client = client;
        this.dbFactory = dbFactory;
        this.logger = logger;
        this.auditContext = auditContext;
    }

    /// <summary>
    ///     Allows a user to enter a specific giveaway after verifying a Turnstile captcha token.
    /// </summary>
    /// <param name="request">The request containing user ID, giveaway ID, and Turnstile token.</param>
    /// <returns>
    ///     An <see cref="OkResult" /> if entry is successful, or <see cref="BadRequestObjectResult" /> if captcha fails
    ///     or entry is disallowed.
    /// </returns>
    [HttpPost("enter")]
    public async Task<IActionResult> EnterGiveaway(
        [FromBody] GiveawayEntryRequest request)
    {
        var authenticatedUserId = await HttpContext.GetDashboardUserIdAsync();
        if (authenticatedUserId is null)
            return Unauthorized("You must be signed in to enter a giveaway.");

        var verificationResponse = await VerifyTurnstileToken(request.TurnstileToken);
        if (!verificationResponse.Success)
        {
            return BadRequest("Captcha verification failed");
        }

        var (successful, reason) = await service.AddUserToGiveaway(authenticatedUserId.Value, request.GiveawayId);

        if (!successful)
            return BadRequest(reason);
        return Ok();
    }

    /// <summary>
    ///     Gets a specific giveaway by its unique ID.
    /// </summary>
    /// <param name="giveawayId">The integer ID of the giveaway.</param>
    /// <returns>
    ///     An <see cref="OkObjectResult" /> containing the giveaway details if found, otherwise potentially null or an
    ///     error depending on service implementation.
    /// </returns>
    [HttpGet("{giveawayId:int}")]
    public async Task<IActionResult> GetGiveaway(int giveawayId)
    {
        var giveaway = await service.GetGiveawayById(giveawayId);
        return Ok(giveaway);
    }

    /// <summary>
    ///     Gets all active or relevant giveaways for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>
    ///     An <see cref="OkObjectResult" /> containing a list of giveaways, or <see cref="NotFoundResult" /> if an error
    ///     occurs.
    /// </returns>
    [HttpGet("guild/{guildId:ulong}")]
    public async Task<IActionResult> GetGiveawaysForGuild(ulong guildId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var giveaways = await db.Giveaways
                .Where(x => x.ServerId == guildId)
                .ToListAsync();
            return Ok(giveaways);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve giveaways for guild {GuildId}", guildId);
            return NotFound(
                "Failed to retrieve giveaways for the specified guild.");
        }
    }

    /// <summary>
    ///     Creates a new giveaway initiated from an external source (e.g., dashboard).
    /// </summary>
    /// <param name="guildId">The ID of the guild where the giveaway will run.</param>
    /// <param name="model">The giveaway details.</param>
    /// <returns>
    ///     An <see cref="OkObjectResult" /> containing the created giveaway details, or
    ///     <see cref="BadRequestObjectResult" /> if creation fails.
    /// </returns>
    [HttpPost("{guildId:ulong}")]
    public async Task<IActionResult> CreateGiveaway(ulong guildId, [FromBody] Giveaway model)
    {
        try
        {
            var createdGiveaway = await service.CreateGiveawayFromDashboard(guildId, model);
            auditContext.RecordAfter(createdGiveaway);
            return Ok(createdGiveaway);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create dashboard giveaway for guild {GuildId}", guildId);
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    ///     Manually ends a specific giveaway.
    /// </summary>
    /// <param name="guildId">The ID of the guild (used for routing, not directly in logic shown).</param>
    /// <param name="giveawayId">The ID of the giveaway to end.</param>
    /// <returns>
    ///     An <see cref="OkResult" /> if ending is initiated, <see cref="NotFoundObjectResult" /> if giveaway doesn't
    ///     exist, or <see cref="BadRequestObjectResult" /> on error.
    /// </returns>
    [HttpPatch("{guildId:ulong}/{giveawayId:int}")]
    public async Task<IActionResult> EndGiveaway(ulong guildId, int giveawayId)
    {
        try
        {
            var gway = await service.GetGiveawayById(giveawayId);
            if (gway is null || gway.ServerId != guildId)
                return NotFound("That giveaway doesnt exist.");

            if (gway.Ended == GiveawayStatus.Draft)
                return BadRequest("That giveaway is a draft and has not been started.");

            if (gway.Ended == GiveawayStatus.Ended)
                return BadRequest("That giveaway has already ended.");

            auditContext.RecordBefore(gway);
            await service.GiveawayTimerAction(gway);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to end giveaway {GiveawayId} for guild {GuildId}", giveawayId, guildId);
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    ///     Picks new winners for an ended giveaway.
    /// </summary>
    /// <param name="guildId">The ID of the guild the giveaway belongs to.</param>
    /// <param name="giveawayId">The ID of the giveaway to reroll.</param>
    /// <returns>
    ///     An <see cref="OkResult" /> once new winners are picked, <see cref="NotFoundObjectResult" /> if the giveaway
    ///     doesn't exist, or <see cref="BadRequestObjectResult" /> if it hasn't ended.
    /// </returns>
    [HttpPost("{guildId:ulong}/{giveawayId:int}/reroll")]
    public async Task<IActionResult> RerollGiveaway(ulong guildId, int giveawayId)
    {
        try
        {
            var gway = await service.GetGiveawayById(giveawayId);
            if (gway is null || gway.ServerId != guildId)
                return NotFound("That giveaway doesnt exist.");

            if (gway.Ended != GiveawayStatus.Ended)
                return BadRequest("That giveaway has not ended yet.");

            auditContext.RecordBefore(gway);
            await service.GiveawayTimerAction(gway);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reroll giveaway {GiveawayId} for guild {GuildId}", giveawayId, guildId);
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    ///     Changes the extra entries and automatic prize of a running giveaway or a draft.
    /// </summary>
    /// <param name="guildId">The ID of the guild the giveaway belongs to.</param>
    /// <param name="giveawayId">The ID of the giveaway.</param>
    /// <param name="request">The new extra entries and prize.</param>
    /// <returns>
    ///     An <see cref="OkObjectResult" /> containing the updated giveaway, <see cref="NotFoundObjectResult" /> if it
    ///     doesn't exist, or <see cref="BadRequestObjectResult" /> if the values are invalid.
    /// </returns>
    [HttpPatch("{guildId:ulong}/{giveawayId:int}/options")]
    public async Task<IActionResult> UpdateOptions(ulong guildId, int giveawayId,
        [FromBody] GiveawayOptionsRequest request)
    {
        var gway = await service.GetGiveawayById(giveawayId);
        if (gway is null || gway.ServerId != guildId)
            return NotFound("That giveaway doesnt exist.");

        if (gway.Ended == GiveawayStatus.Ended)
            return BadRequest("That giveaway has already ended.");

        if (request.PrizeType is < 0 or > (int)GiveawayPrizeType.Coins || request.PrizeAmount < 0 ||
            (request.PrizeType != (int)GiveawayPrizeType.None && request.PrizeAmount == 0))
            return BadRequest("Pick a prize type and an amount above zero.");

        auditContext.RecordBefore(gway);

        gway.ExtraEntryRoles = GiveawayExtraEntries.Format(GiveawayExtraEntries.Parse(request.ExtraEntryRoles));
        gway.PrizeType = request.PrizeType;
        gway.PrizeAmount = request.PrizeType == (int)GiveawayPrizeType.None ? 0 : request.PrizeAmount;

        await service.UpdateGiveawayOptionsAsync(gway);
        auditContext.RecordAfter(gway);
        return Ok(gway);
    }

    /// <summary>
    ///     Gets how many members entered each button or captcha giveaway of a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>An <see cref="OkObjectResult" /> mapping giveaway IDs to their entry counts.</returns>
    [HttpGet("entries/{guildId:ulong}")]
    public async Task<IActionResult> GetEntryCounts(ulong guildId)
    {
        return Ok(await service.GetEntryCountsAsync(guildId));
    }

    /// <summary>
    ///     Gets a guild's giveaway defaults.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>An <see cref="OkObjectResult" /> containing the giveaway defaults.</returns>
    [HttpGet("settings/{guildId:ulong}")]
    public async Task<IActionResult> GetSettings(ulong guildId)
    {
        var gc = await service.GetSettingsAsync(guildId);

        return Ok(new GiveawaySettings
        {
            Emote = gc.GiveawayEmote,
            PingRole = gc.GiveawayPingRole,
            Banner = gc.GiveawayBanner,
            EmbedColor = gc.GiveawayEmbedColor,
            DmOnWin = gc.DmOnGiveawayWin,
            EndMessage = gc.GiveawayEndMessage
        });
    }

    /// <summary>
    ///     Changes a guild's giveaway defaults.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="settings">The new defaults.</param>
    /// <returns>
    ///     An <see cref="OkObjectResult" /> containing the saved defaults, or <see cref="BadRequestObjectResult" /> if a
    ///     value is invalid.
    /// </returns>
    [HttpPut("settings/{guildId:ulong}")]
    public async Task<IActionResult> UpdateSettings(ulong guildId, [FromBody] GiveawaySettings settings)
    {
        var emote = settings.Emote?.Trim();
        if (!string.IsNullOrEmpty(emote) && !emote.TryToIEmote(out _))
            return BadRequest("That emote is not valid.");

        var banner = settings.Banner?.Trim();
        if (!string.IsNullOrEmpty(banner) && !Uri.IsWellFormedUriString(banner, UriKind.Absolute))
            return BadRequest("The banner must be a full image URL.");

        var color = settings.EmbedColor?.Trim();
        if (!string.IsNullOrEmpty(color))
        {
            if (!SKColor.TryParse(color, out var parsed))
                return BadRequest("The colour must be a hex value such as #5865f2.");

            color = $"#{parsed.Red:x2}{parsed.Green:x2}{parsed.Blue:x2}";
        }

        var before = await service.GetSettingsAsync(guildId);
        auditContext.RecordBefore(new GiveawaySettings
        {
            Emote = before.GiveawayEmote,
            PingRole = before.GiveawayPingRole,
            Banner = before.GiveawayBanner,
            EmbedColor = before.GiveawayEmbedColor,
            DmOnWin = before.DmOnGiveawayWin,
            EndMessage = before.GiveawayEndMessage
        });

        var message = string.IsNullOrWhiteSpace(settings.EndMessage) ? null : settings.EndMessage;

        await service.UpdateSettingsAsync(guildId, gc =>
        {
            if (!string.IsNullOrEmpty(emote))
                gc.GiveawayEmote = emote;

            gc.GiveawayPingRole = settings.PingRole;
            gc.GiveawayBanner = banner ?? "";
            gc.GiveawayEmbedColor = color ?? "";
            gc.DmOnGiveawayWin = settings.DmOnWin;
            gc.GiveawayEndMessage = message;
        });

        settings.Emote = emote;
        settings.Banner = banner;
        settings.EmbedColor = color;
        settings.EndMessage = message;
        auditContext.RecordAfter(settings);
        return Ok(settings);
    }

    /// <summary>
    ///     Starts a saved giveaway draft.
    /// </summary>
    /// <param name="guildId">The ID of the guild the draft belongs to.</param>
    /// <param name="giveawayId">The ID of the draft to start.</param>
    /// <returns>
    ///     An <see cref="OkObjectResult" /> containing the running giveaway, <see cref="NotFoundObjectResult" /> if the
    ///     draft doesn't exist, or <see cref="BadRequestObjectResult" /> if starting fails.
    /// </returns>
    [HttpPost("{guildId:ulong}/{giveawayId:int}/start")]
    public async Task<IActionResult> StartDraft(ulong guildId, int giveawayId)
    {
        try
        {
            var draft = await service.GetGiveawayById(giveawayId);
            if (draft is null || draft.ServerId != guildId || draft.Ended != GiveawayStatus.Draft)
                return NotFound("That draft doesnt exist.");

            auditContext.RecordBefore(draft);
            var started = await service.StartDraftAsync(draft, draft.UserId);
            auditContext.RecordAfter(started);
            return Ok(started);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start giveaway draft {GiveawayId} for guild {GuildId}", giveawayId,
                guildId);
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    ///     Deletes a saved giveaway draft.
    /// </summary>
    /// <param name="guildId">The ID of the guild the draft belongs to.</param>
    /// <param name="giveawayId">The ID of the draft to delete.</param>
    /// <returns>No content on success, or <see cref="NotFoundObjectResult" /> if the draft doesn't exist.</returns>
    [HttpDelete("{guildId:ulong}/{giveawayId:int}")]
    public async Task<IActionResult> DeleteDraft(ulong guildId, int giveawayId)
    {
        return await service.DeleteDraftAsync(guildId, giveawayId)
            ? NoContent()
            : NotFound("That draft doesnt exist.");
    }

    /// <summary>
    ///     Verifies a Cloudflare Turnstile token.
    /// </summary>
    /// <param name="token">The Turnstile token from the client.</param>
    /// <returns>The deserialized response from the verification endpoint.</returns>
    private async Task<TurnstileVerificationResponse> VerifyTurnstileToken(string token)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            {
                "secret", creds.TurnstileKey
            },
            {
                "response", token
            }
        });

        try
        {
            var response =
                await client.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", content);
            response.EnsureSuccessStatusCode(); // Throw on bad response
            var responseStream = await response.Content.ReadAsStreamAsync();
            var verificationData = await JsonSerializer.DeserializeAsync<TurnstileVerificationResponse>(responseStream);
            return verificationData ?? new TurnstileVerificationResponse
            {
                Success = false
            };
        }
        catch (HttpRequestException httpEx)
        {
            logger.LogError(httpEx, "HTTP error verifying Turnstile token");
            return new TurnstileVerificationResponse
            {
                Success = false
            };
        }
        catch (JsonException jsonEx)
        {
            logger.LogError(jsonEx, "JSON error deserializing Turnstile response");
            return new TurnstileVerificationResponse
            {
                Success = false
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Generic error verifying Turnstile token");
            return new TurnstileVerificationResponse
            {
                Success = false
            };
        }
    }
}