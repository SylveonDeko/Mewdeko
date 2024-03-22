using Discord.Commands;

namespace Mewdeko.Common.Attributes.TextCommands;

/// <summary>
/// Attribute to check bot permissions before executing a command or method.
/// </summary>
public class BotPermAttribute : RequireBotPermissionAttribute
{
    /// <summary>
    /// Initializes a new instance of the BotPermAttribute class with guild permissions.
    /// </summary>
    /// <param name="permission">The required guild permission.</param>
    public BotPermAttribute(GuildPermission permission) : base(permission)
    {
    }

    /// <summary>
    /// Initializes a new instance of the BotPermAttribute class with channel permissions.
    /// </summary>
    /// <param name="permission">The required channel permission.</param>
    public BotPermAttribute(ChannelPermission permission) : base(permission)
    {
    }
}