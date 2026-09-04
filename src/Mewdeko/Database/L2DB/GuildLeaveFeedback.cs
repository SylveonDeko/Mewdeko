using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A record of the feedback prompt sent to a guild owner after Mewdeko was removed
///     from their server, plus the answer they gave if they gave one.
/// </summary>
[Table("GuildLeaveFeedbacks")]
public class GuildLeaveFeedback
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The guild the bot was removed from.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The guild's name at the time of removal, kept because the guild is no longer reachable.
    /// </summary>
    [Column("GuildName")]
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    ///     How many members the guild had at the time of removal.
    /// </summary>
    [Column("MemberCount")]
    public int MemberCount { get; set; }

    /// <summary>
    ///     The owner the prompt was sent to.
    /// </summary>
    [Column("OwnerId")]
    public ulong OwnerId { get; set; }

    /// <summary>
    ///     When the bot originally joined the guild, if known.
    /// </summary>
    [Column("JoinedAt")]
    public DateTime? JoinedAt { get; set; }

    /// <summary>
    ///     The reason key the owner selected, or null if they have not answered.
    /// </summary>
    [Column("Reason")]
    public string? Reason { get; set; }

    /// <summary>
    ///     Free text the owner wrote in the follow-up modal.
    /// </summary>
    [Column("Comment")]
    public string? Comment { get; set; }

    /// <summary>
    ///     Whether the owner explicitly dismissed the prompt.
    /// </summary>
    [Column("Dismissed")]
    public bool Dismissed { get; set; }

    /// <summary>
    ///     The message posted to the report channel, so a later comment can edit it instead of
    ///     posting a second report. Zero when nothing has been reported yet.
    /// </summary>
    [Column("ReportMessageId")]
    public ulong ReportMessageId { get; set; }

    /// <summary>
    ///     When the owner answered or dismissed the prompt.
    /// </summary>
    [Column("AnsweredAt")]
    public DateTime? AnsweredAt { get; set; }

    /// <summary>
    ///     When the prompt was sent.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}