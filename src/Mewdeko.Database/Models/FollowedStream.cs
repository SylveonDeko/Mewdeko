using Mewdeko.Database.Common;

namespace Mewdeko.Database.Models;

public class FollowedStream : DbEntity
{
    public enum FType
    {
        Twitch = 0,
        Picarto = 3,
        Youtube = 4,
        Facebook = 5,
        Trovo = 6
    }

    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public string Username { get; set; }
    public FType Type { get; set; }
    public string Message { get; set; }

    protected bool Equals(FollowedStream other)
        => ChannelId == other.ChannelId
           && string.Equals(Username.Trim(), other.Username.Trim()
               , StringComparison.InvariantCultureIgnoreCase)
           && Type == other.Type;

    public override int GetHashCode()
        => HashCode.Combine(ChannelId, Username, (int)Type);

    public override bool Equals(object obj)
        => obj is FollowedStream fs && Equals(fs);

    public StreamDataKey CreateKey()
        => new(Type, Username.ToLower());
}