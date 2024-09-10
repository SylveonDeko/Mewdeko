using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using LinqToDB;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Giveaways.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;

namespace Mewdeko.Controllers;

/// <inheritdoc />
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class GiveawaysController(GiveawayService service, BotCredentials creds, HttpClient client, DbContextProvider dbContext) : Controller
{

    /// <summary>
    ///
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("enter")]
    public async Task<IActionResult> EnterGiveaway(
        [FromBody] GiveawayEntryRequest request)
    {
        // Verify Turnstile token
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
    /// Gets a giveaway by its Id
    /// </summary>
    /// <param name="giveawayId"></param>
    /// <returns></returns>
    [HttpGet("{giveawayId:int}")]
    public async Task<IActionResult> GetGiveaway(int giveawayId)
    {
        var giveaway = await service.GetGiveawayById(giveawayId);
        return Ok(giveaway);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="guildId"></param>
    /// <returns></returns>
    [HttpGet("{guildId}")]
    public async Task<IActionResult> GetGiveawaysForGuild(ulong guildId)
    {
        try
        {
            await using var db = await dbContext.GetContextAsync();
            // You'll need to implement this method in the GiveawayService
            var giveaways = await db.Giveaways.Where(x => x.ServerId == guildId).ToListAsync();
            return Ok(giveaways);
        }
        catch
        {
            return NotFound();
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("{guildId}")]
    public async Task<IActionResult> CreateGiveaway(ulong guildId, [FromBody] Giveaways model)
    {
        try
        {
            // You'll need to implement this method in the GiveawayService
            var createdGiveaway = await service.CreateGiveawayFromDashboard(guildId, model);
            return Ok(createdGiveaway);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="giveawayId"></param>
    /// <returns></returns>
    [HttpPatch("{guildId}/{giveawayId}")]
    public async Task<IActionResult> EndGiveaway(ulong guildId, int giveawayId)
    {
        try
        {
            var gway = await service.GetGiveawayById(giveawayId);
            // You'll need to implement this method in the GiveawayService
            await service.GiveawayTimerAction(gway);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private async Task<TurnstileVerificationResponse> VerifyTurnstileToken(string token)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            {"secret", creds.TurnstileKey},
            {"response", token}
        });

        var response = await client.PostAsync(
            "https://challenges.cloudflare.com/turnstile/v0/siteverify", content);
        var responseString = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<TurnstileVerificationResponse>(responseString);
    }
}

/// <summary>
///
/// </summary>
public class GiveawayEntryRequest
{
    /// <summary>
    ///
    /// </summary>
    public ulong GuildId { get; set; }
    /// <summary>
    ///
    /// </summary>
    public int GiveawayId { get; set; }
    /// <summary>
    ///
    /// </summary>
    public ulong UserId { get; set; }
    /// <summary>
    ///
    /// </summary>
    public string TurnstileToken { get; set; }
}

/// <summary>
///
/// </summary>
public class TurnstileVerificationResponse
{
    /// <summary>
    ///
    /// </summary>
    public bool Success { get; set; }
    // Add other properties as needed
}