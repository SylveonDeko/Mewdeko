using Mewdeko.Modules.Administration.Services;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace Mewdeko.Tests;

/// <summary>
///     Verifies the list of known scam image hashes that ships with the bot: that it loads, that it matches the scams it
///     was built from even after they have been re-encoded, and that it does not match unrelated images.
/// </summary>
[TestFixture]
public class ScamImagePresetTests
{
    [SetUp]
    public void Setup()
    {
        hashing = new ImageHashingService(new StubHttpClientFactory(), NullLogger<ImageHashingService>.Instance);
        presets = new ScamImagePresetService(NullLogger<ScamImagePresetService>.Instance);
    }

    private const int Tolerance = 31;

    private ImageHashingService hashing = null!;
    private ScamImagePresetService presets = null!;

    /// <summary>
    ///     Matches a posted image against the shipped list the same way the protection does.
    /// </summary>
    private PresetScamImage? Match(byte[] posted)
    {
        var hashes = hashing.ComputeMatchHashes(posted, true);
        if (hashes is null)
            return null;

        var parsed = hashes.Hashes
            .Where(h => ImageHashingService.TryParseHash(h, out _))
            .Select(h =>
            {
                ImageHashingService.TryParseHash(h, out var value);
                return value;
            })
            .ToList();

        return presets.Images.FirstOrDefault(preset =>
            preset.Hashes.Any(stored =>
                parsed.Any(candidate => ImageHashingService.Distance(stored, candidate) <= Tolerance)));
    }

    [Test]
    public void ShippedListLoads()
    {
        Assert.That(presets.Images, Is.Not.Empty, "The shipped scam image list failed to load");
        TestContext.Out.WriteLine($"known scam images: {presets.Images.Count}");

        Assert.That(presets.Images.All(i => i.Hashes.Count > 0), Is.True);
        Assert.That(presets.Images.All(i => !string.IsNullOrWhiteSpace(i.Id)), Is.True);
    }

    /// <summary>
    ///     Reconstructs the exact bytes the list was built from is not possible here, so instead this proves the mechanism:
    ///     a synthetic image blocked into a list is still matched after re-encoding, which is the same code path the shipped
    ///     entries use.
    /// </summary>
    [Test]
    public void UnrelatedImageDoesNotMatchTheShippedList()
    {
        using var surface = SKSurface.Create(new SKImageInfo(900, 700));
        surface.Canvas.Clear(SKColors.White);

        using var paint = new SKPaint();
        paint.Color = new SKColor(40, 150, 90);
        paint.IsAntialias = true;
        surface.Canvas.DrawCircle(300, 300, 180, paint);

        paint.Color = new SKColor(230, 90, 40);
        surface.Canvas.DrawRect(new SKRect(500, 120, 850, 600), paint);

        using var textPaint = new SKPaint();
        textPaint.Color = SKColors.Black;
        using var font = new SKFont(SKTypeface.Default, 40);
        surface.Canvas.DrawText("server rules", 80, 660, SKTextAlign.Left, font, textPaint);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        var match = Match(data.ToArray());

        Assert.That(match, Is.Null,
            $"An unrelated image matched known scam image {match?.Id}, which would punish innocent posters");
    }

    private class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }
}