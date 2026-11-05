using Mewdeko.Controllers.Common.ChannelTools;
using Mewdeko.Modules.Server_Management.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Reads and changes a guild's auto-purge settings and voice text links for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class ChannelToolsController(
    DiscordShardedClient client,
    AutoPurgeService autoPurge,
    VoiceTextLinkService voiceText,
    ChannelTimedActionService timed,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Lists the channels with auto-purge.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet("autopurge")]
    public async Task<IActionResult> GetAutoPurge(ulong guildId) =>
        Ok((await autoPurge.GetAllAsync(guildId)).Select(ToDto));

    /// <summary>
    ///     Turns on auto-purge for a channel or changes it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The settings.</param>
    [HttpPost("autopurge")]
    public async Task<IActionResult> SetAutoPurge(ulong guildId, [FromBody] AutoPurgeDto request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found");
        if (guild.GetTextChannel(request.ChannelId) is null)
            return BadRequest("Channel not found");

        var before = (await autoPurge.GetAllAsync(guildId)).FirstOrDefault(x => x.ChannelId == request.ChannelId);
        if (before is not null)
            auditContext.RecordBefore(ToDto(before));

        var (error, setting) = await autoPurge.SetAsync(guildId, request.ChannelId, request.MaxAgeMinutes,
            request.IntervalMinutes, request.Filter, request.KeepPinned);
        if (error != AutoPurgeError.None)
            return BadRequest(error.ToString());

        var after = ToDto(setting!);
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    /// <summary>
    ///     Turns off auto-purge for a channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    [HttpDelete("autopurge/{channelId}")]
    public async Task<IActionResult> DeleteAutoPurge(ulong guildId, ulong channelId)
    {
        var before = (await autoPurge.GetAllAsync(guildId)).FirstOrDefault(x => x.ChannelId == channelId);
        if (before is null)
            return NotFound("Channel has no auto-purge");

        auditContext.RecordBefore(ToDto(before));
        await autoPurge.RemoveAsync(channelId);
        return Ok();
    }

    /// <summary>
    ///     Purges a channel now with its saved settings.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>How many messages were deleted.</returns>
    [HttpPost("autopurge/{channelId}/run")]
    public async Task<IActionResult> RunAutoPurge(ulong guildId, ulong channelId)
    {
        if (!(await autoPurge.GetAllAsync(guildId)).Any(x => x.ChannelId == channelId))
            return NotFound("Channel has no auto-purge");

        var deleted = await autoPurge.RunNowAsync(channelId);
        return deleted is null ? BadRequest("The bot cannot purge that channel") : Ok(deleted);
    }

    /// <summary>
    ///     Lists the voice channels linked to text channels.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet("voicetext")]
    public IActionResult GetVoiceText(ulong guildId) => Ok(voiceText.GetLinks(guildId).Select(ToDto));

    /// <summary>
    ///     Links a voice channel to a text channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The link.</param>
    [HttpPost("voicetext")]
    public async Task<IActionResult> SetVoiceText(ulong guildId, [FromBody] VoiceTextLinkDto request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found");
        if (guild.GetVoiceChannel(request.VoiceChannelId) is null)
            return BadRequest("Voice channel not found");
        if (guild.GetTextChannel(request.TextChannelId) is null)
            return BadRequest("Text channel not found");

        var before = voiceText.GetLinks(guildId).FirstOrDefault(x => x.VoiceChannelId == request.VoiceChannelId);
        if (before is not null)
            auditContext.RecordBefore(ToDto(before));

        await voiceText.LinkAsync(guildId, request.VoiceChannelId, request.TextChannelId);
        auditContext.RecordAfter(request);
        return Ok(request);
    }

    /// <summary>
    ///     Removes the link of a voice channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="voiceChannelId">The voice channel ID.</param>
    [HttpDelete("voicetext/{voiceChannelId}")]
    public async Task<IActionResult> DeleteVoiceText(ulong guildId, ulong voiceChannelId)
    {
        var before = voiceText.GetLinks(guildId).FirstOrDefault(x => x.VoiceChannelId == voiceChannelId);
        if (before is null)
            return NotFound("Voice channel is not linked");

        auditContext.RecordBefore(ToDto(before));
        await voiceText.UnlinkAsync(voiceChannelId);
        return Ok();
    }

    /// <summary>
    ///     Lists the channels that are locked or hidden by the bot's lock and hide commands.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet("locks")]
    public async Task<IActionResult> GetLocks(ulong guildId) =>
        Ok((await timed.ListAsync(guildId)).Select(x => new ChannelTimedActionDto
        {
            ChannelId = x.ChannelId,
            Kind = x.Kind,
            EndsAt = x.EndsAt is { } ends ? DateTime.SpecifyKind(ends, DateTimeKind.Utc) : null,
            CreatedBy = x.CreatedBy
        }));

    private static AutoPurgeDto ToDto(DataModel.AutoPurgeSetting setting) => new()
    {
        ChannelId = setting.ChannelId,
        MaxAgeMinutes = setting.MaxAgeMinutes,
        IntervalMinutes = setting.IntervalMinutes,
        Filter = setting.Filter,
        KeepPinned = setting.KeepPinned,
        LastRunAt = setting.LastRunAt is { } last ? DateTime.SpecifyKind(last, DateTimeKind.Utc) : null
    };

    private static VoiceTextLinkDto ToDto(DataModel.VoiceTextLink link) => new()
    {
        VoiceChannelId = link.VoiceChannelId, TextChannelId = link.TextChannelId
    };
}
