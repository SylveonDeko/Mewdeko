using NadekoBot.Modules.Music.Common;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Core.Services.Impl;
using System;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Music.Extensions
{
    public static class Extensions
    {
        public static Task<SongInfo> GetSongInfo(this SoundCloudVideo svideo) =>
            Task.FromResult(new SongInfo
            {
                Title = svideo.FullName,
                Provider = "SoundCloud",
                Uri = () => svideo.StreamLink(),
                ProviderType = MusicType.Soundcloud,
                Query = svideo.TrackLink,
                Thumbnail = svideo.ArtworkUrl,
                TotalTime = TimeSpan.FromMilliseconds(svideo.Duration)
            });
    }
}
