using System.Text.Json.Serialization;
using DataModel;

namespace Mewdeko.Modules.Automations.Common;

/// <summary>
///     The limits that keep automations small and safe.
/// </summary>
public static class AutomationLimits
{
    /// <summary>
    ///     The most conditions one automation may have.
    /// </summary>
    public const int MaxConditions = 25;

    /// <summary>
    ///     The most actions one automation may have, and the most one run will perform.
    /// </summary>
    public const int MaxActions = 15;

    /// <summary>
    ///     The longest name an automation may have.
    /// </summary>
    public const int MaxNameLength = 100;

    /// <summary>
    ///     The most items a list field may hold.
    /// </summary>
    public const int MaxListItems = 25;

    /// <summary>
    ///     The longest text field, in characters.
    /// </summary>
    public const int MaxTextLength = 2000;

    /// <summary>
    ///     The longest embed JSON, in characters.
    /// </summary>
    public const int MaxEmbedLength = 8000;

    /// <summary>
    ///     The longest regex pattern, in characters.
    /// </summary>
    public const int MaxPatternLength = 200;

    /// <summary>
    ///     The longest a single wait action may be, in seconds.
    /// </summary>
    public const int MaxWaitSeconds = 60;

    /// <summary>
    ///     The most seconds all wait actions of one automation may add up to.
    /// </summary>
    public const int MaxTotalWaitSeconds = 120;

    /// <summary>
    ///     The longest cooldown, in seconds.
    /// </summary>
    public const int MaxCooldownSeconds = 86400;

    /// <summary>
    ///     The most buttons one message may carry.
    /// </summary>
    public const int MaxButtons = 5;

    /// <summary>
    ///     The most coins or XP one action may give or take.
    /// </summary>
    public const int MaxRewardAmount = 100_000_000;

    /// <summary>
    ///     The most runs one guild may start in a minute.
    /// </summary>
    public const int MaxRunsPerMinute = 30;

    /// <summary>
    ///     How many runs are kept in each automation's log.
    /// </summary>
    public const int RunsKept = 50;

    /// <summary>
    ///     How many times an action is tried when retrying is on, counting the first try.
    /// </summary>
    public const int MaxAttempts = 3;

    /// <summary>
    ///     The prefix put on the custom IDs of buttons an automation posts.
    /// </summary>
    public const string ButtonPrefix = "automation:";
}

/// <summary>
///     What an event gives an automation to work with.
/// </summary>
[Flags]
public enum AutomationContext
{
    /// <summary>
    ///     Nothing.
    /// </summary>
    None = 0,

    /// <summary>
    ///     A member.
    /// </summary>
    User = 1,

    /// <summary>
    ///     A channel.
    /// </summary>
    Channel = 2,

    /// <summary>
    ///     A message.
    /// </summary>
    Message = 4,

    /// <summary>
    ///     A reaction.
    /// </summary>
    Reaction = 8,

    /// <summary>
    ///     A clicked button.
    /// </summary>
    Button = 16,

    /// <summary>
    ///     A role that was gained or lost.
    /// </summary>
    Role = 32,

    /// <summary>
    ///     A thread.
    /// </summary>
    Thread = 64
}

/// <summary>
///     The kind of value a field holds, which decides how a dashboard shows it and how it is checked.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AutomationFieldKind
{
    /// <summary>
    ///     A short piece of text.
    /// </summary>
    Text,

    /// <summary>
    ///     A long piece of text with placeholders.
    /// </summary>
    LongText,

    /// <summary>
    ///     Embed JSON as made by the embed builder.
    /// </summary>
    EmbedJson,

    /// <summary>
    ///     A whole number.
    /// </summary>
    Integer,

    /// <summary>
    ///     True or false.
    /// </summary>
    Boolean,

    /// <summary>
    ///     A channel ID.
    /// </summary>
    Channel,

    /// <summary>
    ///     A role ID.
    /// </summary>
    Role,

    /// <summary>
    ///     A member ID.
    /// </summary>
    User,

    /// <summary>
    ///     An emoji, either the character or a custom emoji.
    /// </summary>
    Emoji,

    /// <summary>
    ///     A list of channel or category IDs.
    /// </summary>
    ChannelList,

    /// <summary>
    ///     A list of role IDs.
    /// </summary>
    RoleList,

    /// <summary>
    ///     A list of member IDs.
    /// </summary>
    UserList,

    /// <summary>
    ///     A list of emojis.
    /// </summary>
    EmojiList,

    /// <summary>
    ///     One value out of a fixed set.
    /// </summary>
    Choice,

    /// <summary>
    ///     Any number of values out of a fixed set.
    /// </summary>
    ChoiceList,

    /// <summary>
    ///     A list of buttons with a custom ID, label, style and optional emoji.
    /// </summary>
    Buttons
}

