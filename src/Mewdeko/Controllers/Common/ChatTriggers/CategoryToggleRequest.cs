namespace Mewdeko.Controllers.Common.ChatTriggers;

/// <summary>
///     Request model for enabling or disabling every trigger in a category at once.
/// </summary>
public class CategoryToggleRequest
{
    /// <summary>
    ///     The category to act on.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    ///     Whether the category's triggers should be disabled.
    /// </summary>
    public bool Disabled { get; set; }
}