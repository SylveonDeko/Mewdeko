using System.IO;
using System.Net;
using System.Net.Http;
using Mewdeko.Common.Cards;
using Mewdeko.Common.Palette;
using Mewdeko.Modules.MultiGreets.Common;
using Mewdeko.Modules.MultiGreets.Services;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace Mewdeko.Tests;

/// <summary>
///     Tests for the shared background presets: reading the catalog, reading <c>preset:id</c> references, and what a
///     card does when a preset can't be loaded.
/// </summary>
[TestFixture]
public class CardPresetTests
{
    private const string Catalog = """
        {
          "version": 1,
          "categories": [
            { "id": "space", "name": "Space", "order": 2 },
            { "id": "nature", "name": "Nature", "order": 1 },
            { "id": "Bad Id", "name": "Broken", "order": 0 },
            { "id": "nature", "name": "Duplicate", "order": 0 }
          ],
          "presets": [
            { "id": "forest", "category": "nature", "name": "Forest", "image": "nature/forest.jpg",
              "thumb": "nature/forest-thumb.jpg", "width": 1200, "height": 420, "credit": "Someone" },
            { "id": "nebula", "category": "space", "name": "", "image": "space/nebula.png",
              "thumb": "space/nebula-thumb.png", "width": 1600, "height": 560 },
            { "id": "lost", "category": "missing", "name": "Lost", "image": "a.png", "thumb": "b.png" },
            { "id": "escape", "category": "nature", "name": "Escape", "image": "../secret.png", "thumb": "b.png" },
            { "id": "remote", "category": "nature", "name": "Remote", "image": "https://evil.example/x.png",
              "thumb": "b.png" },
            { "id": "forest", "category": "nature", "name": "Again", "image": "x.png", "thumb": "y.png" },
            { "id": "no thumb", "category": "nature", "name": "No thumb", "image": "x.png", "thumb": "" }
          ]
        }
        """;

    /// <summary>
    ///     A catalog keeps its good entries in order and skips the unusable ones.
    /// </summary>
    [Test]
    public void CatalogKeepsGoodEntriesAndSkipsBadOnes()
    {
        var catalog = CardPresets.ParseCatalog(Catalog)!;
        Assert.Multiple(() =>
        {
            Assert.That(catalog.Categories.Select(c => c.Id), Is.EqualTo(new[] { "nature", "space" }));
            Assert.That(catalog.Presets.Select(p => p.Id), Is.EqualTo(new[] { "forest", "nebula" }));
            Assert.That(catalog.Find("forest")!.Credit, Is.EqualTo("Someone"));
            Assert.That(catalog.Find("forest")!.Width, Is.EqualTo(1200));
            Assert.That(catalog.Find("nebula")!.Name, Is.EqualTo("nebula"));
            Assert.That(catalog.Find("nebula")!.Credit, Is.Null);
            Assert.That(catalog.Find("lost"), Is.Null);
        });
    }

    /// <summary>
    ///     An empty catalog is valid, and text that isn't a catalog is not.
    /// </summary>
    [Test]
    public void EmptyCatalogIsValidAndGarbageIsNot()
    {
        var empty = CardPresets.ParseCatalog("""{ "version": 1, "categories": [], "presets": [] }""");
        Assert.Multiple(() =>
        {
            Assert.That(empty, Is.Not.Null);
            Assert.That(empty!.Presets, Is.Empty);
            Assert.That(CardPresets.ParseCatalog("{}")!.Presets, Is.Empty);
            Assert.That(CardPresets.ParseCatalog("{not json"), Is.Null);
            Assert.That(CardPresets.ParseCatalog("  "), Is.Null);
            Assert.That(CardPresets.ParseCatalog(null), Is.Null);
        });
    }

