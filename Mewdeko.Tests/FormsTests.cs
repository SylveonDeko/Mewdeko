using DataModel;
using Mewdeko.Modules.Forms.Common;
using Mewdeko.Modules.Forms.Services;

namespace Mewdeko.Tests;

/// <summary>
///     Verifies the form rules that do not need a database: condition operators, placeholders, picture
///     answers and the builder's checks.
/// </summary>
[TestFixture]
public class FormsTests
{
    [TestCase("is_answered", "yes", true)]
    [TestCase("is_answered", "", false)]
    [TestCase("is_answered", "   ", false)]
    [TestCase("is_not_answered", "", true)]
    [TestCase("is_not_answered", "yes", false)]
    [TestCase("equals", "Yes", true)]
    [TestCase("not_equals", "Yes", false)]
    public void CompareHandlesAnsweredAndTextOperators(string comparison, string actual, bool expected)
    {
        Assert.That(FormConditionOperators.Compare(actual, comparison, "yes"), Is.EqualTo(expected));
    }

    [Test]
    public void CompareReadsNumbersForGreaterAndLess()
    {
        Assert.That(FormConditionOperators.Compare("10", "greater_than", "9"), Is.True);
        Assert.That(FormConditionOperators.Compare("abc", "greater_than", "9"), Is.False);
        Assert.That(FormConditionOperators.Compare("1", "less_than", "9"), Is.True);
    }

    [Test]
    public void PresenceOperatorsAreRecognised()
    {
        Assert.That(FormConditionOperators.IsPresence("is_answered"), Is.True);
        Assert.That(FormConditionOperators.IsPresence("is_not_answered"), Is.True);
        Assert.That(FormConditionOperators.IsPresence("equals"), Is.False);
        Assert.That(FormConditionOperators.IsKnown("is_answered"), Is.True);
        Assert.That(FormConditionOperators.IsKnown("matches"), Is.False);
    }

    [Test]
    public void ValidatorOnlyNeedsAValueForComparingOperators()
    {
        var presence = Question("is_answered", null);
        var comparing = Question("equals", null);
        var unknown = Question("matches", "x");

        Assert.That(FormValidator.ValidateQuestion(presence), Is.Empty);
        Assert.That(FormValidator.ValidateQuestion(comparing), Is.Not.Empty);
        Assert.That(FormValidator.ValidateQuestion(unknown), Is.Not.Empty);
    }

    [Test]
    public void ValidatorAcceptsPictureQuestionsAndRefusesUnknownTypes()
    {
        var picture = Question(null, null);
        picture.QuestionType = "image";
        Assert.That(FormValidator.ValidateQuestion(picture), Is.Empty);

        picture.QuestionType = "hologram";
        Assert.That(FormValidator.ValidateQuestion(picture), Is.Not.Empty);
    }

    [Test]
    public void PlaceholdersFillFormUserAndServerTokens()
    {
        var form = new Form
        {
            Id = 1, GuildId = 99, Name = "Staff", MaxResponses = 5, RequiredRoleId = 7,
            ExpiresAt = new DateTime(2026, 12, 1, 18, 30, 0, DateTimeKind.Utc)
        };

        var plain = new FormPlaceholderContext(form, "Home", 42, "sam", "Sam", "https://x/forms/abc", "Members", true);

        Assert.That(
            FormPlaceholders.Apply(
                "%form.name% %form.submissions% %user.mention% %user.name% %server.name% %form.role% %form.expiresat% %form.opensat% %custom.thing%",
                plain),
            Is.EqualTo("Staff 5 @Sam sam Home @Members 2026-12-01 18:30 UTC  %custom.thing%"));

        var rich = plain with
        {
            PlainText = false
        };

        Assert.That(FormPlaceholders.Apply("%user.mention% %form.role%", rich), Is.EqualTo("<@42> <@&7>"));
    }

    [Test]
    public void PlaceholdersNeverTurnValuesIntoMarkup()
    {
        var form = new Form
        {
            Name = "<img src=x onerror=alert(1)>", GuildId = 1
        };

        var context = new FormPlaceholderContext(form, null, null, null, null, null, null, true);

        Assert.That(FormPlaceholders.Apply("%form.name%", context), Is.EqualTo("<img src=x onerror=alert(1)>"));
        Assert.That(FormPlaceholders.Apply("%form.name%", context with { EscapeQuotes = true }),
            Is.EqualTo("<img src=x onerror=alert(1)>"));
    }

    [Test]
    public void PlaceholdersEscapeQuotesWhenAskedTo()
    {
        var form = new Form
        {
            Name = "A \"quoted\" name", GuildId = 1
        };

        var context = new FormPlaceholderContext(form, null, null, null, null, null, null, false, true);

        Assert.That(FormPlaceholders.Apply("%form.name%", context), Is.EqualTo("A \\\"quoted\\\" name"));
    }

    [Test]
    public void OnlyFilesWrittenByTheServiceCountAsStoredPictures()
    {
        const string cdn = "https://cdn.test/";
        var name = new string('a', 32);

        Assert.That(FormImageService.IsStoredImageAddress($"https://cdn.test/forms/images/3/{name}.webp", cdn, 3),
            Is.True);
        Assert.That(FormImageService.IsStoredImageAddress($"https://cdn.test/forms/images/4/{name}.webp", cdn, 3),
            Is.False);
        Assert.That(FormImageService.IsStoredImageAddress($"https://evil.test/forms/images/3/{name}.webp", cdn, 3),
            Is.False);
        Assert.That(FormImageService.IsStoredImageAddress($"https://cdn.test/forms/images/3/../{name}.webp", cdn, 3),
            Is.False);
        Assert.That(FormImageService.IsStoredImageAddress("https://cdn.test/forms/images/3/short.webp", cdn, 3),
            Is.False);
        Assert.That(FormImageService.IsStoredImageAddress($"https://cdn.test/forms/images/3/{name}.webp", "", 3),
            Is.False);
    }

    [Test]
    public void UploadsAreCheckedByTheirBytes()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 };

        Assert.That(FormImageService.HasAllowedSignature(png), Is.True);
        Assert.That(FormImageService.HasAllowedSignature("<svg></svg>"u8.ToArray()), Is.False);

        Assert.That(FormImageService.TryDecode("data:image/png;base64," + Convert.ToBase64String(png), out var bytes,
            out _), Is.True);
        Assert.That(bytes, Is.EqualTo(png));
        Assert.That(FormImageService.TryDecode("not base64!", out _, out var problem), Is.False);
        Assert.That(problem, Is.Not.Null);
        Assert.That(FormImageService.TryDecode("", out _, out _), Is.False);
    }

    private static FormQuestion Question(string? comparison, string? expected)
    {
        return new FormQuestion
        {
            QuestionText = "Why?", QuestionType = "short_text",
            ConditionalParentQuestionId = comparison == null ? null : 1,
            ConditionalOperator = comparison, ConditionalExpectedValue = expected
        };
    }
}
