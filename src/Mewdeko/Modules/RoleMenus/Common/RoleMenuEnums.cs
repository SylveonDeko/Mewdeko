using Discord.Interactions;

namespace Mewdeko.Modules.RoleMenus.Common;

/// <summary>
///     How a role menu shows its options on the posted message.
/// </summary>
public enum RoleMenuStyle
{
    /// <summary>
    ///     One dropdown listing every option.
    /// </summary>
    [ChoiceDisplay("Dropdown")]
    Dropdown = 0,

    /// <summary>
    ///     One button per option, in rows of five.
    /// </summary>
    [ChoiceDisplay("Buttons")]
    Buttons = 1
}

/// <summary>
///     How many roles a member can pick from one menu.
/// </summary>
public enum RoleMenuMode
{
    /// <summary>
    ///     Members can pick any number of roles, within the menu's limits.
    /// </summary>
    [ChoiceDisplay("Pick any")]
    Multi = 0,

    /// <summary>
    ///     Members hold one role at a time. Picking another swaps it.
    /// </summary>
    [ChoiceDisplay("Pick one")]
    Exclusive = 1
}

/// <summary>
///     Whether members get a note after using a menu.
/// </summary>
public enum RoleMenuReplyMode
{
    /// <summary>
    ///     A private note lists the roles that were added and removed.
    /// </summary>
    [ChoiceDisplay("Private confirmation")]
    Private = 0,

    /// <summary>
    ///     No note unless something went wrong.
    /// </summary>
    [ChoiceDisplay("No confirmation")]
    Silent = 1
}

/// <summary>
///     Button colors a menu option can use. Values match Discord's button styles.
/// </summary>
public enum RoleMenuButtonColor
{
    /// <summary>
    ///     Discord's primary blurple button.
    /// </summary>
    [ChoiceDisplay("Blurple")]
    Blurple = 1,

    /// <summary>
    ///     Discord's secondary grey button.
    /// </summary>
    [ChoiceDisplay("Grey")]
    Grey = 2,

    /// <summary>
    ///     Discord's green success button.
    /// </summary>
    [ChoiceDisplay("Green")]
    Green = 3,

    /// <summary>
    ///     Discord's red danger button.
    /// </summary>
    [ChoiceDisplay("Red")]
    Red = 4
}

/// <summary>
///     The current state of a menu's posted message.
/// </summary>
public enum RoleMenuStatus
{
    /// <summary>
    ///     The message is posted and members can use it.
    /// </summary>
    Live,

    /// <summary>
    ///     The message is posted but its controls are greyed out.
    /// </summary>
    Paused,

    /// <summary>
    ///     The menu has no posted message.
    /// </summary>
    NotPosted,

    /// <summary>
    ///     The menu's channel no longer exists.
    /// </summary>
    ChannelMissing
}

/// <summary>
///     Why a role can't be offered on a menu.
/// </summary>
public enum RoleMenuRoleProblem
{
    /// <summary>
    ///     The role can be offered.
    /// </summary>
    None,

    /// <summary>
    ///     The role no longer exists.
    /// </summary>
    Missing,

    /// <summary>
    ///     The role is the server's everyone role.
    /// </summary>
    Everyone,

    /// <summary>
    ///     An integration manages the role.
    /// </summary>
    Managed,

    /// <summary>
    ///     The role is at or above the bot's highest role, or the bot lacks Manage Roles.
    /// </summary>
    AboveBot,

    /// <summary>
    ///     The role is at or above the highest role of the person editing the menu.
    /// </summary>
    AboveActor
}

/// <summary>
///     Why a role menu write was refused.
/// </summary>
public enum RoleMenuError
{
    /// <summary>
    ///     No error.
    /// </summary>
    None,

    /// <summary>
    ///     The menu does not exist in this server.
    /// </summary>
    NotFound,

    /// <summary>
    ///     The server already has the most menus allowed.
    /// </summary>
    TooManyMenus,

    /// <summary>
    ///     The menu name is too long.
    /// </summary>
    NameInvalid,

    /// <summary>
    ///     The menu has no options.
    /// </summary>
    NoOptions,

    /// <summary>
    ///     The menu has more options than allowed.
    /// </summary>
    TooManyOptions,

    /// <summary>
    ///     A role is on the menu more than once.
    /// </summary>
    DuplicateRole,

    /// <summary>
    ///     A role on the menu no longer exists.
    /// </summary>
    RoleMissing,

    /// <summary>
    ///     The bot can't give out a role on the menu.
    /// </summary>
    RoleNotAssignable,

    /// <summary>
    ///     A role on the menu is at or above the editor's highest role.
    /// </summary>
    RoleAboveActor,

    /// <summary>
    ///     An option's emoji can't be used by the bot.
    /// </summary>
    InvalidEmoji,

    /// <summary>
    ///     The channel is missing, the wrong type, or the bot can't post there.
    /// </summary>
    InvalidChannel,

    /// <summary>
    ///     A text field is too long.
    /// </summary>
    TextTooLong,

    /// <summary>
    ///     The required role does not exist or is the everyone role.
    /// </summary>
    InvalidRequiredRole,

    /// <summary>
    ///     A reorder did not list every option exactly once.
    /// </summary>
    InvalidOrder,

    /// <summary>
    ///     Discord rejected the message.
    /// </summary>
    PostFailed,

    /// <summary>
    ///     The older emoji role setup to import was not found.
    /// </summary>
    ImportSourceNotFound,

    /// <summary>
    ///     None of the roles in the older setup exist anymore.
    /// </summary>
    ImportNoRoles
}
