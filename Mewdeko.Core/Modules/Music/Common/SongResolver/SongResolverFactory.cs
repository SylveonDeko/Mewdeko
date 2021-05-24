using System.Threading.Tasks;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Core.Services.Impl;
using Mewdeko.Modules.Music.Common.SongResolver.Strategies;

namespace Mewdeko.Modules.Music.Common.SongResolver
{
    public class SongResolverFactory : ISongResolverFactory
    {
        private readonly SoundCloudApiService _sc;

        public SongResolverFactory(SoundCloudApiService sc)
        {
            _sc = sc;
        }

        public async Task<IResolveStrategy> GetResolveStrategy(string query, MusicType? musicType)
        {
            await Task.Yield(); //for async warning
            switch (musicType)
            {
                case MusicType.YouTube:
                    return new YoutubeResolveStrategy();
                case MusicType.Spotify:
                    return new SpotifyResolveStrategy();
                case MusicType.Radio:
                    return new RadioResolveStrategy();
                case MusicType.Local:
                    return new LocalSongResolveStrategy();
                case MusicType.Soundcloud:
                    return new SoundcloudResolveStrategy(_sc);
                case MusicType.Url:
                    return new UrlResolverStrategy();
                default:
                    if (_sc.IsSoundCloudLink(query))
                        return new SoundcloudResolveStrategy(_sc);
                    else if (RadioResolveStrategy.IsRadioLink(query))
                        return new RadioResolveStrategy();
                    // maybe add a check for local files in the future
                    else
                        return new YoutubeResolveStrategy();
            }
        }
    }
}