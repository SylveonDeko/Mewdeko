using Mewdeko.Modules.Music.Common.SongResolver.Impl;

namespace Mewdeko.Modules.Music.Common
{
    public interface ICachableTrackData
    {
        string Id { get; set; }
        string Url { get; set; }
        string Thumbnail { get; set; }
        public TimeSpan Duration { get; }
        MusicPlatform Platform { get; set; }
        string Title { get; set; }
    }
}