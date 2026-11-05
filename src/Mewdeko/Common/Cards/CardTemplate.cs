using System.Text.Json;

namespace Mewdeko.Common.Cards;

/// <summary>
///     A design for a card: its size and background, and every element drawn on it in order, back to front. Every
///     feature that draws cards stores its designs in this shape and draws them with <see cref="CardRenderer" />.
/// </summary>
public sealed class CardTemplate
{
    /// <summary>
    ///     Smallest and largest card width.
    /// </summary>
    public const int MinWidth = 600, MaxWidth = 1600;

    /// <summary>
    ///     Smallest and largest card height.
    /// </summary>
    public const int MinHeight = 200, MaxHeight = 900;

    /// <summary>
    ///     Most elements a card holds.
    /// </summary>
    public const int MaxElements = 64;

    /// <summary>
    ///     Longest text an element holds.
    /// </summary>
    public const int MaxText = 300;

    /// <summary>
    ///     The JSON shape templates are stored and sent in.
    /// </summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    ///     Card width in pixels.
    /// </summary>
    public int Width { get; set; } = 1200;

    /// <summary>
    ///     Card height in pixels.
    /// </summary>
    public int Height { get; set; } = 420;

    /// <summary>
    ///     Corner radius of the card.
    /// </summary>
    public float Radius { get; set; } = 36;

    /// <summary>
    ///     Border color, as a color token.
    /// </summary>
    public string BorderColor { get; set; } = "primary@30";

    /// <summary>
    ///     Border width, or 0 for none.
    /// </summary>
    public float BorderWidth { get; set; } = 2;

    /// <summary>
    ///     Whether a soft shadow sits under the card.
    /// </summary>
    public bool Shadow { get; set; } = true;

    /// <summary>
    ///     What fills the card behind the elements.
    /// </summary>
    public CardBackground Background { get; set; } = new();

    /// <summary>
    ///     The elements, back to front.
    /// </summary>
    public List<CardElement> Elements { get; set; } = [];

    /// <summary>
    ///     Reads a stored template, or the profile's default when there is none or it can't be read.
    /// </summary>
    /// <param name="json">The stored JSON.</param>
    /// <param name="profile">What the feature allows on its cards.</param>
    /// <returns>The template.</returns>
    public static CardTemplate Parse(string? json, CardProfile profile)
    {
        if (string.IsNullOrWhiteSpace(json))
            return profile.Default();
        try
        {
            var parsed = JsonSerializer.Deserialize<CardTemplate>(json, Json);
            return parsed is null ? profile.Default() : CardRules.Sanitize(parsed, profile);
        }
        catch (JsonException)
        {
            return profile.Default();
        }
    }

    /// <summary>
    ///     The template as stored.
    /// </summary>
    /// <returns>The JSON.</returns>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, Json);
    }

    /// <summary>
    ///     A deep copy.
    /// </summary>
    /// <returns>The copy.</returns>
    public CardTemplate Clone()
    {
        return JsonSerializer.Deserialize<CardTemplate>(Serialize(), Json)!;
    }
}

/// <summary>
///     What a feature allows on its cards and how a missing or unreadable design is replaced.
/// </summary>
public sealed class CardProfile
{
    /// <summary>
    ///     Kinds the feature always draws once: they can be hidden but not repeated or removed.
    /// </summary>
    public required IReadOnlyList<string> BuiltIn { get; init; }

    /// <summary>
    ///     Kinds a design can hold any number of.
    /// </summary>
    public required IReadOnlyList<string> Custom { get; init; }

    /// <summary>
    ///     The placeholders text can use. Text written with braces instead of percent signs is read back with them.
    /// </summary>
    public required IReadOnlyList<string> Placeholders { get; init; }

    /// <summary>
    ///     Makes a fresh copy of the feature's built in design.
    /// </summary>
    public required Func<CardTemplate> Default { get; init; }

    /// <summary>
    ///     Whether elements can be limited to a state of the card, with <see cref="CardElement.Show" />.
    /// </summary>
    public bool AllowShow { get; init; }

    /// <summary>
    ///     Whether built in kinds are drawn once each with their own text when not overridden.
    /// </summary>
    /// <param name="type">The kind.</param>
    /// <returns>True for built in kinds.</returns>
    public bool IsBuiltIn(string type)
    {
        return BuiltIn.Contains(type);
    }
}

/// <summary>
///     What fills a card behind its elements.
/// </summary>
public sealed class CardBackground
{
    /// <summary>
    ///     <c>palette</c> for the dashboard's palette card, <c>solid</c>, <c>gradient</c>, or <c>image</c>.
    /// </summary>
    public string Kind { get; set; } = "palette";

    /// <summary>
    ///     The solid color or the gradient's first stop, as a color token.
    /// </summary>
    public string Color { get; set; } = "#1a202c";

