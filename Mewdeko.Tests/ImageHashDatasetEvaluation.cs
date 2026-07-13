using Mewdeko.Modules.Administration.Services;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace Mewdeko.Tests;

/// <summary>
///     Evaluates anti-image-hash matching against a corpus of real scam images. Explicit because it needs a local dataset
///     and takes minutes to run: point <see cref="DatasetPath" /> at a folder of images and run it by name.
/// </summary>
[TestFixture]
[Explicit("Requires a local scam image dataset")]
public class ImageHashDatasetEvaluation
{
    [SetUp]
    public void Setup()
    {
        service = new ImageHashingService(new StubHttpClientFactory(), NullLogger<ImageHashingService>.Instance);
    }

    private const string DatasetPath = "/Users/sylveondeko/Downloads/dataset (1)";
    private const int Tolerance = 31;

    private ImageHashingService service = null!;

    private static List<string> DatasetImages()
    {
        return Directory
            .EnumerateFiles(DatasetPath, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToList();
    }

    private static string Label(string path)
    {
        var dir = Path.GetFileName(Path.GetDirectoryName(path))!;
        return $"{dir}/{Path.GetFileName(path)}";
    }

    /// <summary>
    ///     Matches a posted image against a blocked one exactly as the protection does: every stored variant against every
    ///     posted variant, keeping the closest.
    /// </summary>
    private static int Best(ImageHashSet blocked, ImageMatchHashes posted)
    {
        var stored = new List<string>
        {
            blocked.Hash
        };
        stored.AddRange(blocked.Variants);

        var best = int.MaxValue;

        foreach (var s in stored)
        {
            if (!ImageHashingService.TryParseHash(s, out var left))
                continue;

            foreach (var p in posted.Hashes)
            {
                if (!ImageHashingService.TryParseHash(p, out var right))
                    continue;

                best = Math.Min(best, ImageHashingService.Distance(left, right));
            }
        }

        return best;
    }

    /// <summary>
    ///     Reports quality scores, and the distance between every pair of distinct images in the corpus. Pairs that land
    ///     within tolerance are either genuine near duplicates (the same scam photographed twice) or false positives, so they
    ///     are listed for inspection.
    /// </summary>
    [Test]
    public void ReportSeparationBetweenDistinctScams()
    {
        var files = DatasetImages();
        TestContext.Out.WriteLine($"images: {files.Count}\n");

        var sets = new Dictionary<string, ImageHashSet>();
        var matches = new Dictionary<string, ImageMatchHashes>();

        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
            var set = service.ComputeHashSet(bytes);
            var match = service.ComputeMatchHashes(bytes, true);

            if (set is null || match is null)
            {
                TestContext.Out.WriteLine($"UNREADABLE: {Label(file)}");
                continue;
            }

            sets[file] = set;
            matches[file] = match;
        }

        var lowQuality = sets
            .Where(s => s.Value.Quality < ImageHashingService.MinReliableQuality)
            .ToList();

        TestContext.Out.WriteLine($"quality: min={sets.Values.Min(s => s.Quality)} " +
                                  $"median={sets.Values.OrderBy(s => s.Quality).ElementAt(sets.Count / 2).Quality} " +
                                  $"max={sets.Values.Max(s => s.Quality)}");
        TestContext.Out.WriteLine($"rejected as too plain (quality < {ImageHashingService.MinReliableQuality}): " +
                                  $"{lowQuality.Count}");
        foreach (var (file, set) in lowQuality)
            TestContext.Out.WriteLine($"   {set.Quality,3}  {Label(file)}");

        var keys = sets.Keys.ToList();
        var pairDistances = new List<(int Distance, string A, string B)>();

        for (var i = 0; i < keys.Count; i++)
        {
            for (var j = i + 1; j < keys.Count; j++)
            {
                var distance = Best(sets[keys[i]], matches[keys[j]]);
                pairDistances.Add((distance, Label(keys[i]), Label(keys[j])));
            }
        }

        var within = pairDistances.Where(p => p.Distance <= Tolerance).OrderBy(p => p.Distance).ToList();

        TestContext.Out.WriteLine($"\npairs compared: {pairDistances.Count}");
        TestContext.Out.WriteLine($"pairs within tolerance {Tolerance}: {within.Count}");
        TestContext.Out.WriteLine(
            $"distance percentiles: p1={Percentile(pairDistances, 0.01)} p5={Percentile(pairDistances, 0.05)} " +
            $"median={Percentile(pairDistances, 0.5)}");

        TestContext.Out.WriteLine("\nclosest 40 pairs (inspect: same scam re-photographed, or false positive?):");
        foreach (var (distance, a, b) in pairDistances.OrderBy(p => p.Distance).Take(40))
            TestContext.Out.WriteLine($"  {distance,3}  {a}   <->   {b}");
    }

