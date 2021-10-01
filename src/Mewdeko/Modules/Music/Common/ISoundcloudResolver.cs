using System.Collections.Generic;

namespace Mewdeko.Modules.Music.Common
{
    public interface ISoundcloudResolver : IPlatformQueryResolver
    {
        bool IsSoundCloudLink(string url);
        IAsyncEnumerable<ITrackInfo> ResolvePlaylistAsync(string playlist);
    }
}