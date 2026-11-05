using DataModel;

namespace Mewdeko.Modules.Automations.Common;

/// <summary>
///     The settings of an automation as they are created or changed.
/// </summary>
public sealed class AutomationInput
{
    /// <summary>
    ///     Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the trigger.
    /// </summary>
    public AutomationTriggerType Trigger { get; set; }

    /// <summary>
    ///     Gets or sets the conditions.
    /// </summary>
    public List<AutomationNode> Conditions { get; set; } = [];

    /// <summary>
    ///     Gets or sets the actions.
    /// </summary>
    public List<AutomationNode> Actions { get; set; } = [];

    /// <summary>
    ///     Gets or sets whether the automation runs.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the cooldown in seconds, or 0 for none.
    /// </summary>
    public int CooldownSeconds { get; set; }

    /// <summary>
    ///     Gets or sets what the cooldown is counted per.
    /// </summary>
    public AutomationCooldownScope CooldownScope { get; set; }

    /// <summary>
    ///     Gets or sets whether transient failures are tried again.
    /// </summary>
    public bool RetryOnFailure { get; set; }

    /// <summary>
    ///     Gets or sets whether events caused by bots are ignored.
    /// </summary>
    public bool IgnoreBots { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether messages may mention everyone, here and roles.
    /// </summary>
    public bool AllowMassMentions { get; set; }
}

/// <summary>
///     Something a template needs to be told, such as the channel to post in.
/// </summary>
/// <param name="Name">The parameter name: channel, role or user.</param>
/// <param name="Kind">The kind of value.</param>
/// <param name="Description">What it is used for.</param>
/// <param name="Required">Whether it has to be given.</param>
public sealed record AutomationTemplateParameter(string Name, AutomationFieldKind Kind, string Description,
    bool Required);

/// <summary>
///     A ready-made automation, or set of them, to start from.
/// </summary>
/// <param name="Id">The template ID.</param>
/// <param name="Name">A short name.</param>
/// <param name="Description">What it does.</param>
/// <param name="Parameters">What it needs to be told.</param>
public sealed record AutomationTemplate(string Id, string Name, string Description,
    IReadOnlyList<AutomationTemplateParameter> Parameters);

/// <summary>
///     The automations a template makes, and anything it does once when it is created.
/// </summary>
/// <param name="Automations">The automations to create.</param>
/// <param name="Setup">Actions run once after creating, such as posting a message with a button.</param>
public sealed record AutomationTemplateResult(List<AutomationInput> Automations, List<AutomationNode> Setup);

/// <summary>
///     Why a template could not be built.
/// </summary>
public enum AutomationTemplateError
{
    /// <summary>
    ///     Nothing is wrong.
    /// </summary>
    None,

    /// <summary>
    ///     There is no template with that ID.
    /// </summary>
    UnknownTemplate,

    /// <summary>
    ///     A required parameter was not given.
    /// </summary>
    MissingParameter,

    /// <summary>
    ///     The server has no booster role and none was given.
    /// </summary>
    NoBoosterRole,

    /// <summary>
    ///     The role is not in the server.
    /// </summary>
    UnknownRole,

    /// <summary>
    ///     The built automation did not pass validation.
    /// </summary>
    Invalid
}

/// <summary>
///     The starter templates.
/// </summary>
public static class AutomationTemplates
{
    private static readonly AutomationTemplateParameter Channel = new("channel", AutomationFieldKind.Channel,
        "The channel to post in.", true);

    /// <summary>
    ///     Every template.
    /// </summary>
    public static readonly IReadOnlyList<AutomationTemplate> All =
    [
        new("thank-boosters", "Thank boosters", "Thanks members in a channel when they start boosting.",
        [
            Channel,
            new("role", AutomationFieldKind.Role, "The booster role. Found automatically when left out.", false)
        ]),
        new("welcome", "Welcome message", "Welcomes each new member in a channel.", [Channel]),
        new("thread-joiner", "Thread joiner", "Adds a member to every new thread.",
            [new("user", AutomationFieldKind.User, "The member to add.", true)]),
        new("starboard", "Starboard repost", "Reposts a message in a channel once it gets three star reactions.",
            [Channel]),
        new("button-role", "Button role toggle", "Posts a button that gives a role, or takes it back when pressed again.",
        [
            Channel,
            new("role", AutomationFieldKind.Role, "The role to toggle.", true)
        ])
    ];

