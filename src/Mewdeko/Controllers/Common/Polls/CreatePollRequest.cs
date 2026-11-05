using Mewdeko.Modules.Games.Common;

namespace Mewdeko.Controllers.Common.Polls;

/// <summary>
/// Request model for creating a new poll.
/// </summary>
public class CreatePollRequest
{
    /// <summary>
    /// Gets or sets the poll question text.
    /// </summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of poll options.
    /// </summary>
    public List<PollOptionRequest> Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the type of poll to create.
    /// </summary>
    public PollType Type { get; set; }

    /// <summary>
    /// Gets or sets the Discord channel ID where the poll will be posted.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the poll duration in minutes before auto-close.
    /// </summary>
    public int? DurationMinutes { get; set; }

    /// <summary>
    /// Gets or sets whether users can select multiple options.
    /// </summary>
    public bool AllowMultipleVotes { get; set; }

    /// <summary>
    /// Gets or sets whether votes should be anonymous.
    /// </summary>
    public bool IsAnonymous { get; set; }

    /// <summary>
    /// Gets or sets the list of role IDs that can vote on this poll.
    /// </summary>
    public List<ulong>? AllowedRoles { get; set; }

    /// <summary>
    /// Gets or sets the list of role IDs that cannot vote on this poll.
    /// </summary>
    public List<ulong>? BlockedRoles { get; set; }

    /// <summary>
    /// Gets or sets the number of votes after which the poll ends automatically.
    /// </summary>
    public int? MaxVotes { get; set; }

    /// <summary>
    /// Gets or sets the message posted when the poll ends, with poll placeholders.
    /// </summary>
    public string? ResultsMessage { get; set; }

    /// <summary>
    /// Gets or sets the embed color as a hex string.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets whether users can change their votes.
    /// </summary>
    public bool AllowVoteChanges { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to show vote counts while the poll is active.
    /// </summary>
    public bool ShowResults { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to show progress bars for vote counts.
    /// </summary>
    public bool ShowProgressBars { get; set; } = true;

    /// <summary>
    /// Gets or sets the Discord user ID of the person creating the poll.
    /// </summary>
    public ulong UserId { get; set; }
}

/// <summary>
/// Represents a poll option in the creation request.
/// </summary>
public class PollOptionRequest
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