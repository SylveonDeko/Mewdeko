using Mewdeko.Modules.Administration.Services;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace Mewdeko.Tests;

/// <summary>
///     Verifies the anti-image-hash matching against the transformations a re-posted scam image actually goes through.
///     PDQ handles re-encoding, rescaling and brightness on its own; mirroring and borders only pass because of the
///     variant hashes, so these tests are what stop that from silently regressing. Cropping is asserted to fail, because
///     it does, and pretending otherwise would be worse than the gap itself.
/// </summary>
[TestFixture]
public class ImageHashingServiceTests
{
    [SetUp]
    public void Setup()
    {
        service = new ImageHashingService(new StubHttpClientFactory(),
            NullLogger<ImageHashingService>.Instance);
    }

    /// <summary>
    ///     PDQ's standard "same image" threshold, and the default the protection ships with.
    /// </summary>
    private const int Tolerance = 31;

    private ImageHashingService service = null!;

    /// <summary>
    ///     Matches a posted image against a blocked image exactly the way the protection does: every stored variant against
    ///     every posted variant, keeping the closest.
    /// </summary>
    private int BestDistance(byte[] blocked, byte[] posted)
    {
        var blockedSet = service.ComputeHashSet(blocked);
        var postedSet = service.ComputeMatchHashes(posted, true);

        Assert.That(blockedSet, Is.Not.Null);
        Assert.That(postedSet, Is.Not.Null);

        var stored = new List<string>
        {
            blockedSet!.Hash
        };
        stored.AddRange(blockedSet.Variants);

        var best = int.MaxValue;

        foreach (var storedHash in stored)
        {
            foreach (var postedHash in postedSet!.Hashes)
            {
                Assert.That(ImageHashingService.TryParseHash(storedHash, out var left), Is.True);
                Assert.That(ImageHashingService.TryParseHash(postedHash, out var right), Is.True);

                best = Math.Min(best, ImageHashingService.Distance(left, right));
            }
        }

        return best;
    }

    [Test]
    public void MatchesReEncodedAndResizedCopy()
    {
        var blocked = Poster(800, 600, SKEncodedImageFormat.Png, 100);
        var posted = Poster(320, 240, SKEncodedImageFormat.Jpeg, 55);

        var distance = BestDistance(blocked, posted);
        TestContext.Out.WriteLine($"resized + jpeg q55: {distance}/256");

        Assert.That(distance, Is.LessThanOrEqualTo(Tolerance));
    }

    /// <summary>
    ///     Documents the constraint that shapes the whole design: PDQ is so sensitive to framing that a modest crop blows
    ///     the entire match budget. Measured on real scam photos, even a 1% crop costs about 30 of the 256 bits. This is why
    ///     blocked images carry mirrored and border-stripped variants rather than a set of guessed crops, which would only
    ///     ever catch the crops that were guessed. The synthetic poster here is flatter than a real photo and so drifts more
    ///     slowly, hence the larger crop.
    /// </summary>
    [Test]
    public void CroppingMovesTheHashBeyondTolerance()
    {
        var original = Poster(800, 600, SKEncodedImageFormat.Png, 100);
        var cropped = Poster(800, 600, SKEncodedImageFormat.Png, 100, 1f, 0.10f);

        var distance = BestDistance(original, cropped);
        TestContext.Out.WriteLine($"cropped 10%: {distance}/256 (tolerance {Tolerance})");

        Assert.That(distance, Is.GreaterThan(Tolerance),
            "If a crop now lands inside tolerance, PDQ's crop sensitivity has changed and the border-stripping design " +
            "should be revisited");
    }

    [Test]
    public void MatchesMirroredCopy()
    {
        var blocked = Poster(800, 600, SKEncodedImageFormat.Png, 100);
        var posted = Poster(800, 600, SKEncodedImageFormat.Png, 100, 1f, 0f, "CLAIM YOUR BTC NOW", true);

        var distance = BestDistance(blocked, posted);
        TestContext.Out.WriteLine($"mirrored: {distance}/256");

        Assert.That(distance, Is.LessThanOrEqualTo(Tolerance),
            "A flipped re-upload escaped the blocklist; the mirrored variant hashes have regressed");
    }

    /// <summary>
    ///     The border width is deliberately an awkward number: a border is caught because it is measured and stripped, so
    ///     this must pass for any width rather than for widths the service happens to guess at.
    /// </summary>
    [Test]
    public void MatchesCopyWrappedInABorder()
    {
        var blocked = Poster(800, 600, SKEncodedImageFormat.Png, 100);
        var posted = AddBorder(blocked, 0.11f);

        var distance = BestDistance(blocked, posted);
        TestContext.Out.WriteLine($"border added: {distance}/256");

        Assert.That(distance, Is.LessThanOrEqualTo(Tolerance),
            "A bordered re-upload escaped the blocklist; border stripping has regressed");
    }

    [Test]
    public void DoesNotMatchDifferentImage()
    {
        var blocked = Poster(800, 600, SKEncodedImageFormat.Png, 100);
        var posted = OtherPoster(800, 600);

        var distance = BestDistance(blocked, posted);
        TestContext.Out.WriteLine($"different image: {distance}/256");

        Assert.That(distance, Is.GreaterThan(Tolerance),
            "A different image matched the blocklist, which would ban innocent posters");
    }

    [Test]
    public void ReportsLowQualityForFlatImages()
    {
        using var surface = SKSurface.Create(new SKImageInfo(600, 600));
        surface.Canvas.Clear(new SKColor(128, 128, 128));
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);

