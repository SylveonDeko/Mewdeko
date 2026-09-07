using DataModel;

namespace Mewdeko.Modules.Forms.Common;

/// <summary>
///     A whole form frozen at a moment in time: its settings, its questions, and everything hanging
///     off them. Stored as JSON on every save so an edit can be reviewed and undone.
/// </summary>
public class FormSnapshot
{
    /// <summary>
    ///     The form's own settings as they stood.
    /// </summary>
    public Form Form { get; set; } = null!;

    /// <summary>
    ///     The questions as they stood, in display order.
    /// </summary>
    public List<FormQuestionSnapshot> Questions { get; set; } = [];
}

/// <summary>
///     One question frozen at a moment in time, with the options and conditions belonging to it.
/// </summary>
public class FormQuestionSnapshot
{
    /// <summary>
    ///     The question itself.
    /// </summary>
    public FormQuestion Question { get; set; } = null!;

    /// <summary>
    ///     The question's options, in display order. Empty for question types that take none.
    /// </summary>
    public List<FormQuestionOption> Options { get; set; } = [];

    /// <summary>
    ///     The conditions deciding whether the question is shown.
    /// </summary>
    public List<FormQuestionCondition> Conditions { get; set; } = [];
}

/// <summary>
///     What a single entry in a version comparison represents.
/// </summary>
public enum FormVersionChangeKind
{
    /// <summary>Something was added in this version.</summary>
    Added,

    /// <summary>Something was changed in this version.</summary>
    Changed,

    /// <summary>Something was removed in this version.</summary>
    Removed
}

/// <summary>
///     One difference between a saved version of a form and the version before it.
/// </summary>
/// <param name="Kind">Whether the entry describes an addition, an edit or a removal.</param>
/// <param name="Section">The heading this change belongs under, so a large save reads a section at a time.</param>
/// <param name="Label">What changed.</param>
/// <param name="Before">The value in the earlier version, or null when there was none.</param>
/// <param name="After">The value in this version, or null when there is none.</param>
public record FormVersionChange(
    FormVersionChangeKind Kind,
    string Section,
    string Label,
    string? Before,
    string? After);