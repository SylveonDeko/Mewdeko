using DataModel;
using Mewdeko.Common.Cards;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Modules.CardDesigns.Common;

namespace Mewdeko.Tests;

/// <summary>
///     Tests for the pure rules behind designs members make themselves.
/// </summary>
[TestFixture]
public class MemberCardDesignTests
{
    private static CardDesignSetting Settings(MemberDesignWho who = MemberDesignWho.Everyone, bool enabled = true,
        string roles = "", int level = 5)
    {
        return new CardDesignSetting
        {
            GuildId = 1,
            MemberEnabled = enabled,
            MemberWho = (short)who,
            MemberRoleIds = roles,
            MemberMinLevel = level
        };
    }

    private static CardDesign Member(ulong owner = 7, CardDesignTarget target = CardDesignTarget.Rank,
        int? upload = null, CardDesignReview review = CardDesignReview.Approved, string? data = null)
    {
        return new CardDesign
        {
            Id = 3,
            GuildId = 1,
            Name = CardDesignRules.MemberName(owner, target),
            Target = (short)target,
            Data = data ?? CardDesignRules.RankData("preset:nature-dusk"),
            OwnerUserId = owner,
            UploadId = upload,
            ReviewStatus = (short)review
        };
    }

    /// <summary>
    ///     Nobody qualifies while the community has member designs off.
    /// </summary>
    [Test]
    public void DisabledCommunitiesAllowNobody()
    {
        Assert.That(CardDesignRules.CheckMember(Settings(enabled: false), 99, [1UL]), Is.EqualTo(MemberEligibility.Disabled));
    }

    /// <summary>
    ///     Everyone qualifies without checking roles or level.
    /// </summary>
    [Test]
    public void EveryoneQualifies()
    {
        Assert.That(CardDesignRules.CheckMember(Settings(), 0, []), Is.EqualTo(MemberEligibility.Eligible));
    }

    /// <summary>
    ///     Any one of the chosen roles is enough, and an empty list lets nobody in.
    /// </summary>
    [Test]
    public void RoleOptionNeedsAnyChosenRole()
    {
        var settings = Settings(MemberDesignWho.Roles, roles: "10,20");
        Assert.That(CardDesignRules.CheckMember(settings, 0, [20UL, 30UL]), Is.EqualTo(MemberEligibility.Eligible));
        Assert.That(CardDesignRules.CheckMember(settings, 99, [30UL]), Is.EqualTo(MemberEligibility.NeedsRole));
        Assert.That(CardDesignRules.CheckMember(Settings(MemberDesignWho.Roles), 99, [30UL]),
            Is.EqualTo(MemberEligibility.NeedsRole));
    }

    /// <summary>
    ///     The level option is at or above, and a reset level loses eligibility.
    /// </summary>
    [Test]
    public void LevelOptionIsAtOrAbove()
    {
        var settings = Settings(MemberDesignWho.Level, level: 5);
        Assert.That(CardDesignRules.CheckMember(settings, 5, []), Is.EqualTo(MemberEligibility.Eligible));
        Assert.That(CardDesignRules.CheckMember(settings, 6, []), Is.EqualTo(MemberEligibility.Eligible));
        Assert.That(CardDesignRules.CheckMember(settings, 4, []), Is.EqualTo(MemberEligibility.NeedsLevel));
        Assert.That(CardDesignRules.CheckMember(settings, 0, []), Is.EqualTo(MemberEligibility.NeedsLevel));
    }

    /// <summary>
    ///     Role IDs survive storage, ignoring junk and repeats.
    /// </summary>
    [Test]
    public void RoleIdsRoundTrip()
    {
        Assert.That(CardDesignRules.ParseRoleIds("1, 2,x,2,,0"), Is.EqualTo(new ulong[] { 1, 2 }));
        Assert.That(CardDesignRules.FormatRoleIds([3, 3, 4, 0]), Is.EqualTo("3,4"));
        Assert.That(CardDesignRules.ParseRoleIds(null), Is.Empty);
    }

    /// <summary>
    ///     Settings stay inside their limits.
    /// </summary>
    [TestCase(0, 0, 1, 0L, true)]
    [TestCase(1, 25, 1000, 100_000_000L, true)]
    [TestCase(3, 0, 1, 0L, false)]
    [TestCase(1, 26, 1, 0L, false)]
    [TestCase(2, 0, 0, 0L, false)]
    [TestCase(2, 0, 1001, 0L, false)]
    [TestCase(0, 0, 1, -1L, false)]
    [TestCase(0, 0, 1, 100_000_001L, false)]
    public void SettingsLimits(int who, int roles, int level, long cost, bool valid)
    {
        Assert.That(CardDesignRules.ValidateMemberSettings(who, roles, level, cost) is null, Is.EqualTo(valid));
    }

