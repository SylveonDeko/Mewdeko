using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Represents an individual answer to a question in a form submission
/// </summary>
[Table("form_answers")]
public class FormAnswer
{
    [Column("id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("response_id", CanBeNull = false)]
    public int ResponseId { get; set; }

    [Column("question_id", CanBeNull = false)]
    public int QuestionId { get; set; }

    [Column("answer_text")]
    public string? AnswerText { get; set; }

    [Column("answer_values")]
    public string[]? AnswerValues { get; set; } // For multi-select questions (checkboxes)

    /// <summary>
    ///     The question as it was worded when this answer was given. Kept here so a response stays
    ///     readable after its question is reworded or deleted.
    /// </summary>
    [Column("question_text")]
    public string? QuestionText { get; set; }

    /// <summary>
    ///     The kind of input the question presented when this answer was given.
    /// </summary>
    [Column("question_type")]
    public string? QuestionType { get; set; }

    /// <summary>
    ///     The answer rendered for reading, with option values resolved to the labels the submitter saw.
    /// </summary>
    [Column("answer_display")]
    public string? AnswerDisplay { get; set; }

    /// <summary>
    ///     The form version this answer was given against, so the exact form can be replayed.
    /// </summary>
    [Column("form_version_id")]
    public int? FormVersionId { get; set; }

    [Column("created_at", CanBeNull = false)]
    public DateTime CreatedAt { get; set; }
}