using Mewdeko.Controllers.Common.ServerBackups;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Creates, lists, previews, restores and deletes a user's server backups for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{userId}")]
[Authorize("ApiKeyPolicy")]
public class ServerBackupsController(ServerBackupService service, DiscordShardedClient client) : Controller
{
    /// <summary>
    ///     Gets a user's backups.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    [HttpGet]
    public async Task<IActionResult> List(ulong userId)
    {
        var backups = await service.ListAsync(userId);
        return Ok(new ServerBackupListResponse
        {
            MaxBackups = ServerBackupService.MaxBackupsPerUser,
            CooldownMinutes = (int)ServerBackupService.CreateCooldown.TotalMinutes,
            Backups = backups.Select(ToDto).ToList()
        });
    }

    /// <summary>
    ///     Gets what one of a user's backups holds.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="id">The backup ID.</param>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Preview(ulong userId, int id)
    {
        var backup = await service.GetAsync(userId, id);
        if (backup is null)
            return NotFound();

        var data = ServerBackupService.ReadData(backup);
        return data is null ? BadRequest("The backup could not be read") : Ok(BackupPreviewDto.From(data));
    }

    /// <summary>
    ///     Backs up a server the user owns or administrates.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The server to back up.</param>
    /// <param name="request">How many messages to save and from which channels.</param>
    [HttpPost("create/{guildId}")]
    public async Task<IActionResult> Create(ulong userId, ulong guildId, [FromBody] CreateServerBackupRequest request)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");
        if (await Member(guild, userId) is not { } member || !ServerBackupService.IsOwnerOrAdmin(guild, member))
            return BadRequest(service.ErrorText(guildId, BackupError.NotAdmin));

        var messages = Math.Clamp(request.Messages, 0, ServerBackupService.MaxMessagesPerChannel);
        var channelIds = request.ChannelIds.Distinct().ToList();
        var (error, backup, wait) = await service.CreateAsync(guild, userId, messages, channelIds);
        if (error != BackupError.None || backup is null)
            return BadRequest(service.ErrorText(guildId, error, wait: wait));

        return Ok(ToDto(backup));
    }

    /// <summary>
    ///     Starts restoring one of a user's backups into a server they own or administrate.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="id">The backup ID.</param>
    /// <param name="guildId">The server to restore into.</param>
    /// <param name="request">Whether to replace everything or only add what is missing.</param>
    [HttpPost("{id:int}/restore/{guildId}")]
    public async Task<IActionResult> Restore(ulong userId, int id, ulong guildId,
        [FromBody] RestoreServerBackupRequest request)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");
        if (await Member(guild, userId) is not { } member)
            return BadRequest(service.ErrorText(guildId, BackupError.NotAdmin));
        if (!Enum.TryParse<BackupRestoreMode>(request.Mode, true, out var mode))
            return BadRequest("Unknown mode");

        var backup = await service.GetAsync(userId, id);
        if (backup is null)
            return NotFound();

        var data = ServerBackupService.ReadData(backup);
        if (data is null)
            return BadRequest(service.ErrorText(guildId, BackupError.Corrupt));

        var (error, detail) = await service.CheckRestoreAsync(guild, member, backup, data.Messages.Count > 0);
        if (error != BackupError.None)
            return BadRequest(service.ErrorText(guildId, error, detail));

        service.StartRestore(guild, member, data, mode);
        return Accepted();
    }

    /// <summary>
    ///     Gets how far the latest restore into a server has got.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The server.</param>
    [HttpGet("restore/{guildId}")]
    public async Task<IActionResult> RestoreStatus(ulong userId, ulong guildId)
    {
        if (client.GetGuild(guildId) is not { } guild || await Member(guild, userId) is not { } member ||
            !ServerBackupService.IsOwnerOrAdmin(guild, member))
            return NotFound();

        if (service.GetJob(guildId) is not { } job)
            return Ok(new RestoreStatusDto());

        var progress = job.Progress;
        return Ok(new RestoreStatusDto
        {
            Started = true,
            Finished = job.Finished,
            Step = progress.Step,
            RolesCreated = progress.RolesCreated,
            ChannelsCreated = progress.ChannelsCreated,
            EmojisCreated = progress.EmojisCreated,
            MessagesRestored = progress.MessagesRestored,
            Failures = progress.Failures.ToList()
        });
    }

    /// <summary>
    ///     Deletes one of a user's backups.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="id">The backup ID.</param>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(ulong userId, int id)
    {
        return await service.DeleteAsync(userId, id) ? Ok() : NotFound();
    }

    internal static async Task<IGuildUser?> Member(SocketGuild guild, ulong userId)
    {
        return guild.GetUser(userId) ?? await ((IGuild)guild).GetUserAsync(userId);
    }

    private static ServerBackupDto ToDto(DataModel.ServerBackup x)
    {
        return new ServerBackupDto
        {
            Id = x.Id,
            SourceGuildId = x.SourceGuildId,
            SourceGuildName = x.SourceGuildName,
            SizeBytes = x.SizeBytes,
            RoleCount = x.RoleCount,
            ChannelCount = x.ChannelCount,
            EmojiCount = x.EmojiCount,
            MessageCount = x.MessageCount,
            CreatedAt = DateTime.SpecifyKind(x.DateAdded, DateTimeKind.Utc)
        };
    }
}
