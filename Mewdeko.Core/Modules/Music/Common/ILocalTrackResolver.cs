using System.Collections.Generic;

namespace Mewdeko.Core.Modules.Music
{
    public interface ILocalTrackResolver : IPlatformQueryResolver
    {
        IAsyncEnumerable<ITrackInfo> ResolveDirectoryAsync(string dirPath);
    }
}