    /// <summary>
    ///     The gradient's last stop, as a color token.
    /// </summary>
    public string Color2 { get; set; } = "primary@40";

    /// <summary>
    ///     The gradient's angle in CSS degrees.
    /// </summary>
    public float Angle { get; set; } = 135;

    /// <summary>
    ///     The image, as an https URL, <c>upload:id</c> or <c>preset:id</c>.
    /// </summary>
    public string Url { get; set; } = "";

    /// <summary>
    ///     <c>cover</c> to fill the card, or <c>contain</c> to fit inside it.
    /// </summary>
    public string Fit { get; set; } = "cover";

    /// <summary>
    ///     How much the dark page color covers an image, from 0 to 1, to keep text readable.
    /// </summary>
    public float Dim { get; set; } = 0.35f;

    /// <summary>
    ///     Whether the server's palette wash is laid over a solid, gradient, or image background.
    /// </summary>
    public bool Wash { get; set; }
}

/// <summary>
///     The kinds of element every card feature draws. Each feature allows the kinds it fills in through its
///     <see cref="CardProfile" />.
/// </summary>
public static class CardElementType
{
    /// <summary>Achievements: the achievement's icon on a tile.</summary>
    public const string Icon = "icon";

    /// <summary>Achievements: the grade badge.</summary>
    public const string Grade = "grade";

    /// <summary>Achievements: the state line, such as "Achievement unlocked".</summary>
    public const string Label = "label";

    /// <summary>Achievements: the achievement's name.</summary>
    public const string Title = "title";

    /// <summary>Achievements: the achievement's description.</summary>
    public const string Description = "description";

    /// <summary>Achievements: the progress bar of a locked achievement.</summary>
    public const string Progress = "progress";

    /// <summary>The member's avatar.</summary>
    public const string Avatar = "avatar";

    /// <summary>Achievements: the member's name and the server.</summary>
    public const string Member = "member";

    /// <summary>Achievements: the category badge.</summary>
    public const string Category = "category";

    /// <summary>Achievements: the points badge.</summary>
    public const string Points = "points";

    /// <summary>Achievements: the badge counting other unlocks at the same time.</summary>
    public const string More = "more";

    /// <summary>The member's name.</summary>
    public const string Username = "username";

    /// <summary>The server's name.</summary>
    public const string ServerName = "servername";

    /// <summary>The server's icon.</summary>
    public const string ServerIcon = "servericon";

    /// <summary>The server's member count.</summary>
    public const string MemberCount = "membercount";

    /// <summary>A rectangle.</summary>
    public const string Rectangle = "rectangle";

    /// <summary>An ellipse.</summary>
    public const string Ellipse = "ellipse";

    /// <summary>Text with placeholders.</summary>
    public const string Text = "text";

    /// <summary>An image.</summary>
    public const string Image = "image";

    /// <summary>A Font Awesome glyph.</summary>
    public const string Glyph = "glyph";

    /// <summary>
    ///     Kinds drawn as lines of text.
    /// </summary>
    public static readonly IReadOnlyList<string> TextKinds =
        [Label, Title, Description, Text, Member, Username, ServerName, MemberCount];

    /// <summary>
    ///     Kinds drawn as badges.
    /// </summary>
    public static readonly IReadOnlyList<string> PillKinds = [Grade, Category, Points, More];

    /// <summary>
    ///     Kinds drawn as a picture of the member or the server.
    /// </summary>
    public static readonly IReadOnlyList<string> PortraitKinds = [Avatar, ServerIcon];
}

/// <summary>
///     When an element is drawn.
/// </summary>
public static class CardShow
{
    /// <summary>On every card.</summary>
    public const string Always = "always";

    /// <summary>Only on unlocked achievements.</summary>
    public const string Unlocked = "unlocked";

    /// <summary>Only on locked achievements.</summary>
    public const string Locked = "locked";
}

/// <summary>
///     One element of a card. Which properties matter depends on <see cref="Type" />; the rest are ignored.
/// </summary>
public sealed class CardElement
{
    /// <summary>Unique within the card: lowercase letters, digits, and dashes.</summary>
    public string Id { get; set; } = "";

    /// <summary>The kind, from <see cref="CardElementType" />.</summary>
    public string Type { get; set; } = CardElementType.Rectangle;

    /// <summary>The name shown in the layer list.</summary>
    public string Name { get; set; } = "";

    /// <summary>Whether it is drawn at all.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>When it is drawn, from <see cref="CardShow" />.</summary>
    public string Show { get; set; } = CardShow.Always;

    /// <summary>Left edge.</summary>
    public float X { get; set; }

    /// <summary>Top edge.</summary>
    public float Y { get; set; }

