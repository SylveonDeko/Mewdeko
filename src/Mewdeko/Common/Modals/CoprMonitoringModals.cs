using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal for entering a COPR build notification template.
/// </summary>
public class CoprTemplateModal : IModal
{
    /// <summary>
    ///     Gets or sets the message text or embed json used for the notification.
    /// </summary>
    [InputLabel("Template")]
    [ModalTextInput("template", TextInputStyle.Paragraph,
        "Text or embed json. Placeholders: %copr.package%, %copr.url%")]
    public string Template { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title
    {
        get
        {
            return "COPR Build Template";
        }
    }
}