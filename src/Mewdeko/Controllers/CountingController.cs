using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Controllers.Common.Counting;
using Mewdeko.Modules.Counting.Common;
using Mewdeko.Modules.Counting.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
/// Controller for managing counting channels and functionality.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class CountingController : Controller
{
    private readonly DiscordShardedClient client;
    private readonly CountingService countingService;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<CountingController> logger;
    private readonly CountingModerationService moderationService;
    private readonly CountingStatsService statsService;
    private readonly IDashboardAuditContext auditContext;

    /// <summary>
    /// Initializes a new instance of the CountingController.
    /// </summary>
    /// <param name="countingService">The main counting service</param>
    /// <param name="statsService">The counting statistics service</param>
    /// <param name="moderationService">The counting moderation service</param>
    /// <param name="client">The Discord client</param>
    /// <param name="logger">The logger instance</param>
    /// <param name="dbFactory">The database connection factory</param>
    /// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
    public CountingController(
        CountingService countingService,
        CountingStatsService statsService,
        CountingModerationService moderationService,
        DiscordShardedClient client,
        ILogger<CountingController> logger,
        IDataConnectionFactory dbFactory,
        IDashboardAuditContext auditContext)
    {
        this.countingService = countingService;
        this.statsService = statsService;
        this.moderationService = moderationService;
        this.client = client;
        this.logger = logger;
        this.dbFactory = dbFactory;
        this.auditContext = auditContext;
    }

    #region Utility

    /// <summary>
    ///     Purges all data for a counting channel (WARNING: This is irreversible).
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="request">The purge request details</param>
    /// <returns>Success or failure response</returns>
    [HttpDelete("channels/{channelId}/purge")]
    public async Task<IActionResult> PurgeCountingChannel(ulong guildId, ulong channelId,
        [FromBody] PurgeChannelRequest request)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var user = await guild.GetUserAsync(request.UserId);
            if (user == null)
                return NotFound("User not found");

            var success = await countingService.PurgeCountingChannelAsync(channelId, request.UserId, request.Reason);

            if (success)
                return Ok("Counting channel purged successfully");
            return BadRequest("Failed to purge counting channel");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error purging counting channel {ChannelId} in guild {GuildId}", channelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Channel Management

    /// <summary>
    /// Gets all counting channels in a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <returns>List of counting channels with their basic information</returns>
    [HttpGet("channels")]
    public async Task<IActionResult> GetCountingChannels(ulong guildId)
    {
        try
        {
            var channels = await countingService.GetGuildCountingChannelsAsync(guildId);
            IGuild guild = client.GetGuild(guildId);

            if (guild == null)
                return NotFound("Guild not found");

            var result = await Task.WhenAll(channels.Select(async channel =>
            {
                var discordChannel = await guild.GetTextChannelAsync(channel.ChannelId);
                var lastUser = channel.LastUserId > 0 ? await guild.GetUserAsync(channel.LastUserId) : null;

                return new CountingChannelResponse
                {
                    Id = channel.Id,
                    GuildId = channel.GuildId,
                    ChannelId = channel.ChannelId,
                    ChannelName = discordChannel?.Name ?? "Deleted Channel",
                    CurrentNumber = channel.CurrentNumber,
                    StartNumber = channel.StartNumber,
                    Increment = channel.Increment,
                    LastUserId = channel.LastUserId,
                    LastUsername = lastUser?.Username,
                    IsActive = channel.IsActive,
                    CreatedAt = channel.CreatedAt,
                    HighestNumber = channel.HighestNumber,
                    HighestNumberReachedAt = channel.HighestNumberReachedAt,
                    TotalCounts = channel.TotalCounts
                };
            }));

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving counting channels for guild {GuildId}", guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Sets up counting in a specific channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="request">The setup configuration</param>
    /// <returns>The created counting channel information</returns>
    [HttpPost("channels/{channelId}/setup")]
    public async Task<IActionResult> SetupCountingChannel(ulong guildId, ulong channelId,
        [FromBody] SetupCountingChannelRequest request)
    {
        try
        {
            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var channel = await guild.GetTextChannelAsync(channelId);
            if (channel == null)
                return NotFound("Channel not found");

            if (request.Increment <= 0)
                return BadRequest("Increment must be greater than 0");

            var result =
                await countingService.SetupCountingChannelAsync(guildId, channelId, request.StartNumber,
                    request.Increment);

            if (!result.Success)
                return BadRequest(result.ErrorMessage);

            var lastUser = result.Channel?.LastUserId > 0 ? await guild.GetUserAsync(result.Channel.LastUserId) : null;

            return Ok(new CountingChannelResponse
            {
                Id = result.Channel!.Id,
                GuildId = result.Channel.GuildId,
                ChannelId = result.Channel.ChannelId,
                ChannelName = channel.Name,
                CurrentNumber = result.Channel.CurrentNumber,
                StartNumber = result.Channel.StartNumber,
                Increment = result.Channel.Increment,
                LastUserId = result.Channel.LastUserId,
                LastUsername = lastUser?.Username,
                IsActive = result.Channel.IsActive,
                CreatedAt = result.Channel.CreatedAt,
                HighestNumber = result.Channel.HighestNumber,
                HighestNumberReachedAt = result.Channel.HighestNumberReachedAt,
                TotalCounts = result.Channel.TotalCounts
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting up counting channel {ChannelId} in guild {GuildId}", channelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets the status and statistics of a counting channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <returns>Detailed channel status and statistics</returns>
    [HttpGet("channels/{channelId}/status")]
    public async Task<IActionResult> GetChannelStatus(ulong guildId, ulong channelId)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var channel = await guild.GetTextChannelAsync(channelId);
            var lastUser = countingChannel.LastUserId > 0 ? await guild.GetUserAsync(countingChannel.LastUserId) : null;
            var stats = await countingService.GetChannelStatsAsync(channelId);

            var response = new CountingChannelResponse
            {
                Id = countingChannel.Id,
                GuildId = countingChannel.GuildId,
                ChannelId = countingChannel.ChannelId,
                ChannelName = channel?.Name ?? "Deleted Channel",
                CurrentNumber = countingChannel.CurrentNumber,
                StartNumber = countingChannel.StartNumber,
                Increment = countingChannel.Increment,
                LastUserId = countingChannel.LastUserId,
                LastUsername = lastUser?.Username,
                IsActive = countingChannel.IsActive,
                CreatedAt = countingChannel.CreatedAt,
                HighestNumber = countingChannel.HighestNumber,
                HighestNumberReachedAt = countingChannel.HighestNumberReachedAt,
                TotalCounts = countingChannel.TotalCounts
            };

            return Ok(new
            {
                Channel = response,
                Statistics = new
                {
                    stats?.TotalParticipants,
                    stats?.TotalErrors,
                    stats?.MilestonesReached,
                    stats?.AverageAccuracy,
                    stats?.LastActivity
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting status for counting channel {ChannelId} in guild {GuildId}", channelId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Disables counting in a specific channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="userId">The ID of the user disabling the channel</param>
    /// <param name="reason">Optional reason for disabling</param>
    /// <returns>Success or failure response</returns>
    [HttpDelete("channels/{channelId}")]
    public async Task<IActionResult> DisableCountingChannel(ulong guildId, ulong channelId, [FromQuery] ulong userId,
        [FromQuery] string? reason = null)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var user = await guild.GetUserAsync(userId);
            if (user == null)
                return NotFound("User not found");

            auditContext.RecordBefore(countingChannel);
            var success = await countingService.DisableCountingChannelAsync(channelId, userId, reason);

            if (success)
                return Ok("Counting channel disabled successfully");
            else
                return BadRequest("Failed to disable counting channel");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disabling counting channel {ChannelId} in guild {GuildId}", channelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Gets the configuration for a counting channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <returns>The channel configuration</returns>
    [HttpGet("channels/{channelId}/config")]
    public async Task<IActionResult> GetChannelConfig(ulong guildId, ulong channelId)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            var config = await countingService.GetCountingConfigAsync(channelId);
            if (config == null)
                return NotFound("Counting configuration not found");

            var response = new CountingConfigResponse
            {
                Id = config.Id,
                ChannelId = config.ChannelId,
                AllowRepeatedUsers = config.AllowRepeatedUsers,
                Cooldown = config.Cooldown,
                RequiredRoles = config.RequiredRoles,
                BannedRoles = config.BannedRoles,
                MaxNumber = config.MaxNumber,
                ResetOnError = config.ResetOnError,
                DeleteWrongMessages = config.DeleteWrongMessages,
                Pattern = (CountingPattern)config.Pattern,
                NumberBase = config.NumberBase,
                SuccessEmote = config.SuccessEmote,
                ErrorEmote = config.ErrorEmote,
                EnableAchievements = config.EnableAchievements,
                EnableCompetitions = config.EnableCompetitions
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting config for counting channel {ChannelId} in guild {GuildId}", channelId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Updates the configuration for a counting channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="request">The configuration updates</param>
    /// <returns>Success or failure response</returns>
    [HttpPut("channels/{channelId}/config")]
    public async Task<IActionResult> UpdateChannelConfig(ulong guildId, ulong channelId,
        [FromBody] UpdateCountingConfigRequest request)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            var config = await countingService.GetCountingConfigAsync(channelId);
            if (config == null)
                return NotFound("Counting configuration not found");

            auditContext.RecordBefore(config);

            if (request.AllowRepeatedUsers.HasValue) config.AllowRepeatedUsers = request.AllowRepeatedUsers.Value;
            if (request.Cooldown.HasValue) config.Cooldown = request.Cooldown.Value;
            if (request.RequiredRoles != null) config.RequiredRoles = request.RequiredRoles;
            if (request.BannedRoles != null) config.BannedRoles = request.BannedRoles;
            if (request.MaxNumber.HasValue) config.MaxNumber = request.MaxNumber.Value;
            if (request.ResetOnError.HasValue) config.ResetOnError = request.ResetOnError.Value;
            if (request.DeleteWrongMessages.HasValue) config.DeleteWrongMessages = request.DeleteWrongMessages.Value;
            if (request.Pattern.HasValue) config.Pattern = (int)request.Pattern.Value;
            if (request.NumberBase.HasValue) config.NumberBase = request.NumberBase.Value;
            if (request.SuccessEmote != null) config.SuccessEmote = request.SuccessEmote;
            if (request.ErrorEmote != null) config.ErrorEmote = request.ErrorEmote;
            if (request.EnableAchievements.HasValue) config.EnableAchievements = request.EnableAchievements.Value;
            if (request.EnableCompetitions.HasValue) config.EnableCompetitions = request.EnableCompetitions.Value;

            var success = await countingService.UpdateCountingConfigAsync(channelId, config);

            if (success)
            {
                auditContext.RecordAfter(config);
                return Ok("Configuration updated successfully");
            }

            else
                return BadRequest("Failed to update configuration");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating config for counting channel {ChannelId} in guild {GuildId}", channelId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Gets comprehensive statistics for a counting channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <returns>Channel statistics including top contributors and performance metrics</returns>
    [HttpGet("channels/{channelId}/stats")]
    public async Task<IActionResult> GetChannelStats(ulong guildId, ulong channelId)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var stats = await countingService.GetChannelStatsAsync(channelId);
            var channel = await guild.GetTextChannelAsync(channelId);
            var lastUser = countingChannel.LastUserId > 0 ? await guild.GetUserAsync(countingChannel.LastUserId) : null;

            var channelResponse = new CountingChannelResponse
            {
                Id = countingChannel.Id,
                GuildId = countingChannel.GuildId,
                ChannelId = countingChannel.ChannelId,
                ChannelName = channel?.Name ?? "Deleted Channel",
                CurrentNumber = countingChannel.CurrentNumber,
                StartNumber = countingChannel.StartNumber,
                Increment = countingChannel.Increment,
                LastUserId = countingChannel.LastUserId,
                LastUsername = lastUser?.Username,
                IsActive = countingChannel.IsActive,
                CreatedAt = countingChannel.CreatedAt,
                HighestNumber = countingChannel.HighestNumber,
                HighestNumberReachedAt = countingChannel.HighestNumberReachedAt,
                TotalCounts = countingChannel.TotalCounts
            };

            CountingUserStatsResponse? topContributor = null;
            if (stats?.TopContributor != null)
            {
                var topUser = await guild.GetUserAsync(stats.TopContributor.UserId);
                var rank = await statsService.GetUserRankAsync(channelId, stats.TopContributor.UserId);

                topContributor = new CountingUserStatsResponse
                {
                    UserId = stats.TopContributor.UserId,
                    Username = topUser?.Username,
                    AvatarUrl = topUser?.GetAvatarUrl() ?? topUser?.GetDefaultAvatarUrl(),
                    ContributionsCount = stats.TopContributor.ContributionsCount,
                    HighestStreak = stats.TopContributor.HighestStreak,
                    CurrentStreak = stats.TopContributor.CurrentStreak,
                    LastContribution = stats.TopContributor.LastContribution,
                    TotalNumbersCounted = stats.TopContributor.TotalNumbersCounted,
                    ErrorsCount = stats.TopContributor.ErrorsCount,
                    Accuracy = stats.TopContributor.Accuracy,
                    Rank = rank
                };
            }

            var response = new CountingStatsResponse
            {
                Channel = channelResponse,
                TotalParticipants = stats?.TotalParticipants ?? 0,
                TotalErrors = stats?.TotalErrors ?? 0,
                MilestonesReached = stats?.MilestonesReached ?? 0,
                TopContributor = topContributor,
                LastActivity = stats?.LastActivity,
                AverageAccuracy = stats?.AverageAccuracy ?? 0
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting stats for counting channel {ChannelId} in guild {GuildId}", channelId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets the leaderboard for a counting channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="type">The type of leaderboard to retrieve</param>
    /// <param name="limit">Maximum number of entries to return</param>
    /// <returns>Ranked list of users based on the specified criteria</returns>
    [HttpGet("channels/{channelId}/leaderboard")]
    public async Task<IActionResult> GetChannelLeaderboard(
        ulong guildId,
        ulong channelId,
        [FromQuery] string type = "contributions",
        [FromQuery] int limit = 20)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            if (!Enum.TryParse<LeaderboardType>(type, true, out var leaderboardType))
                return BadRequest(
                    $"Invalid leaderboard type. Valid types: {string.Join(", ", Enum.GetNames<LeaderboardType>())}");

            if (limit is < 1 or > 100)
                limit = 20;

            var leaderboard = await statsService.GetLeaderboardAsync(channelId, leaderboardType, limit);
            IGuild guild = client.GetGuild(guildId);

            if (guild == null)
                return NotFound("Guild not found");

            var result = await Task.WhenAll(leaderboard.Select(async entry =>
            {
                var user = await guild.GetUserAsync(entry.UserId);
                return new CountingUserStatsResponse
                {
                    UserId = entry.UserId,
                    Username = user?.Username ?? "Unknown User",
                    AvatarUrl = user?.GetAvatarUrl() ?? user?.GetDefaultAvatarUrl(),
                    ContributionsCount = entry.ContributionsCount,
                    HighestStreak = entry.HighestStreak,
                    CurrentStreak = entry.CurrentStreak,
                    LastContribution = entry.LastContribution,
                    TotalNumbersCounted = entry.TotalNumbersCounted,
                    ErrorsCount = 0, // Not available in leaderboard entry
                    Accuracy = entry.Accuracy,
                    Rank = entry.Rank
                };
            }));

            return Ok(new
            {
                Type = leaderboardType.ToString(), Entries = result
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting leaderboard for counting channel {ChannelId} in guild {GuildId}",
                channelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets counting statistics for a specific user in a channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="userId">The Discord user ID</param>
    /// <returns>User-specific counting statistics</returns>
    [HttpGet("channels/{channelId}/users/{userId}/stats")]
    public async Task<IActionResult> GetUserStats(ulong guildId, ulong channelId, ulong userId)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var user = await guild.GetUserAsync(userId);
            if (user == null)
                return NotFound("User not found");

            var userStats = await statsService.GetUserStatsAsync(channelId, userId);
            if (userStats == null)
                return NotFound("User has no counting statistics in this channel");

            var rank = await statsService.GetUserRankAsync(channelId, userId);

            var response = new CountingUserStatsResponse
            {
                UserId = userStats.UserId,
                Username = user.Username,
                AvatarUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl(),
                ContributionsCount = userStats.ContributionsCount,
                HighestStreak = userStats.HighestStreak,
                CurrentStreak = userStats.CurrentStreak,
                LastContribution = userStats.LastContribution,
                TotalNumbersCounted = userStats.TotalNumbersCounted,
                ErrorsCount = userStats.ErrorsCount,
                Accuracy = userStats.Accuracy,
                Rank = rank
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error getting user stats for user {UserId} in counting channel {ChannelId} in guild {GuildId}", userId,
                channelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Management

    /// <summary>
    /// Resets the counting in a channel to a specific number.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="request">The reset request containing new number, user, and reason</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("channels/{channelId}/reset")]
    public async Task<IActionResult> ResetCountingChannel(ulong guildId, ulong channelId,
        [FromBody] ResetCountingChannelRequest request)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var user = await guild.GetUserAsync(request.UserId);
            if (user == null)
                return NotFound("User not found");

            var success =
                await countingService.ResetCountingChannelAsync(channelId, request.NewNumber, request.UserId,
                    request.Reason);

            if (success)
                return Ok($"Counting reset to {request.NewNumber} successfully");
            else
                return BadRequest("Failed to reset counting channel");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resetting counting channel {ChannelId} in guild {GuildId}", channelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Creates a save point for a counting channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="request">The save point creation request</param>
    /// <returns>The created save point information</returns>
    [HttpPost("channels/{channelId}/saves")]
    public async Task<IActionResult> CreateSavePoint(ulong guildId, ulong channelId,
        [FromBody] CreateSavePointRequest request)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var user = await guild.GetUserAsync(request.UserId);
            if (user == null)
                return NotFound("User not found");

            var saveId = await countingService.CreateSavePointAsync(channelId, request.UserId, request.Reason);

            if (saveId > 0)
            {
                return Ok(new SavePointResponse
                {
                    Id = saveId,
                    SavedNumber = countingChannel.CurrentNumber,
                    SavedAt = DateTime.UtcNow,
                    SavedBy = request.UserId,
                    SavedByUsername = user.Username,
                    Reason = request.Reason ?? "Manual save",
                    IsActive = true
                });
            }
            else
            {
                return BadRequest("Failed to create save point");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating save point for counting channel {ChannelId} in guild {GuildId}",
                channelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Restores a counting channel from a save point.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="request">The restore request containing save ID and user</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("channels/{channelId}/restore")]
    public async Task<IActionResult> RestoreFromSave(ulong guildId, ulong channelId,
        [FromBody] RestoreSavePointRequest request)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var user = await guild.GetUserAsync(request.UserId);
            if (user == null)
                return NotFound("User not found");

            var success = await countingService.RestoreFromSaveAsync(channelId, request.SaveId, request.UserId);

            if (success)
                return Ok($"Successfully restored from save point {request.SaveId}");
            else
                return BadRequest("Failed to restore from save point");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error restoring counting channel {ChannelId} from save {SaveId} in guild {GuildId}",
                channelId, request.SaveId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Moderation

    /// <summary>
    ///     Bans a user from counting in a specific channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="userId">The Discord user ID to ban</param>
    /// <param name="request">The ban request details</param>
    /// <returns>Success or failure response</returns>
    [HttpPost("channels/{channelId}/users/{userId}/ban")]
    public async Task<IActionResult> BanUserFromCounting(ulong guildId, ulong channelId, ulong userId,
        [FromBody] BanUserRequest request)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var user = await guild.GetUserAsync(userId);
            if (user == null)
                return NotFound("User not found");

            var bannedBy = await guild.GetUserAsync(request.BannedBy);
            if (bannedBy == null)
                return NotFound("Moderator not found");

            TimeSpan? duration = request.DurationMinutes.HasValue
                ? TimeSpan.FromMinutes(request.DurationMinutes.Value)
                : null;

            var success =
                await moderationService.BanUserFromCountingAsync(channelId, userId, request.BannedBy, duration,
                    request.Reason);

            if (success)
                return Ok($"User {user.Username} banned from counting successfully");
            return BadRequest("Failed to ban user from counting");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error banning user {UserId} from counting in channel {ChannelId} in guild {GuildId}",
                userId, channelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Unbans a user from counting in a specific channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="userId">The Discord user ID to unban</param>
    /// <param name="request">The unban request details</param>
    /// <returns>Success or failure response</returns>
    [HttpDelete("channels/{channelId}/users/{userId}/ban")]
    public async Task<IActionResult> UnbanUserFromCounting(ulong guildId, ulong channelId, ulong userId,
        [FromBody] UnbanUserRequest request)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var user = await guild.GetUserAsync(userId);
            if (user == null)
                return NotFound("User not found");

            var unbannedBy = await guild.GetUserAsync(request.UnbannedBy);
            if (unbannedBy == null)
                return NotFound("Moderator not found");

            var success =
                await moderationService.UnbanUserFromCountingAsync(channelId, userId, request.UnbannedBy,
                    request.Reason);

            if (success)
                return Ok($"User {user.Username} unbanned from counting successfully");
            return BadRequest("Failed to unban user from counting");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unbanning user {UserId} from counting in channel {ChannelId} in guild {GuildId}",
                userId, channelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Gets violation statistics for a counting channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="hours">Hours to look back for violations (default: 24)</param>
    /// <returns>Violation statistics</returns>
    [HttpGet("channels/{channelId}/violations")]
    public async Task<IActionResult> GetViolationStats(ulong guildId, ulong channelId, [FromQuery] int hours = 24)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            var period = TimeSpan.FromHours(hours);
            var stats = await moderationService.GetViolationStatsAsync(channelId, period);

            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting violation stats for channel {ChannelId} in guild {GuildId}", channelId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Gets the current wrong count for a user in a channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="userId">The Discord user ID</param>
    /// <returns>Current wrong count for the user</returns>
    [HttpGet("channels/{channelId}/users/{userId}/wrongcount")]
    public async Task<IActionResult> GetUserWrongCount(ulong guildId, ulong channelId, ulong userId)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            var wrongCount = await moderationService.GetUserWrongCountAsync(channelId, userId);

            return Ok(new
            {
                UserId = userId, WrongCount = wrongCount
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting wrong count for user {UserId} in channel {ChannelId} in guild {GuildId}",
                userId, channelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Clears the wrong count for a user in a channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="userId">The Discord user ID</param>
    /// <param name="moderatorId">The ID of the moderator clearing the count</param>
    /// <returns>Success or failure response</returns>
    [HttpDelete("channels/{channelId}/users/{userId}/wrongcount")]
    public async Task<IActionResult> ClearUserWrongCount(ulong guildId, ulong channelId, ulong userId,
        [FromQuery] ulong moderatorId)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var user = await guild.GetUserAsync(userId);
            if (user == null)
                return NotFound("User not found");

            var moderator = await guild.GetUserAsync(moderatorId);
            if (moderator == null)
                return NotFound("Moderator not found");

            var success = await moderationService.ClearUserWrongCountsAsync(channelId, userId);

            if (success)
                return Ok($"Wrong count cleared for user {user.Username}");
            return BadRequest("Failed to clear wrong count");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error clearing wrong count for user {UserId} in channel {ChannelId} in guild {GuildId}", userId,
                channelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Save Points

    /// <summary>
    ///     Gets all save points for a counting channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <returns>List of save points</returns>
    [HttpGet("channels/{channelId}/saves")]
    public async Task<IActionResult> GetSavePoints(ulong guildId, ulong channelId)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var saves = await GetChannelSavePointsAsync(channelId);
            var result = await Task.WhenAll(saves.Select(async save =>
            {
                var user = await guild.GetUserAsync(save.SavedBy);
                return new SavePointResponse
                {
                    Id = save.Id,
                    SavedNumber = save.SavedNumber,
                    SavedAt = save.SavedAt,
                    SavedBy = save.SavedBy,
                    SavedByUsername = user?.Username,
                    Reason = save.Reason,
                    IsActive = save.IsActive
                };
            }));

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting save points for channel {ChannelId} in guild {GuildId}", channelId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Deletes a save point.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="saveId">The save point ID to delete</param>
    /// <param name="userId">The ID of the user deleting the save point</param>
    /// <returns>Success or failure response</returns>
    [HttpDelete("channels/{channelId}/saves/{saveId}")]
    public async Task<IActionResult> DeleteSavePoint(ulong guildId, ulong channelId, int saveId,
        [FromQuery] ulong userId)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            IGuild guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var user = await guild.GetUserAsync(userId);
            if (user == null)
                return NotFound("User not found");

            var success = await DeleteSavePointAsync(channelId, saveId);

            if (success)
                return Ok($"Save point {saveId} deleted successfully");
            return BadRequest("Failed to delete save point");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting save point {SaveId} for channel {ChannelId} in guild {GuildId}", saveId,
                channelId, guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Customization

    /// <summary>
    ///     Sets a custom success message for a counting channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="request">The custom message request</param>
    /// <returns>Success or failure response</returns>
    [HttpPut("channels/{channelId}/messages/success")]
    public async Task<IActionResult> SetSuccessMessage(ulong guildId, ulong channelId,
        [FromBody] SetCustomMessageRequest request)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            var success = await countingService.SetSuccessMessageAsync(channelId, request.Message);

            if (success)
                return Ok("Success message updated successfully");
            return BadRequest("Failed to update success message");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting success message for channel {ChannelId} in guild {GuildId}", channelId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Sets a custom failure message for a counting channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="request">The custom message request</param>
    /// <returns>Success or failure response</returns>
    [HttpPut("channels/{channelId}/messages/failure")]
    public async Task<IActionResult> SetFailureMessage(ulong guildId, ulong channelId,
        [FromBody] SetCustomMessageRequest request)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            var success = await countingService.SetFailureMessageAsync(channelId, request.Message);

            if (success)
                return Ok("Failure message updated successfully");
            else
                return BadRequest("Failed to update failure message");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting failure message for channel {ChannelId} in guild {GuildId}", channelId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Sets a custom milestone message for a counting channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="request">The custom message request</param>
    /// <returns>Success or failure response</returns>
    [HttpPut("channels/{channelId}/messages/milestone")]
    public async Task<IActionResult> SetMilestoneMessage(ulong guildId, ulong channelId,
        [FromBody] SetCustomMessageRequest request)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            var success = await countingService.SetMilestoneMessageAsync(channelId, request.Message);

            if (success)
                return Ok("Milestone message updated successfully");
            return BadRequest("Failed to update milestone message");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting milestone message for channel {ChannelId} in guild {GuildId}", channelId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Sets custom milestones for a counting channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <param name="request">The custom milestones request</param>
    /// <returns>Success or failure response</returns>
    [HttpPut("channels/{channelId}/milestones")]
    public async Task<IActionResult> SetCustomMilestones(ulong guildId, ulong channelId,
        [FromBody] SetMilestonesRequest request)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            var success = await countingService.SetMilestonesAsync(channelId, request.Milestones);

            if (success)
                return Ok("Custom milestones updated successfully");
            return BadRequest("Failed to update custom milestones");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting custom milestones for channel {ChannelId} in guild {GuildId}", channelId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Gets custom milestones for a counting channel.
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="channelId">The Discord channel ID</param>
    /// <returns>List of custom milestones</returns>
    [HttpGet("channels/{channelId}/milestones")]
    public async Task<IActionResult> GetCustomMilestones(ulong guildId, ulong channelId)
    {
        try
        {
            var countingChannel = await countingService.GetCountingChannelAsync(channelId);
            if (countingChannel == null || countingChannel.GuildId != guildId)
                return NotFound("Counting channel not found");

            var milestones = await countingService.GetMilestonesAsync(channelId);

            return Ok(new
            {
                Milestones = milestones
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting custom milestones for channel {ChannelId} in guild {GuildId}", channelId,
                guildId);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Gets save points for a channel from the database.
    /// </summary>
    private async Task<List<CountingSaves>> GetChannelSavePointsAsync(ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.CountingSaves
            .Where(x => x.ChannelId == channelId && x.IsActive)
            .OrderByDescending(x => x.SavedAt)
            .ToListAsync();
    }

    /// <summary>
    ///     Deletes a save point from the database.
    /// </summary>
    private async Task<bool> DeleteSavePointAsync(ulong channelId, int saveId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var rowsAffected = await db.CountingSaves
                .Where(x => x.ChannelId == channelId && x.Id == saveId)
                .UpdateAsync(x => new CountingSaves
                {
                    IsActive = false
                });

            return rowsAffected > 0;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}