using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Controllers.Common.UtilityExtras;
using Mewdeko.Modules.Utility.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Reads and edits messages the bot posted, by message link, for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class BotMessagesController(
    DiscordShardedClient client,
    BotMessageEditService editor,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Finds a message the bot posted and returns its text and embeds as editable content.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="link">The message link.</param>
    [HttpGet]
    public async Task<IActionResult> GetMessage(ulong guildId, [FromQuery] string link)
    {
        var (result, message) = await editor.FindAsync(guildId, link);
        if (message == null)
            return Failure(result);

        if (!await CanManageAsync(guildId, message))
            return Forbid();

        return Ok(ToDto(guildId, message, BotMessageEditService.ToEditableContent(message)));
    }

    /// <summary>
    ///     Replaces the text and embeds of a message the bot posted.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The link and the new content.</param>
    [HttpPut]
    public async Task<IActionResult> EditMessage(ulong guildId, [FromBody] EditBotMessageRequest request)
    {
        var (result, message) = await editor.FindAsync(guildId, request.Link);
        if (message == null)
            return Failure(result);

        if (!await CanManageAsync(guildId, message))
            return Forbid();

        auditContext.RecordBefore(ToDto(guildId, message, BotMessageEditService.ToEditableContent(message)));

        result = await editor.EditAsync(message, guildId, request.Content);
        if (result != BotMessageEditResult.Ok)
            return Failure(result);

        auditContext.RecordAfter(ToDto(guildId, message, request.Content));
        return Ok();
    }

    private async Task<bool> CanManageAsync(ulong guildId, IUserMessage message)
    {
        var userId = await HttpContext.GetDashboardUserIdAsync();
        if (userId == null)
            return true;

        var user = client.GetGuild(guildId)?.GetUser(userId.Value);
        return user != null && message.Channel is IGuildChannel channel &&
               user.GetPermissions(channel).ManageMessages;
    }

    private IActionResult Failure(BotMessageEditResult result)
    {
        return result switch
        {
            BotMessageEditResult.NotFound => NotFound("Message not found"),
            BotMessageEditResult.NotMine => BadRequest("The bot did not post that message"),
            BotMessageEditResult.WrongGuild => BadRequest("That message is in another server"),
            BotMessageEditResult.Empty => BadRequest("The message would be empty"),
            _ => BadRequest("That is not a message link")
        };
    }

    private static BotMessageDto ToDto(ulong guildId, IUserMessage message, string content)
    {
        return new BotMessageDto
        {
            ChannelId = message.Channel.Id,
            MessageId = message.Id,
            Link = $"https://discord.com/channels/{guildId}/{message.Channel.Id}/{message.Id}",
            Content = content
        };
    }
}
