using System.Net.Http;
using System.Text.Json;
using DataModel;
using LinqToDB;
using Mewdeko.Controllers.Common.Giveaways;
using Mewdeko.Modules.Giveaways.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API Controller for managing and interacting with Giveaways.
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class GiveawaysController : Controller
{
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
    public GiveawaysController(
        GiveawayService service,
        BotCredentials creds,
        HttpClient client,
        IDataConnectionFactory dbFactory, ILogger<GiveawaysController> logger)
    {
        this.service = service;
        this.creds = creds;
        this.client = client;
        this.dbFactory = dbFactory;
        this.logger = logger;
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
        var verificationResponse = await VerifyTurnstileToken(request.TurnstileToken);
        if (!verificationResponse.Success)
        {
            return BadRequest("Captcha verification failed");
        }


        var (successful, reason) = await service.AddUserToGiveaway(request.UserId, request.GiveawayId);

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
    [HttpGet("{guildId:ulong}")]
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
            if (gway is null)
                return NotFound("That giveaway doesnt exist.");

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