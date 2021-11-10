#nullable enable
using System.Threading.Tasks;
using Mewdeko.Modules.Music.Common.SongResolver.Impl;
using Serilog;

namespace Mewdeko.Modules.Music.Common.SongResolver.Resolvers
{
    public sealed class TrackResolveProvider : ITrackResolveProvider
    {
        private readonly ILocalTrackResolver _localResolver;
        private readonly IRadioResolver _radioResolver;
        private readonly ISoundcloudResolver _soundcloudResolver;
        private readonly ISpotifyResolver _sResolver;
        private readonly IYoutubeResolver _ytResolver;

        public TrackResolveProvider(IYoutubeResolver ytResolver, ILocalTrackResolver localResolver,
            ISoundcloudResolver soundcloudResolver, IRadioResolver radioResolver, ISpotifyResolver sResolver)
        {
            _ytResolver = ytResolver;
            _localResolver = localResolver;
            _soundcloudResolver = soundcloudResolver;
            _radioResolver = radioResolver;
            _sResolver = sResolver;
        }

        public Task<ITrackInfo?> QuerySongAsync(string query, MusicPlatform? forcePlatform)
        {
            switch (forcePlatform)
            {
                case MusicPlatform.Radio:
                    return _radioResolver.ResolveByQueryAsync(query);
                case MusicPlatform.Youtube:
                    return _ytResolver.ResolveByQueryAsync(query);
                case MusicPlatform.Local:
                    return _localResolver.ResolveByQueryAsync(query);
                case MusicPlatform.SoundCloud:
                    return _soundcloudResolver.ResolveByQueryAsync(query);
                case MusicPlatform.Spotify:
                    return _sResolver.ResolveByQueryAsync(query);
                case null:
                    var match = _ytResolver.YtVideoIdRegex.Match(query);
                    if (match.Success)
                        return _ytResolver.ResolveByIdAsync(match.Groups["id"].Value);
                    else if (_soundcloudResolver.IsSoundCloudLink(query))
                        return _soundcloudResolver.ResolveByQueryAsync(query);
                    else if (Uri.TryCreate(query, UriKind.Absolute, out var uri) && uri.IsFile)
                        return _localResolver.ResolveByQueryAsync(uri.AbsolutePath);
                    else if (IsRadioLink(query))
                        return _radioResolver.ResolveByQueryAsync(query);
                    else
                        return _ytResolver.ResolveByQueryAsync(query, false);
                default:
                    Log.Error("Unsupported platform: {MusicPlatform}", forcePlatform);
                    return Task.FromResult<ITrackInfo?>(null);
            }
        }

        public static bool IsRadioLink(string query)
        {
            return (query.StartsWith("http", StringComparison.InvariantCulture) ||
                    query.StartsWith("ww", StringComparison.InvariantCulture))
                   &&
                   (query.Contains(".pls") ||
                    query.Contains(".m3u") ||
                    query.Contains(".asx") ||
                    query.Contains(".xspf"));
        }
    }
}