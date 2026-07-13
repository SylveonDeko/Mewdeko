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
public class ProtectionController(
    ProtectionService protectionService,
    ImageHashingService imageHashing,
    IDashboardAuditContext auditContext) : Controller
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
        var imageHashStats = protectionService.GetAntiImageHashStats(guildId);

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
            },
            antiImageHash = new
            {
                enabled = imageHashStats != null,
                action = imageHashStats?.AntiImageHashSettings?.Action ?? 2,
                punishDuration = imageHashStats?.AntiImageHashSettings?.PunishDuration ?? 0,
                roleId = imageHashStats?.AntiImageHashSettings?.RoleId ?? 0,
                hashThreshold = imageHashStats?.AntiImageHashSettings?.HashThreshold ?? 31,
                deleteMessages = imageHashStats?.AntiImageHashSettings?.DeleteMessages ?? true,
                notifyUser = imageHashStats?.AntiImageHashSettings?.NotifyUser ?? true,
                ignoreBots = imageHashStats?.AntiImageHashSettings?.IgnoreBots ?? true,
                checkEmbeds = imageHashStats?.AntiImageHashSettings?.CheckEmbeds ?? true,
                checkBorders = imageHashStats?.AntiImageHashSettings?.CheckBorders ?? true,
                usePresetList = imageHashStats?.AntiImageHashSettings?.UsePresetList ?? false,
                presetTriggers = imageHashStats?.AntiImageHashSettings?.PresetTriggers ?? 0,
                presetCount = protectionService.PresetScamImageCount,
                maxImageSizeMb = imageHashStats?.AntiImageHashSettings?.MaxImageSizeMb ?? 8,
                hashCount = imageHashStats?.Hashes.Count ?? 0,
                ignoredRoles = imageHashStats?.IgnoredRoles.ToList() ?? [],
                ignoredChannels = imageHashStats?.IgnoredChannels.ToList() ?? [],
                counter = imageHashStats?.Counter ?? 0
            }
        });
    }

    /// <summary>
    ///     Configures anti-raid protection
    /// </summary>
    [HttpPut("anti-raid")]
    public async Task<IActionResult> ConfigureAntiRaid(ulong guildId, [FromBody] AntiRaidConfigRequest request)
    {
        auditContext.RecordBefore(protectionService.GetAntiStats(guildId));
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
        auditContext.RecordBefore(protectionService.GetAntiStats(guildId));
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
        auditContext.RecordBefore(protectionService.GetAntiStats(guildId));
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
        auditContext.RecordBefore(protectionService.GetAntiStats(guildId));
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
        auditContext.RecordBefore(protectionService.GetAntiStats(guildId));
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
        auditContext.RecordBefore(protectionService.GetAntiStats(guildId));
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
        auditContext.RecordBefore(protectionService.GetAntiStats(guildId));
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
        auditContext.RecordBefore(protectionService.GetAntiStats(guildId));
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
        auditContext.RecordBefore(protectionService.GetAntiStats(guildId));
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

    /// <summary>
    ///     Configures anti-image-hash protection
    /// </summary>
    [HttpPut("anti-image-hash")]
    public async Task<IActionResult> ConfigureAntiImageHash(ulong guildId,
        [FromBody] AntiImageHashConfigRequest? request)
    {
        if (request == null)
            return BadRequest("Invalid request data");

        auditContext.RecordBefore(protectionService.GetAntiImageHashStats(guildId));

        if (!request.Enabled)
        {
            var stopped = await protectionService.TryStopAntiImageHash(guildId);
            return Ok(new
            {
                success = stopped
            });
        }

        if (request.HashThreshold is < 0 or > 64)
            return BadRequest("Hash threshold must be between 0 and 64 bits");

        if (request.PunishDuration is < 0 or > 1440)
            return BadRequest("Punishment duration must be between 0 and 1440 minutes");

        if (request.MaxImageSizeMb is < 1 or > 32)
            return BadRequest("Max image size must be between 1 and 32 megabytes");

        var result = await protectionService.StartAntiImageHashAsync(
            guildId,
            request.Action,
            request.PunishDuration,
            request.RoleId,
            request.HashThreshold,
            request.DeleteMessages,
            request.NotifyUser,
            request.IgnoreBots,
            request.CheckEmbeds,
            request.CheckBorders,
            request.UsePresetList,
            request.MaxImageSizeMb);

        if (result == null)
            return BadRequest("Failed to start anti-image-hash protection");

        return Ok(new
        {
            success = true
        });
    }

    /// <summary>
    ///     Gets the blocked image list for a guild, including how many times each image has been caught
    /// </summary>
    [HttpGet("anti-image-hash/hashes")]
    public async Task<IActionResult> GetBannedImageHashes(ulong guildId)
    {
        var hashes = await protectionService.GetBannedImageHashesAsync(guildId);
        return Ok(hashes);
    }

    /// <summary>
    ///     Adds an image to the blocked image list, from a precomputed hash, an image URL, or an uploaded image
    /// </summary>
    [HttpPost("anti-image-hash/hashes")]
    public async Task<IActionResult> AddBannedImageHash(ulong guildId, [FromBody] AddBannedImageHashRequest? request)
    {
        if (request == null)
            return BadRequest("Invalid request data");

        auditContext.RecordBefore(protectionService.GetAntiImageHashStats(guildId));

        ImageHashSet? hashSet;

        if (!string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            if (!TryDecodeBase64Image(request.ImageBase64, out var bytes))
                return BadRequest("Uploaded image is not valid base64");

            hashSet = imageHashing.ComputeHashSet(bytes);
        }
        else if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            hashSet = await imageHashing.ComputeHashSetFromUrlAsync(request.ImageUrl);
        }
        else if (!string.IsNullOrWhiteSpace(request.Hash))
        {
            // A bare hash has no variants, so the entry only matches the full frame: no crop or mirror resistance.
            hashSet = new ImageHashSet(request.Hash, 100, []);
        }
        else
        {
            return BadRequest("Provide an image URL, an uploaded image, or a hash");
        }

        if (hashSet == null)
            return BadRequest("Could not read that image. Supported formats are png, jpeg, webp, gif, and bmp");

        if (hashSet.Quality < ImageHashingService.MinReliableQuality)
        {
            return BadRequest(
                "That image is too plain to identify reliably, so blocking it would catch unrelated images too.");
        }

        var entry = await protectionService.AddBannedImageHashAsync(
            guildId,
            hashSet,
            request.Name,
            request.ImageUrl,
            request.AddedBy,
            request.Action,
            request.PunishDuration,
            request.RoleId);

        if (entry == null)
            return Conflict("That hash is invalid or already blocked");

        return Ok(entry);
    }

    /// <summary>
    ///     Removes an image from the blocked image list
    /// </summary>
    [HttpDelete("anti-image-hash/hashes/{hashId:int}")]
    public async Task<IActionResult> RemoveBannedImageHash(ulong guildId, int hashId)
    {
        auditContext.RecordBefore(protectionService.GetAntiImageHashStats(guildId));
        var success = await protectionService.RemoveBannedImageHashAsync(guildId, hashId);
        return Ok(new
        {
            success
        });
    }

    /// <summary>
    ///     Computes the perceptual hash of an uploaded image or an image URL without blocking it. Used by the dashboard so a
    ///     hash can be previewed before it is added.
    /// </summary>
    [HttpPost("anti-image-hash/compute")]
    public async Task<IActionResult> ComputeImageHash(ulong guildId, [FromBody] AddBannedImageHashRequest? request)
    {
        if (request == null)
            return BadRequest("Invalid request data");

        ImageHashSet? hashSet;

        if (!string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            if (!TryDecodeBase64Image(request.ImageBase64, out var bytes))
                return BadRequest("Uploaded image is not valid base64");

            hashSet = imageHashing.ComputeHashSet(bytes);
        }
        else if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            hashSet = await imageHashing.ComputeHashSetFromUrlAsync(request.ImageUrl);
        }
        else
        {
            return BadRequest("Provide an image URL or an uploaded image");
        }

        if (hashSet == null)
            return BadRequest("Could not read that image. Supported formats are png, jpeg, webp, gif, and bmp");

        return Ok(new
        {
            hash = hashSet.Hash,
            quality = hashSet.Quality,
            reliable = hashSet.Quality >= ImageHashingService.MinReliableQuality,
            minQuality = ImageHashingService.MinReliableQuality
        });
    }

    /// <summary>
    ///     Turns the shipped list of known scam images on or off for a guild
    /// </summary>
    [HttpPost("anti-image-hash/preset/{enabled:bool}")]
    public async Task<IActionResult> SetPresetScamImages(ulong guildId, bool enabled)
    {
        auditContext.RecordBefore(protectionService.GetAntiImageHashStats(guildId));
        var success = await protectionService.SetPresetScamImagesAsync(guildId, enabled);

        return Ok(new
        {
            success, presetCount = protectionService.PresetScamImageCount
        });
    }

    /// <summary>
    ///     Toggles a role as exempt from anti-image-hash protection
    /// </summary>
    [HttpPost("anti-image-hash/ignored-roles/{roleId}")]
    public async Task<IActionResult> ToggleAntiImageHashIgnoredRole(ulong guildId, ulong roleId)
    {
        auditContext.RecordBefore(protectionService.GetAntiImageHashStats(guildId));
        var added = await protectionService.ToggleAntiImageHashIgnoredRoleAsync(guildId, roleId);
        return Ok(new
        {
            added
        });
    }

    /// <summary>
    ///     Toggles a channel as exempt from anti-image-hash protection
    /// </summary>
    [HttpPost("anti-image-hash/ignored-channels/{channelId}")]
    public async Task<IActionResult> ToggleAntiImageHashIgnoredChannel(ulong guildId, ulong channelId)
    {
        auditContext.RecordBefore(protectionService.GetAntiImageHashStats(guildId));
        var added = await protectionService.ToggleAntiImageHashIgnoredChannelAsync(guildId, channelId);
        return Ok(new
        {
            added
        });
    }

    /// <summary>
    ///     Decodes an uploaded image, accepting both a bare base64 payload and a data URL.
    /// </summary>
    private static bool TryDecodeBase64Image(string input, out byte[] bytes)
    {
        var payload = input;
        var comma = payload.IndexOf(',');

        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
            payload = payload[(comma + 1)..];

        bytes = [];

        try
        {
            bytes = Convert.FromBase64String(payload);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}