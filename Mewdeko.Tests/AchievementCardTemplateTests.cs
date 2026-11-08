using DataModel;
using Mewdeko.Common.Cards;
using Mewdeko.Common.Palette;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Modules.MultiGreets.Common;
using NUnit.Framework;
using SkiaSharp;

namespace Mewdeko.Tests;

/// <summary>
///     Tests for the shared card engine through achievement cards and welcome cards: storage round trips, limits, and
///     color tokens.
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
        var json = AchievementCards.Default().Serialize();
        Assert.That(AchievementCards.Parse(json).Serialize(), Is.EqualTo(json));
    }

    /// <summary>
    ///     Empty or unreadable storage falls back to the default.
    /// </summary>
    [Test]
    public void BadStorageFallsBackToDefault()
    {
        var expected = AchievementCards.Default().Serialize();
        Assert.Multiple(() =>
        {
            Assert.That(AchievementCards.Parse(null).Serialize(), Is.EqualTo(expected));
            Assert.That(AchievementCards.Parse("{not json").Serialize(), Is.EqualTo(expected));
        });
    }

    /// <summary>
    ///     Removed built in elements come back hidden, duplicates are dropped, and every built in keeps its type as id.
    /// </summary>
    [Test]
    public void BuiltInsStayUnique()
    {
        var template = AchievementCards.Default();
        template.Elements.RemoveAll(e => e.Type == CardElementType.Points);
        template.Elements.Add(new CardElement
        {
            Id = "second-title", Type = CardElementType.Title
        });

        var clean = AchievementCards.Sanitize(template);
        Assert.Multiple(() =>
        {
            Assert.That(clean.Elements.Count(e => e.Type == CardElementType.Title), Is.EqualTo(1));
            Assert.That(clean.Elements.Single(e => e.Type == CardElementType.Points).Visible, Is.False);
            Assert.That(clean.Elements.Where(e => AchievementCards.Profile.IsBuiltIn(e.Type)).All(e => e.Id == e.Type),
                Is.True);
        });
    }

    /// <summary>
    ///     Bad values are reset: unknown types dropped, colors and links checked, numbers clamped, ids made unique.
    /// </summary>
    [Test]
    public void BadValuesAreCorrected()
    {
        var template = AchievementCards.Default();
        template.Width = 99999;
        template.Elements.Add(new CardElement { Id = "x", Type = "spaceship" });
        template.Elements.Add(new CardElement
        {
            Id = "box", Type = CardElementType.Rectangle, Fill = "red", Opacity = 5, Url = "http://insecure"
        });
        template.Elements.Add(new CardElement { Id = "box", Type = CardElementType.Ellipse });
        template.Elements.Add(new CardElement
        {
            Id = "pic", Type = CardElementType.Image, Url = "upload:12", FollowId = "nowhere"
        });

        var clean = AchievementCards.Sanitize(template);
        var box = clean.Elements.Single(e => e.Id == "box");
        Assert.Multiple(() =>
        {
            Assert.That(clean.Width, Is.EqualTo(CardTemplate.MaxWidth));
            Assert.That(clean.Elements.Any(e => e.Type == "spaceship"), Is.False);
            Assert.That(box.Fill, Is.Empty);
            Assert.That(box.Opacity, Is.EqualTo(1));
            Assert.That(box.Url, Is.Empty);
            Assert.That(clean.Elements.Single(e => e.Type == CardElementType.Ellipse).Id,
                Is.Not.EqualTo("box"));
            Assert.That(clean.Elements.Single(e => e.Id == "pic").Url, Is.EqualTo("upload:12"));
            Assert.That(clean.Elements.Single(e => e.Id == "pic").FollowId, Is.Empty);
            Assert.That(CardRules.Uploads(clean), Is.EquivalentTo(new[] { 12 }));
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
            Assert.That(CardRules.Resolve("primary", palette, grade), Is.EqualTo(palette.PrimaryColor));
            Assert.That(CardRules.Resolve("primary@30", palette, grade),
                Is.EqualTo(palette.PrimaryColor.WithAlpha(0x30)));
            Assert.That(CardRules.Resolve("grade@20", palette, grade), Is.EqualTo(grade.WithAlpha(0x20)));
            Assert.That(CardRules.Resolve("#ff000080", palette, grade),
                Is.EqualTo(new SKColor(0xFF, 0, 0, 0x80)));
            Assert.That(CardRules.Resolve("", palette, grade), Is.Null);
            Assert.That(CardRules.Resolve("blue", palette, grade), Is.Null);
        });
    }

    /// <summary>
    ///     Card text splits into words, spaces, breaks, custom emoji, and Unicode emoji with Twemoji file names.
    /// </summary>
    [Test]
    public void CardTextSplitsIntoPieces()
    {
        var pieces = CardText.Parse("Hi <a:party:123456789012345678> 🎉\nGG 👋🏽 ❤️ 👨‍👩‍👧 {icon:no-such-glyph}");
        var kinds = pieces.Select(p => p.Kind).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(kinds, Is.EqualTo(new[]
            {
                CardTextPieceKind.Text, CardTextPieceKind.Space, CardTextPieceKind.Emoji,
                CardTextPieceKind.Space, CardTextPieceKind.Emoji, CardTextPieceKind.Break,
                CardTextPieceKind.Text, CardTextPieceKind.Space, CardTextPieceKind.Emoji,
                CardTextPieceKind.Space, CardTextPieceKind.Emoji, CardTextPieceKind.Space,
                CardTextPieceKind.Emoji, CardTextPieceKind.Space
            }));
            Assert.That(pieces[2].Url, Is.EqualTo("https://cdn.discordapp.com/emojis/123456789012345678.png?size=96"));
            Assert.That(pieces[2].Text, Is.EqualTo(":party:"));
            Assert.That(pieces[4].Url, Does.EndWith("/1f389.png"));
            Assert.That(pieces[8].Url, Does.EndWith("/1f44b-1f3fd.png"));
            Assert.That(pieces[10].Url, Does.EndWith("/2764.png"));
            Assert.That(pieces[12].Url, Does.EndWith("/1f468-200d-1f469-200d-1f467.png"));
            Assert.That(CardText.Parse("Level 5 © ✓").All(p => p.Kind != CardTextPieceKind.Emoji));
        });
    }

    /// <summary>
    ///     Card text saved with brace placeholders reads back with the percent placeholders the rest of the bot uses.
    /// </summary>
    [Test]
    public void BracePlaceholdersBecomePercent()
    {
        var template = AchievementCards.Default();
        var label = template.Elements.First(e => e.Type == CardElementType.Label);
        label.Text = "{achievement.name} by {user.name} {icon:star} {nope}";
        label.LockedText = "{progress.percent} left";
        var read = AchievementCards.Parse(template.Serialize());
        var after = read.Elements.First(e => e.Type == CardElementType.Label);
        Assert.Multiple(() =>
        {
            Assert.That(after.Text, Is.EqualTo("%achievement.name% by %user.name% {icon:star} {nope}"));
            Assert.That(after.LockedText, Is.EqualTo("%progress.percent% left"));
        });
    }

    /// <summary>
    ///     Each feature's profile decides which kinds a design holds: welcome cards drop the achievement kinds and
    ///     never force built in elements, and achievement cards drop the welcome kinds.
    /// </summary>
    [Test]
    public void ProfilesDecideWhichKindsAreKept()
    {
        var welcome = new CardTemplate
        {
            Elements =
            [
                new CardElement { Id = "a", Type = CardElementType.Username },
                new CardElement { Id = "b", Type = CardElementType.Title },
                new CardElement { Id = "c", Type = CardElementType.ServerIcon }
            ]
        };
        var achievement = new CardTemplate
        {
            Elements =
            [
                new CardElement { Id = "a", Type = CardElementType.Username },
                new CardElement { Id = "b", Type = CardElementType.ServerIcon }
            ]
        };

        var cleanWelcome = WelcomeCardTemplates.Sanitize(welcome);
        var cleanAchievement = AchievementCards.Sanitize(achievement);
        Assert.Multiple(() =>
        {
            Assert.That(cleanWelcome.Elements.Select(e => e.Type),
                Is.EqualTo(new[] { CardElementType.Username, CardElementType.ServerIcon }));
            Assert.That(cleanAchievement.Elements.Any(e => e.Type is CardElementType.Username or CardElementType.ServerIcon),
                Is.False);
            Assert.That(cleanAchievement.Elements.Count(e => AchievementCards.Profile.IsBuiltIn(e.Type)),
                Is.EqualTo(AchievementCards.BuiltIn.Count));
        });
    }

    /// <summary>
    ///     State filters only survive on features that have states, and the font and brace placeholders follow the
    ///     feature's own list.
    /// </summary>
    [Test]
    public void ShowFontAndPlaceholdersFollowTheProfile()
    {
        var template = new CardTemplate
        {
            Elements =
            [
                new CardElement
                {
                    Id = "t", Type = CardElementType.Text, Show = CardShow.Locked, Font = "comic",
                    Text = "{server.members} {achievement.name}"
                },
                new CardElement { Id = "u", Type = CardElementType.Text, Font = CardRenderer.UniSans }
            ]
        };

        var clean = WelcomeCardTemplates.Sanitize(template);
        var text = clean.Elements.Single(e => e.Id == "t");
        Assert.Multiple(() =>
        {
            Assert.That(text.Show, Is.EqualTo(CardShow.Always));
            Assert.That(text.Font, Is.Empty);
            Assert.That(text.Text, Is.EqualTo("%server.members% {achievement.name}"));
            Assert.That(clean.Elements.Single(e => e.Id == "u").Font, Is.EqualTo(CardRenderer.UniSans));
        });
    }

    /// <summary>
    ///     Every starter layout is already within limits, so saving it changes nothing, and it holds only kinds welcome
    ///     cards allow.
    /// </summary>
    [Test]
    public void StartersAreValid()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WelcomeCardTemplates.Starters.Select(s => s.Id), Is.Unique);
            Assert.That(WelcomeCardTemplates.Starters.Any(s => s.Id == WelcomeCardTemplates.ClassicId));
            foreach (var starter in WelcomeCardTemplates.Starters)
            foreach (var target in Enum.GetValues<WelcomeCardTarget>())
            {
                var built = starter.Build(target);
                Assert.That(WelcomeCardTemplates.Sanitize(built.Clone()).Serialize(), Is.EqualTo(built.Serialize()),
                    $"{starter.Id} {target}");
            }
        });
    }

    /// <summary>
    ///     Cards for other messages start with their own text, and saved cards left at the greet text pick it up.
    /// </summary>
    [Test]
    public void CardsAreWordedPerMessage()
    {
        var boost = WelcomeCardTemplates.Classic(WelcomeCardTarget.Boost);
        var reworded = WelcomeCardTemplates.Reword(WelcomeCardTemplates.Classic(), WelcomeCardTarget.Bye);
        Assert.Multiple(() =>
        {
            Assert.That(boost.Elements.Single(e => e.Id == "title").Text,
                Is.EqualTo(WelcomeCardTemplates.Wording(WelcomeCardTarget.Boost).Title));
            Assert.That(reworded.Elements.Single(e => e.Id == "title").Text, Is.EqualTo("Goodbye, %user.name%"));
            Assert.That(WelcomeCardTemplates.Classic().Elements.Single(e => e.Id == "title").Text,
                Is.EqualTo("Welcome, %user.name%"));
        });
    }
}
