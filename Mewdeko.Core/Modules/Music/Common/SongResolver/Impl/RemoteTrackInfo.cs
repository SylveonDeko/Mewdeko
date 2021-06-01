#nullable enable
using System;
using System.Threading.Tasks;

namespace Mewdeko.Core.Modules.Music
{
    public sealed class RemoteTrackInfo : ITrackInfo
    {
        public string Title { get; }
        public string Url { get; }
        public string Thumbnail { get; }
        public TimeSpan Duration { get; }
        public MusicPlatform Platform { get; }

        private readonly Func<Task<string?>> _streamFactory;

        public RemoteTrackInfo(string title, string url, string thumbnail, TimeSpan duration, MusicPlatform platform,
            Func<Task<string?>> streamFactory)
        {
            _streamFactory = streamFactory;
            Title = title;
            Url = url;
            Thumbnail = thumbnail;
            Duration = duration;
            Platform = platform;
        }

        public async ValueTask<string?> GetStreamUrl() => await _streamFactory();
    }
}