    /// <summary>
    ///     References are read case insensitively and only well formed ids are accepted.
    /// </summary>
    [Test]
    public void ReferencesParse()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CardPresets.TryParse("preset:forest", out var id), Is.True);
            Assert.That(id, Is.EqualTo("forest"));
            Assert.That(CardPresets.TryParse("  PRESET: Forest-2 ", out id), Is.True);
            Assert.That(id, Is.EqualTo("forest-2"));
            Assert.That(CardPresets.TryParse("preset:", out _), Is.False);
            Assert.That(CardPresets.TryParse("preset:../x", out _), Is.False);
            Assert.That(CardPresets.TryParse("preset:two words", out _), Is.False);
            Assert.That(CardPresets.TryParse("upload:4", out _), Is.False);
            Assert.That(CardPresets.TryParse("https://example.com/a.png", out _), Is.False);
            Assert.That(CardPresets.TryParse(null, out _), Is.False);
            Assert.That(CardPresets.Reference("forest"), Is.EqualTo("preset:forest"));
            Assert.That(CardPresets.Url("https://cdn.example/x/", "/a/b.png"), Is.EqualTo("https://cdn.example/x/a/b.png"));
        });
    }

    /// <summary>
    ///     Designs keep well formed preset links and drop broken ones.
    /// </summary>
    [Test]
    public void SanitizeKeepsPresetLinks()
    {
        var template = new CardTemplate
        {
            Background = new CardBackground { Kind = "image", Url = "preset:Forest" },
            Elements = [new CardElement { Id = "pic", Type = CardElementType.Image, Url = "preset:../nope" }]
        };
        var clean = WelcomeCardTemplates.Sanitize(template);
        Assert.Multiple(() =>
        {
            Assert.That(clean.Background.Url, Is.EqualTo("preset:forest"));
            Assert.That(clean.Elements.Single(e => e.Id == "pic").Url, Is.Empty);
        });
    }

    /// <summary>
    ///     A preset the catalog doesn't have, or one that can't be reached, draws the colour background and doesn't throw.
    /// </summary>
    [Test]
    public async Task UnknownPresetFallsBackToTheColourBackground()
    {
        var folder = TempFolder();
        try
        {
            var service = CardPresetService.Create(new StubHttp(Catalog, null), "https://cdn.test/card", folder,
                NullLogger<CardPresetService>.Instance);
            var unreachable = CardPresetService.Create(new StubHttp(null, null), "https://cdn.test/card", folder,
                NullLogger<CardPresetService>.Instance);

            Assert.Multiple(async () =>
            {
                Assert.That(await Pixel(service, "preset:nope"), Is.EqualTo(new SKColor(0x12, 0x34, 0x56)));
                Assert.That(await Pixel(service, "preset:forest"), Is.EqualTo(new SKColor(0x12, 0x34, 0x56)));
                Assert.That(await Pixel(unreachable, "preset:forest"), Is.EqualTo(new SKColor(0x12, 0x34, 0x56)));
                Assert.That(await service.LoadBytesAsync("preset:nope"), Is.Null);
                Assert.That((await unreachable.GetCatalogAsync()).Presets, Is.Empty);
            });
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    /// <summary>
    ///     A preset that is in the catalog draws its image, and the catalog and image are fetched once.
    /// </summary>
    [Test]
    public async Task KnownPresetDrawsItsImage()
    {
        var folder = TempFolder();
        try
        {
            var http = new StubHttp(Catalog, Png(new SKColor(0xC8, 0x20, 0x20)));
            var service = CardPresetService.Create(http, "https://cdn.test/card/", folder,
                NullLogger<CardPresetService>.Instance);

            var first = await Pixel(service, "preset:forest");
            var second = await Pixel(service, "preset:forest");

            Assert.Multiple(() =>
            {
                Assert.That(first.Red, Is.EqualTo(0xC8).Within(2));
                Assert.That(second.Red, Is.EqualTo(0xC8).Within(2));
                Assert.That(http.Requests.Count(r => r.EndsWith("catalog.json")), Is.EqualTo(1));
                Assert.That(http.Requests.Count(r => r.EndsWith("nature/forest.jpg")), Is.EqualTo(1));
                Assert.That(http.Requests, Does.Contain("https://cdn.test/card/catalog.json"));
            });
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    /// <summary>
    ///     Something that isn't an image is refused rather than cached.
    /// </summary>
    [Test]
    public async Task NonImageDownloadsAreRefused()
    {
        var folder = TempFolder();
        try
        {
            var service = CardPresetService.Create(new StubHttp(Catalog, "<html>not an image</html>"u8.ToArray()),
                "https://cdn.test/card", folder, NullLogger<CardPresetService>.Instance);
            Assert.Multiple(async () =>
            {
                Assert.That(await service.LoadBytesAsync("preset:forest"), Is.Null);
                Assert.That(CardPresetService.IsImage(Png(SKColors.Red)), Is.True);
                Assert.That(CardPresetService.IsImage("nope"u8.ToArray()), Is.False);
            });
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    private static string TempFolder()
    {
        return Path.Combine(Path.GetTempPath(), "card-presets-" + Guid.NewGuid().ToString("N"));
    }

    private static byte[] Png(SKColor color)
    {
        using var bitmap = new SKBitmap(40, 20);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static async Task<SKColor> Pixel(CardPresetService service, string link)
    {
        var renderer = new CardRenderer(new StubHttp(null, null), NullLogger<CardRenderer>.Instance, service);
        var template = new CardTemplate
        {
            Width = 200,
            Height = 100,
            Shadow = false,
            Radius = 0,
            Background = new CardBackground { Kind = "image", Url = link, Color = "#123456", Dim = 0 },
            Elements = []
        };
        var content = new WelcomeCardContent(text => text, _ => Task.FromResult<SKBitmap?>(null),
            _ => Task.FromResult<SKBitmap?>(null))
        {
            Palette = DashboardPalette.Default
        };
        var result = await renderer.RenderAsync(content, template, null, 0);
        using var bitmap = SKBitmap.Decode(result.Image.ToArray());
        return bitmap.GetPixel(100, 50);
    }

    private sealed class StubHttp(string? catalog, byte[]? image) : IHttpClientFactory
    {
        public List<string> Requests { get; } = [];

        public string? Catalog => catalog;

        public byte[]? Image => image;

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new Handler(this));
        }

        private sealed class Handler(StubHttp owner) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var url = request.RequestUri!.ToString();
                owner.Requests.Add(url);
                if (url.EndsWith("catalog.json") && owner.Catalog is not null)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(owner.Catalog)
                    });
                if (!url.EndsWith("catalog.json") && owner.Image is not null)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(owner.Image)
                    });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }
    }
}
