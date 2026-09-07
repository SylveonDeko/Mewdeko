using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A partly filled form, kept so a long submission survives a closed tab. One row per form and
///     submitter, replaced as they type and deleted once they submit.
/// </summary>
[Table("form_response_drafts")]
public class FormResponseDraft
{
    [Column("id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("form_id", CanBeNull = false)]
    public int FormId { get; set; }

    [Column("user_id", CanBeNull = false)]
    public ulong UserId { get; set; }

    /// <summary>
    ///     The answers filled in so far, as JSON keyed by question identifier.
    /// </summary>
    [Column("answers", CanBeNull = false)]
    public string Answers { get; set; } = null!;

    /// <summary>
    ///     The page the submitter had reached, so they resume where they left off.
    /// </summary>
    [Column("page", CanBeNull = false)]
    public int Page { get; set; }

    [Column("updated_at", CanBeNull = false)]
    public DateTime UpdatedAt { get; set; }
}