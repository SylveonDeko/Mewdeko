using Discord.Interactions;

namespace Mewdeko.Modules.RoleMenus.Common;

/// <summary>
///     The premade role menus that create their roles and menu together.
/// </summary>
public enum RoleMenuTemplateKind
{
    /// <summary>
    ///     One coloured role per colour, pick one.
    /// </summary>
    [ChoiceDisplay("Colours")]
    Colors = 0,

    /// <summary>
    ///     Pronoun roles, pick any.
    /// </summary>
    [ChoiceDisplay("Pronouns")]
    Pronouns = 1,

    /// <summary>
    ///     Mentionable roles for announcement pings, pick any.
    /// </summary>
    [ChoiceDisplay("Notification pings")]
    Pings = 2,

    /// <summary>
    ///     One role per continent, pick one.
    /// </summary>
    [ChoiceDisplay("Regions")]
    Regions = 3,

    /// <summary>
    ///     Age range roles, pick one.
    /// </summary>
    [ChoiceDisplay("Age ranges")]
    Ages = 4,

    /// <summary>
    ///     A single role given by one press.
    /// </summary>
    [ChoiceDisplay("Verification")]
    Verification = 5
}

/// <summary>
///     One role a template creates and offers on its menu.
/// </summary>
/// <param name="Label">The role name and the name shown on the menu.</param>
/// <param name="Emoji">The unicode emoji shown beside the name.</param>
/// <param name="Color">The role colour as an RGB value, or null for no colour.</param>
public sealed record RoleMenuTemplateEntry(string Label, string Emoji, int? Color = null);

/// <summary>
///     A premade role menu.
/// </summary>
/// <param name="Kind">Which template this is.</param>
/// <param name="Name">The menu name.</param>
/// <param name="Style">How the menu shows its options unless the person picks another style.</param>
/// <param name="Mode">How many roles a member can hold from the menu.</param>
/// <param name="Mentionable">Whether the created roles can be mentioned by anyone.</param>
/// <param name="Entries">The roles to create.</param>
public sealed record RoleMenuTemplate(
    RoleMenuTemplateKind Kind,
    string Name,
    RoleMenuStyle Style,
    RoleMenuMode Mode,
    bool Mentionable,
    IReadOnlyList<RoleMenuTemplateEntry> Entries);

/// <summary>
///     The catalogue of premade role menus.
/// </summary>
public static class RoleMenuTemplates
{
    /// <summary>
    ///     Every premade role menu.
    /// </summary>
    public static readonly IReadOnlyList<RoleMenuTemplate> All =
    [
        new(RoleMenuTemplateKind.Colors, "Colours", RoleMenuStyle.Dropdown, RoleMenuMode.Exclusive, false,
        [
            new("Red", "🔴", 0xE74C3C),
            new("Orange", "🟠", 0xE67E22),
            new("Yellow", "🟡", 0xF1C40F),
            new("Green", "🟢", 0x2ECC71),
            new("Teal", "🌊", 0x1ABC9C),
            new("Blue", "🔵", 0x3498DB),
            new("Purple", "🟣", 0x9B59B6),
            new("Pink", "🌸", 0xFF69B4),
            new("Brown", "🟤", 0x8B5A2B),
            new("White", "⚪", 0xECF0F1)
        ]),
        new(RoleMenuTemplateKind.Pronouns, "Pronouns", RoleMenuStyle.Buttons, RoleMenuMode.Multi, false,
        [
            new("He/Him", "🙂"),
            new("She/Her", "🙃"),
            new("They/Them", "😀"),
            new("Any Pronouns", "🌈"),
            new("Ask Me", "❓")
        ]),
        new(RoleMenuTemplateKind.Pings, "Notification Pings", RoleMenuStyle.Buttons, RoleMenuMode.Multi, true,
        [
            new("Announcements", "📢"),
            new("News", "📰"),
            new("Events", "🎉"),
            new("Giveaways", "🎁"),
            new("Polls", "📊"),
            new("Updates", "🔧")
        ]),
        new(RoleMenuTemplateKind.Regions, "Regions", RoleMenuStyle.Dropdown, RoleMenuMode.Exclusive, false,
        [
            new("North America", "🦅"),
            new("South America", "🌴"),
            new("Europe", "🏰"),
            new("Africa", "🦁"),
            new("Asia", "🏯"),
            new("Oceania", "🦘"),
            new("Antarctica", "🐧")
        ]),
        new(RoleMenuTemplateKind.Ages, "Age Ranges", RoleMenuStyle.Buttons, RoleMenuMode.Exclusive, false,
        [
            new("13-17", "🌱"),
            new("18-24", "🌿"),
            new("25-34", "🌳"),
            new("35 and over", "🍂")
        ]),
        new(RoleMenuTemplateKind.Verification, "Verification", RoleMenuStyle.Buttons, RoleMenuMode.Verify, false,
        [
            new("Verified", "✅", 0x2ECC71)
        ])
    ];

    /// <summary>
    ///     Finds a template by kind.
    /// </summary>
    /// <param name="kind">The template kind.</param>
    /// <returns>The template.</returns>
    public static RoleMenuTemplate Get(RoleMenuTemplateKind kind)
    {
        return All.First(x => x.Kind == kind);
    }
}

/// <summary>
///     A template being edited by one person before it is posted. Nothing is created until it is posted.
/// </summary>
public sealed class RoleMenuTemplateSession
{
    /// <summary>
    ///     Gets the template this session started from.
    /// </summary>
    public required RoleMenuTemplate Template { get; init; }

    /// <summary>
    ///     Gets or sets the channel the menu will be posted in.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets how the menu shows its options.
    /// </summary>
    public RoleMenuStyle Style { get; set; }

    /// <summary>
    ///     Gets or sets the menu name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the message above the menu, or null for the default one.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    ///     Gets or sets the roles that will be created.
    /// </summary>
    public List<RoleMenuTemplateEntry> Entries { get; set; } = [];

    /// <summary>
    ///     Gets or sets when this session stops being valid.
    /// </summary>
    public DateTime Expires { get; set; }
}
