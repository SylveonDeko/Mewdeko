namespace Mewdeko.Modules.Forms.Common;

/// <summary>
///     Thrown when a submission is refused because of the answers it carries, as opposed to who is
///     submitting it or when.
/// </summary>
/// <remarks>
///     The errors are carried per question rather than as one sentence, so the form can mark the
///     answers that need fixing where they sit instead of showing a single message at the bottom of
///     the page and leaving the reader to hunt.
/// </remarks>
public class FormSubmissionException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FormSubmissionException" /> class.
    /// </summary>
    /// <param name="errors">One message per question that failed, keyed by question identifier.</param>
    public FormSubmissionException(IReadOnlyDictionary<int, string> errors)
        : base("Some answers need fixing")
    {
        Errors = errors;
    }

    /// <summary>
    ///     One message per question that failed, keyed by question identifier.
    /// </summary>
    public IReadOnlyDictionary<int, string> Errors { get; }
}