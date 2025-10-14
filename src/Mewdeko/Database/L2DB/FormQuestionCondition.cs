using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Represents a single condition in a multi-condition set for advanced question visibility logic
/// </summary>
[Table("form_question_conditions")]
public class FormQuestionCondition
{
    [Column("id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     The question this condition applies to
    /// </summary>
    [Column("question_id", CanBeNull = false)]
    public int QuestionId { get; set; }

    /// <summary>
    ///     Group number for OR logic (conditions in same group are ANDed, different groups are ORed)
    /// </summary>
    [Column("condition_group", CanBeNull = false)]
    public int ConditionGroup { get; set; }

    /// <summary>
    ///     Type of condition (0=Question, 1=Role, 2=Tenure, etc.)
    /// </summary>
    [Column("condition_type", CanBeNull = false)]
    public int ConditionType { get; set; }

    // Question-based condition fields
    /// <summary>
    ///     Target question ID for question-based conditions
    /// </summary>
    [Column("target_question_id")]
    public int? TargetQuestionId { get; set; }

    /// <summary>
    ///     Operator for question-based conditions
    /// </summary>
    [Column("operator")]
    public string? Operator { get; set; }

    /// <summary>
    ///     Expected value for question-based conditions
    /// </summary>
    [Column("expected_value")]
    public string? ExpectedValue { get; set; }

    // Role-based condition fields
    /// <summary>
    ///     Comma-separated role IDs for role-based conditions
    /// </summary>
    [Column("target_role_ids")]
    public string? TargetRoleIds { get; set; }

    // Tenure-based condition fields
    /// <summary>
    ///     Days threshold for tenure-based conditions
    /// </summary>
    [Column("days_threshold")]
    public int? DaysThreshold { get; set; }

    // Boost/Nitro condition fields
    /// <summary>
    ///     Whether boost is required
    /// </summary>
    [Column("requires_boost")]
    public bool? RequiresBoost { get; set; }

    /// <summary>
    ///     Whether Nitro is required
    /// </summary>
    [Column("requires_nitro")]
    public bool? RequiresNitro { get; set; }

    // Permission-based condition fields
    /// <summary>
    ///     Permission flags required
    /// </summary>
    [Column("permission_flags")]
    public long? PermissionFlags { get; set; }

    /// <summary>
    ///     How to combine this condition with others in the same group (AND/OR)
    /// </summary>
    [Column("logic_type")]
    public string LogicType { get; set; } = "AND";

    [Column("created_at", CanBeNull = false)]
    public DateTime CreatedAt { get; set; }
}