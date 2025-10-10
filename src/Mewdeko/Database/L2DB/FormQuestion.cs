using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Represents a question in a form with conditional logic support
/// </summary>
[Table("form_questions")]
public class FormQuestion
{
    [Column("id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("form_id", CanBeNull = false)]
    public int FormId { get; set; }

    [Column("question_text", CanBeNull = false)]
    public string QuestionText { get; set; } = null!;

    [Column("question_type", CanBeNull = false)]
    public string QuestionType { get; set; } = null!; // 'short_text', 'long_text', 'multiple_choice', etc.

    [Column("is_required", CanBeNull = false)]
    public bool IsRequired { get; set; }

    [Column("display_order", CanBeNull = false)]
    public int DisplayOrder { get; set; }

    [Column("placeholder")]
    public string? Placeholder { get; set; }

    [Column("min_value")]
    public int? MinValue { get; set; }

    [Column("max_value")]
    public int? MaxValue { get; set; }

    [Column("min_length")]
    public int? MinLength { get; set; }

    [Column("max_length")]
    public int? MaxLength { get; set; }

    // Conditional logic fields
    [Column("conditional_parent_question_id")]
    public int? ConditionalParentQuestionId { get; set; }

    [Column("conditional_operator")]
    public string? ConditionalOperator { get; set; } // 'equals', 'contains', 'not_equals', etc.

    [Column("conditional_expected_value")]
    public string? ConditionalExpectedValue { get; set; }

    [Column("created_at", CanBeNull = false)]
    public DateTime CreatedAt { get; set; }
}