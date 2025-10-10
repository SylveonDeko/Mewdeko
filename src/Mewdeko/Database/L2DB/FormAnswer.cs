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

    [Column("created_at", CanBeNull = false)]
    public DateTime CreatedAt { get; set; }
}