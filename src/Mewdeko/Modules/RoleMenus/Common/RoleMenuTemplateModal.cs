using Discord.Interactions;
using Mewdeko.Modules.RoleMenus.Services;

namespace Mewdeko.Modules.RoleMenus.Common;

/// <summary>
///     What a person typed when editing a template draft.
/// </summary>
public class RoleMenuTemplateModal : IModal
{
    /// <summary>
    ///     Gets or sets the menu name.
    /// </summary>
    [InputLabel("Menu name")]
    [ModalTextInput("name", TextInputStyle.Short, maxLength: RoleMenuService.NameLength)]
    public string? Name { get; set; }

    /// <summary>
    ///     Gets or sets the message above the menu.
    /// </summary>
    [InputLabel("Message")]
    [ModalTextInput("message", TextInputStyle.Paragraph, maxLength: 4000)]
    [RequiredInput(false)]
    public string? Message { get; set; }

    /// <summary>
    ///     Gets or sets the roles, one per line.
    /// </summary>
    [InputLabel("Roles")]
    [ModalTextInput("entries", TextInputStyle.Paragraph, maxLength: 4000)]
    public string? Entries { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title => "Edit template";
}
