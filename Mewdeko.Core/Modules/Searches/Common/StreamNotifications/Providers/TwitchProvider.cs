using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mewdeko.Core.Services.Database.Models;
using Newtonsoft.Json;
using NLog;

#nullable enable
namespace Mewdeko.Core.Modules.Searches.Common.StreamNotifications.Providers
{
    public class TwitchProvider : Provider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly Logger _log;

        public TwitchProvider(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _log = LogManager.GetCurrentClassLogger();
        }

        private static Regex Regex { get; } = new(@"twitch.tv/(?<name>.+[^/])/?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public override FollowedStream.FType Platform => FollowedStream.FType.Twitch;

        public override Task<bool> IsValidUrl(string url)
        {
            var match = Regex.Match(url);
            if (!match.Success)
                return Task.FromResult(false);

            // var username = match.Groups["name"].Value;
            return Task.FromResult(true);
        }

        public override Task<StreamData?> GetStreamDataByUrlAsync(string url)
        {
            var match = Regex.Match(url);
            if (match.Success)
            {
                var name = match.Groups["name"].Value;
                return GetStreamDataAsync(name);
            }

            return Task.FromResult<StreamData?>(null);
        }

        public override async Task<StreamData?> GetStreamDataAsync(string id)
        {
            var data = await GetStreamDataAsync(new List<string> {id});

            return data.FirstOrDefault();
        }

        public override async Task<List<StreamData>> GetStreamDataAsync(List<string> logins)
        {
            if (logins.Count == 0)
                return new List<StreamData>();

            using (var http = _httpClientFactory.CreateClient())
            {
                http.DefaultRequestHeaders.Add("Client-Id", "67w6z9i09xv2uoojdm9l0wsyph4hxo6");
                http.DefaultRequestHeaders.Add("Accept", "application/vnd.twitchtv.v5+json");

                var toReturn = new List<StreamData>();
                foreach (var login in logins)
                    try
                    {
                        // get id based on the username
                        var idsStr = await http.GetStringAsync($"https://api.twitch.tv/kraken/users?login={login}");
                        var userData = JsonConvert.DeserializeObject<TwitchUsersResponseV5>(idsStr);
                        var user = userData?.Users.FirstOrDefault();

                        // if user can't be found, skip, it means there is no such user
                        if (user is null)
                            continue;

                        // get stream data
                        var str = await http.GetStringAsync($"https://api.twitch.tv/kraken/streams/{user.Id}");
                        var resObj =
                            JsonConvert.DeserializeAnonymousType(str, new {Stream = new TwitchResponseV5.Stream()});

                        // if stream is null, user is not streaming
                        if (resObj?.Stream is null)
                        {
                            // if user is not streaming, get his offline banner
                            var chStr = await http.GetStringAsync($"https://api.twitch.tv/kraken/channels/{user.Id}");
                            var ch = JsonConvert.DeserializeObject<TwitchResponseV5.Channel>(chStr);

                            if (ch != null)
                                toReturn.Add(new StreamData
                                {
                                    StreamType = FollowedStream.FType.Twitch,
                                    Name = ch.DisplayName,
                                    UniqueName = ch.Name,
                                    Title = ch.Status,
                                    IsLive = false,
                                    AvatarUrl = ch.Logo,
                                    StreamUrl = $"https://twitch.tv/{ch.Name}",
                                    Preview = ch.VideoBanner // set video banner as the preview,
                                });
                            continue; // move on
                        }

                        toReturn.Add(ToStreamData(resObj.Stream));
                        _failingStreams.TryRemove(login, out _);
                    }
                    catch (Exception ex)
                    {
                        _log.Warn($"Something went wrong retreiving {Platform} stream data for {login}: {ex.Message}");
                        _failingStreams.TryAdd(login, DateTime.UtcNow);
                    }

                return toReturn;
            }
        }

        private StreamData ToStreamData(TwitchResponseV5.Stream stream)
        {
            return new()
            {
                StreamType = FollowedStream.FType.Twitch,
                Name = stream.Channel.DisplayName,
                UniqueName = stream.Channel.Name,
                Viewers = stream.Viewers,
                Title = stream.Channel.Status,
                IsLive = true,
                Preview = stream.Preview.Large,
                Game = stream.Channel.Game,
                StreamUrl = $"https://twitch.tv/{stream.Channel.Name}",
                AvatarUrl = stream.Channel.Logo
            };
        }
    }
}