using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    [Group]
    public class XkcdCommands : MewdekoSubmodule
    {
        private const string _xkcdUrl = "https://xkcd.com";
        private readonly IHttpClientFactory _httpFactory;

        public XkcdCommands(IHttpClientFactory factory) => _httpFactory = factory;

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [Priority(0)]
        public async Task Xkcd(string arg = null)
        {
            if (arg?.ToLowerInvariant().Trim() == "latest")
            {
                try
                {
                    using var http = _httpFactory.CreateClient();
                    var res = await http.GetStringAsync($"{_xkcdUrl}/info.0.json").ConfigureAwait(false);
                    var comic = JsonConvert.DeserializeObject<XkcdComic>(res);
                    var embed = new EmbedBuilder().WithColor(Mewdeko.Services.Mewdeko.OkColor)
                        .WithImageUrl(comic.ImageLink)
                        .WithAuthor(eab =>
                            eab.WithName(comic.Title).WithUrl($"{_xkcdUrl}/{comic.Num}")
                                .WithIconUrl("https://xkcd.com/s/919f27.ico"))
                        .AddField(efb =>
                            efb.WithName(GetText("comic_number")).WithValue(comic.Num.ToString())
                                .WithIsInline(true))
                        .AddField(efb =>
                            efb.WithName(GetText("date")).WithValue($"{comic.Month}/{comic.Year}")
                                .WithIsInline(true));
                    var sent = await ctx.Channel.EmbedAsync(embed)
                        .ConfigureAwait(false);

                    await Task.Delay(10000).ConfigureAwait(false);

                    await sent.ModifyAsync(m =>
                            m.Embed = embed.AddField(efb =>
                                efb.WithName("Alt").WithValue(comic.Alt).WithIsInline(false)).Build())
                        .ConfigureAwait(false);
                }
                catch (HttpRequestException)
                {
                    await ReplyErrorLocalizedAsync("comic_not_found").ConfigureAwait(false);
                }

                return;
            }

            await Xkcd(new MewdekoRandom().Next(1, 1750)).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [Priority(1)]
        public async Task Xkcd(int num)
        {
            if (num < 1)
                return;
            try
            {
                using var http = _httpFactory.CreateClient();
                var res = await http.GetStringAsync($"{_xkcdUrl}/{num}/info.0.json").ConfigureAwait(false);

                var comic = JsonConvert.DeserializeObject<XkcdComic>(res);
                var embed = new EmbedBuilder().WithColor(Mewdeko.Services.Mewdeko.OkColor)
                    .WithImageUrl(comic.ImageLink)
                    .WithAuthor(eab =>
                        eab.WithName(comic.Title).WithUrl($"{_xkcdUrl}/{num}")
                            .WithIconUrl("https://xkcd.com/s/919f27.ico"))
                    .AddField(efb =>
                        efb.WithName(GetText("comic_number")).WithValue(comic.Num.ToString())
                            .WithIsInline(true))
                    .AddField(efb =>
                        efb.WithName(GetText("date")).WithValue($"{comic.Month}/{comic.Year}")
                            .WithIsInline(true));
                var sent = await ctx.Channel.EmbedAsync(embed)
                    .ConfigureAwait(false);

                await Task.Delay(10000).ConfigureAwait(false);

                await sent.ModifyAsync(m =>
                        m.Embed = embed
                            .AddField(efb => efb.WithName("Alt").WithValue(comic.Alt).WithIsInline(false))
                            .Build())
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                await ReplyErrorLocalizedAsync("comic_not_found").ConfigureAwait(false);
            }
        }
    }

    public class XkcdComic
    {
        public int Num { get; set; }
        public string Month { get; set; }
        public string Year { get; set; }

        [JsonProperty("safe_title")] public string Title { get; set; }

        [JsonProperty("img")] public string ImageLink { get; set; }

        public string Alt { get; set; }
    }
}