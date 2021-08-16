#nullable enable
using System.Threading.Tasks;

namespace Mewdeko.Core.Modules.Music
{
    public interface ITrackResolveProvider
    {
        Task<ITrackInfo?> QuerySongAsync(string query, MusicPlatform? forcePlatform);
    }
}