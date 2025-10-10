using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Represents an option for multiple choice, checkbox, or dropdown questions
/// </summary>
[Table("form_question_options")]
public class FormQuestionOption
{
    [Column("id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("question_id", CanBeNull = false)]
    public int QuestionId { get; set; }

    [Column("option_text", CanBeNull = false)]
    public string OptionText { get; set; } = null!;

    [Column("option_value", CanBeNull = false)]
    public string OptionValue { get; set; } = null!;

    [Column("display_order", CanBeNull = false)]
    public int DisplayOrder { get; set; }
}