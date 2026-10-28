using Mewdeko.Common.Palette;
using Mewdeko.Modules.Achievements.Common;
using NUnit.Framework;
using SkiaSharp;

namespace Mewdeko.Tests;

/// <summary>
///     Tests for achievement card templates: storage round trips, limits, and color tokens.
/// </summary>
[TestFixture]
public class AchievementCardTemplateTests
{
    /// <summary>
    ///     The default template survives being stored and read back unchanged.
    /// </summary>
    [Test]
    public void DefaultRoundTrips()
    {
        var json = AchievementCardTemplate.Default().Serialize();
        Assert.That(AchievementCardTemplate.Parse(json).Serialize(), Is.EqualTo(json));
    }

    /// <summary>
    ///     Empty or unreadable storage falls back to the default.
    /// </summary>
    [Test]
    public void BadStorageFallsBackToDefault()
    {
        var expected = AchievementCardTemplate.Default().Serialize();
        Assert.Multiple(() =>
        {
            Assert.That(AchievementCardTemplate.Parse(null).Serialize(), Is.EqualTo(expected));
            Assert.That(AchievementCardTemplate.Parse("{not json").Serialize(), Is.EqualTo(expected));
        });
    }

    /// <summary>
    ///     Removed built in elements come back hidden, duplicates are dropped, and every built in keeps its type as id.
    /// </summary>
    [Test]
    public void BuiltInsStayUnique()
    {
        var template = AchievementCardTemplate.Default();
        template.Elements.RemoveAll(e => e.Type == AchievementCardElementType.Points);
        template.Elements.Add(new AchievementCardElement
        {
            Id = "second-title", Type = AchievementCardElementType.Title
        });

        var clean = AchievementCardRules.Sanitize(template);
        Assert.Multiple(() =>
        {
            Assert.That(clean.Elements.Count(e => e.Type == AchievementCardElementType.Title), Is.EqualTo(1));
            Assert.That(clean.Elements.Single(e => e.Type == AchievementCardElementType.Points).Visible, Is.False);
            Assert.That(clean.Elements.Where(e => AchievementCardElementType.IsBuiltIn(e.Type)).All(e => e.Id == e.Type),
                Is.True);
        });
    }

    /// <summary>
    ///     Bad values are reset: unknown types dropped, colors and links checked, numbers clamped, ids made unique.
    /// </summary>
    [Test]
    public void BadValuesAreCorrected()
    {
        var template = AchievementCardTemplate.Default();
        template.Width = 99999;
        template.Elements.Add(new AchievementCardElement { Id = "x", Type = "spaceship" });
        template.Elements.Add(new AchievementCardElement
        {
            Id = "box", Type = AchievementCardElementType.Rectangle, Fill = "red", Opacity = 5, Url = "http://insecure"
        });
        template.Elements.Add(new AchievementCardElement { Id = "box", Type = AchievementCardElementType.Ellipse });
        template.Elements.Add(new AchievementCardElement
        {
            Id = "pic", Type = AchievementCardElementType.Image, Url = "upload:12", FollowId = "nowhere"
        });

        var clean = AchievementCardRules.Sanitize(template);
        var box = clean.Elements.Single(e => e.Id == "box");
        Assert.Multiple(() =>
        {
            Assert.That(clean.Width, Is.EqualTo(AchievementCardTemplate.MaxWidth));
            Assert.That(clean.Elements.Any(e => e.Type == "spaceship"), Is.False);
            Assert.That(box.Fill, Is.Empty);
            Assert.That(box.Opacity, Is.EqualTo(1));
            Assert.That(box.Url, Is.Empty);
            Assert.That(clean.Elements.Single(e => e.Type == AchievementCardElementType.Ellipse).Id,
                Is.Not.EqualTo("box"));
            Assert.That(clean.Elements.Single(e => e.Id == "pic").Url, Is.EqualTo("upload:12"));
            Assert.That(clean.Elements.Single(e => e.Id == "pic").FollowId, Is.Empty);
            Assert.That(AchievementCardRules.Uploads(clean), Is.EquivalentTo(new[] { 12 }));
        });
    }

