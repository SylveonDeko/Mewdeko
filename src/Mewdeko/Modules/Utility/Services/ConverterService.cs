using System.IO;
using System.Net.Http;
using System.Threading;
using Mewdeko.Modules.Utility.Common;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
/// Provides services for converting units and currencies, with automatic updates for currency conversion rates.
/// </summary>
public class ConverterService : INService, IUnloadableService
{
    private readonly IDataCache cache;

    private readonly Timer currencyUpdater;
    private readonly IHttpClientFactory httpFactory;
    private readonly TimeSpan updateInterval = new(12, 0, 0);

    /// <summary>
    /// Initializes a new instance of the <see cref="ConverterService"/>.
    /// </summary>
    /// <param name="client">The Discord client, used for identifying the primary shard.</param>
    /// <param name="cache">The cache service for storing conversion units.</param>
    /// <param name="factory">The HTTP client factory for fetching currency conversion rates.</param>
    public ConverterService(DiscordSocketClient client,
        IDataCache cache, IHttpClientFactory factory)
    {
        this.cache = cache;
        httpFactory = factory;

        if (client.ShardId == 0)
        {
            currencyUpdater = new Timer(
                async shouldLoad => await UpdateCurrency((bool)shouldLoad).ConfigureAwait(false),
                client.ShardId == 0,
                TimeSpan.Zero,
                updateInterval);
        }
    }

    /// <summary>
    /// Gets the current set of conversion units, including currencies, from the cache.
    /// </summary>
    public ConvertUnit[] Units =>
        cache.Redis.GetDatabase()
            .StringGet("converter_units")
            .ToString()
            .MapJson<ConvertUnit[]>();

    /// <summary>
    /// Unloads the service, stopping any background tasks such as the currency updater.
    /// </summary>
    /// <returns>A task representing the asynchronous unload operation.</returns>
    public Task Unload()
    {
        currencyUpdater.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    private async Task<Rates> GetCurrencyRates()
    {
        using var http = httpFactory.CreateClient();
        var res = await http.GetStringAsync("https://convertapi.nadeko.bot/latest").ConfigureAwait(false);
        return JsonConvert.DeserializeObject<Rates>(res);
    }

    private async Task UpdateCurrency(bool shouldLoad)
    {
        try
        {
            const string unitTypeString = "currency";
            if (shouldLoad)
            {
                var currencyRates = await GetCurrencyRates().ConfigureAwait(false);
                var baseType = new ConvertUnit
                {
                    Triggers =
                    [
                        currencyRates.Base
                    ],
                    Modifier = decimal.One,
                    UnitType = unitTypeString
                };
                var range = currencyRates.ConversionRates.Select(u => new ConvertUnit
                {
                    Triggers =
                    [
                        u.Key
                    ],
                    Modifier = u.Value,
                    UnitType = unitTypeString
                }).ToArray();

                var fileData = (JsonConvert.DeserializeObject<ConvertUnit[]>(
                                    await File.ReadAllTextAsync("data/units.json").ConfigureAwait(false)) ??
                                Array.Empty<ConvertUnit>())
                    .Where(x => x.UnitType != "currency");

                var data = JsonConvert.SerializeObject(range.Append(baseType).Concat(fileData).ToList());
                cache.Redis.GetDatabase()
                    .StringSet("converter_units", data, flags: CommandFlags.FireAndForget);
            }
        }
        catch
        {
            // ignored
        }
    }
}

/// <summary>
/// Represents the currency rates for conversion.
/// </summary>
public class Rates
{
    /// <summary>
    /// Gets or sets the base currency used for conversion.
    /// </summary>
    public string Base { get; set; }

    /// <summary>
    /// Gets or sets the date when the rates were last updated.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets the conversion rates for different currencies.
    /// The key is the currency code and the value is the conversion rate to the base currency.
    /// </summary>
    [JsonProperty("rates")]
    public Dictionary<string, decimal> ConversionRates { get; set; }
}