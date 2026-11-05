using Mewdeko.Common.Cards;
using SkiaSharp;

namespace Mewdeko.Modules.MultiGreets.Services;

/// <summary>
///     What a welcome card shows: the member, the server, and the images a design links to.
/// </summary>
/// <param name="replace">Fills in the placeholders of a text.</param>
/// <param name="loadImage">Loads the member's avatar (<c>avatar</c>) and the server's icon (<c>servericon</c>).</param>
/// <param name="loadLink">Loads an image a design links to.</param>
public sealed class WelcomeCardContent(
    Func<string, string> replace,
    Func<string, Task<SKBitmap?>> loadImage,
    Func<string, Task<SKBitmap?>> loadLink) : CardContent
{
    /// <inheritdoc />
    public override bool TextShadows => true;

    /// <inheritdoc />
    public override IEnumerable<string> ImageKeys => ["avatar", "servericon"];

    /// <inheritdoc />
    public override string TextOf(CardElement element)
    {
        if (element.Text.Trim().Length > 0)
            return replace(element.Text);
        return element.Type switch
        {
            CardElementType.Username => replace("%user.name%"),
            CardElementType.ServerName => replace("%server.name%"),
            CardElementType.MemberCount => replace("%server.members%"),
            _ => ""
        };
    }

    /// <inheritdoc />
    public override Task<SKBitmap?> LoadImageAsync(string key)
    {
        return loadImage(key);
    }

    /// <inheritdoc />
    public override Task<SKBitmap?> LoadLinkAsync(string link)
    {
        return loadLink(link);
    }
}
