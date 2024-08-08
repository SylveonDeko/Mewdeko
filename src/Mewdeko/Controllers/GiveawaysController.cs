using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using Mewdeko.Modules.Giveaways.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;

namespace Mewdeko.Controllers;

/// <inheritdoc />
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class GiveawaysController(GiveawayService service, BotCredentials creds, HttpClient client) : Controller
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