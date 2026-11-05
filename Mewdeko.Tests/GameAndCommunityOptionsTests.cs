using DataModel;
using Mewdeko.Modules.Birthday.Common;
using Mewdeko.Modules.Birthday.Services;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Giveaways.Common;
using Mewdeko.Modules.UserProfile.Common;

namespace Mewdeko.Tests;

/// <summary>
///     Tests for economy boost windows, giveaway extra entries and birth year rules.
/// </summary>
[TestFixture]
public class GameAndCommunityOptionsTests
{
    private static EconomyBoost Boost(EconomyBoostSchedule schedule, DateTime startsAt, int minutes, string commands = "")
    {
        return new EconomyBoost
        {
            ScheduleType = (int)schedule,
            StartsAt = startsAt,
            DurationMinutes = minutes,
            Commands = commands,
            PercentChange = 50,
            Enabled = true
        };
    }

    /// <summary>
    ///     A one-off boost is active only inside its window.
    /// </summary>
    [Test]
    public void OneOffBoostIsActiveInsideItsWindow()
    {
        var boost = Boost(EconomyBoostSchedule.OneOff, new DateTime(2026, 1, 1, 10, 0, 0), 60);

        Assert.That(EconomyBoostService.IsActive(boost, new DateTime(2026, 1, 1, 9, 59, 0)), Is.False);
        Assert.That(EconomyBoostService.IsActive(boost, new DateTime(2026, 1, 1, 10, 30, 0)), Is.True);
        Assert.That(EconomyBoostService.IsActive(boost, new DateTime(2026, 1, 1, 11, 0, 0)), Is.False);
    }

    /// <summary>
    ///     A daily boost repeats at the same time every day but never before its first start.
    /// </summary>
    [Test]
    public void DailyBoostRepeatsEveryDay()
    {
        var boost = Boost(EconomyBoostSchedule.Daily, new DateTime(2026, 1, 1, 18, 0, 0), 120);

        Assert.That(EconomyBoostService.IsActive(boost, new DateTime(2025, 12, 31, 19, 0, 0)), Is.False);
        Assert.That(EconomyBoostService.IsActive(boost, new DateTime(2026, 3, 9, 19, 30, 0)), Is.True);
        Assert.That(EconomyBoostService.IsActive(boost, new DateTime(2026, 3, 9, 21, 0, 0)), Is.False);
    }

    /// <summary>
    ///     A window that crosses midnight stays open into the next day.
    /// </summary>
    [Test]
    public void DailyBoostCrossesMidnight()
    {
        var boost = Boost(EconomyBoostSchedule.Daily, new DateTime(2026, 1, 1, 23, 0, 0), 120);

        Assert.That(EconomyBoostService.IsActive(boost, new DateTime(2026, 2, 5, 0, 30, 0)), Is.True);
        Assert.That(EconomyBoostService.IsActive(boost, new DateTime(2026, 2, 5, 1, 30, 0)), Is.False);
    }

    /// <summary>
    ///     A weekly boost opens on the same weekday.
    /// </summary>
    [Test]
    public void WeeklyBoostOpensOnItsWeekday()
    {
        var start = new DateTime(2026, 1, 2, 12, 0, 0);
        var boost = Boost(EconomyBoostSchedule.Weekly, start, 24 * 60);

        Assert.That(EconomyBoostService.IsActive(boost, start.AddDays(14).AddHours(5)), Is.True);
        Assert.That(EconomyBoostService.IsActive(boost, start.AddDays(16)), Is.False);
    }

    /// <summary>
    ///     A monthly boost set on the 31st opens on the last day of shorter months.
    /// </summary>
    [Test]
    public void MonthlyBoostClampsToShortMonths()
    {
        var boost = Boost(EconomyBoostSchedule.Monthly, new DateTime(2026, 1, 31, 8, 0, 0), 60);

        Assert.That(EconomyBoostService.IsActive(boost, new DateTime(2026, 2, 28, 8, 30, 0)), Is.True);
        Assert.That(EconomyBoostService.IsActive(boost, new DateTime(2026, 2, 27, 8, 30, 0)), Is.False);
    }

