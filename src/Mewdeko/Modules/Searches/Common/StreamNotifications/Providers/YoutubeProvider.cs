// Disabled until google makes their api not shit


// using System.Net.Http;
// using System.Text.RegularExpressions;
// using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
// using Google.Apis.YouTube.v3;
// using Google.Apis.Services;
// using HtmlAgilityPack;
// using Serilog;
// using Channel = Google.Apis.YouTube.v3.Data.Channel;
//
// namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Providers
// {
//     public partial class YouTubeProvider : Provider
//     {
//         private static Regex Regex { get; } = MyRegex();
//
//
//         public override FollowedStream.FType Platform
//             => FollowedStream.FType.Youtube;
//
//         private readonly YouTubeService youtubeService;
//
//         public YouTubeProvider(IBotCredentials credsProvider)
//         {
//             var creds = credsProvider;
//             var apiKey = creds.GoogleApiKey;
//             youtubeService = new YouTubeService(new BaseClientService.Initializer
//             {
//                 ApiKey = apiKey,
//                 ApplicationName = GetType().ToString()
//             });
//         }
//
//         public override Task<bool> IsValidUrl(string url)
//         {
//             var match = Regex.Match(url);
//             if (!match.Success)
//             {
//                 return Task.FromResult(false);
//             }
//
//             // If the URL has a channel ID, it's valid even if the channel name is not present
//             if (match.Groups["id"].Success)
//             {
//                 return Task.FromResult(true);
//             }
//
//             // If the URL has a channel name, it's also valid
//             if (match.Groups["name"].Success)
//             {
//                 return Task.FromResult(true);
//             }
//
//             // If neither the channel ID nor the channel name is present, the URL is not valid
//             return Task.FromResult(false);
//         }
//
//         public override Task<StreamData?> GetStreamDataByUrlAsync(string url)
//         {
//             var match = Regex.Match(url);
//             if (!match.Success) return Task.FromResult<StreamData>(null);
//
//             // Use the channel ID if it's present, otherwise use the channel name
//             var channelIdOrName = match.Groups["id"].Success
//                 ? match.Groups["id"].Value
//                 : match.Groups["name"].Value;
//
//             return GetStreamDataAsync(channelIdOrName);
//         }
//
//         public override async Task<StreamData?> GetStreamDataAsync(string channelId)
//         {
//             var data = await GetStreamDataAsync(new List<string>
//             {
//                 channelId
//             }).ConfigureAwait(false);
//
//             return data.FirstOrDefault();
//         }
//
//         public override async Task<IReadOnlyCollection<StreamData>> GetStreamDataAsync(List<string> channelIds)
//         {
//             if (youtubeService.ApiKey == null)
//             {
//                 Log.Warning("YouTube API key is not set, skipping YouTube stream notifications");
//                 return new List<StreamData>();
//             }
//             var httpClient = new HttpClient();
//             var htmlDocument = new HtmlDocument();
//             var streamDataList = new List<StreamData>();
//
//             foreach (var channelId in channelIds)
//             {
//                 var actualChannelId = channelId;
//
//                 // Check if the channelId is actually a URL
//                 if (actualChannelId.StartsWith("https://"))
//                 {
//                     // Fetch the HTML of the page
//                     var html = await httpClient.GetStringAsync(actualChannelId);
//                     htmlDocument.LoadHtml(html);
//
//                     // Find the meta tag with property="og:url"
//                     var metaTag = htmlDocument.DocumentNode.SelectSingleNode("//meta[@property='og:url']");
//
//                     // Extract the content of the meta tag, which should be the URL of the channel
//                     var channelUrl = metaTag.GetAttributeValue("content", string.Empty);
//
//                     // Extract the actual channelId from the URL
//                     actualChannelId = channelUrl.Split('/').Last();
//                 }
//
//                 // Create a new search list request
//                 var searchListRequest = youtubeService.Search.List("snippet");
//                 searchListRequest.ChannelId = actualChannelId;
//                 searchListRequest.Type = "video";
//                 searchListRequest.EventType = SearchResource.ListRequest.EventTypeEnum.Live;
//                 searchListRequest.MaxResults = 1;
//
//                 // Execute the request
//                 var searchListResponse = await searchListRequest.ExecuteAsync();
//
//                 // If the response is empty, continue to the next channelId
//                 if (searchListResponse.Items.Count <= 0) continue;
//
//                 var liveBroadcast = searchListResponse.Items[0];
//
//                 // Create a new StreamData object for this channel
//                 var channelData = new StreamData
//                 {
//                     UniqueName = liveBroadcast.Snippet.ChannelId,
//                     Name = liveBroadcast.Snippet.ChannelTitle,
//                     AvatarUrl = liveBroadcast.Snippet.Thumbnails.Default__.Url,
//                     IsLive = true,
//                     StreamUrl = $"https://www.youtube.com/watch?v={liveBroadcast.Id.VideoId}",
//                     StreamType = FollowedStream.FType.Youtube,
//                     Preview = liveBroadcast.Snippet.Thumbnails.Default__.Url,
//                     Title = liveBroadcast.Snippet.Title,
//                 };
//
//                 // Add the new StreamData object to the list
//                 streamDataList.Add(channelData);
//             }
//
//             return streamDataList;
//         }
//
//
//
//
//         private static StreamData UserToStreamData(Channel channel)
//         => new()
//         {
//             UniqueName = channel.Id,
//             Name = channel.Snippet.Title,
//             AvatarUrl = channel.Snippet.Thumbnails.Default__.Url,
//             IsLive = false, // needs to be updated after we check if the channel is live
//             StreamUrl = $"https://www.youtube.com/channel/{channel.Id}",
//             StreamType = FollowedStream.FType.Youtube,
//             Preview = channel.Snippet.Thumbnails.Default__.Url
//         };
//         [GeneratedRegex(@"(youtube.com/@(?<name>[^/]+)/?|youtube.com/channel/(?<id>[^/]+)/?)", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
//         private static partial Regex MyRegex();
//     }
// }

