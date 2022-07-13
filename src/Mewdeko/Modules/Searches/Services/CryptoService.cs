using Mewdeko.Modules.Searches.Common;
using Newtonsoft.Json;
using Serilog;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Searches.Services;

public class CryptoService : INService
{
    private readonly IDataCache _cache;
    private readonly IHttpClientFactory _httpFactory;

    private readonly SemaphoreSlim _getCryptoLock = new(1, 1);

    public CryptoService(IDataCache cache, IHttpClientFactory httpFactory)
    {
        _cache = cache;
        _httpFactory = httpFactory;
    }

    public async Task<(CryptoResponseData? Data, CryptoResponseData? Nearest)> GetCryptoData(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return (null, null);

        name = name.ToUpperInvariant();
        var cryptos = await CryptoData().ConfigureAwait(false);

        var crypto = cryptos
            .FirstOrDefault(x => x.Id.ToUpperInvariant() == name || x.Name.ToUpperInvariant() == name
                                                                  || x.Symbol.ToUpperInvariant() == name);

        (CryptoResponseData Elem, int Distance)? nearest = null;
        if (crypto == null)
        {
            nearest = cryptos
                      .Select(x => (x, Distance: x.Name.ToUpperInvariant().LevenshteinDistance(name)))
                      .OrderBy(x => x.Distance)
                      .FirstOrDefault(x => x.Distance <= 2);

            crypto = nearest?.Elem;
        }

        if (nearest != null) return (null, crypto);

        return (crypto, null);
    }

    public async Task<List<CryptoResponseData>> CryptoData()
    {
        await _getCryptoLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var fullStrData = await _cache.GetOrAddCachedDataAsync("Mewdeko:crypto_data", async _ =>
            {
                try
                {
                    using var http = _httpFactory.CreateClient();
                    var strData = await http.GetStringAsync(new Uri(
                        "https://pro-api.coinmarketcap.com/v1/cryptocurrency/listings/latest?CMC_PRO_API_KEY=8e6b4b51-3399-4ffb-ac25-0e11667a1a23&start=1&convert=USD")).ConfigureAwait(false);

                    JsonConvert.DeserializeObject<CryptoResponse>(strData); // just to see if its' valid

                    return strData;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error getting crypto data: {Message}", ex.Message);
                    return default;
                }
            }, "", TimeSpan.FromHours(1)).ConfigureAwait(false);

            return JsonConvert.DeserializeObject<CryptoResponse>(fullStrData).Data;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error retreiving crypto data: {Message}", ex.Message);
            return default;
        }
        finally
        {
            _getCryptoLock.Release();
        }
    }
}