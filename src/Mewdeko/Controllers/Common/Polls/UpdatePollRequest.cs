namespace Mewdeko.Controllers.Common.Polls;

/// <summary>
/// Request model for updating an existing poll.
/// </summary>
public class UpdatePollRequest
{
    /// <summary>
    /// Gets or sets the new poll question text.
    /// </summary>
    public string? Question { get; set; }

    /// <summary>
    /// Gets or sets the new poll duration in minutes before auto-close.
    /// </summary>
    public int? DurationMinutes { get; set; }

    /// <summary>
    /// Gets or sets whether users can select multiple options.
    /// </summary>
    public bool? AllowMultipleVotes { get; set; }

    /// <summary>
    /// Gets or sets whether votes should be anonymous.
    /// </summary>
    public bool? IsAnonymous { get; set; }

    /// <summary>
    /// Gets or sets the list of role IDs that can vote on this poll.
    /// </summary>
    public List<ulong>? AllowedRoles { get; set; }

    /// <summary>
    /// Gets or sets the list of role IDs that cannot vote on this poll.
    /// </summary>
    public List<ulong>? BlockedRoles { get; set; }

    /// <summary>
    /// Gets or sets the number of votes after which the poll ends automatically. Zero removes the limit.
    /// </summary>
    public int? MaxVotes { get; set; }

    /// <summary>
    /// Gets or sets the message posted when the poll ends, with poll placeholders. An empty string clears it.
    /// </summary>
    public string? ResultsMessage { get; set; }

    /// <summary>
    /// Gets or sets the embed color as a hex string.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets whether users can change their votes.
    /// </summary>
    public bool? AllowVoteChanges { get; set; }

    /// <summary>
    /// Gets or sets whether to show vote counts while the poll is active.
    /// </summary>
    public bool? ShowResults { get; set; }

    /// <summary>
    /// Gets or sets whether to show progress bars for vote counts.
    /// </summary>
    public bool? ShowProgressBars { get; set; }

    /// <summary>
    /// Gets or sets the Discord user ID of the person updating the poll.
    /// </summary>
    public ulong UserId { get; set; }
}