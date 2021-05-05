using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Mewdeko.Core.Services.Impl
{
    public class SoundCloudApiService : INService
    {
        private readonly IHttpClientFactory _httpFactory;

        public SoundCloudApiService(IHttpClientFactory factory)
        {
            _httpFactory = factory;
        }

        public async Task<SoundCloudVideo> ResolveVideoAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentNullException(nameof(url));

            string response = "";

            using (var http = _httpFactory.CreateClient())
            {
                response = await http.GetStringAsync($"https://scapi.Mewdeko.bot/resolve?url={url}").ConfigureAwait(false);
            }


            var responseObj = JsonConvert.DeserializeObject<SoundCloudVideo>(response);
            if (responseObj?.Kind != "track")
                throw new InvalidOperationException("Url is either not a track, or it doesn't exist.");

            return responseObj;
        }

        public bool IsSoundCloudLink(string url) =>
            System.Text.RegularExpressions.Regex.IsMatch(url, "(.*)(soundcloud.com|snd.sc)(.*)");

        public async Task<SoundCloudVideo> GetVideoByQueryAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));

            var response = "";
            using (var http = _httpFactory.CreateClient())
            {
                response = await http.GetStringAsync(new Uri($"https://scapi.Mewdeko.bot/tracks?q={Uri.EscapeDataString(query)}")).ConfigureAwait(false);
            }

            var responseObj = JsonConvert.DeserializeObject<SoundCloudVideo[]>(response).Where(s => s.Streamable is true).FirstOrDefault();
            if (responseObj?.Kind != "track")
                throw new InvalidOperationException("Query yielded no results.");

            return responseObj;
        }
    }

    public class SoundCloudVideo
    {
        public string Kind { get; set; } = "";
        public long Id { get; set; } = 0;
        public SoundCloudUser User { get; set; } = new SoundCloudUser();
        public string Title { get; set; } = "";
        [JsonIgnore]
        public string FullName => User.Name + " - " + Title;
        public bool? Streamable { get; set; } = false;
        public int Duration { get; set; }
        [JsonProperty("permalink_url")]
        public string TrackLink { get; set; } = "";
        [JsonProperty("artwork_url")]
        public string ArtworkUrl { get; set; } = "";
        public async Task<string> StreamLink()
        {
            using (var http = new HttpClient())
            {
                var url = await http.GetStringAsync(new Uri($"http://scapi.Mewdeko.bot/stream/{Id}"));
                return url;
            }
        }
    }
    public class SoundCloudUser
    {
        [JsonProperty("username")]
        public string Name { get; set; }
    }
    /*
    {"kind":"track",
    "id":238888167,
    "created_at":"2015/12/24 01:04:52 +0000",
    "user_id":43141975,
    "duration":120852,
    "commentable":true,
    "state":"finished",
    "original_content_size":4834829,
    "last_modified":"2015/12/24 01:17:59 +0000",
    "sharing":"public",
    "tag_list":"Funky",
    "permalink":"18-fd",
    "streamable":true,
    "embeddable_by":"all",
    "downloadable":false,
    "purchase_url":null,
    "label_id":null,
    "purchase_title":null,
    "genre":"Disco",
    "title":"18 Ж",
    "description":"",
    "label_name":null,
    "release":null,
    "track_type":null,
    "key_signature":null,
    "isrc":null,
    "video_url":null,
    "bpm":null,
    "release_year":null,
    "release_month":null,
    "release_day":null,
    "original_format":"mp3",
    "license":"all-rights-reserved",
    "uri":"https://api.soundcloud.com/tracks/238888167",
    "user":{
        "id":43141975,
        "kind":"user",
        "permalink":"mrb00gi",
        "username":"Mrb00gi",
        "last_modified":"2015/12/01 16:06:57 +0000",
        "uri":"https://api.soundcloud.com/users/43141975",
        "permalink_url":"http://soundcloud.com/mrb00gi",
        "avatar_url":"https://a1.sndcdn.com/images/default_avatar_large.png"
        },
    "permalink_url":"http://soundcloud.com/mrb00gi/18-fd",
    "artwork_url":null,
    "waveform_url":"https://w1.sndcdn.com/gsdLfvEW1cUK_m.png",
    "stream_url":"https://api.soundcloud.com/tracks/238888167/stream",
    "playback_count":7,
    "download_count":0,
    "favoritings_count":1,
    "comment_count":0,
    "attachments_uri":"https://api.soundcloud.com/tracks/238888167/attachments"}

    */

}
