using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal used to collect the language and code for the run command.
/// </summary>
public class CodeRunModal : IModal
{
    /// <summary>
    ///     Gets or sets the language name or alias.
    /// </summary>
    [InputLabel("Language")]
    [ModalTextInput("language", TextInputStyle.Short, "python", 1, 24)]
    public string Language { get; set; }

    /// <summary>
    ///     Gets or sets the code.
    /// </summary>
    [InputLabel("Code")]
    [ModalTextInput("code", TextInputStyle.Paragraph, "print(\"hi\")", 1, 4000)]
    public string Code { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title
    {
        get
        {
            return "Run code";
        }
    }
}
