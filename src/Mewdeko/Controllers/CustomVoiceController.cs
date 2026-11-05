using System.Text.Json;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Controllers.Common.CustomVoice;
using Mewdeko.Database.DbContextStuff;
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
public class CustomVoiceController(
    IDataConnectionFactory dbFactory,
    IDashboardAuditContext auditContext,
    CustomVoiceService customVoiceService) : Controller
{
    /// <summary>
    ///     Gets a member's saved custom voice preferences. Returns an empty preference set (all values null)
    ///     when the member has never saved any, so the dashboard can still edit them.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member's user ID.</param>
    [HttpGet("user-preferences/{userId}")]
    public async Task<IActionResult> GetUserPreferences(ulong guildId, ulong userId)
    {
        if (userId == 0)
        {
            return BadRequest(new
            {
                error = "A valid user ID is required"
            });
        }

        var prefs = await customVoiceService.GetUserPreferencesAsync(guildId, userId);

        return Ok(new CustomVoiceUserPreferenceResponse
        {
            UserId = userId,
            GuildId = guildId,
            DefaultName = prefs?.NameFormat,
            DefaultUserLimit = prefs?.UserLimit,
            DefaultBitrate = prefs?.Bitrate
        });
    }

    /// <summary>
    ///     Replaces a member's saved channel name, user limit, and bitrate preferences, creating the record if needed.
    ///     Lock, keep alive, and whitelist/blacklist preferences are left untouched.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member's user ID.</param>
    /// <param name="request">The new preference values; null clears a value.</param>
    [HttpPut("user-preferences/{userId}")]
    public async Task<IActionResult> UpdateUserPreferences(ulong guildId, ulong userId,
        [FromBody] UpdateCustomVoiceUserPreferenceRequest request)
    {
        if (userId == 0)
        {
            return BadRequest(new
            {
                error = "A valid user ID is required"
            });
        }

        if (request.DefaultUserLimit is < 0)
        {
            return BadRequest(new
            {
                error = "User limit cannot be negative"
            });
        }

        if (request.DefaultBitrate is <= 0)
        {
            return BadRequest(new
            {
                error = "Bitrate must be greater than 0"
            });
        }

        var prefs = await customVoiceService.GetUserPreferencesAsync(guildId, userId);

        auditContext.RecordBefore(prefs);

        prefs ??= new UserVoicePreference
        {
            GuildId = guildId, UserId = userId, DateAdded = DateTime.UtcNow
        };

        prefs.NameFormat = string.IsNullOrWhiteSpace(request.DefaultName) ? null : request.DefaultName.Trim();
        prefs.UserLimit = request.DefaultUserLimit;
        prefs.Bitrate = request.DefaultBitrate;

        await customVoiceService.SetUserPreferencesAsync(prefs);

        auditContext.RecordAfter(prefs);
        return Ok(new
        {
            success = true, message = "User preferences updated successfully"
        });
    }

    /// <summary>
    ///     Gets the custom voice configuration for a guild
    /// </summary>
    [HttpGet("configuration")]
    public async Task<IActionResult> GetConfiguration(ulong guildId, [FromQuery] ulong? hub = null)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var config = await FindConfigAsync(db, guildId, hub);

        if (config == null)
        {
            return Ok(new
            {
                enabled = false
            });
        }

        return Ok(ToResponse(config));
    }

    /// <summary>
    ///     Gets the settings of every join-to-create hub in a guild
    /// </summary>
    [HttpGet("hubs")]
    public async Task<IActionResult> GetHubs(ulong guildId)
    {
        var hubs = await customVoiceService.GetHubConfigsAsync(guildId);
        return Ok(hubs.Select(ToResponse).ToList());
    }

    /// <summary>
    ///     Finds a guild's hub settings, defaulting to the first hub
    /// </summary>
    private static async Task<CustomVoiceConfig?> FindConfigAsync(MewdekoDb db, ulong guildId, ulong? hub)
    {
        var configs = await db.CustomVoiceConfigs.Where(c => c.GuildId == guildId).OrderBy(c => c.Id).ToListAsync();
        return hub is { } hubId
            ? configs.FirstOrDefault(c => c.HubVoiceChannelId == hubId)
            : configs.OrderBy(c => c.HubVoiceChannelId == 0).FirstOrDefault();
    }

    /// <summary>
    ///     Maps hub settings to the response model
    /// </summary>
    private static CustomVoiceConfigurationResponse ToResponse(CustomVoiceConfig config)
    {
        return new CustomVoiceConfigurationResponse
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
            CreateTextChannel = config.CreateTextChannel,
            CustomVoiceAdminRoleId = config.CustomVoiceAdminRoleId,
            HubName = config.HubName,
            OwnerLockMinutes = config.OwnerLockMinutes,
            OwnerManageChannel = config.OwnerManageChannel,
            OwnerManageRoles = config.OwnerManageRoles,
            OwnerMoveMembers = config.OwnerMoveMembers,
            OwnerPrioritySpeaker = config.OwnerPrioritySpeaker,
            SyncPermissions = config.SyncPermissions,
            ModeratorRoleIds = CustomVoiceService.ParseRoleIds(config.ModeratorRoleIds),
            CleanupLeftovers = config.CleanupLeftovers
        };
    }

    /// <summary>
    ///     Updates the custom voice configuration for a guild. Without a hub it updates the first hub, and a hub
    ///     that does not exist yet is created.
    /// </summary>
    [HttpPut("configuration")]
    public async Task<IActionResult> UpdateConfiguration(ulong guildId,
        [FromBody] CustomVoiceConfigurationRequest request, [FromQuery] ulong? hub = null)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var config = await FindConfigAsync(db, guildId, hub) ?? new CustomVoiceConfig
        {
            GuildId = guildId, DateAdded = DateTime.UtcNow
        };

        auditContext.RecordBefore(config.Id == 0 ? null : config);

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
        config.CreateTextChannel = request.CreateTextChannel;
        config.CustomVoiceAdminRoleId = request.CustomVoiceAdminRoleId;
        if (request.HubName != null)
            config.HubName = string.IsNullOrWhiteSpace(request.HubName) ? null : request.HubName.Trim();
        if (request.OwnerLockMinutes is { } ownerLock)
            config.OwnerLockMinutes = Math.Clamp(ownerLock, 0, 10080);
        if (request.OwnerManageChannel is { } manageChannel)
            config.OwnerManageChannel = manageChannel;
        if (request.OwnerManageRoles is { } manageRoles)
            config.OwnerManageRoles = manageRoles;
        if (request.OwnerMoveMembers is { } moveMembers)
            config.OwnerMoveMembers = moveMembers;
        if (request.OwnerPrioritySpeaker is { } prioritySpeaker)
            config.OwnerPrioritySpeaker = prioritySpeaker;
        if (request.SyncPermissions is { } syncPermissions && Enum.IsDefined((CustomVoiceSyncMode)syncPermissions))
            config.SyncPermissions = syncPermissions;
        if (request.ModeratorRoleIds != null)
            config.ModeratorRoleIds = string.Join(',', request.ModeratorRoleIds.Where(x => x != 0).Distinct());
        if (request.CleanupLeftovers is { } cleanupLeftovers)
            config.CleanupLeftovers = cleanupLeftovers;

        if (config.Id == 0)
        {
            await db.InsertAsync(config);
        }
        else
        {
            await db.UpdateAsync(config);
        }

        auditContext.RecordAfter(config);
        return Ok(new
        {
            success = true, message = "Custom voice configuration updated successfully"
        });
    }

    /// <summary>
    ///     Disables custom voice for a guild, or removes a single hub when one is given
    /// </summary>
    [HttpDelete("configuration")]
    public async Task<IActionResult> DisableCustomVoice(ulong guildId, [FromQuery] ulong? hub = null)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        auditContext.RecordBefore(await FindConfigAsync(db, guildId, hub));
        await db.CustomVoiceConfigs
            .Where(c => c.GuildId == guildId && (hub == null || c.HubVoiceChannelId == hub))
            .DeleteAsync();

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
            IsHidden = c.IsHidden,
            HubVoiceChannelId = c.HubVoiceChannelId,
            OwnerLeftAt = c.OwnerLeftAt,
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
            IsHidden = channel.IsHidden,
            HubVoiceChannelId = channel.HubVoiceChannelId,
            OwnerLeftAt = channel.OwnerLeftAt,
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
    ///     Hides a custom voice channel from members who are not in it, or reveals it again
    /// </summary>
    [HttpPut("channels/{channelId}/hidden")]
    public async Task<IActionResult> SetChannelHidden(ulong guildId, ulong channelId,
        [FromBody] SetCustomVoiceChannelHiddenRequest request)
    {
        if (await customVoiceService.GetChannelAsync(guildId, channelId) == null)
        {
            return NotFound(new
            {
                error = "Custom voice channel not found"
            });
        }

        if (!await customVoiceService.SetHiddenAsync(guildId, channelId, request.Hidden))
            return StatusCode(500, new
            {
                error = "Failed to change whether the channel is hidden"
            });

        return Ok(new
        {
            success = true, message = request.Hidden ? "Channel hidden" : "Channel revealed"
        });
    }

    /// <summary>
    ///     Disconnects a member from a custom voice channel
    /// </summary>
    [HttpPost("channels/{channelId}/kick/{userId}")]
    public async Task<IActionResult> KickFromChannel(ulong guildId, ulong channelId, ulong userId)
    {
        if (!await customVoiceService.KickUserAsync(guildId, channelId, userId))
        {
            return BadRequest(new
            {
                error = "The member is not in that channel, or is its owner"
            });
        }

        return Ok(new
        {
            success = true, message = "Member disconnected"
        });
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

        auditContext.RecordBefore(channel);

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

        auditContext.RecordAfter(channel);
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

        auditContext.RecordBefore(await db.CustomVoiceChannels
            .FirstOrDefaultAsync(c => c.GuildId == guildId && c.ChannelId == channelId));

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