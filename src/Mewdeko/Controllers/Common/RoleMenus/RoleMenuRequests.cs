namespace Mewdeko.Controllers.Common.RoleMenus;

/// <summary>
///     The full desired state of a role menu, used to create a menu or replace one.
/// </summary>
public class RoleMenuRequest
{
    /// <summary>
    ///     Internal name shown in lists and as the default message title. Blank means the default name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Channel the message lives in.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Plain text or embed builder JSON. Empty or null means the default message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    ///     0 dropdown, 1 buttons, 2 reactions.
    /// </summary>
    public int Style { get; set; }

    /// <summary>
    ///     Dropdown hint text. Null means the default for the mode.
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    ///     0 pick any, 1 pick one, 2 add only, 3 remove only, 4 reverse.
    /// </summary>
    public int Mode { get; set; }

    /// <summary>
    ///     Fewest roles from this menu a member must keep once they pick.
    /// </summary>
    public int MinRoles { get; set; }

    /// <summary>
    ///     Most roles from this menu a member can hold. 0 means no limit.
    /// </summary>
    public int MaxRoles { get; set; }

    /// <summary>
    ///     Role needed to use the menu. Null or 0 means anyone.
    /// </summary>
    public ulong? RequiredRoleId { get; set; }

    /// <summary>
    ///     Roles that stop a member from using the menu. Empty means none.
    /// </summary>
    public List<ulong> BlockedRoleIds { get; set; } = [];

    /// <summary>
    ///     Seconds a role from this menu lasts before it is taken back. 0 means it stays.
    /// </summary>
    public int TempRoleSeconds { get; set; }

    /// <summary>
    ///     When the menu deletes its message and turns itself off, in UTC. Null means never.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    ///     Group the menu is linked into, or null or 0 for none.
    /// </summary>
    public int? GroupId { get; set; }

    /// <summary>
    ///     RGB colour of the default message as a number from 0 to 16777215. Null means the bot's usual colour.
    /// </summary>
    public int? AccentColor { get; set; }

    /// <summary>
    ///     Button colour for options left on grey: 1 blurple, 3 green, 4 red, 0 keeps grey.
    /// </summary>
    public int ButtonColor { get; set; }

    /// <summary>
    ///     0 private confirmation, 1 no confirmation.
    /// </summary>
    public int ReplyMode { get; set; }

    /// <summary>
    ///     False to save the menu paused.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Options in display order.
    /// </summary>
    public List<RoleMenuOptionRequest> Options { get; set; } = [];
}

/// <summary>
///     One option on a role menu request.
/// </summary>
public class RoleMenuOptionRequest
{
    /// <summary>
    ///     An existing option ID to keep, or null or 0 for a new option.
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    ///     The role the option gives and takes.
    /// </summary>
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Text on the option. Blank means the role name.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    ///     Unicode emoji or custom emoji text such as &lt;:name:id&gt;.
    /// </summary>
    public string? Emoji { get; set; }

    /// <summary>
    ///     Short text under the dropdown option and in the default message.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Button color: 1 blurple, 2 grey, 3 green, 4 red.
    /// </summary>
    public int ButtonStyle { get; set; } = 2;

    /// <summary>
    ///     Seconds this role lasts before it is taken back. 0 uses the menu's setting.
    /// </summary>
    public int TempRoleSeconds { get; set; }

    /// <summary>
    ///     Most members who can hold this role through the menu. 0 means no limit.
    /// </summary>
    public int MaxHolders { get; set; }
}

/// <summary>
///     Pauses or resumes a menu.
/// </summary>
public class RoleMenuEnabledRequest
{
    /// <summary>
    ///     False to pause, true to resume.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
///     A new option order for a menu.
/// </summary>
public class RoleMenuOrderRequest
{
    /// <summary>
    ///     Every option ID of the menu exactly once, in the new order.
    /// </summary>
    public List<int> OptionIds { get; set; } = [];
}

/// <summary>
///     Posts a fresh copy of a menu.
/// </summary>
public class RoleMenuRepostRequest
{
    /// <summary>
    ///     Where to post it. Null or 0 means the menu's current channel.
    /// </summary>
    public ulong? ChannelId { get; set; }
}

/// <summary>
///     Moves an older emoji role setup into a new role menu.
/// </summary>
public class RoleMenuImportRequest
{
    /// <summary>
    ///     ID of the older setup, from the import sources list.
    /// </summary>
    public int SourceId { get; set; }

    /// <summary>
    ///     0 dropdown, 1 buttons.
    /// </summary>
    public int Style { get; set; }

    /// <summary>
    ///     Channel for the new menu. Null or 0 means the older setup's channel.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     Name for the new menu. Blank means the default name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Whether to copy the older message's text and embeds.
    /// </summary>
    public bool CopyMessage { get; set; } = true;

    /// <summary>
    ///     Whether to remove the older setup once the new menu is posted.
    /// </summary>
    public bool RetireOriginal { get; set; } = true;
}

/// <summary>
///     Creates a premade role menu and its roles in one go.
/// </summary>
public class RoleMenuTemplateRequest
{
    /// <summary>
    ///     Channel to post the menu in.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     0 dropdown, 1 buttons, 2 reactions. Null uses the template's own style.
    /// </summary>
    public int? Style { get; set; }

    /// <summary>
    ///     Menu name. Blank uses the template's name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Message above the menu. Blank uses the default message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    ///     The roles to create. Null uses the template's own roles.
    /// </summary>
    public List<RoleMenuTemplateRoleRequest>? Roles { get; set; }
}

/// <summary>
///     One role a premade role menu creates.
/// </summary>
public class RoleMenuTemplateRoleRequest
{
    /// <summary>
    ///     Role name and the name shown on the menu.
    /// </summary>
    public string Label { get; set; } = "";

    /// <summary>
    ///     Emoji shown beside the name.
    /// </summary>
    public string? Emoji { get; set; }

    /// <summary>
    ///     Role colour as an RGB value, or null for no colour.
    /// </summary>
    public int? Color { get; set; }
}

/// <summary>
///     Creates or changes a role menu group.
/// </summary>
public class RoleMenuGroupRequest
{
    /// <summary>
    ///     Group name. Ignored when changing an existing group.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Most roles a member can hold across the group's menus. 1 means one at a time, 0 means no limit.
    /// </summary>
    public int MaxRoles { get; set; } = 1;
}

/// <summary>
///     Creates a channel for role menus.
/// </summary>
public class RoleMenuChannelRequest
{
    /// <summary>
    ///     Channel name.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    ///     Category to put the channel in. Null or 0 means none.
    /// </summary>
    public ulong? CategoryId { get; set; }
}
