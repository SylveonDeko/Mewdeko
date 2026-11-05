using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Tests;

/// <summary>
///     Verifies the counts behind the emoji, spoiler, zalgo and media-only automod rules.
/// </summary>
[TestFixture]
public class AutomodDetectorTests
{
    [TestCase("", 0)]
    [TestCase("plain text", 0)]
    [TestCase("hi 😀", 1)]
    [TestCase("😀😀😀", 3)]
    [TestCase("<:wave:123456789> <a:dance:987654321>", 2)]
    [TestCase("<:wave:123456789> 😀 ❤️", 3)]
    [TestCase("👍🏽 👨‍👩‍👧 🇺🇸", 3)]
    [TestCase("1️⃣ 2️⃣", 2)]
    [TestCase("1 2 3 # *", 0)]
    [TestCase("© ® ™", 0)]
    public void CountEmojisCountsCustomAndUnicode(string content, int expected)
    {
        Assert.That(AutomodDetectors.CountEmojis(content), Is.EqualTo(expected));
    }

    [TestCase("", 0)]
    [TestCase("no spoilers", 0)]
    [TestCase("||one||", 1)]
    [TestCase("||one|| and ||two|| and ||three||", 3)]
    [TestCase("||multi\nline||", 1)]
    [TestCase("||||", 0)]
    [TestCase("a || b", 0)]
    public void CountSpoilersCountsTags(string content, int expected)
    {
        Assert.That(AutomodDetectors.CountSpoilers(content, []), Is.EqualTo(expected));
    }

    [Test]
    public void CountSpoilersCountsSpoilerAttachments()
    {
        string[] files = ["SPOILER_cat.png", "dog.png", "SPOILER_bird.jpg"];
        Assert.That(AutomodDetectors.CountSpoilers("||tag||", files), Is.EqualTo(3));
    }

    [Test]
    public void MaxCombiningMarksIgnoresNormalText()
    {
        Assert.That(AutomodDetectors.MaxCombiningMarks(""), Is.EqualTo(0));
        Assert.That(AutomodDetectors.MaxCombiningMarks("hello world"), Is.EqualTo(0));
        Assert.That(AutomodDetectors.MaxCombiningMarks("café résumé"), Is.EqualTo(0));
        Assert.That(AutomodDetectors.MaxCombiningMarks("café"), Is.EqualTo(1));
        Assert.That(AutomodDetectors.MaxCombiningMarks("❤️ 1️⃣"), Is.LessThanOrEqualTo(1));
    }

    [Test]
    public void MaxCombiningMarksFindsStackedMarks()
    {
        const string zalgo = "h̀́̂̃̄̅̆i";
        Assert.That(AutomodDetectors.MaxCombiningMarks(zalgo), Is.EqualTo(7));
        Assert.That(AutomodDetectors.MaxCombiningMarks("hi " + zalgo), Is.EqualTo(7));
    }

    [Test]
    public void MaxCombiningMarksSkipsVariationSelectors()
    {
        Assert.That(AutomodDetectors.MaxCombiningMarks("❤️"), Is.EqualTo(0));
    }

    [TestCase("cat.PNG", true)]
    [TestCase("cat.webp", true)]
    [TestCase("clip.mp4", false)]
    [TestCase("noextension", false)]
    public void IsImageNameMatchesImageExtensions(string name, bool expected)
    {
        Assert.That(AutomodDetectors.IsImageName(name), Is.EqualTo(expected));
    }

    [TestCase("clip.MP4", true)]
    [TestCase("clip.webm", true)]
    [TestCase("cat.png", false)]
    public void IsVideoNameMatchesVideoExtensions(string name, bool expected)
    {
        Assert.That(AutomodDetectors.IsVideoName(name), Is.EqualTo(expected));
    }

    [TestCase("look https://example.com/a.png?x=1", true)]
    [TestCase("https://tenor.com/view/cat-123", true)]
    [TestCase("https://media.giphy.com/media/abc", true)]
    [TestCase("https://example.com/page", false)]
    [TestCase("no link", false)]
    public void HasImageLinkFindsImages(string content, bool expected)
    {
        Assert.That(AutomodDetectors.HasImageLink(content), Is.EqualTo(expected));
    }

    [TestCase("https://example.com/clip.mp4", true)]
    [TestCase("https://www.youtube.com/watch?v=abc", true)]
    [TestCase("https://youtu.be/abc", true)]
    [TestCase("https://example.com/cat.png", false)]
    [TestCase("no link", false)]
    public void HasVideoLinkFindsVideos(string content, bool expected)
    {
        Assert.That(AutomodDetectors.HasVideoLink(content), Is.EqualTo(expected));
    }
}
