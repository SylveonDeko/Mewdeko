#nullable enable
using System;
using System.Threading.Tasks;
using Mewdeko.Modules.Music.Common.SongResolver.Impl;

namespace Mewdeko.Modules.Music.Common
{
    public interface ITrackInfo
    {
        public string Title { get; }
        public string Url { get; }
        public string Thumbnail { get; }
        public TimeSpan Duration { get; }
        public MusicPlatform Platform { get; }
        public ValueTask<string?> GetStreamUrl();
    }
}