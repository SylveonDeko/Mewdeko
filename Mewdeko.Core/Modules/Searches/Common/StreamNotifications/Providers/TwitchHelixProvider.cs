namespace Mewdeko.Core.Modules.Searches.Common.StreamNotifications.Providers
{
    // public class TwitchProvider : IProvider
    // {
    //         //
    //     private static Regex Regex { get; } = new Regex(@"twitch.tv/(?<name>.+[^/])/?",
    //         RegexOptions.Compiled | RegexOptions.IgnoreCase);
    //
    //     public FollowedStream.FType Platform => FollowedStream.FType.Twitch;
    //
    //     public TwitchProvider()
    //     {
    //         _log = LogManager.GetCurrentClassLogger();
    //     }
    //
    //     public Task<bool> IsValidUrl(string url)
    //     {
    //         var match = Regex.Match(url);
    //         if (!match.Success)
    //             return Task.FromResult(false);
    //
    //         var username = match.Groups["name"].Value;
    //         return Task.FromResult(true);
    //     }
    //     
    //     public Task<StreamData?> GetStreamDataByUrlAsync(string url)
    //     {
    //         var match = Regex.Match(url);
    //         if (match.Success)
    //         {
    //             var name = match.Groups["name"].Value;
    //             return GetStreamDataAsync(name);
    //         }
    //
    //         return Task.FromResult<StreamData?>(null);
    //     }
    //
    //     public async Task<StreamData?> GetStreamDataAsync(string id)
    //     {
    //         var data = await GetStreamDataAsync(new List<string> {id});
    //
    //         return data.FirstOrDefault();
    //     }
    //
    //     public async Task<List<StreamData>> GetStreamDataAsync(List<string> logins)
    //     {
    //         if (logins.Count == 0)
    //             return new List<StreamData>();
    //
    //         using (var http = new HttpClient())
    //         {
    //             http.DefaultRequestHeaders.Add("Client-Id","67w6z9i09xv2uoojdm9l0wsyph4hxo6");
    //             http.DefaultRequestHeaders.Add("Authorization","Bearer ");
    //
    //             string str;
    //             TwitchResponse res;
    //             try
    //             {
    //                 str = await http.GetStringAsync($"https://api.twitch.tv/helix/streams" +
    //                                                 $"?user_login={logins}" +
    //                                                 $"&first=100");
    //                 res = JsonConvert.DeserializeObject<TwitchResponse>(str);
    //             }
    //             catch (Exception ex)
    //             {
    //                 Log.Warning($"Something went wrong retreiving {Platform} streams.");
    //                 Log.Warning(ex.ToString());
    //                 return new List<StreamData>();
    //             }
    //
    //             if (res.Data.Count == 0)
    //             {
    //                 return new List<StreamData>();
    //             }
    //
    //             return res.Data.Select(ToStreamData).ToList();
    //         }
    //     }
    //
    //     private StreamData ToStreamData(TwitchResponse.StreamApiData apiData)
    //     {
    //         return new StreamData()
    //         {
    //             StreamType = FollowedStream.FType.Twitch,
    //             Name = apiData.UserName,
    //             Viewers = apiData.ViewerCount,
    //             Title = apiData.Title,
    //             IsLive = apiData.Type == "live",
    //             Preview = apiData.ThumbnailUrl,
    //             Game = apiData.GameId,
    //         };
    //     }
    // }
    //
    // public class TwitchResponse
    // {
    //     public List<StreamApiData> Data { get; set; }
    //
    //     public class StreamApiData
    //     {
    //         public string Id { get; set; }
    //         public string UserId { get; set; }
    //         public string UserName { get; set; }
    //         public string GameId { get; set; }
    //         public string Type { get; set; }
    //         public string Title { get; set; }
    //         public int ViewerCount { get; set; }
    //         public string Language { get; set; }
    //         public string ThumbnailUrl { get; set; }
    //         public DateTime StartedAt { get; set; }
    //     }
    // }
}