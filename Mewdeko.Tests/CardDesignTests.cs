using DataModel;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.CardDesigns.Common;
using Mewdeko.Modules.Currency.Services;

namespace Mewdeko.Tests;

/// <summary>
///     Tests for the pure rules behind card designs and the economy setup.
/// </summary>
[TestFixture]
public class CardDesignTests
{
    private static CardDesign Design(CardDesignAvailability availability = CardDesignAvailability.Free,
        CardDesignUnlockType? unlock = null, int? level = null, string? key = null, ulong? role = null,
        string name = "Sunset")
    {
        return new CardDesign
        {
            Id = 1,
            Name = name,
            Availability = (short)availability,
            UnlockType = (short?)unlock,
            UnlockLevel = level,
            UnlockAchievementKey = key,
            UnlockRoleId = role
        };
    }

    /// <summary>
    ///     Names must be present and short.
    /// </summary>
    [TestCase(null)]
    [TestCase("  ")]
    [TestCase("123456789012345678901234567890123456789012")]
    public void BadNamesAreRefused(string? name)
    {
        Assert.That(CardDesignRules.Validate(name, CardDesignAvailability.Free, null, null, null, null), Is.Not.Null);
    }

    /// <summary>
    ///     Free and shop designs need no unlock fields.
    /// </summary>
    [Test]
    public void FreeAndShopDesignsNeedNoUnlock()
    {
        Assert.That(CardDesignRules.Validate("Sunset", CardDesignAvailability.Free, null, null, null, null), Is.Null);
        Assert.That(CardDesignRules.Validate("Sunset", CardDesignAvailability.Shop, null, null, null, null), Is.Null);
    }

    /// <summary>
    ///     Each unlock type needs its own field.
    /// </summary>
    [Test]
    public void UnlockDesignsNeedTheirField()
    {
        const CardDesignAvailability unlock = CardDesignAvailability.Unlock;
        Assert.That(CardDesignRules.Validate("A", unlock, CardDesignUnlockType.Level, null, null, null), Is.Not.Null);
        Assert.That(CardDesignRules.Validate("A", unlock, CardDesignUnlockType.Level, 0, null, null), Is.Not.Null);
        Assert.That(CardDesignRules.Validate("A", unlock, CardDesignUnlockType.Level, 2000, null, null), Is.Not.Null);
        Assert.That(CardDesignRules.Validate("A", unlock, CardDesignUnlockType.Level, 10, null, null), Is.Null);
        Assert.That(CardDesignRules.Validate("A", unlock, CardDesignUnlockType.Achievement, null, " ", null),
            Is.Not.Null);
        Assert.That(CardDesignRules.Validate("A", unlock, CardDesignUnlockType.Achievement, null, "first", null),
            Is.Null);
        Assert.That(CardDesignRules.Validate("A", unlock, CardDesignUnlockType.Role, null, null, 0), Is.Not.Null);
        Assert.That(CardDesignRules.Validate("A", unlock, CardDesignUnlockType.Role, null, null, 5), Is.Null);
        Assert.That(CardDesignRules.Validate("A", unlock, null, null, null, null), Is.Not.Null);
    }

    /// <summary>
    ///     Fields a design doesn't use are cleared.
    /// </summary>
    [Test]
    public void UnusedUnlockFieldsAreCleared()
    {
        var design = Design(CardDesignAvailability.Unlock, CardDesignUnlockType.Level, 5, "key", 9);
        CardDesignRules.ClearUnusedUnlock(design);
        Assert.That(design.UnlockLevel, Is.EqualTo(5));
        Assert.That(design.UnlockAchievementKey, Is.Null);
        Assert.That(design.UnlockRoleId, Is.Null);

        var free = Design(CardDesignAvailability.Free, CardDesignUnlockType.Level, 5);
        CardDesignRules.ClearUnusedUnlock(free);
        Assert.That(free.UnlockType, Is.Null);
        Assert.That(free.UnlockLevel, Is.Null);
    }

    /// <summary>
    ///     A level design unlocks at its level and above.
    /// </summary>
    [Test]
    public void LevelUnlocksAtOrAbove()
    {
        var design = Design(CardDesignAvailability.Unlock, CardDesignUnlockType.Level, 10);
        Assert.That(CardDesignRules.Unlocks(design, 9, null, null), Is.False);
        Assert.That(CardDesignRules.Unlocks(design, 10, null, null), Is.True);
        Assert.That(CardDesignRules.Unlocks(design, 40, null, null), Is.True);
        Assert.That(CardDesignRules.Unlocks(design, null, ["first"], [1UL]), Is.False);
    }