        var result = service.ComputeHashSet(data.ToArray());

        Assert.That(result, Is.Not.Null);
        TestContext.Out.WriteLine($"flat image quality: {result!.Quality}/100");
        Assert.That(result.Quality, Is.LessThan(ImageHashingService.MinReliableQuality),
            "A flat image should be rejected as unhashable rather than blocked");
    }

    [Test]
    public void ReturnsNullForNonImageData()
    {
        Assert.That(service.ComputeHashSet("this is not an image"u8.ToArray()), Is.Null);
    }

    /// <summary>
    ///     Draws a fixed "scam poster" scene, then encodes it at the requested size and quality, so two calls differ only by
    ///     the transformations a re-upload would apply.
    /// </summary>
    private static byte[] Poster(int width, int height, SKEncodedImageFormat format, int quality,
        float brightness = 1f, float cropPct = 0f, string caption = "CLAIM YOUR BTC NOW", bool mirrored = false)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        if (mirrored)
        {
            canvas.Translate(width, 0);
            canvas.Scale(-1, 1);
        }

        var sx = width / 800f;
        var sy = height / 600f;

        using var sky = new SKPaint();
        sky.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, 600 * sy),
            [new SKColor(20, 24, 70), new SKColor(90, 40, 120)], null, SKShaderTileMode.Clamp);
        canvas.DrawRect(new SKRect(0, 0, 800 * sx, 600 * sy), sky);

        using var face = new SKPaint();
        face.Color = new SKColor(235, 190, 150);
        face.IsAntialias = true;
        canvas.DrawCircle(230 * sx, 300 * sy, 130 * Math.Min(sx, sy), face);

        using var hair = new SKPaint();
        hair.Color = new SKColor(60, 40, 30);
        canvas.DrawRect(new SKRect(120 * sx, 180 * sy, 340 * sx, 240 * sy), hair);

        using var bar = new SKPaint();
        bar.Color = new SKColor(250, 210, 30);
        canvas.DrawRect(new SKRect(420 * sx, 220 * sy, 770 * sx, 330 * sy), bar);

        using var coin = new SKPaint();
        coin.Color = new SKColor(240, 160, 20);
        coin.IsAntialias = true;
        canvas.DrawCircle(600 * sx, 450 * sy, 70 * Math.Min(sx, sy), coin);

        using var textPaint = new SKPaint();
        textPaint.Color = SKColors.White;
        using var font = new SKFont(SKTypeface.Default, 28 * Math.Min(sx, sy));
        canvas.DrawText(caption, 60 * sx, 550 * sy, SKTextAlign.Left, font, textPaint);

        using var image = surface.Snapshot();

        if (cropPct > 0)
        {
            var cw = (int)(width * (1 - cropPct));
            var ch = (int)(height * (1 - cropPct));
            using var cropped = image.Subset(new SKRectI((width - cw) / 2, (height - ch) / 2,
                (width - cw) / 2 + cw, (height - ch) / 2 + ch));
            using var croppedData = cropped.Encode(format, quality);
            return croppedData.ToArray();
        }

        if (Math.Abs(brightness - 1f) > 0.001)
        {
            using var bright = SKSurface.Create(new SKImageInfo(width, height));
            using var brightPaint = new SKPaint();
            brightPaint.ColorFilter = SKColorFilter.CreateColorMatrix([
                brightness, 0, 0, 0, 0,
                0, brightness, 0, 0, 0,
                0, 0, brightness, 0, 0,
                0, 0, 0, 1, 0
            ]);
            bright.Canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear), brightPaint);
            using var brightImage = bright.Snapshot();
            using var brightData = brightImage.Encode(format, quality);
            return brightData.ToArray();
        }

        using var data = image.Encode(format, quality);
        return data.ToArray();
    }

    /// <summary>
    ///     Pads an image with a solid border, the way a scammer wrapping the original in a frame or watermark strip would.
    /// </summary>
    private static byte[] AddBorder(byte[] source, float borderPct)
    {
        using var original = SKBitmap.Decode(source);
        var pad = (int)(Math.Max(original.Width, original.Height) * borderPct);

        using var surface = SKSurface.Create(new SKImageInfo(original.Width + 2 * pad, original.Height + 2 * pad));
        surface.Canvas.Clear(new SKColor(10, 10, 10));

        using var image = SKImage.FromBitmap(original);
        surface.Canvas.DrawImage(image, pad, pad, new SKSamplingOptions(SKFilterMode.Linear));

        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    ///     A different scam poster: same genre and palette, different composition. This is the false positive risk case.
    /// </summary>
    private static byte[] OtherPoster(int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(15, 15, 40));

        var sx = width / 800f;
        var sy = height / 600f;

        using var moon = new SKPaint();
        moon.Color = new SKColor(200, 200, 210);
        moon.IsAntialias = true;
        canvas.DrawCircle(560 * sx, 280 * sy, 140 * Math.Min(sx, sy), moon);

        using var bar = new SKPaint();
        bar.Color = new SKColor(40, 180, 120);
        canvas.DrawRect(new SKRect(40 * sx, 380 * sy, 420 * sx, 470 * sy), bar);

        using var textPaint = new SKPaint();
        textPaint.Color = SKColors.White;
        using var font = new SKFont(SKTypeface.Default, 30 * Math.Min(sx, sy));
        canvas.DrawText("ETH GIVEAWAY LIVE", 50 * sx, 540 * sy, SKTextAlign.Left, font, textPaint);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }
}