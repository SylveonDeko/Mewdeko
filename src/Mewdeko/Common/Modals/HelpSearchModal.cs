using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Represents a modal for searching commands inside a module.
/// </summary>
public class HelpSearchModal : IModal
{
    /// <summary>
    ///     Gets or sets the term to match against command aliases and descriptions.
    /// </summary>
    [InputLabel("Search term")]
    [ModalTextInput("term", TextInputStyle.Short, "ban, role, purge...")]
    public string Term { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Search commands";
        }
    }
}