using Discord.Commands;

namespace Discord
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
