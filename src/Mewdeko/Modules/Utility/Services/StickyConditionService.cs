using System.Text.Json;
using DataModel;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Service for evaluating sticky message conditions and scheduling.
/// </summary>
public class StickyConditionService : INService
{
    private readonly GuildTimezoneService guildTimezoneService;
    private readonly ILogger<StickyConditionService> logger;

    /// <summary>
    ///     Initializes a new instance of the StickyConditionService.
    /// </summary>
    /// <param name="guildTimezoneService">Service for guild timezone management.</param>
    /// <param name="logger">Logger for this service.</param>
    public StickyConditionService(GuildTimezoneService guildTimezoneService, ILogger<StickyConditionService> logger)
    {
        this.guildTimezoneService = guildTimezoneService;
        this.logger = logger;
    }

    /// <summary>
    ///     Checks if a sticky message should be active based on its time conditions.
    /// </summary>
    /// <param name="repeater">The repeater configuration.</param>
    /// <param name="guildId">The guild ID for timezone lookup.</param>
    /// <returns>True if the sticky should be active at the current time.</returns>
    public bool ShouldDisplayAtCurrentTime(GuildRepeater repeater, ulong guildId)
    {
        return IsWithinTimeConditions(repeater.TimeConditions, guildId, $"repeater {repeater.Id}");
    }

    /// <summary>
    ///     Checks whether the guild's current local time satisfies a set of serialized time conditions.
    /// </summary>
    /// <param name="timeConditionsJson">The serialized conditions, or null or empty for no restriction.</param>
    /// <param name="guildId">The guild ID, used to resolve the guild's timezone.</param>
    /// <param name="context">A description of what owns the conditions, used only for logging.</param>
    /// <returns>True if any condition matches the current time, or if there are no conditions.</returns>
    /// <remarks>
    ///     Shared by sticky messages and chat triggers so both interpret an identical condition format, including
    ///     overnight ranges and day-of-week filters.
    /// </remarks>
    public bool IsWithinTimeConditions(string? timeConditionsJson, ulong guildId, string context)
    {
        if (string.IsNullOrWhiteSpace(timeConditionsJson))
            return true;

        try
        {
            var conditions = JsonSerializer.Deserialize<TimeCondition[]>(timeConditionsJson);
            if (conditions == null || conditions.Length == 0)
                return true;

            var guildTime = GetGuildTime(guildId);

            // If ANY condition matches, the owner should be active
            return conditions.Any(condition => condition.IsActiveAt(guildTime));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse time conditions for {Context}", context);
            return true; // Default to active if parsing fails
        }
    }

    /// <summary>
    ///     Checks if a sticky message should be displayed in a forum thread based on tag conditions.
    /// </summary>
    /// <param name="repeater">The repeater configuration.</param>
    /// <param name="threadTags">The forum thread tags.</param>
    /// <returns>True if the sticky should be displayed based on forum tag conditions.</returns>
    public bool ShouldDisplayForForumTags(GuildRepeater repeater, IEnumerable<ulong> threadTags)
    {
        if (!repeater.ThreadAutoSticky || string.IsNullOrWhiteSpace(repeater.ForumTagConditions))
            return repeater.ThreadAutoSticky;

        try
        {
            var condition = JsonSerializer.Deserialize<ForumTagCondition>(repeater.ForumTagConditions);
            return condition?.IsValidForTags(threadTags) ?? true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse forum tag conditions for repeater {RepeaterId}", repeater.Id);
            return true; // Default to showing if parsing fails
        }
    }

    /// <summary>
    ///     Checks if a sticky has expired based on age or trigger count limits.
    /// </summary>
    /// <param name="repeater">The repeater configuration.</param>
    /// <returns>True if the sticky has expired and should be removed.</returns>
    public bool HasExpired(GuildRepeater repeater)
    {
        // Check max age
        if (!string.IsNullOrWhiteSpace(repeater.MaxAge) && repeater.DateAdded.HasValue)
        {
            if (TimeSpan.TryParse(repeater.MaxAge, out var maxAge))
            {
                if (DateTime.UtcNow - repeater.DateAdded.Value > maxAge)
                    return true;
            }
        }

        // Check max triggers
        if (repeater.MaxTriggers.HasValue && repeater.DisplayCount >= repeater.MaxTriggers.Value)
            return true;

        return false;
    }

