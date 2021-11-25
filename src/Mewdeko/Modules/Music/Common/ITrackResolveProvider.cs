#nullable enable
using System.Threading.Tasks;
using Mewdeko.Modules.Music.Common.SongResolver.Impl;

namespace Mewdeko.Modules.Music.Common
{
    public interface ITrackResolveProvider
    {
        Task<ITrackInfo?> QuerySongAsync(string query, MusicPlatform? forcePlatform);
    }
}