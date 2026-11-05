using DataModel;
using Mewdeko.Modules.Moderation.Services;

namespace Mewdeko.Tests;

/// <summary>
///     Covers reason alias expansion and the placeholders of custom punishment DMs.
/// </summary>
[TestFixture]
public class ModCasePunishTests
{
    private static readonly ModReasonAlias[] Aliases =
    [
        new()
        {
            Name = "spam", Reason = "Spamming in channels"
        },
        new()
        {
            Name = "raid", Reason = "Taking part in a raid"
        }
    ];

    [Test]
    public void ExpandReason_ReplacesAnExactAliasName()
    {
        Assert.That(ModCaseService.ExpandReason("spam", Aliases), Is.EqualTo("Spamming in channels"));
    }

    [Test]
    public void ExpandReason_IgnoresCaseAndSurroundingSpaces()
    {
        Assert.That(ModCaseService.ExpandReason("  RAID ", Aliases), Is.EqualTo("Taking part in a raid"));
    }

    [Test]
    public void ExpandReason_LeavesLongerReasonsAlone()
    {
        Assert.That(ModCaseService.ExpandReason("spam in voice", Aliases), Is.EqualTo("spam in voice"));
    }

    [Test]
    public void ExpandReason_LeavesEmptyReasonsAlone()
    {
        Assert.That(ModCaseService.ExpandReason(null, Aliases), Is.Null);
        Assert.That(ModCaseService.ExpandReason("  ", Aliases), Is.EqualTo("  "));
    }

    [Test]
    public void RenderPunishDm_FillsEveryPlaceholder()
    {
        var text = ModCaseService.RenderPunishDm("%server%|%user%|%reason%|%mod%|%duration%|%case%", "Guild",
            "Alex", "Spamming", "Sam", TimeSpan.FromHours(2), 14);

        Assert.That(text, Is.EqualTo("Guild|Alex|Spamming|Sam|2 hours|14"));
    }

    [Test]
    public void RenderPunishDm_UsesDashesWhenThereIsNoDurationOrCase()
    {
        var text = ModCaseService.RenderPunishDm("%duration% %case%", "Guild", "Alex", "r", "Sam", null, null);

        Assert.That(text, Is.EqualTo("- -"));
    }

    [Test]
    public void RenderPunishDm_IsCaseInsensitiveAndFillsRepeats()
    {
        var text = ModCaseService.RenderPunishDm("%CASE% and %case%", "Guild", "Alex", "r", "Sam", null, 3);

        Assert.That(text, Is.EqualTo("3 and 3"));
    }

    [Test]
    public void RenderPunishDm_FillsTheBanMessagePlaceholders()
    {
        var text = ModCaseService.RenderPunishDm("%ban.user% by %ban.mod%: %ban.reason% (%ban.duration%)", "Guild",
            "Alex", "Spamming", "Sam", null, 1);

        Assert.That(text, Is.EqualTo("Alex by Sam: Spamming (-)"));
    }
}
