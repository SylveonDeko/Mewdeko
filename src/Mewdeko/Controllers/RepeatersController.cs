using Mewdeko.Controllers.Common.Repeaters;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API controller for managing repeater/sticky message configurations
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class RepeatersController : Controller
{
    private readonly DiscordShardedClient client;
    private readonly ILogger<RepeatersController> logger;
    private readonly MessageCountService? messageCountService;
    private readonly MessageRepeaterService repeaterService;
    private readonly StickyConditionService? stickyConditionService;
    private readonly GuildTimezoneService? timezoneService;

    /// <summary>
    ///     Initializes a new instance of the RepeatersController.
    /// </summary>
    public RepeatersController(
        MessageRepeaterService repeaterService,
        DiscordShardedClient client,
        ILogger<RepeatersController> logger,
        MessageCountService? messageCountService = null,
        StickyConditionService? stickyConditionService = null,
        GuildTimezoneService? timezoneService = null)
    {
        this.repeaterService = repeaterService;
        this.client = client;
        this.logger = logger;
        this.messageCountService = messageCountService;
        this.stickyConditionService = stickyConditionService;
        this.timezoneService = timezoneService;
    }

    /// <summary>
    ///     Gets all repeaters for a guild
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRepeaters(ulong guildId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var repeaters = repeaterService.GetGuildRepeaters(guildId);
            var guildTimezone = timezoneService?.GetTimeZoneOrDefault(guildId);
            var response = repeaters.Select(runner => new RepeaterResponse
            {
                Id = runner.Repeater.Id,
                ChannelId = runner.Repeater.ChannelId,
                Message = runner.Repeater.Message ?? string.Empty,
                Interval = runner.Repeater.Interval ?? "00:05:00",
                StartTimeOfDay = runner.Repeater.StartTimeOfDay,
                NoRedundant = runner.Repeater.NoRedundant,
                IsEnabled = runner.Repeater.IsEnabled,
                TriggerMode = (StickyTriggerMode)runner.Repeater.TriggerMode,
                ActivityThreshold = runner.Repeater.ActivityThreshold,
                ActivityTimeWindow = runner.Repeater.ActivityTimeWindow ?? "00:05:00",
                ConversationDetection = runner.Repeater.ConversationDetection,
                ConversationThreshold = runner.Repeater.ConversationThreshold,
                Priority = runner.Repeater.Priority,
                QueuePosition = runner.Repeater.QueuePosition,
                TimeConditions = runner.Repeater.TimeConditions,
                MaxAge = runner.Repeater.MaxAge,
                MaxTriggers = runner.Repeater.MaxTriggers,
                ThreadAutoSticky = runner.Repeater.ThreadAutoSticky,
                ThreadOnlyMode = runner.Repeater.ThreadOnlyMode,
                SuppressNotifications = runner.Repeater.SuppressNotifications,
                ForumTagConditions = runner.Repeater.ForumTagConditions,
                ThreadStickyMessages = runner.Repeater.ThreadStickyMessages,
                DisplayCount = runner.Repeater.DisplayCount,
                LastDisplayed = runner.Repeater.LastDisplayed,
                DateAdded = runner.Repeater.DateAdded,
                NextExecution = runner.NextDateTime,
                GuildTimezone = guildTimezone?.Id ?? "UTC",
                RequiresTimezone = !string.IsNullOrWhiteSpace(runner.Repeater.TimeConditions) && guildTimezone == null
            }).ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get repeaters for guild {GuildId}", guildId);
            return StatusCode(500, "Failed to retrieve repeaters");
        }
    }

    /// <summary>
    ///     Gets a specific repeater by ID
    /// </summary>
    [HttpGet("{repeaterId:int}")]
    public async Task<IActionResult> GetRepeater(ulong guildId, int repeaterId)
    {
        try
        {
            var repeaters = repeaterService.GetGuildRepeaters(guildId);
            var repeater = repeaters.FirstOrDefault(r => r.Repeater.Id == repeaterId);

            if (repeater == null)
                return NotFound("Repeater not found");

            var guildTimezone = timezoneService?.GetTimeZoneOrDefault(guildId);
            var response = new RepeaterResponse
            {
                Id = repeater.Repeater.Id,
                ChannelId = repeater.Repeater.ChannelId,
                Message = repeater.Repeater.Message ?? string.Empty,
                Interval = repeater.Repeater.Interval ?? "00:05:00",
                StartTimeOfDay = repeater.Repeater.StartTimeOfDay,
                NoRedundant = repeater.Repeater.NoRedundant,
                IsEnabled = repeater.Repeater.IsEnabled,
                TriggerMode = (StickyTriggerMode)repeater.Repeater.TriggerMode,
                ActivityThreshold = repeater.Repeater.ActivityThreshold,
                ActivityTimeWindow = repeater.Repeater.ActivityTimeWindow ?? "00:05:00",
                ConversationDetection = repeater.Repeater.ConversationDetection,
                ConversationThreshold = repeater.Repeater.ConversationThreshold,
                Priority = repeater.Repeater.Priority,
                QueuePosition = repeater.Repeater.QueuePosition,
                TimeConditions = repeater.Repeater.TimeConditions,
                MaxAge = repeater.Repeater.MaxAge,
                MaxTriggers = repeater.Repeater.MaxTriggers,
                ThreadAutoSticky = repeater.Repeater.ThreadAutoSticky,
                ForumTagConditions = repeater.Repeater.ForumTagConditions,
                DisplayCount = repeater.Repeater.DisplayCount,
                LastDisplayed = repeater.Repeater.LastDisplayed,
                DateAdded = repeater.Repeater.DateAdded,
                NextExecution = repeater.NextDateTime,
                GuildTimezone = guildTimezone?.Id ?? "UTC",
                RequiresTimezone = !string.IsNullOrWhiteSpace(repeater.Repeater.TimeConditions) && guildTimezone == null
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get repeater {RepeaterId} for guild {GuildId}", repeaterId, guildId);
            return StatusCode(500, "Failed to retrieve repeater");
        }
    }

    /// <summary>
    ///     Creates a new repeater
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateRepeater(ulong guildId, [FromBody] CreateRepeaterRequest request)
    {
        try
        {
            logger.LogInformation(
                "Creating repeater for guild {GuildId} with trigger mode {TriggerMode}, threadOnlyMode: {ThreadOnlyMode}, threadAutoSticky: {ThreadAutoSticky}",
                guildId, request.TriggerMode, request.ThreadOnlyMode, request.ThreadAutoSticky);

            var guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            // Validate immediate mode on forum channels
            if (request.TriggerMode == StickyTriggerMode.Immediate)
            {
                var channel = guild.GetChannel(request.ChannelId);
                if (channel is IForumChannel && !request.ThreadOnlyMode)
                {
                    return BadRequest(
                        "Immediate mode on forum channels requires Thread-Only Mode to be enabled, as bots cannot post directly to forum channels.");
                }
            }

            // Validate activity-based modes require message counting
            if (request.TriggerMode is StickyTriggerMode.OnActivity or StickyTriggerMode.OnNoActivity
                    or StickyTriggerMode.AfterMessages && messageCountService != null)
            {
                var (_, enabled) = await messageCountService.GetAllCountsForEntity(
                    MessageCountService.CountQueryType.Guild, guildId, guildId);

                if (!enabled)
                {
                    return BadRequest("Activity-based trigger modes require message counting to be enabled");
                }
            }

            // Parse and validate interval (skip for immediate mode)
            TimeSpan interval;
            if (request.TriggerMode == StickyTriggerMode.Immediate)
            {
                // Immediate mode doesn't use intervals
                interval = TimeSpan.FromSeconds(1); // Dummy value
            }
            else
            {
                if (!TimeSpan.TryParse(request.Interval, out interval))
                    return BadRequest("Invalid interval format");

                if (interval < TimeSpan.FromSeconds(5) || interval > TimeSpan.FromMinutes(25000))
                    return BadRequest("Interval must be between 5 seconds and 25000 minutes");
            }

            // Validate activity settings
            if (request.ActivityThreshold < 1)
                return BadRequest("Activity threshold must be at least 1");

            if (!TimeSpan.TryParse(request.ActivityTimeWindow, out var activityWindow) ||
                activityWindow < TimeSpan.FromSeconds(30) || activityWindow > TimeSpan.FromHours(6))
                return BadRequest("Activity time window must be between 30 seconds and 6 hours");

            // Validate priority
            if (request.Priority is < 0 or > 100)
                return BadRequest("Priority must be between 0 and 100");

            // Handle time conditions and validate timezone
            string? timeConditionsJson;
            if (!string.IsNullOrWhiteSpace(request.TimeSchedulePreset) && stickyConditionService != null)
            {
                // Check if guild has timezone set for time-based scheduling
                var guildTimezone = timezoneService?.GetTimeZoneOrDefault(guildId);
                if (guildTimezone == null && request.TimeSchedulePreset.ToLowerInvariant() != "none")
                {
                    return BadRequest(
                        "Time-based scheduling requires a guild timezone to be set. Use the timezone command first.");
                }

                timeConditionsJson = request.TimeSchedulePreset.ToLowerInvariant() switch
                {
                    "business" => stickyConditionService.CreateBusinessHoursCondition(),
                    "evening" => stickyConditionService.CreateEveningHoursCondition(),
                    "weekend" => stickyConditionService.CreateWeekendCondition(),
                    _ => request.TimeConditions
                };
            }
            else
            {
                timeConditionsJson = request.TimeConditions;
            }

            // Create the repeater
            var runner = await repeaterService.CreateRepeaterAsync(
                guildId,
                request.ChannelId,
                interval,
                request.Message,
                request.StartTimeOfDay,
                request.AllowMentions,
                request.TriggerMode,
                request.ThreadAutoSticky,
                request.ThreadOnlyMode);

            if (runner == null)
                return StatusCode(500, "Failed to create repeater");

            logger.LogInformation("Successfully created repeater with ID {RepeaterId}", runner.Repeater.Id);

            // Update additional sticky properties
            await UpdateRepeaterProperties(guildId, runner.Repeater.Id, request, timeConditionsJson);

            var responseTimezone = timezoneService?.GetTimeZoneOrDefault(guildId);
            var response = new RepeaterResponse
            {
                Id = runner.Repeater.Id,
                ChannelId = runner.Repeater.ChannelId,
                Message = runner.Repeater.Message ?? string.Empty,
                Interval = runner.Repeater.Interval ?? "00:05:00",
                StartTimeOfDay = runner.Repeater.StartTimeOfDay,
                NoRedundant = runner.Repeater.NoRedundant,
                IsEnabled = runner.Repeater.IsEnabled,
                TriggerMode = (StickyTriggerMode)runner.Repeater.TriggerMode,
                ActivityThreshold = runner.Repeater.ActivityThreshold,
                ActivityTimeWindow = runner.Repeater.ActivityTimeWindow ?? "00:05:00",
                ConversationDetection = runner.Repeater.ConversationDetection,
                ConversationThreshold = runner.Repeater.ConversationThreshold,
                Priority = runner.Repeater.Priority,
                QueuePosition = runner.Repeater.QueuePosition,
                TimeConditions = runner.Repeater.TimeConditions,
                MaxAge = runner.Repeater.MaxAge,
                MaxTriggers = runner.Repeater.MaxTriggers,
                ThreadAutoSticky = runner.Repeater.ThreadAutoSticky,
                ThreadOnlyMode = runner.Repeater.ThreadOnlyMode,
                SuppressNotifications = runner.Repeater.SuppressNotifications,
                ForumTagConditions = runner.Repeater.ForumTagConditions,
                ThreadStickyMessages = runner.Repeater.ThreadStickyMessages,
                DisplayCount = runner.Repeater.DisplayCount,
                LastDisplayed = runner.Repeater.LastDisplayed,
                DateAdded = runner.Repeater.DateAdded,
                NextExecution = runner.NextDateTime,
                GuildTimezone = responseTimezone?.Id ?? "UTC",
                RequiresTimezone = !string.IsNullOrWhiteSpace(runner.Repeater.TimeConditions) &&
                                   responseTimezone == null
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create repeater for guild {GuildId}", guildId);
            return StatusCode(500, "Failed to create repeater");
        }
    }

    /// <summary>
    ///     Updates an existing repeater
    /// </summary>
    [HttpPatch("{repeaterId:int}")]
    public async Task<IActionResult> UpdateRepeater(ulong guildId, int repeaterId,
        [FromBody] UpdateRepeaterRequest request)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null)
                return NotFound("Guild not found");

            var repeaters = repeaterService.GetGuildRepeaters(guildId);
            var repeater = repeaters.FirstOrDefault(r => r.Repeater.Id == repeaterId);

            if (repeater == null)
                return NotFound("Repeater not found");

            // Validate activity-based modes if changing trigger mode
            if (request.TriggerMode.HasValue && messageCountService != null)
            {
                var triggerMode = request.TriggerMode.Value;
                if (triggerMode is StickyTriggerMode.OnActivity or StickyTriggerMode.OnNoActivity
                    or StickyTriggerMode.AfterMessages)
                {
                    var (_, enabled) = await messageCountService.GetAllCountsForEntity(
                        MessageCountService.CountQueryType.Guild, guildId, guildId);

                    if (!enabled)
                    {
                        return BadRequest("Activity-based trigger modes require message counting to be enabled");
                    }
                }
            }

            // Update individual properties
            var updateTasks = new List<Task<bool>>();

            if (!string.IsNullOrWhiteSpace(request.Message))
            {
                updateTasks.Add(repeaterService.UpdateRepeaterMessageAsync(guildId, repeaterId, request.Message,
                    request.AllowMentions ?? false));
            }

            if (request.ChannelId.HasValue)
            {
                updateTasks.Add(repeaterService.UpdateRepeaterChannelAsync(guildId, repeaterId,
                    request.ChannelId.Value));
            }

            if (request.TriggerMode.HasValue)
            {
                updateTasks.Add(repeaterService.UpdateRepeaterTriggerModeAsync(guildId, repeaterId,
                    request.TriggerMode.Value));
            }

            if (request.ActivityThreshold.HasValue && request.ActivityTimeWindow != null)
            {
                if (TimeSpan.TryParse(request.ActivityTimeWindow, out var timeWindow))
                {
                    updateTasks.Add(repeaterService.UpdateRepeaterActivityThresholdAsync(guildId, repeaterId,
                        request.ActivityThreshold.Value, timeWindow));
                }
            }

            if (request.Priority.HasValue)
            {
                updateTasks.Add(repeaterService.UpdateRepeaterPriorityAsync(guildId, repeaterId,
                    request.Priority.Value));
            }

            if (request.ConversationDetection.HasValue)
            {
                updateTasks.Add(repeaterService.ToggleRepeaterConversationDetectionAsync(guildId, repeaterId));
            }

            if (request.NoRedundant.HasValue)
            {
                updateTasks.Add(repeaterService.ToggleRepeaterRedundancyAsync(guildId, repeaterId));
            }

            if (request.IsEnabled.HasValue)
            {
                updateTasks.Add(repeaterService.ToggleRepeaterEnabledAsync(guildId, repeaterId));
            }

            if (request.TimeConditions != null)
            {
                updateTasks.Add(repeaterService.UpdateRepeaterTimeConditionsAsync(guildId, repeaterId,
                    request.TimeConditions));
            }

            // Wait for all updates to complete
            var results = await Task.WhenAll(updateTasks);
            if (results.Any(r => !r))
            {
                return StatusCode(500, "Some updates failed");
            }

            // Return updated repeater
            return await GetRepeater(guildId, repeaterId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update repeater {RepeaterId} for guild {GuildId}", repeaterId, guildId);
            return StatusCode(500, "Failed to update repeater");
        }
    }

    /// <summary>
    ///     Deletes a repeater
    /// </summary>
    [HttpDelete("{repeaterId:int}")]
    public async Task<IActionResult> DeleteRepeater(ulong guildId, int repeaterId)
    {
        try
        {
            var repeaters = repeaterService.GetGuildRepeaters(guildId);
            var repeater = repeaters.FirstOrDefault(r => r.Repeater.Id == repeaterId);

            if (repeater == null)
                return NotFound("Repeater not found");

            await repeaterService.RemoveRepeater(repeater.Repeater);

            return Ok(new
            {
                success = true, message = "Repeater deleted successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete repeater {RepeaterId} for guild {GuildId}", repeaterId, guildId);
            return StatusCode(500, "Failed to delete repeater");
        }
    }

    /// <summary>
    ///     Triggers a repeater immediately
    /// </summary>
    [HttpPost("{repeaterId:int}/trigger")]
    public async Task<IActionResult> TriggerRepeater(ulong guildId, int repeaterId)
    {
        try
        {
            var repeaters = repeaterService.GetGuildRepeaters(guildId);
            var repeater = repeaters.FirstOrDefault(r => r.Repeater.Id == repeaterId);

            if (repeater == null)
                return NotFound("Repeater not found");

            repeater.Reset();
            await repeater.Trigger();

            return Ok(new
            {
                success = true, message = "Repeater triggered successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to trigger repeater {RepeaterId} for guild {GuildId}", repeaterId, guildId);
            return StatusCode(500, "Failed to trigger repeater");
        }
    }

    /// <summary>
    ///     Gets statistics for all repeaters in a guild
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetRepeaterStatistics(ulong guildId)
    {
        try
        {
            var repeaters = repeaterService.GetGuildRepeaters(guildId);

            var stats = new RepeaterStatsResponse
            {
                TotalRepeaters = repeaters.Count,
                ActiveRepeaters = repeaters.Count(r => r.Repeater.IsEnabled),
                DisabledRepeaters = repeaters.Count(r => !r.Repeater.IsEnabled),
                TotalDisplays = repeaters.Sum(r => r.Repeater.DisplayCount),
                TimeScheduledRepeaters = repeaters.Count(r => !string.IsNullOrWhiteSpace(r.Repeater.TimeConditions)),
                ConversationAwareRepeaters = repeaters.Count(r => r.Repeater.ConversationDetection)
            };

            // Calculate trigger mode distribution
            var modeGroups = repeaters.GroupBy(r => (StickyTriggerMode)r.Repeater.TriggerMode);
            foreach (var group in modeGroups)
            {
                stats.TriggerModeDistribution[group.Key.ToString()] = group.Count();
            }

            // Find most active repeater
            var mostActive = repeaters.OrderByDescending(r => r.Repeater.DisplayCount).FirstOrDefault();
            if (mostActive != null)
            {
                stats.MostActiveRepeater = new RepeaterResponse
                {
                    Id = mostActive.Repeater.Id,
                    ChannelId = mostActive.Repeater.ChannelId,
                    Message = mostActive.Repeater.Message ?? string.Empty,
                    DisplayCount = mostActive.Repeater.DisplayCount,
                    TriggerMode = (StickyTriggerMode)mostActive.Repeater.TriggerMode,
                    Priority = mostActive.Repeater.Priority
                };
            }

            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get repeater statistics for guild {GuildId}", guildId);
            return StatusCode(500, "Failed to retrieve statistics");
        }
    }

    /// <summary>
    ///     Bulk enables/disables multiple repeaters
    /// </summary>
    [HttpPatch("bulk-toggle")]
    public async Task<IActionResult> BulkToggleRepeaters(ulong guildId, [FromBody] List<int> repeaterIds,
        [FromQuery] bool enable = true)
    {
        try
        {
            var results = new List<object>();
            var repeaters = repeaterService.GetGuildRepeaters(guildId);

            foreach (var repeaterId in repeaterIds)
            {
                var repeater = repeaters.FirstOrDefault(r => r.Repeater.Id == repeaterId);
                if (repeater == null)
                {
                    results.Add(new
                    {
                        repeaterId, success = false, error = "Repeater not found"
                    });
                    continue;
                }

                if (repeater.Repeater.IsEnabled == enable)
                {
                    results.Add(new
                    {
                        repeaterId, success = true, message = "No change needed"
                    });
                    continue;
                }

                var success = await repeaterService.ToggleRepeaterEnabledAsync(guildId, repeaterId);
                results.Add(new
                {
                    repeaterId, success, message = success ? "Updated" : "Failed to update"
                });
            }

            return Ok(new
            {
                results
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to bulk toggle repeaters for guild {GuildId}", guildId);
            return StatusCode(500, "Failed to bulk toggle repeaters");
        }
    }

    /// <summary>
    ///     Checks if message counting is enabled (required for activity-based features)
    /// </summary>
    [HttpGet("message-counting-status")]
    public async Task<IActionResult> GetMessageCountingStatus(ulong guildId)
    {
        if (messageCountService == null)
        {
            return Ok(new
            {
                enabled = false, available = false, message = "Message counting service not available"
            });
        }

        try
        {
            var (_, enabled) = await messageCountService.GetAllCountsForEntity(
                MessageCountService.CountQueryType.Guild, guildId, guildId);

            return Ok(new
            {
                enabled, available = true
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check message counting status for guild {GuildId}", guildId);
            return StatusCode(500, "Failed to check message counting status");
        }
    }

    private async Task UpdateRepeaterProperties(ulong guildId, int repeaterId, CreateRepeaterRequest request,
        string? timeConditionsJson)
    {
        // Update all the new sticky properties that aren't handled by the basic CreateRepeaterAsync
        var updateTasks = new List<Task<bool>>
        {
            repeaterService.UpdateRepeaterActivityThresholdAsync(guildId, repeaterId, request.ActivityThreshold,
                TimeSpan.Parse(request.ActivityTimeWindow)),
            repeaterService.UpdateRepeaterPriorityAsync(guildId, repeaterId, request.Priority)
        };

        if (request.ConversationDetection)
        {
            updateTasks.Add(repeaterService.ToggleRepeaterConversationDetectionAsync(guildId, repeaterId));
        }

        if (request.ConversationThreshold > 0)
        {
            updateTasks.Add(
                repeaterService.UpdateRepeaterConversationThresholdAsync(guildId, repeaterId,
                    request.ConversationThreshold));
        }

        if (request.NoRedundant)
        {
            updateTasks.Add(repeaterService.ToggleRepeaterRedundancyAsync(guildId, repeaterId));
        }

        if (!string.IsNullOrWhiteSpace(timeConditionsJson))
        {
            updateTasks.Add(
                repeaterService.UpdateRepeaterTimeConditionsAsync(guildId, repeaterId, timeConditionsJson));
        }

        if (!string.IsNullOrWhiteSpace(request.MaxAge))
        {
            updateTasks.Add(
                repeaterService.UpdateRepeaterExpiryAsync(guildId, repeaterId, request.MaxAge, request.MaxTriggers));
        }

        if (request.ThreadOnlyMode)
        {
            updateTasks.Add(repeaterService.ToggleRepeaterThreadOnlyModeAsync(guildId, repeaterId));
        }

        if (!string.IsNullOrWhiteSpace(request.ForumTagConditions))
        {
            updateTasks.Add(
                repeaterService.UpdateRepeaterForumTagConditionsAsync(guildId, repeaterId, request.ForumTagConditions));
        }

        await Task.WhenAll(updateTasks);
    }
}