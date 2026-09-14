using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal for setting the custom stream notification template for a guild.
/// </summary>
public class StreamTemplateModal : IModal
{
    /// <summary>
    ///     Gets or sets the template text or embed json. Type reset to clear the template.
    /// </summary>
    [InputLabel("Template")]
    [ModalTextInput("template", TextInputStyle.Paragraph,
        "Text or embed json with placeholders like %stream.name%. Type reset to clear.")]
    public string Template { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title
    {
        get
        {
            return "Stream Notification Template";
        }
    }
}