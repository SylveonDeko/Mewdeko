using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;

namespace Mewdeko.Modules.Nsfw.Common.Downloaders;

public class Rule34ImageDownloader : ImageDownloader<Rule34Object>
{
    public Rule34ImageDownloader(IHttpClientFactory http)
        : base(Booru.Rule34, http)
    {
    }

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

        using var http = _http.CreateClient();
        http.DefaultRequestHeaders
            .TryAddWithoutValidation("cookie", "cf_clearance=Gg3bVffg9fOL_.9fIdKmu5PJS86eTI.yTrhbR8z2tPc-1652310659-0-250");

        http.DefaultRequestHeaders
            .TryAddWithoutValidation("user-agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.60 Safari/537.36");
        var images = await http.GetFromJsonAsync<List<Rule34Object>>(uri, _serializerOptions, cancel);

        return images is null ? new List<Rule34Object>() : images.Where(img => !string.IsNullOrWhiteSpace(img.Image)).ToList();
    }
}