    /// <summary>Width.</summary>
    public float W { get; set; } = 160;

    /// <summary>Height.</summary>
    public float H { get; set; } = 60;

    /// <summary>Rotation in degrees around the center.</summary>
    public float Rotation { get; set; }

    /// <summary>Opacity from 0 to 1.</summary>
    public float Opacity { get; set; } = 1;

    /// <summary>Fill color token, or empty for none.</summary>
    public string Fill { get; set; } = "";

    /// <summary>The fill gradient's last stop, or empty for a solid fill.</summary>
    public string Fill2 { get; set; } = "";

    /// <summary>The fill gradient's angle in CSS degrees.</summary>
    public float FillAngle { get; set; } = 135;

    /// <summary>Outline color token, or empty for none.</summary>
    public string Stroke { get; set; } = "";

    /// <summary>Outline width.</summary>
    public float StrokeWidth { get; set; }

    /// <summary>Corner radius.</summary>
    public float Radius { get; set; }

    /// <summary>Shadow color token, or empty for none.</summary>
    public string ShadowColor { get; set; } = "";

    /// <summary>Shadow blur.</summary>
    public float ShadowBlur { get; set; }

    /// <summary>Shadow horizontal offset.</summary>
    public float ShadowX { get; set; }

    /// <summary>Shadow vertical offset.</summary>
    public float ShadowY { get; set; }

    /// <summary>Main ink: text, the icon and glyph elements' glyph, and the progress fill.</summary>
    public string Color { get; set; } = "text";

    /// <summary>Second ink: the glyph beside text, the server after the member's name, and the progress label.</summary>
    public string Color2 { get; set; } = "muted";

    /// <summary>A duo glyph's back layer, drawn at half opacity, or empty for the glyph's front color.</summary>
    public string Color3 { get; set; } = "";

    /// <summary>Font size.</summary>
    public float FontSize { get; set; } = 24;

    /// <summary>Bold or regular weight.</summary>
    public bool Bold { get; set; } = true;

    /// <summary>The typeface: empty for Noto Sans, or <c>unisans</c>.</summary>
    public string Font { get; set; } = "";

    /// <summary>
    ///     Whether single line text shrinks to fit its box, down to 20 pixels, before it is cut short with dots.
    /// </summary>
    public bool Shrink { get; set; }

    /// <summary><c>left</c>, <c>center</c>, or <c>right</c>.</summary>
    public string Align { get; set; } = "left";

    /// <summary>Whether text is drawn in capitals.</summary>
    public bool Uppercase { get; set; }

    /// <summary>Extra space between letters.</summary>
    public float Spacing { get; set; }

    /// <summary>Line height as a multiple of the font size.</summary>
    public float LineHeight { get; set; } = 1.25f;

    /// <summary>Most lines text wraps to.</summary>
    public int MaxLines { get; set; } = 1;

    /// <summary>
    ///     Text with placeholders, emoji, and <c>{icon:name}</c> glyphs. Text elements draw it; built in text and
    ///     badges draw it in place of their own text when it is not blank.
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>What the label says on locked achievements instead of its own text, when not blank.</summary>
    public string LockedText { get; set; } = "";

    /// <summary>
    ///     A Font Awesome glyph name: the glyph of glyph elements, or one drawn before text. <c>none</c> hides the
    ///     label's state glyph.
    /// </summary>
    public string Glyph { get; set; } = "";

    /// <summary>An image as an https URL, <c>upload:id</c> or <c>preset:id</c>, for image elements.</summary>
    public string Url { get; set; } = "";

    /// <summary><c>cover</c> or <c>contain</c>, for image elements.</summary>
    public string Fit { get; set; } = "cover";

    /// <summary>Whether a badge shrinks to its text and sits at <see cref="Align" /> inside its box.</summary>
    public bool AutoWidth { get; set; } = true;

    /// <summary>
    ///     An element this one sits under: when that element's text wraps to more lines than one, this one moves
    ///     down by the extra height and its bottom edge stays put.
    /// </summary>
    public string FollowId { get; set; } = "";

    /// <summary>
    ///     A badge this one sits beside: a right aligned badge ends <see cref="Gap" /> pixels before it, and a left
    ///     aligned badge starts <see cref="Gap" /> pixels after it.
    /// </summary>
    public string BesideId { get; set; } = "";

    /// <summary>The space kept from <see cref="BesideId" />.</summary>
    public float Gap { get; set; } = 10;

    /// <summary>
    ///     A copy drawn at another font size, for text that shrinks to fit.
    /// </summary>
    /// <param name="size">The font size.</param>
    /// <returns>The copy.</returns>
    internal CardElement WithFontSize(float size)
    {
        var copy = (CardElement)MemberwiseClone();
        copy.FontSize = size;
        return copy;
    }
}