/// <summary>
///     Describes one field of a condition or action.
/// </summary>
/// <param name="Name">The field name used in JSON.</param>
/// <param name="Kind">The kind of value.</param>
/// <param name="Description">What the field does.</param>
/// <param name="Required">Whether the field has to be filled in.</param>
/// <param name="Min">The smallest number, or the fewest list items.</param>
/// <param name="Max">The largest number, or the longest text.</param>
/// <param name="Options">The allowed values of a choice.</param>
/// <param name="Default">The value used when the field is left out.</param>
public sealed record AutomationFieldSpec(
    string Name,
    AutomationFieldKind Kind,
    string Description,
    bool Required = false,
    long? Min = null,
    long? Max = null,
    string[]? Options = null,
    object? Default = null);

/// <summary>
///     Describes a trigger.
/// </summary>
/// <param name="Type">The trigger type.</param>
/// <param name="Name">A short name for pickers.</param>
/// <param name="Description">When the trigger fires.</param>
/// <param name="Provides">What the event gives conditions and actions to work with.</param>
public sealed record AutomationTriggerSpec(
    AutomationTriggerType Type,
    string Name,
    string Description,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    AutomationContext Provides);

/// <summary>
///     Describes a condition or an action.
/// </summary>
/// <param name="Type">The type used in JSON.</param>
/// <param name="Name">A short name for pickers.</param>
/// <param name="Description">What it does.</param>
/// <param name="Requires">What the trigger has to provide for it to be usable.</param>
/// <param name="Fields">The fields it takes.</param>
public sealed record AutomationNodeSpec(
    string Type,
    string Name,
    string Description,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    AutomationContext Requires,
    IReadOnlyList<AutomationFieldSpec> Fields);

/// <summary>
///     Lists every trigger, condition and action with its fields, so editors can be built from it.
/// </summary>
public static class AutomationSchema
{
    /// <summary>
    ///     The allowed button styles.
    /// </summary>
    public static readonly string[] ButtonStyles = ["Primary", "Secondary", "Success", "Danger"];

    /// <summary>
    ///     The allowed attachment types.
    /// </summary>
    public static readonly string[] AttachmentTypes = ["Image", "Video", "Audio", "File"];

    /// <summary>
    ///     The allowed comparisons.
    /// </summary>
    public static readonly string[] Comparisons = ["GreaterOrEqual", "LessOrEqual", "Equal"];

    /// <summary>
    ///     The allowed targets of actions that affect a member.
    /// </summary>
    public static readonly string[] Targets = ["TriggerUser", "MessageAuthor"];

    /// <summary>
    ///     The allowed thread archive times.
    /// </summary>
    public static readonly string[] ArchiveTimes = ["OneHour", "OneDay", "ThreeDays", "OneWeek"];

    private const AutomationContext Everything = AutomationContext.User | AutomationContext.Channel |
                                                 AutomationContext.Message;

