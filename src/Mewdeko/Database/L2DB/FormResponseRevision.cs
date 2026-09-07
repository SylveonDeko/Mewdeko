using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A snapshot of a response's answers taken just before the submitter edits it, so the wording a
///     reviewer originally read is never lost.
/// </summary>
[Table("form_response_revisions")]
public class FormResponseRevision
{
    [Column("id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("response_id", CanBeNull = false)]
    public int ResponseId { get; set; }

    /// <summary>
    ///     The answers as they stood, as JSON.
    /// </summary>
    [Column("snapshot", CanBeNull = false)]
    public string Snapshot { get; set; } = null!;

    [Column("edited_by")]
    public ulong? EditedBy { get; set; }

    [Column("created_at", CanBeNull = false)]
    public DateTime CreatedAt { get; set; }
}