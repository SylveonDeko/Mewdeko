using Mewdeko.Controllers.Common.ServerBackups;
using Mewdeko.Modules.Administration.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Lists and deletes a user's server backups for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{userId}")]
[Authorize("ApiKeyPolicy")]
public class ServerBackupsController(ServerBackupService service) : Controller
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
            Backups = backups.Select(x => new ServerBackupDto
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
            }).ToList()
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
}
