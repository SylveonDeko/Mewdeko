using Discord;
using Discord.Interactions;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Searches.Services;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    public class CryptoCommands : MewdekoSubmodule<CryptoService>
    {
        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Crypto(string? name)
        {
            name = name?.ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(name))
                return;

            var (crypto, nearest) = await Service.GetCryptoData(name).ConfigureAwait(false);

            if (nearest != null)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(GetText("crypto_not_found"))
                    .WithDescription(GetText("did_you_mean", Format.Bold($"{nearest.Name} ({nearest.Symbol})")));

                if (await PromptUserConfirmAsync(embed, ctx.User.Id).ConfigureAwait(false)) crypto = nearest;
            }

            if (crypto == null)
            {
                await ReplyErrorLocalizedAsync("crypto_not_found").ConfigureAwait(false);
                return;
            }

            var sevenDay = decimal.TryParse(crypto.Quote.Usd.PercentChange7D, out var sd)
                ? sd.ToString("F2")
                : crypto.Quote.Usd.PercentChange7D;

            var lastDay = decimal.TryParse(crypto.Quote.Usd.PercentChange24H, out var ld)
                ? ld.ToString("F2")
                : crypto.Quote.Usd.PercentChange24H;

            await ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle($"{crypto.Name} ({crypto.Symbol})")
                    .WithUrl($"https://coinmarketcap.com/currencies/{crypto.Slug}/")
                    .WithThumbnailUrl($"https://s3.coinmarketcap.com/static/img/coins/128x128/{crypto.Id}.png")
                    .AddField(GetText("market_cap"), $"${crypto.Quote.Usd.MarketCap:n0}", true)
                    .AddField(GetText("price"), $"${crypto.Quote.Usd.Price}", true)
                    .AddField(GetText("volume_24h"), $"${crypto.Quote.Usd.Volume24H:n0}", true)
                    .AddField(GetText("change_7d_24h"), $"{sevenDay}% / {lastDay}%", true)
                    .WithImageUrl($"https://s3.coinmarketcap.com/generated/sparklines/web/7d/usd/{crypto.Id}.png"))
                .ConfigureAwait(false);
        }
    }

    public class Slow
    {
        [JsonProperty("gwei")] public int Gwei { get; set; }

        [JsonProperty("usd")] public double Usd { get; set; }
    }

    public class Normal
    {
        [JsonProperty("gwei")] public int Gwei { get; set; }

        [JsonProperty("usd")] public double Usd { get; set; }
    }

    public class Fast
    {
        [JsonProperty("gwei")] public int Gwei { get; set; }

        [JsonProperty("usd")] public double Usd { get; set; }
    }

    public class Instant
    {
        [JsonProperty("gwei")] public int Gwei { get; set; }

        [JsonProperty("usd")] public double Usd { get; set; }
    }

    public class Source
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("source")] public string Source1 { get; set; }

        [JsonProperty("fast")] public int Fast { get; set; }

        [JsonProperty("standard")] public int? Standard { get; set; }

        [JsonProperty("slow")] public int Slow { get; set; }

        [JsonProperty("lastBlock")] public int? LastBlock { get; set; }

        [JsonProperty("instant")] public int? Instant { get; set; }

        [JsonProperty("lastUpdate")] public long? LastUpdate { get; set; }
    }

    public class GasAPiResult
    {
        [JsonProperty("slow")] public Slow Slow { get; set; }

        [JsonProperty("normal")] public Normal Normal { get; set; }

        [JsonProperty("fast")] public Fast Fast { get; set; }

        [JsonProperty("instant")] public Instant Instant { get; set; }

        [JsonProperty("ethPrice")] public double EthPrice { get; set; }

        [JsonProperty("lastUpdated")] public long LastUpdated { get; set; }

        [JsonProperty("sources")] public List<Source> Sources { get; set; }
    }
}