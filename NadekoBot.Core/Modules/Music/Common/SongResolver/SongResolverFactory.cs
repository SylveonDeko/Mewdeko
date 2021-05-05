using System.Threading.Tasks;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Core.Services.Impl;
using NadekoBot.Modules.Music.Common.SongResolver.Strategies;

namespace NadekoBot.Modules.Music.Common.SongResolver
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
                case MusicType.Radio:
                    return new RadioResolveStrategy();
                case MusicType.Local:
                    return new LocalSongResolveStrategy();
                case MusicType.Soundcloud:
                    return new SoundcloudResolveStrategy(_sc);
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