    private static int Percentile(List<(int Distance, string A, string B)> pairs, double p)
    {
        var ordered = pairs.Select(x => x.Distance).OrderBy(x => x).ToList();
        return ordered[(int)Math.Clamp(p * (ordered.Count - 1), 0, ordered.Count - 1)];
    }

    /// <summary>
    ///     Blocks each image, then re-posts a transformed copy of it and reports how often the protection still catches it.
    /// </summary>
    [Test]
    public void ReportRecallAgainstTransforms()
    {
        var files = DatasetImages();

        // Deliberately awkward numbers: the transforms must not line up with anything the service does internally,
        // or the test just measures its own assumptions back at itself.
        (string Name, Func<byte[], byte[]> Apply)[] transforms =
        [
            ("resize 37%", b => Reencode(b, 0.37f, SKEncodedImageFormat.Png, 100)),
            ("jpeg q40 + resize 63%", b => Reencode(b, 0.63f, SKEncodedImageFormat.Jpeg, 40)),
            ("discord recompress", b => Reencode(b, 0.8f, SKEncodedImageFormat.Jpeg, 75)),
            ("mirror", Mirror),
            ("brightness +25%", b => Brightness(b, 1.25f)),
            ("black border 7%", b => Border(b, 0.07f, new SKColor(0, 0, 0))),
            ("black border 13%", b => Border(b, 0.13f, new SKColor(0, 0, 0))),
            ("white border 11%", b => Border(b, 0.11f, SKColors.White)),
            ("discord-grey border 9%", b => Border(b, 0.09f, new SKColor(54, 57, 63))),
            ("crop 3%", b => Crop(b, 0.03f)),
            ("crop 9%", b => Crop(b, 0.09f)),
            ("crop 17%", b => Crop(b, 0.17f))
        ];

        var caught = transforms.ToDictionary(t => t.Name, _ => 0);
        var worst = transforms.ToDictionary(t => t.Name, _ => 0);
        var total = 0;

        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
            var blocked = service.ComputeHashSet(bytes);
            if (blocked is null || blocked.Quality < ImageHashingService.MinReliableQuality)
                continue;

            total++;

            foreach (var (name, apply) in transforms)
            {
                var posted = service.ComputeMatchHashes(apply(bytes), true);
                if (posted is null)
                    continue;

                var distance = Best(blocked, posted);
                if (distance <= Tolerance)
                    caught[name]++;
                else if (distance > worst[name])
                    worst[name] = distance;
            }
        }

        TestContext.Out.WriteLine($"blocked images tested: {total}\n");
        TestContext.Out.WriteLine($"{"transform",-24} {"caught",-12} {"worst miss",-10}");
        TestContext.Out.WriteLine(new string('-', 50));