    /// <summary>
    ///     Color tokens resolve against the palette and grade, with dashboard style hex alpha.
    /// </summary>
    [Test]
    public void ColorTokensResolve()
    {
        var palette = DashboardPalette.Default;
        var grade = new SKColor(0x10, 0xB9, 0x81);
        Assert.Multiple(() =>
        {
            Assert.That(AchievementCardRules.Resolve("primary", palette, grade), Is.EqualTo(palette.PrimaryColor));
            Assert.That(AchievementCardRules.Resolve("primary@30", palette, grade),
                Is.EqualTo(palette.PrimaryColor.WithAlpha(0x30)));
            Assert.That(AchievementCardRules.Resolve("grade@20", palette, grade), Is.EqualTo(grade.WithAlpha(0x20)));
            Assert.That(AchievementCardRules.Resolve("#ff000080", palette, grade),
                Is.EqualTo(new SKColor(0xFF, 0, 0, 0x80)));
            Assert.That(AchievementCardRules.Resolve("", palette, grade), Is.Null);
            Assert.That(AchievementCardRules.Resolve("blue", palette, grade), Is.Null);
        });
    }

    /// <summary>
    ///     Card text splits into words, spaces, breaks, custom emoji, and Unicode emoji with Twemoji file names.
    /// </summary>
    [Test]
    public void CardTextSplitsIntoPieces()
    {
        var pieces = AchievementCardText.Parse("Hi <a:party:123456789012345678> 🎉\nGG 👋🏽 ❤️ 👨‍👩‍👧 {icon:no-such-glyph}");
        var kinds = pieces.Select(p => p.Kind).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(kinds, Is.EqualTo(new[]
            {
                AchievementTextPieceKind.Text, AchievementTextPieceKind.Space, AchievementTextPieceKind.Emoji,
                AchievementTextPieceKind.Space, AchievementTextPieceKind.Emoji, AchievementTextPieceKind.Break,
                AchievementTextPieceKind.Text, AchievementTextPieceKind.Space, AchievementTextPieceKind.Emoji,
                AchievementTextPieceKind.Space, AchievementTextPieceKind.Emoji, AchievementTextPieceKind.Space,
                AchievementTextPieceKind.Emoji, AchievementTextPieceKind.Space
            }));
            Assert.That(pieces[2].Url, Is.EqualTo("https://cdn.discordapp.com/emojis/123456789012345678.png?size=96"));
            Assert.That(pieces[2].Text, Is.EqualTo(":party:"));
            Assert.That(pieces[4].Url, Does.EndWith("/1f389.png"));
            Assert.That(pieces[8].Url, Does.EndWith("/1f44b-1f3fd.png"));
            Assert.That(pieces[10].Url, Does.EndWith("/2764.png"));
            Assert.That(pieces[12].Url, Does.EndWith("/1f468-200d-1f469-200d-1f467.png"));
            Assert.That(AchievementCardText.Parse("Level 5 © ✓").All(p => p.Kind != AchievementTextPieceKind.Emoji));
        });
    }

    /// <summary>
    ///     Card text saved with brace placeholders reads back with the percent placeholders the rest of the bot uses.
    /// </summary>
    [Test]
    public void BracePlaceholdersBecomePercent()
    {
        var template = AchievementCardTemplate.Default();
        var label = template.Elements.First(e => e.Type == AchievementCardElementType.Label);
        label.Text = "{achievement.name} by {user.name} {icon:star} {nope}";
        label.LockedText = "{progress.percent} left";
        var read = AchievementCardTemplate.Parse(template.Serialize());
        var after = read.Elements.First(e => e.Type == AchievementCardElementType.Label);
        Assert.Multiple(() =>
        {
            Assert.That(after.Text, Is.EqualTo("%achievement.name% by %user.name% {icon:star} {nope}"));
            Assert.That(after.LockedText, Is.EqualTo("%progress.percent% left"));
        });
    }
}
