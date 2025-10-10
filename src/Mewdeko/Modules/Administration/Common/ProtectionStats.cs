using System.Threading;
using DataModel;
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
    MassMention,

    /// <summary>
    ///     Protection against username/display name patterns.
    /// </summary>
    PatternMatching,

    /// <summary>
    ///     Protection against mass posting across channels.
    /// </summary>
    MassPosting,

    /// <summary>
    ///     Protection for honeypot channels that auto-ban posters.
    /// </summary>
    PostChannelBan
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
    public int Action
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

/// <summary>
///     Represents statistics related to anti-pattern measures.
/// </summary>
public class AntiPatternStats
{
    private int counter;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AntiPatternStats" /> class with the specified anti-pattern setting.
    /// </summary>
    /// <param name="setting">The anti-pattern setting.</param>
    public AntiPatternStats(AntiPatternSetting setting)
    {
        AntiPatternSettings = setting;
    }

    /// <summary>
    ///     Gets or sets the anti-pattern settings.
    /// </summary>
    public AntiPatternSetting AntiPatternSettings { get; set; }

    /// <summary>
    ///     Gets the action to be taken against pattern matches.
    /// </summary>
    public int Action
    {
        get
        {
            return AntiPatternSettings.Action;
        }
    }

    /// <summary>
    ///     Gets the duration of the action against pattern matches in minutes.
    /// </summary>
    public int PunishDuration
    {
        get
        {
            return AntiPatternSettings.PunishDuration;
        }
    }

    /// <summary>
    ///     Gets the ID of the role associated with pattern matching punishment.
    /// </summary>
    public ulong? RoleId
    {
        get
        {
            return AntiPatternSettings.RoleId;
        }
    }

    /// <summary>
    ///     Gets the counter for pattern matching occurrences.
    /// </summary>
    public int Counter
    {
        get
        {
            return counter;
        }
    }

    /// <summary>
    ///     Increments the counter for pattern matching occurrences.
    /// </summary>
    public void Increment()
    {
        Interlocked.Increment(ref counter);
    }
}

/// <summary>
///     Represents statistics related to anti-mass-post measures.
/// </summary>
public class AntiMassPostStats
{
    private int counter;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AntiMassPostStats" /> class with the specified anti-mass-post setting.
    /// </summary>
    /// <param name="setting">The anti-mass-post setting.</param>
    public AntiMassPostStats(AntiMassPostSetting setting)
    {
        AntiMassPostSettings = setting;
    }

    /// <summary>
    ///     Gets or sets the anti-mass-post settings.
    /// </summary>
    public AntiMassPostSetting AntiMassPostSettings { get; set; }

    /// <summary>
    ///     Tracks user message history across channels.
    /// </summary>
    public ConcurrentDictionary<ulong, UserMassPostStats> UserStats { get; } = new();

    /// <summary>
    ///     Gets the action to be taken against mass posting.
    /// </summary>
    public int Action
    {
        get
        {
            return AntiMassPostSettings.Action;
        }
    }

    /// <summary>
    ///     Gets the duration of the punishment in minutes.
    /// </summary>
    public int PunishDuration
    {
        get
        {
            return AntiMassPostSettings.PunishDuration;
        }
    }

    /// <summary>
    ///     Gets the ID of the role associated with mass post punishment.
    /// </summary>
    public ulong? RoleId
    {
        get
        {
            return AntiMassPostSettings.RoleId;
        }
    }

    /// <summary>
    ///     Gets the counter for mass post occurrences.
    /// </summary>
    public int Counter
    {
        get
        {
            return counter;
        }
    }

    /// <summary>
    ///     Increments the counter for mass post occurrences.
    /// </summary>
    public void Increment()
    {
        Interlocked.Increment(ref counter);
    }
}

/// <summary>
///     Tracks a user's message history across channels for anti-mass-post detection.
/// </summary>
public class UserMassPostStats : IDisposable
{
    private readonly int maxTracked;
    private readonly List<TrackedMessage> messages = new();
    private readonly int timeWindowSeconds;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UserMassPostStats" /> class.
    /// </summary>
    /// <param name="timeWindowSeconds">Time window to track messages.</param>
    /// <param name="maxTracked">Maximum number of messages to track.</param>
    public UserMassPostStats(int timeWindowSeconds, int maxTracked)
    {
        this.timeWindowSeconds = timeWindowSeconds;
        this.maxTracked = maxTracked;
    }

