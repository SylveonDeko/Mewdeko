using System.IO;
using System.Net.Http;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Controllers.Common.MultiGreets;
using Mewdeko.Modules.MultiGreets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing MultiGreet functionality within the Discord bot
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class MultiGreetController : Controller
{
    private readonly DiscordShardedClient client;
    private readonly MultiGreetService multiGreetService;
    private readonly IDashboardAuditContext auditContext;

    /// <summary>
    ///     Initializes a new instance of the MultiGreetController
    /// </summary>
    /// <param name="multiGreetService">Service for managing MultiGreet operations</param>
    /// <param name="client">Discord client instance</param>
    /// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
    public MultiGreetController(
        MultiGreetService multiGreetService,
        DiscordShardedClient client,
        IDashboardAuditContext auditContext)
    {
        this.multiGreetService = multiGreetService;
        this.client = client;
        this.auditContext = auditContext;
    }

    /// <summary>
    ///     Retrieves all MultiGreet configurations for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild to get MultiGreets for</param>
    /// <returns>List of MultiGreet configurations with resolved channel information</returns>
    [HttpGet]
    public async Task<IActionResult> GetAllGreets(ulong guildId)
    {
        var greets = await multiGreetService.GetGreets(guildId);
        IGuild guild = client.GetGuild(guildId);

        if (greets == null)
            return NotFound();

        var result = await Task.WhenAll(greets.Where(g => g != null).Select(async greet =>
        {
            var channel = await guild.GetTextChannelAsync(greet.ChannelId);
            return new
            {
                greet.Id,
                greet.GuildId,
                greet.ChannelId,
                ChannelName = channel?.Name ?? "Deleted Channel",
                ChannelMention = channel != null ? MentionUtils.MentionChannel(channel.Id) : null,
                greet.Message,
                greet.DeleteTime,
                greet.WebhookUrl,
                greet.GreetBots,
                greet.Disabled
            };
        }));

        return Ok(result);
    }


    /// <summary>
    ///     Adds a new MultiGreet configuration to a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild to add the MultiGreet to</param>
    /// <param name="channelId">The ID of the channel for the MultiGreet</param>
    /// <returns>Success or failure response</returns>
    [HttpPost]
    public async Task<IActionResult> AddGreet(ulong guildId, [FromBody] ulong channelId)
    {
        var success = await multiGreetService.AddMultiGreet(guildId, channelId);
        if (!success)
            return BadRequest("Channel has reached maximum greets or guild has reached maximum total greets");

        return Ok();
    }

    /// <summary>
    ///     Removes a MultiGreet configuration from a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild containing the MultiGreet</param>
    /// <param name="greetId">The ID of the MultiGreet to remove</param>
    /// <returns>Success or not found response</returns>
    [HttpDelete("{greetId}")]
    public async Task<IActionResult> RemoveGreet(ulong guildId, int greetId)
    {
        var greets = await multiGreetService.GetGreets(guildId);
        if (greets is null)
            return NotFound("Somehow multigreets are null.");
        var greet = greets.FirstOrDefault(x => x.Id == greetId);

        if (greet == null)
            return NotFound();

        auditContext.RecordBefore(greet);
        await multiGreetService.RemoveMultiGreetInternal(greet);
        return Ok();
    }

    /// <summary>
    ///     Updates the message content of a MultiGreet
    /// </summary>
    /// <param name="guildId">The ID of the guild containing the MultiGreet</param>
    /// <param name="greetId">The ID of the MultiGreet to update</param>
    /// <param name="message">The new message content</param>
    /// <returns>Success or not found response</returns>
    [HttpPut("{greetId}/message")]
    public async Task<IActionResult> UpdateMessage(ulong guildId, int greetId, [FromBody] string message)
    {
        var greets = await multiGreetService.GetGreets(guildId);
        if (greets is null)
            return NotFound("Somehow multigreets are null.");

        var greet = greets.FirstOrDefault(x => x.Id == greetId);


        if (greet == null)
            return NotFound();

        auditContext.RecordBefore(greet);
        await multiGreetService.ChangeMgMessage(greet, message);
        return Ok();
    }

    /// <summary>
    ///     Updates the deletion time for a MultiGreet message
    /// </summary>
    /// <param name="guildId">The ID of the guild containing the MultiGreet</param>
    /// <param name="greetId">The ID of the MultiGreet to update</param>
    /// <param name="time">The new deletion time in a human-readable format</param>
    /// <returns>Success or not found response</returns>
    [HttpPut("{greetId}/delete-time")]
    public async Task<IActionResult> UpdateDeleteTime(ulong guildId, int greetId, [FromBody] string time)
    {
        var greets = await multiGreetService.GetGreets(guildId);
        if (greets is null)
            return NotFound("Somehow multigreets are null.");
        var greet = greets.FirstOrDefault(x => x.Id == greetId);


        if (greet == null)
            return NotFound();

        auditContext.RecordBefore(greet);
        var stoopidTime = StoopidTime.FromInput(time);
        await multiGreetService.ChangeMgDelete(greet, (int)stoopidTime.Time.TotalSeconds);
        return Ok();
    }

    /// <summary>
    ///     Updates whether a MultiGreet should greet bots
    /// </summary>
    /// <param name="guildId">The ID of the guild containing the MultiGreet</param>
    /// <param name="greetId">The ID of the MultiGreet to update</param>
    /// <param name="enabled">Whether to enable bot greetings</param>
    /// <returns>Success or not found response</returns>
    [HttpPut("{greetId}/greet-bots")]
    public async Task<IActionResult> UpdateGreetBots(ulong guildId, int greetId, [FromBody] bool enabled)
    {
        var greets = await multiGreetService.GetGreets(guildId);
        if (greets is null)
            return NotFound("Somehow multigreets are null.");

        var greet = greets.FirstOrDefault(x => x.Id == greetId);


        if (greet == null)
            return NotFound();

        auditContext.RecordBefore(greet);
        await multiGreetService.ChangeMgGb(greet, enabled);
        return Ok();
    }

    /// <summary>
    ///     Updates the webhook configuration for a MultiGreet
    /// </summary>
    /// <param name="guildId">The ID of the guild containing the MultiGreet</param>
    /// <param name="greetId">The ID of the MultiGreet to update</param>
    /// <param name="request">The webhook configuration update request</param>
    /// <returns>Success, not found, or bad request response</returns>
    [HttpPut("{greetId}/webhook")]
    public async Task<IActionResult> UpdateWebhook(ulong guildId, int greetId, [FromBody] WebhookUpdateRequest request)
    {
        var greets = await multiGreetService.GetGreets(guildId);
        if (greets is null)
            return NotFound("Somehow multigreets are null.");
        var greet = greets.FirstOrDefault(x => x.Id == greetId);


        if (greet == null)
            return NotFound();

        auditContext.RecordBefore(greet);

        IGuild guild = client.GetGuild(guildId);
        var channel = await guild.GetTextChannelAsync(greet.ChannelId);

        if (channel == null)
            return BadRequest("Channel not found");

        if (request.Name == null)
        {
            await multiGreetService.ChangeMgWebhook(greet, "");
            return Ok();
        }

        var webhook = request.AvatarUrl != null
            ? await channel.CreateWebhookAsync(request.Name, await GetAvatarStream(request.AvatarUrl))
            : await channel.CreateWebhookAsync(request.Name);

        await multiGreetService.ChangeMgWebhook(greet,
            $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}");

        return Ok();
    }

    /// <summary>
    ///     Updates whether a MultiGreet is disabled
    /// </summary>
    /// <param name="guildId">The ID of the guild containing the MultiGreet</param>
    /// <param name="greetId">The ID of the MultiGreet to update</param>
    /// <param name="disabled">Whether to disable the MultiGreet</param>
    /// <returns>Success or not found response</returns>
    [HttpPut("{greetId}/disable")]
    public async Task<IActionResult> UpdateDisabled(ulong guildId, int greetId, [FromBody] bool disabled)
    {
        var greets = await multiGreetService.GetGreets(guildId);
        if (greets is null)
            return NotFound("Somehow multigreets are null.");
        var greet = greets.FirstOrDefault(x => x.Id == greetId);


        if (greet == null)
            return NotFound();

        auditContext.RecordBefore(greet);
        await multiGreetService.MultiGreetDisable(greet, disabled);
        return Ok();
    }


    /// <summary>
    ///     Updates the MultiGreet type for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild to update</param>
    /// <param name="type">The new MultiGreet type (0: MultiGreet, 1: RandomGreet, 3: Off)</param>
    /// <returns>Success or bad request response</returns>
    [HttpPut("type")]
    public async Task<IActionResult> UpdateGreetType(ulong guildId, [FromBody] int type)
    {
        if (type is < 0 or > 2)
            return BadRequest("Invalid greet type");

        var guild = client.GetGuild(guildId);
        auditContext.RecordBefore(await multiGreetService.GetMultiGreetType(guildId));
        await multiGreetService.SetMultiGreetType(guild, type);
        auditContext.RecordAfter(type);
        return Ok();
    }

    /// <summary>
    ///     Gets the current MultiGreet type for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild to check</param>
    /// <returns>The current MultiGreet type</returns>
    [HttpGet("type")]
    public async Task<IActionResult> GetGreetType(ulong guildId)
    {
        var type = await multiGreetService.GetMultiGreetType(guildId);
        return Ok(type);
    }

    private static async Task<Stream?> GetAvatarStream(string url)
    {
        using var http = new HttpClient();
        var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        var imgData = await response.Content.ReadAsByteArrayAsync();
        return imgData.ToStream();
    }
}