using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Suggestions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
/// Service for managing suggestions for guilds
/// </summary>
/// <param name="service"></param>
[ApiController]
[Route("api/[controller]/{guildId}")]
public class SuggestionsController(SuggestionsService service, DbContextProvider provider) : Controller
{
    /// <summary>
    /// Gets suggestions for a guild, optionally for a user in a guild
    /// </summary>
    /// <param name="guildId">The guildid to retrieve suggestions for</param>
    /// <param name="userId">The user to retrieve suggestions for. (Optional)</param>
    /// <returns>A 404 if data is not found, or an 200 with data if found.</returns>
    [HttpGet("{userId?}")]
    public async Task<IActionResult> GetSuggestions(ulong guildId, ulong userId = 0)
    {

        var suggestions = await service.Suggestions(guildId);


        if (suggestions.Count==0)
            return NotFound("No suggestions for this guild.");

        if (userId == 0) return Ok(suggestions);
        var userSuggestions = suggestions.Where(x => x.UserId == userId);
        if (!userSuggestions.Any())
            return NotFound("No suggestions for this user.");
        return Ok(userSuggestions);

    }

    /// <summary>
    /// Removes a suggestion by its ID
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSuggestion(ulong guildId, ulong id)
    {
        var context = await provider.GetContextAsync();
        var suggestion = await service.Suggestions(guildId, id);

        if (suggestion == null || suggestion.Length==0)
            return NotFound();

        context.Suggestions.Remove(suggestion.FirstOrDefault());
        await context.SaveChangesAsync();

        return Ok();
    }
}