using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mewdeko.Core.Modules.Music;
using Mewdeko.Core.Services.Impl;
using Mewdeko.Extensions;
using Newtonsoft.Json.Linq;

#nullable enable
namespace Mewdeko.Modules.Music.Resolvers
{
    public sealed class SoundcloudResolver : ISoundcloudResolver
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly SoundCloudApiService _sc;
        private readonly ITrackCacher _trackCacher;

        public SoundcloudResolver(SoundCloudApiService sc, ITrackCacher trackCacher, IHttpClientFactory httpFactory)
        {
            _sc = sc;
            _trackCacher = trackCacher;
            _httpFactory = httpFactory;
        }

        public bool IsSoundCloudLink(string url)
        {
            return Regex.IsMatch(url, "(.*)(soundcloud.com|snd.sc)(.*)");
        }

        public async IAsyncEnumerable<ITrackInfo> ResolvePlaylistAsync(string playlist)
        {
            playlist = Uri.EscapeDataString(playlist);

            using var http = _httpFactory.CreateClient();
            var responseString = await http.GetStringAsync($"https://scapi.nadeko.bot/resolve?url={playlist}");
            var scvids = JObject.Parse(responseString)["tracks"]?.ToObject<SoundCloudVideo[]>();
            if (scvids is null) yield break;

            foreach (var videosChunk in scvids.Where(x => x.Streamable is true).Chunk(5))
            {
                var cachableTracks = videosChunk
                    .Select(VideoModelToCachedData)
                    .ToList();

                await Task.WhenAll(cachableTracks.Select(_trackCacher.CacheTrackDataAsync));
                foreach (var info in cachableTracks.Select(CachableDataToTrackInfo)) yield return info;
            }
        }

        public async Task<ITrackInfo?> ResolveByQueryAsync(string query)
        {
            var cached = await _trackCacher.GetCachedDataByQueryAsync(query, MusicPlatform.SoundCloud);
            if (!(cached is null))
                return CachableDataToTrackInfo(cached);

            var svideo = !IsSoundCloudLink(query)
                ? await _sc.GetVideoByQueryAsync(query).ConfigureAwait(false)
                : await _sc.ResolveVideoAsync(query).ConfigureAwait(false);

            if (svideo is null)
                return null;

            var cachableData = VideoModelToCachedData(svideo);
            await _trackCacher.CacheTrackDataByQueryAsync(query, cachableData);

            return CachableDataToTrackInfo(cachableData);
        }

        private ICachableTrackData VideoModelToCachedData(SoundCloudVideo svideo)
        {
            return new CachableTrackData
            {
                Title = svideo.FullName,
                Url = svideo.TrackLink,
                Thumbnail = svideo.ArtworkUrl,
                TotalDurationMs = svideo.Duration,
                Id = svideo.Id.ToString(),
                Platform = MusicPlatform.SoundCloud
            };
        }

        private ITrackInfo CachableDataToTrackInfo(ICachableTrackData trackData)
        {
            return new SimpleTrackInfo(
                trackData.Title,
                trackData.Url,
                trackData.Thumbnail,
                trackData.Duration,
                trackData.Platform,
                GetStreamUrl(trackData.Id)
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetStreamUrl(string trackId)
        {
            return $"https://api.soundcloud.com/tracks/{trackId}/stream?client_id=368b0c85751007cd588d869d3ae61ac0";
        }
    }
}
