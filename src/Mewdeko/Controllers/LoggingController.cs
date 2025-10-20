using System.Text.Json;
using DataModel;
using LinqToDB.Async;
using Mewdeko.Controllers.Common.Logging;
using Mewdeko.Modules.Administration.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing logging configuration with comprehensive channel mappings and event support
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class LoggingController(LogCommandService logService, IDataConnectionFactory dbFactory) : Controller
{
    /// <summary>
    ///     Gets the complete logging configuration for a guild
    /// </summary>
    [HttpGet("configuration")]
    public async Task<IActionResult> GetLoggingConfiguration(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var logSettings = await db.LoggingV2.FirstOrDefaultAsync(ls => ls.GuildId == guildId);

        // Get ignored channels from cache or database
        var ignoredChannels = await logService.GetIgnoredChannels(guildId);

        var config = new LoggingConfigurationResponse
        {
            Enabled = logSettings != null,
            IgnoredChannels = ignoredChannels.ToList(),
            LogTypes = new Dictionary<string, ulong?>()
        };

        if (logSettings != null)
        {
            // Map all log types to their channel IDs
            config.LogTypes["Other"] = logSettings.LogOtherId;
            config.LogTypes["MessageUpdated"] = logSettings.MessageUpdatedId;
            config.LogTypes["MessageDeleted"] = logSettings.MessageDeletedId;
            config.LogTypes["ThreadCreated"] = logSettings.ThreadCreatedId;
            config.LogTypes["ThreadDeleted"] = logSettings.ThreadDeletedId;
            config.LogTypes["ThreadUpdated"] = logSettings.ThreadUpdatedId;
            config.LogTypes["UsernameUpdated"] = logSettings.UsernameUpdatedId;
            config.LogTypes["NicknameUpdated"] = logSettings.NicknameUpdatedId;
            config.LogTypes["AvatarUpdated"] = logSettings.AvatarUpdatedId;
            config.LogTypes["UserLeft"] = logSettings.UserLeftId;
            config.LogTypes["UserBanned"] = logSettings.UserBannedId;
            config.LogTypes["UserUnbanned"] = logSettings.UserUnbannedId;
            config.LogTypes["UserUpdated"] = logSettings.UserUpdatedId;
            config.LogTypes["UserJoined"] = logSettings.UserJoinedId;
            config.LogTypes["UserRoleAdded"] = logSettings.UserRoleAddedId;
            config.LogTypes["UserRoleRemoved"] = logSettings.UserRoleRemovedId;
            config.LogTypes["UserMuted"] = logSettings.UserMutedId;
            config.LogTypes["VoicePresence"] = logSettings.LogVoicePresenceId;
            config.LogTypes["VoicePresenceTts"] = logSettings.LogVoicePresenceTtsId;
            config.LogTypes["ServerUpdated"] = logSettings.ServerUpdatedId;
            config.LogTypes["RoleUpdated"] = logSettings.RoleUpdatedId;
            config.LogTypes["RoleDeleted"] = logSettings.RoleDeletedId;
            config.LogTypes["EventCreated"] = logSettings.EventCreatedId;
            config.LogTypes["RoleCreated"] = logSettings.RoleCreatedId;
            config.LogTypes["ChannelCreated"] = logSettings.ChannelCreatedId;
            config.LogTypes["ChannelDestroyed"] = logSettings.ChannelDestroyedId;
            config.LogTypes["ChannelUpdated"] = logSettings.ChannelUpdatedId;
            config.LogTypes["ReactionEvents"] = logSettings.ReactionEventsId;
        }

        return Ok(config);
    }

    /// <summary>
    ///     Sets the channel for a specific log type
    /// </summary>
    [HttpPut("log-type/{logType}")]
    public async Task<IActionResult> SetLogChannel(ulong guildId, string logType,
        [FromBody] SetLogChannelRequest request)
    {
        if (!Enum.TryParse<LogCommandService.LogType>(logType, out var type))
            return BadRequest($"Invalid log type: {logType}");

        await logService.SetLogChannel(guildId, request.ChannelId ?? 0, type);

        return Ok(new
        {
            success = true, logType, channelId = request.ChannelId
        });
    }

    /// <summary>
    ///     Sets multiple log channels at once by category
    /// </summary>
    [HttpPut("log-category/{category}")]
    public async Task<IActionResult> SetLogCategory(ulong guildId, string category,
        [FromBody] SetLogChannelRequest request)
    {
        if (!Enum.TryParse<LogCommandService.LogCategoryTypes>(category, out var categoryType))
            return BadRequest($"Invalid log category: {category}");

        await logService.LogSetByType(guildId, request.ChannelId ?? 0, categoryType);

        return Ok(new
        {
            success = true, category, channelId = request.ChannelId
        });
    }

    /// <summary>
    ///     Toggles ignored status for a channel
    /// </summary>
    [HttpPost("ignored-channels/{channelId}")]
    public async Task<IActionResult> ToggleIgnoredChannel(ulong guildId, ulong channelId)
    {
        var result = await logService.LogIgnore(guildId, channelId);
        var ignoredChannels = await logService.GetIgnoredChannels(guildId);

        var action = result switch
        {
            LogCommandService.IgnoreResult.Added => "added",
            LogCommandService.IgnoreResult.Removed => "removed",
            _ => "error"
        };

        if (result == LogCommandService.IgnoreResult.Error)
            return BadRequest(new
            {
                success = false, error = "Failed to toggle channel ignore status"
            });

        return Ok(new
        {
            success = true, channelId, action, currentIgnoredChannels = ignoredChannels.ToList()
        });
    }

    /// <summary>
    ///     Gets all ignored channels
    /// </summary>
    [HttpGet("ignored-channels")]
    public async Task<IActionResult> GetIgnoredChannels(ulong guildId)
    {
        var ignoredChannels = await logService.GetIgnoredChannels(guildId);
        return Ok(new
        {
            ignoredChannels = ignoredChannels.ToList()
        });
    }

    /// <summary>
    ///     Sets the complete list of ignored channels
    /// </summary>
    [HttpPut("ignored-channels")]
    public async Task<IActionResult> SetIgnoredChannels(ulong guildId, [FromBody] SetIgnoredChannelsRequest request)
    {
        var ignoredChannels = new HashSet<ulong>(request.ChannelIds ?? new List<ulong>());
        await logService.UpdateIgnoredChannelsAsync(guildId, ignoredChannels);

        return Ok(new
        {
            success = true, ignoredChannels = ignoredChannels.ToList()
        });
    }

    /// <summary>
    ///     Clears all ignored channels
    /// </summary>
    [HttpDelete("ignored-channels")]
    public async Task<IActionResult> ClearIgnoredChannels(ulong guildId)
    {
        await logService.UpdateIgnoredChannelsAsync(guildId, new HashSet<ulong>());
        return Ok(new
        {
            success = true, message = "All ignored channels cleared"
        });
    }

    /// <summary>
    ///     Sets multiple log types at once
    /// </summary>
    [HttpPut("bulk-update")]
    public async Task<IActionResult> BulkUpdateLogChannels(ulong guildId,
        [FromBody] BulkUpdateLogChannelsRequest request)
    {
        var results = new List<object>();

        foreach (var mapping in request.LogTypeMappings)
        {
            if (Enum.TryParse<LogCommandService.LogType>(mapping.LogType, out var type))
            {
                await logService.SetLogChannel(guildId, mapping.ChannelId ?? 0, type);

                results.Add(new
                {
                    logType = mapping.LogType, channelId = mapping.ChannelId, success = true
                });
            }
            else
            {
                results.Add(new
                {
                    logType = mapping.LogType, error = "Invalid log type", success = false
                });
            }
        }

        return Ok(new
        {
            results
        });
    }

    /// <summary>
    ///     Disables all logging for a guild
    /// </summary>
    [HttpDelete("disable-all")]
    public async Task<IActionResult> DisableAllLogging(ulong guildId)
    {
        await logService.LogSetByType(guildId, 0, LogCommandService.LogCategoryTypes.None);

        return Ok(new
        {
            success = true, message = "All logging disabled"
        });
    }

    /// <summary>
    ///     Gets a summary of logging statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetLoggingStatistics(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var logSettings = await db.LoggingV2.FirstOrDefaultAsync(ls => ls.GuildId == guildId);
        if (logSettings == null)
        {
            return Ok(new
            {
                enabled = false,
                totalConfiguredTypes = 0,
                ignoredChannelCount = 0,
                configuredCategories = new List<string>()
            });
        }

        var ignoredChannels = await logService.GetIgnoredChannels(guildId);
        var ignoredChannelCount = ignoredChannels.Count;

        // Count configured log types
        var configuredTypes = 0;
        var properties = typeof(LoggingV2).GetProperties()
            .Where(p => p.Name.EndsWith("Id") && p.Name != "Id" && p.Name != "GuildId");

        foreach (var prop in properties)
        {
            var value = prop.GetValue(logSettings) as ulong?;
            if (value is > 0)
                configuredTypes++;
        }

        // Determine configured categories
        var configuredCategories = new List<string>();

        // Check message category
        if ((logSettings.MessageUpdatedId ?? 0) > 0 || (logSettings.MessageDeletedId ?? 0) > 0)
            configuredCategories.Add("Messages");

        // Check user category
        if ((logSettings.UserJoinedId ?? 0) > 0 || (logSettings.UserLeftId ?? 0) > 0 ||
            (logSettings.UserBannedId ?? 0) > 0 || (logSettings.UserUnbannedId ?? 0) > 0 ||
            (logSettings.UserUpdatedId ?? 0) > 0 || (logSettings.UsernameUpdatedId ?? 0) > 0 ||
            (logSettings.NicknameUpdatedId ?? 0) > 0 || (logSettings.AvatarUpdatedId ?? 0) > 0 ||
            (logSettings.UserRoleAddedId ?? 0) > 0 || (logSettings.UserRoleRemovedId ?? 0) > 0 ||
            (logSettings.UserMutedId ?? 0) > 0 || (logSettings.LogVoicePresenceId ?? 0) > 0)
            configuredCategories.Add("Users");

        // Check thread category
        if ((logSettings.ThreadCreatedId ?? 0) > 0 || (logSettings.ThreadDeletedId ?? 0) > 0 ||
            (logSettings.ThreadUpdatedId ?? 0) > 0)
            configuredCategories.Add("Threads");

        // Check roles category
        if ((logSettings.RoleCreatedId ?? 0) > 0 || (logSettings.RoleDeletedId ?? 0) > 0 ||
            (logSettings.RoleUpdatedId ?? 0) > 0)
            configuredCategories.Add("Roles");

        // Check server category
        if ((logSettings.ServerUpdatedId ?? 0) > 0 || (logSettings.EventCreatedId ?? 0) > 0)
            configuredCategories.Add("Server");

        // Check channel category
        if ((logSettings.ChannelCreatedId ?? 0) > 0 || (logSettings.ChannelDestroyedId ?? 0) > 0 ||
            (logSettings.ChannelUpdatedId ?? 0) > 0)
            configuredCategories.Add("Channel");

        return Ok(new
        {
            enabled = true, totalConfiguredTypes = configuredTypes, ignoredChannelCount, configuredCategories
        });
    }

    /// <summary>
    ///     Gets real-time updates for logging events via WebSocket
    /// </summary>
    [HttpGet("events/subscribe")]
    public async Task<IActionResult> SubscribeToLoggingEvents(ulong guildId)
    {
        // This endpoint would typically be used with Server-Sent Events or WebSockets
        // For now, return current configuration with subscription info
        var config = await GetLoggingConfiguration(guildId);

        return Ok(new
        {
            currentConfiguration = config,
            subscriptionInfo = new
            {
                message = "For real-time updates, use WebSocket connection or Server-Sent Events",
                websocketEndpoint = $"/ws/logging/{guildId}",
                sseEndpoint = $"/api/logging/{guildId}/events/stream"
            }
        });
    }

    /// <summary>
    ///     Stream logging events using Server-Sent Events
    /// </summary>
    [HttpGet("events/stream")]
    public async Task StreamLoggingEvents(ulong guildId)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        // Send initial configuration
        var config = await GetLoggingConfiguration(guildId);
        await Response.WriteAsync($"data: {JsonSerializer.Serialize(new { type = "initial", config })}\n\n");
        await Response.Body.FlushAsync();

        // Keep connection alive and send periodic configuration updates
        try
        {
            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                await Task.Delay(30000); // Send heartbeat every 30 seconds
                await Response.WriteAsync(
                    $"data: {JsonSerializer.Serialize(new { type = "heartbeat", timestamp = DateTime.UtcNow })}\n\n");
                await Response.Body.FlushAsync();
            }
        }
        catch (Exception)
        {
            // Connection closed
        }
    }
}