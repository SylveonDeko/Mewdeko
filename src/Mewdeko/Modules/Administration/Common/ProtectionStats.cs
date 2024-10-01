using System.Threading;
using Mewdeko.Common.Collections;

namespace Mewdeko.Modules.Administration.Common;

/// <summary>
///     Enumeration representing different types of protection against unwanted activities.
/// </summary>
public enum ProtectionType
{
    /// <summary>
    ///     Protection against raiding.
    /// </summary>
    Raiding,

    /// <summary>
    ///     Protection against spamming.
    /// </summary>
    Spamming,

    /// <summary>
    ///     Protection against alting.
    /// </summary>
    Alting,

    /// <summary>
    ///     Protection against mass mention.
    /// </summary>
    MassMention
}

/// <summary>
///     Represents statistics related to anti-raid measures.
/// </summary>
public class AntiRaidStats
{
    /// <summary>
    ///     Gets or sets the anti-raid settings.
    /// </summary>
    public AntiRaidSetting AntiRaidSettings { get; set; }

    /// <summary>
    ///     Gets or sets the count of users involved in the raid.
    /// </summary>
    public int UsersCount { get; set; }

    /// <summary>
    ///     Gets or sets the set of users involved in the raid.
    /// </summary>
    public ConcurrentHashSet<IGuildUser> RaidUsers { get; set; } = [];
}

/// <summary>
///     Represents statistics related to anti-spam measures.
/// </summary>
public class AntiSpamStats
{
    /// <summary>
    ///     Gets or sets the anti-spam settings.
    /// </summary>
    public AntiSpamSetting AntiSpamSettings { get; set; }

    /// <summary>
    ///     Gets or sets the statistics for each user involved in spamming.
    /// </summary>
    public ConcurrentDictionary<ulong, UserSpamStats> UserStats { get; set; }
        = new();
}

/// <summary>
///     Represents statistics related to anti-alting measures.
/// </summary>
public class AntiAltStats
{
    private readonly AntiAltSetting setting;
    private int counter;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AntiAltStats" /> class with the specified anti-alt setting.
    /// </summary>
    /// <param name="setting">The anti-alt setting.</param>
    public AntiAltStats(AntiAltSetting setting)
    {
        this.setting = setting;
    }

    /// <summary>
    ///     Gets the action to be taken against alting.
    /// </summary>
    public PunishmentAction Action
    {
        get
        {
            return setting.Action;
        }
    }

    /// <summary>
    ///     Gets the duration of the action against alting in minutes.
    /// </summary>
    public int ActionDurationMinutes
    {
        get
        {
            return setting.ActionDurationMinutes;
        }
    }

    /// <summary>
    ///     Gets the ID of the role associated with alting punishment.
    /// </summary>
    public ulong? RoleId
    {
        get
        {
            return setting.RoleId;
        }
    }

    /// <summary>
    ///     Gets the minimum age required for a user to be considered as an alt.
    /// </summary>
    public string MinAge
    {
        get
        {
            return setting.MinAge;
        }
    }

    /// <summary>
    ///     Gets the counter for alting occurrences.
    /// </summary>
    public int Counter
    {
        get
        {
            return counter;
        }
    }

    /// <summary>
    ///     Increments the counter for alting occurrences.
    /// </summary>
    public void Increment()
    {
        Interlocked.Increment(ref counter);
    }
}

/// <summary>
///     Stores the settings and stats related to anti-mass mention protection.
/// </summary>
public class AntiMassMentionStats
{
    /// <summary>
    ///     Anti mass Mention Setting
    /// </summary>
    public AntiMassMentionSetting AntiMassMentionSettings { get; set; }

    /// <summary>
    ///     Tracks the mention counts per user.
    /// </summary>
    public ConcurrentDictionary<ulong, UserMentionStats> UserStats { get; } = new();
}

/// <summary>
///     Stores the stats for a user's mentions.
/// </summary>
public class UserMentionStats : IDisposable
{
    private readonly List<DateTime> mentionTimestamps = new();
    private readonly int timeFrameSeconds;

    /// <summary>
    ///     User Mention Stats
    /// </summary>
    /// <param name="timeFrameSeconds"></param>
    public UserMentionStats(int timeFrameSeconds)
    {
        this.timeFrameSeconds = timeFrameSeconds;
    }

    /// <summary>
    ///     Dispose
    /// </summary>
    public void Dispose()
    {
        mentionTimestamps.Clear();
    }

    /// <summary>
    ///     Adds a mention timestamp and checks whether it exceeds the allowed threshold.
    /// </summary>
    /// <param name="mentionCount">The number of mentions in the current message.</param>
    /// <param name="threshold">The allowed number of mentions over the specified time.</param>
    /// <returns>True if the mention threshold is exceeded, otherwise false.</returns>
    public bool AddMentions(int mentionCount, int threshold)
    {
        var now = DateTime.UtcNow;

        // Remove old mentions outside of the time window
        mentionTimestamps.RemoveAll(t => (now - t).TotalSeconds > timeFrameSeconds);

        // Add the current mentions
        for (var i = 0; i < mentionCount; i++)
        {
            mentionTimestamps.Add(now);
        }

        // Check if the number of mentions exceeds the threshold
        return mentionTimestamps.Count >= threshold;
    }
}