    /// <summary>
    ///     An achievement design unlocks only for that achievement.
    /// </summary>
    [Test]
    public void AchievementUnlocksOnMatchingKey()
    {
        var design = Design(CardDesignAvailability.Unlock, CardDesignUnlockType.Achievement, key: "first_message");
        Assert.That(CardDesignRules.Unlocks(design, null, ["other"], null), Is.False);
        Assert.That(CardDesignRules.Unlocks(design, null, ["FIRST_MESSAGE"], null), Is.True);
        Assert.That(CardDesignRules.Unlocks(design, 99, null, null), Is.False);
    }

    /// <summary>
    ///     A role design unlocks only for holders of that role.
    /// </summary>
    [Test]
    public void RoleUnlocksForHolders()
    {
        var design = Design(CardDesignAvailability.Unlock, CardDesignUnlockType.Role, role: 77);
        Assert.That(CardDesignRules.Unlocks(design, null, null, [1UL, 2UL]), Is.False);
        Assert.That(CardDesignRules.Unlocks(design, null, null, [1UL, 77UL]), Is.True);
    }

    /// <summary>
    ///     Free and shop designs are never unlocked by standing.
    /// </summary>
    [Test]
    public void NonUnlockDesignsNeverUnlock()
    {
        Assert.That(CardDesignRules.Unlocks(Design(), 100, ["a"], [1UL]), Is.False);
        Assert.That(CardDesignRules.Unlocks(Design(CardDesignAvailability.Shop), 100, ["a"], [1UL]), Is.False);
    }

    /// <summary>
    ///     Only free or owned designs can be equipped.
    /// </summary>
    [Test]
    public void EquipNeedsFreeOrOwned()
    {
        Assert.That(CardDesignRules.CanEquip(Design(), false), Is.True);
        Assert.That(CardDesignRules.CanEquip(Design(CardDesignAvailability.Shop), false), Is.False);
        Assert.That(CardDesignRules.CanEquip(Design(CardDesignAvailability.Shop), true), Is.True);
        Assert.That(CardDesignRules.CanEquip(Design(CardDesignAvailability.Unlock), false), Is.False);
    }

    /// <summary>
    ///     A usable equipped design wins over the default, which wins over the built in design.
    /// </summary>
    [Test]
    public void PickOrder()
    {
        var equipped = Design(name: "Mine");
        var paid = Design(CardDesignAvailability.Shop, name: "Paid");
        var community = Design(name: "Community");

        Assert.That(CardDesignRules.Pick(equipped, false, community), Is.SameAs(equipped));
        Assert.That(CardDesignRules.Pick(paid, true, community), Is.SameAs(paid));
        Assert.That(CardDesignRules.Pick(paid, false, community), Is.SameAs(community));
        Assert.That(CardDesignRules.Pick(null, false, community), Is.SameAs(community));
        Assert.That(CardDesignRules.Pick(null, false, null), Is.Null);
    }

    /// <summary>
    ///     Rank card backgrounds are tried equipped design first, built in last.
    /// </summary>
    [Test]
    public void RankSourceOrder()
    {
        Assert.That(CardDesignRules.RankOrder, Is.EqualTo(new[]
        {
            RankSource.Equipped, RankSource.Personal, RankSource.CommunityDefault, RankSource.Server,
            RankSource.BuiltIn
        }));
    }

    /// <summary>
    ///     Designs are found by name regardless of case.
    /// </summary>
    [Test]
    public void FindByNameIgnoresCase()
    {
        var designs = new[]
        {
            Design(name: "Sunset"), Design(name: "Night")
        };
        Assert.That(CardDesignRules.FindByName(designs, " sunset ")?.Name, Is.EqualTo("Sunset"));
        Assert.That(CardDesignRules.FindByName(designs, "dawn"), Is.Null);
        Assert.That(CardDesignRules.FindByName(designs, ""), Is.Null);
    }

