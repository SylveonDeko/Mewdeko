using System.Text.Json;
using DataModel;
using LinqToDB;
using Mewdeko.Controllers.Common.CustomVoice;
using Mewdeko.Modules.CustomVoice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing custom voice channels and hub configuration
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class CustomVoiceController(CustomVoiceService customVoiceService, IDataConnectionFactory dbFactory) : Controller
{
    /// <summary>
    ///     Gets the custom voice configuration for a guild
    /// </summary>
    [HttpGet("configuration")]
    public async Task<IActionResult> GetConfiguration(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var config = await db.CustomVoiceConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);

        if (config == null)
        {
            return Ok(new
            {
                enabled = false
            });
        }

        return Ok(new CustomVoiceConfigurationResponse
        {
            Enabled = true,
            HubVoiceChannelId = config.HubVoiceChannelId,
            ChannelCategoryId = config.ChannelCategoryId,
            DefaultNameFormat = config.DefaultNameFormat,
            DefaultUserLimit = config.DefaultUserLimit,
            DefaultBitrate = config.DefaultBitrate,
            DeleteWhenEmpty = config.DeleteWhenEmpty,
            EmptyChannelTimeout = config.EmptyChannelTimeout,
            AllowMultipleChannels = config.AllowMultipleChannels,
            AllowNameCustomization = config.AllowNameCustomization,
            AllowUserLimitCustomization = config.AllowUserLimitCustomization,
            AllowBitrateCustomization = config.AllowBitrateCustomization,
            AllowLocking = config.AllowLocking,
            AllowUserManagement = config.AllowUserManagement,
            MaxUserLimit = config.MaxUserLimit,
            MaxBitrate = config.MaxBitrate,
            PersistUserPreferences = config.PersistUserPreferences,
            AutoPermission = config.AutoPermission,
            CustomVoiceAdminRoleId = config.CustomVoiceAdminRoleId
        });
    }

    /// <summary>
    ///     Updates the custom voice configuration for a guild
    /// </summary>
    [HttpPut("configuration")]
    public async Task<IActionResult> UpdateConfiguration(ulong guildId,
        [FromBody] CustomVoiceConfigurationRequest request)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var config = await db.CustomVoiceConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId) ?? new CustomVoiceConfig
        {
            GuildId = guildId, DateAdded = DateTime.UtcNow
        };

        // Update all configurable properties
        config.HubVoiceChannelId = request.HubVoiceChannelId;
        config.ChannelCategoryId = request.ChannelCategoryId;
        config.DefaultNameFormat = request.DefaultNameFormat ?? "{username}'s Channel";
        config.DefaultUserLimit = request.DefaultUserLimit;
        config.DefaultBitrate = request.DefaultBitrate;
        config.DeleteWhenEmpty = request.DeleteWhenEmpty;
        config.EmptyChannelTimeout = request.EmptyChannelTimeout;
        config.AllowMultipleChannels = request.AllowMultipleChannels;
        config.AllowNameCustomization = request.AllowNameCustomization;
        config.AllowUserLimitCustomization = request.AllowUserLimitCustomization;
        config.AllowBitrateCustomization = request.AllowBitrateCustomization;
        config.AllowLocking = request.AllowLocking;
        config.AllowUserManagement = request.AllowUserManagement;
        config.MaxUserLimit = request.MaxUserLimit;
        config.MaxBitrate = request.MaxBitrate;
        config.PersistUserPreferences = request.PersistUserPreferences;
        config.AutoPermission = request.AutoPermission;
        config.CustomVoiceAdminRoleId = request.CustomVoiceAdminRoleId;

        if (config.Id == 0)
        {
            await db.InsertAsync(config);
        }
        else
        {
            await db.UpdateAsync(config);
        }

        return Ok(new
        {
            success = true, message = "Custom voice configuration updated successfully"
        });
    }

    /// <summary>
    ///     Disables custom voice for a guild
    /// </summary>
    [HttpDelete("configuration")]
    public async Task<IActionResult> DisableCustomVoice(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        await db.CustomVoiceConfigs.Where(c => c.GuildId == guildId).DeleteAsync();

        return Ok(new
        {
            success = true, message = "Custom voice disabled successfully"
        });
    }


    /// <summary>
    ///     Gets all active custom voice channels for a guild
    /// </summary>
    [HttpGet("channels")]
    public async Task<IActionResult> GetActiveChannels(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var channels = await db.CustomVoiceChannels
            .Where(c => c.GuildId == guildId)
            .OrderByDescending(c => c.LastActive)
            .ToListAsync();

        var channelData = channels.Select(c => new CustomVoiceChannelResponse
        {
            ChannelId = c.ChannelId,
            OwnerId = c.OwnerId,
            CreatedAt = c.CreatedAt,
            LastActive = c.LastActive,
            IsLocked = c.IsLocked,
            KeepAlive = c.KeepAlive,
            AllowedUsers =
                string.IsNullOrEmpty(c.AllowedUsersJson)
                    ? new List<ulong>()
                    : JsonSerializer.Deserialize<List<ulong>>(c.AllowedUsersJson) ?? new List<ulong>(),
            DeniedUsers = string.IsNullOrEmpty(c.DeniedUsersJson)
                ? new List<ulong>()
                : JsonSerializer.Deserialize<List<ulong>>(c.DeniedUsersJson) ?? new List<ulong>()
        }).ToList();

        return Ok(channelData);
    }

    /// <summary>
    ///     Gets details for a specific custom voice channel
    /// </summary>
    [HttpGet("channels/{channelId}")]
    public async Task<IActionResult> GetChannelDetails(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var channel = await db.CustomVoiceChannels
            .FirstOrDefaultAsync(c => c.GuildId == guildId && c.ChannelId == channelId);

        if (channel == null)
        {
            return NotFound(new
            {
                error = "Custom voice channel not found"
            });
        }

        var response = new CustomVoiceChannelResponse
        {
            ChannelId = channel.ChannelId,
            OwnerId = channel.OwnerId,
            CreatedAt = channel.CreatedAt,
            LastActive = channel.LastActive,
            IsLocked = channel.IsLocked,
            KeepAlive = channel.KeepAlive,
            AllowedUsers =
                string.IsNullOrEmpty(channel.AllowedUsersJson)
                    ? new List<ulong>()
                    : JsonSerializer.Deserialize<List<ulong>>(channel.AllowedUsersJson) ?? new List<ulong>(),
            DeniedUsers = string.IsNullOrEmpty(channel.DeniedUsersJson)
                ? new List<ulong>()
                : JsonSerializer.Deserialize<List<ulong>>(channel.DeniedUsersJson) ?? new List<ulong>()
        };

        return Ok(response);
    }

    /// <summary>
    ///     Updates a custom voice channel's settings
    /// </summary>
    [HttpPut("channels/{channelId}")]
    public async Task<IActionResult> UpdateChannel(ulong guildId, ulong channelId,
        [FromBody] UpdateCustomVoiceChannelRequest request)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var channel = await db.CustomVoiceChannels
            .FirstOrDefaultAsync(c => c.GuildId == guildId && c.ChannelId == channelId);

        if (channel == null)
        {
            return NotFound(new
            {
                error = "Custom voice channel not found"
            });
        }

        // Update channel properties
        if (request.IsLocked.HasValue)
            channel.IsLocked = request.IsLocked.Value;

        if (request.KeepAlive.HasValue)
            channel.KeepAlive = request.KeepAlive.Value;

        if (request.AllowedUsers != null)
            channel.AllowedUsersJson = JsonSerializer.Serialize(request.AllowedUsers);

        if (request.DeniedUsers != null)
            channel.DeniedUsersJson = JsonSerializer.Serialize(request.DeniedUsers);

        channel.LastActive = DateTime.UtcNow;

        await db.UpdateAsync(channel);

        return Ok(new
        {
            success = true, message = "Channel updated successfully"
        });
    }

    /// <summary>
    ///     Deletes a custom voice channel
    /// </summary>
    [HttpDelete("channels/{channelId}")]
    public async Task<IActionResult> DeleteChannel(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var deleted = await db.CustomVoiceChannels
            .Where(c => c.GuildId == guildId && c.ChannelId == channelId)
            .DeleteAsync();

        if (deleted == 0)
        {
            return NotFound(new
            {
                error = "Custom voice channel not found"
            });
        }

        return Ok(new
        {
            success = true, message = "Channel deleted successfully"
        });
    }

    /// <summary>
    ///     Gets statistics for custom voice usage
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var config = await db.CustomVoiceConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);

        if (config == null)
        {
            return Ok(new
            {
                enabled = false, totalChannels = 0, activeChannels = 0
            });
        }

        var totalChannels = await db.CustomVoiceChannels.CountAsync(c => c.GuildId == guildId);
        var activeChannels = await db.CustomVoiceChannels
            .CountAsync(c => c.GuildId == guildId && c.LastActive > DateTime.UtcNow.AddHours(-1));
        var lockedChannels = await db.CustomVoiceChannels
            .CountAsync(c => c.GuildId == guildId && c.IsLocked);
        var keepAliveChannels = await db.CustomVoiceChannels
            .CountAsync(c => c.GuildId == guildId && c.KeepAlive);

        return Ok(new
        {
            enabled = true,
            totalChannels,
            activeChannels,
            lockedChannels,
            keepAliveChannels,
            hubChannelId = config.HubVoiceChannelId,
            categoryId = config.ChannelCategoryId
        });
    }

    /// <summary>
    ///     Bulk deletes inactive custom voice channels
    /// </summary>
    [HttpDelete("cleanup")]
    public async Task<IActionResult> CleanupInactiveChannels(ulong guildId, [FromQuery] int hoursInactive = 24)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var cutoffTime = DateTime.UtcNow.AddHours(-hoursInactive);

        var deleted = await db.CustomVoiceChannels
            .Where(c => c.GuildId == guildId && c.LastActive < cutoffTime && !c.KeepAlive)
            .DeleteAsync();

        return Ok(new
        {
            success = true, deletedChannels = deleted, message = $"Cleaned up {deleted} inactive channels"
        });
    }
}