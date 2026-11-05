using Mewdeko.Controllers.Common.ServerTemplates;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Lists and deletes a user's server templates for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{userId}")]
[Authorize("ApiKeyPolicy")]
public class ServerTemplatesController(ServerTemplateService service) : Controller
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
