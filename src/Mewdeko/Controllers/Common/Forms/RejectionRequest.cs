namespace Mewdeko.Controllers.Common.Forms;

/// <summary>
///     Request model for rejecting a form response.
/// </summary>
public class RejectionRequest
{
    /// <summary>
    ///     The Discord user ID of the reviewer rejecting the response.
    /// </summary>
    public required ulong ReviewerId { get; set; }

    /// <summary>
    ///     Notes explaining the rejection decision.
    /// </summary>
    public required string Notes { get; set; }
}