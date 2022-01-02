using System;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

namespace Mewdeko.Services.Database.Models;

public class FollowedStream : DbEntity
{
    public enum FType
    {
        Twitch = 0,
        [Obsolete("No longer supported.")] Smashcast = 1,
        [Obsolete("No longer supported.")] Mixer = 2,
        Picarto = 3,
        Youtube = 4,
        Facebook = 5
    }

    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public string Username { get; set; }
    public FType Type { get; set; }
    public string Message { get; set; }

    protected bool Equals(FollowedStream other) =>
        ChannelId == other.ChannelId
        && Username.Trim().ToUpperInvariant() == other.Username.Trim().ToUpperInvariant()
        && Type == other.Type;

    public override int GetHashCode() => HashCode.Combine(ChannelId, Username, (int) Type);

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((FollowedStream) obj);
    }

    public StreamDataKey CreateKey() => new StreamDataKey(Type, Username.ToLower());
}