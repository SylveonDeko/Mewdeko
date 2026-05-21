using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

/// <summary>
///     Downloader for images from Rule34.
/// </summary>
public class Rule34ImageDownloader : ImageDownloader<Rule34Object>
{
    private readonly string? apiKey;
    private readonly string? userId;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rule34ImageDownloader" /> class.
    /// </summary>
    /// <param name="http">The HTTP client factory.</param>
    /// <param name="apiKey">
    ///     The Rule34 API key. As of 2024 the Rule34 API rejects unauthenticated requests, so this is required for
    ///     results to be returned.
    /// </param>
    /// <param name="userId">The Rule34 user ID associated with the API key.</param>
    public Rule34ImageDownloader(IHttpClientFactory http, string? apiKey = null, string? userId = null)
        : base(Booru.Rule34, http)
    {
        this.apiKey = apiKey;
        this.userId = userId;
    }

    /// <inheritdoc />
    public override async Task<List<Rule34Object>> DownloadImagesAsync(
        string[] tags,
        int page,
        bool isExplicit = false,
        CancellationToken cancel = default)
    {
        var tagString = ImageDownloaderHelper.GetTagString(tags);
        var uri = $"https://api.rule34.xxx//index.php?page=dapi&s=post"
                  + $"&q=index"
                  + $"&json=1"
                  + $"&limit=100"
                  + $"&tags={tagString}"
                  + $"&pid={page}";

        if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(userId))
            uri += $"&api_key={Uri.EscapeDataString(apiKey)}&user_id={Uri.EscapeDataString(userId)}";

        using var http = Http.CreateClient();
        http.DefaultRequestHeaders.TryAddWithoutValidation("user-agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.60 Safari/537.36");

        List<Rule34Object>? images;
        try
        {
            images = await http.GetFromJsonAsync<List<Rule34Object>>(uri, SerializerOptions, cancel);
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }

        return images is null
            ? []
            : images.Where(img => !string.IsNullOrWhiteSpace(img.Image)).ToList();
    }
}
