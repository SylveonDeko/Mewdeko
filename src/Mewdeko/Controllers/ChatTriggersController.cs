using Mewdeko.Modules.Chat_Triggers.Services;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
/// Api endpoint to for chat triggers for guilds
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
public class ChatTriggersController(ChatTriggersService service) : Controller
{

    /// <summary>
    /// Retrieves chat triggers for a guild id
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
    /// Updates the provided trigger for a guild
    /// </summary>
    /// <param name="guildId">The guild id to update a trigger for</param>
    /// <param name="toUpdate">The updated trigger info</param>
    /// <returns></returns>
    [HttpPatch]
    public async Task<IActionResult> UpdateTriggerForGuild(ulong guildId, [FromBody] ChatTriggers toUpdate)
    {
        await service.UpdateInternalAsync(guildId, toUpdate);
        return Ok();
    }

    /// <summary>
    /// Adds a trigger to a guild
    /// </summary>
    /// <param name="guildId">The guild id to add the triggers for</param>
    /// <param name="toAdd">The trigger to add</param>
    /// <returns>The model that was added including its ID</returns>
    [HttpPost]
    public async Task<IActionResult> AddTriggerToGuild(ulong guildId, [FromBody] ChatTriggers toAdd)
    {
        var added = await service.AddTrigger(guildId, toAdd);
        return Ok(added);
    }

    /// <summary>
    /// Remove a trigger
    /// </summary>
    /// <param name="guildId">The guild to remove it from</param>
    /// <param name="id">The id of the trigger</param>
    /// <returns></returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> RemoveTriggerFromGuild(ulong guildId, int id)
    {
        await service.DeleteAsync(guildId, id);
        return Ok();
    }

}