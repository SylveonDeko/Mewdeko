using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Core.Services.Impl;
using NLog;
using YoutubeExplode;
using YoutubeExplode.Common;

namespace Mewdeko.Modules.Music.Common.SongResolver.Strategies
{
    public class SpotifyResolveStrategy : IResolveStrategy
    {
        private readonly Logger _log;

        public SpotifyResolveStrategy()
        {
            _log = LogManager.GetCurrentClassLogger();
        }

        public async Task<SongInfo> ResolveSong(string query)
        {
            return await ResolveWithYtDl(query).ConfigureAwait(false);
        }

        private async Task<SongInfo> ResolveWithYtDl(string query)
        {
            string[] data;
            try
            {
                var ytdl = new YtdlOperation();
                data = (await ytdl.GetDataAsync(query).ConfigureAwait(false)).Split('\n');

                if (data.Length < 6)
                {
                    _log.Info("No song found. Data less than 6");
                    return null;
                }

                if (!TimeSpan.TryParseExact(data[4],
                    new[] { "ss", "m\\:ss", "mm\\:ss", "h\\:mm\\:ss", "hh\\:mm\\:ss", "hhh\\:mm\\:ss" },
                    CultureInfo.InvariantCulture, out var time))
                    time = TimeSpan.FromHours(24);

                return new SongInfo()
                {
                    Title = data[0],
                    VideoId = data[1],
                    Uri = async () =>
                    {
                        var ytdlo = new YtdlOperation();
                        data = (await ytdlo.GetDataAsync(query).ConfigureAwait(false)).Split('\n');
                        if (data.Length < 6)
                        {
                            _log.Info("No song found. Data less than 6");
                            return null;
                        }

                        return data[2];
                    },
                    Thumbnail = data[3],
                    TotalTime = time,
                    Provider = "Spotify",
                    ProviderType = MusicType.Spotify,
                    Query = "https://youtube.com/watch?v=" + data[1],
                };
            }
            catch (Exception ex)
            {
                _log.Warn(ex);
                return null;
            }
        }
    }
}
