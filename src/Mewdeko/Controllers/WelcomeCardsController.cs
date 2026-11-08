using DataModel;
using Mewdeko.Common.Cards;
using Mewdeko.Common.Palette;
using Mewdeko.Controllers.Common.AuditLog;
using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Controllers.Common.WelcomeCards;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Modules.MultiGreets.Common;
using Mewdeko.Modules.MultiGreets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Reads and changes a guild's welcome cards for the dashboard. Every card is a design on the shared card engine,
///     and the dashboard edits it with the same designer achievement cards use.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}/{target}")]
[Authorize("ApiKeyPolicy")]
public class WelcomeCardsController(
    WelcomeCardService service,
    DiscordShardedClient client,
    GuildPaletteService paletteService,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Gets the card: whether it is on, its design, and what the designer needs.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Greet, Bye, DmGreet or Boost.</param>
    [HttpGet]
    public async Task<IActionResult> Get(ulong guildId, WelcomeCardTarget target, [FromQuery] int ownerId = 0)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        return Ok(await ResponseAsync(guild, target, ownerId));
    }

    /// <summary>
    ///     Saves the card's state, its design, or both. The design is brought within limits first.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Greet, Bye, DmGreet or Boost.</param>
    /// <param name="request">The state and design.</param>
    [HttpPost]
    public async Task<IActionResult> Save(ulong guildId, WelcomeCardTarget target,
        [FromBody] WelcomeCardSaveRequest request, [FromQuery] int ownerId = 0)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(new
        {
            (await service.GetCardAsync(guildId, target, ownerId)).Placement
        });
        await service.SaveAsync(guildId, target, ownerId, request.Placement, request.Template);
        var response = await ResponseAsync(guild, target, ownerId);
        auditContext.RecordAfter(new
        {
            response.Placement, Elements = response.Template.Elements.Count
        });
        return Ok(response);
    }

    /// <summary>
    ///     Lists the starter layouts.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Greet, Bye, DmGreet or Boost.</param>
    [HttpGet("starters")]
    [SkipAudit]
    public IActionResult Starters(ulong guildId, WelcomeCardTarget target)
    {
        return Ok(WelcomeCardTemplates.Starters.Select(s => new WelcomeCardStarterResponse
        {
            Id = s.Id, Name = s.Name, Template = s.Build()
        }).ToList());
    }

    /// <summary>
    ///     Replaces the card's design with a starter layout.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Greet, Bye, DmGreet or Boost.</param>
    /// <param name="starterId">The layout's short name.</param>
    [HttpPost("starters/{starterId}")]
    public async Task<IActionResult> UseStarter(ulong guildId, WelcomeCardTarget target, string starterId,
        [FromQuery] int ownerId = 0)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(null);
        var error = await service.ResetToStarterAsync(guildId, target, ownerId, starterId);
        if (error != WelcomeCardError.None)
            return BadRequest(error.ToString());

        auditContext.RecordAfter(new
        {
            starterId
        });
        return Ok(await ResponseAsync(guild, target, ownerId));
    }

    /// <summary>
    ///     Draws a design before it is saved, as the dashboard user, and reports where each element landed so editors
    ///     can draw handles over the image. The saved design is drawn when none is sent.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Greet, Bye, DmGreet or Boost.</param>
    /// <param name="request">The design and who to draw it for.</param>
    [HttpPost("preview")]
    [SkipAudit]
    public async Task<IActionResult> Preview(ulong guildId, WelcomeCardTarget target,
        [FromBody] WelcomeCardPreviewRequest request, [FromQuery] int ownerId = 0)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        var template = request.Template is null
            ? await service.GetTemplateAsync(guildId, target, ownerId)
            : await service.OwnedAsync(guildId, request.Template);
        var memberId = ulong.TryParse(request.UserId, out var requested)
            ? requested
            : await HttpContext.GetDashboardUserIdAsync() ?? 0;
        IUser member = guild.GetUser(memberId) ?? (IUser)guild.CurrentUser;

        CardRenderResult result;
        try
        {
            result = await service.RenderCardAsync(guildId, template, member, request.EditingId);
        }
        catch (Exception)
        {
            return StatusCode(500, "Couldn't draw that card.");
        }

        await using var image = result.Image;
        return Ok(new WelcomeCardPreviewResponse
        {
            Image = "data:image/png;base64," + Convert.ToBase64String(image.ToArray()),
            Width = result.CardWidth,
            Height = result.CardHeight,
            Margin = result.Margin,
            Layout = result.Layout.Select(b => new WelcomeCardBoxResponse
            {
                Id = b.Id, X = b.X, Y = b.Y, W = b.W, H = b.H, Drawn = b.Drawn
            }).ToList(),
            Template = template
        });
    }

    /// <summary>
    ///     Draws the saved card for a member of the guild as a PNG.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Greet, Bye, DmGreet or Boost.</param>
    /// <param name="userId">The member to draw the card for.</param>
    [HttpGet("preview")]
    public async Task<IActionResult> PreviewFile(ulong guildId, WelcomeCardTarget target, [FromQuery] ulong userId,
        [FromQuery] int ownerId = 0)
    {
        var user = client.GetGuild(guildId)?.GetUser(userId);
        if (user is null)
            return NotFound();

        return File(await service.RenderAsync(guildId, target, ownerId, user), "image/png");
    }

    /// <summary>
    ///     Draws the saved card and returns it as a data URI, for callers that cannot read binary responses.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Greet, Bye, DmGreet or Boost.</param>
    /// <param name="userId">The member to draw the card for. The bot is used when omitted or not found.</param>
    [HttpGet("preview/image")]
    [SkipAudit]
    public async Task<IActionResult> PreviewImage(ulong guildId, WelcomeCardTarget target,
        [FromQuery] ulong? userId = null, [FromQuery] int ownerId = 0)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound();

        IUser? user = userId is { } id ? guild.GetUser(id) : null;
        user ??= guild.CurrentUser;

        var bytes = await service.RenderAsync(guildId, target, ownerId, user);
        return Ok(new
        {
            image = $"data:image/png;base64,{Convert.ToBase64String(bytes)}"
        });
    }

    /// <summary>
    ///     Stores an image sent as base64 or a data URI for the server's designs, shrunk to 1600 pixels and saved as
    ///     PNG. Designs link it as <c>upload:id</c>.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Greet, Bye, DmGreet or Boost.</param>
    /// <param name="request">The image data.</param>
    [HttpPost("images")]
    public async Task<IActionResult> UploadImage(ulong guildId, WelcomeCardTarget target,
        [FromBody] WelcomeCardImageRequest request)
    {
        if (client.GetGuild(guildId) is null)
            return NotFound("Guild not found.");

        var payload = request.Data.Trim();
        var comma = payload.IndexOf(',');
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
            payload = payload[(comma + 1)..];

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return BadRequest(WelcomeCardError.InvalidImage.ToString());
        }

        auditContext.RecordBefore(null);
        var (error, image) = await service.UploadImageAsync(guildId, bytes);
        if (error != WelcomeCardError.None || image is null)
            return BadRequest(error.ToString());

        var response = ImageResponse(image);
        auditContext.RecordAfter(new
        {
            response.Id
        });
        return Ok(response);
    }

    /// <summary>
    ///     Deletes a stored image and removes it from every design that uses it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Greet, Bye, DmGreet or Boost.</param>
    /// <param name="id">The image ID.</param>
    [HttpDelete("images/{id:int}")]
    public async Task<IActionResult> DeleteImage(ulong guildId, WelcomeCardTarget target, int id,
        [FromQuery] int ownerId = 0)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(new
        {
            id
        });
        if (!await service.DeleteImageAsync(guildId, id))
            return NotFound();

        return Ok(await ResponseAsync(guild, target, ownerId));
    }

    private async Task<WelcomeCardResponse> ResponseAsync(SocketGuild guild, WelcomeCardTarget target, int ownerId)
    {
        var setting = await service.GetCardAsync(guild.Id, target, ownerId);
        var template = await service.GetTemplateAsync(guild.Id, target, ownerId);
        var palette = await paletteService.GetAsync(guild);
        var images = await service.GetImagesAsync(guild.Id);
        return new WelcomeCardResponse
        {
            Placement = setting.Placement,
            Template = template,
            BuiltIn = WelcomeCardTemplates.Classic(),
            Images = images.Select(ImageResponse).ToList(),
            Placeholders = WelcomeCardTemplates.Placeholders.ToList(),
            Palette = new Dictionary<string, string>
            {
                ["primary"] = palette.Primary,
                ["secondary"] = palette.Secondary,
                ["accent"] = palette.Accent,
                ["text"] = palette.Text,
                ["muted"] = palette.Muted,
                ["background"] = palette.Background
            }
        };
    }

    private static WelcomeCardImageResponse ImageResponse(WelcomeCardImage image)
    {
        return new WelcomeCardImageResponse
        {
            Id = image.Id,
            Icon = AchievementIcons.UploadPrefix + image.Id,
            Url = image.Thumbnail.Length == 0
                ? ""
                : "data:image/png;base64," + Convert.ToBase64String(image.Thumbnail)
        };
    }
}