    /// <summary>
    ///     Every trigger.
    /// </summary>
    public static readonly IReadOnlyList<AutomationTriggerSpec> Triggers =
    [
        new(AutomationTriggerType.MessageSent, "Message sent", "A member sends a message.", Everything),
        new(AutomationTriggerType.MessageEdited, "Message edited", "A member edits a message.", Everything),
        new(AutomationTriggerType.MessageDeleted, "Message deleted",
            "A message is deleted. Its text and author are only known when the bot had seen it.", Everything),
        new(AutomationTriggerType.ReactionAdded, "Reaction added", "A member adds a reaction to a message.",
            Everything | AutomationContext.Reaction),
        new(AutomationTriggerType.ReactionRemoved, "Reaction removed", "A member removes a reaction from a message.",
            Everything | AutomationContext.Reaction),
        new(AutomationTriggerType.ButtonClicked, "Button clicked",
            "A member clicks a button that an automation posted.", Everything | AutomationContext.Button),
        new(AutomationTriggerType.MemberJoined, "Member joined", "A member joins the server.",
            AutomationContext.User),
        new(AutomationTriggerType.MemberLeft, "Member left", "A member leaves the server.", AutomationContext.User),
        new(AutomationTriggerType.RoleGained, "Role gained", "A member gains a role.",
            AutomationContext.User | AutomationContext.Role),
        new(AutomationTriggerType.RoleLost, "Role lost", "A member loses a role.",
            AutomationContext.User | AutomationContext.Role),
        new(AutomationTriggerType.VoiceJoined, "Voice joined", "A member joins a voice channel.",
            AutomationContext.User | AutomationContext.Channel),
        new(AutomationTriggerType.VoiceLeft, "Voice left", "A member leaves a voice channel.",
            AutomationContext.User | AutomationContext.Channel),
        new(AutomationTriggerType.ThreadCreated, "Thread created", "A thread is created.",
            AutomationContext.User | AutomationContext.Channel | AutomationContext.Thread)
    ];

    /// <summary>
    ///     Every condition.
    /// </summary>
    public static readonly IReadOnlyList<AutomationNodeSpec> Conditions =
    [
        new("InChannels", "In channels", "The event happened in one of these channels, threads or categories.",
            AutomationContext.Channel,
            [ListField("channelIds", AutomationFieldKind.ChannelList, "Channels or categories.")]),
        new("HasAnyRole", "Has any of roles", "The member has at least one of these roles.", AutomationContext.User,
            [ListField("roleIds", AutomationFieldKind.RoleList, "Roles.")]),
        new("HasAllRoles", "Has all roles", "The member has every one of these roles.", AutomationContext.User,
            [ListField("roleIds", AutomationFieldKind.RoleList, "Roles.")]),
        new("HasNoneOfRoles", "Has none of roles", "The member has none of these roles.", AutomationContext.User,
            [ListField("roleIds", AutomationFieldKind.RoleList, "Roles.")]),
        new("LacksAnyRole", "Lacks at least one of roles", "The member is missing at least one of these roles.",
            AutomationContext.User, [ListField("roleIds", AutomationFieldKind.RoleList, "Roles.")]),
        new("FromUsers", "From specific users", "The member is one of these users.", AutomationContext.User,
            [ListField("userIds", AutomationFieldKind.UserList, "Users.")]),
        new("MessageContains", "Message contains", "The message text contains this text.", AutomationContext.Message,
        [
            new("text", AutomationFieldKind.Text, "The text to look for.", true, Max: 500),
            new("caseSensitive", AutomationFieldKind.Boolean, "Match the exact letter case.", Default: false)
        ]),
        new("MessageEquals", "Message equals", "The message text is exactly this text.", AutomationContext.Message,
        [
            new("text", AutomationFieldKind.Text, "The text to match.", true, Max: 500),
            new("caseSensitive", AutomationFieldKind.Boolean, "Match the exact letter case.", Default: false)
        ]),
        new("MessageDoesNotContain", "Message does not contain", "The message text does not contain this text.",
            AutomationContext.Message,
        [
            new("text", AutomationFieldKind.Text, "The text to avoid.", true, Max: 500),
            new("caseSensitive", AutomationFieldKind.Boolean, "Match the exact letter case.", Default: false)
        ]),
        new("MessageMatchesRegex", "Message matches regex", "The message text matches this regular expression.",
            AutomationContext.Message,
            [new("pattern", AutomationFieldKind.Text, "The regular expression.", true, Max: AutomationLimits.MaxPatternLength)]),
        new("HasAttachment", "Has attachment of type", "The message has an attachment of one of these types.",
            AutomationContext.Message,
        [
            new("types", AutomationFieldKind.ChoiceList, "Attachment types.", true, 1, Options: AttachmentTypes)
        ]),
        new("HasText", "Has text", "The message has text.", AutomationContext.Message, []),
        new("IsReply", "Is a reply", "The message is a reply to another message.", AutomationContext.Message, []),
        new("IsNotReply", "Is not a reply", "The message is not a reply.", AutomationContext.Message, []),
        new("ReactionEmojiIn", "Reaction emoji is one of", "The reaction is one of these emojis.",
            AutomationContext.Reaction, [ListField("emojis", AutomationFieldKind.EmojiList, "Emojis.")]),
        new("ReactionCount", "Reaction count", "How many members used this reaction on the message.",
            AutomationContext.Reaction,
        [
            new("comparison", AutomationFieldKind.Choice, "How to compare.", true, Options: Comparisons),
            new("count", AutomationFieldKind.Integer, "The number to compare with.", true, 0, 100000)
        ]),
        new("ButtonCustomIdIs", "Button custom ID is", "The clicked button has this custom ID.",
            AutomationContext.Button,
            [new("customId", AutomationFieldKind.Text, "The custom ID.", true, Max: 60)]),
        new("TriggerRoleIs", "Role is one of", "The role that was gained or lost is one of these roles.",
            AutomationContext.Role, [ListField("roleIds", AutomationFieldKind.RoleList, "Roles.")])
    ];

