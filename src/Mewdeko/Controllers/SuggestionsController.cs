using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Suggestions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Service for managing suggestions for guilds
/// </summary>
/// <param name="service"></param>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class SuggestionsController(
    SuggestionsService service,
    DbContextProvider provider,
    DiscordShardedClient client)
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
    ///     Gets the minimum length for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The minimum length for suggestions</returns>
    [HttpGet("minLength")]
    public async Task<IActionResult> GetMinLength(ulong guildId)
    {
        var minLength = await service.GetMinLength(guildId);
        return Ok(minLength);
    }

    /// <summary>
    ///     Sets the minimum length for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="minLength">The new minimum length for suggestions</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("minLength")]
    public async Task<IActionResult> SetMinLength(ulong guildId, [FromBody] int minLength)
    {
        var guild = client.GetGuild(guildId);
        await service.SetMinLength(guild, minLength);
        return Ok();
    }

    /// <summary>
    ///     Gets the maximum length for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The maximum length for suggestions</returns>
    [HttpGet("maxLength")]
    public async Task<IActionResult> GetMaxLength(ulong guildId)
    {
        var maxLength = await service.GetMaxLength(guildId);
        return Ok(maxLength);
    }

    /// <summary>
    ///     Sets the maximum length for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="maxLength">The new maximum length for suggestions</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("maxLength")]
    public async Task<IActionResult> SetMaxLength(ulong guildId, [FromBody] int maxLength)
    {
        var guild = client.GetGuild(guildId);
        await service.SetMaxLength(guild, maxLength);
        return Ok();
    }

    /// <summary>
    ///     Gets the accept message for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The accept message for suggestions</returns>
    [HttpGet("acceptMessage")]
    public async Task<IActionResult> GetAcceptMessage(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var message = await service.GetAcceptMessage(guild);
        return Ok(message);
    }

    /// <summary>
    ///     Sets the accept message for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="message">The new accept message for suggestions</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("acceptMessage")]
    public async Task<IActionResult> SetAcceptMessage(ulong guildId, [FromBody] string message)
    {
        var guild = client.GetGuild(guildId);
        await service.SetAcceptMessage(guild, message);
        return Ok();
    }

    /// <summary>
    ///     Gets the deny message for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The deny message for suggestions</returns>
    [HttpGet("denyMessage")]
    public async Task<IActionResult> GetDenyMessage(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var message = await service.GetDenyMessage(guild);
        return Ok(message);
    }

    /// <summary>
    ///     Sets the deny message for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="message">The new deny message for suggestions</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("denyMessage")]
    public async Task<IActionResult> SetDenyMessage(ulong guildId, [FromBody] string message)
    {
        var guild = client.GetGuild(guildId);
        await service.SetDenyMessage(guild, message);
        return Ok();
    }

    /// <summary>
    ///     Gets the consider message for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The consider message for suggestions</returns>
    [HttpGet("considerMessage")]
    public async Task<IActionResult> GetConsiderMessage(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var message = await service.GetConsiderMessage(guild);
        return Ok(message);
    }

    /// <summary>
    ///     Sets the consider message for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="message">The new consider message for suggestions</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("considerMessage")]
    public async Task<IActionResult> SetConsiderMessage(ulong guildId, [FromBody] string message)
    {
        var guild = client.GetGuild(guildId);
        await service.SetConsiderMessage(guild, message);
        return Ok();
    }

    /// <summary>
    ///     Gets the implement message for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The implement message for suggestions</returns>
    [HttpGet("implementMessage")]
    public async Task<IActionResult> GetImplementMessage(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var message = await service.GetImplementMessage(guild);
        return Ok(message);
    }

    /// <summary>
    ///     Sets the implement message for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="message">The new implement message for suggestions</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("implementMessage")]
    public async Task<IActionResult> SetImplementMessage(ulong guildId, [FromBody] string message)
    {
        var guild = client.GetGuild(guildId);
        await service.SetImplementMessage(guild, message);
        return Ok();
    }

    /// <summary>
    ///     Gets the accept channel for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The ID of the accept channel for suggestions</returns>
    [HttpGet("acceptChannel")]
    public async Task<IActionResult> GetAcceptChannel(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var channelId = await service.GetAcceptChannel(guild);
        return Ok(channelId);
    }

    /// <summary>
    ///     Sets the accept channel for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The ID of the new accept channel for suggestions</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("acceptChannel")]
    public async Task<IActionResult> SetAcceptChannel(ulong guildId, [FromBody] ulong channelId)
    {
        var guild = client.GetGuild(guildId);
        await service.SetAcceptChannel(guild, channelId);
        return Ok();
    }

    /// <summary>
    ///     Gets the deny channel for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The ID of the deny channel for suggestions</returns>
    [HttpGet("denyChannel")]
    public async Task<IActionResult> GetDenyChannel(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var channelId = await service.GetDenyChannel(guild);
        return Ok(channelId);
    }

    /// <summary>
    ///     Sets the deny channel for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The ID of the new deny channel for suggestions</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("denyChannel")]
    public async Task<IActionResult> SetDenyChannel(ulong guildId, [FromBody] ulong channelId)
    {
        var guild = client.GetGuild(guildId);
        await service.SetDenyChannel(guild, channelId);
        return Ok();
    }

    /// <summary>
    ///     Gets the consider channel for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The ID of the consider channel for suggestions</returns>
    [HttpGet("considerChannel")]
    public async Task<IActionResult> GetConsiderChannel(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var channelId = await service.GetConsiderChannel(guild);
        return Ok(channelId);
    }

    /// <summary>
    ///     Sets the consider channel for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The ID of the new consider channel for suggestions</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("considerChannel")]
    public async Task<IActionResult> SetConsiderChannel(ulong guildId, [FromBody] ulong channelId)
    {
        var guild = client.GetGuild(guildId);
        await service.SetConsiderChannel(guild, channelId);
        return Ok();
    }

    /// <summary>
    ///     Gets the implement channel for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The ID of the implement channel for suggestions</returns>
    [HttpGet("implementChannel")]
    public async Task<IActionResult> GetImplementChannel(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var channelId = await service.GetImplementChannel(guild);
        return Ok(channelId);
    }

    /// <summary>
    ///     Sets the implement channel for suggestions in a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The ID of the new implement channel for suggestions</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("implementChannel")]
    public async Task<IActionResult> SetImplementChannel(ulong guildId, [FromBody] ulong channelId)
    {
        var guild = client.GetGuild(guildId);
        await service.SetImplementChannel(guild, channelId);
        return Ok();
    }

    /// <summary>
    ///     Gets the suggest threads type for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The suggest threads type</returns>
    [HttpGet("suggestThreadsType")]
    public async Task<IActionResult> GetSuggestThreadsType(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var type = await service.GetThreadType(guild);
        return Ok(type);
    }

    /// <summary>
    ///     Sets the suggest threads type for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="type">The new suggest threads type</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("suggestThreadsType")]
    public async Task<IActionResult> SetSuggestThreadsType(ulong guildId, [FromBody] int type)
    {
        var guild = client.GetGuild(guildId);
        await service.SetSuggestThreadsType(guild, type);
        return Ok();
    }

    /// <summary>
    ///     Gets the suggest button channel for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The ID of the suggest button channel</returns>
    [HttpGet("suggestButtonChannel")]
    public async Task<IActionResult> GetSuggestButtonChannel(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var channelId = await service.GetSuggestButtonChannel(guild);
        return Ok(channelId);
    }

    /// <summary>
    ///     Sets the suggest button channel for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The ID of the new suggest button channel</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("suggestButtonChannel")]
    public async Task<IActionResult> SetSuggestButtonChannel(ulong guildId, [FromBody] ulong channelId)
    {
        var guild = client.GetGuild(guildId);
        await service.SetSuggestButtonChannel(guild, channelId);
        return Ok();
    }

    /// <summary>
    ///     Gets the suggest button message for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The suggest button message</returns>
    [HttpGet("suggestButtonMessage")]
    public async Task<IActionResult> GetSuggestButtonMessage(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var message = await service.GetSuggestButtonMessage(guild);
        return Ok(message == "-" ? "" : message);
    }

    /// <summary>
    ///     Sets the suggest button message for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="message">The new suggest button message</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("suggestButtonMessage")]
    public async Task<IActionResult> SetSuggestButtonMessage(ulong guildId, [FromBody] string message)
    {
        var guild = client.GetGuild(guildId);
        await service.SetSuggestButtonMessage(guild, message);
        return Ok();
    }

    /// <summary>
    ///     Gets the suggest button label for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The suggest button label</returns>
    [HttpGet("suggestButtonLabel")]
    public async Task<IActionResult> GetSuggestButtonLabel(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var label = await service.GetSuggestButton(guild);
        return Ok(label);
    }

    /// <summary>
    ///     Sets the suggest button label for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="label">The new suggest button label</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("suggestButtonLabel")]
    public async Task<IActionResult> SetSuggestButtonLabel(ulong guildId, [FromBody] string label)
    {
        var guild = client.GetGuild(guildId);
        await service.SetSuggestButtonLabel(guild, label);
        return Ok();
    }

    /// <summary>
    ///     Gets the suggest button emote for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The suggest button emote</returns>
    [HttpGet("suggestButtonEmote")]
    public async Task<IActionResult> GetSuggestButtonEmote(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var emote = await service.GetSuggestButtonEmote(guild);
        return Ok(emote == "-" ? "" : emote);
    }

    /// <summary>
    ///     Sets the suggest button emote for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="emote">The new suggest button emote</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("suggestButtonEmote")]
    public async Task<IActionResult> SetSuggestButtonEmote(ulong guildId, [FromBody] string emote)
    {
        var guild = client.GetGuild(guildId);
        await service.SetSuggestButtonEmote(guild, emote);
        return Ok();
    }

    /// <summary>
    ///     Gets the archive on deny setting for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The archive on deny setting</returns>
    [HttpGet("archiveOnDeny")]
    public async Task<IActionResult> GetArchiveOnDeny(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var archiveOnDeny = await service.GetArchiveOnDeny(guild);
        return Ok(archiveOnDeny);
    }

    /// <summary>
    ///     Sets the archive on deny setting for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="archiveOnDeny">The new archive on deny setting</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("archiveOnDeny")]
    public async Task<IActionResult> SetArchiveOnDeny(ulong guildId, [FromBody] bool archiveOnDeny)
    {
        var guild = client.GetGuild(guildId);
        await service.SetArchiveOnDeny(guild, archiveOnDeny);
        return Ok();
    }

    /// <summary>
    ///     Gets the archive on accept setting for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The archive on accept setting</returns>
    [HttpGet("archiveOnAccept")]
    public async Task<IActionResult> GetArchiveOnAccept(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var archiveOnAccept = await service.GetArchiveOnAccept(guild);
        return Ok(archiveOnAccept);
    }

    /// <summary>
    ///     Sets the archive on accept setting for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="archiveOnAccept">The new archive on accept setting</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("archiveOnAccept")]
    public async Task<IActionResult> SetArchiveOnAccept(ulong guildId, [FromBody] bool archiveOnAccept)
    {
        var guild = client.GetGuild(guildId);
        await service.SetArchiveOnAccept(guild, archiveOnAccept);
        return Ok();
    }

    /// <summary>
    ///     Gets the archive on consider setting for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The archive on consider setting</returns>
    [HttpGet("archiveOnConsider")]
    public async Task<IActionResult> GetArchiveOnConsider(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var archiveOnConsider = await service.GetArchiveOnConsider(guild);
        return Ok(archiveOnConsider);
    }

    /// <summary>
    ///     Sets the archive on consider setting for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="archiveOnConsider">The new archive on consider setting</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("archiveOnConsider")]
    public async Task<IActionResult> SetArchiveOnConsider(ulong guildId, [FromBody] bool archiveOnConsider)
    {
        var guild = client.GetGuild(guildId);
        await service.SetArchiveOnConsider(guild, archiveOnConsider);
        return Ok();
    }

    /// <summary>
    ///     Gets the archive on implement setting for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The archive on implement setting</returns>
    [HttpGet("archiveOnImplement")]
    public async Task<IActionResult> GetArchiveOnImplement(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var archiveOnImplement = await service.GetArchiveOnImplement(guild);
        return Ok(archiveOnImplement);
    }

    /// <summary>
    ///     Sets the archive on implement setting for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="archiveOnImplement">The new archive on implement setting</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("archiveOnImplement")]
    public async Task<IActionResult> SetArchiveOnImplement(ulong guildId, [FromBody] bool archiveOnImplement)
    {
        var guild = client.GetGuild(guildId);
        await service.SetArchiveOnImplement(guild, archiveOnImplement);
        return Ok();
    }

    /// <summary>
    ///     Gets the suggest emotes for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The suggest emotes</returns>
    [HttpGet("suggestEmotes")]
    public async Task<IActionResult> GetSuggestEmotes(ulong guildId)
    {
        var emotes = await service.GetEmotes(guildId);
        return Ok(emotes);
    }

    /// <summary>
    ///     Sets the suggest emotes for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="emotes">The new suggest emotes</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("suggestEmotes")]
    public async Task<IActionResult> SetSuggestEmotes(ulong guildId, [FromBody] string emotes)
    {
        var guild = client.GetGuild(guildId);
        await service.SetSuggestionEmotes(guild, emotes);
        return Ok("");
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