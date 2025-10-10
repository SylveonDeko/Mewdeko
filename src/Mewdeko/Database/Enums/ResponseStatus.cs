namespace Mewdeko.Database.Enums;

/// <summary>
///     Represents the status of a form response in the workflow.
/// </summary>
public enum ResponseStatus
{
    /// <summary>
    ///     The response has been submitted and is awaiting review.
    /// </summary>
    Pending = 0,

    /// <summary>
    ///     The response is currently being reviewed by a moderator.
    /// </summary>
    UnderReview = 1,

    /// <summary>
    ///     The response has been approved by a moderator.
    /// </summary>
    Approved = 2,

    /// <summary>
    ///     The response has been rejected by a moderator.
    /// </summary>
    Rejected = 3
}