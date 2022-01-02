using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Mewdeko._Extensions;
using Mewdeko.Modules.Searches.Common;
using Mewdeko.Services;
using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Modules.Searches.Services;

public class CryptoService : INService
{
    private readonly IDataCache _cache;
    private readonly IBotCredentials _creds;
    private readonly IHttpClientFactory _httpFactory;

    private readonly SemaphoreSlim getCryptoLock = new(1, 1);

    public CryptoService(IDataCache cache, IHttpClientFactory httpFactory, IBotCredentials creds)
    {
        _cache = cache;
        _httpFactory = httpFactory;
        _creds = creds;
    }

    public async Task<(CryptoResponseData Data, CryptoResponseData Nearest)> GetCryptoData(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return (null, null);

        name = name.ToUpperInvariant();
        var cryptos = await CryptoData().ConfigureAwait(false);

        var crypto = cryptos
            ?.FirstOrDefault(x => x.Id.ToUpperInvariant() == name || x.Name.ToUpperInvariant() == name
                                                                  || x.Symbol.ToUpperInvariant() == name);

        (CryptoResponseData Elem, int Distance)? nearest = null;
        if (crypto == null)
        {
            nearest = cryptos.Select(x => (x, Distance: x.Name.ToUpperInvariant().LevenshteinDistance(name)))
                .OrderBy(x => x.Distance)
                .Where(x => x.Distance <= 2)
                .FirstOrDefault();

            crypto = nearest?.Elem;
        }

        if (nearest != null) return (null, crypto);

        return (crypto, null);
    }

    public async Task<List<CryptoResponseData>> CryptoData()
    {
        await getCryptoLock.WaitAsync();
        try
        {
            var fullStrData = await _cache.GetOrAddCachedDataAsync("Mewdeko:crypto_data", async _ =>
            {
                try
                {
                    using var _http = _httpFactory.CreateClient();
                    var strData = await _http.GetStringAsync(new Uri(
                        "https://pro-api.coinmarketcap.com/v1/cryptocurrency/listings/latest?" +
                        $"CMC_PRO_API_KEY={_creds.CoinmarketcapApiKey}" +
                        "&start=1" +
                        "&limit=500" +
                        "&convert=USD"));

                    JsonConvert.DeserializeObject<CryptoResponse>(strData); // just to see if its' valid

                    return strData;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error getting crypto data: {Message}", ex.Message);
                    return default;
                }
            }, "", TimeSpan.FromHours(1));

            return JsonConvert.DeserializeObject<CryptoResponse>(fullStrData).Data;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error retreiving crypto data: {Message}", ex.Message);
            return default;
        }
        finally
        {
            getCryptoLock.Release();
        }
    }
}