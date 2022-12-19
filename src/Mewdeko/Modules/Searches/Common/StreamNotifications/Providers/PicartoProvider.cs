#nullable enable
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Providers;

public class PicartoProvider : Provider
{
    private static Regex Regex { get; } = new(@"picarto.tv/(?<name>.+[^/])/?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override FollowedStream.FType Platform
        => FollowedStream.FType.Picarto;

    private readonly IHttpClientFactory httpClientFactory;

    public PicartoProvider(IHttpClientFactory httpClientFactory)
        => this.httpClientFactory = httpClientFactory;

    public override Task<bool> IsValidUrl(string url)
    {
        var match = Regex.Match(url);
        return Task.FromResult(match.Success);

        // var username = match.Groups["name"].Value;
    }

    public override Task<StreamData?> GetStreamDataByUrlAsync(string url)
    {
        var match = Regex.Match(url);
        if (!match.Success) return Task.FromResult<StreamData?>(null);
        var name = match.Groups["name"].Value;
        return GetStreamDataAsync(name);
    }

#pragma warning disable CS8609
    public override async Task<StreamData?> GetStreamDataAsync(string login)
#pragma warning restore CS8609
    {
        var data = await GetStreamDataAsync(new List<string>
        {
            login
        }).ConfigureAwait(false);

        return data.FirstOrDefault();
    }

    public override async Task<IReadOnlyCollection<StreamData>> GetStreamDataAsync(List<string> logins)
    {
        if (logins.Count == 0)
            return new List<StreamData>();

        using var http = httpClientFactory.CreateClient();
        var toReturn = new List<StreamData>();
        foreach (var login in logins)
        {
            try
            {
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                // get id based on the username
                var res = await http.GetAsync($"https://api.picarto.tv/v1/channel/name/{login}").ConfigureAwait(false);

                if (!res.IsSuccessStatusCode)
                    continue;

                var userData =
                    JsonConvert.DeserializeObject<PicartoChannelResponse>(await res.Content.ReadAsStringAsync().ConfigureAwait(false))!;

                toReturn.Add(ToStreamData(userData));
                FailingStreams.TryRemove(login, out _);
            }
            catch (Exception ex)
            {
                Log.Warning("Something went wrong retreiving {StreamPlatform} stream data for {Login}: {ErrorMessage}",
                    Platform,
                    login,
                    ex.Message);
                FailingStreams.TryAdd(login, DateTime.UtcNow);
            }
        }

        return toReturn;
    }

    private static StreamData ToStreamData(PicartoChannelResponse stream)
        => new()
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