namespace Mewdeko.Modules.Forms.Common;

/// <summary>
///     The kind of record a form route names, which decides how its guild is found.
/// </summary>
public enum FormIdKind
{
    /// <summary>
    ///     A form id.
    /// </summary>
    Form,

    /// <summary>
    ///     A question id.
    /// </summary>
    Question,

    /// <summary>
    ///     A question option id.
    /// </summary>
    Option,

    /// <summary>
    ///     A question condition id.
    /// </summary>
    Condition,

    /// <summary>
    ///     A response id.
    /// </summary>
    Response
}

/// <summary>
///     The form, and so the guild, that a record id belongs to.
/// </summary>
public sealed class FormAccessScope
{
    /// <summary>
    ///     Gets the form the record belongs to.
    /// </summary>
    public int FormId { get; init; }

    /// <summary>
    ///     Gets the guild that owns the form.
    /// </summary>
    public ulong GuildId { get; init; }

    /// <summary>
    ///     Gets whether the form is still a draft.
    /// </summary>
    public bool IsDraft { get; init; }

    /// <summary>
    ///     Gets the question the record belongs to, when the id named an option or a condition or a question.
    /// </summary>
    public int? QuestionId { get; init; }

    /// <summary>
    ///     Gets the person who submitted the response, when the id named a response.
    /// </summary>
    public ulong? ResponseUserId { get; init; }

    /// <summary>
    ///     Gets or sets whether the caller passed the staff rule for the guild.
    /// </summary>
    public bool CallerIsStaff { get; set; }
}
