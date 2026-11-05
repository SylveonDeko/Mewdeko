using DataModel;
using Mewdeko.Controllers.Common.GreetMessages;
using Mewdeko.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Reads and changes a guild's goodbye, DM welcome and boost messages for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class GreetMessagesController(
    GuildSettingsService guildSettings,
    IDashboardAuditContext auditContext) : Controller
{
    private const int MaxDeleteAfter = 600;

    /// <summary>
    ///     Gets a message.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="kind">bye, dmgreet or boost.</param>
    [HttpGet("{kind}")]
    public async Task<IActionResult> Get(ulong guildId, string kind)
    {
        var config = await guildSettings.GetGuildConfig(guildId);
        if (config is null)
            return NotFound();

        return Read(config, kind) is { } dto ? Ok(dto) : BadRequest("UnknownKind");
    }

    /// <summary>
    ///     Saves a message. Every field is written.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="kind">bye, dmgreet or boost.</param>
    /// <param name="request">The message settings.</param>
    [HttpPost("{kind}")]
    public async Task<IActionResult> Update(ulong guildId, string kind, [FromBody] GreetMessageDto request)
    {
        var config = await guildSettings.GetGuildConfig(guildId);
        if (config is null)
            return NotFound();

        var before = Read(config, kind);
        if (before is null)
            return BadRequest("UnknownKind");

        if (request.DeleteAfter is < 0 or > MaxDeleteAfter)
            return BadRequest("InvalidDeleteAfter");

        auditContext.RecordBefore(before);

        var message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.SanitizeMentions();
        var channelId = request.ChannelId ?? 0;

        switch (kind.ToLowerInvariant())
        {
            case "bye":
                config.SendChannelByeMessage = request.Enabled;
                config.ChannelByeMessageText = message;
                config.ByeMessageChannelId = channelId;
                config.AutoDeleteByeMessagesTimer = request.DeleteAfter;
                break;
            case "dmgreet":
                config.SendDmGreetMessage = request.Enabled;
                config.DmGreetMessageText = message;
                break;
            case "boost":
                config.SendBoostMessage = request.Enabled;
                config.BoostMessage = message;
                config.BoostMessageChannelId = channelId;
                config.BoostMessageDeleteAfter = request.DeleteAfter;
                break;
        }

        await guildSettings.UpdateGuildConfig(guildId, config);

        var after = Read(config, kind)!;
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    private static GreetMessageDto? Read(GuildConfig config, string kind) => kind.ToLowerInvariant() switch
    {
        "bye" => new GreetMessageDto
        {
            Enabled = config.SendChannelByeMessage,
            Message = config.ChannelByeMessageText,
            ChannelId = config.ByeMessageChannelId == 0 ? null : config.ByeMessageChannelId,
            DeleteAfter = config.AutoDeleteByeMessagesTimer
        },
        "dmgreet" => new GreetMessageDto
        {
            Enabled = config.SendDmGreetMessage,
            Message = config.DmGreetMessageText
        },
        "boost" => new GreetMessageDto
        {
            Enabled = config.SendBoostMessage,
            Message = config.BoostMessage,
            ChannelId = config.BoostMessageChannelId == 0 ? null : config.BoostMessageChannelId,
            DeleteAfter = config.BoostMessageDeleteAfter
        },
        _ => null
    };
}
