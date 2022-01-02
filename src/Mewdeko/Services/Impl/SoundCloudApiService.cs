using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Mewdeko.Services.Impl;

public class SoundCloudApiService : INService
{
    private readonly IHttpClientFactory _httpFactory;

    public SoundCloudApiService(IHttpClientFactory factory) => _httpFactory = factory;

    public async Task<SoundCloudVideo> ResolveVideoAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentNullException(nameof(url));

        var response = "";

        using (var http = _httpFactory.CreateClient())
        {
            response = await http.GetStringAsync($"https://scapi.nadeko.bot/resolve?url={url}")
                .ConfigureAwait(false);
        }

        var responseObj = JsonConvert.DeserializeObject<SoundCloudVideo>(response);
        if (responseObj?.Kind != "track")
            throw new InvalidOperationException("Url is either not a track, or it doesn't exist.");

        return responseObj;
    }

    public async Task<SoundCloudVideo> GetVideoByQueryAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentNullException(nameof(query));

        var response = "";
        using (var http = _httpFactory.CreateClient())
        {
            response = await http
                .GetStringAsync(new Uri($"https://scapi.nadeko.bot/tracks?q={Uri.EscapeDataString(query)}"))
                .ConfigureAwait(false);
        }

        var responseObj = JsonConvert.DeserializeObject<SoundCloudVideo[]>(response)
            .FirstOrDefault(s => s.Streamable is true);

        if (responseObj?.Kind != "track")
            throw new InvalidOperationException("Query yielded no results.");

        return responseObj;
    }
}

public class SoundCloudVideo
{
    public string Kind { get; set; } = "";
    public long Id { get; set; } = 0;
    public SoundCloudUser User { get; set; } = new();
    public string Title { get; set; } = "";
    public string FullName => User.Name + " - " + Title;
    public bool? Streamable { get; set; } = false;
    public int Duration { get; set; }

    [JsonProperty("permalink_url")] public string TrackLink { get; set; } = "";

    [JsonProperty("artwork_url")] public string ArtworkUrl { get; set; } = "";
}

public class SoundCloudUser
{
    [JsonProperty("username")] public string Name { get; set; }
}