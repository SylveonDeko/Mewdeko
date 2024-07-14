namespace Mewdeko.Database.Models;

/// <summary>
/// Represents a chat trigger configuration.
/// </summary>
public class ChatTriggers : DbEntity
{
    /// <summary>
    /// Gets or sets the number of times this trigger has been used.
    /// </summary>
    public ulong UseCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the trigger is a regular expression.
    /// </summary>
    public bool IsRegex { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the trigger can only be used by the owner.
    /// </summary>
    public bool OwnerOnly { get; set; } = false;

    /// <summary>
    /// Gets or sets the ID of the guild where this trigger is active. Null if global.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets the response to be sent when the trigger is activated.
    /// </summary>
    public string? Response { get; set; }

    /// <summary>
    /// Gets or sets the trigger text or pattern.
    /// </summary>
    public string? Trigger { get; set; }

    /// <summary>
    /// Gets or sets the prefix requirement type for this trigger.
    /// </summary>
    public RequirePrefixType PrefixType { get; set; } = RequirePrefixType.None;

    /// <summary>
    /// Gets or sets the custom prefix for this trigger.
    /// </summary>
    public string? CustomPrefix { get; set; } = "";

    /// <summary>
    /// Gets or sets a value indicating whether the triggering message should be automatically deleted.
    /// </summary>
    public bool AutoDeleteTrigger { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the bot should react to the triggering message.
    /// </summary>
    public bool ReactToTrigger { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the bot should not respond to the trigger.
    /// </summary>
    public bool NoRespond { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the response should be sent as a DM.
    /// </summary>
    public bool DmResponse { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the trigger can be activated if it's contained anywhere in the message.
    /// </summary>
    public bool ContainsAnywhere { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the trigger allows targeting.
    /// </summary>
    public bool AllowTarget { get; set; } = false;

    /// <summary>
    /// Gets or sets the reactions to be added when the trigger is activated.
    /// </summary>
    public string? Reactions { get; set; }

    /// <summary>
    /// Gets or sets the roles to be granted when the trigger is activated.
    /// </summary>
    public string? GrantedRoles { get; set; } = "";

    /// <summary>
    /// Gets or sets the roles to be removed when the trigger is activated.
    /// </summary>
    public string? RemovedRoles { get; set; } = "";

    /// <summary>
    /// Gets or sets the type of role grant for this trigger.
    /// </summary>
    public CtRoleGrantType RoleGrantType { get; set; }

    /// <summary>
    /// Gets or sets the valid trigger types for this chat trigger.
    /// </summary>
    public ChatTriggerType ValidTriggerTypes { get; set; } = (ChatTriggerType)0b1111;

    /// <summary>
    /// Gets or sets the ID of the associated application command.
    /// </summary>
    public ulong ApplicationCommandId { get; set; } = 0;

    /// <summary>
    /// Gets or sets the name of the associated application command.
    /// </summary>
    public string? ApplicationCommandName { get; set; } = "";

    /// <summary>
    /// Gets or sets the description of the associated application command.
    /// </summary>
    public string? ApplicationCommandDescription { get; set; } = "";

    /// <summary>
    /// Gets or sets the type of the associated application command.
    /// </summary>
    public CtApplicationCommandType ApplicationCommandType { get; set; } = CtApplicationCommandType.None;

    /// <summary>
    /// Gets or sets a value indicating whether the response should be ephemeral.
    /// </summary>
    public bool EphemeralResponse { get; set; } = false;

    /// <summary>
    /// Gets or sets the ID of the channel for crossposting.
    /// </summary>
    public ulong CrosspostingChannelId { get; set; } = 0;

    /// <summary>
    /// Gets or sets the webhook URL for crossposting.
    /// </summary>
    public string? CrosspostingWebhookUrl { get; set; } = "";

    /// <summary>
    /// Gets the real name of the trigger, which is either the application command name or the trigger text.
    /// </summary>
    public string? RealName => (string.IsNullOrEmpty(ApplicationCommandName) ? Trigger : ApplicationCommandName).Trim();

    /// <summary>
    /// Gets an array of reactions associated with this trigger.
    /// </summary>
    /// <returns>An array of reaction string?s.</returns>
    public string?[] GetReactions() =>
        string.IsNullOrWhiteSpace(Reactions)
            ? Array.Empty<string?>()
            : Reactions.Split("@@@");

    /// <summary>
    /// Determines whether this trigger is global (not associated with a specific guild).
    /// </summary>
    /// <returns>True if the trigger is global, false otherwise.</returns>
    public bool IsGlobal() => GuildId is null or 0;
}

/// <summary>
/// Represents a reaction response configuration.
/// </summary>
public class ReactionResponse : DbEntity
{
    /// <summary>
    /// Gets or sets a value indicating whether this reaction response is owner-only.
    /// </summary>
    public bool OwnerOnly { get; set; } = false;

    /// <summary>
    /// Gets or sets the text response for this reaction.
    /// </summary>
    public string? Text { get; set; }
}

/// <summary>
/// Specifies the type of role grant for chat triggers.
/// </summary>
public enum CtRoleGrantType
{
    /// <summary>
    /// Grant or remove roles from the sender of the message.
    /// </summary>
    Sender,

    /// <summary>
    /// Grant or remove roles from the mentioned user(s) in the message.
    /// </summary>
    Mentioned,

    /// <summary>
    /// Grant or remove roles from both the sender and mentioned user(s).
    /// </summary>
    Both
}

/// <summary>
/// Specifies the type of application command for chat triggers.
/// </summary>
public enum CtApplicationCommandType
{
    /// <summary>
    /// No application command associated.
    /// </summary>
    None,

    /// <summary>
    /// A slash command.
    /// </summary>
    Slash,

    /// <summary>
    /// A message context menu command.
    /// </summary>
    Message,

    /// <summary>
    /// A user context menu command.
    /// </summary>
    User
}

/// <summary>
/// Specifies the types of chat triggers.
/// </summary>
[Flags]
public enum ChatTriggerType
{
    /// <summary>
    /// Triggered by a regular message.
    /// </summary>
    Message = 0b0001,

    /// <summary>
    /// Triggered by an interaction.
    /// </summary>
    Interaction = 0b0010,

    /// <summary>
    /// Triggered by a button press.
    /// </summary>
    Button = 0b0100

    // Commented out as not yet developed
    // /// <summary>
    // /// Triggered by reactions.
    // /// </summary>
    // Reactions = 0b10000,
}

/// <summary>
/// Specifies the prefix requirement type for chat triggers.
/// </summary>
public enum RequirePrefixType
{
    /// <summary>
    /// No prefix required.
    /// </summary>
    None,

    /// <summary>
    /// Requires the global prefix.
    /// </summary>
    Global,

    /// <summary>
    /// Requires either the guild-specific prefix or the global prefix.
    /// </summary>
    GuildOrGlobal,

    /// <summary>
    /// Requires the guild-specific prefix if set, otherwise no prefix.
    /// </summary>
    GuildOrNone,

    /// <summary>
    /// Requires a custom prefix.
    /// </summary>
    Custom
}