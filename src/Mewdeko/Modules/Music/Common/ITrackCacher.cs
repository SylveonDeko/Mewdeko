#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Mewdeko.Modules.Music.Common.SongResolver.Impl;

namespace Mewdeko.Modules.Music.Common
{
    public interface ITrackCacher
    {
        Task<string?> GetOrCreateStreamLink(
            string id,
            MusicPlatform platform,
            Func<Task<(string StreamUrl, TimeSpan Expiry)>> streamUrlFactory
        );

        Task CacheTrackDataAsync(ICachableTrackData data);
        Task<ICachableTrackData?> GetCachedDataByIdAsync(string id, MusicPlatform platform);
        Task<ICachableTrackData?> GetCachedDataByQueryAsync(string query, MusicPlatform platform);
        Task CacheTrackDataByQueryAsync(string query, ICachableTrackData data);
        Task CacheStreamUrlAsync(string id, MusicPlatform platform, string url, TimeSpan expiry);
        Task<IReadOnlyCollection<string>> GetPlaylistTrackIdsAsync(string playlistId, MusicPlatform platform);
        Task CachePlaylistTrackIdsAsync(string playlistId, MusicPlatform platform, IEnumerable<string> ids);
        Task CachePlaylistIdByQueryAsync(string query, MusicPlatform platform, string playlistId);
        Task<string?> GetPlaylistIdByQueryAsync(string query, MusicPlatform platform);
    }
}