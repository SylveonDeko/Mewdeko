using Discord;
using Discord.Commands;

namespace Mewdeko.Common.Attributes
{
    public class BotPermAttribute : RequireBotPermissionAttribute
    {
        public BotPermAttribute(GuildPermission permission) : base(permission)
        {
        }

        public BotPermAttribute(ChannelPermission permission) : base(permission)
        {
        }
    }
}