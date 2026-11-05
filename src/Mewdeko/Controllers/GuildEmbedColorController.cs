using Mewdeko.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Reads and changes the colour of a guild's ok and info embeds for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class GuildEmbedColorController(GuildEmbedColorService service, IDashboardAuditContext auditContext)
    : Controller
{
    /// <summary>
    ///     Gets the guild's embed colour.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet]
    public IActionResult Get(ulong guildId)
    {
        return Ok(ToResponse(guildId));
    }

    /// <summary>
    ///     Sets the guild's embed colour, or clears it when the colour is empty.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The colour as a hex code or colour name.</param>
    [HttpPost]
    public async Task<IActionResult> Set(ulong guildId, [FromBody] GuildEmbedColorRequest request)
    {
        auditContext.RecordBefore(ToResponse(guildId));

        if (string.IsNullOrWhiteSpace(request.Color))
        {
            await service.SetAsync(guildId, null);
        }
        else
        {
            if (!ColorUtils.TryParseColor(request.Color, out var parsed))
                return BadRequest("Color must be a hex code such as #ff66aa or a colour name");

            await service.SetAsync(guildId, parsed);
        }

        var after = ToResponse(guildId);
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    private object ToResponse(ulong guildId)
    {
        var own = service.Get(guildId);
        return new
        {
            color = own is { } c ? $"#{c.RawValue:X6}" : null, isDefault = own is null
        };
    }
}

/// <summary>
///     Request to set a guild's embed colour.
/// </summary>
public class GuildEmbedColorRequest
{
    /// <summary>
    ///     The colour as a hex code or colour name. Empty goes back to the default.
    /// </summary>
    public string? Color { get; set; }
}
