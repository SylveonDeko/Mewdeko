using LinqToDB.Async;
using Mewdeko.Controllers.Common.Birthday;
using Mewdeko.Modules.Birthday.Common;
using Mewdeko.Modules.Birthday.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API Controller for managing birthday announcements and configurations via the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class BirthdayController : Controller
{
    private readonly BirthdayService birthdayService;
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<BirthdayController> logger;
    private readonly IDashboardAuditContext auditContext;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BirthdayController" /> class.
    /// </summary>
    /// <param name="birthdayService">The birthday service instance.</param>
    /// <param name="client">The Discord sharded client instance.</param>
    /// <param name="dbFactory">The factory for creating database connections.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
    public BirthdayController(
        BirthdayService birthdayService,
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        ILogger<BirthdayController> logger,
        IDashboardAuditContext auditContext)
    {
        this.birthdayService = birthdayService;
        this.client = client;
        this.dbFactory = dbFactory;
        this.logger = logger;
        this.auditContext = auditContext;
    }

    /// <summary>
    ///     Gets the birthday configuration for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get configuration for.</param>
    /// <returns>The birthday configuration or NotFound if guild doesn't exist.</returns>
    [HttpGet("config")]
    public async Task<IActionResult> GetBirthdayConfig(ulong guildId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild is null)
                return NotFound("Guild not found.");

            var config = await birthdayService.GetBirthdayConfigAsync(guildId);

            var response = new BirthdayConfigResponse
            {
                BirthdayChannelId = config.BirthdayChannelId,
                BirthdayRoleId = config.BirthdayRoleId,
                BirthdayMessage = config.BirthdayMessage ?? "🎉 Happy Birthday {user}! 🎂",
                BirthdayPingRoleId = config.BirthdayPingRoleId,
                BirthdayReminderDays = config.BirthdayReminderDays,
                DefaultTimezone = config.DefaultTimezone ?? "UTC",
                EnabledFeatures = config.EnabledFeatures,
                DateAdded = config.DateAdded,
                DateModified = config.DateModified
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get birthday configuration for guild {GuildId}", guildId);
            return StatusCode(500, "Failed to retrieve birthday configuration.");
        }
    }

    /// <summary>
    ///     Updates the birthday configuration for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to update configuration for.</param>
    /// <param name="request">The configuration update request.</param>
    /// <returns>Success status or error details.</returns>
    [HttpPut("config")]
    public async Task<IActionResult> UpdateBirthdayConfig(ulong guildId, [FromBody] BirthdayConfigRequest request)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild is null)
                return NotFound("Guild not found.");

            // Validate channel if provided
            if (request.BirthdayChannelId.HasValue)
            {
                var channel = guild.GetTextChannel(request.BirthdayChannelId.Value);
                if (channel is null)
                    return BadRequest("Birthday channel not found in guild.");
            }

            // Validate birthday role if provided
            if (request.BirthdayRoleId.HasValue)
            {
                var role = guild.GetRole(request.BirthdayRoleId.Value);
                if (role is null)
                    return BadRequest("Birthday role not found in guild.");
            }

            // Validate ping role if provided
            if (request.BirthdayPingRoleId.HasValue)
            {
                var pingRole = guild.GetRole(request.BirthdayPingRoleId.Value);
                if (pingRole is null)
                    return BadRequest("Birthday ping role not found in guild.");
            }

            // Validate timezone if provided
            if (!string.IsNullOrEmpty(request.DefaultTimezone))
            {
                if (!IsValidTimezone(request.DefaultTimezone))
                    return BadRequest("Invalid timezone provided.");
            }

            auditContext.RecordBefore(await birthdayService.GetBirthdayConfigAsync(guildId));

            await birthdayService.UpdateBirthdayConfigAsync(guildId, config =>
            {
                if (request.BirthdayChannelId.HasValue)
                    config.BirthdayChannelId = request.BirthdayChannelId.Value;
                if (request.BirthdayRoleId.HasValue)
                    config.BirthdayRoleId = request.BirthdayRoleId.Value;
                if (!string.IsNullOrEmpty(request.BirthdayMessage))
                    config.BirthdayMessage = request.BirthdayMessage;
                if (request.BirthdayPingRoleId.HasValue)
                    config.BirthdayPingRoleId = request.BirthdayPingRoleId.Value;
                if (request.BirthdayReminderDays.HasValue)
                    config.BirthdayReminderDays = request.BirthdayReminderDays.Value;
                if (!string.IsNullOrEmpty(request.DefaultTimezone))
                    config.DefaultTimezone = request.DefaultTimezone;
            });

            auditContext.RecordAfter(await birthdayService.GetBirthdayConfigAsync(guildId));
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update birthday configuration for guild {GuildId}", guildId);
            return StatusCode(500, "Failed to update birthday configuration.");
        }
    }

    /// <summary>
    ///     Resets the birthday configuration to defaults for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to reset configuration for.</param>
    /// <returns>Success status or error details.</returns>
    [HttpPost("config/reset")]
    public async Task<IActionResult> ResetBirthdayConfig(ulong guildId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild is null)
                return NotFound("Guild not found.");

            auditContext.RecordBefore(await birthdayService.GetBirthdayConfigAsync(guildId));

            await birthdayService.UpdateBirthdayConfigAsync(guildId, config =>
            {
                config.BirthdayChannelId = null;
                config.BirthdayRoleId = null;
                config.BirthdayMessage = null;
                config.BirthdayPingRoleId = null;
                config.BirthdayReminderDays = 0;
                config.DefaultTimezone = "UTC";
                config.EnabledFeatures = 0;
            });

            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reset birthday configuration for guild {GuildId}", guildId);
            return StatusCode(500, "Failed to reset birthday configuration.");
        }
    }

    /// <summary>
    ///     Gets upcoming birthdays for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get birthdays for.</param>
    /// <param name="days">Number of days to look ahead (default: 7, max: 30).</param>
    /// <returns>List of upcoming birthdays or error details.</returns>
    [HttpGet("upcoming")]
    public async Task<IActionResult> GetUpcomingBirthdays(ulong guildId, [FromQuery] int days = 7)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild is null)
                return NotFound("Guild not found.");

            if (days is < 1 or > 30)
                return BadRequest("Days must be between 1 and 30.");

            var upcomingBirthdays = new List<BirthdayUserResponse>();
            var today = DateTime.UtcNow.Date;

            for (var i = 0; i < days; i++)
            {
                var checkDate = today.AddDays(i);
                var birthdayUsers = await birthdayService.GetBirthdayUsersForDateAsync(guildId, checkDate);

                foreach (var birthdayUser in birthdayUsers)
                {
                    var guildUser = guild.GetUser(birthdayUser.UserId);
                    if (guildUser != null)
                    {
                        upcomingBirthdays.Add(new BirthdayUserResponse
                        {
                            UserId = birthdayUser.UserId,
                            Username = guildUser.Username,
                            Nickname = guildUser.Nickname,
                            AvatarUrl = guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl(),
                            Birthday = birthdayUser.Birthday,
                            BirthdayDisplayMode = birthdayUser.BirthdayDisplayMode,
                            BirthdayAnnouncementsEnabled = birthdayUser.BirthdayAnnouncementsEnabled,
                            BirthdayTimezone = birthdayUser.BirthdayTimezone,
                            DaysUntil = i
                        });
                    }
                }
            }

            return Ok(upcomingBirthdays.OrderBy(x => x.DaysUntil).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get upcoming birthdays for guild {GuildId}", guildId);
            return StatusCode(500, "Failed to retrieve upcoming birthdays.");
        }
    }

    /// <summary>
    ///     Gets today's birthdays for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get birthdays for.</param>
    /// <returns>List of today's birthdays or error details.</returns>
    [HttpGet("today")]
    public async Task<IActionResult> GetTodaysBirthdays(ulong guildId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild is null)
                return NotFound("Guild not found.");

            var birthdayUsers = await birthdayService.GetBirthdayUsersForDateAsync(guildId, DateTime.UtcNow.Date);
            var todaysBirthdays = new List<BirthdayUserResponse>();

            foreach (var birthdayUser in birthdayUsers)
            {
                var guildUser = guild.GetUser(birthdayUser.UserId);
                if (guildUser != null)
                {
                    todaysBirthdays.Add(new BirthdayUserResponse
                    {
                        UserId = birthdayUser.UserId,
                        Username = guildUser.Username,
                        Nickname = guildUser.Nickname,
                        AvatarUrl = guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl(),
                        Birthday = birthdayUser.Birthday,
                        BirthdayDisplayMode = birthdayUser.BirthdayDisplayMode,
                        BirthdayAnnouncementsEnabled = birthdayUser.BirthdayAnnouncementsEnabled,
                        BirthdayTimezone = birthdayUser.BirthdayTimezone,
                        DaysUntil = 0
                    });
                }
            }

            return Ok(todaysBirthdays);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get today's birthdays for guild {GuildId}", guildId);
            return StatusCode(500, "Failed to retrieve today's birthdays.");
        }
    }

    /// <summary>
    ///     Gets all users with birthdays set in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to get users for.</param>
    /// <returns>List of users with birthdays or error details.</returns>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsersWithBirthdays(ulong guildId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild is null)
                return NotFound("Guild not found.");

            await using var db = await dbFactory.CreateConnectionAsync();
            var usersWithBirthdays = await db.DiscordUsers
                .Where(x => x.Birthday.HasValue)
                .ToListAsync();

            var birthdayUsers = new List<BirthdayUserResponse>();

            foreach (var user in usersWithBirthdays)
            {
                var guildUser = guild.GetUser(user.UserId);
                if (guildUser != null)
                {
                    birthdayUsers.Add(new BirthdayUserResponse
                    {
                        UserId = user.UserId,
                        Username = guildUser.Username,
                        Nickname = guildUser.Nickname,
                        AvatarUrl = guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl(),
                        Birthday = user.Birthday,
                        BirthdayDisplayMode = user.BirthdayDisplayMode,
                        BirthdayAnnouncementsEnabled = user.BirthdayAnnouncementsEnabled,
                        BirthdayTimezone = user.BirthdayTimezone,
                        DaysUntil = null
                    });
                }
            }

            return Ok(birthdayUsers.OrderBy(x => x.Username).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get users with birthdays for guild {GuildId}", guildId);
            return StatusCode(500, "Failed to retrieve users with birthdays.");
        }
    }

    /// <summary>
    ///     Gets a specific user's birthday information.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID to get birthday info for.</param>
    /// <returns>User's birthday information or NotFound if user doesn't exist.</returns>
    [HttpGet("users/{userId}")]
    public async Task<IActionResult> GetUserBirthday(ulong guildId, ulong userId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild is null)
                return NotFound("Guild not found.");

            var guildUser = guild.GetUser(userId);
            if (guildUser is null)
                return NotFound("User not found in guild.");

            await using var db = await dbFactory.CreateConnectionAsync();
            var user = await db.GetOrCreateUser(guildUser);

            if (!user.Birthday.HasValue)
                return NotFound("User has no birthday set.");

            var response = new BirthdayUserResponse
            {
                UserId = user.UserId,
                Username = guildUser.Username,
                Nickname = guildUser.Nickname,
                AvatarUrl = guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl(),
                Birthday = user.Birthday,
                BirthdayDisplayMode = user.BirthdayDisplayMode,
                BirthdayAnnouncementsEnabled = user.BirthdayAnnouncementsEnabled,
                BirthdayTimezone = user.BirthdayTimezone,
                DaysUntil = null
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get birthday info for user {UserId} in guild {GuildId}", userId, guildId);
            return StatusCode(500, "Failed to retrieve user birthday information.");
        }
    }

    /// <summary>
    ///     Enables a specific birthday feature for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="feature">The feature to enable.</param>
    /// <returns>Success status or error details.</returns>
    [HttpPost("features/{feature}/enable")]
    public async Task<IActionResult> EnableBirthdayFeature(ulong guildId, BirthdayFeature feature)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild is null)
                return NotFound("Guild not found.");

            auditContext.RecordBefore(await birthdayService.GetBirthdayConfigAsync(guildId));
            await birthdayService.EnableBirthdayFeatureAsync(guildId, feature);
            auditContext.RecordAfter(await birthdayService.GetBirthdayConfigAsync(guildId));
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enable birthday feature {Feature} for guild {GuildId}", feature, guildId);
            return StatusCode(500, "Failed to enable birthday feature.");
        }
    }

    /// <summary>
    ///     Disables a specific birthday feature for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="feature">The feature to disable.</param>
    /// <returns>Success status or error details.</returns>
    [HttpPost("features/{feature}/disable")]
    public async Task<IActionResult> DisableBirthdayFeature(ulong guildId, BirthdayFeature feature)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild is null)
                return NotFound("Guild not found.");

            auditContext.RecordBefore(await birthdayService.GetBirthdayConfigAsync(guildId));
            await birthdayService.DisableBirthdayFeatureAsync(guildId, feature);
            auditContext.RecordAfter(await birthdayService.GetBirthdayConfigAsync(guildId));
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to disable birthday feature {Feature} for guild {GuildId}", feature, guildId);
            return StatusCode(500, "Failed to disable birthday feature.");
        }
    }

    /// <summary>
    ///     Gets the status of all birthday features for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>Feature status information or error details.</returns>
    [HttpGet("features")]
    public async Task<IActionResult> GetFeatureStatus(ulong guildId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild is null)
                return NotFound("Guild not found.");

            var config = await birthdayService.GetBirthdayConfigAsync(guildId);
            var features = Enum.GetValues<BirthdayFeature>()
                .Where(f => f != BirthdayFeature.None)
                .ToDictionary(
                    f => f.ToString(),
                    f => (config.EnabledFeatures & (int)f) != 0
                );

            return Ok(new FeatureStatusResponse
            {
                Features = features
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get feature status for guild {GuildId}", guildId);
            return StatusCode(500, "Failed to retrieve feature status.");
        }
    }

    /// <summary>
    ///     Gets birthday statistics for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>Birthday statistics or error details.</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetBirthdayStats(ulong guildId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild is null)
                return NotFound("Guild not found.");

            var guildUserIds = guild.Users.Select(u => u.Id).ToList();

            await using var db = await dbFactory.CreateConnectionAsync();

            var totalUsers = guild.MemberCount;

            var usersWithBirthdays = await db.DiscordUsers
                .Where(x => x.Birthday.HasValue && guildUserIds.Contains(x.UserId))
                .CountAsync();

            var usersWithAnnouncementsEnabled = await db.DiscordUsers
                .Where(x => x.Birthday.HasValue && x.BirthdayAnnouncementsEnabled && guildUserIds.Contains(x.UserId))
                .CountAsync();

            var todaysBirthdays = await birthdayService.GetBirthdayUsersForDateAsync(guildId, DateTime.UtcNow.Date);

            var stats = new BirthdayStatsResponse
            {
                TotalUsers = totalUsers,
                UsersWithBirthdays = usersWithBirthdays,
                UsersWithAnnouncementsEnabled = usersWithAnnouncementsEnabled,
                TodaysBirthdayCount = todaysBirthdays.Count,
                BirthdaySetPercentage = totalUsers > 0 ? (double)usersWithBirthdays / totalUsers * 100 : 0
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get birthday stats for guild {GuildId}", guildId);
            return StatusCode(500, "Failed to retrieve birthday statistics.");
        }
    }

    /// <summary>
    ///     Validates if a timezone string is valid.
    /// </summary>
    /// <param name="timezone">The timezone to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    private static bool IsValidTimezone(string timezone)
    {
        var validTimezones = new[]
        {
            "UTC", "GMT", "EST", "CST", "MST", "PST", "EDT", "CDT", "MDT", "PDT", "Europe/London", "Europe/Paris",
            "Europe/Berlin", "Europe/Rome", "Europe/Madrid", "Europe/Amsterdam", "Europe/Brussels", "Europe/Vienna",
            "Europe/Stockholm", "Europe/Oslo", "Europe/Copenhagen", "Europe/Helsinki", "Europe/Warsaw", "Europe/Prague",
            "Europe/Budapest", "Europe/Bucharest", "Europe/Sofia", "Europe/Athens", "Europe/Kiev", "Europe/Moscow",
            "Europe/Istanbul", "America/New_York", "America/Chicago", "America/Denver", "America/Los_Angeles",
            "America/Phoenix", "America/Anchorage", "America/Honolulu", "America/Toronto", "America/Vancouver",
            "America/Montreal", "America/Halifax", "America/Winnipeg", "America/Edmonton", "America/Regina",
            "America/St_Johns", "America/Mexico_City", "America/Bogota", "America/Lima", "America/Buenos_Aires",
            "America/Sao_Paulo", "America/Santiago", "America/Caracas", "America/La_Paz", "America/Montevideo",
            "Asia/Tokyo", "Asia/Seoul", "Asia/Shanghai", "Asia/Hong_Kong", "Asia/Singapore", "Asia/Manila",
            "Asia/Bangkok", "Asia/Ho_Chi_Minh", "Asia/Jakarta", "Asia/Kuala_Lumpur", "Asia/Mumbai", "Asia/Kolkata",
            "Asia/Dhaka", "Asia/Karachi", "Asia/Dubai", "Asia/Tehran", "Asia/Baghdad", "Asia/Riyadh", "Asia/Jerusalem",
            "Asia/Beirut", "Australia/Sydney", "Australia/Melbourne", "Australia/Brisbane", "Australia/Perth",
            "Australia/Adelaide", "Australia/Darwin", "Australia/Hobart", "Pacific/Auckland", "Pacific/Fiji",
            "Pacific/Honolulu", "Pacific/Tahiti", "Pacific/Marquesas", "Africa/Cairo", "Africa/Lagos", "Africa/Nairobi",
            "Africa/Johannesburg", "Africa/Casablanca", "Africa/Tunis", "Africa/Algiers", "Africa/Tripoli"
        };

        return validTimezones.Contains(timezone);
    }
}