        foreach (var (name, _) in transforms)
        {
            var pct = 100.0 * caught[name] / total;
            var miss = worst[name] == 0 ? "-" : worst[name].ToString();
            TestContext.Out.WriteLine($"{name,-24} {caught[name],3}/{total,-3} ({pct,5:F1}%)  {miss,-10}");
        }
    }

    /// <summary>
    ///     End to end check of the shipped scam image list: every sample, re-uploaded the way Discord would mangle it, must
    ///     be caught by the hashes the bot actually ships rather than by hashes computed in the test.
    /// </summary>
    [Test]
    public void ShippedListCatchesTheRealScamImages()
    {
        var presets = new ScamImagePresetService(NullLogger<ScamImagePresetService>.Instance);
        Assert.That(presets.Images, Is.Not.Empty);

        var files = DatasetImages();
        var caught = 0;

        foreach (var file in files)
        {
            var posted = service.ComputeMatchHashes(Reencode(File.ReadAllBytes(file), 0.8f,
                SKEncodedImageFormat.Jpeg, 75), true);

            if (posted is null)
                continue;

            var parsed = posted.Hashes
                .Select(h => ImageHashingService.TryParseHash(h, out var v) ? v : null)
                .Where(h => h is not null)
                .ToList();

            var hit = presets.Images.Any(p => p.Hashes.Any(stored =>
                parsed.Any(candidate => ImageHashingService.Distance(stored, candidate!) <= Tolerance)));

            if (hit)
                caught++;
            else
                TestContext.Out.WriteLine($"MISSED: {Label(file)}");
        }

        TestContext.Out.WriteLine($"\nshipped list caught {caught}/{files.Count} re-uploaded scam images");
        Assert.That(caught, Is.EqualTo(files.Count));
    }

    private static byte[] Reencode(byte[] source, float scale, SKEncodedImageFormat format, int quality)
    {
        using var bitmap = SKBitmap.Decode(source);
        var width = Math.Max(1, (int)(bitmap.Width * scale));
        var height = Math.Max(1, (int)(bitmap.Height * scale));

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        using var image = SKImage.FromBitmap(bitmap);
        surface.Canvas.DrawImage(image, new SKRect(0, 0, bitmap.Width, bitmap.Height),
            new SKRect(0, 0, width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(format, quality);
        return data.ToArray();
    }

    private static byte[] Crop(byte[] source, float pct)
    {
        using var bitmap = SKBitmap.Decode(source);
        var width = (int)(bitmap.Width * (1 - pct));
        var height = (int)(bitmap.Height * (1 - pct));

        using var image = SKImage.FromBitmap(bitmap);
        using var cropped = image.Subset(new SKRectI((bitmap.Width - width) / 2, (bitmap.Height - height) / 2,
            (bitmap.Width - width) / 2 + width, (bitmap.Height - height) / 2 + height));
        using var data = cropped.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] Mirror(byte[] source)
    {
        using var bitmap = SKBitmap.Decode(source);
        using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height));
        surface.Canvas.Translate(bitmap.Width, 0);
        surface.Canvas.Scale(-1, 1);

        using var image = SKImage.FromBitmap(bitmap);
        surface.Canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));

        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] Border(byte[] source, float pct, SKColor color)
    {
        using var bitmap = SKBitmap.Decode(source);
        var pad = (int)(Math.Max(bitmap.Width, bitmap.Height) * pct);

        using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width + 2 * pad, bitmap.Height + 2 * pad));
        surface.Canvas.Clear(color);

        using var image = SKImage.FromBitmap(bitmap);
        surface.Canvas.DrawImage(image, pad, pad, new SKSamplingOptions(SKFilterMode.Linear));

        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] Brightness(byte[] source, float factor)
    {
        using var bitmap = SKBitmap.Decode(source);
        using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height));

        using var paint = new SKPaint();
        paint.ColorFilter = SKColorFilter.CreateColorMatrix([
            factor, 0, 0, 0, 0,
            0, factor, 0, 0, 0,
            0, 0, factor, 0, 0,
            0, 0, 0, 1, 0
        ]);

        using var image = SKImage.FromBitmap(bitmap);
        surface.Canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear), paint);

        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100);
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