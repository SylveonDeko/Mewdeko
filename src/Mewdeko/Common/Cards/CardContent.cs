using Mewdeko.Common.Palette;
using Mewdeko.Modules.Achievements.Common;
using SkiaSharp;

namespace Mewdeko.Common.Cards;

/// <summary>
///     What one card shows: the values and images a feature supplies to <see cref="CardRenderer" /> when it draws a
///     design.
/// </summary>
public abstract class CardContent
{
    /// <summary>
    ///     The server's dashboard palette, which color tokens resolve against.
    /// </summary>
    public required DashboardPalette Palette { get; init; }

    /// <summary>
    ///     The color the <c>grade</c> token stands for.
    /// </summary>
    public virtual SKColor Accent => Palette.PrimaryColor;

    /// <summary>
    ///     Whether the card shows something not earned yet, which mutes icon images.
    /// </summary>
    public virtual bool Dimmed => false;

    /// <summary>
    ///     Whether text elements draw a shadow when they have a shadow color and offset.
    /// </summary>
    public virtual bool TextShadows => false;

    /// <summary>
    ///     Where, shown after the member's name by <see cref="CardElementType.Member" />.
    /// </summary>
    public virtual string? Scope => null;

    /// <summary>
    ///     Progress from 0 to 1 for the progress bar, or null for none.
    /// </summary>
    public virtual float? Progress => null;

    /// <summary>
    ///     Text beside the progress bar.
    /// </summary>
    public virtual string? ProgressLabel => null;

    /// <summary>
    ///     The glyph drawn on the icon element when there is no image for it.
    /// </summary>
    public virtual AchievementGlyph? IconGlyph => null;

    /// <summary>
    ///     The keys of the images <see cref="LoadImageAsync" /> supplies: <c>avatar</c>, <c>servericon</c>, <c>icon</c>.
    /// </summary>
    public virtual IEnumerable<string> ImageKeys => [];

    /// <summary>
    ///     Whether an element is drawn on this card, after its own visibility.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>True to draw it.</returns>
    public virtual bool IsShown(CardElement element)
    {
        return true;
    }

    /// <summary>
    ///     The text an element shows with placeholders filled in: its own text when it has some, otherwise what its
    ///     kind shows by default.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>The text, or empty for none.</returns>
    public abstract string TextOf(CardElement element);

    /// <summary>
    ///     The glyph drawn beside an element's text when the element doesn't pick one.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>The glyph, or null for none.</returns>
    public virtual AchievementGlyph? DefaultGlyph(CardElement element)
    {
        return null;
    }

    /// <summary>
    ///     Loads one of the card's own images.
    /// </summary>
    /// <param name="key">One of <see cref="ImageKeys" />.</param>
    /// <returns>The image, or null when there is none.</returns>
    public abstract Task<SKBitmap?> LoadImageAsync(string key);

    /// <summary>
    ///     Loads an image a design links to.
    /// </summary>
    /// <param name="link">An https URL or <c>upload:id</c>.</param>
    /// <returns>The image, or null when it can't be loaded.</returns>
    public abstract Task<SKBitmap?> LoadLinkAsync(string link);
}
