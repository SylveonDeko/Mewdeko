using System.Net.Http;
using Mewdeko.Controllers.Common.Guild;
using Mewdeko.Modules.Administration.Services;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for guild/server information endpoints
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
public class GuildController : ControllerBase
{
    private readonly AdministrationService adminService;
    private readonly DiscordShardedClient client;
    private readonly ILogger<GuildController> logger;

    /// <summary>
    ///     Initializes a new instance of the GuildController class
    /// </summary>
    /// <param name="client">The Discord sharded client</param>
    /// <param name="logger">The logger instance</param>
    /// <param name="adminService">The administration service</param>
    public GuildController(DiscordShardedClient client, ILogger<GuildController> logger,
        AdministrationService adminService)
    {
        this.client = client;
        this.logger = logger;
        this.adminService = adminService;
    }

    /// <summary>
    ///     Gets essential guild information for dashboard display
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>Guild information for dashboard theming and display</returns>
    [HttpGet("{guildId}/info")]
    public IActionResult GetGuildInfo(ulong guildId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound($"Guild with ID {guildId} not found");

            var guildInfo = new GuildInfoModel
            {
                Id = guild.Id,
                Name = guild.Name,
                Icon = guild.IconId,
                IconUrl =
                    guild.IconId != null
                        ? $"https://cdn.discordapp.com/icons/{guild.Id}/{guild.IconId}.{(guild.IconId.StartsWith("a_") ? "gif" : "png")}"
                        : null,
                Banner = guild.BannerId,
                BannerUrl =
                    guild.BannerId != null
                        ? $"https://cdn.discordapp.com/banners/{guild.Id}/{guild.BannerId}.{(guild.BannerId.StartsWith("a_") ? "gif" : "png")}"
                        : null,
                Description = guild.Description,
                MemberCount = guild.MemberCount,
                PremiumTier = (int)guild.PremiumTier,
                OwnerId = guild.OwnerId,
                CreatedAt = guild.CreatedAt
            };

            return Ok(guildInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving guild info for {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Gets the bot's guild-specific profile (avatar, banner, bio)
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>Bot's guild profile information</returns>
    [HttpGet("{guildId}/bot-profile")]
    public async Task<IActionResult> GetBotGuildProfile(ulong guildId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound($"Guild with ID {guildId} not found");

            var profileData = await adminService.GetGuildProfile(guildId);

            var response = new BotGuildProfileResponse
            {
                Avatar = profileData.TryGetValue("avatar", out var avatar) ? avatar?.ToString() : null,
                Banner = profileData.TryGetValue("banner", out var banner) ? banner?.ToString() : null,
                Bio = profileData.TryGetValue("bio", out var bio) ? bio?.ToString() : null,
                Nickname = profileData.TryGetValue("nick", out var nick) ? nick?.ToString() : null
            };

            // Avatar and Banner are stored as full URLs in DB, just pass them through
            response.AvatarUrl = response.Avatar;
            response.BannerUrl = response.Banner;

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving bot guild profile for {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Sets the bot's guild-specific profile (avatar, banner, bio)
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="request">Profile update request</param>
    /// <returns>Success status</returns>
    [HttpPost("{guildId}/bot-profile")]
    public async Task<IActionResult> SetBotGuildProfile(ulong guildId, [FromBody] SetGuildProfileRequest request)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound($"Guild with ID {guildId} not found");

            // Validate at least one field is provided
            if (string.IsNullOrWhiteSpace(request.AvatarUrl) &&
                string.IsNullOrWhiteSpace(request.BannerUrl) &&
                string.IsNullOrWhiteSpace(request.Bio))
            {
                return BadRequest("At least one field (AvatarUrl, BannerUrl, or Bio) must be provided");
            }

            await adminService.SetGuildProfile(guildId, request.AvatarUrl, request.BannerUrl, request.Bio);

            return Ok(new
            {
                success = true, message = "Guild profile updated successfully"
            });
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error setting bot guild profile for {GuildId}", guildId);
            return StatusCode(502, "Failed to communicate with Discord API");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting bot guild profile for {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }
}