    /// <summary>
    ///     An empty command list covers every command and a listed one covers only itself.
    /// </summary>
    [Test]
    public void BoostCommandsAreMatched()
    {
        var all = Boost(EconomyBoostSchedule.OneOff, DateTime.UtcNow, 60);
        var some = Boost(EconomyBoostSchedule.OneOff, DateTime.UtcNow, 60, "work,daily");

        Assert.That(EconomyBoostService.AppliesTo(all, "crime"), Is.True);
        Assert.That(EconomyBoostService.AppliesTo(some, "daily"), Is.True);
        Assert.That(EconomyBoostService.AppliesTo(some, "crime"), Is.False);
    }

    /// <summary>
    ///     The games key covers every game but not the earning commands, and rob can be listed.
    /// </summary>
    [Test]
    public void BoostCommandsCoverGamesAndRob()
    {
        var games = Boost(EconomyBoostSchedule.OneOff, DateTime.UtcNow, 60, "games");
        var rob = Boost(EconomyBoostSchedule.OneOff, DateTime.UtcNow, 60, "rob");

        Assert.That(EconomyBoostService.AppliesTo(games, "slots"), Is.True);
        Assert.That(EconomyBoostService.AppliesTo(games, "work"), Is.False);
        Assert.That(EconomyBoostService.AppliesTo(rob, "rob"), Is.True);
        Assert.That(EconomyBoostService.BoostableCommands, Does.Contain("rob").And.Contain("games"));
    }

    /// <summary>
    ///     Extra entry pairs round trip and ignore malformed parts.
    /// </summary>
    [Test]
    public void ExtraEntriesParseAndFormat()
    {
        var parsed = GiveawayExtraEntries.Parse("100:2 200:5 bad 300:0 400:x");

        Assert.That(parsed, Has.Count.EqualTo(2));
        Assert.That(GiveawayExtraEntries.Format(parsed), Is.EqualTo("100:2 200:5"));
        Assert.That(GiveawayExtraEntries.Format(new Dictionary<ulong, int>()), Is.Null);
        Assert.That(GiveawayExtraEntries.WeightFor([100, 200, 999], parsed), Is.EqualTo(8));
    }

    /// <summary>
    ///     Winners are unique and never more than the pool holds.
    /// </summary>
    [Test]
    public void WinnersAreUniqueAndCapped()
    {
        var pool = Enumerable.Range(1, 5).Select(x => (x, x)).ToList();

        var winners = GiveawayExtraEntries.PickWinners(pool, 10);

        Assert.That(winners, Has.Count.EqualTo(5));
        Assert.That(winners.Distinct().Count(), Is.EqualTo(5));
    }

    /// <summary>
    ///     A heavily weighted entry wins far more often than an unweighted one.
    /// </summary>
    [Test]
    public void ExtraEntriesImproveTheOdds()
    {
        var pool = new List<(string, int)> { ("heavy", 99), ("light", 1) };
        var heavyWins = Enumerable.Range(0, 1000)
            .Count(_ => GiveawayExtraEntries.PickWinners(pool, 1)[0] == "heavy");

        Assert.That(heavyWins, Is.GreaterThan(900));
    }

    /// <summary>
    ///     The birth year rule and the member's display mode decide whether an age may be shown.
    /// </summary>
    [Test]
    public void BirthYearRuleControlsAges()
    {
        var shared = new DiscordUser { Birthday = new DateTime(2000, 5, 14), BirthdayDisplayMode = 0 };
        var hidden = new DiscordUser
        {
            Birthday = new DateTime(2000, 5, 14), BirthdayDisplayMode = (int)BirthdayDisplayModeEnum.MonthAndDate
        };

        Assert.That(BirthdayService.SharesBirthYear(shared, (int)BirthYearRule.Optional), Is.True);
        Assert.That(BirthdayService.SharesBirthYear(shared, (int)BirthYearRule.NotAllowed), Is.False);
        Assert.That(BirthdayService.SharesBirthYear(hidden, (int)BirthYearRule.Required), Is.False);
    }
}
