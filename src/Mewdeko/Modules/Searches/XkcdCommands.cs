using System.Net.Http;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    /// <summary>
    ///     Provides commands for fetching and displaying XKCD comics.
    /// </summary>
    [Group]
    public class XkcdCommands(IHttpClientFactory factory) : MewdekoSubmodule
    {
        private const string XkcdUrl = "https://xkcd.com";

        /// <summary>
        ///     Fetches and displays an XKCD comic. If 'latest' is specified, fetches the latest comic; otherwise, fetches a random
        ///     comic.
        /// </summary>
        /// <param name="arg">The comic number to fetch or 'latest' for the latest comic.</param>
        [Cmd]
        [Aliases]
        [Priority(0)]
        public async Task Xkcd(string? arg = null)
        {
            if (arg?.ToLowerInvariant().Trim() == "latest")
            {
                try
                {
                    using var http = factory.CreateClient();
                    var res = await http.GetStringAsync($"{XkcdUrl}/info.0.json").ConfigureAwait(false);
                    var comic = JsonConvert.DeserializeObject<XkcdComic>(res);
                    var embed = new EmbedBuilder().WithColor(Mewdeko.OkColor)
                        .WithImageUrl(comic.ImageLink)
                        .WithAuthor(eab =>
                            eab.WithName(comic.Title).WithUrl($"{XkcdUrl}/{comic.Num}")
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

            await Xkcd(new MewdekoRandom().Next(1, 2607)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Fetches and displays an XKCD comic by its number.
        /// </summary>
        /// <param name="num">The number of the XKCD comic to fetch.</param>
        [Cmd]
        [Aliases]
        [Priority(1)]
        public async Task Xkcd(int num)
        {
            if (num < 1)
                return;
            try
            {
                using var http = factory.CreateClient();
                var res = await http.GetStringAsync($"{XkcdUrl}/{num}/info.0.json").ConfigureAwait(false);

                var comic = JsonConvert.DeserializeObject<XkcdComic>(res);
                var embed = new EmbedBuilder().WithColor(Mewdeko.OkColor)
                    .WithImageUrl(comic.ImageLink)
                    .WithAuthor(eab =>
                        eab.WithName(comic.Title).WithUrl($"{XkcdUrl}/{num}")
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
}