    /// <summary>
    ///     Creates a default time condition for business hours (9 AM - 5 PM, weekdays).
    /// </summary>
    /// <returns>JSON string representing business hours condition.</returns>
    public string CreateBusinessHoursCondition()
    {
        var condition = new TimeCondition
        {
            Name = "Business Hours",
            StartTime = "09:00",
            EndTime = "17:00",
            DaysOfWeek = [1, 2, 3, 4, 5], // Monday through Friday
            Enabled = true
        };

        return JsonSerializer.Serialize(new[]
        {
            condition
        });
    }

    /// <summary>
    ///     Creates a default time condition for evening hours (6 PM - 11 PM, all days).
    /// </summary>
    /// <returns>JSON string representing evening hours condition.</returns>
    public string CreateEveningHoursCondition()
    {
        var condition = new TimeCondition
        {
            Name = "Evening Hours",
            StartTime = "18:00",
            EndTime = "23:00",
            DaysOfWeek = null, // All days
            Enabled = true
        };

        return JsonSerializer.Serialize(new[]
        {
            condition
        });
    }

    /// <summary>
    ///     Creates a default time condition for weekend (Saturday and Sunday, all day).
    /// </summary>
    /// <returns>JSON string representing weekend condition.</returns>
    public string CreateWeekendCondition()
    {
        var condition = new TimeCondition
        {
            Name = "Weekend",
            StartTime = null, // All day
            EndTime = null,
            DaysOfWeek = [0, 6], // Sunday and Saturday
            Enabled = true
        };

        return JsonSerializer.Serialize(new[]
        {
            condition
        });
    }

    /// <summary>
    ///     Parses time conditions from JSON string.
    /// </summary>
    /// <param name="json">JSON string containing time conditions.</param>
    /// <returns>Array of time conditions, or empty array if parsing fails.</returns>
    public TimeCondition[] ParseTimeConditions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<TimeCondition[]>(json) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse time conditions from JSON: {Json}", json);
            return [];
        }
    }

    /// <summary>
    ///     Serializes time conditions to JSON string.
    /// </summary>
    /// <param name="conditions">Time conditions to serialize.</param>
    /// <returns>JSON string representation of the conditions.</returns>
    public string SerializeTimeConditions(TimeCondition[] conditions)
    {
        try
        {
            return JsonSerializer.Serialize(conditions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to serialize time conditions");
            return "[]";
        }
    }

    /// <summary>
    ///     Parses forum tag conditions from JSON string.
    /// </summary>
    /// <param name="json">JSON string containing forum tag conditions.</param>
    /// <returns>Forum tag condition object, or empty condition if parsing fails.</returns>
    public ForumTagCondition ParseForumTagConditions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ForumTagCondition();

        try
        {
            return JsonSerializer.Deserialize<ForumTagCondition>(json) ?? new ForumTagCondition();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse forum tag conditions from JSON: {Json}", json);
            return new ForumTagCondition();
        }
    }

    /// <summary>
    ///     Serializes forum tag conditions to JSON string.
    /// </summary>
    /// <param name="condition">Forum tag condition to serialize.</param>
    /// <returns>JSON string representation of the condition.</returns>
    public string SerializeForumTagConditions(ForumTagCondition condition)
    {
        try
        {
            return JsonSerializer.Serialize(condition);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to serialize forum tag conditions");
            return "{}";
        }
    }

    /// <summary>
    ///     Gets the current time in the guild's configured timezone.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>Current time in the guild's timezone, or UTC if no timezone is set.</returns>
    private DateTime GetGuildTime(ulong guildId)
    {
        var timezone = guildTimezoneService.GetTimeZoneOrUtc(guildId);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
    }
}