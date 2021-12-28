using Mewdeko.Services.Database.Models;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

public readonly struct StreamDataKey
{
    public FollowedStream.FType Type { get; }
    public string Name { get; }

    public StreamDataKey(FollowedStream.FType type, string name)
    {
        Type = type;
        Name = name;
    }
}