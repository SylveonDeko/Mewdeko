using DataModel;
using Mewdeko.Controllers.Common.Guild;
using Mewdeko.Modules.Moderation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for the general bot settings of a guild.
/// </summary>
/// <param name="service">The guild settings service.</param>
/// <param name="muteService">The mute service, which keeps its own copy of the mute role name.</param>
/// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
/// <param name="logger">The logger instance for structured logging.</param>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class BotSettingsController(
    GuildSettingsService service,
    MuteService muteService,
    IDashboardAuditContext auditContext,
    ILogger<BotSettingsController> logger) : Controller
{
    /// <summary>
    ///     Gets the general bot settings of a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>The settings</returns>
    [HttpGet]
    public async Task<IActionResult> GetSettings(ulong guildId)
    {
        try
        {
            var config = await service.GetGuildConfig(guildId);
            return Ok(Describe(config));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error getting bot settings for guild {GuildId}", guildId);
            return StatusCode(500);
        }
    }

    /// <summary>
    ///     Changes the general bot settings of a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="request">The settings to change</param>
    /// <returns>The settings after the change</returns>
    [HttpPost]
    public async Task<IActionResult> UpdateSettings(ulong guildId, [FromBody] BotSettings request)
    {
        try
        {
            if (request.Prefix is not null && string.IsNullOrWhiteSpace(request.Prefix))
                return BadRequest("Prefix can't be empty.");
            if (request.CurrencyEmoji is not null && string.IsNullOrWhiteSpace(request.CurrencyEmoji))
                return BadRequest("Currency emoji can't be empty.");
            if (request.WarnExpireHours is < 0)
                return BadRequest("Warning expiry can't be negative.");
            if (request.WarnExpireAction is < 0 or > 1)
                return BadRequest("Warning expiry action must be 0 or 1.");

            var config = await service.GetGuildConfig(guildId);
            auditContext.RecordBefore(Describe(config));

            if (request.Prefix is not null)
                config.Prefix = request.Prefix.Trim();
            if (request.CommandLogChannel.HasValue)
                config.CommandLogChannel = request.CommandLogChannel.Value;
            if (request.CurrencyEmoji is not null)
                config.CurrencyEmoji = request.CurrencyEmoji.Trim();
            if (request.WarnlogChannelId.HasValue)
                config.WarnlogChannelId = request.WarnlogChannelId.Value;
            if (request.WarnExpireHours.HasValue)
                config.WarnExpireHours = request.WarnExpireHours.Value;
            if (request.WarnExpireAction.HasValue)
                config.WarnExpireAction = request.WarnExpireAction.Value;
            if (request.Locale is not null)
                config.Locale = string.IsNullOrWhiteSpace(request.Locale) ? null : request.Locale.Trim();
            if (request.Snipeset.HasValue)
                config.Snipeset = request.Snipeset.Value;
            if (request.PreviewLinks.HasValue)
                config.PreviewLinks = request.PreviewLinks.Value ? 1 : 0;
            if (request.RemoveRolesOnMute.HasValue)
                config.Removeroles = request.RemoveRolesOnMute.Value ? 1 : 0;
            if (request.VerboseErrors.HasValue)
                config.VerboseErrors = request.VerboseErrors.Value;

            if (request.MuteRoleName is not null)
            {
                var name = request.MuteRoleName.Trim();
                if (name.Length == 0)
                {
                    config.MuteRoleName = null;
                    muteService.GuildMuteRoles.TryRemove(guildId, out _);
                }
                else
                {
                    config.MuteRoleName = name;
                    muteService.GuildMuteRoles.AddOrUpdate(guildId, name, (_, _) => name);
                }
            }

            await service.UpdateGuildConfig(guildId, config);

            var updated = Describe(config);
            auditContext.RecordAfter(updated);
            return Ok(updated);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating bot settings for guild {GuildId}", guildId);
            return StatusCode(500);
        }
    }

    private static BotSettings Describe(GuildConfig config)
    {
        return new BotSettings
        {
            Prefix = config.Prefix,
            CommandLogChannel = config.CommandLogChannel,
            CurrencyEmoji = config.CurrencyEmoji,
            WarnlogChannelId = config.WarnlogChannelId,
            WarnExpireHours = config.WarnExpireHours,
            WarnExpireAction = config.WarnExpireAction,
            Locale = config.Locale,
            Snipeset = config.Snipeset,
            PreviewLinks = config.PreviewLinks == 1,
            MuteRoleName = config.MuteRoleName,
            RemoveRolesOnMute = config.Removeroles == 1,
            VerboseErrors = config.VerboseErrors
        };
    }
}
