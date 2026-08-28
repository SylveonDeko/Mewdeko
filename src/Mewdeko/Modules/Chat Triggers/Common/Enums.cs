namespace Mewdeko.Modules.Chat_Triggers.Common;

/// <summary>
///     Specifies the type of role grant for chat triggers.
/// </summary>
public enum CtRoleGrantType
{
    /// <summary>
    ///     Grant or remove roles from the sender of the message.
    /// </summary>
    Sender,

    /// <summary>
    ///     Grant or remove roles from the mentioned user(s) in the message.
    /// </summary>
    Mentioned,

    /// <summary>
    ///     Grant or remove roles from both the sender and mentioned user(s).
    /// </summary>
    Both
}

/// <summary>
///     Specifies the type of application command for chat triggers.
/// </summary>
public enum CtApplicationCommandType
{
    /// <summary>
    ///     No application command associated.
    /// </summary>
    None,

    /// <summary>
    ///     A slash command.
    /// </summary>
    Slash,

    /// <summary>
    ///     A message context menu command.
    /// </summary>
    Message,

    /// <summary>
    ///     A user context menu command.
    /// </summary>
    User
}

/// <summary>
///     Specifies the types of chat triggers.
/// </summary>
[Flags]
public enum ChatTriggerType
{
    /// <summary>
    ///     Triggered by a regular message.
    /// </summary>
    Message = 0b0001,

    /// <summary>
    ///     Triggered by an interaction.
    /// </summary>
    Interaction = 0b0010,

    /// <summary>
    ///     Triggered by a button press.
    /// </summary>
    Button = 0b0100,

    /// <summary>
    ///     Triggered by reactions.
    /// </summary>
    Reactions = 0b1000,

    /// <summary>
    ///     Triggered by a reaction being removed, which makes reaction role style toggles possible.
    /// </summary>
    ReactionsRemoved = 0b10000,

    /// <summary>
    ///     Triggered by an event raised elsewhere in the bot, such as an XP level up or a member joining.
    /// </summary>
    Event = 0b100000
}

/// <summary>
///     Specifies the bot event a chat trigger listens for.
/// </summary>
/// <remarks>
///     These let a trigger act as the formatting layer for another module's notification, so a server can use the full
///     trigger response toolkit, embeds, components, conditions and role grants, in place of that module's own fixed
///     message.
/// </remarks>
public enum CtEventType
{
    /// <summary>
    ///     The trigger does not listen for an event.
    /// </summary>
    None,

    /// <summary>
    ///     A member gained a level.
    /// </summary>
    XpLevelUp,

    /// <summary>
    ///     A member lost a level.
    /// </summary>
    XpLevelDown,

    /// <summary>
    ///     A member joined the server.
    /// </summary>
    MemberJoin,

    /// <summary>
    ///     A member left the server.
    /// </summary>
    MemberLeave,

    /// <summary>
    ///     A member joined a voice channel.
    /// </summary>
    VoiceJoin,

    /// <summary>
    ///     A member left a voice channel.
    /// </summary>
    VoiceLeave,

    /// <summary>
    ///     A member started boosting the server.
    /// </summary>
    Boost,

    /// <summary>
    ///     A member stopped boosting the server.
    /// </summary>
    BoostEnd,

    /// <summary>
    ///     A ticket was opened.
    /// </summary>
    TicketOpened,

    /// <summary>
    ///     A ticket was closed.
    /// </summary>
    TicketClosed,

    /// <summary>
    ///     A giveaway was won.
    /// </summary>
    GiveawayWon
}

/// <summary>
///     Specifies who a chat trigger's cooldown applies to.
/// </summary>
public enum CtCooldownScope
{
    /// <summary>
    ///     Each member has their own cooldown.
    /// </summary>
    User,

    /// <summary>
    ///     The cooldown is shared by everyone in a channel.
    /// </summary>
    Channel,

    /// <summary>
    ///     The cooldown is shared by the whole server.
    /// </summary>
    Guild
}

/// <summary>
///     Specifies how a chat trigger picks between its responses when it has more than one.
/// </summary>
public enum CtResponseMode
{
    /// <summary>
    ///     Always send the primary response, ignoring any additional ones.
    /// </summary>
    Single,

    /// <summary>
    ///     Send one response chosen at random. Repeating a response makes it proportionally more likely, which is how
    ///     weighting is expressed.
    /// </summary>
    Random,

    /// <summary>
    ///     Send responses in order, advancing one place each time the trigger fires and wrapping at the end.
    /// </summary>
    RoundRobin,

    /// <summary>
    ///     Send every response, in order.
    /// </summary>
    All
}

/// <summary>
///     Specifies the prefix requirement type for chat triggers.
/// </summary>
public enum RequirePrefixType
{
    /// <summary>
    ///     No prefix required.
    /// </summary>
    None,

    /// <summary>
    ///     Requires the global prefix.
    /// </summary>
    Global,

    /// <summary>
    ///     Requires either the guild-specific prefix or the global prefix.
    /// </summary>
    GuildOrGlobal,

    /// <summary>
    ///     Requires the guild-specific prefix if set, otherwise no prefix.
    /// </summary>
    GuildOrNone,

    /// <summary>
    ///     Requires a custom prefix.
    /// </summary>
    Custom
}