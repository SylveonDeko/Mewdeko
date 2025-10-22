using System.Text.Json;
using DataModel;
using Discord.Commands;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Controllers.Common.Administration;
using Mewdeko.Controllers.Common.Permissions;
using Mewdeko.Controllers.Common.Protection;
using Mewdeko.Modules.Administration;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Help;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing guild administration settings (auto-assign roles, protection, etc.)
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class AdministrationController(
    AutoAssignRoleService autoAssignRoleService,
    ProtectionService protectionService,
    SelfAssignedRolesService selfAssignedRolesService,
    AdministrationService administrationService,
    AutoBanRoleService autoBanRoleService,
    VcRoleService vcRoleService,
    GuildTimezoneService timezoneService,
    DiscordPermOverrideService permOverrideService,
    ServerRecoveryService serverRecoveryService,
    GameVoiceChannelService gameVoiceChannelService,
    RoleCommandsService roleCommandsService,
    GuildSettingsService guildSettingsService,
    PermissionService permissionService,
    CmdCdService cmdCdService,
    CommandService commandService,
    IDataConnectionFactory dbFactory,
    UserPunishService userPunishService,
    DiscordShardedClient client) : Controller
{
    /// <summary>
    ///     Gets auto-assign role settings for normal users
    /// </summary>
    [HttpGet("auto-assign-roles")]
    public async Task<IActionResult> GetAutoAssignRoles(ulong guildId)
    {
        var normalRoles = await autoAssignRoleService.TryGetNormalRoles(guildId);
        var botRoles = await autoAssignRoleService.TryGetBotRoles(guildId);

        return Ok(new
        {
            normalRoles = normalRoles.ToList(), botRoles = botRoles.ToList()
        });
    }

    /// <summary>
    ///     Sets auto-assign roles for normal users
    /// </summary>
    [HttpPost("auto-assign-roles/normal")]
    public async Task<IActionResult> SetAutoAssignRoles(ulong guildId, [FromBody] List<ulong> roleIds)
    {
        await autoAssignRoleService.SetAarRolesAsync(guildId, roleIds);
        return Ok();
    }

    /// <summary>
    ///     Sets auto-assign roles for bots
    /// </summary>
    [HttpPost("auto-assign-roles/bots")]
    public async Task<IActionResult> SetBotAutoAssignRoles(ulong guildId, [FromBody] List<ulong> roleIds)
    {
        await autoAssignRoleService.SetAabrRolesAsync(guildId, roleIds);
        return Ok();
    }

    /// <summary>
    ///     Toggles an auto-assign role for normal users
    /// </summary>
    [HttpPost("auto-assign-roles/normal/{roleId}/toggle")]
    public async Task<IActionResult> ToggleAutoAssignRole(ulong guildId, ulong roleId)
    {
        var result = await autoAssignRoleService.ToggleAarAsync(guildId, roleId);
        return Ok(result);
    }

    /// <summary>
    ///     Toggles an auto-assign role for bots
    /// </summary>
    [HttpPost("auto-assign-roles/bots/{roleId}/toggle")]
    public async Task<IActionResult> ToggleBotAutoAssignRole(ulong guildId, ulong roleId)
    {
        var result = await autoAssignRoleService.ToggleAabrAsync(guildId, roleId);
        return Ok(result);
    }

    /// <summary>
    ///     Gets current protection settings status
    /// </summary>
    [HttpGet("protection/status")]
    public async Task<IActionResult> GetProtectionStatus(ulong guildId)
    {
        await Task.CompletedTask;

        // Get protection stats using the GetAntiStats method
        var (antiSpamStats, antiRaidStats, antiAltStats, antiMassMentionStats, antiPatternStats, antiMassPostStats,
                antiPostChannelStats) =
            protectionService.GetAntiStats(guildId);

        return Ok(new
        {
            antiRaid = new
            {
                enabled = antiRaidStats != null,
                userThreshold = antiRaidStats?.AntiRaidSettings.UserThreshold ?? 0,
                seconds = antiRaidStats?.AntiRaidSettings.Seconds ?? 0,
                action = antiRaidStats?.AntiRaidSettings.Action ?? 0,
                punishDuration = antiRaidStats?.AntiRaidSettings.PunishDuration ?? 0,
                usersCount = antiRaidStats?.UsersCount ?? 0
            },
            antiSpam = new
            {
                enabled = antiSpamStats != null,
                messageThreshold = antiSpamStats?.AntiSpamSettings.MessageThreshold ?? 0,
                action = antiSpamStats?.AntiSpamSettings.Action ?? 0,
                muteTime = antiSpamStats?.AntiSpamSettings.MuteTime ?? 0,
                roleId = antiSpamStats?.AntiSpamSettings.RoleId ?? 0,
                userCount = antiSpamStats?.UserStats.Count ?? 0
            },
            antiAlt = new
            {
                enabled = antiAltStats != null,
                minAge = antiAltStats?.MinAge ?? "",
                action = antiAltStats?.Action ?? 0,
                actionDuration = antiAltStats?.ActionDurationMinutes ?? 0,
                roleId = antiAltStats?.RoleId ?? 0,
                counter = antiAltStats?.Counter ?? 0
            },
            antiMassMention = new
            {
                enabled = antiMassMentionStats != null,
                mentionThreshold = antiMassMentionStats?.AntiMassMentionSettings.MentionThreshold ?? 0,
                maxMentionsInTimeWindow = antiMassMentionStats?.AntiMassMentionSettings.MaxMentionsInTimeWindow ?? 0,
                timeWindowSeconds = antiMassMentionStats?.AntiMassMentionSettings.TimeWindowSeconds ?? 0,
                action = antiMassMentionStats?.AntiMassMentionSettings.Action ?? 0,
                muteTime = antiMassMentionStats?.AntiMassMentionSettings.MuteTime ?? 0,
                roleId = antiMassMentionStats?.AntiMassMentionSettings.RoleId ?? 0,
                ignoreBots = antiMassMentionStats?.AntiMassMentionSettings.IgnoreBots ?? false,
                userCount = antiMassMentionStats?.UserStats.Count ?? 0
            },
            antiPattern = new
            {
                enabled = antiPatternStats != null,
                action = antiPatternStats?.AntiPatternSettings.Action ?? 0,
                punishDuration = antiPatternStats?.AntiPatternSettings.PunishDuration ?? 0,
                roleId = antiPatternStats?.AntiPatternSettings.RoleId ?? 0,
                checkAccountAge = antiPatternStats?.AntiPatternSettings.CheckAccountAge ?? false,
                maxAccountAgeMonths = antiPatternStats?.AntiPatternSettings.MaxAccountAgeMonths ?? 6,
                checkJoinTiming = antiPatternStats?.AntiPatternSettings.CheckJoinTiming ?? false,
                maxJoinHours = antiPatternStats?.AntiPatternSettings.MaxJoinHours ?? 48.0,
                checkBatchCreation = antiPatternStats?.AntiPatternSettings.CheckBatchCreation ?? false,
                checkOfflineStatus = antiPatternStats?.AntiPatternSettings.CheckOfflineStatus ?? false,
                checkNewAccounts = antiPatternStats?.AntiPatternSettings.CheckNewAccounts ?? false,
                newAccountDays = antiPatternStats?.AntiPatternSettings.NewAccountDays ?? 7,
                minimumScore = antiPatternStats?.AntiPatternSettings.MinimumScore ?? 15,
                patternCount = antiPatternStats?.AntiPatternSettings.AntiPatternPatterns?.Count() ?? 0,
                counter = antiPatternStats?.Counter ?? 0
            },
            antiMassPost = new
            {
                enabled = antiMassPostStats != null,
                action = antiMassPostStats?.AntiMassPostSettings.Action ?? 0,
                channelThreshold = antiMassPostStats?.AntiMassPostSettings.ChannelThreshold ?? 3,
                timeWindowSeconds = antiMassPostStats?.AntiMassPostSettings.TimeWindowSeconds ?? 60,
                contentSimilarityThreshold = antiMassPostStats?.AntiMassPostSettings.ContentSimilarityThreshold ?? 0.8,
                minContentLength = antiMassPostStats?.AntiMassPostSettings.MinContentLength ?? 20,
                checkLinksOnly = antiMassPostStats?.AntiMassPostSettings.CheckLinksOnly ?? true,
                checkDuplicateContent = antiMassPostStats?.AntiMassPostSettings.CheckDuplicateContent ?? true,
                requireIdenticalContent = antiMassPostStats?.AntiMassPostSettings.RequireIdenticalContent ?? false,
                caseSensitive = antiMassPostStats?.AntiMassPostSettings.CaseSensitive ?? false,
                deleteMessages = antiMassPostStats?.AntiMassPostSettings.DeleteMessages ?? true,
                notifyUser = antiMassPostStats?.AntiMassPostSettings.NotifyUser ?? true,
                punishDuration = antiMassPostStats?.AntiMassPostSettings.PunishDuration ?? 0,
                roleId = antiMassPostStats?.AntiMassPostSettings.RoleId ?? 0,
                ignoreBots = antiMassPostStats?.AntiMassPostSettings.IgnoreBots ?? true,
                maxMessagesTracked = antiMassPostStats?.AntiMassPostSettings.MaxMessagesTracked ?? 50,
                userCount = antiMassPostStats?.UserStats.Count ?? 0,
                counter = antiMassPostStats?.Counter ?? 0
            },
            antiPostChannel = new
            {
                enabled = antiPostChannelStats != null,
                action = antiPostChannelStats?.AntiPostChannelSettings.Action ?? 0,
                deleteMessages = antiPostChannelStats?.AntiPostChannelSettings.DeleteMessages ?? true,
                notifyUser = antiPostChannelStats?.AntiPostChannelSettings.NotifyUser ?? true,
                punishDuration = antiPostChannelStats?.AntiPostChannelSettings.PunishDuration ?? 0,
                roleId = antiPostChannelStats?.AntiPostChannelSettings.RoleId ?? 0,
                ignoreBots = antiPostChannelStats?.AntiPostChannelSettings.IgnoreBots ?? true,
                channelCount = antiPostChannelStats?.AntiPostChannelSettings.AntiPostChannelChannels?.Count() ?? 0,
                channels =
                    antiPostChannelStats?.AntiPostChannelSettings.AntiPostChannelChannels?.Select(c => c.ChannelId)
                        .ToList() ?? new List<ulong>(),
                ignoredRoles =
                    antiPostChannelStats?.AntiPostChannelSettings.AntiPostChannelIgnoredRoles?.Select(r => r.RoleId)
                        .ToList() ?? new List<ulong>(),
                ignoredUsers =
                    antiPostChannelStats?.AntiPostChannelSettings.AntiPostChannelIgnoredUsers?.Select(u => u.UserId)
                        .ToList() ?? new List<ulong>(),
                counter = antiPostChannelStats?.Counter ?? 0
            }
        });
    }

    /// <summary>
    ///     Configures anti-raid protection
    /// </summary>
    [HttpPut("protection/anti-raid")]
    public async Task<IActionResult> ConfigureAntiRaid(ulong guildId, [FromBody] AntiRaidConfigRequest? request)
    {
        if (request == null)
            return BadRequest("Invalid request data");

        if (request.Enabled)
        {
            // Validate parameters based on bot requirements
            if (request.UserThreshold is < 2 or > 30)
                return BadRequest("User threshold must be between 2 and 30");

            if (request.Seconds is < 2 or > 300)
                return BadRequest("Time window must be between 2 and 300 seconds");

            if (request.PunishDuration is < 0 or > 1440)
                return BadRequest("Punishment duration must be between 0 and 1440 minutes");

            if (request.Action == PunishmentAction.AddRole)
                return BadRequest("AddRole action is not supported for anti-raid");

            var result = await protectionService.StartAntiRaidAsync(
                guildId,
                request.UserThreshold,
                request.Seconds,
                request.Action,
                request.PunishDuration);

            if (result == null)
                return BadRequest("Failed to start anti-raid protection");

            return Ok(new
            {
                success = true, settings = result
            });
        }
        else
        {
            var success = await protectionService.TryStopAntiRaid(guildId);
            return Ok(new
            {
                success
            });
        }
    }

    /// <summary>
    ///     Configures anti-spam protection
    /// </summary>
    [HttpPut("protection/anti-spam")]
    public async Task<IActionResult> ConfigureAntiSpam(ulong guildId, [FromBody] AntiSpamConfigRequest? request)
    {
        if (request == null)
            return BadRequest("Invalid request data");

        if (request.Enabled)
        {
            // Validate parameters based on bot requirements
            if (request.MessageThreshold is < 2 or > 10)
                return BadRequest("Message threshold must be between 2 and 10");

            if (request.MuteTime is < 0 or > 1440)
                return BadRequest("Mute time must be between 0 and 1440 minutes");

            await protectionService.StartAntiSpamAsync(
                guildId,
                request.MessageThreshold,
                request.Action,
                request.MuteTime,
                request.RoleId);

            return Ok(new
            {
                success = true
            });
        }
        else
        {
            var success = await protectionService.TryStopAntiSpam(guildId);
            return Ok(new
            {
                success
            });
        }
    }

    /// <summary>
    ///     Toggles ignored channel for anti-spam
    /// </summary>
    [HttpPost("protection/anti-spam/ignored-channels/{channelId}")]
    public async Task<IActionResult> ToggleAntiSpamIgnoredChannel(ulong guildId, ulong channelId)
    {
        var added = await protectionService.AntiSpamIgnoreAsync(guildId, channelId);
        return Ok(new
        {
            added
        });
    }

    /// <summary>
    ///     Configures anti-alt protection
    /// </summary>
    [HttpPut("protection/anti-alt")]
    public async Task<IActionResult> ConfigureAntiAlt(ulong guildId, [FromBody] AntiAltConfigRequest? request)
    {
        if (request == null)
            return BadRequest("Invalid request data");

        if (request.Enabled)
        {
            // Validate parameters
            if (request.MinAgeMinutes < 1)
                return BadRequest("Minimum age must be at least 1 minute");

            if (request.ActionDurationMinutes is < 0 or > 1440)
                return BadRequest("Action duration must be between 0 and 1440 minutes");

            await protectionService.StartAntiAltAsync(
                guildId,
                request.MinAgeMinutes,
                request.Action,
                request.ActionDurationMinutes,
                request.RoleId);

            return Ok(new
            {
                success = true
            });
        }
        else
        {
            var success = await protectionService.TryStopAntiAlt(guildId);
            return Ok(new
            {
                success
            });
        }
    }

    /// <summary>
    ///     Configures anti-mass mention protection
    /// </summary>
    [HttpPut("protection/anti-mass-mention")]
    public async Task<IActionResult> ConfigureAntiMassMention(ulong guildId,
        [FromBody] AntiMassMentionConfigRequest? request)
    {
        if (request == null)
            return BadRequest("Invalid request data");

        if (request.Enabled)
        {
            // Validate parameters
            if (request.MentionThreshold < 1)
                return BadRequest("Mention threshold must be at least 1");

            if (request.TimeWindowSeconds < 1)
                return BadRequest("Time window must be at least 1 second");

            if (request.MaxMentionsInTimeWindow < 1)
                return BadRequest("Max mentions in time window must be at least 1");

            if (request.MuteTime is < 0 or > 1440)
                return BadRequest("Mute time must be between 0 and 1440 minutes");

            await protectionService.StartAntiMassMentionAsync(
                guildId,
                request.MentionThreshold,
                request.TimeWindowSeconds,
                request.MaxMentionsInTimeWindow,
                request.IgnoreBots,
                request.Action,
                request.MuteTime,
                request.RoleId);

            return Ok(new
            {
                success = true
            });
        }
        else
        {
            var success = await protectionService.TryStopAntiMassMention(guildId);
            return Ok(new
            {
                success
            });
        }
    }

    /// <summary>
    ///     Configures anti-mass-post protection
    /// </summary>
    [HttpPut("protection/anti-mass-post")]
    public async Task<IActionResult> ConfigureAntiMassPost(ulong guildId, [FromBody] AntiMassPostConfigRequest? request)
    {
        if (request == null)
            return BadRequest("Invalid request data");

        if (request.Enabled)
        {
            if (request.ChannelThreshold is < 2 or > 20)
                return BadRequest("Channel threshold must be between 2 and 20");

            if (request.TimeWindowSeconds is < 10 or > 600)
                return BadRequest("Time window must be between 10 and 600 seconds");

            if (request.PunishDuration is < 0 or > 1440)
                return BadRequest("Punishment duration must be between 0 and 1440 minutes");

            var result = await protectionService.StartAntiMassPostAsync(
                guildId,
                request.ChannelThreshold,
                request.TimeWindowSeconds,
                request.ContentSimilarityThreshold,
                request.MinContentLength,
                request.CheckLinksOnly,
                request.CheckDuplicateContent,
                request.RequireIdenticalContent,
                request.CaseSensitive,
                request.DeleteMessages,
                request.NotifyUser,
                request.Action,
                request.PunishDuration,
                request.RoleId,
                request.IgnoreBots,
                request.MaxMessagesTracked);

            if (result == null)
                return BadRequest("Failed to start anti-mass-post protection");

            return Ok(new
            {
                success = true
            });
        }

        var success = await protectionService.TryStopAntiMassPost(guildId);
        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Configures anti-post-channel protection
    /// </summary>
    [HttpPut("protection/anti-post-channel")]
    public async Task<IActionResult> ConfigureAntiPostChannel(ulong guildId,
        [FromBody] AntiPostChannelConfigRequest? request)
    {
        if (request == null)
            return BadRequest("Invalid request data");

        if (request.Enabled)
        {
            if (request.PunishDuration is < 0 or > 1440)
                return BadRequest("Punishment duration must be between 0 and 1440 minutes");

            var result = await protectionService.StartAntiPostChannelAsync(
                guildId,
                request.Action,
                request.PunishDuration,
                request.RoleId,
                request.DeleteMessages,
                request.NotifyUser,
                request.IgnoreBots);

            if (result == null)
                return BadRequest("Failed to start anti-post-channel protection");

            return Ok(new
            {
                success = true
            });
        }

        var success = await protectionService.TryStopAntiPostChannel(guildId);
        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Adds a honeypot channel to anti-post-channel protection
    /// </summary>
    [HttpPost("protection/anti-post-channel/channels/{channelId}")]
    public async Task<IActionResult> AddAntiPostChannel(ulong guildId, ulong channelId)
    {
        var success = await protectionService.AddAntiPostChannelAsync(guildId, channelId);
        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Removes a honeypot channel from anti-post-channel protection
    /// </summary>
    [HttpDelete("protection/anti-post-channel/channels/{channelId}")]
    public async Task<IActionResult> RemoveAntiPostChannel(ulong guildId, ulong channelId)
    {
        var success = await protectionService.RemoveAntiPostChannelAsync(guildId, channelId);
        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Gets list of honeypot channels
    /// </summary>
    [HttpGet("protection/anti-post-channel/channels")]
    public async Task<IActionResult> GetAntiPostChannelChannels(ulong guildId)
    {
        var channels = await protectionService.GetAntiPostChannelChannelsAsync(guildId);
        return Ok(channels);
    }

    /// <summary>
    ///     Toggles an ignored role for anti-post-channel protection
    /// </summary>
    [HttpPost("protection/anti-post-channel/ignored-roles/{roleId}")]
    public async Task<IActionResult> ToggleAntiPostChannelIgnoredRole(ulong guildId, ulong roleId)
    {
        var added = await protectionService.ToggleAntiPostChannelIgnoredRoleAsync(guildId, roleId);
        return Ok(new
        {
            added
        });
    }

    /// <summary>
    ///     Gets list of ignored roles for anti-post-channel
    /// </summary>
    [HttpGet("protection/anti-post-channel/ignored-roles")]
    public async Task<IActionResult> GetAntiPostChannelIgnoredRoles(ulong guildId)
    {
        var roles = await protectionService.GetAntiPostChannelIgnoredRolesAsync(guildId);
        return Ok(roles);
    }

    /// <summary>
    ///     Toggles an ignored user for anti-post-channel protection
    /// </summary>
    [HttpPost("protection/anti-post-channel/ignored-users/{userId}")]
    public async Task<IActionResult> ToggleAntiPostChannelIgnoredUser(ulong guildId, ulong userId)
    {
        var added = await protectionService.ToggleAntiPostChannelIgnoredUserAsync(guildId, userId);
        return Ok(new
        {
            added
        });
    }

    /// <summary>
    ///     Gets list of ignored users for anti-post-channel
    /// </summary>
    [HttpGet("protection/anti-post-channel/ignored-users")]
    public async Task<IActionResult> GetAntiPostChannelIgnoredUsers(ulong guildId)
    {
        var users = await protectionService.GetAntiPostChannelIgnoredUsersAsync(guildId);
        return Ok(users);
    }

    /// <summary>
    ///     Gets protection statistics
    /// </summary>
    [HttpGet("protection/statistics")]
    public async Task<IActionResult> GetProtectionStatistics(ulong guildId)
    {
        await Task.CompletedTask;
        var (antiSpamStats, antiRaidStats, antiAltStats, antiMassMentionStats, _, antiMassPostStats,
                antiPostChannelStats) =
            protectionService.GetAntiStats(guildId);

        return Ok(new
        {
            antiRaid = new
            {
                enabled = antiRaidStats != null,
                usersCount = antiRaidStats?.UsersCount ?? 0,
                recentUsers = antiRaidStats?.RaidUsers.Select(u => u.Id).TakeLast(10).ToList() ?? new List<ulong>()
            },
            antiSpam = new
            {
                enabled = antiSpamStats != null,
                userCount = antiSpamStats?.UserStats.Count ?? 0,
                topOffenders = antiSpamStats?.UserStats.OrderByDescending(x => x.Value.Count)
                    .Take(10)
                    .ToDictionary(x => x.Key, x => x.Value.Count) ?? new Dictionary<ulong, int>()
            },
            antiAlt = new
            {
                enabled = antiAltStats != null, counter = antiAltStats?.Counter ?? 0
            },
            antiMassMention = new
            {
                enabled = antiMassMentionStats != null, userCount = antiMassMentionStats?.UserStats.Count ?? 0
            },
            antiMassPost = new
            {
                enabled = antiMassPostStats != null,
                userCount = antiMassPostStats?.UserStats.Count ?? 0,
                counter = antiMassPostStats?.Counter ?? 0
            },
            antiPostChannel = new
            {
                enabled = antiPostChannelStats != null, counter = antiPostChannelStats?.Counter ?? 0
            }
        });
    }

    /// <summary>
    ///     Gets self-assignable roles
    /// </summary>
    [HttpGet("self-assignable-roles")]
    public async Task<IActionResult> GetSelfAssignableRoles(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var roles = await selfAssignedRolesService.GetRoles(guild);
        return Ok(roles);
    }

    /// <summary>
    ///     Adds a self-assignable role
    /// </summary>
    [HttpPost("self-assignable-roles/{roleId}")]
    public async Task<IActionResult> AddSelfAssignableRole(ulong guildId, ulong roleId,
        [FromBody] AddSelfAssignableRoleRequest? request = null)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var role = guild.GetRole(roleId);
        if (role == null)
            return NotFound("Role not found");

        var group = request?.Group ?? 0;
        var success = await selfAssignedRolesService.AddNew(guildId, role, group);
        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Removes a self-assignable role
    /// </summary>
    [HttpDelete("self-assignable-roles/{roleId}")]
    public async Task<IActionResult> RemoveSelfAssignableRole(ulong guildId, ulong roleId)
    {
        var success = await selfAssignedRolesService.RemoveSar(guildId, roleId);
        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Gets staff role for guild
    /// </summary>
    [HttpGet("staff-role")]
    public async Task<IActionResult> GetStaffRole(ulong guildId)
    {
        var roleId = await administrationService.GetStaffRole(guildId);
        return Ok(roleId);
    }

    /// <summary>
    ///     Sets staff role for guild
    /// </summary>
    [HttpPost("staff-role")]
    public async Task<IActionResult> SetStaffRole(ulong guildId, [FromBody] ulong roleId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        await administrationService.StaffRoleSet(guild, roleId);
        return Ok();
    }

    /// <summary>
    ///     Gets member role for guild
    /// </summary>
    [HttpGet("member-role")]
    public async Task<IActionResult> GetMemberRole(ulong guildId)
    {
        var roleId = await administrationService.GetMemberRole(guildId);
        return Ok(roleId);
    }

    /// <summary>
    ///     Sets member role for guild
    /// </summary>
    [HttpPost("member-role")]
    public async Task<IActionResult> SetMemberRole(ulong guildId, [FromBody] ulong roleId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        await administrationService.MemberRoleSet(guild, roleId);
        return Ok();
    }

    /// <summary>
    ///     Gets delete message on command settings
    /// </summary>
    [HttpGet("delete-message-on-command")]
    public async Task<IActionResult> GetDeleteMessageOnCommand(ulong guildId)
    {
        var (enabled, channels) = await administrationService.GetDelMsgOnCmdData(guildId);
        return Ok(new
        {
            enabled,
            channels = channels.Select(c => new
            {
                channelId = c.ChannelId, state = c.State
            })
        });
    }

    /// <summary>
    ///     Toggles delete message on command globally
    /// </summary>
    [HttpPost("delete-message-on-command/toggle")]
    public async Task<IActionResult> ToggleDeleteMessageOnCommand(ulong guildId)
    {
        var newState = await administrationService.ToggleDeleteMessageOnCommand(guildId);
        return Ok(newState);
    }

    /// <summary>
    ///     Sets delete message on command state for specific channel
    /// </summary>
    [HttpPost("delete-message-on-command/channel")]
    public async Task<IActionResult> SetDeleteMessageOnCommandState(ulong guildId,
        [FromBody] SetChannelStateRequest request)
    {
        var state = request.State switch
        {
            "enable" => Administration.State.Enable,
            "disable" => Administration.State.Disable,
            _ => Administration.State.Inherit
        };

        await administrationService.SetDelMsgOnCmdState(guildId, request.ChannelId, state);
        return Ok();
    }

    /// <summary>
    ///     Toggles statistics opt-out
    /// </summary>
    [HttpPost("stats-opt-out/toggle")]
    public async Task<IActionResult> ToggleStatsOptOut(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var newState = await administrationService.ToggleOptOut(guild);
        return Ok(newState);
    }

    /// <summary>
    ///     Deletes statistics data for guild
    /// </summary>
    [HttpDelete("stats-data")]
    public async Task<IActionResult> DeleteStatsData(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var deleted = await administrationService.DeleteStatsData(guild);
        return Ok(deleted);
    }

    /// <summary>
    ///     Gets auto-ban roles
    /// </summary>
    [HttpGet("auto-ban-roles")]
    public async Task<IActionResult> GetAutoBanRoles(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var roles = await autoBanRoleService.GetAutoBanRoles(guildId);
        var roleData = roles.Select(roleId =>
        {
            var role = guild.GetRole(roleId);
            return new
            {
                roleId, roleName = role?.Name ?? $"Role {roleId}"
            };
        });

        return Ok(roleData);
    }

    /// <summary>
    ///     Adds auto-ban role
    /// </summary>
    [HttpPost("auto-ban-roles")]
    public async Task<IActionResult> AddAutoBanRole(ulong guildId, [FromBody] ulong roleId)
    {
        var guild = client.GetGuild(guildId);
        if (guild?.GetRole(roleId) == null)
            return NotFound("Role not found");

        var success = await autoBanRoleService.AddAutoBanRole(guildId, roleId);
        return Ok(success);
    }

    /// <summary>
    ///     Removes auto-ban role
    /// </summary>
    [HttpDelete("auto-ban-roles/{roleId}")]
    public async Task<IActionResult> RemoveAutoBanRole(ulong guildId, ulong roleId)
    {
        var success = await autoBanRoleService.RemoveAutoBanRole(guildId, roleId);
        return Ok(success);
    }

    /// <summary>
    ///     Gets voice channel roles
    /// </summary>
    [HttpGet("voice-channel-roles")]
    public async Task<IActionResult> GetVoiceChannelRoles(ulong guildId)
    {
        if (client.GetGuild(guildId) is not IGuild guild)
            return NotFound("Guild not found");

        if (!vcRoleService.VcRoles.TryGetValue(guildId, out var vcRoles))
            return Ok(Array.Empty<object>());

        var roleDataTasks = vcRoles.Select(async kvp =>
        {
            var channel = await guild.GetVoiceChannelAsync(kvp.Key);
            var role = guild.GetRole(kvp.Value.Id);
            return new
            {
                channelId = kvp.Key,
                channelName = channel?.Name ?? $"Channel {kvp.Key}",
                roleId = kvp.Value,
                roleName = role?.Name ?? $"Role {kvp.Value}"
            };
        });

        var roleData = await Task.WhenAll(roleDataTasks);
        return Ok(roleData);
    }

    /// <summary>
    ///     Adds voice channel role
    /// </summary>
    [HttpPost("voice-channel-roles")]
    public async Task<IActionResult> AddVoiceChannelRole(ulong guildId, [FromBody] VoiceChannelRoleRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild?.GetVoiceChannel(request.ChannelId) == null || guild.GetRole(request.RoleId) == null)
            return NotFound("Channel or role not found");

        await vcRoleService.AddVcRole(guildId, guild.GetRole(request.RoleId), request.ChannelId);
        return Ok();
    }

    /// <summary>
    ///     Removes voice channel role
    /// </summary>
    [HttpDelete("voice-channel-roles/{channelId}")]
    public async Task<IActionResult> RemoveVoiceChannelRole(ulong guildId, ulong channelId)
    {
        var success = await vcRoleService.RemoveVcRole(guildId, channelId);
        return Ok(success);
    }

    /// <summary>
    ///     Sets self-assignable role group name
    /// </summary>
    [HttpPost("self-assignable-roles/groups")]
    public async Task<IActionResult> SetSelfAssignableRoleGroup(ulong guildId, [FromBody] SetGroupRequest request)
    {
        var success = request.Name != null &&
                      await selfAssignedRolesService.SetNameAsync(guildId, request.Group, request.Name);
        return Ok(success);
    }

    /// <summary>
    ///     Toggles self-assignable roles exclusivity
    /// </summary>
    [HttpPost("self-assignable-roles/exclusive/toggle")]
    public async Task<IActionResult> ToggleSelfAssignableRolesExclusive(ulong guildId)
    {
        var exclusive = await selfAssignedRolesService.ToggleEsar(guildId);
        return Ok(exclusive);
    }

    /// <summary>
    ///     Sets level requirement for self-assignable role
    /// </summary>
    [HttpPost("self-assignable-roles/{roleId}/level")]
    public async Task<IActionResult> SetSelfAssignableRoleLevelRequirement(ulong guildId, ulong roleId,
        [FromBody] int level)
    {
        var guild = client.GetGuild(guildId);
        var role = guild?.GetRole(roleId);
        if (role == null)
            return NotFound("Role not found");

        var success = await selfAssignedRolesService.SetLevelReq(guildId, role, level);
        return Ok(success);
    }

    /// <summary>
    ///     Toggles auto-delete for self-assign messages
    /// </summary>
    [HttpPost("self-assignable-roles/auto-delete/toggle")]
    public async Task<IActionResult> ToggleAutoDeleteSelfAssign(ulong guildId)
    {
        var newState = await selfAssignedRolesService.ToggleAdSarm(guildId);
        return Ok(newState);
    }

    /// <summary>
    ///     Gets reaction roles ordered by Index (0-based indexing for removal)
    /// </summary>
    [HttpGet("reaction-roles")]
    public async Task<IActionResult> GetReactionRoles(ulong guildId)
    {
        var (success, reactionRoles) = await roleCommandsService.Get(guildId);
        return Ok(new
        {
            success, reactionRoles
        });
    }

    /// <summary>
    ///     Adds reaction roles to message
    /// </summary>
    [HttpPost("reaction-roles")]
    public async Task<IActionResult> AddReactionRoles(ulong guildId, [FromBody] AddReactionRolesRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        if (request.MessageId == 0)
            return BadRequest("MessageId is required");

        if (request.ChannelId == 0)
            return BadRequest("ChannelId is required");

        if (request.Roles.Count == 0)
            return BadRequest("At least one role must be provided");

        // Fetch the channel
        var channel = guild.GetTextChannel(request.ChannelId);
        if (channel == null)
            return NotFound("Channel not found");

        // Fetch the message
        var message = await channel.GetMessageAsync(request.MessageId);
        if (message == null)
            return NotFound("Message not found");

        if (message is not IUserMessage userMessage)
            return BadRequest("Message must be a user message");

        // Parse and validate emotes, then add reactions
        var reactionRoles = new List<ReactionRole>();
        foreach (var roleData in request.Roles)
        {
            // Validate role exists
            var role = guild.GetRole(roleData.RoleId);
            if (role == null)
                return NotFound($"Role {roleData.RoleId} not found");

            // Parse emote
            IEmote emote;
            try
            {
                emote = roleData.EmoteName.ToIEmote();
            }
            catch
            {
                return BadRequest($"Invalid emote: {roleData.EmoteName}");
            }

            // Add reaction to message
            try
            {
                await userMessage.AddReactionAsync(emote, new RequestOptions
                {
                    RetryMode = RetryMode.Retry502 | RetryMode.RetryRatelimit
                });
                await Task.Delay(500); // Rate limit protection
            }
            catch (HttpException ex)
            {
                return BadRequest($"Failed to add reaction {roleData.EmoteName}: {ex.Message}");
            }

            reactionRoles.Add(new ReactionRole
            {
                EmoteName = roleData.EmoteName, RoleId = roleData.RoleId
            });
        }

        // Create reaction role message with correct ChannelId
        var reactionRoleMessage = new ReactionRoleMessage
        {
            MessageId = request.MessageId,
            ChannelId = request.ChannelId,
            Exclusive = request.Exclusive,
            ReactionRoles = reactionRoles
        };

        var success = await roleCommandsService.Add(guildId, reactionRoleMessage);
        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Removes reaction role by 0-based index (from ordered list)
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="index">0-based index in the ordered list of reaction roles (0 = first item)</param>
    [HttpDelete("reaction-roles/{index}")]
    public async Task<IActionResult> RemoveReactionRole(ulong guildId, int index)
    {
        if (index < 0)
            return BadRequest("Index must be non-negative");

        await roleCommandsService.Remove(guildId, index);
        return Ok();
    }

    /// <summary>
    ///     Gets available timezones
    /// </summary>
    [HttpGet("timezones")]
    public IActionResult GetAvailableTimezones()
    {
        var timezones = TimeZoneInfo.GetSystemTimeZones()
            .OrderBy(tz => tz.BaseUtcOffset)
            .Select(tz => new
            {
                id = tz.Id,
                displayName = tz.DisplayName,
                offset = DateTimeOffset.UtcNow.ToOffset(tz.GetUtcOffset(DateTimeOffset.UtcNow)).ToString("zzz")
            });

        return Ok(timezones);
    }

    /// <summary>
    ///     Gets guild timezone
    /// </summary>
    [HttpGet("timezone")]
    public async Task<IActionResult> GetGuildTimezone(ulong guildId)
    {
        await Task.CompletedTask;
        var timezone = timezoneService.GetTimeZoneOrUtc(guildId);
        return Ok(timezone.Id);
    }

    /// <summary>
    ///     Sets guild timezone
    /// </summary>
    [HttpPost("timezone")]
    public async Task<IActionResult> SetGuildTimezone(ulong guildId, [FromBody] SetTimezoneRequest request)
    {
        try
        {
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(request.TimezoneId);
            await timezoneService.SetTimeZone(guildId, timezone);
            return Ok();
        }
        catch
        {
            return BadRequest("Invalid timezone ID");
        }
    }

    /// <summary>
    ///     Gets permission overrides
    /// </summary>
    [HttpGet("permission-overrides")]
    public async Task<IActionResult> GetPermissionOverrides(ulong guildId)
    {
        var overrides = await permOverrideService.GetAllOverrides(guildId);
        var result = overrides.Select(o => new
        {
            command = o.Command, permission = ((GuildPermission)o.Perm).ToString()
        });

        return Ok(result);
    }

    /// <summary>
    ///     Adds permission override
    /// </summary>
    [HttpPost("permission-overrides")]
    public async Task<IActionResult> AddPermissionOverride(ulong guildId, [FromBody] PermissionOverrideRequest request)
    {
        if (!Enum.TryParse<GuildPermission>(request.Permission, out var permission))
            return BadRequest("Invalid permission");

        var result = await permOverrideService.AddOverride(guildId, request.Command, permission);
        return Ok(result);
    }

    /// <summary>
    ///     Removes permission override
    /// </summary>
    [HttpDelete("permission-overrides/{command}")]
    public async Task<IActionResult> RemovePermissionOverride(ulong guildId, string command)
    {
        await permOverrideService.RemoveOverride(guildId, command);
        return Ok();
    }

    /// <summary>
    ///     Clears all permission overrides
    /// </summary>
    [HttpDelete("permission-overrides")]
    public async Task<IActionResult> ClearAllPermissionOverrides(ulong guildId)
    {
        await permOverrideService.ClearAllOverrides(guildId);
        return Ok();
    }

    /// <summary>
    ///     Gets game voice channel
    /// </summary>
    [HttpGet("game-voice-channel")]
    public async Task<IActionResult> GetGameVoiceChannel(ulong guildId)
    {
        var guildConfig = await guildSettingsService.GetGuildConfig(guildId);
        var channelId = guildConfig.GameVoiceChannel == 0 ? null : guildConfig.GameVoiceChannel;
        return Ok(channelId);
    }

    /// <summary>
    ///     Toggles game voice channel
    /// </summary>
    [HttpPost("game-voice-channel/toggle")]
    public async Task<IActionResult> ToggleGameVoiceChannel(ulong guildId,
        [FromBody] ToggleGameVoiceChannelRequest request)
    {
        var channelId = await gameVoiceChannelService.ToggleGameVoiceChannel(guildId, request.ChannelId);
        return Ok(channelId);
    }

    /// <summary>
    ///     Gets server recovery status
    /// </summary>
    [HttpGet("server-recovery")]
    public async Task<IActionResult> GetServerRecoveryStatus(ulong guildId)
    {
        var (isSetup, store) = await serverRecoveryService.RecoveryIsSetup(guildId);
        return Ok(new
        {
            isSetup, recoveryKey = store?.RecoveryKey
        });
    }

    /// <summary>
    ///     Sets up server recovery
    /// </summary>
    [HttpPost("server-recovery")]
    public async Task<IActionResult> SetupServerRecovery(ulong guildId, [FromBody] ServerRecoveryRequest request)
    {
        await serverRecoveryService.SetupRecovery(guildId, request.RecoveryKey, request.TwoFactorKey);
        return Ok();
    }

    /// <summary>
    ///     Clears server recovery
    /// </summary>
    [HttpDelete("server-recovery")]
    public async Task<IActionResult> ClearServerRecovery(ulong guildId)
    {
        var (isSetup, store) = await serverRecoveryService.RecoveryIsSetup(guildId);
        if (isSetup && store != null)
        {
            await serverRecoveryService.ClearRecoverySetup(store);
        }

        return Ok();
    }

    /// <summary>
    ///     Gets ban message
    /// </summary>
    [HttpGet("ban-message")]
    public async Task<IActionResult> GetBanMessage(ulong guildId)
    {
        var banMsg = await userPunishService.GetBanTemplate(guildId);
        return Ok(banMsg);
    }

    /// <summary>
    ///     Sets ban message
    /// </summary>
    [HttpPost("ban-message")]
    public async Task<IActionResult> SetBanMessage(ulong guildId, [FromBody] SetBanMessageRequest request)
    {
        await userPunishService.SetBanTemplate(guildId, request.Message);
        return Ok();
    }

    /// <summary>
    ///     Mass ban users
    /// </summary>
    [HttpPost("mass-ban")]
    public async Task<IActionResult> MassBan(ulong guildId, [FromBody] MassBanRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var succeeded = 0;
        var failed = 0;

        foreach (var userId in request.UserIds)
        {
            try
            {
                await guild.AddBanAsync(userId, reason: request.Reason);
                succeeded++;
            }
            catch
            {
                failed++;
            }
        }

        return Ok(new
        {
            succeeded, failed
        });
    }

    /// <summary>
    ///     Mass rename users
    /// </summary>
    [HttpPost("mass-rename")]
    public async Task<IActionResult> MassRename(ulong guildId, [FromBody] MassRenameRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var renamed = 0;
        foreach (var user in guild.Users)
        {
            try
            {
                var newNickname = request.Pattern.Replace("{username}", user.Username);
                await user.ModifyAsync(u => u.Nickname = newNickname);
                renamed++;
            }
            catch
            {
                // Ignore failures
            }
        }

        return Ok(new
        {
            renamed
        });
    }

    /// <summary>
    ///     Prune users
    /// </summary>
    [HttpPost("prune")]
    public async Task<IActionResult> PruneUsers(ulong guildId, [FromBody] PruneRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var pruned = await guild.PruneUsersAsync(request.Days);
        return Ok(new
        {
            pruned
        });
    }

    /// <summary>
    ///     Prune messages to specific message
    /// </summary>
    [HttpPost("prune-to")]
    public async Task<IActionResult> PruneToMessage(ulong guildId, [FromBody] PruneToMessageRequest request)
    {
        var guild = client.GetGuild(guildId);
        var channel = guild?.GetTextChannel(request.ChannelId);
        if (channel == null)
            return NotFound("Channel not found");

        var messages = await channel.GetMessagesAsync(request.MessageId, Direction.After).FlattenAsync();
        var deleted = 0;

        foreach (var message in messages)
        {
            try
            {
                await message.DeleteAsync();
                deleted++;
            }
            catch
            {
                // Ignore failures
            }
        }

        return Ok(new
        {
            deleted
        });
    }

    #region Permissions Management

    /// <summary>
    ///     Gets permissions for a guild
    /// </summary>
    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions(ulong guildId)
    {
        var perms = await permissionService.GetCacheFor(guildId);
        return Ok(perms);
    }

    /// <summary>
    ///     Adds a new permission
    /// </summary>
    [HttpPost("permissions")]
    public async Task<IActionResult> AddPermission(ulong guildId, [FromBody] Permission1 permission)
    {
        await permissionService.AddPermissions(guildId, permission);
        return Ok();
    }

    /// <summary>
    ///     Removes a permission by index
    /// </summary>
    [HttpDelete("permissions/{index}")]
    public async Task<IActionResult> RemovePermission(ulong guildId, int index)
    {
        await permissionService.RemovePerm(guildId, index);
        return Ok();
    }

    /// <summary>
    ///     Moves a permission to a new position
    /// </summary>
    [HttpPost("permissions/move")]
    public async Task<IActionResult> MovePermission(ulong guildId, [FromBody] MovePermRequest request)
    {
        await permissionService.UnsafeMovePerm(guildId, request.From, request.To);
        return Ok();
    }

    /// <summary>
    ///     Resets all permissions for a guild
    /// </summary>
    [HttpPost("permissions/reset")]
    public async Task<IActionResult> ResetPermissions(ulong guildId)
    {
        await permissionService.Reset(guildId);
        return Ok();
    }

    /// <summary>
    ///     Sets verbose mode for permissions
    /// </summary>
    [HttpPost("permissions/verbose")]
    public async Task<IActionResult> SetVerbose(ulong guildId, [FromBody] JsonElement request)
    {
        var verbose = request.GetProperty("verbose").GetBoolean();

        await using var db = await dbFactory.CreateConnectionAsync();

        var config = await db.GuildConfigs.Where(gc => gc.GuildId == guildId).FirstOrDefaultAsync();
        if (config == null)
            return NotFound("Guild configuration not found");

        await db.GuildConfigs
            .Where(gc => gc.GuildId == guildId)
            .Set(gc => gc.VerbosePermissions, verbose)
            .UpdateAsync();

        // Update cache
        var permissions = await db.Permissions1
            .Where(p => p.GuildId == guildId)
            .ToListAsync();

        permissionService.UpdateCache(guildId, permissions, config);

        return Ok();
    }

    /// <summary>
    ///     Sets the permission role for the guild
    /// </summary>
    [HttpPost("permissions/role")]
    public async Task<IActionResult> SetPermissionRole(ulong guildId, [FromBody] string roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var rowsAffected = await db.GuildConfigs
            .Where(gc => gc.GuildId == guildId)
            .Set(gc => gc.PermissionRole, roleId)
            .UpdateAsync();

        if (rowsAffected == 0)
            return NotFound($"Configuration for guild {guildId} not found.");

        var config = await db.GuildConfigs.Where(gc => gc.GuildId == guildId).FirstOrDefaultAsync();
        var permissions = await db.Permissions1
            .Where(p => p.GuildId == guildId).ToListAsync();

        permissionService.UpdateCache(guildId, permissions, config);

        return Ok();
    }

    /// <summary>
    ///     Gets all commands and modules
    /// </summary>
    [HttpGet("commands")]
    public async Task<IActionResult> GetCommandsAndModules()
    {
        await Task.CompletedTask;

        var modules = commandService.Modules;
        var moduleList = modules
            .Select(module =>
            {
                var moduleName = module.IsSubmodule ? module.Parent!.Name : module.Name;
                var commands = module.Commands.OrderByDescending(x => x.Name)
                    .Select(cmd =>
                    {
                        var userPerm =
                            cmd.Preconditions.FirstOrDefault(ca => ca is UserPermAttribute) as UserPermAttribute;
                        var botPerm =
                            cmd.Preconditions.FirstOrDefault(ca => ca is BotPermAttribute) as BotPermAttribute;
                        var isDragon =
                            cmd.Preconditions.FirstOrDefault(ca => ca is RequireDragonAttribute) as
                                RequireDragonAttribute;

                        return new Command
                        {
                            BotVersion = StatsService.BotVersion,
                            CommandName = cmd.Aliases.Any() ? cmd.Aliases[0] : cmd.Name,
                            Description = cmd.Summary ?? "No description available",
                            Example = cmd.Remarks?.Split('\n').ToList() ?? [],
                            GuildUserPermissions = userPerm?.UserPermissionAttribute.GuildPermission?.ToString() ?? "",
                            ChannelUserPermissions =
                                userPerm?.UserPermissionAttribute.ChannelPermission?.ToString() ?? "",
                            GuildBotPermissions = botPerm?.GuildPermission?.ToString() ?? "",
                            ChannelBotPermissions = botPerm?.ChannelPermission?.ToString() ?? "",
                            IsDragon = isDragon != null
                        };
                    })
                    .ToList();

                return new Module(commands, moduleName);
            })
            .ToList();

        return Ok(moduleList);
    }

    #endregion

    #region Command Cooldowns

    /// <summary>
    ///     Gets command cooldowns for the guild
    /// </summary>
    [HttpGet("command-cooldowns")]
    public async Task<IActionResult> GetCommandCooldowns(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var cooldowns = await db.CommandCooldowns
            .Where(cc => cc.GuildId == guildId)
            .ToListAsync();

        return Ok(cooldowns);
    }

    /// <summary>
    ///     Sets command cooldown
    /// </summary>
    [HttpPut("command-cooldowns/{commandName}")]
    public async Task<IActionResult> SetCommandCooldown(ulong guildId, string commandName, [FromBody] int seconds)
    {
        if (seconds is < 0 or > 90000)
            return BadRequest("Cooldown must be between 0 and 90000 seconds");

        await using var db = await dbFactory.CreateConnectionAsync();

        // Remove existing cooldown
        await db.CommandCooldowns
            .Where(cc => cc.GuildId == guildId && cc.CommandName == commandName)
            .DeleteAsync();

        if (seconds > 0)
        {
            // Add new cooldown
            await db.InsertAsync(new CommandCooldown
            {
                GuildId = guildId, CommandName = commandName, Seconds = seconds
            });
        }

        // Clear active cooldowns
        if (cmdCdService.ActiveCooldowns.TryGetValue(guildId, out var activeCds))
        {
            activeCds.RemoveWhere(ac => ac.Command == commandName);
        }

        return Ok();
    }

    /// <summary>
    ///     Removes command cooldown
    /// </summary>
    [HttpDelete("command-cooldowns/{commandName}")]
    public async Task<IActionResult> RemoveCommandCooldown(ulong guildId, string commandName)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        await db.CommandCooldowns
            .Where(cc => cc.GuildId == guildId && cc.CommandName == commandName)
            .DeleteAsync();

        // Clear active cooldowns
        if (cmdCdService.ActiveCooldowns.TryGetValue(guildId, out var activeCds))
        {
            activeCds.RemoveWhere(ac => ac.Command == commandName);
        }

        return Ok();
    }

    #endregion
}