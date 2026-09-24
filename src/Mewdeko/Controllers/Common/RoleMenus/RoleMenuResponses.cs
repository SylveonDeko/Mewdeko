namespace Mewdeko.Controllers.Common.RoleMenus;

/// <summary>
///     A role menu with its options and posting state.
/// </summary>
public class RoleMenuResponse
{
    /// <summary>
    ///     Menu ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Internal name.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    ///     Channel the message lives in.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Channel name, or null when the channel is gone.
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    ///     Posted message ID, or null when not posted.
    /// </summary>
    public ulong? MessageId { get; set; }

    /// <summary>
    ///     Link to the posted message, or null when not posted.
    /// </summary>
    public string? JumpUrl { get; set; }

    /// <summary>
    ///     Raw stored message source: plain text or embed builder JSON. Null means the default message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    ///     0 dropdown, 1 buttons.
    /// </summary>
    public int Style { get; set; }

    /// <summary>
    ///     Dropdown hint text, or null for the default.
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    ///     0 pick any, 1 pick one.
    /// </summary>
    public int Mode { get; set; }

    /// <summary>
    ///     Fewest roles a member must keep once they pick.
    /// </summary>
    public int MinRoles { get; set; }

    /// <summary>
    ///     Most roles a member can hold. 0 means no limit.
    /// </summary>
    public int MaxRoles { get; set; }

    /// <summary>
    ///     Role needed to use the menu, or null for anyone.
    /// </summary>
    public ulong? RequiredRoleId { get; set; }

    /// <summary>
    ///     0 private confirmation, 1 no confirmation.
    /// </summary>
    public int ReplyMode { get; set; }

    /// <summary>
    ///     False when paused.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     "live", "paused", "not_posted", or "channel_missing".
    /// </summary>
    public string Status { get; set; } = "live";

    /// <summary>
    ///     Discord user ID of the creator, 0 when unknown.
    /// </summary>
    public ulong CreatedBy { get; set; }

    /// <summary>
    ///     When the menu was created, UTC.
    /// </summary>
    public DateTime DateAdded { get; set; }

    /// <summary>
    ///     When the menu last changed, UTC.
    /// </summary>
    public DateTime DateModified { get; set; }

    /// <summary>
    ///     Options sorted by position.
    /// </summary>
    public List<RoleMenuOptionResponse> Options { get; set; } = [];
}

/// <summary>
///     One option on a role menu.
/// </summary>
public class RoleMenuOptionResponse
{
    /// <summary>
    ///     Option ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The role the option gives and takes.
    /// </summary>
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Role name, or null when the role is gone.
    /// </summary>
    public string? RoleName { get; set; }

    /// <summary>
    ///     Role color as a 24 bit RGB value, 0 when none.
    /// </summary>
    public uint RoleColor { get; set; }

    /// <summary>
    ///     Text on the option.
    /// </summary>
    public string Label { get; set; } = null!;

    /// <summary>
    ///     Unicode emoji or custom emoji text, or null.
    /// </summary>
    public string? Emoji { get; set; }

    /// <summary>
    ///     Short description, or null.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Button color: 1 blurple, 2 grey, 3 green, 4 red.
    /// </summary>
    public int ButtonStyle { get; set; }

    /// <summary>
    ///     0-based position.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    ///     Why the bot can't give out this role, or null when it can.
    /// </summary>
    public string? Problem { get; set; }
}

/// <summary>
///     Everything the menu editor needs to offer channels, roles, and emojis.
/// </summary>
public class RoleMenuLookupsResponse
{
    /// <summary>
    ///     Text and announcement channels, in Discord order.
    /// </summary>
    public List<RoleMenuChannelLookup> Channels { get; set; } = [];

    /// <summary>
    ///     Every role except the everyone role, highest first.
    /// </summary>
    public List<RoleMenuRoleLookup> Roles { get; set; } = [];

    /// <summary>
    ///     This server's available custom emojis.
    /// </summary>
    public List<RoleMenuEmojiLookup> Emojis { get; set; } = [];

    /// <summary>
    ///     Whether the bot has Manage Roles.
    /// </summary>
    public bool BotCanManageRoles { get; set; }

