using LinqToDB;
using Mewdeko.Controllers.Common.Suggestions;
using Mewdeko.Modules.Suggestions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Service for managing suggestions for guilds
/// </summary>
/// <param name="service"></param>
/// <param name="client"></param>
/// <param name="dbFactory"></param>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class SuggestionsController(
    SuggestionsService service,
    DiscordShardedClient client,
    IDataConnectionFactory dbFactory)
    : Controller
{
    /// <summary>
    ///     Gets suggestions for a guild, optionally for a user in a guild
    /// </summary>
    /// <param name="guildId">The guildid to retrieve suggestions for</param>
    /// <param name="userId">The user to retrieve suggestions for. (Optional)</param>
    /// <returns>A 404 if data is not found, or an 200 with data if found.</returns>
    [HttpGet("{userId?}")]
    public async Task<IActionResult> GetSuggestions(ulong guildId, ulong? userId = null)
    {
        var suggestions = await service.Suggestions(guildId);


        if (suggestions.Count == 0)
            return NotFound("No suggestions for this guild.");

        // Fetch user info from guild cache only (no expensive REST calls)
        var guild = client.GetGuild(guildId);
        var uniqueUserIds = suggestions.Select(s => s.UserId).Distinct().ToList();

        // Bulk fetch all cached guild users at once instead of individual GetUser calls
        var guildUsersLookup = guild?.Users.ToDictionary(u => u.Id) ?? new Dictionary<ulong, SocketGuildUser>();
        var userInfoMap = new Dictionary<ulong, object>();

        foreach (var uid in uniqueUserIds)
        {
            if (guildUsersLookup.TryGetValue(uid, out var guildUser))
            {
                userInfoMap[uid] = new
                {
                    Id = guildUser.Id.ToString(),
                    guildUser.Username,
                    AvatarUrl = guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl()
                };
            }
            else
            {
                // User not in guild cache - don't make expensive REST calls
                userInfoMap[uid] = new
                {
                    Id = uid.ToString(),
                    Username = $"Unknown User ({uid})",
                    AvatarUrl = "https://cdn.discordapp.com/embed/avatars/0.png"
                };
            }
        }

        // Enrich suggestions with user data and emote counts
        var enrichedSuggestions = suggestions.Select(s => new
        {
            s.Id,
            s.GuildId,
            s.UserId,
            s.SuggestionId,
            s.Suggestion1,
            s.CurrentState,
            s.DateAdded,
            s.StateChangeUser,
            s.StateChangeCount,
            s.MessageId,
            EmoteCounts = new
            {
                Emote1 = s.EmoteCount1,
                Emote2 = s.EmoteCount2,
                Emote3 = s.EmoteCount3,
                Emote4 = s.EmoteCount4,
                Emote5 = s.EmoteCount5
            },
            User = userInfoMap[s.UserId]
        });

        if (userId is null) return Ok(enrichedSuggestions);
        var userSuggestions = enrichedSuggestions.Where(x => x.UserId == userId);
        if (!userSuggestions.Any())
            return NotFound("No suggestions for this user.");
        return Ok(userSuggestions);
    }

    /// <summary>
    ///     Removes a suggestion by its ID
    /// </summary>
    /// <param name="guildId">The ID of the guild where the suggestion exists</param>
    /// <param name="id">The ID of the suggestion to delete</param>
    /// <returns>OK if deleted successfully, NotFound if suggestion doesn't exist</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSuggestion(ulong guildId, ulong id)
    {
        // We still need the service to check if the suggestion exists
        var suggestion = await service.Suggestions(guildId, id);

        if (suggestion == null || suggestion.Length == 0)
            return NotFound();

        // Use linq2db to delete the suggestion
        await using var db = await dbFactory.CreateConnectionAsync();

        await db.Suggestions
            .Where(s => s.GuildId == guildId && s.SuggestionId == id)
            .DeleteAsync();

        return Ok();
    }

    /// <summary>
    ///     Updates the status of a suggestion in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the suggestion was made.</param>
    /// <param name="id">The ID of the suggestion to update.</param>
    /// <param name="update">The new state of the suggestion.</param>
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
    ///     Gets the suggest channel setting for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The suggestion channel id</returns>
    [HttpGet("suggestChannel")]
    public async Task<IActionResult> GetSuggestChannel(ulong guildId)
    {
        var archiveOnImplement = await service.GetSuggestionChannel(guildId);
        return Ok(archiveOnImplement);
    }

    /// <summary>
    ///     Sets the suggest channel setting for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelid">The new channelId for suggestions</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("suggestChannel")]
    public async Task<IActionResult> SetSuggestChannel(ulong guildId, [FromBody] ulong channelid)
    {
        var guild = client.GetGuild(guildId);
        await service.SetSuggestionChannelId(guild, channelid);
        return Ok();
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
    ///     Gets the suggestion message format for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The suggestion message format</returns>
    [HttpGet("suggestionMessage")]
    public async Task<IActionResult> GetSuggestionMessage(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var message = await service.GetSuggestionMessage(guild);
        return Ok(message == "-" ? "" : message);
    }

    /// <summary>
    ///     Sets the suggestion message format for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="message">The new suggestion message format</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("suggestionMessage")]
    public async Task<IActionResult> SetSuggestionMessage(ulong guildId, [FromBody] string message)
    {
        var guild = client.GetGuild(guildId);
        await service.SetSuggestionMessage(guild, message);
        return Ok();
    }

    /// <summary>
    ///     Gets the emote mode for suggestions in a guild (0 = reactions, 1 = buttons)
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The emote mode</returns>
    [HttpGet("emoteMode")]
    public async Task<IActionResult> GetEmoteMode(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var mode = await service.GetEmoteMode(guild);
        return Ok(mode);
    }

    /// <summary>
    ///     Sets the emote mode for suggestions in a guild (0 = reactions, 1 = buttons)
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="mode">The new emote mode</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("emoteMode")]
    public async Task<IActionResult> SetEmoteMode(ulong guildId, [FromBody] int mode)
    {
        var guild = client.GetGuild(guildId);
        await service.SetEmoteMode(guild, mode);
        return Ok();
    }

    /// <summary>
    ///     Gets the suggest button color for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>The button style/color</returns>
    [HttpGet("suggestButtonColor")]
    public async Task<IActionResult> GetSuggestButtonColor(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var color = await service.GetSuggestButtonColor(guild);
        return Ok((int)color);
    }

    /// <summary>
    ///     Sets the suggest button color for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="color">The new button color (1=Blue, 2=Grey, 3=Green, 4=Red)</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("suggestButtonColor")]
    public async Task<IActionResult> SetSuggestButtonColor(ulong guildId, [FromBody] int color)
    {
        var guild = client.GetGuild(guildId);
        await service.SetSuggestButtonColor(guild, color);
        return Ok();
    }

    /// <summary>
    ///     Gets the button style for a specific emote button
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="buttonId">The button number (1-5)</param>
    /// <returns>The button style for the specified emote</returns>
    [HttpGet("emoteButtonStyle/{buttonId}")]
    public async Task<IActionResult> GetEmoteButtonStyle(ulong guildId, int buttonId)
    {
        var guild = client.GetGuild(guildId);
        var style = await service.GetButtonStyle(guild, buttonId);
        return Ok((int)style);
    }

    /// <summary>
    ///     Sets the button style for a specific emote button
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="buttonId">The button number (1-5)</param>
    /// <param name="color">The new button color (1=Blue, 2=Grey, 3=Green, 4=Red)</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpPost("emoteButtonStyle/{buttonId}")]
    public async Task<IActionResult> SetEmoteButtonStyle(ulong guildId, int buttonId, [FromBody] int color)
    {
        var guild = client.GetGuild(guildId);
        await service.SetButtonType(guild, buttonId, color);
        return Ok();
    }

    /// <summary>
    ///     Clears all suggestions for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>An IActionResult indicating the result of the operation</returns>
    [HttpDelete("clear")]
    public async Task<IActionResult> ClearSuggestions(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var suggestions = await service.Suggestions(guildId);
        if (suggestions.Count == 0)
            return NotFound("No suggestions to clear");

        await service.SuggestReset(guild);
        return Ok();
    }
}