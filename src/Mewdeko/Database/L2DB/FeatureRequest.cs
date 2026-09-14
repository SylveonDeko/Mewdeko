using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A feature request, bug report, or other suggestion submitted from the dashboard.
/// </summary>
[Table("FeatureRequests")]
public class FeatureRequest
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The user who submitted the request.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     The submitter's name at the time, kept so the list stays readable if they leave.
    /// </summary>
    [Column("UserName")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    ///     The guild the submitter was managing when they wrote the request, if any.
    /// </summary>
    [Column("GuildId")]
    public ulong? GuildId { get; set; }

    /// <summary>
    ///     That guild's name at the time.
    /// </summary>
    [Column("GuildName")]
    public string? GuildName { get; set; }

    /// <summary>
    ///     The category key: feature, bug, or other.
    /// </summary>
    [Column("Category")]
    public string Category { get; set; } = "feature";

    /// <summary>
    ///     The short title.
    /// </summary>
    [Column("Title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     The full description.
    /// </summary>
    [Column("Body")]
    public string Body { get; set; } = string.Empty;

    /// <summary>
    ///     The status key: open, planned, done, or declined.
    /// </summary>
    [Column("Status")]
    public string Status { get; set; } = "open";

    /// <summary>
    ///     A note a bot owner left when changing the status, shown to the submitter.
    /// </summary>
    [Column("OwnerNote")]
    public string? OwnerNote { get; set; }

    /// <summary>
    ///     Cached upvote count, kept in step with the votes table.
    /// </summary>
    [Column("Votes")]
    public int Votes { get; set; }

    /// <summary>
    ///     The message posted to the report channel, so status changes edit it instead of posting again.
    /// </summary>
    [Column("ReportMessageId")]
    public ulong ReportMessageId { get; set; }

    /// <summary>
    ///     When the status or note last changed.
    /// </summary>
    [Column("UpdatedAt")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     When the request was submitted.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}

/// <summary>
///     One user's upvote on a feature request.
/// </summary>
[Table("FeatureRequestVotes")]
public class FeatureRequestVote
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The request voted on.
    /// </summary>
    [Column("RequestId")]
    public int RequestId { get; set; }

    /// <summary>
    ///     The voting user.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     When the vote was cast.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}
