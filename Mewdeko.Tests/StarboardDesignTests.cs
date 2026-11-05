using Discord;
using Mewdeko.Modules.Starboard.Common;
using NUnit.Framework;

namespace Mewdeko.Tests;

/// <summary>
///     Tests for rendering starboard post designs.
/// </summary>
[TestFixture]
public class StarboardDesignTests
{
    private static StarboardPostData Data(string content = "hello", string? image = null) => new(
        1, "Guild", 2, 3, "Author", "https://example.com/avatar.png", content, 4, "general",
        "https://discord.com/channels/1/4/2", 5, "⭐", "⭐ **5**", [], image,
        new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc));

    /// <summary>
    ///     Quotes and line breaks in the message do not break the json.
    /// </summary>
    [Test]
    public void MessageTextIsEscaped()
    {
        const string design = """{"embed":{"description":"%star.content%","footer":{"text":"%star.count% stars"}}}""";

        var rendered = StarboardPostDesign.TryRender(design, Data("say \"hi\"\nthere \\ ok"), out _, out var embeds,
            out _);

        Assert.That(rendered, Is.True);
        Assert.That(embeds![0].Description, Is.EqualTo("say \"hi\"\nthere \\ ok"));
        Assert.That(embeds[0].Footer!.Value.Text, Is.EqualTo("5 stars"));
    }

    /// <summary>
    ///     The timestamp, image and link button of a design are kept, and a button with no link is dropped.
    /// </summary>
    [Test]
    public void TimestampImageAndLinkButtonsAreKept()
    {
        const string design = """
            {"content":"%star.author.mention%","embed":{"description":"x","timestamp":"%star.timestamp%",
              "image":{"url":"%star.image%"}},
             "components":[{"displayName":"Jump","url":"%star.link%"},{"displayName":"Image","url":"%star.image%"}]}
            """;

        var rendered = StarboardPostDesign.TryRender(design, Data(image: null), out var text, out var embeds,
            out var buttons);

        Assert.That(rendered, Is.True);
        Assert.That(text, Is.EqualTo("<@3>"));
        Assert.That(embeds![0].Timestamp, Is.EqualTo(new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero)));
        Assert.That(embeds[0].Image.HasValue, Is.False);
        Assert.That(buttons!.Build().Components.OfType<ActionRowComponent>().SelectMany(x => x.Components).Count(),
            Is.EqualTo(1));
    }

    /// <summary>
    ///     Json that is not an embed message, or shows nothing, does not render.
    /// </summary>
    [Test]
    public void InvalidDesignsDoNotRender()
    {
        Assert.That(StarboardPostDesign.TryRender("not json", Data(), out _, out _, out _), Is.False);
        Assert.That(StarboardPostDesign.TryRender("""{"embed":{"color":"#ffffff"}}""", Data(), out _, out _, out _),
            Is.False);
    }
}
