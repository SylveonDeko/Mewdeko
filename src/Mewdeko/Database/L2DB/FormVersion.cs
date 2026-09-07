using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A saved snapshot of a form, taken every time it is saved, so that edits can be reviewed and
///     undone.
/// </summary>
[Table("form_versions")]
public class FormVersion
{
    [Column("id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("form_id", CanBeNull = false)]
    public int FormId { get; set; }

    /// <summary>
    ///     The version's place in the form's history, counting from one.
    /// </summary>
    [Column("version_number", CanBeNull = false)]
    public int VersionNumber { get; set; }

    /// <summary>
    ///     The whole form as JSON: its settings, questions, options and conditions.
    /// </summary>
    [Column("snapshot", CanBeNull = false)]
    public string Snapshot { get; set; } = null!;

    /// <summary>
    ///     How many questions the form had, so a version list reads without deserializing every snapshot.
    /// </summary>
    [Column("question_count", CanBeNull = false)]
    public int QuestionCount { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("created_at", CanBeNull = false)]
    public DateTime CreatedAt { get; set; }
}