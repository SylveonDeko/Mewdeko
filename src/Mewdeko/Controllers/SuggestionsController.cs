using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Suggestions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Service for managing suggestions for guilds
/// </summary>
/// <param name="service"></param>
[ApiController]
[Route("api/[controller]/{guildId}")]
public class SuggestionsController(SuggestionsService service, DbContextProvider provider, DiscordShardedClient client)
    : Controller
{
    /// <summary>
    ///     Gets suggestions for a guild, optionally for a user in a guild
    /// </summary>
    /// <param name="guildId">The guildid to retrieve suggestions for</param>
    /// <param name="userId">The user to retrieve suggestions for. (Optional)</param>
    /// <returns>A 404 if data is not found, or an 200 with data if found.</returns>
    [HttpGet("{userId?}")]
    public async Task<IActionResult> GetSuggestions(ulong guildId, ulong userId = 0)
    {
        var suggestions = await service.Suggestions(guildId);


        if (suggestions.Count == 0)
            return NotFound("No suggestions for this guild.");

        if (userId == 0) return Ok(suggestions);
        var userSuggestions = suggestions.Where(x => x.UserId == userId);
        if (!userSuggestions.Any())
            return NotFound("No suggestions for this user.");
        return Ok(userSuggestions);
    }

    /// <summary>
    ///     Removes a suggestion by its ID
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSuggestion(ulong guildId, ulong id)
    {
        var context = await provider.GetContextAsync();
        var suggestion = await service.Suggestions(guildId, id);

        if (suggestion == null || suggestion.Length == 0)
            return NotFound();

        context.Suggestions.Remove(suggestion.FirstOrDefault());
        await context.SaveChangesAsync();

        return Ok();
    }

    /// <summary>
    ///     Updates the status of a suggestion in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the suggestion was made.</param>
    /// <param name="userId">The ID of the user updating the suggestion status.</param>
    /// <param name="id">The ID of the suggestion to update.</param>
    /// <param name="state">The new state of the suggestion.</param>
    /// <param name="reason">The reason for the status update (optional).</param>
    /// <returns>An IActionResult indicating the result of the operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the state is not a valid SuggestState value.</exception>
    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateSuggestionStatus(ulong guildId, ulong id,
        [FromBody] SuggestStateUpdate update)
    {
        var guild = client.GetGuild(guildId);
        var user = client.GetUser(update.UserId);
        switch (update.State)
        {
            case SuggestionsService.SuggestState.Accepted:
                await service.SendAcceptEmbed(guild, user, id, reason: update.Reason);
                break;
            case SuggestionsService.SuggestState.Denied:
                await service.SendDenyEmbed(guild, user, id, reason: update.Reason);
                break;
            case SuggestionsService.SuggestState.Considered:
                await service.SendConsiderEmbed(guild, user, id, reason: update.Reason);
                break;
            case SuggestionsService.SuggestState.Implemented:
                await service.SendImplementEmbed(guild, user, id, reason: update.Reason);
                break;
            case SuggestionsService.SuggestState.Suggested:
            default:
                throw new ArgumentOutOfRangeException(nameof(update.State), update.State, null);
        }

        return Ok();
    }

    /// <summary>
    /// </summary>
    public class SuggestStateUpdate
    {
        /// <summary>
        /// </summary>
        public SuggestionsService.SuggestState State { get; set; }

        /// <summary>
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// </summary>
        public ulong UserId { get; set; }
    }
}