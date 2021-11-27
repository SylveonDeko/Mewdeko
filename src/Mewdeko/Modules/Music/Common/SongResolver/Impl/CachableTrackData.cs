using System;
using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Music.Common.SongResolver.Impl
{
    public sealed class CachableTrackData : ICachableTrackData
    {
        public double TotalDurationMs { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Thumbnail { get; set; } = string.Empty;

        [JsonIgnore] public TimeSpan Duration => TimeSpan.FromMilliseconds(TotalDurationMs);

        public MusicPlatform Platform { get; set; }
    }
}