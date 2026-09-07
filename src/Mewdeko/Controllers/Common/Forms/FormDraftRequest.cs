namespace Mewdeko.Controllers.Common.Forms;

/// <summary>
///     Request model for saving a partly filled form, so a long submission survives a closed tab.
/// </summary>
public class FormDraftRequest
{
    /// <summary>
    ///     The user the draft belongs to.
    /// </summary>
    public required ulong UserId { get; set; }

    /// <summary>
    ///     The answers filled in so far, keyed by question identifier. Nothing here is validated,
    ///     because a draft is by definition incomplete.
    /// </summary>
    public required Dictionary<int, object> Answers { get; set; }

    /// <summary>
    ///     The page the submitter had reached, so they resume where they left off.
    /// </summary>
    public int Page { get; set; }
}