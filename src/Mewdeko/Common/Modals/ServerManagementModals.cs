using Discord.Interactions;

// ReSharper disable NotNullOrRequiredMemberIsNotInitialized

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal for creating multiple roles at once, one role name per line.
/// </summary>
public class CreateRolesModal : IModal
{
    /// <summary>
    ///     Gets or sets the role names, one per line.
    /// </summary>
    [InputLabel("Role Names")]
    [ModalTextInput("role_names", TextInputStyle.Paragraph, "One role name per line")]
    public string RoleNames { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title
    {
        get
        {
            return "Create Roles";
        }
    }
}