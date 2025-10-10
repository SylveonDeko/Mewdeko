using Mewdeko.Controllers.Common.Protection;
using Mewdeko.Modules.Administration.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for comprehensive protection system management
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class ProtectionController(ProtectionService protectionService) : Controller
{
    /// <summary>
    ///     Gets comprehensive protection status for all protection types
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetProtectionStatus(ulong guildId)
    {
        await Task.CompletedTask;

        var (antiSpamStats, antiRaidStats, antiAltStats, antiMassMentionStats, antiPatternStats, antiMassPostStats,
                antiPostChannelStats) =
            protectionService.GetAntiStats(guildId);

        return Ok(new
        {
            antiRaid = new
            {
                enabled = antiRaidStats != null,
                userThreshold = antiRaidStats?.AntiRaidSettings?.UserThreshold ?? 0,
                seconds = antiRaidStats?.AntiRaidSettings?.Seconds ?? 0,
                action = antiRaidStats?.AntiRaidSettings?.Action ?? 0,
                punishDuration = antiRaidStats?.AntiRaidSettings?.PunishDuration ?? 0,
                usersCount = antiRaidStats?.UsersCount ?? 0
            },
            antiSpam = new
            {
                enabled = antiSpamStats != null,
                messageThreshold = antiSpamStats?.AntiSpamSettings?.MessageThreshold ?? 0,
                action = antiSpamStats?.AntiSpamSettings?.Action ?? 0,
                muteTime = antiSpamStats?.AntiSpamSettings?.MuteTime ?? 0,
                roleId = antiSpamStats?.AntiSpamSettings?.RoleId ?? 0,
                ignoredChannels = new List<ulong>(), // Ignored channels are retrieved separately
                userCount = antiSpamStats?.UserStats?.Count ?? 0
            },
            antiAlt = new
            {
                enabled = antiAltStats != null,
                minAge = antiAltStats?.MinAge ?? "",
                minAgeMinutes = int.TryParse(antiAltStats?.MinAge ?? "0", out var minAge) ? minAge : 0,
                action = antiAltStats?.Action ?? 0,
                actionDuration = antiAltStats?.ActionDurationMinutes ?? 0,
                roleId = antiAltStats?.RoleId ?? 0,
                counter = antiAltStats?.Counter ?? 0
            },
            antiMassMention = new
            {
                enabled = antiMassMentionStats != null,
                mentionThreshold = antiMassMentionStats?.AntiMassMentionSettings?.MentionThreshold ?? 0,
                maxMentionsInTimeWindow = antiMassMentionStats?.AntiMassMentionSettings?.MaxMentionsInTimeWindow ?? 0,
                timeWindowSeconds = antiMassMentionStats?.AntiMassMentionSettings?.TimeWindowSeconds ?? 0,
                action = antiMassMentionStats?.AntiMassMentionSettings?.Action ?? 0,
                muteTime = antiMassMentionStats?.AntiMassMentionSettings?.MuteTime ?? 0,
                roleId = antiMassMentionStats?.AntiMassMentionSettings?.RoleId ?? 0,
                ignoreBots = antiMassMentionStats?.AntiMassMentionSettings?.IgnoreBots ?? false,
                userCount = antiMassMentionStats?.UserStats?.Count ?? 0
            },
            antiPattern = new
            {
                enabled = antiPatternStats != null,
                action = antiPatternStats?.AntiPatternSettings?.Action ?? 0,
                punishDuration = antiPatternStats?.AntiPatternSettings?.PunishDuration ?? 0,
                roleId = antiPatternStats?.AntiPatternSettings?.RoleId ?? 0,
                checkAccountAge = antiPatternStats?.AntiPatternSettings?.CheckAccountAge ?? false,
                maxAccountAgeMonths = antiPatternStats?.AntiPatternSettings?.MaxAccountAgeMonths ?? 6,
                checkJoinTiming = antiPatternStats?.AntiPatternSettings?.CheckJoinTiming ?? false,
                maxJoinHours = antiPatternStats?.AntiPatternSettings?.MaxJoinHours ?? 48.0,
                checkBatchCreation = antiPatternStats?.AntiPatternSettings?.CheckBatchCreation ?? false,
                checkOfflineStatus = antiPatternStats?.AntiPatternSettings?.CheckOfflineStatus ?? false,
                checkNewAccounts = antiPatternStats?.AntiPatternSettings?.CheckNewAccounts ?? false,
                newAccountDays = antiPatternStats?.AntiPatternSettings?.NewAccountDays ?? 7,
                minimumScore = antiPatternStats?.AntiPatternSettings?.MinimumScore ?? 15,
                patternCount = antiPatternStats?.AntiPatternSettings?.AntiPatternPatterns?.Count() ?? 0,
                counter = antiPatternStats?.Counter ?? 0
            },
            antiMassPost = new
            {
                enabled = antiMassPostStats != null,
                action = antiMassPostStats?.AntiMassPostSettings?.Action ?? 0,
                channelThreshold = antiMassPostStats?.AntiMassPostSettings?.ChannelThreshold ?? 3,
                timeWindowSeconds = antiMassPostStats?.AntiMassPostSettings?.TimeWindowSeconds ?? 60,
                contentSimilarityThreshold = antiMassPostStats?.AntiMassPostSettings?.ContentSimilarityThreshold ?? 0.8,
                minContentLength = antiMassPostStats?.AntiMassPostSettings?.MinContentLength ?? 20,
                checkLinksOnly = antiMassPostStats?.AntiMassPostSettings?.CheckLinksOnly ?? true,
                checkDuplicateContent = antiMassPostStats?.AntiMassPostSettings?.CheckDuplicateContent ?? true,
                requireIdenticalContent = antiMassPostStats?.AntiMassPostSettings?.RequireIdenticalContent ?? false,
                caseSensitive = antiMassPostStats?.AntiMassPostSettings?.CaseSensitive ?? false,
                deleteMessages = antiMassPostStats?.AntiMassPostSettings?.DeleteMessages ?? true,
                notifyUser = antiMassPostStats?.AntiMassPostSettings?.NotifyUser ?? true,
                punishDuration = antiMassPostStats?.AntiMassPostSettings?.PunishDuration ?? 0,
                roleId = antiMassPostStats?.AntiMassPostSettings?.RoleId ?? 0,
                ignoreBots = antiMassPostStats?.AntiMassPostSettings?.IgnoreBots ?? true,
                maxMessagesTracked = antiMassPostStats?.AntiMassPostSettings?.MaxMessagesTracked ?? 50,
                userCount = antiMassPostStats?.UserStats?.Count ?? 0,
                counter = antiMassPostStats?.Counter ?? 0
            },
            antiPostChannel = new
            {
                enabled = antiPostChannelStats != null,
                action = antiPostChannelStats?.AntiPostChannelSettings?.Action ?? 0,
                deleteMessages = antiPostChannelStats?.AntiPostChannelSettings?.DeleteMessages ?? true,
                notifyUser = antiPostChannelStats?.AntiPostChannelSettings?.NotifyUser ?? true,
                punishDuration = antiPostChannelStats?.AntiPostChannelSettings?.PunishDuration ?? 0,
                roleId = antiPostChannelStats?.AntiPostChannelSettings?.RoleId ?? 0,
                ignoreBots = antiPostChannelStats?.AntiPostChannelSettings?.IgnoreBots ?? true,
                channelCount = antiPostChannelStats?.AntiPostChannelSettings?.AntiPostChannelChannels?.Count() ?? 0,
                counter = antiPostChannelStats?.Counter ?? 0
            }
        });
    }

    /// <summary>
    ///     Configures anti-raid protection
    /// </summary>
    [HttpPut("anti-raid")]
    public async Task<IActionResult> ConfigureAntiRaid(ulong guildId, [FromBody] AntiRaidConfigRequest request)
    {
        if (request.Enabled)
        {
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
    [HttpPut("anti-spam")]
    public async Task<IActionResult> ConfigureAntiSpam(ulong guildId, [FromBody] AntiSpamConfigRequest request)
    {
        if (request.Enabled)
        {
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
    ///     Manages ignored channels for anti-spam
    /// </summary>
    [HttpPost("anti-spam/ignored-channels/{channelId}")]
    public async Task<IActionResult> ToggleAntiSpamIgnoredChannel(ulong guildId, ulong channelId)
    {
        var added = await protectionService.AntiSpamIgnoreAsync(guildId, channelId);

        return Ok(new
        {
            added
            // Note: To get the complete list of ignored channels, query the database directly
        });
    }

    /// <summary>
    ///     Configures anti-alt protection
    /// </summary>
    [HttpPut("anti-alt")]
    public async Task<IActionResult> ConfigureAntiAlt(ulong guildId, [FromBody] AntiAltConfigRequest request)
    {
        if (request.Enabled)
        {
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
    [HttpPut("anti-mass-mention")]
    public async Task<IActionResult> ConfigureAntiMassMention(ulong guildId,
        [FromBody] AntiMassMentionConfigRequest request)
    {
        if (request.Enabled)
        {
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
    ///     Gets protection statistics and recent triggers
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetProtectionStatistics(ulong guildId)
    {
        await Task.CompletedTask;

        var (antiSpamStats, antiRaidStats, antiAltStats, antiMassMentionStats, antiPatternStats, antiMassPostStats,
                antiPostChannelStats) =
            protectionService.GetAntiStats(guildId);

        return Ok(new
        {
            antiRaid = new
            {
                enabled = antiRaidStats != null,
                usersCount = antiRaidStats?.UsersCount ?? 0,
                recentUsers = antiRaidStats?.RaidUsers?.Select(u => u.Id).TakeLast(10).ToList() ?? new List<ulong>()
            },
            antiSpam = new
            {
                enabled = antiSpamStats != null,
                userCount = antiSpamStats?.UserStats?.Count ?? 0,
                topOffenders = antiSpamStats?.UserStats?
                    .OrderByDescending(x => x.Value.Count)
                    .Take(10)
                    .ToDictionary(x => x.Key, x => x.Value.Count) ?? new Dictionary<ulong, int>()
            },
            antiAlt = new
            {
                enabled = antiAltStats != null, counter = antiAltStats?.Counter ?? 0
            },
            antiMassMention = new
            {
                enabled = antiMassMentionStats != null, userCount = antiMassMentionStats?.UserStats?.Count ?? 0
            },
            antiPattern = new
            {
                enabled = antiPatternStats != null, counter = antiPatternStats?.Counter ?? 0
            },
            antiMassPost = new
            {
                enabled = antiMassPostStats != null,
                userCount = antiMassPostStats?.UserStats?.Count ?? 0,
                counter = antiMassPostStats?.Counter ?? 0
            },
            antiPostChannel = new
            {
                enabled = antiPostChannelStats != null, counter = antiPostChannelStats?.Counter ?? 0
            }
        });
    }

    /// <summary>
    ///     Configures anti-pattern protection
    /// </summary>
    [HttpPut("anti-pattern")]
    public async Task<IActionResult> ConfigureAntiPattern(ulong guildId, [FromBody] AntiPatternConfigRequest request)
    {
        if (request.Enabled)
        {
            var result = await protectionService.StartAntiPatternAsync(
                guildId,
                request.Action,
                request.PunishDuration,
                request.RoleId,
                request.CheckAccountAge,
                request.MaxAccountAgeMonths,
                request.CheckJoinTiming,
                request.MaxJoinHours,
                request.CheckBatchCreation,
                request.CheckOfflineStatus,
                request.CheckNewAccounts,
                request.NewAccountDays,
                request.MinimumScore);

            if (result == null)
                return BadRequest("Failed to start anti-pattern protection");

            return Ok(new
            {
                success = true, settings = result
            });
        }

        var success = await protectionService.TryStopAntiPattern(guildId);
        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Adds a pattern to anti-pattern protection
    /// </summary>
    [HttpPost("anti-pattern/patterns")]
    public async Task<IActionResult> AddPattern(ulong guildId, [FromBody] AddPatternRequest request)
    {
        var success = await protectionService.AddPatternAsync(
            guildId,
            request.Pattern,
            request.Name,
            request.CheckUsername,
            request.CheckDisplayName);

        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Removes a pattern from anti-pattern protection
    /// </summary>
    [HttpDelete("anti-pattern/patterns/{patternId}")]
    public async Task<IActionResult> RemovePattern(ulong guildId, int patternId)
    {
        var success = await protectionService.RemovePatternAsync(guildId, patternId);
        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Updates anti-pattern configuration
    /// </summary>
    [HttpPatch("anti-pattern/config")]
    public async Task<IActionResult> UpdateAntiPatternConfig(ulong guildId,
        [FromBody] UpdateAntiPatternConfigRequest request)
    {
        var success = await protectionService.UpdateAntiPatternConfigAsync(
            guildId,
            request.CheckAccountAge,
            request.MaxAccountAgeMonths,
            request.CheckJoinTiming,
            request.MaxJoinHours,
            request.CheckBatchCreation,
            request.CheckOfflineStatus,
            request.CheckNewAccounts,
            request.NewAccountDays,
            request.MinimumScore);

        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Gets all anti-pattern patterns for a guild
    /// </summary>
    [HttpGet("anti-pattern/patterns")]
    public async Task<IActionResult> GetAntiPatternPatterns(ulong guildId)
    {
        var patterns = await protectionService.GetAntiPatternPatternsAsync(guildId);
        return Ok(patterns);
    }
}