namespace Mewdeko.Controllers.Common.Forms;

/// <summary>
///     Request model for checking form eligibility.
/// </summary>
public class EligibilityCheckRequest
{
    /// <summary>
    ///     The Discord user ID to check eligibility for.
    /// </summary>
    public required ulong UserId { get; set; }
}