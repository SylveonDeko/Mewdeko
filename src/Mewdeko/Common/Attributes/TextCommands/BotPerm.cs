using Discord.Commands;

namespace Mewdeko.Common.Attributes.TextCommands;

public class BotPermAttribute : RequireBotPermissionAttribute
{
    public BotPermAttribute(GuildPermission permission) : base(permission)
    {
    }

    public BotPermAttribute(ChannelPermission permission) : base(permission)
    {
    }
}