using Discord;
using Mewdeko.Modules.RoleMenus.Common;
using Mewdeko.Modules.RoleMenus.Services;

namespace Mewdeko.Tests;

/// <summary>
///     Tests for the premade role menus and the text their roles are edited as.
/// </summary>
[TestFixture]
public class RoleMenuTemplateTests
{
    [Test]
    public void EveryTemplateKindHasATemplate()
    {
        foreach (var kind in Enum.GetValues<RoleMenuTemplateKind>())
            Assert.That(RoleMenuTemplates.All.Count(t => t.Kind == kind), Is.EqualTo(1), kind.ToString());
    }

    [Test]
    public void EveryTemplateFitsAReactionMenu()
    {
        foreach (var template in RoleMenuTemplates.All)
        {
            Assert.That(template.Entries, Is.Not.Empty, template.Name);
            Assert.That(template.Entries.Count, Is.LessThanOrEqualTo(RoleMenuService.MaxReactionOptions),
                template.Name);
            Assert.That(template.Entries.Select(e => e.Emoji).Distinct().Count(), Is.EqualTo(template.Entries.Count),
                template.Name);
            Assert.That(template.Entries.All(e => Emoji.TryParse(e.Emoji, out _)), Is.True, template.Name);
            Assert.That(template.Entries.All(e => e.Label.Length is > 0 and <= RoleMenuService.LabelLength),
                Is.True, template.Name);
        }
    }

    [Test]
    public void EntriesSurviveAnEditRoundTrip()
    {
        foreach (var template in RoleMenuTemplates.All)
        {
            var text = RoleMenuService.EntriesToText(template.Entries);

            Assert.That(RoleMenuService.TryParseEntries(text, out var parsed), Is.True, template.Name);
            Assert.That(parsed, Is.EqualTo(template.Entries), template.Name);
        }
    }

    [Test]
    public void LinesWithoutAnEmojiOrColourAreKept()
    {
        Assert.That(RoleMenuService.TryParseEntries("Gamer\n🎮 Streamer #112233", out var entries), Is.True);
        Assert.That(entries, Is.EqualTo(new[]
        {
            new RoleMenuTemplateEntry("Gamer", ""),
            new RoleMenuTemplateEntry("Streamer", "🎮", 0x112233)
        }));
    }

    [Test]
    public void EmptyTextIsRefused()
    {
        Assert.That(RoleMenuService.TryParseEntries("  \n ", out _), Is.False);
        Assert.That(RoleMenuService.TryParseEntries("🎮", out _), Is.False);
    }
}
