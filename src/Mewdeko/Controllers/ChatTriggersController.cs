using DataModel;
using Mewdeko.Controllers.Common.ChatTriggers;
using Mewdeko.Modules.Chat_Triggers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Api endpoint to for chat triggers for guilds
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class ChatTriggersController(
    ChatTriggersService service,
    TriggerCounterService counters,
    DiscordShardedClient client,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Retrieves chat triggers for a guild id
    /// </summary>
    /// <param name="guildId">The guildid to get triggers for</param>
    /// <returns>Either a 404 if none are found or a CtModel array of triggers.</returns>
    [HttpGet]
    public async Task<IActionResult> GetTriggersForGuild(ulong guildId)
    {
        try
        {
            var triggers = await service.GetChatTriggersFor(guildId);
            return Ok(triggers);
        }
        catch
        {
            return NotFound();
        }
    }

    /// <summary>
    ///     Updates the provided trigger for a guild
    /// </summary>
    /// <param name="guildId">The guild id to update a trigger for</param>
    /// <param name="toUpdate">The updated trigger info</param>
    /// <returns></returns>
    [HttpPatch]
    public async Task<IActionResult> UpdateTriggerForGuild(ulong guildId, [FromBody] ChatTrigger toUpdate)
    {
        var existing = (await service.GetChatTriggersFor(guildId)).FirstOrDefault(t => t.Id == toUpdate.Id);
        auditContext.RecordBefore(existing);
        await service.UpdateInternalAsync(guildId, toUpdate);
        auditContext.RecordAfter(toUpdate);
        return Ok();
    }

    /// <summary>
    ///     Adds a trigger to a guild
    /// </summary>
    /// <param name="guildId">The guild id to add the triggers for</param>
    /// <param name="toAdd">The trigger to add</param>
    /// <returns>The model that was added including its ID</returns>
    [HttpPost]
    public async Task<IActionResult> AddTriggerToGuild(ulong guildId, [FromBody] ChatTrigger toAdd)
    {
        var added = await service.AddTrigger(guildId, toAdd);
        auditContext.RecordAfter(added);
        return Ok(added);
    }

    /// <summary>
    ///     Remove a trigger
    /// </summary>
    /// <param name="guildId">The guild to remove it from</param>
    /// <param name="id">The id of the trigger</param>
    /// <returns></returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> RemoveTriggerFromGuild(ulong guildId, int id)
    {
        var existing = (await service.GetChatTriggersFor(guildId)).FirstOrDefault(t => t.Id == id);
        auditContext.RecordBefore(existing);
        await service.DeleteAsync(guildId, id);
        return Ok();
    }

    /// <summary>
    ///     Enables or disables every trigger in a category at once.
    /// </summary>
    /// <param name="guildId">The guild to act on.</param>
    /// <param name="request">The category and the state to set it to.</param>
    /// <returns>The number of triggers changed.</returns>
    [HttpPost("category/toggle")]
    public async Task<IActionResult> ToggleCategory(ulong guildId, [FromBody] CategoryToggleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Category))
            return BadRequest();

        var changed = await service.SetCategoryDisabledAsync(guildId, request.Category, request.Disabled);
        return Ok(new
        {
            changed
        });
    }

    /// <summary>
    ///     Lists the counters chat triggers in a guild read and update.
    /// </summary>
    /// <param name="guildId">The guild to list counters for.</param>
    /// <returns>The guild's counters.</returns>
    [HttpGet("counters")]
    public async Task<IActionResult> GetCounters(ulong guildId)
    {
        return Ok(await counters.ListAsync(guildId));
    }

    /// <summary>
    ///     Sets a counter to an exact value, creating it when it does not exist.
    /// </summary>
    /// <param name="guildId">The guild the counter belongs to.</param>
    /// <param name="request">The counter name and value.</param>
    /// <returns>An empty OK response.</returns>
    [HttpPost("counters")]
    public async Task<IActionResult> SetCounter(ulong guildId, [FromBody] CounterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest();

        await counters.SetAsync(guildId, request.Name.ToLowerInvariant(), request.Value);
        return Ok();
    }

    /// <summary>
    ///     Deletes a counter along with every per-user value stored under its name.
    /// </summary>
    /// <param name="guildId">The guild the counter belongs to.</param>
    /// <param name="name">The counter's name.</param>
    /// <returns>The number of rows removed.</returns>
    [HttpDelete("counters/{name}")]
    public async Task<IActionResult> DeleteCounter(ulong guildId, string name)
    {
        var removed = await counters.DeleteAsync(guildId, name.ToLowerInvariant());
        return Ok(new
        {
            removed
        });
    }

    /// <summary>
    ///     Lists the contextual placeholders available in trigger responses.
    /// </summary>
    /// <param name="guildId">The guild being configured. Placeholders are the same everywhere.</param>
    /// <returns>The available placeholder tokens.</returns>
    [HttpGet("placeholders")]
    public IActionResult GetPlaceholders(ulong guildId)
    {
        return Ok(service.GetContextualPlaceholders());
    }

    /// <summary>
    ///     Reports whether a trigger would fire for a sample message, and what blocks it if not.
    /// </summary>
    /// <param name="guildId">The guild to test in.</param>
    /// <param name="id">The trigger to test.</param>
    /// <param name="request">The sample message and the user to test as.</param>
    /// <returns>Whether the sample matched, and the reason it is blocked if it is.</returns>
    /// <remarks>
    ///     Nothing is charged, sent or recorded. The dashboard user supplies the member to test as, since a trigger's
    ///     result depends on that member's roles, level and balance.
    /// </remarks>
    [HttpPost("{id:int}/test")]
    public async Task<IActionResult> TestTrigger(ulong guildId, int id, [FromBody] TriggerTestRequest request)
    {
        var trigger = await service.GetGuildOrGlobalTriggers(guildId, id);
        if (trigger is null)
            return NotFound();

        if (client.GetGuild(guildId) is not { } guild)
            return NotFound();

        var user = guild.GetUser(request.UserId);
        if (user is null)
            return BadRequest();

        var channel = request.ChannelId == 0
            ? guild.DefaultChannel
            : guild.GetTextChannel(request.ChannelId);

        if (channel is null)
            return BadRequest();

        var (matched, blocker) = await service.TestTriggerAsync(trigger, guild, user, channel,
            request.Sample ?? "");

        return Ok(new
        {
            matched, blocker, wouldFire = matched && blocker is null
        });
    }

    /// <summary>
    ///     Gets how often a trigger has fired, and its most recent fires.
    /// </summary>
    /// <param name="guildId">The guild to read history for.</param>
    /// <param name="id">The trigger to read history for.</param>
    /// <returns>The total number of fires and the most recent ones.</returns>
    [HttpGet("{id:int}/stats")]
    public async Task<IActionResult> GetTriggerStats(ulong guildId, int id)
    {
        var (total, recent) = await service.GetTriggerHistoryAsync(guildId, id);
        return Ok(new
        {
            total, recent
        });
    }
}