    /// <summary>
    ///     Disposes resources.
    /// </summary>
    public void Dispose()
    {
        messages.Clear();
    }

    /// <summary>
    ///     Adds a message and checks if cross-channel posting threshold is exceeded.
    /// </summary>
    /// <param name="channelId">The channel where the message was posted.</param>
    /// <param name="content">The message content.</param>
    /// <param name="channelThreshold">Threshold of different channels.</param>
    /// <param name="similarityThreshold">Content similarity threshold.</param>
    /// <param name="requireIdentical">Whether content must be identical.</param>
    /// <param name="caseSensitive">Whether comparison is case sensitive.</param>
    /// <returns>List of message IDs if threshold exceeded, otherwise null.</returns>
    public List<ulong>? AddMessage(ulong channelId, string content, int channelThreshold, double similarityThreshold,
        bool requireIdentical, bool caseSensitive)
    {
        var now = DateTime.UtcNow;

        // Remove old messages
        messages.RemoveAll(m => (now - m.Timestamp).TotalSeconds > timeWindowSeconds);

        // Remove excess messages
        while (messages.Count >= maxTracked)
        {
            messages.RemoveAt(0);
        }

        // Add new message
        messages.Add(new TrackedMessage
        {
            ChannelId = channelId, Content = caseSensitive ? content : content.ToLower(), Timestamp = now
        });

        // Check if content is similar to other recent messages
        var similarMessages = messages.Where(m =>
        {
            if (requireIdentical)
            {
                return m.Content == (caseSensitive ? content : content.ToLower());
            }

            return CalculateSimilarity(m.Content, caseSensitive ? content : content.ToLower()) >=
                   similarityThreshold;
        }).ToList();

        if (similarMessages.Count < 2) return null;

        // Count unique channels
        var uniqueChannels = similarMessages.Select(m => m.ChannelId).Distinct().Count();

        if (uniqueChannels >= channelThreshold)
        {
            return similarMessages.Select(m => m.MessageId).ToList();
        }

        return null;
    }

    /// <summary>
    ///     Calculates similarity between two strings using Levenshtein distance.
    /// </summary>
    private static double CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
            return 0;

        var maxLength = Math.Max(s1.Length, s2.Length);
        if (maxLength == 0) return 1.0;

        var distance = LevenshteinDistance(s1, s2);
        return 1.0 - (double)distance / maxLength;
    }

    /// <summary>
    ///     Calculates Levenshtein distance between two strings.
    /// </summary>
    private static int LevenshteinDistance(string s1, string s2)
    {
        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (var i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;
        for (var j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;

        for (var i = 1; i <= s1.Length; i++)
        {
            for (var j = 1; j <= s2.Length; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
    }

    private class TrackedMessage
    {
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

/// <summary>
///     Represents statistics related to anti-post-channel measures.
/// </summary>
public class AntiPostChannelStats
{
    private int counter;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AntiPostChannelStats" /> class with the specified anti-post-channel
    ///     setting.
    /// </summary>
    /// <param name="setting">The anti-post-channel setting.</param>
    public AntiPostChannelStats(AntiPostChannelSetting setting)
    {
        AntiPostChannelSettings = setting;
    }

    /// <summary>
    ///     Gets or sets the anti-post-channel settings.
    /// </summary>
    public AntiPostChannelSetting AntiPostChannelSettings { get; set; }

    /// <summary>
    ///     Gets the action to be taken when posting in honeypot channels.
    /// </summary>
    public int Action
    {
        get
        {
            return AntiPostChannelSettings.Action;
        }
    }

    /// <summary>
    ///     Gets the duration of the punishment in minutes.
    /// </summary>
    public int PunishDuration
    {
        get
        {
            return AntiPostChannelSettings.PunishDuration;
        }
    }

    /// <summary>
    ///     Gets the ID of the role associated with post channel punishment.
    /// </summary>
    public ulong? RoleId
    {
        get
        {
            return AntiPostChannelSettings.RoleId;
        }
    }

    /// <summary>
    ///     Gets the counter for post channel occurrences.
    /// </summary>
    public int Counter
    {
        get
        {
            return counter;
        }
    }

    /// <summary>
    ///     Increments the counter for post channel occurrences.
    /// </summary>
    public void Increment()
    {
        Interlocked.Increment(ref counter);
    }
}