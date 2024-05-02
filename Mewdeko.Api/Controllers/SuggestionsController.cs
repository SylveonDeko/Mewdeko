using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuggestionsController(DbService dbService) : Controller
{
    [HttpGet("{guildId}/{userId?}")]
    public async Task<IActionResult> GetSuggestions(ulong guildId, ulong userId = 0)
    {
        await using var uow = dbService.GetDbContext();
        var suggestions = await uow.Suggestions.ToLinqToDB().ToListAsync();
        if (suggestions.Count == 0)
            return NotFound("No suggestions in database at all.");

        var guildSuggestions = suggestions.Where(x => x.GuildId == guildId);

        if (!guildSuggestions.Any())
            return NotFound("No suggestions for this guild.");

        if (userId != 0)
        {
            var userSuggestions = guildSuggestions.Where(x => x.UserId == userId);
            if (!userSuggestions.Any())
                return NotFound("No suggestions for this user.");
            return Ok(userSuggestions);
        }

        return Ok(guildSuggestions);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSuggestion(int id)
    {
        await using var uow = dbService.GetDbContext();
        var suggestion = await uow.Suggestions.FirstOrDefaultAsync(x => x.Id == id);

        if (suggestion == null)
            return NotFound();

        uow.Suggestions.Remove(suggestion);
        await uow.SaveChangesAsync();

        return Ok();
    }
}