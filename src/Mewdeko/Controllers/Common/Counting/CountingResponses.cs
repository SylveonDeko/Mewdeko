using Mewdeko.Modules.Counting.Common;

namespace Mewdeko.Controllers.Common.Counting;

/// <summary>
/// Response containing counting channel information.
/// </summary>
public class CountingChannelResponse
{
    /// <summary>
    /// The counting channel ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// The Discord channel ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// The channel name.
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    /// The current number in the counting sequence.
    /// </summary>
    public long CurrentNumber { get; set; }

    /// <summary>
    /// The number the counting started from.
    /// </summary>
    public long StartNumber { get; set; }

    /// <summary>
    /// The increment value for each count.
    /// </summary>
    public int Increment { get; set; }

    /// <summary>
    /// The ID of the user who made the last valid count.
    /// </summary>
    public ulong LastUserId { get; set; }

    /// <summary>
    /// The username of the user who made the last valid count.
    /// </summary>
    public string? LastUsername { get; set; }

    /// <summary>
    /// Whether this counting channel is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// When this counting channel was created.
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// The highest number reached in this channel.
    /// </summary>
    public long HighestNumber { get; set; }

    /// <summary>
    /// When the highest number was reached.
    /// </summary>
    public DateTime? HighestNumberReachedAt { get; set; }

    /// <summary>
    /// Total number of valid counts made in this channel.
    /// </summary>
    public long TotalCounts { get; set; }
}

/// <summary>
/// Response containing counting channel configuration.
/// </summary>
public class CountingConfigResponse
{
    /// <summary>
    /// The counting channel configuration ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The Discord channel ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Whether the same user can count consecutively.
    /// </summary>
    public bool AllowRepeatedUsers { get; set; }

    /// <summary>
    /// Cooldown in seconds between counts from the same user.
    /// </summary>
    public int Cooldown { get; set; }

    /// <summary>
    /// List of role IDs that can participate in counting.
    /// </summary>
    public string? RequiredRoles { get; set; }

    /// <summary>
    /// List of role IDs that are banned from counting.
    /// </summary>
    public string? BannedRoles { get; set; }

    /// <summary>
    /// Maximum number allowed in this channel (0 for unlimited).
    /// </summary>
    public long MaxNumber { get; set; }

    /// <summary>
    /// Whether to reset the count when an error occurs.
    /// </summary>
    public bool ResetOnError { get; set; }

    /// <summary>
    /// Whether to delete messages with wrong numbers.
    /// </summary>
    public bool DeleteWrongMessages { get; set; }

    /// <summary>
    /// The counting pattern/mode.
    /// </summary>
    public CountingPattern Pattern { get; set; }

    /// <summary>
    /// Base for number systems (2-36, default 10).
    /// </summary>
    public int NumberBase { get; set; }

    /// <summary>
    /// Custom emote to react with on correct counts.
    /// </summary>
    public string? SuccessEmote { get; set; }

    /// <summary>
    /// Custom emote to react with on incorrect counts.
    /// </summary>
    public string? ErrorEmote { get; set; }

    /// <summary>
    /// Whether to enable achievement tracking for this channel.
    /// </summary>
    public bool EnableAchievements { get; set; }

    /// <summary>
    /// Whether to enable competition features for this channel.
    /// </summary>
    public bool EnableCompetitions { get; set; }

    /// <summary>
    /// Custom message shown on a successful count, or null for the default.
    /// </summary>
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Custom message shown on a failed count, or null for the default.
    /// </summary>
    public string? FailureMessage { get; set; }

    /// <summary>
    /// Custom message shown when a milestone is reached, or null for the default.
    /// </summary>
    public string? MilestoneMessage { get; set; }
}

/// <summary>
/// Response containing counting statistics for a channel.
/// </summary>
public class CountingStatsResponse
{
    /// <summary>
    /// The counting channel information.
    /// </summary>
    public CountingChannelResponse Channel { get; set; } = null!;

    /// <summary>
    /// Total number of unique participants.
    /// </summary>
    public int TotalParticipants { get; set; }

    /// <summary>
    /// Total number of counting errors.
    /// </summary>
    public int TotalErrors { get; set; }

    /// <summary>
    /// Number of milestones reached.
    /// </summary>
    public int MilestonesReached { get; set; }

    /// <summary>
    /// The user with the most contributions.
    /// </summary>
    public CountingUserStatsResponse? TopContributor { get; set; }

    /// <summary>
    /// When the last counting activity occurred.
    /// </summary>
    public DateTime? LastActivity { get; set; }

    /// <summary>
    /// Average accuracy across all users.
    /// </summary>
    public double AverageAccuracy { get; set; }
}

/// <summary>
/// Response containing user counting statistics.
/// </summary>
public class CountingUserStatsResponse
{
    /// <summary>
    /// The user's Discord ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// The user's username.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// The user's avatar URL.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Total number of contributions made by this user.
    /// </summary>
    public long ContributionsCount { get; set; }

    /// <summary>
    /// The highest streak achieved by this user.
    /// </summary>
    public int HighestStreak { get; set; }

    /// <summary>
    /// The current active streak for this user.
    /// </summary>
    public int CurrentStreak { get; set; }

    /// <summary>
    /// When this user last contributed to counting.
    /// </summary>
    public DateTime? LastContribution { get; set; }

    /// <summary>
    /// Total numbers counted by this user.
    /// </summary>
    public long TotalNumbersCounted { get; set; }

    /// <summary>
    /// Number of errors made by this user.
    /// </summary>
    public int ErrorsCount { get; set; }

    /// <summary>
    /// User's accuracy percentage.
    /// </summary>
    public double Accuracy { get; set; }

    /// <summary>
    /// The user's rank in the channel.
    /// </summary>
    public int? Rank { get; set; }
}

/// <summary>
/// Response containing save point information.
/// </summary>
public class SavePointResponse
{
    /// <summary>
    /// The save point ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The saved number at this save point.
    /// </summary>
    public long SavedNumber { get; set; }

    /// <summary>
    /// When this save point was created.
    /// </summary>
    public DateTime? SavedAt { get; set; }

    /// <summary>
    /// The ID of the user who created this save point.
    /// </summary>
    public ulong SavedBy { get; set; }

    /// <summary>
    /// The username of the user who created this save point.
    /// </summary>
    public string? SavedByUsername { get; set; }

    /// <summary>
    /// The reason for creating this save point.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Whether this save point is still available for restore.
    /// </summary>
    public bool IsActive { get; set; }
}