    /// <summary>
    ///     Every action.
    /// </summary>
    public static readonly IReadOnlyList<AutomationNodeSpec> Actions =
    [
        new("SendMessage", "Send message", "Sends a message in a channel, with text, an embed and buttons.",
            AutomationContext.None,
        [
            new("channelId", AutomationFieldKind.Channel, "The channel. Leave empty for the channel of the event."),
            new("text", AutomationFieldKind.LongText, "The message text. Placeholders work.", Max: AutomationLimits.MaxTextLength),
            new("embed", AutomationFieldKind.EmbedJson, "Embed JSON. Placeholders work.", Max: AutomationLimits.MaxEmbedLength),
            new("buttons", AutomationFieldKind.Buttons, "Buttons that start automations with the button trigger.",
                Max: AutomationLimits.MaxButtons),
            new("ephemeral", AutomationFieldKind.Boolean,
                "Only the member who clicked sees it. Only for the button trigger.", Default: false)
        ]),
        new("SendDm", "Send DM", "Sends a direct message to a member.", AutomationContext.User,
        [
            TargetField(),
            new("text", AutomationFieldKind.LongText, "The message text. Placeholders work.", Max: AutomationLimits.MaxTextLength),
            new("embed", AutomationFieldKind.EmbedJson, "Embed JSON. Placeholders work.", Max: AutomationLimits.MaxEmbedLength)
        ]),
        new("ReplyToMessage", "Reply to the message", "Replies to the message that started the run.",
            AutomationContext.Message,
        [
            new("text", AutomationFieldKind.LongText, "The message text. Placeholders work.", Max: AutomationLimits.MaxTextLength),
            new("embed", AutomationFieldKind.EmbedJson, "Embed JSON. Placeholders work.", Max: AutomationLimits.MaxEmbedLength),
            new("pingAuthor", AutomationFieldKind.Boolean, "Ping the author in the reply.", Default: false)
        ]),
        new("RepostMessage", "Repost the message", "Posts a copy of the message in another channel.",
            AutomationContext.Message,
        [
            new("channelId", AutomationFieldKind.Channel, "The channel to post in.", true),
            new("includeLink", AutomationFieldKind.Boolean, "Add a link to the original message.", Default: true)
        ]),
        new("DeleteMessage", "Delete the message", "Deletes the message that started the run.",
            AutomationContext.Message, []),
        new("AddReactions", "Add reactions", "Adds reactions to the message.", AutomationContext.Message,
            [ListField("emojis", AutomationFieldKind.EmojiList, "Emojis.")]),
        new("RemoveTriggeringReaction", "Remove the reaction", "Removes the reaction that started the run.",
            AutomationContext.Reaction, []),
        new("GiveRoles", "Give roles", "Gives roles to a member.", AutomationContext.User,
        [
            TargetField(),
            ListField("roleIds", AutomationFieldKind.RoleList, "Roles.")
        ]),
        new("TakeRoles", "Take roles", "Takes roles from a member.", AutomationContext.User,
        [
            TargetField(),
            ListField("roleIds", AutomationFieldKind.RoleList, "Roles.")
        ]),
        new("CreateThread", "Create a thread", "Creates a thread, from the message when there is one.",
            AutomationContext.None,
        [
            new("channelId", AutomationFieldKind.Channel, "The channel. Leave empty for the channel of the event."),
            new("name", AutomationFieldKind.Text, "The thread name. Placeholders work.", true, Max: 100),
            new("firstMessage", AutomationFieldKind.LongText, "The first message in the thread. Placeholders work.",
                Max: AutomationLimits.MaxTextLength),
            new("fromMessage", AutomationFieldKind.Boolean, "Start the thread from the message when there is one.",
                Default: true),
            new("archiveAfter", AutomationFieldKind.Choice, "When the thread archives itself.",
                Options: ArchiveTimes, Default: "OneDay")
        ]),
        new("SendInThread", "Send in the thread", "Sends a message in the thread created earlier in the run.",
            AutomationContext.None,
        [
            new("text", AutomationFieldKind.LongText, "The message text. Placeholders work.", Max: AutomationLimits.MaxTextLength),
            new("embed", AutomationFieldKind.EmbedJson, "Embed JSON. Placeholders work.", Max: AutomationLimits.MaxEmbedLength)
        ]),
        new("AddThreadMembers", "Add members to the thread", "Adds members to the thread created earlier in the run.",
            AutomationContext.None,
        [
            new("userIds", AutomationFieldKind.UserList, "Members to add.", Max: AutomationLimits.MaxListItems),
            new("addTriggerUser", AutomationFieldKind.Boolean, "Also add the member who started the run.",
                Default: false)
        ]),
        new("PinMessage", "Pin the message", "Pins the message that started the run.", AutomationContext.Message, []),
        new("GiveCoins", "Give coins", "Gives coins to a member.", AutomationContext.User,
        [
            TargetField(),
            new("amount", AutomationFieldKind.Integer, "How many coins.", true, 1, AutomationLimits.MaxRewardAmount)
        ]),
        new("TakeCoins", "Take coins", "Takes coins from a member, down to nothing at most.", AutomationContext.User,
        [
            TargetField(),
            new("amount", AutomationFieldKind.Integer, "How many coins.", true, 1, AutomationLimits.MaxRewardAmount)
        ]),
        new("GiveXp", "Give XP", "Gives XP to a member.", AutomationContext.User,
        [
            TargetField(),
            new("amount", AutomationFieldKind.Integer, "How much XP.", true, 1, AutomationLimits.MaxRewardAmount)
        ]),
        new("TakeXp", "Take XP", "Takes XP from a member, down to nothing at most.", AutomationContext.User,
        [
            TargetField(),
            new("amount", AutomationFieldKind.Integer, "How much XP.", true, 1, AutomationLimits.MaxRewardAmount)
        ]),
        new("Wait", "Wait", "Waits before the next action.", AutomationContext.None,
        [
            new("seconds", AutomationFieldKind.Integer, "How many seconds.", true, 1, AutomationLimits.MaxWaitSeconds)
        ])
    ];

    /// <summary>
    ///     Gets the details of a trigger.
    /// </summary>
    /// <param name="type">The trigger type.</param>
    public static AutomationTriggerSpec GetTrigger(AutomationTriggerType type) =>
        Triggers.First(x => x.Type == type);

    /// <summary>
    ///     Finds a condition by its type.
    /// </summary>
    /// <param name="type">The condition type.</param>
    public static AutomationNodeSpec? FindCondition(string type) =>
        Conditions.FirstOrDefault(x => x.Type.Equals(type, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Finds an action by its type.
    /// </summary>
    /// <param name="type">The action type.</param>
    public static AutomationNodeSpec? FindAction(string type) =>
        Actions.FirstOrDefault(x => x.Type.Equals(type, StringComparison.OrdinalIgnoreCase));

    private static AutomationFieldSpec ListField(string name, AutomationFieldKind kind, string description) =>
        new(name, kind, description, true, 1, AutomationLimits.MaxListItems);

    private static AutomationFieldSpec TargetField() =>
        new("target", AutomationFieldKind.Choice,
            "Who it affects. The message author is only available when the event has a message.",
            Options: Targets, Default: "TriggerUser");
}
