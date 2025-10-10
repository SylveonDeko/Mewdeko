namespace Mewdeko.Controllers.Common.Forms;

/// <summary>
///     Request model for approving a form response.
/// </summary>
public class ApprovalRequest
{
    /// <summary>
    ///     The Discord user ID of the reviewer approving the response.
    /// </summary>
    public required ulong ReviewerId { get; set; }

    /// <summary>
    ///     Optional notes explaining the approval decision.
    /// </summary>
    public string? Notes { get; set; }
}