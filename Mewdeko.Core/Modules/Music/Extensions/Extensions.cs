using System;
using System.Threading.Tasks;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Core.Services.Impl;
using Mewdeko.Modules.Music.Common;

namespace Mewdeko.Modules.Music.Extensions
{
    public static class Extensions
    {
        public static Task<SongInfo> GetSongInfo(this SoundCloudVideo svideo)
        {
            return Task.FromResult(new SongInfo
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
}