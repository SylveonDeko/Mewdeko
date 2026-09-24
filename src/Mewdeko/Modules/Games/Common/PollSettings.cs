using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Games.Common;

/// <summary>
/// Represents configuration settings for a poll.
/// </summary>
public class PollSettings
{
    /// <summary>
    /// Gets or sets whether users can select multiple options.
    /// </summary>
    [JsonPropertyName("allowMultipleVotes")]
    public bool AllowMultipleVotes { get; set; }

    /// <summary>
    /// Gets or sets whether votes should be anonymous.
    /// </summary>
    [JsonPropertyName("isAnonymous")]
    public bool IsAnonymous { get; set; }

    /// <summary>
    /// Gets or sets whether users can change their votes.
    /// </summary>
    [JsonPropertyName("allowVoteChanges")]
    public bool AllowVoteChanges { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of role IDs that can vote on this poll.
    /// </summary>
    [JsonPropertyName("allowedRoles")]
    public List<ulong>? AllowedRoles { get; set; }

    /// <summary>
    /// Gets or sets the embed color as a hex string.
    /// </summary>
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of options that must be selected in multi-choice polls.
    /// </summary>
    [JsonPropertyName("minSelections")]
    public int? MinSelections { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of options that can be selected in multi-choice polls.
    /// </summary>
    [JsonPropertyName("maxSelections")]
    public int? MaxSelections { get; set; }

    /// <summary>
    /// Gets or sets whether to show vote counts while the poll is active.
    /// </summary>
    [JsonPropertyName("showResults")]
    public bool ShowResults { get; set; } = true;

    /// <summary>
    /// Gets or sets the poll type this template should default to when reused.
    /// </summary>
    [JsonPropertyName("defaultType")]
    public PollType? DefaultType { get; set; }

    /// <summary>
    /// Gets or sets whether to show progress bars for vote counts.
    /// </summary>
    [JsonPropertyName("showProgressBars")]
    public bool ShowProgressBars { get; set; } = true;

    /// <summary>
    /// Gets or sets the duration in minutes for the poll to automatically close.
    /// </summary>
    [JsonPropertyName("durationMinutes")]
    public int? DurationMinutes { get; set; }

    /// <summary>
    /// Gets or sets the duration in hours for the poll to automatically close.
    /// </summary>
    [JsonPropertyName("durationHours")]
    public int? DurationHours { get; set; }

    /// <summary>
    /// Gets or sets the duration in days for the poll to automatically close.
    /// </summary>
    [JsonPropertyName("durationDays")]
    public int? DurationDays { get; set; }
}

/// <summary>
/// Represents statistics for a poll.
/// </summary>
public class PollStats
{
    /// <summary>
    /// Gets or sets the total number of votes cast.
    /// </summary>
    public int TotalVotes { get; set; }

    /// <summary>
    /// Gets or sets the number of unique users who voted.
    /// </summary>
    public int UniqueVoters { get; set; }

    /// <summary>
    /// Gets or sets the vote count for each option by index.
    /// </summary>
    public Dictionary<int, int> OptionVotes { get; set; } = new();

    /// <summary>
    /// Gets or sets the chronological list of vote events.
    /// </summary>
    public List<VoteHistoryEntry> VoteHistory { get; set; } = [];

    /// <summary>
    /// Gets or sets the vote distribution by Discord role.
    /// </summary>
    public Dictionary<string, int> VotesByRole { get; set; } = new();

    /// <summary>
    /// Gets or sets the average time from poll creation to vote.
    /// </summary>
    public TimeSpan AverageVoteTime { get; set; }

    /// <summary>
    /// Gets or sets the participation rate as a percentage.
    /// </summary>
    public double ParticipationRate { get; set; }
}

/// <summary>
/// Represents a single vote event in the poll history.
/// </summary>
public class VoteHistoryEntry
{
    /// <summary>
    /// Gets or sets the Discord user ID of the voter.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the voter at the time of voting.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the selected option indices.
    /// </summary>
    public int[] OptionIndices { get; set; } = [];

    /// <summary>
    /// Gets or sets when the vote was cast.
    /// </summary>
    public DateTime VotedAt { get; set; }

    /// <summary>
    /// Gets or sets whether this vote was cast anonymously.
    /// </summary>
    public bool IsAnonymous { get; set; }
}

/// <summary>
/// Represents a poll option for creation requests.
/// </summary>
public class PollOptionData
{
    /// <summary>
    /// Gets or sets the display text for this option.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hex color code for this option's visual display.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets the Discord emote string for this option.
    /// </summary>
    public string? Emote { get; set; }
}