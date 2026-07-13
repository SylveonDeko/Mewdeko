using System.Text.Json;
using Mewdeko.Modules.Administration.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mewdeko.Tests;

/// <summary>
///     Regenerates the shipped list of known scam image hashes from a folder of sample images. Explicit because it needs
///     the images locally and rewrites a file in the bot's data folder: run it by name when new samples are collected.
///     Only the hashes are shipped, never the images.
/// </summary>
[TestFixture]
[Explicit("Regenerates data/scam_image_hashes.json from a local image folder")]
public class ScamPresetGenerator
{
    private const string DatasetPath = "/Users/sylveondeko/Downloads/dataset (1)";

    private const string OutputPath =
        "/Users/sylveondeko/CombinedProjects/MewdekoCombined/mewdeko/src/Mewdeko/data/scam_image_hashes.json";

    /// <summary>
    ///     Images closer than this to one already kept are the same picture again, so only one of them is shipped.
    /// </summary>
    private const int DedupeDistance = 31;

    [Test]
    public void Generate()
    {
        var service = new ImageHashingService(new StubHttpClientFactory(), NullLogger<ImageHashingService>.Instance);

        var files = Directory.EnumerateFiles(DatasetPath, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToList();

        var kept = new List<(ImageHashSet Set, ulong[] Full)>();
        var skippedDuplicate = 0;
        var skippedQuality = 0;

        foreach (var file in files)
        {
            var set = service.ComputeHashSet(File.ReadAllBytes(file));

            if (set is null || set.Quality < ImageHashingService.MinReliableQuality)
            {
                skippedQuality++;
                continue;
            }

            if (!ImageHashingService.TryParseHash(set.Hash, out var full))
                continue;

            if (kept.Any(k => ImageHashingService.Distance(k.Full, full) <= DedupeDistance))
            {
                skippedDuplicate++;
                continue;
            }

            kept.Add((set, full));
        }

        var entries = kept.Select((k, i) => new
        {
            id = $"crypto-casino-{i + 1:D3}",
            name = "Crypto casino giveaway scam",
            hash = k.Set.Hash,
            variants = k.Set.Variants,
            quality = k.Set.Quality
        }).ToList();

        var payload = new
        {
            version = 1, campaign = "Fake MrBeast / crypto casino promo code giveaway", entries
        };

        File.WriteAllText(OutputPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        TestContext.Out.WriteLine($"scanned:    {files.Count}");
        TestContext.Out.WriteLine($"duplicates: {skippedDuplicate}");
        TestContext.Out.WriteLine($"unhashable: {skippedQuality}");
        TestContext.Out.WriteLine($"shipped:    {entries.Count}");
        TestContext.Out.WriteLine($"written to: {OutputPath}");
    }

    private class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }
}