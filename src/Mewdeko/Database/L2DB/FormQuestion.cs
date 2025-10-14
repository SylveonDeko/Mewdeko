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

    // Basic conditional logic fields (legacy - still supported)
    [Column("conditional_parent_question_id")]
    public int? ConditionalParentQuestionId { get; set; }

    [Column("conditional_operator")]
    public string? ConditionalOperator { get; set; } // 'equals', 'contains', 'not_equals', etc.

    [Column("conditional_expected_value")]
    public string? ConditionalExpectedValue { get; set; }

    // Advanced conditional logic
    /// <summary>
    ///     Type of conditional logic (0=QuestionBased, 1=DiscordRole, 2=ServerTenure, 3=BoostStatus, 4=Permission,
    ///     5=MultipleConditions)
    /// </summary>
    [Column("conditional_type", CanBeNull = false)]
    public int ConditionalType { get; set; }

    // Discord role-based conditionals
    /// <summary>
    ///     Comma-separated role IDs for role-based conditionals
    /// </summary>
    [Column("conditional_role_ids")]
    public string? ConditionalRoleIds { get; set; }

    /// <summary>
    ///     How to evaluate multiple roles: "any", "all", or "none"
    /// </summary>
    [Column("conditional_role_logic")]
    public string? ConditionalRoleLogic { get; set; }

    // Server tenure conditionals
    /// <summary>
    ///     Minimum days user must be in server (null = no requirement)
    /// </summary>
    [Column("conditional_days_in_server")]
    public int? ConditionalDaysInServer { get; set; }

    /// <summary>
    ///     Minimum Discord account age in days (null = no requirement)
    /// </summary>
    [Column("conditional_account_age_days")]
    public int? ConditionalAccountAgeDays { get; set; }

    // Boost/Premium conditionals
    /// <summary>
    ///     Whether user must be boosting the server (null = no requirement)
    /// </summary>
    [Column("conditional_requires_boost")]
    public bool? ConditionalRequiresBoost { get; set; }

    /// <summary>
    ///     Whether user must have Discord Nitro (null = no requirement)
    /// </summary>
    [Column("conditional_requires_nitro")]
    public bool? ConditionalRequiresNitro { get; set; }

    // Permission-based conditionals
    /// <summary>
    ///     GuildPermissions flags user must have (null = no requirement)
    /// </summary>
    [Column("conditional_permission_flags")]
    public long? ConditionalPermissionFlags { get; set; }

    // Conditional required
    /// <summary>
    ///     Question ID that determines if this question is required
    /// </summary>
    [Column("required_when_parent_question_id")]
    public int? RequiredWhenParentQuestionId { get; set; }

    /// <summary>
    ///     Operator for required condition evaluation
    /// </summary>
    [Column("required_when_operator")]
    public string? RequiredWhenOperator { get; set; }

    /// <summary>
    ///     Expected value for required condition evaluation
    /// </summary>
    [Column("required_when_value")]
    public string? RequiredWhenValue { get; set; }

    // Answer piping
    /// <summary>
    ///     Whether question text contains {{QX}} placeholders for answer piping
    /// </summary>
    [Column("enable_answer_piping", CanBeNull = false)]
    public bool EnableAnswerPiping { get; set; }

    [Column("created_at", CanBeNull = false)]
    public DateTime CreatedAt { get; set; }
}