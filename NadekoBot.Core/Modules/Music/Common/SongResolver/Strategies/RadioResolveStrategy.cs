using NadekoBot.Core.Services.Database.Models;
using NLog;
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Music.Common.SongResolver.Strategies
{
    public class RadioResolveStrategy : IResolveStrategy
    {
        private readonly Regex plsRegex = new Regex("File1=(?<url>.*?)\\n", RegexOptions.Compiled);
        private readonly Regex m3uRegex = new Regex("(?<url>^[^#].*)", RegexOptions.Compiled | RegexOptions.Multiline);
        private readonly Regex asxRegex = new Regex("<ref href=\"(?<url>.*?)\"", RegexOptions.Compiled);
        private readonly Regex xspfRegex = new Regex("<location>(?<url>.*?)</location>", RegexOptions.Compiled);
        private readonly Logger _log;

        public RadioResolveStrategy()
        {
            _log = LogManager.GetCurrentClassLogger();
        }

        public async Task<SongInfo> ResolveSong(string query)
        {
            if (IsRadioLink(query))
                query = await HandleStreamContainers(query).ConfigureAwait(false);

            return new SongInfo
            {
                Uri = () => Task.FromResult(query),
                Title = query,
                Provider = "Radio Stream",
                ProviderType = MusicType.Radio,
                Query = query,
                TotalTime = TimeSpan.MaxValue,
                Thumbnail = "https://cdn.discordapp.com/attachments/155726317222887425/261850925063340032/1482522097_radio.png",
            };
        }

        public static bool IsRadioLink(string query) =>
            (query.StartsWith("http", StringComparison.InvariantCulture) ||
            query.StartsWith("ww", StringComparison.InvariantCulture))
            &&
            (query.Contains(".pls") ||
            query.Contains(".m3u") ||
            query.Contains(".asx") ||
            query.Contains(".xspf"));

        private async Task<string> HandleStreamContainers(string query)
        {
            string file = null;
            try
            {
                using (var http = new HttpClient())
                {
                    file = await http.GetStringAsync(query).ConfigureAwait(false);
                }
            }
            catch
            {
                return query;
            }
            if (query.Contains(".pls"))
            {
                //File1=http://armitunes.com:8000/
                //Regex.Match(query)
                try
                {
                    var m = plsRegex.Match(file);
                    var res = m.Groups["url"]?.ToString();
                    return res?.Trim();
                }
                catch
                {
                    _log.Warn($"Failed reading .pls:\n{file}");
                    return null;
                }
            }
            if (query.Contains(".m3u"))
            {
                /* 
# This is a comment
                   C:\xxx4xx\xxxxxx3x\xx2xxxx\xx.mp3
                   C:\xxx5xx\x6xxxxxx\x7xxxxx\xx.mp3
                */
                try
                {
                    var m = m3uRegex.Match(file);
                    var res = m.Groups["url"]?.ToString();
                    return res?.Trim();
                }
                catch
                {
                    _log.Warn($"Failed reading .m3u:\n{file}");
                    return null;
                }

            }
            if (query.Contains(".asx"))
            {
                //<ref href="http://armitunes.com:8000"/>
                try
                {
                    var m = asxRegex.Match(file);
                    var res = m.Groups["url"]?.ToString();
                    return res?.Trim();
                }
                catch
                {
                    _log.Warn($"Failed reading .asx:\n{file}");
                    return null;
                }
            }
            if (query.Contains(".xspf"))
            {
                /*
                <?xml version="1.0" encoding="UTF-8"?>
                    <playlist version="1" xmlns="http://xspf.org/ns/0/">
                        <trackList>
                            <track><location>file:///mp3s/song_1.mp3</location></track>
                */
                try
                {
                    var m = xspfRegex.Match(file);
                    var res = m.Groups["url"]?.ToString();
                    return res?.Trim();
                }
                catch
                {
                    _log.Warn($"Failed reading .xspf:\n{file}");
                    return null;
                }
            }

            return query;
        }
    }
}