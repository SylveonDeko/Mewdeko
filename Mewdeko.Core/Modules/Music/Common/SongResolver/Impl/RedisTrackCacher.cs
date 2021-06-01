#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Core.Modules.Music
{
    public sealed class RedisTrackCacher : ITrackCacher
    {
        private readonly ConnectionMultiplexer _multiplexer;

        public RedisTrackCacher(ConnectionMultiplexer multiplexer)
        {
            _multiplexer = multiplexer;
        }
        
        public async Task<string?> GetOrCreateStreamLink(
            string id,
            MusicPlatform platform,
            Func<Task<(string StreamUrl, TimeSpan Expiry)>> streamUrlFactory
        )
        {
            var trackStreamKey = CreateStreamKey(id, platform);
            
            var value = await GetStreamFromCacheInternalAsync(trackStreamKey);
            
            // if there is no cached value
            if (value == default)
            {
                // otherwise retrieve and cache a new value, and run this method again
                var success = await CreateAndCacheStreamUrlAsync(trackStreamKey, streamUrlFactory);
                if (!success)
                    return null;
                
                return await GetOrCreateStreamLink(id, platform, streamUrlFactory);
            }

            // cache new one for future use
            _ = Task.Run(() => CreateAndCacheStreamUrlAsync(trackStreamKey, streamUrlFactory));
            
            return value;
        }
        
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string CreateStreamKey(string id, MusicPlatform platform)
            => $"track:stream:{platform}:{id}";

        private async Task<bool> CreateAndCacheStreamUrlAsync(
            string trackStreamKey,
            Func<Task<(string StreamUrl, TimeSpan Expiry)>> factory)
        {
            try
            {
                var data = await factory();
                if (data == default)
                    return false;

                await CacheStreamUrlInternalAsync(trackStreamKey, data.StreamUrl, data.Expiry);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error resolving stream link for {TrackCacheKey}", trackStreamKey);
                return false;
            }
        }

        public Task CacheStreamUrlAsync(string id, MusicPlatform platform, string url, TimeSpan expiry)
            => CacheStreamUrlInternalAsync(CreateStreamKey(id, platform), url, expiry);

        private async Task CacheStreamUrlInternalAsync(string trackStreamKey, string url, TimeSpan expiry)
        {
            // keys need to be expired after an hour
            // to make sure client doesn't get an expired stream url
            // to achieve this, track keys will be just pointers to real data
            // but that data will expire
            
            var db = _multiplexer.GetDatabase();
            var dataKey = $"entry:{Guid.NewGuid()}:{trackStreamKey}";
            await db.StringSetAsync(dataKey, url, expiry: expiry);
            await db.ListRightPushAsync(trackStreamKey, dataKey);
        }

        private async Task<string?> GetStreamFromCacheInternalAsync(string trackStreamKey)
        {
            // Job of the method which retrieves keys is to pop the elements
            // from the list of cached trackurls until it finds a non-expired key

            var db = _multiplexer.GetDatabase();
            while(true)
            {
                string? dataKey = await db.ListLeftPopAsync(trackStreamKey);
                if (dataKey == default)
                    return null;

                var streamUrl = await db.StringGetAsync(dataKey);
                if (streamUrl == default)
                    continue;

                return streamUrl;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string CreateCachedDataKey(string id, MusicPlatform platform)
            => $"track:data:{platform}:{id}";
        
        public Task CacheTrackDataAsync(ICachableTrackData data)
        {
            var db = _multiplexer.GetDatabase();

            var trackDataKey = CreateCachedDataKey(data.Id, data.Platform);
            var dataString = JsonSerializer.Serialize((object)data);
            // cache for 1 day
            return db.StringSetAsync(trackDataKey, dataString, expiry: TimeSpan.FromDays(1));
        }

        public async Task<ICachableTrackData?> GetCachedDataByIdAsync(string id, MusicPlatform platform)
        {
            var db = _multiplexer.GetDatabase();
            
            var trackDataKey = CreateCachedDataKey(id, platform);
            var data = await db.StringGetAsync(trackDataKey);
            if (data == default)
                return null;

            return JsonSerializer.Deserialize<CachableTrackData>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string CreateCachedQueryDataKey(string query, MusicPlatform platform)
            => $"track:query_to_id:{platform}:{query}";
        public async Task<ICachableTrackData?> GetCachedDataByQueryAsync(string query, MusicPlatform platform)
        {
            query = Uri.EscapeDataString(query.Trim());
            
            var db = _multiplexer.GetDatabase();
            var queryDataKey = CreateCachedQueryDataKey(query, platform);

            var trackId = await db.StringGetAsync(queryDataKey);
            if (trackId == default)
                return null;

            return await GetCachedDataByIdAsync(trackId, platform);
        }

        public async Task CacheTrackDataByQueryAsync(string query, ICachableTrackData data)
        {
            query = Uri.EscapeDataString(query.Trim());

            // first cache the data
            await CacheTrackDataAsync(data);
            
            // then map the query to cached data's id
            var db = _multiplexer.GetDatabase();

            var queryDataKey = CreateCachedQueryDataKey(query, data.Platform);
            await db.StringSetAsync(queryDataKey, data.Id, TimeSpan.FromDays(7));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string CreateCachedPlaylistKey(string playlistId, MusicPlatform platform)
            => $"playlist:{platform}:{playlistId}";
        public async Task<IReadOnlyCollection<string>> GetPlaylistTrackIdsAsync(string playlistId, MusicPlatform platform)
        {
            var db = _multiplexer.GetDatabase();
            var key = CreateCachedPlaylistKey(playlistId, platform);
            var vals = await db.ListRangeAsync(key);
            if (vals == default || vals.Length == 0)
                return Array.Empty<string>();

            return vals.Select(x => x.ToString()).ToList();
        }

        public async Task CachePlaylistTrackIdsAsync(string playlistId, MusicPlatform platform, IEnumerable<string> ids)
        {
            var db = _multiplexer.GetDatabase();
            var key = CreateCachedPlaylistKey(playlistId, platform);
            await db.ListRightPushAsync(key, ids.Select(x => (RedisValue) x).ToArray());
            await db.KeyExpireAsync(key, TimeSpan.FromDays(7));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string CreateCachedPlaylistQueryKey(string query, MusicPlatform platform)
            => $"playlist:query:{platform}:{query}";
        public Task CachePlaylistIdByQueryAsync(string query, MusicPlatform platform, string playlistId)
        {
            query = Uri.EscapeDataString(query.Trim());
            var key = CreateCachedPlaylistQueryKey(query, platform);
            var db = _multiplexer.GetDatabase();
            return db.StringSetAsync(key, playlistId, TimeSpan.FromDays(7));
        }

        public async Task<string?> GetPlaylistIdByQueryAsync(string query, MusicPlatform platform)
        {
            query = Uri.EscapeDataString(query.Trim());
            var key = CreateCachedPlaylistQueryKey(query, platform);

            var val = await _multiplexer.GetDatabase().StringGetAsync(key);
            if (val == default)
                return null;

            return val;
        }
    }
}