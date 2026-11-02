namespace Mewdeko.Modules.Tickets.Common;

/// <summary>
///     Represents detailed information about a ticket button component
/// </summary>
public class ButtonInfo
{
    /// <summary>
    ///     The unique identifier for the button in the database
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The custom ID used to identify the button in Discord interactions
    /// </summary>
    public string CustomId { get; set; }

    /// <summary>
    ///     The text label displayed on the button
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    ///     The visual style of the button (Primary, Secondary, Success, or Danger)
    /// </summary>
    public ButtonStyle Style { get; set; }

    /// <summary>
    ///     The emoji displayed on the button (if any)
    /// </summary>
    public string Emoji { get; set; }

    /// <summary>
    ///     The ID of the category where tickets created by this button will be placed
    /// </summary>
    public ulong? CategoryId { get; set; }

    /// <summary>
    ///     The ID of the category where tickets will be moved when archived
    /// </summary>
    public ulong? ArchiveCategoryId { get; set; }

    /// <summary>
    ///     The ID of the text or forum channel tickets open in as threads or posts, or null for their own channels
    /// </summary>
    public ulong? ThreadChannelId { get; set; }

    /// <summary>
    ///     List of role IDs that have support permissions for tickets created by this button
    /// </summary>
    public List<ulong> SupportRoles { get; set; }

    /// <summary>
    ///     List of role IDs that have view-only permissions for tickets created by this button
    /// </summary>
    public List<ulong> ViewerRoles { get; set; }

    /// <summary>
    ///     Indicates whether this button shows a modal form when clicked
    /// </summary>
    public bool HasModal { get; set; }

    /// <summary>
    ///     Indicates whether this button uses a custom message when opening tickets
    /// </summary>
    public bool HasCustomOpenMessage { get; set; }
}

/// <summary>
///     Represents detailed information about a ticket select menu component
/// </summary>
public class SelectMenuInfo
{
    /// <summary>
    ///     The unique identifier for the select menu in the database
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The custom ID used to identify the select menu in Discord interactions
    /// </summary>
    public string CustomId { get; set; }

    /// <summary>
    ///     The placeholder text shown when no option is selected
    /// </summary>
    public string Placeholder { get; set; }

    /// <summary>
    ///     List of options available in the select menu
    /// </summary>
    public List<SelectOptionInfo> Options { get; set; }
}

/// <summary>
///     Represents detailed information about a select menu option
/// </summary>
public class SelectOptionInfo
{
    /// <summary>
    ///     The unique identifier for the option in the database
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The text label displayed for the option
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    ///     The value returned when this option is selected
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    ///     The description shown when hovering over the option
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    ///     The emoji displayed with the option (if any)
    /// </summary>
    public string Emoji { get; set; }

    /// <summary>
    ///     The ID of the category where tickets created by this option will be placed
    /// </summary>
    public ulong? CategoryId { get; set; }

    /// <summary>
    ///     The ID of the category where tickets will be moved when archived
    /// </summary>
    public ulong? ArchiveCategoryId { get; set; }

    /// <summary>
    ///     The ID of the text or forum channel tickets open in as threads or posts, or null for their own channels
    /// </summary>
    public ulong? ThreadChannelId { get; set; }

    /// <summary>
    ///     Indicates whether this option shows a modal form when selected
    /// </summary>
    public bool HasModal { get; set; }

    /// <summary>
    ///     Indicates whether this option uses a custom message when opening tickets
    /// </summary>
    public bool HasCustomOpenMessage { get; set; }
}

/// <summary>
///     Represents complete information about a ticket panel and all its components
/// </summary>
public class PanelInfo
{
    /// <summary>
    ///     The ID of the Discord message containing the panel
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    ///     The ID of the channel containing the panel
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     List of all buttons on the panel
    /// </summary>
    public List<ButtonInfo> Buttons { get; set; }

    /// <summary>
    ///     List of all select menus on the panel
    /// </summary>
    public List<SelectMenuInfo> SelectMenus { get; set; }
}

/// <summary>
///     Represents the status of a ticket panel.
/// </summary>
public class PanelStatusInfo
{
    /// <summary>
    ///     Gets or sets the panel's message ID.
    /// </summary>
    public ulong PanelId { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID where the panel should be.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the channel name.
    /// </summary>
    public string ChannelName { get; set; }

    /// <summary>
    ///     Gets or sets the status of the panel.
    /// </summary>
    public PanelStatus Status { get; set; }
}

/// <summary>
///     Represents the possible statuses of a ticket panel.
/// </summary>
public enum PanelStatus
{
    /// <summary>
    ///     Panel message exists and is accessible.
    /// </summary>
    OK,

    /// <summary>
    ///     Panel message is missing or deleted.
    /// </summary>
    MessageMissing,

    /// <summary>
    ///     The channel containing the panel has been deleted.
    /// </summary>
    ChannelDeleted
}