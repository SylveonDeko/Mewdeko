using Mewdeko.Controllers.Common.ServerBackups;
using Mewdeko.Controllers.Common.ServerTemplates;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Lists, previews, saves, applies and deletes a user's server templates for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{userId}")]
[Authorize("ApiKeyPolicy")]
public class ServerTemplatesController(ServerTemplateService service, ServerBackupService backups,
    DiscordShardedClient client) : Controller
{
    /// <summary>
    ///     Gets the starter templates and a user's saved templates.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    [HttpGet]
    public async Task<IActionResult> List(ulong userId)
    {
        return Ok(new ServerTemplateListResponse
        {
            BuiltIn = BuiltInServerTemplates.Names.ToList(),
            Templates = (await service.ListAsync(userId)).Select(x => new ServerTemplateDto
            {
                Name = x.Name,
                SizeBytes = x.SizeBytes,
                RoleCount = x.RoleCount,
                ChannelCount = x.ChannelCount,
                CreatedAt = DateTime.SpecifyKind(x.DateAdded, DateTimeKind.Utc)
            }).ToList()
        });
    }

    /// <summary>
    ///     Gets what a starter template or one of a user's saved templates holds.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="name">The template name.</param>
    [HttpGet("{name}")]
    public async Task<IActionResult> Preview(ulong userId, string name)
    {
        var data = await service.GetDataAsync(userId, ServerTemplateService.NormalizeName(name) ?? "");
        return data is null ? NotFound() : Ok(BackupPreviewDto.From(data));
    }

    /// <summary>
    ///     Saves the roles and channels of a server the user owns or administrates as a template.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The server to save.</param>
    /// <param name="request">The template name.</param>
    [HttpPost("save/{guildId}")]
    public async Task<IActionResult> Save(ulong userId, ulong guildId, [FromBody] SaveServerTemplateRequest request)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");
        if (await ServerBackupsController.Member(guild, userId) is not { } member ||
            !ServerBackupService.IsOwnerOrAdmin(guild, member))
            return BadRequest(backups.ErrorText(guildId, BackupError.NotAdmin));

        var name = ServerTemplateService.NormalizeName(request.Name);
        if (name is null)
            return BadRequest($"Names are 1 to {ServerTemplateService.MaxNameLength} characters");
        if (ServerTemplateService.IsBuiltIn(name))
            return BadRequest("That name belongs to a starter template");

        var template = await service.SaveAsync(guild, userId, name);
        return Ok(new ServerTemplateDto
        {
            Name = template.Name,
            SizeBytes = template.SizeBytes,
            RoleCount = template.RoleCount,
            ChannelCount = template.ChannelCount,
            CreatedAt = DateTime.SpecifyKind(template.DateAdded, DateTimeKind.Utc)
        });
    }

    /// <summary>
    ///     Starts adding the roles and channels a template has to a server the user owns or administrates.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The server to apply it to.</param>
    /// <param name="name">The template name.</param>
    [HttpPost("apply/{guildId}/{name}")]
    public async Task<IActionResult> Apply(ulong userId, ulong guildId, string name)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");
        if (await ServerBackupsController.Member(guild, userId) is not { } member)
            return BadRequest(backups.ErrorText(guildId, BackupError.NotAdmin));

        var data = await service.GetDataAsync(userId, ServerTemplateService.NormalizeName(name) ?? "");
        if (data is null)
            return NotFound();

        var (error, detail) = await service.CheckApplyAsync(guild, member);
        if (error != BackupError.None)
            return BadRequest(service.ErrorText(guildId, error, detail));

        backups.StartRestore(guild, member, data, BackupRestoreMode.AddMissing);
        return Accepted();
    }

    /// <summary>
    ///     Deletes one of a user's saved templates.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="name">The template name.</param>
    [HttpDelete("{name}")]
    public async Task<IActionResult> Delete(ulong userId, string name)
    {
        return await service.DeleteAsync(userId, name) ? Ok() : NotFound();
    }
}
