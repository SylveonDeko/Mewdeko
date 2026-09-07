using DataModel;

namespace Mewdeko.Controllers.Common.Forms;

/// <summary>
///     Request model for saving a whole form in one call: its settings, its questions, and the
///     options and conditions belonging to them.
/// </summary>
/// <remarks>
///     The builder used to save a form by issuing one request per question and one per option,
///     which for a long form meant dozens of sequential round trips and left the form in a
///     half-saved state if any of them failed. Saving the whole tree at once is both faster and
///     atomic.
/// </remarks>
public class FormSaveRequest
{
    /// <summary>
    ///     The form's settings. The identifier decides whether this creates or updates.
    /// </summary>
    public required Form Form { get; set; }

    /// <summary>
    ///     The questions, in the order they should be displayed. Display order is taken from this
    ///     order rather than from each question's own field, so the two cannot disagree.
    /// </summary>
    public List<FormQuestionSaveRequest> Questions { get; set; } = [];

    /// <summary>
    ///     Who is saving, recorded against the version this creates.
    /// </summary>
    public ulong? UserId { get; set; }
}

/// <summary>
///     One question within a whole-form save, with everything hanging off it.
/// </summary>
public class FormQuestionSaveRequest
{
    /// <summary>
    ///     The question. An identifier of zero creates a new one; anything else updates in place,
    ///     which is what keeps conditions, answer piping and stored responses pointing at the
    ///     right question.
    /// </summary>
    public required FormQuestion Question { get; set; }

    /// <summary>
    ///     The question's options, in display order. Empty for question types that take none.
    /// </summary>
    public List<FormQuestionOption> Options { get; set; } = [];

    /// <summary>
    ///     The conditions deciding whether the question is shown.
    /// </summary>
    public List<FormQuestionCondition> Conditions { get; set; } = [];
}