using Discord;
using Discord.Commands;

namespace Mewdeko.Common.Attributes
{
    public class BotPermAttribute : RequireBotPermissionAttribute
    {
        public BotPermAttribute(GuildPerm permission) : base((GuildPermission)permission)
        {
        }

        public BotPermAttribute(ChannelPerm permission) : base((ChannelPermission)permission)
        {
        }
    }
}