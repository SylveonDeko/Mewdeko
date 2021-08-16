using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mewdeko.Core.Services.Database.Models;
using Newtonsoft.Json;
using Serilog;

#nullable enable
namespace Mewdeko.Core.Modules.Searches.Common.StreamNotifications.Providers
{
    public class PicartoProvider : Provider
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public PicartoProvider(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        private static Regex Regex { get; } = new(@"picarto.tv/(?<name>.+[^/])/?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public override FollowedStream.FType Platform => FollowedStream.FType.Picarto;

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
                var toReturn = new List<StreamData>();
                foreach (var login in logins)
                    try
                    {
                        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        // get id based on the username
                        var res = await http.GetAsync($"https://api.picarto.tv/v1/channel/name/{login}");

                        if (!res.IsSuccessStatusCode)
                            continue;

                        var userData =
                            JsonConvert.DeserializeObject<PicartoChannelResponse>(
                                await res.Content.ReadAsStringAsync());

                        toReturn.Add(ToStreamData(userData));
                        _failingStreams.TryRemove(login, out _);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex,
                            $"Something went wrong retreiving {Platform} stream data for {login}: {ex.Message}");
                        _failingStreams.TryAdd(login, DateTime.UtcNow);
                    }

                return toReturn;
            }
        }

        private StreamData ToStreamData(PicartoChannelResponse stream)
        {
            return new()
            {
                StreamType = FollowedStream.FType.Picarto,
                Name = stream.Name,
                UniqueName = stream.Name,
                Viewers = stream.Viewers,
                Title = stream.Title,
                IsLive = stream.Online,
                Preview = stream.Thumbnails.Web,
                Game = stream.Category,
                StreamUrl = $"https://picarto.tv/{stream.Name}",
                AvatarUrl = stream.Avatar
            };
        }
    }
}