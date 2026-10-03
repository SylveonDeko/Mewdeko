using Mewdeko.Common;
using NUnit.Framework;

namespace Mewdeko.Tests;

/// <summary>
///     Tests for parsing embed JSON into messages Discord accepts.
/// </summary>
[TestFixture]
public class SmartEmbedTests
{
    /// <summary>
    ///     An embed with only a color is dropped, since Discord rejects the whole message over it.
    /// </summary>
    [Test]
    public void ColorOnlyEmbedIsDropped()
    {
        const string json = """
            {"content":"Niiiice job!","embeds":[
              {"title":"Verify Via ID","color":"#ab9810","image":{"url":"https://example.com/a.png"}},
              {"title":"Verify Via Server","color":"#ab9810"},
              {"color":"#5865F2"}]}
            """;

        Assert.That(SmartEmbed.TryParse(json, null, out var embeds, out var plainText, out _), Is.True);
        Assert.That(plainText, Is.EqualTo("Niiiice job!"));
        Assert.That(embeds!.Select(e => e.Title), Is.EqualTo(new[] { "Verify Via ID", "Verify Via Server" }));
    }
}
