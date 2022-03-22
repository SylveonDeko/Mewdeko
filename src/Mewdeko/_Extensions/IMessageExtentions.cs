using Discord;

namespace Mewdeko._Extensions;

public static class MessageExtentions
{
    public static string GetJumpLink(this IMessage message)
        => $"https://discord.com/channels/{(message.Channel is ITextChannel channel ? channel.GuildId : "@me")}/{message.Channel.Id}/{message.Id}";
}
