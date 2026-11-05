using System.IO;
using System.Net.Http;
using DataModel;
using Mewdeko.Common.Cards;
using Mewdeko.Common.Palette;
using Mewdeko.Common.Replacements;
using Mewdeko.Modules.MultiGreets.Common;
using Mewdeko.Modules.MultiGreets.Services;
using Mewdeko.Modules.Verification.Services;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace Mewdeko.Tests;

/// <summary>
///     Verifies captcha answer checking and the welcome card design: placeholders, option validation, the conversion of
///     cards saved before designs existed, and how the classic layout draws.
/// </summary>
[TestFixture]
public class VerificationAndWelcomeCardTests
{
    [Test]
    public void GeneratedCodeIsSolvable()
    {
        for (var i = 0; i < 50; i++)
        {
            var code = VerificationCaptcha.NewCode();
            Assert.That(code, Has.Length.EqualTo(5));
            Assert.That(VerificationCaptcha.Matches(code, code), Is.True);
        }
    }

    [Test]
    public void GeneratedCodesAreNotAllTheSame() =>
        Assert.That(Enumerable.Range(0, 20).Select(_ => VerificationCaptcha.NewCode()).Distinct().Count(),
            Is.GreaterThan(1));

    [TestCase("AB3DE", "ab3de")]
    [TestCase("AB3DE", "  AB3DE  ")]
    [TestCase("AB3DE", "ab 3d e")]
    public void AnswersIgnoreCaseAndSpaces(string code, string answer) =>
        Assert.That(VerificationCaptcha.Matches(code, answer), Is.True);

    [TestCase("AB3DE", "AB3DF")]
    [TestCase("AB3DE", "")]
    [TestCase("AB3DE", null)]
    public void WrongAnswersAreRejected(string code, string? answer) =>
        Assert.That(VerificationCaptcha.Matches(code, answer), Is.False);

