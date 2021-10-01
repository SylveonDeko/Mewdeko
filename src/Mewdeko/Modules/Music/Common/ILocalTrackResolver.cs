using System.Collections.Generic;

namespace Mewdeko.Modules.Music.Common
{
    public interface ILocalTrackResolver : IPlatformQueryResolver
    {
        IAsyncEnumerable<ITrackInfo> ResolveDirectoryAsync(string dirPath);
    }
}