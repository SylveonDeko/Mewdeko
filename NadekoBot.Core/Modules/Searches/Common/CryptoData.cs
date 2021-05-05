using Newtonsoft.Json;
using System.Collections.Generic;

namespace NadekoBot.Core.Modules.Searches.Common
{
    public class CryptoResponse
    {
        public List<CryptoResponseData> Data { get; set; }
    }

    public class CryptoResponseData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Symbol { get; set; }
        public string Slug { get; set; }

        [JsonProperty("cmc_rank")]
        public int Rank { get; set; }
        public CurrencyQuotes Quote { get; set; }
    }

    public class CurrencyQuotes
    {
        public Quote Usd { get; set; }
    }

    public class Quote
    {
        public double Price { get; set; }
        public double Market_Cap { get; set; }
        public string Percent_Change_1h { get; set; }
        public string Percent_Change_24h { get; set; }
        public string Percent_Change_7d { get; set; }
        public double? Volume_24h { get; set; }
    }
}