    /// <summary>
    ///     Background references are presets, uploads or web addresses.
    /// </summary>
    [TestCase("", true)]
    [TestCase("preset:nature-dusk", true)]
    [TestCase("upload:12", true)]
    [TestCase("upload:abc", false)]
    [TestCase("https://example.com/a.png", true)]
    [TestCase("ftp://example.com/a.png", false)]
    [TestCase("not a url", false)]
    public void BackgroundReferenceShapes(string reference, bool valid)
    {
        Assert.That(CardDesignRules.IsValidBackgroundRef(reference), Is.EqualTo(valid));
    }

    /// <summary>
    ///     Rank design data and achievement templates carry their background through a round trip.
    /// </summary>
    [Test]
    public void BackgroundRoundTrips()
    {
        var rank = CardDesignRules.WithBackground(CardDesignTarget.Rank, null, "upload:3");
        Assert.That(CardDesignRules.BackgroundRef(CardDesignTarget.Rank, rank), Is.EqualTo("upload:3"));
        Assert.That(CardDesignRules.BackgroundRef(CardDesignTarget.Rank,
            CardDesignRules.WithBackground(CardDesignTarget.Rank, rank, null)), Is.Null);

        var card = CardDesignRules.WithBackground(CardDesignTarget.Achievement, null, "preset:nature-dusk");
        Assert.That(CardDesignRules.BackgroundRef(CardDesignTarget.Achievement, card),
            Is.EqualTo("preset:nature-dusk"));
        Assert.That(CardDesignRules.BackgroundRef(CardDesignTarget.Achievement,
            CardDesignRules.WithBackground(CardDesignTarget.Achievement, card, null)), Is.Null);
    }

    /// <summary>
    ///     Unreadable data has no background.
    /// </summary>
    [Test]
    public void UnreadableDataHasNoBackground()
    {
        Assert.That(CardDesignRules.BackgroundRef(CardDesignTarget.Rank, "{nope"), Is.Null);
        Assert.That(CardDesignRules.BackgroundRef(CardDesignTarget.Achievement, "{nope"), Is.Null);
        Assert.That(CardDesignRules.BackgroundRef(CardDesignTarget.Rank, null), Is.Null);
    }

    /// <summary>
    ///     The daily reward needs a sane amount and cooldown.
    /// </summary>
    [Test]
    public void RewardBounds()
    {
        Assert.That(CurrencySetupRules.ValidateReward(100, 86400), Is.Null);
        Assert.That(CurrencySetupRules.ValidateReward(0, 0), Is.Null);
        Assert.That(CurrencySetupRules.ValidateReward(-1, 86400), Is.Not.Null);
        Assert.That(CurrencySetupRules.ValidateReward(2_000_000_000, 86400), Is.Not.Null);
        Assert.That(CurrencySetupRules.ValidateReward(100, 10), Is.Not.Null);
        Assert.That(CurrencySetupRules.ValidateReward(100, 3_000_000), Is.Not.Null);
    }

    /// <summary>
    ///     Currency emoji must be a real emoji.
    /// </summary>
    [TestCase(null, false)]
    [TestCase(" ", false)]
    [TestCase("coins", false)]
    [TestCase("<:coin:123456789012345678>", true)]
    [TestCase("<a:spin:123456789012345678>", true)]
    public void EmojiShapes(string? emoji, bool valid)
    {
        Assert.That(CurrencySetupRules.ValidateEmoji(emoji) is null, Is.EqualTo(valid));
    }

    /// <summary>
    ///     Minimums can't exceed maximums, and an open ended bet maximum is allowed.
    /// </summary>
    [Test]
    public void RangesNeedMinAtMostMax()
    {
        Assert.That(CurrencySetupRules.ValidateRanges(null, null, null, null), Is.Null);
        Assert.That(CurrencySetupRules.ValidateRanges((5, 5), (1, 9), (0, 0), (1, 100)), Is.Null);
        Assert.That(CurrencySetupRules.ValidateRanges((6, 5), null, null, null), Is.Not.Null);
        Assert.That(CurrencySetupRules.ValidateRanges(null, (10, 1), null, null), Is.Not.Null);
        Assert.That(CurrencySetupRules.ValidateRanges(null, null, (10, 1), null), Is.Not.Null);
        Assert.That(CurrencySetupRules.ValidateRanges(null, null, null, (50, 10)), Is.Not.Null);
        Assert.That(CurrencySetupRules.ValidateRanges(null, null, null, (50, 0)), Is.Null);
    }
}