    [Test]
    public void CaptchaImageIsAPng()
    {
        var image = VerificationCaptcha.Render(VerificationCaptcha.NewCode());
        Assert.That(image.Take(4), Is.EqualTo(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
    }

    [Test]
    public void PlaceholdersAreReplacedInTitleAndSubtitle()
    {
        var replacer = new ReplacementBuilder()
            .WithOverride("%user.name%", () => "Mew")
            .WithOverride("%server.name%", () => "Home")
            .WithOverride("%server.members%", () => "42")
            .Build();
        var content = Content(replacer);
        var template = WelcomeCardTemplates.Classic();

        Assert.Multiple(() =>
        {
            Assert.That(content.TextOf(template.Elements.Single(e => e.Id == "title")), Is.EqualTo("Welcome, Mew"));
            Assert.That(content.TextOf(template.Elements.Single(e => e.Id == "subtitle")),
                Is.EqualTo("You are member #42 of Home"));
        });
    }

    [Test]
    public void BuiltInTextElementsShowTheirOwnValueUntilTyped()
    {
        var replacer = new ReplacementBuilder()
            .WithOverride("%user.name%", () => "Mew")
            .WithOverride("%server.name%", () => "Home")
            .WithOverride("%server.members%", () => "42")
            .Build();
        var content = Content(replacer);

        Assert.Multiple(() =>
        {
            Assert.That(content.TextOf(new CardElement { Type = CardElementType.Username }), Is.EqualTo("Mew"));
            Assert.That(content.TextOf(new CardElement { Type = CardElementType.ServerName }), Is.EqualTo("Home"));
            Assert.That(content.TextOf(new CardElement { Type = CardElementType.MemberCount }), Is.EqualTo("42"));
            Assert.That(content.TextOf(new CardElement { Type = CardElementType.Username, Text = "Hi %user.name%" }),
                Is.EqualTo("Hi Mew"));
            Assert.That(content.TextOf(new CardElement { Type = CardElementType.Avatar }), Is.Empty);
        });
    }

    [Test]
    public void DefaultBasicsAreValid() =>
        Assert.That(new WelcomeCardBasics().Validate(), Is.EqualTo(WelcomeCardError.None));

    [TestCase("#fff")]
    [TestCase("#23272A")]
    public void HexColoursAreAccepted(string color) =>
        Assert.That(new WelcomeCardBasics { BackgroundColor = color, TextColor = color }.Validate(),
            Is.EqualTo(WelcomeCardError.None));

    [TestCase("notacolor")]
    [TestCase("")]
    public void BadColoursAreRejected(string color)
    {
        Assert.That(new WelcomeCardBasics { BackgroundColor = color }.Validate(),
            Is.EqualTo(WelcomeCardError.InvalidColor));
        Assert.That(new WelcomeCardBasics { TextColor = color }.Validate(), Is.EqualTo(WelcomeCardError.InvalidColor));
    }

    [TestCase(-1)]
    [TestCase(101)]
    public void OpacityOutsideRangeIsRejected(int opacity) =>
        Assert.That(new WelcomeCardBasics { OverlayOpacity = opacity }.Validate(),
            Is.EqualTo(WelcomeCardError.InvalidOpacity));

    [TestCase(0)]
    [TestCase(100)]
    public void OpacityAtTheEdgesIsAccepted(int opacity) =>
        Assert.That(new WelcomeCardBasics { OverlayOpacity = opacity }.Validate(), Is.EqualTo(WelcomeCardError.None));

    [Test]
    public void UnknownFontIsRejected() =>
        Assert.That(new WelcomeCardBasics { FontName = "comic" }.Validate(), Is.EqualTo(WelcomeCardError.InvalidFont));

    [TestCase("noto-bold")]
    [TestCase("noto")]
    [TestCase("unisans")]
    public void BundledFontsAreAccepted(string font) =>
        Assert.That(new WelcomeCardBasics { FontName = font }.Validate(), Is.EqualTo(WelcomeCardError.None));

    [Test]
    public void EmptyOrLongTextIsRejected()
    {
        Assert.That(new WelcomeCardBasics { Title = " " }.Validate(), Is.EqualTo(WelcomeCardError.InvalidText));
        Assert.That(new WelcomeCardBasics { Title = new string('a', 101) }.Validate(),
            Is.EqualTo(WelcomeCardError.InvalidText));
        Assert.That(new WelcomeCardBasics { Subtitle = new string('a', 151) }.Validate(),
            Is.EqualTo(WelcomeCardError.InvalidText));
        Assert.That(new WelcomeCardBasics { Subtitle = "" }.Validate(), Is.EqualTo(WelcomeCardError.None));
    }

    [Test]
    public void CardsWithNoTemplateConvertTheirOldSettings()
    {
        var template = WelcomeCardTemplates.FromLegacy(new WelcomeCardSetting
        {
            BackgroundColor = "#112233",
            OverlayOpacity = 50,
            Title = "Hi %user.name%",
            Subtitle = "",
            FontName = "unisans",
            TextColor = "#ff8800",
            AvatarShape = WelcomeCardAvatarShape.Square
        }, null);
        var clean = WelcomeCardTemplates.Sanitize(template.Clone());

        Assert.Multiple(() =>
        {
            Assert.That(clean.Serialize(), Is.EqualTo(template.Serialize()));
            Assert.That((clean.Width, clean.Height, clean.Radius, clean.BorderWidth, clean.Shadow),
                Is.EqualTo((1000, 360, 0f, 0f, false)));
            Assert.That(clean.Background.Kind, Is.EqualTo("solid"));
            Assert.That(clean.Background.Color, Is.EqualTo("#112233"));
            Assert.That(clean.Elements.Single(e => e.Id == "overlay").Fill, Is.EqualTo("#0000007f"));
            Assert.That(clean.Elements.Single(e => e.Id == "avatar").Radius, Is.EqualTo(0));
            var title = clean.Elements.Single(e => e.Id == "title");
            Assert.That(title.Text, Is.EqualTo("Hi %user.name%"));
            Assert.That(title.Font, Is.EqualTo(CardRenderer.UniSans));
            Assert.That(title.Color, Is.EqualTo("#ff8800"));
            Assert.That(clean.Elements.Single(e => e.Id == "subtitle").Text, Is.Empty);
        });
    }

    [Test]
    public void ClassicLayoutMatchesTheOldDefaults()
    {
        var template = WelcomeCardTemplates.Classic();
        var avatar = template.Elements.Single(e => e.Id == "avatar");
        var title = template.Elements.Single(e => e.Id == "title");
        var subtitle = template.Elements.Single(e => e.Id == "subtitle");

        Assert.Multiple(() =>
        {
            Assert.That(template.Background.Color, Is.EqualTo("#23272a"));
            Assert.That(template.Elements.Single(e => e.Id == "overlay").Fill, Is.EqualTo("#00000066"));
            Assert.That((avatar.X, avatar.Y, avatar.W, avatar.H, avatar.Radius), Is.EqualTo((60f, 70f, 220f, 220f, 110f)));
            Assert.That((title.X, title.W, title.FontSize, title.Bold), Is.EqualTo((310f, 650f, 64f, true)));
            Assert.That((subtitle.X, subtitle.FontSize), Is.EqualTo((310f, 34f)));
            Assert.That(title.Text, Is.EqualTo("Welcome, %user.name%"));
            Assert.That(subtitle.Text, Is.EqualTo("You are member #%server.members% of %server.name%"));
        });
    }

    [Test]
    public void LegacyBackgroundPictureBecomesAnImageBackground()
    {
        var template = WelcomeCardTemplates.FromLegacy(new WelcomeCardSetting(), 7);
        Assert.Multiple(() =>
        {
            Assert.That(template.Background.Kind, Is.EqualTo("image"));
            Assert.That(template.Background.Url, Is.EqualTo("upload:7"));
            Assert.That(template.Background.Dim, Is.EqualTo(0));
            Assert.That(CardRules.Uploads(template), Is.EquivalentTo(new[] { 7 }));
        });
    }

    [Test]
    public void BasicEditsChangeTheDesign()
    {
        var template = WelcomeCardTemplates.Classic();
        var error = WelcomeCardTemplates.Apply(template, new WelcomeCardBasics
        {
            BackgroundColor = "#fff",
            TextColor = "#000000",
            OverlayOpacity = 100,
            Title = "Hello",
            Subtitle = "",
            FontName = "noto",
            AvatarShape = WelcomeCardAvatarShape.Rounded
        });
        var summary = WelcomeCardTemplates.Summarize(template);

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.EqualTo(WelcomeCardError.None));
            Assert.That(summary.Background, Is.EqualTo("#ffffff"));
            Assert.That(summary.Overlay, Is.EqualTo(100));
            Assert.That(summary.Title, Is.EqualTo("Hello"));
            Assert.That(summary.Subtitle, Is.Empty);
            Assert.That(summary.Font, Is.EqualTo("noto"));
            Assert.That(summary.TextColor, Is.EqualTo("#000000"));
            Assert.That(summary.Avatar, Is.EqualTo("Rounded"));
        });
    }

    [Test]
    public void BasicEditsReportMissingElements()
    {
        var template = new CardTemplate
        {
            Elements = [new CardElement { Id = "box", Type = CardElementType.Rectangle }]
        };
        Assert.Multiple(() =>
        {
            Assert.That(WelcomeCardTemplates.Apply(template, new WelcomeCardBasics { Title = "Hi" }),
                Is.EqualTo(WelcomeCardError.MissingElement));
            Assert.That(WelcomeCardTemplates.Apply(template, new WelcomeCardBasics { AvatarShape = WelcomeCardAvatarShape.Circle }),
                Is.EqualTo(WelcomeCardError.MissingElement));
            Assert.That(WelcomeCardTemplates.Apply(template, new WelcomeCardBasics { OverlayOpacity = 30 }),
                Is.EqualTo(WelcomeCardError.None));
            Assert.That(template.Elements[0].Id, Is.EqualTo("overlay"));
        });
    }

    [Test]
    public async Task ClassicCardDrawsAtItsOwnSizeOnTheOldBackground()
    {
        var renderer = new CardRenderer(new PlainHttpClientFactory(), NullLogger<CardRenderer>.Instance,
            CardPresetService.Create(new PlainHttpClientFactory(), "http://127.0.0.1:1/none", Path.Combine(Path.GetTempPath(), "card-preset-tests"), NullLogger<CardPresetService>.Instance));
        var template = WelcomeCardTemplates.Classic();
        var replacer = new ReplacementBuilder()
            .WithOverride("%user.name%", () => "Mew")
            .WithOverride("%server.name%", () => "Home")
            .WithOverride("%server.members%", () => "42")
            .Build();

        var result = await renderer.RenderAsync(Content(replacer), template, null,
            WelcomeCardService.MarginFor(template));
        using var bitmap = SKBitmap.Decode(result.Image.ToArray());

        Assert.Multiple(() =>
        {
            Assert.That((bitmap.Width, bitmap.Height), Is.EqualTo((1000, 360)));
            Assert.That(result.Margin, Is.Zero);
            var corner = bitmap.GetPixel(4, 4);
            Assert.That(corner.Red, Is.EqualTo(21).Within(2));
            Assert.That(corner.Green, Is.EqualTo(23).Within(2));
            Assert.That(corner.Blue, Is.EqualTo(25).Within(2));
            Assert.That(corner.Alpha, Is.EqualTo(255));
            Assert.That(result.Layout.Select(b => b.Id), Is.EqualTo(new[] { "overlay", "avatar", "title", "subtitle" }));
            Assert.That(result.Layout.All(b => b.Drawn));
        });
    }

    [Test]
    public async Task LongTextShrinksToFitBeforeItIsCutShort()
    {
        var renderer = new CardRenderer(new PlainHttpClientFactory(), NullLogger<CardRenderer>.Instance,
            CardPresetService.Create(new PlainHttpClientFactory(), "http://127.0.0.1:1/none", Path.Combine(Path.GetTempPath(), "card-preset-tests"), NullLogger<CardPresetService>.Instance));
        var replacer = new ReplacementBuilder()
            .WithOverride("%user.name%", () => "An extremely long member name that cannot fit")
            .Build();
        var shrinking = WelcomeCardTemplates.Classic();
        var fixedSize = WelcomeCardTemplates.Classic();
        fixedSize.Elements.Single(e => e.Id == "title").Shrink = false;

        var shrunk = await renderer.RenderAsync(Content(replacer), shrinking, null, 0);
        var cut = await renderer.RenderAsync(Content(replacer), fixedSize, null, 0);

        Assert.That(shrunk.Image.ToArray(), Is.Not.EqualTo(cut.Image.ToArray()));
    }

    private static WelcomeCardContent Content(Replacer replacer) =>
        new(text => replacer.Replace(text) ?? "", _ => Task.FromResult<SKBitmap?>(null),
            _ => Task.FromResult<SKBitmap?>(null))
        {
            Palette = DashboardPalette.Default
        };

    private sealed class PlainHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
