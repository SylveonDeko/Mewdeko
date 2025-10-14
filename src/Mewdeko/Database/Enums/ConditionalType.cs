namespace Mewdeko.Database.Enums;

/// <summary>
///     Represents the type of conditional logic applied to a form question.
/// </summary>
public enum ConditionalType
{
    /// <summary>
    ///     Condition based on another question's answer.
    /// </summary>
    QuestionBased = 0,

    /// <summary>
    ///     Condition based on user's Discord roles.
    /// </summary>
    DiscordRole = 1,

    /// <summary>
    ///     Condition based on how long user has been in the server.
    /// </summary>
    ServerTenure = 2,

    /// <summary>
    ///     Condition based on whether user is boosting the server.
    /// </summary>
    BoostStatus = 3,

    /// <summary>
    ///     Condition based on user's Discord permissions.
    /// </summary>
    Permission = 4,

    /// <summary>
    ///     Multiple conditions combined with AND/OR logic.
    /// </summary>
    MultipleConditions = 5
}