    /// <summary>
    ///     Finds a template by its ID.
    /// </summary>
    /// <param name="id">The template ID.</param>
    public static AutomationTemplate? Find(string id) =>
        All.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Builds the automations of a template.
    /// </summary>
    /// <param name="id">The template ID.</param>
    /// <param name="guild">The guild they are for.</param>
    /// <param name="values">The parameters, keyed by name.</param>
    /// <returns>The result, or why it could not be built and which parameter was at fault.</returns>
    public static (AutomationTemplateResult? Result, AutomationTemplateError Error, string? Detail) Build(string id,
        SocketGuild guild,
        IReadOnlyDictionary<string, ulong> values)
    {
        var template = Find(id);
        if (template is null)
            return (null, AutomationTemplateError.UnknownTemplate, null);

        foreach (var parameter in template.Parameters.Where(x => x.Required))
        {
            if (!values.TryGetValue(parameter.Name, out var value) || value == 0)
                return (null, AutomationTemplateError.MissingParameter, parameter.Name);
        }

        var channelId = values.GetValueOrDefault("channel");
        var roleId = values.GetValueOrDefault("role");

        switch (template.Id)
        {
            case "thank-boosters":
            {
                if (roleId == 0)
                    roleId = guild.Roles.FirstOrDefault(x => x.Tags?.IsPremiumSubscriberRole == true)?.Id ?? 0;
                if (roleId == 0)
                    return (null, AutomationTemplateError.NoBoosterRole, null);

                return (Single(new AutomationInput
                {
                    Name = "Thank boosters",
                    Trigger = AutomationTriggerType.RoleGained,
                    Conditions = [AutomationNode.Create("TriggerRoleIs", ("roleIds", new[] { roleId }))],
                    Actions =
                    [
                        AutomationNode.Create("SendMessage", ("channelId", channelId),
                            ("text", "%user.mention% just boosted %server%. Thank you!"))
                    ]
                }), AutomationTemplateError.None, null);
            }
            case "welcome":
                return (Single(new AutomationInput
                {
                    Name = "Welcome message",
                    Trigger = AutomationTriggerType.MemberJoined,
                    Actions =
                    [
                        AutomationNode.Create("SendMessage", ("channelId", channelId),
                            ("text", "Welcome to %server%, %user.mention%!"))
                    ]
                }), AutomationTemplateError.None, null);
            case "thread-joiner":
                return (Single(new AutomationInput
                {
                    Name = "Thread joiner",
                    Trigger = AutomationTriggerType.ThreadCreated,
                    Actions =
                    [
                        AutomationNode.Create("AddThreadMembers", ("userIds", new[] { values["user"] }))
                    ]
                }), AutomationTemplateError.None, null);
            case "starboard":
                return (Single(new AutomationInput
                {
                    Name = "Starboard repost",
                    Trigger = AutomationTriggerType.ReactionAdded,
                    Conditions =
                    [
                        AutomationNode.Create("ReactionEmojiIn", ("emojis", new[] { "⭐" })),
                        AutomationNode.Create("ReactionCount", ("comparison", "Equal"), ("count", 3))
                    ],
                    Actions = [AutomationNode.Create("RepostMessage", ("channelId", channelId), ("includeLink", true))]
                }), AutomationTemplateError.None, null);
            case "button-role":
            {
                var role = guild.GetRole(roleId);
                if (role is null)
                    return (null, AutomationTemplateError.UnknownRole, null);

                var customId = $"role-{roleId}";
                var on = new AutomationInput
                {
                    Name = $"Button role: give {role.Name}".TrimTo(AutomationLimits.MaxNameLength),
                    Trigger = AutomationTriggerType.ButtonClicked,
                    Conditions =
                    [
                        AutomationNode.Create("ButtonCustomIdIs", ("customId", customId)),
                        AutomationNode.Create("LacksAnyRole", ("roleIds", new[] { roleId }))
                    ],
                    Actions =
                    [
                        AutomationNode.Create("GiveRoles", ("roleIds", new[] { roleId })),
                        AutomationNode.Create("SendMessage", ("text", "You now have the role."),
                            ("ephemeral", true))
                    ]
                };

                var off = new AutomationInput
                {
                    Name = $"Button role: take {role.Name}".TrimTo(AutomationLimits.MaxNameLength),
                    Trigger = AutomationTriggerType.ButtonClicked,
                    Conditions =
                    [
                        AutomationNode.Create("ButtonCustomIdIs", ("customId", customId)),
                        AutomationNode.Create("HasAnyRole", ("roleIds", new[] { roleId }))
                    ],
                    Actions =
                    [
                        AutomationNode.Create("TakeRoles", ("roleIds", new[] { roleId })),
                        AutomationNode.Create("SendMessage", ("text", "The role was taken away."),
                            ("ephemeral", true))
                    ]
                };

                var button = new[]
                {
                    new Dictionary<string, string>
                    {
                        ["customId"] = customId,
                        ["label"] = role.Name.TrimTo(80),
                        ["style"] = "Primary"
                    }
                };

                return (new AutomationTemplateResult([on, off],
                [
                    AutomationNode.Create("SendMessage", ("channelId", channelId),
                        ("text", $"Press the button to get or drop {role.Name}."), ("buttons", button))
                ]), AutomationTemplateError.None, null);
            }
            default:
                return (null, AutomationTemplateError.UnknownTemplate, null);
        }
    }

    private static AutomationTemplateResult Single(AutomationInput input) => new([input], []);
}
