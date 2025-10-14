namespace Mewdeko.Database.Enums;

/// <summary>
///     Represents how multiple conditions should be combined.
/// </summary>
public enum ConditionLogicType
{
    /// <summary>
    ///     All conditions must be true (AND logic).
    /// </summary>
    And,

    /// <summary>
    ///     At least one condition must be true (OR logic).
    /// </summary>
    Or
}