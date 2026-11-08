using DataModel;
using Discord.Net;
using Mewdeko.Controllers.Common.ChannelTools;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Modules.Server_Management;
using Mewdeko.Modules.Server_Management.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Locks a guild down and runs bulk channel actions such as slowmode, locks, cloning, syncing and purging for the
///     dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class ChannelActionsController(
    DiscordShardedClient client,
    ChannelCommandService lockdown,
    ChannelEditService edit,
    ChannelTimedActionService timed,
    PurgeService purge,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Gets whether the guild is locked down and how.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet("lockdown")]
    public IActionResult GetLockdown(ulong guildId)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");

        return Ok(Status(guild));
    }

    /// <summary>
    ///     Locks the guild down against joins, messages or both.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The kind of lockdown.</param>
    [HttpPost("lockdown")]
    public async Task<IActionResult> StartLockdown(ulong guildId, [FromBody] LockdownRequest request)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");

        var type = request.Type;
        if (type is ServerManagement.LockdownType.Joins or ServerManagement.LockdownType.Full)
        {
            if (request.Action is not (PunishmentAction.Kick or PunishmentAction.Ban))
                return BadRequest("Joiners can only be kicked or banned");

            var missingAction = await lockdown.CheckJoinLockdownActionPermissions(guild, request.Action);
            if (missingAction.Count != 0)
                return BadRequest($"The bot is missing: {string.Join(", ", missingAction)}");
        }

        if (type is ServerManagement.LockdownType.Full or ServerManagement.LockdownType.Readonly)
        {
            var missing = await lockdown.CheckLockdownPermissions(guild, request.Override);
            if (missing.Count != 0 && !request.Override)
                return BadRequest($"The bot is missing: {string.Join(", ", missing)}");

            await lockdown.StoreOriginalPermissions(guild);
        }

        auditContext.RecordBefore(Status(guild));
        var (already, current) = await lockdown.LockdownGuild(guild, type, request.Action);
        if (already)
            return BadRequest($"The server is already in a {current} lockdown");

        if (type is ServerManagement.LockdownType.Full or ServerManagement.LockdownType.Readonly)
            await lockdown.ApplyLockdown(guild);

        var after = Status(guild);
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    /// <summary>
    ///     Lifts the guild's lockdown and puts the channel permissions back.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpDelete("lockdown")]
    public async Task<IActionResult> LiftLockdown(ulong guildId)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");
        if (lockdown.GetLockdownInfo(guild) is not { } info)
            return NotFound("The server is not locked down");

        auditContext.RecordBefore(Status(guild));
        await lockdown.LiftLockdown(guild);
        if (info.lockdownType != ServerManagement.LockdownType.Joins)
            await lockdown.RestoreOriginalPermissions(guild);

        auditContext.RecordAfter(Status(guild));
        return Ok(Status(guild));
    }

    /// <summary>
    ///     Sets the slowmode of several text channels.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The channels and the slowmode.</param>
    [HttpPost("slowmode")]
    public async Task<IActionResult> Slowmode(ulong guildId, [FromBody] BulkSlowmodeRequest request)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");
        if (request.Seconds is < 0 or > ChannelEditService.MaxSlowMode)
            return BadRequest($"Slowmode is 0 to {ChannelEditService.MaxSlowMode} seconds");

        var result = await Each(guild, request.ChannelIds, async channel =>
            channel is ITextChannel
            && await edit.EditAsync(channel, new ChannelEditRequest(SlowMode: request.Seconds)) ==
            ChannelEditError.None);
        auditContext.RecordAfter(new { request.ChannelIds, request.Seconds, result });
        return Ok(result);
    }

    /// <summary>
    ///     Locks or hides several channels, optionally for a set time.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The channels, kind and time.</param>
    [HttpPost("lock")]
    public async Task<IActionResult> Lock(ulong guildId, [FromBody] BulkLockRequest request)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");

        TimeSpan? duration = null;
        if (request.Minutes is { } minutes)
        {
            duration = TimeSpan.FromMinutes(minutes);
            if (duration < ChannelTimedActionService.MinDuration || duration > ChannelTimedActionService.MaxDuration)
                return BadRequest("The time must be between 1 minute and 365 days");
        }

        var result = await Each(guild, request.ChannelIds, async channel =>
        {
            await timed.ApplyAsync(channel, request.Kind, [], duration, request.UserId);
            return true;
        });
        auditContext.RecordAfter(new { request.ChannelIds, request.Kind, request.Minutes, result });
        return Ok(result);
    }

    /// <summary>
    ///     Undoes the locks or hides of several channels.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The channels and kind.</param>
    [HttpPost("unlock")]
    public async Task<IActionResult> Unlock(ulong guildId, [FromBody] BulkUnlockRequest request)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");

        var result = await Each(guild, request.ChannelIds, channel => timed.UndoAsync(channel.Id, request.Kind));
        auditContext.RecordAfter(new { request.ChannelIds, request.Kind, result });
        return Ok(result);
    }

    /// <summary>
    ///     Makes the permissions of several channels match their categories.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The channels.</param>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(ulong guildId, [FromBody] ChannelIdsRequest request)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");

        var result = await Each(guild, request.ChannelIds, channel => edit.SyncPermissionsAsync(channel));
        auditContext.RecordAfter(new { request.ChannelIds, result });
        return Ok(result);
    }

    /// <summary>
    ///     Copies a channel with its settings and permissions.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The channel and the name of the copy.</param>
    [HttpPost("clone")]
    public async Task<IActionResult> Clone(ulong guildId, [FromBody] CloneChannelRequest request)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");
        if (guild.GetChannel(request.ChannelId) is not { } channel)
            return BadRequest("Channel not found");

        try
        {
            var copy = await edit.CloneAsync(channel, request.Name);
            if (copy is null)
                return BadRequest("That kind of channel cannot be copied");

            auditContext.RecordAfter(new { request.ChannelId, copyId = copy.Id });
            return Ok(new { id = copy.Id, name = copy.Name });
        }
        catch (HttpException ex)
        {
            return BadRequest(ex.Reason ?? ex.Message);
        }
    }

    /// <summary>
    ///     Deletes a text channel and creates it again with the same settings, which clears its messages.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The channel.</param>
    [HttpPost("nuke")]
    public async Task<IActionResult> Nuke(ulong guildId, [FromBody] CloneChannelRequest request)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");
        if (guild.GetTextChannel(request.ChannelId) is not { } channel)
            return BadRequest("Text channel not found");

        try
        {
            auditContext.RecordBefore(new { channelId = channel.Id, name = channel.Name });
            var copy = await edit.CloneAsync(channel, null);
            if (copy is null)
                return BadRequest("That kind of channel cannot be recreated");

            await channel.DeleteAsync();
            try
            {
                await copy.ModifyAsync(x => x.Position = channel.Position);
            }
            catch (Exception)
            {
            }

            auditContext.RecordAfter(new { channelId = copy.Id, name = copy.Name });
            return Ok(new { id = copy.Id, name = copy.Name });
        }
        catch (HttpException ex)
        {
            return BadRequest(ex.Reason ?? ex.Message);
        }
    }

    /// <summary>
    ///     Deletes recent messages from a text channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The channel, how many messages and which ones.</param>
    [HttpPost("purge")]
    public async Task<IActionResult> Purge(ulong guildId, [FromBody] PurgeChannelRequest request)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");
        if (guild.GetTextChannel(request.ChannelId) is not { } channel)
            return BadRequest("Text channel not found");
        if (request.Amount is < 1 or > 1000)
            return BadRequest("Amount is 1 to 1000");

        var perms = guild.CurrentUser.GetPermissions(channel);
        if (!perms.ViewChannel || !perms.ReadMessageHistory || !perms.ManageMessages)
            return BadRequest("The bot cannot purge that channel");

        var filter = request.Filter;
        var keepPinned = request.KeepPinned;
        bool Matches(IMessage message) =>
            !(keepPinned && message.IsPinned) && filter switch
            {
                AutoPurgeFilter.Humans => !message.Author.IsBot,
                AutoPurgeFilter.Bots => message.Author.IsBot,
                AutoPurgeFilter.Links => PurgeService.HasLink(message),
                AutoPurgeFilter.Attachments => PurgeService.HasFile(message),
                _ => true
            };

        auditContext.RecordAfter(request);
        await purge.PurgeWhere(channel, (ulong)request.Amount, Matches);
        return Ok();
    }

    private object Status(SocketGuild guild)
    {
        var info = lockdown.GetLockdownInfo(guild);
        return new LockdownStatusDto
        {
            Type = info?.lockdownType.ToString() ?? "None", Action = info?.action?.ToString()
        };
    }

    private async Task<BulkChannelResultDto> Each(SocketGuild guild, IEnumerable<ulong> ids,
        Func<SocketGuildChannel, Task<bool>> action)
    {
        var result = new BulkChannelResultDto();
        foreach (var id in ids.Distinct())
        {
            try
            {
                if (guild.GetChannel(id) is { } channel && await action(channel))
                    result.Succeeded++;
                else
                    result.Failed++;
            }
            catch (Exception)
            {
                result.Failed++;
            }
        }

        return result;
    }
}
