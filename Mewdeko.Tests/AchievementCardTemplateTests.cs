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
}