    /// <summary>
    ///     A member design is only equippable by its owner, whatever its availability says.
    /// </summary>
    [Test]
    public void MemberDesignsFollowOwnership()
    {
        var design = Member();
        Assert.That(CardDesignRules.CanEquip(design, false), Is.False);
        Assert.That(CardDesignRules.CanEquip(design, true), Is.True);
        Assert.That(CardDesignRules.CanEquip(new CardDesign { Availability = (short)CardDesignAvailability.Free }, false),
            Is.True);
    }

    /// <summary>
    ///     Review status follows the upload: none means approved, a new upload waits when review is on, and an old
    ///     one keeps its standing.
    /// </summary>
    [Test]
    public void ReviewFollowsTheUpload()
    {
        Assert.That(CardDesignRules.NextReview(false, false, true, CardDesignReview.Rejected),
            Is.EqualTo(CardDesignReview.Approved));
        Assert.That(CardDesignRules.NextReview(true, true, true, CardDesignReview.Approved),
            Is.EqualTo(CardDesignReview.Pending));
        Assert.That(CardDesignRules.NextReview(true, true, false, CardDesignReview.Approved),
            Is.EqualTo(CardDesignReview.Approved));
        Assert.That(CardDesignRules.NextReview(true, false, true, CardDesignReview.Pending),
            Is.EqualTo(CardDesignReview.Pending));
        Assert.That(CardDesignRules.NextReview(true, false, true, CardDesignReview.Approved),
            Is.EqualTo(CardDesignReview.Approved));
    }

    /// <summary>
    ///     A pending or rejected upload never reaches the card, an approved one does.
    /// </summary>
    [Test]
    public void OnlyApprovedUploadsRender()
    {
        var preset = CardDesignRules.RankData("preset:nature-dusk");
        Assert.That(CardDesignRules.EffectiveData(Member(upload: 9, review: CardDesignReview.Pending)), Is.EqualTo(preset));
        Assert.That(CardDesignRules.EffectiveData(Member(upload: 9, review: CardDesignReview.Rejected)), Is.EqualTo(preset));
        Assert.That(CardDesignRules.EffectiveData(Member()), Is.EqualTo(preset));

        var approved = CardDesignRules.EffectiveData(Member(upload: 9));
        Assert.That(CardDesignRules.BackgroundRef(CardDesignTarget.Rank, approved), Is.EqualTo("upload:9"));
    }

    /// <summary>
    ///     An approved upload becomes the achievement background and keeps the rest of the template.
    /// </summary>
    [Test]
    public void ApprovedUploadReplacesAchievementBackground()
    {
        var template = AchievementCards.Default();
        template.Background.Kind = "image";
        template.Background.Url = "preset:nature-dusk";
        var design = Member(target: CardDesignTarget.Achievement, upload: 4, data: template.Serialize());

        var effective = AchievementCards.Parse(CardDesignRules.EffectiveData(design));
        Assert.That(effective.Background.Url, Is.EqualTo("upload:4"));
        Assert.That(effective.Elements.Count, Is.EqualTo(template.Elements.Count));
    }

    /// <summary>
    ///     Community designs are never touched by the member rules.
    /// </summary>
    [Test]
    public void CommunityDesignsKeepTheirData()
    {
        var design = new CardDesign
        {
            Target = (short)CardDesignTarget.Rank, Data = CardDesignRules.RankData("preset:nature-dusk"), UploadId = 5
        };
        Assert.That(CardDesignRules.EffectiveData(design), Is.EqualTo(design.Data));
    }

    /// <summary>
    ///     Members get presets only: no uploads, no community images and no web addresses.
    /// </summary>
    [TestCase("", true)]
    [TestCase("preset:nature-dusk", true)]
    [TestCase("upload:3", false)]
    [TestCase("https://example.com/a.png", false)]
    public void TemplatesAllowPresetsOnly(string url, bool allowed)
    {
        var template = AchievementCards.Default();
        template.Background.Url = url;
        Assert.That(CardDesignRules.MemberTemplateProblem(template) is null, Is.EqualTo(allowed));

        template.Background.Url = "";
        template.Elements[0].Url = url;
        Assert.That(CardDesignRules.MemberTemplateProblem(template) is null, Is.EqualTo(allowed));
    }

    /// <summary>
    ///     Stored names are per member and per card.
    /// </summary>
    [Test]
    public void MemberNamesAreUniquePerMemberAndCard()
    {
        Assert.That(CardDesignRules.MemberName(1, CardDesignTarget.Rank),
            Is.Not.EqualTo(CardDesignRules.MemberName(1, CardDesignTarget.Achievement)));
        Assert.That(CardDesignRules.MemberName(1, CardDesignTarget.Rank),
            Is.Not.EqualTo(CardDesignRules.MemberName(2, CardDesignTarget.Rank)));
    }
}