    /// <summary>
    ///     How many menus the server has.
    /// </summary>
    public int MenuCount { get; set; }

    /// <summary>
    ///     Size limits enforced by the bot.
    /// </summary>
    public RoleMenuLimitsLookup Limits { get; set; } = new();
}

/// <summary>
///     A channel the menu editor can offer.
/// </summary>
public class RoleMenuChannelLookup
{
    /// <summary>
    ///     Channel ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     Channel name.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    ///     Category name, or null when uncategorized.
    /// </summary>
    public string? CategoryName { get; set; }

    /// <summary>
    ///     Channel position.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    ///     Whether the bot can post a menu here.
    /// </summary>
    public bool CanPost { get; set; }

    /// <summary>
    ///     Why the bot can't post here, or null when it can.
    /// </summary>
    public string? Problem { get; set; }
}

/// <summary>
///     A role the menu editor can offer.
/// </summary>
public class RoleMenuRoleLookup
{
    /// <summary>
    ///     Role ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     Role name.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    ///     Role color as a 24 bit RGB value, 0 when none.
    /// </summary>
    public uint Color { get; set; }

    /// <summary>
    ///     Role position.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    ///     Whether the editing member can put this role on a menu.
    /// </summary>
    public bool Assignable { get; set; }

    /// <summary>
    ///     Why the role can't be offered, or null when it can.
    /// </summary>
    public string? Problem { get; set; }
}

/// <summary>
///     A custom emoji the menu editor can offer.
/// </summary>
public class RoleMenuEmojiLookup
{
    /// <summary>
    ///     Emoji ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     Emoji name.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    ///     Whether the emoji is animated.
    /// </summary>
    public bool Animated { get; set; }

    /// <summary>
    ///     Emoji text to store on an option, such as &lt;:name:id&gt;.
    /// </summary>
    public string Formatted { get; set; } = null!;

    /// <summary>
    ///     Image URL.
    /// </summary>
    public string Url { get; set; } = null!;
}

/// <summary>
///     Size limits enforced by the bot.
/// </summary>
public class RoleMenuLimitsLookup
{
    /// <summary>
    ///     Most menus per server.
    /// </summary>
    public int MaxMenus { get; set; }

    /// <summary>
    ///     Most options per menu.
    /// </summary>
    public int MaxOptions { get; set; }

    /// <summary>
    ///     Buttons per row.
    /// </summary>
    public int ButtonsPerRow { get; set; }

    /// <summary>
    ///     Longest menu name.
    /// </summary>
    public int NameLength { get; set; }

    /// <summary>
    ///     Longest option label.
    /// </summary>
    public int LabelLength { get; set; }

    /// <summary>
    ///     Longest option description.
    /// </summary>
    public int DescriptionLength { get; set; }

    /// <summary>
    ///     Longest dropdown placeholder.
    /// </summary>
    public int PlaceholderLength { get; set; }
}

/// <summary>
///     An older emoji role setup that can be moved into a role menu.
/// </summary>
public class RoleMenuImportSourceResponse
{
    /// <summary>
    ///     ID of the older setup.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Channel of the older message.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Channel name, or null when the channel is gone.
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    ///     ID of the older message.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    ///     Link to the older message.
    /// </summary>
    public string JumpUrl { get; set; } = null!;

    /// <summary>
    ///     Whether members could hold only one role from the setup.
    /// </summary>
    public bool Exclusive { get; set; }

    /// <summary>
    ///     Emoji and role pairs in the setup.
    /// </summary>
    public List<RoleMenuImportPair> Pairs { get; set; } = [];
}

/// <summary>
///     One emoji and role pair in an older setup.
/// </summary>
public class RoleMenuImportPair
{
    /// <summary>
    ///     The emoji text as stored.
    /// </summary>
    public string Emoji { get; set; } = "";

    /// <summary>
    ///     The role ID.
    /// </summary>
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Role name, or null when the role is gone.
    /// </summary>
    public string? RoleName { get; set; }

    /// <summary>
    ///     Whether the role still exists.
    /// </summary>
    public bool RoleExists { get; set; }
}
