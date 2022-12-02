using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches.Common;

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
    public double MarketCap { get; set; }
    public string PercentChange1H { get; set; }
    public string PercentChange24H { get; set; }
    public string PercentChange7D { get; set; }
    public double? Volume24H { get; set; }
}