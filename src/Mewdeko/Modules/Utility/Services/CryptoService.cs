using System.IO;
using System.Net.Http;
using System.Text.Json;
using SkiaSharp;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
/// Service that provides information for crypto coins, its finally back after being removed ages ago
/// </summary>
public class CryptoService(IHttpClientFactory httpFactory) : INService
{
    private async Task<List<float>> GetCryptoPriceData(string cryptoId, int days)
    {
        using var http = httpFactory.CreateClient();
        var res = await http
            .GetStringAsync(
                $"https://api.coingecko.com/api/v3/coins/{cryptoId}/market_chart?vs_currency=usd&days={days}")
            .ConfigureAwait(false);
        var json = JsonDocument.Parse(res);
        var prices = json.RootElement.GetProperty("prices").EnumerateArray()
            .Select(p => (float)p[1].GetDecimal()).ToList();
        return prices;
    }

    /// <summary>
    /// Generates the actual chart
    /// </summary>
    /// <param name="cryptoId">The name of the crypto to return data for</param>
    /// <param name="days">Number of days to fetch data for</param>
    /// <returns></returns>
    public async Task<Tuple<Stream, Embed>> GenerateCryptoPriceChartAsync(string cryptoId, int days)
    {
        var prices = await GetCryptoPriceData(cryptoId, days);

        const int width = 800;
        const int height = 400;
        const int padding = 50;
        var widthWithPadding = width - 2 * padding;
        var heightWithPadding = height - 2 * padding;

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(new SKColor(38, 50, 56));

        var gridPaint = new SKPaint
        {
            Color = new SKColor(55, 71, 79), Style = SKPaintStyle.Stroke
        };

        var paint = new SKPaint
        {
            Color = SKColors.White, StrokeWidth = 3, IsAntialias = true
        };

        var gainPaint = new SKPaint
        {
            Color = SKColors.Green, Style = SKPaintStyle.Fill
        };

        var lossPaint = new SKPaint
        {
            Color = SKColors.Red, Style = SKPaintStyle.Fill
        };

        var maxPrice = prices.Max();
        var minPrice = prices.Min();
        var priceRange = maxPrice - minPrice;

        var scaleX = widthWithPadding / (float)(prices.Count - 1);

        for (var i = 0; i <= 5; i++)
        {
            var percentage = i / 5f;
            var y = height - (padding + percentage * heightWithPadding);

            canvas.DrawLine(padding, y, width - padding, y, gridPaint);

            var label = (minPrice + percentage * priceRange).ToString("0.00");
            canvas.DrawText(label, padding - 10 - paint.MeasureText(label), y + 5, paint);
        }

        for (var i = 0; i < prices.Count; i++)
        {
            var x = padding + i * scaleX;
            var date = DateTime.UtcNow.AddDays(-days + i).ToString("dd/MM");
            canvas.DrawLine(x, padding, x, height - padding, gridPaint);
            canvas.DrawText(date, x - (paint.MeasureText(date) / 2), height - (padding / 2), paint);
        }

        SKPath path = new SKPath();
        for (var i = 0; i < prices.Count; i++)
        {
            var pricePercentage = (prices[i] - minPrice) / priceRange;
            var x = padding + i * scaleX;
            var y = height - padding - (pricePercentage * heightWithPadding);

            if (i == 0)
            {
                path.MoveTo(x, y);
            }
            else
            {
                path.LineTo(x, y);
            }

            if (i > 0)
            {
                var prevPricePercentage = (prices[i - 1] - minPrice) / priceRange;
                var prevY = height - padding - (prevPricePercentage * heightWithPadding);

                if (prices[i] > prices[i - 1])
                {
                    canvas.DrawRect(x - scaleX / 2, y, scaleX, prevY - y, gainPaint);
                }
                else
                {
                    canvas.DrawRect(x - scaleX / 2, prevY, scaleX, y - prevY, lossPaint);
                }
            }
        }

        paint.Style = SKPaintStyle.Stroke;
        canvas.DrawPath(path, paint);

        var imageStream = new MemoryStream();
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        {
            data.SaveTo(imageStream);
        }

        imageStream.Position = 0;

        var embedBuilder = new EmbedBuilder()
            .WithTitle($"Crypto Price Chart for {cryptoId.ToUpper()} (Last {days} Days)")
            .WithOkColor()
            .WithCurrentTimestamp();

        var peakPrice = prices.Max();
        var lowestPrice = prices.Min();
        var averagePrice = prices.Average();

        embedBuilder.AddField("Peak Price", peakPrice.ToString("0.00"), true);
        embedBuilder.AddField("Lowest Price", lowestPrice.ToString("0.00"), true);
        embedBuilder.AddField("Average Price", averagePrice.ToString("0.00"), true);
        embedBuilder.WithImageUrl("attachment://cryptopricechart.png");

        return new Tuple<Stream, Embed>(imageStream, embedBuilder.Build());
    }

}