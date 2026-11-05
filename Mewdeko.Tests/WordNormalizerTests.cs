using Mewdeko.Modules.Permissions.Common;

namespace Mewdeko.Tests;

/// <summary>
///     Verifies that the word filter's normalisation undoes the usual ways of writing a word to get past it.
/// </summary>
[TestFixture]
public class WordNormalizerTests
{
    [TestCase("b.a.d")]
    [TestCase("b a d")]
    [TestCase("b-a_d")]
    [TestCase("baaaad")]
    [TestCase("BAD")]
    [TestCase("b4d")]
    [TestCase("ваd")]
    [TestCase("ｂａｄ")]
    [TestCase("b​ad")]
    [TestCase("bàd")]
    public void SquashReducesEvasionsToTheWord(string text)
    {
        Assert.That(WordNormalizer.Squash(text), Is.EqualTo("bad"));
    }

    [Test]
    public void SquashHandlesOneAsEitherLetter()
    {
        Assert.That(WordNormalizer.Squash("1ate"), Is.EqualTo("iate"));
        Assert.That(WordNormalizer.Squash("1ate", true), Is.EqualTo("late"));
    }

    [Test]
    public void CleanKeepsSeparators()
    {
        Assert.That(WordNormalizer.Clean("B4D w0rd"), Is.EqualTo("bad word"));
    }

    [TestCase("word", true)]
    [TestCase("two words", true)]
    [TestCase(@"\bword\b", false)]
    [TestCase("w.rd", false)]
    [TestCase("", false)]
    public void IsPlainSeparatesWordsFromPatterns(string pattern, bool expected)
    {
        Assert.That(WordNormalizer.IsPlain(pattern), Is.EqualTo(expected));
    }
}
