using Mewdeko.AuthHandlers;
using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Controllers.Common.Embeds;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Utility.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Embed = DataModel.Embed;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing saved embed templates (personal and guild-shared). Since a single route can
///     resolve to either a personal or a guild-shared embed (and the guild ID for mutations often lives in
///     the request body rather than the route), this controller is exempt from the generic
///     <see cref="DashboardAccessEnforcementFilter" /> and enforces guild-shared access itself.
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
[SkipDashboardAccess]
public class EmbedsController(
    EmbedService embedService,
    IDashboardAuditContext auditContext,
    DiscordShardedClient client,
    DashboardAccessService dashboardAccessService) : Controller
{
    private const string Section = "Embeds";

    /// <summary>
    ///     Gets all personal embed templates saved by a user.
    /// </summary>
    /// <param name="userId">The Discord user ID.</param>
    /// <returns>A list of the user's personal embed templates.</returns>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserEmbeds(ulong userId)
    {
        var embeds = await embedService.GetUserEmbedsAsync(userId);
        return Ok(embeds.Select(ToResponse));
    }

    /// <summary>
    ///     Gets all guild-shared embed templates for a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <returns>A list of the guild's shared embed templates.</returns>
    [HttpGet("guild/{guildId}")]
    public async Task<IActionResult> GetGuildEmbeds(ulong guildId)
    {
        if (!await HasGuildSectionAccessAsync(guildId, DashboardAccessLevel.View))
            return Forbid();

        var embeds = await embedService.GetGuildEmbedsAsync(guildId);
        return Ok(embeds.Select(ToResponse));
    }

    /// <summary>
    ///     Gets a single embed template by its ID.
    /// </summary>
    /// <param name="id">The embed template's database ID.</param>
    /// <returns>The embed template, if found.</returns>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetEmbed(int id)
    {
        var embed = await embedService.GetEmbedByIdAsync(id);
        if (embed == null)
            return NotFound("Embed template not found");

        if (embed.GuildId.HasValue && !await HasGuildSectionAccessAsync(embed.GuildId.Value, DashboardAccessLevel.View))
            return Forbid();

        return Ok(ToResponse(embed));
    }

    /// <summary>
    ///     Creates a new personal or guild-shared embed template.
    /// </summary>
    /// <param name="request">The embed template to create.</param>
    /// <returns>The created embed template.</returns>
    [HttpPost]
    public async Task<IActionResult> CreateEmbed([FromBody] CreateEmbedRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EmbedName))
            return BadRequest("Embed name is required");

        if (string.IsNullOrWhiteSpace(request.JsonCode))
            return BadRequest("Embed JSON is required");

        if (!SmartEmbed.TryParse(request.JsonCode, request.GuildId, out _, out _, out _))
            return BadRequest("Invalid embed JSON");

        if (request.IsGuildShared)
        {
            if (!request.GuildId.HasValue)
                return BadRequest("A guild ID is required for guild-shared embeds");

            if (!await HasGuildSectionAccessAsync(request.GuildId.Value, DashboardAccessLevel.Manage))
                return Forbid();

            if (await embedService.GuildEmbedExistsAsync(request.GuildId.Value, request.EmbedName))
                return BadRequest($"A guild embed named '{request.EmbedName}' already exists");

            var created = await embedService.CreateGuildEmbedAsync(request.GuildId.Value, request.UserId,
                request.EmbedName, request.JsonCode);

            auditContext.RecordAfter(created);
            return Ok(ToResponse(created));
        }
        else
        {
            if (await embedService.UserEmbedExistsAsync(request.UserId, request.EmbedName))
                return BadRequest($"A personal embed named '{request.EmbedName}' already exists");

            var created = await embedService.CreateUserEmbedAsync(request.UserId, request.EmbedName,
                request.JsonCode);

            auditContext.RecordAfter(created);
            return Ok(ToResponse(created));
        }
    }

    /// <summary>
    ///     Updates an existing embed template's name and/or JSON.
    /// </summary>
    /// <param name="id">The embed template's database ID.</param>
    /// <param name="request">The fields to update.</param>
    /// <returns>The updated embed template.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateEmbed(int id, [FromBody] UpdateEmbedRequest request)
    {
        var existing = await embedService.GetEmbedByIdAsync(id);
        if (existing == null)
            return NotFound("Embed template not found");

        if (!await CanModifyAsync(existing, request.UserId))
            return Forbid();

        if (request.JsonCode != null &&
            !SmartEmbed.TryParse(request.JsonCode, existing.GuildId, out _, out _, out _))
            return BadRequest("Invalid embed JSON");

        auditContext.RecordBefore(existing);
        var updated = await embedService.UpdateEmbedAsync(id, request.EmbedName, request.JsonCode);
        if (updated == null)
            return NotFound("Embed template not found");

        auditContext.RecordAfter(updated);
        return Ok(ToResponse(updated));
    }

    /// <summary>
    ///     Deletes an embed template.
    /// </summary>
    /// <param name="id">The embed template's database ID.</param>
    /// <param name="userId">The ID of the user requesting the deletion, used for ownership verification.</param>
    /// <returns>Success or failure response.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteEmbed(int id, [FromQuery] ulong userId)
    {
        var existing = await embedService.GetEmbedByIdAsync(id);
        if (existing == null)
            return NotFound("Embed template not found");

        if (!await CanModifyAsync(existing, userId))
            return Forbid();

        auditContext.RecordBefore(existing);
        var success = await embedService.DeleteEmbedByIdAsync(id);

        if (success)
            return Ok("Embed template deleted successfully");
        return BadRequest("Failed to delete embed template");
    }

    /// <summary>
    ///     Personal embeds may only be modified by their owner. Guild-shared embeds require Manage-level
    ///     dashboard access to the Embeds section for that guild.
    /// </summary>
    private async Task<bool> CanModifyAsync(Embed embed, ulong requestingUserId)
    {
        if (embed.GuildId.HasValue)
            return await HasGuildSectionAccessAsync(embed.GuildId.Value, DashboardAccessLevel.Manage);

        return embed.UserId == requestingUserId;
    }

    /// <summary>
    ///     Extracts the verified dashboard user ID from the request's dashboard JWT, if present. Returns
    ///     null for requests authenticated only by the shared API key (mobile/legacy callers), which retain
    ///     their existing unrestricted behavior.
    /// </summary>
    private async Task<ulong?> GetDashboardUserIdAsync()
    {
        if (!Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var authResult = await HttpContext.AuthenticateAsync(DashJwtConstants.SchemeName);
        if (!authResult.Succeeded ||
            !ulong.TryParse(authResult.Principal?.FindFirst(DashJwtConstants.UserIdClaim)?.Value, out var userId))
            return null;

        return userId;
    }

    /// <summary>
    ///     Whether the current dashboard user has at least the given access level to the Embeds section for a
    ///     guild. Guild owners and Administrator-permission holders always pass. Requests without a verified
    ///     dashboard identity are allowed through unchanged.
    /// </summary>
    private async Task<bool> HasGuildSectionAccessAsync(ulong guildId, DashboardAccessLevel required)
    {
        var userId = await GetDashboardUserIdAsync();
        if (userId == null)
            return true;

        var guild = client.GetGuild(guildId);
        var guildUser = guild?.GetUser(userId.Value);
        if (guild == null || guildUser == null)
            return false;

        if (guild.OwnerId == userId.Value || guildUser.GuildPermissions.Has(GuildPermission.Administrator))
            return true;

        var level = await dashboardAccessService.GetSectionAccessAsync(
            guildId, userId.Value, guildUser.Roles.Select(role => role.Id).ToList(), Section);
        return level >= required;
    }

    private static EmbedResponse ToResponse(Embed embed)
    {
        return new EmbedResponse
        {
            Id = embed.Id,
            EmbedName = embed.EmbedName,
            JsonCode = embed.JsonCode,
            UserId = embed.UserId,
            DateAdded = embed.DateAdded,
            GuildId = embed.GuildId,
            IsGuildShared = embed.IsGuildShared
        };
    }
}