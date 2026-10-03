using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mewdeko.Common.Palette;
using SkiaSharp;

namespace Mewdeko.Modules.Achievements.Common;

/// <summary>
///     A server's design for its achievement card: the card's size and background, and every element drawn on it in
///     order, back to front.
/// </summary>
public sealed class AchievementCardTemplate
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
    public AchievementCardBackground Background { get; set; } = new();

    /// <summary>
    ///     The elements, back to front.
    /// </summary>
    public List<AchievementCardElement> Elements { get; set; } = [];

    /// <summary>
    ///     Reads a stored template, or the default when there is none or it can't be read.
    /// </summary>
    /// <param name="json">The stored JSON.</param>
    /// <returns>The template.</returns>
    public static AchievementCardTemplate Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Default();
        try
        {
            var parsed = JsonSerializer.Deserialize<AchievementCardTemplate>(json, Json);
            return parsed is null ? Default() : AchievementCardRules.Sanitize(parsed);
        }
        catch (JsonException)
        {
            return Default();
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
    public AchievementCardTemplate Clone()
    {
        return JsonSerializer.Deserialize<AchievementCardTemplate>(Serialize(), Json)!;
    }

    /// <summary>
    ///     The built in design: a dashboard card in the server's palette with the icon on a tile of the grade's color.
    /// </summary>
    /// <returns>A fresh copy of the default template.</returns>
    public static AchievementCardTemplate Default()
    {
        return new AchievementCardTemplate
        {
            Elements =
            [
                new AchievementCardElement
                {
                    Id = "footer", Type = AchievementCardElementType.Rectangle, Name = "Member row",
                    X = 56, Y = 304, W = 1088, H = 68, Radius = 26, Fill = "primary@08"
                },
                new AchievementCardElement
                {
                    Id = "icon", Type = AchievementCardElementType.Icon, X = 56, Y = 56, W = 176, H = 176,
                    Radius = 48, Fill = "grade@20", Stroke = "grade@50", StrokeWidth = 2, Color = "grade"
                },
                new AchievementCardElement
                {
                    Id = "grade", Type = AchievementCardElementType.Grade, X = 56, Y = 248, W = 176, H = 44,
                    Radius = 22, Fill = "grade@20", Stroke = "grade@40", StrokeWidth = 2, Color = "grade",
                    Color2 = "grade", FontSize = 21, Align = "center", AutoWidth = false
                },
                new AchievementCardElement
                {
                    Id = "label", Type = AchievementCardElementType.Label, X = 276, Y = 62, W = 600, H = 28,
                    Color = "muted", Color2 = "primary", Color3 = "secondary", FontSize = 20, Uppercase = true,
                    Spacing = 1.6f
                },
                new AchievementCardElement
                {
                    Id = "more", Type = AchievementCardElementType.More, X = 744, Y = 56, W = 400, H = 40,
                    Radius = 18, Fill = "primary@20", Color = "primary", FontSize = 21, Align = "right",
                    Show = AchievementCardShow.Unlocked
                },
                new AchievementCardElement
                {
                    Id = "title", Type = AchievementCardElementType.Title, X = 276, Y = 101, W = 868, H = 112,
                    Color = "text", FontSize = 50, LineHeight = 1.12f, MaxLines = 2
                },
                new AchievementCardElement
                {
                    Id = "description", Type = AchievementCardElementType.Description, X = 276, Y = 161, W = 868,
                    H = 95, Color = "muted", FontSize = 25, Bold = false, LineHeight = 1.52f, MaxLines = 2,
                    FollowId = "title"
                },
                new AchievementCardElement
                {
                    Id = "progress", Type = AchievementCardElementType.Progress, X = 276, Y = 270, W = 868, H = 10,
                    Fill = "primary@15", Color = "primary", Color2 = "muted", FontSize = 21,
                    Show = AchievementCardShow.Locked
                },
                new AchievementCardElement
                {
                    Id = "avatar", Type = AchievementCardElementType.Avatar, X = 70, Y = 316, W = 44, H = 44,
                    Radius = 22, Fill = "primary@20", Stroke = "primary@50", StrokeWidth = 2
                },
                new AchievementCardElement
                {
                    Id = "member", Type = AchievementCardElementType.Member, X = 128, Y = 324, W = 560, H = 28,
                    Color = "text", Color2 = "muted", FontSize = 21
                },
                new AchievementCardElement
                {
                    Id = "points", Type = AchievementCardElementType.Points, X = 730, Y = 318, W = 400, H = 40,
                    Radius = 18, Fill = "primary@20", Color = "primary", FontSize = 21, Align = "right"
                },
                new AchievementCardElement
                {
                    Id = "category", Type = AchievementCardElementType.Category, X = 520, Y = 318, W = 400, H = 40,
                    Radius = 18, Fill = "primary@20", Stroke = "primary@30", StrokeWidth = 2, Color = "text",
                    Color2 = "primary", Color3 = "secondary", FontSize = 21, Align = "right", BesideId = "points",
                    Gap = 10
                }
            ]
        };
    }
}

/// <summary>
///     A named card design a server saved.
/// </summary>
/// <param name="Id">The design ID.</param>
/// <param name="Name">Its name.</param>
/// <param name="Template">The design.</param>
/// <param name="DateUpdated">When it was last saved.</param>
public sealed record AchievementCardDesignInfo(int Id, string Name, AchievementCardTemplate Template, DateTime DateUpdated);

/// <summary>
///     Which saved card design categories and achievements use instead of the server's default. An achievement's own
///     pick wins over its category's.
/// </summary>
public sealed class AchievementCardAssignments
{
    /// <summary>
    ///     Design ID by category key.
    /// </summary>
    public Dictionary<string, int> Categories { get; set; } = [];

    /// <summary>
    ///     Design ID by achievement key.
    /// </summary>
    public Dictionary<string, int> Achievements { get; set; } = [];

    /// <summary>
    ///     Reads stored assignments, or none when there are none or they can't be read.
    /// </summary>
    /// <param name="json">The stored JSON.</param>
    /// <returns>The assignments.</returns>
    public static AchievementCardAssignments Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new AchievementCardAssignments();
        try
        {
            var parsed = JsonSerializer.Deserialize<AchievementCardAssignments>(json, AchievementCardTemplate.Json);
            return new AchievementCardAssignments
            {
                Categories = parsed?.Categories ?? [],
                Achievements = parsed?.Achievements ?? []
            };
        }
        catch (JsonException)
        {
            return new AchievementCardAssignments();
        }
    }

    /// <summary>
    ///     The assignments as stored.
    /// </summary>
    /// <returns>The JSON, or empty when there are none.</returns>
    public string Serialize()
    {
        return Categories.Count == 0 && Achievements.Count == 0
            ? ""
            : JsonSerializer.Serialize(this, AchievementCardTemplate.Json);
    }
}

/// <summary>
///     What fills a card behind its elements.
/// </summary>
public sealed class AchievementCardBackground
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
    ///     The image, as an https URL or <c>upload:id</c>.
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
///     The kinds of element. Built in kinds show the achievement and can be hidden but not repeated or removed.
/// </summary>
public static class AchievementCardElementType
{
    /// <summary>The achievement's icon on a tile.</summary>
    public const string Icon = "icon";

    /// <summary>The grade badge.</summary>
    public const string Grade = "grade";

    /// <summary>The state line, such as "Achievement unlocked".</summary>
    public const string Label = "label";

    /// <summary>The achievement's name.</summary>
    public const string Title = "title";

    /// <summary>The achievement's description.</summary>
    public const string Description = "description";

    /// <summary>The progress bar of a locked achievement.</summary>
    public const string Progress = "progress";

    /// <summary>The member's avatar.</summary>
    public const string Avatar = "avatar";

    /// <summary>The member's name and the server.</summary>
    public const string Member = "member";

    /// <summary>The category badge.</summary>
    public const string Category = "category";

    /// <summary>The points badge.</summary>
    public const string Points = "points";

    /// <summary>The badge counting other unlocks at the same time.</summary>
    public const string More = "more";

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
    ///     Every built in kind, in default order.
    /// </summary>
    public static readonly IReadOnlyList<string> BuiltIn =
        [Icon, Grade, Label, Title, Description, Progress, Avatar, Member, Category, Points, More];

    /// <summary>
    ///     Every kind a server can add any number of.
    /// </summary>
    public static readonly IReadOnlyList<string> Custom = [Rectangle, Ellipse, Text, Image, Glyph];

    /// <summary>
    ///     Whether a kind is built in.
    /// </summary>
    /// <param name="type">The kind.</param>
    /// <returns>True for built in kinds.</returns>
    public static bool IsBuiltIn(string type)
    {
        return BuiltIn.Contains(type);
    }
}

/// <summary>
///     When an element is drawn.
/// </summary>
public static class AchievementCardShow
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
public sealed class AchievementCardElement
{
    /// <summary>Unique within the card: lowercase letters, digits, and dashes.</summary>
    public string Id { get; set; } = "";

    /// <summary>The kind, from <see cref="AchievementCardElementType" />.</summary>
    public string Type { get; set; } = AchievementCardElementType.Rectangle;

    /// <summary>The name shown in the layer list.</summary>
    public string Name { get; set; } = "";

    /// <summary>Whether it is drawn at all.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>When it is drawn, from <see cref="AchievementCardShow" />.</summary>
    public string Show { get; set; } = AchievementCardShow.Always;

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

    /// <summary>Text with placeholders, for text elements.</summary>
    public string Text { get; set; } = "";

    /// <summary>
    ///     A Font Awesome glyph name: the glyph of glyph elements, or one drawn before text. <c>none</c> hides the
    ///     label's state glyph.
    /// </summary>
    public string Glyph { get; set; } = "";

    /// <summary>An image as an https URL or <c>upload:id</c>, for image elements.</summary>
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
}

/// <summary>
///     Validation and color resolution for card templates.
/// </summary>
public static partial class AchievementCardRules
{
    /// <summary>
    ///     The palette tokens a color can name, besides <c>grade</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> Tokens = ["primary", "secondary", "accent", "text", "muted", "grade"];

    /// <summary>
    ///     The placeholders text elements can use.
    /// </summary>
    public static readonly IReadOnlyList<string> Placeholders =
    [
        "{achievement.name}", "{achievement.description}", "{achievement.grade}", "{achievement.points}",
        "{achievement.category}", "{user.name}", "{server.name}", "{label}", "{progress}", "{progress.percent}",
        "{more}"
    ];

    [GeneratedRegex("^[a-z0-9-]{1,40}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^(#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?|(primary|secondary|accent|text|muted|grade)(@[0-9a-fA-F]{2})?)$")]
    private static partial Regex ColorPattern();

    /// <summary>
    ///     Whether a string is a valid color token: <c>#rrggbb</c>, <c>#rrggbbaa</c>, or a palette name with an
    ///     optional dashboard style hex alpha, such as <c>primary@30</c>.
    /// </summary>
    /// <param name="value">The token.</param>
    /// <returns>True when valid.</returns>
    public static bool IsColor(string? value)
    {
        return value is not null && ColorPattern().IsMatch(value);
    }

    /// <summary>
    ///     Resolves a color token.
    /// </summary>
    /// <param name="value">The token, or empty for none.</param>
    /// <param name="palette">The server's palette.</param>
    /// <param name="grade">The achievement's grade color, already muted for locked achievements.</param>
    /// <returns>The color, or null for none or an invalid token.</returns>
    public static SKColor? Resolve(string? value, DashboardPalette palette, SKColor grade)
    {
        if (string.IsNullOrEmpty(value) || !IsColor(value))
            return null;
        if (value.StartsWith('#'))
        {
            var rgb = DashboardColorStore.ToColor(value[..7]);
            return value.Length == 9 ? rgb.WithAlpha(byte.Parse(value[7..], NumberStyles.HexNumber)) : rgb;
        }

        var at = value.IndexOf('@');
        var name = at < 0 ? value : value[..at];
        SKColor color = name switch
        {
            "primary" => palette.PrimaryColor,
            "secondary" => palette.SecondaryColor,
            "accent" => palette.AccentColor,
            "text" => palette.TextColor,
            "muted" => palette.MutedColor,
            _ => grade
        };
        return at < 0 ? color : color.WithAlpha(byte.Parse(value[(at + 1)..], NumberStyles.HexNumber));
    }

    /// <summary>
    ///     Brings a template within limits: numbers clamped, unknown values reset, ids made unique, links checked,
    ///     and every built in element present exactly once.
    /// </summary>
    /// <param name="template">The template as given.</param>
    /// <returns>The same template, corrected.</returns>
    public static AchievementCardTemplate Sanitize(AchievementCardTemplate template)
    {
        var defaults = AchievementCardTemplate.Default();
        template.Width = Math.Clamp(template.Width, AchievementCardTemplate.MinWidth, AchievementCardTemplate.MaxWidth);
        template.Height = Math.Clamp(template.Height, AchievementCardTemplate.MinHeight,
            AchievementCardTemplate.MaxHeight);
        template.Radius = Clamp(template.Radius, 0, Math.Min(template.Width, template.Height) / 2f);
        template.BorderColor = ColorOr(template.BorderColor, "");
        template.BorderWidth = Clamp(template.BorderWidth, 0, 20);

        var bg = template.Background ??= new AchievementCardBackground();
        bg.Kind = bg.Kind is "palette" or "solid" or "gradient" or "image" ? bg.Kind : "palette";
        bg.Color = ColorOr(bg.Color, "#1a202c");
        bg.Color2 = ColorOr(bg.Color2, "primary@40");
        bg.Angle = Clamp(bg.Angle, -360, 360);
        bg.Url = LinkOr(bg.Url);
        bg.Fit = bg.Fit is "contain" ? "contain" : "cover";
        bg.Dim = Clamp(bg.Dim, 0, 1);

        var seenIds = new HashSet<string>();
        var seenBuiltIns = new HashSet<string>();
        var elements = new List<AchievementCardElement>();
        foreach (var element in template.Elements ?? [])
        {
            if (element is null || elements.Count >= AchievementCardTemplate.MaxElements)
                continue;
            var type = element.Type ?? "";
            var builtIn = AchievementCardElementType.IsBuiltIn(type);
            if (!builtIn && !AchievementCardElementType.Custom.Contains(type))
                continue;
            if (builtIn && !seenBuiltIns.Add(type))
                continue;

            var id = builtIn ? type : element.Id ?? "";
            if (!IdPattern().IsMatch(id) || AchievementCardElementType.IsBuiltIn(id) && !builtIn || !seenIds.Add(id))
            {
                var n = 1;
                while (!seenIds.Add($"{type}-{n}"))
                    n++;
                id = $"{type}-{n}";
            }

            element.Id = id;
            element.Type = type;
            Clean(element, template);
            elements.Add(element);
        }

        foreach (var missing in defaults.Elements.Where(d =>
                     AchievementCardElementType.IsBuiltIn(d.Type) && !seenBuiltIns.Contains(d.Type)))
        {
            missing.Visible = false;
            elements.Add(missing);
            seenIds.Add(missing.Id);
        }

        foreach (var element in elements)
        {
            if (element.FollowId == element.Id || !seenIds.Contains(element.FollowId))
                element.FollowId = "";
            if (element.BesideId == element.Id || !seenIds.Contains(element.BesideId))
                element.BesideId = "";
        }

        template.Elements = elements;
        return template;
    }

    /// <summary>
    ///     Every <c>upload:id</c> a template links to.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <returns>The upload ids.</returns>
    public static IEnumerable<int> Uploads(AchievementCardTemplate template)
    {
        return template.Elements.Select(e => e.Url).Append(template.Background.Url)
            .Select(u => AchievementIcons.UploadId(AchievementIcons.Parse(u)))
            .OfType<int>();
    }

    private static void Clean(AchievementCardElement e, AchievementCardTemplate template)
    {
        e.Name = (e.Name ?? "").Trim();
        if (e.Name.Length > 40)
            e.Name = e.Name[..40];
        e.Show = e.Show is AchievementCardShow.Unlocked or AchievementCardShow.Locked ? e.Show : AchievementCardShow.Always;
        e.W = Clamp(e.W, 1, template.Width * 2);
        e.H = Clamp(e.H, 1, template.Height * 2);
        e.X = Clamp(e.X, -e.W, template.Width * 2);
        e.Y = Clamp(e.Y, -e.H, template.Height * 2);
        e.Rotation = Clamp(e.Rotation, -360, 360);
        e.Opacity = Clamp(e.Opacity, 0, 1);
        e.Fill = ColorOr(e.Fill, "");
        e.Fill2 = ColorOr(e.Fill2, "");
        e.FillAngle = Clamp(e.FillAngle, -360, 360);
        e.Stroke = ColorOr(e.Stroke, "");
        e.StrokeWidth = Clamp(e.StrokeWidth, 0, 40);
        e.Radius = Clamp(e.Radius, 0, 1000);
        e.ShadowColor = ColorOr(e.ShadowColor, "");
        e.ShadowBlur = Clamp(e.ShadowBlur, 0, 60);
        e.ShadowX = Clamp(e.ShadowX, -100, 100);
        e.ShadowY = Clamp(e.ShadowY, -100, 100);
        e.Color = ColorOr(e.Color, "text");
        e.Color2 = ColorOr(e.Color2, "muted");
        e.Color3 = ColorOr(e.Color3, "");
        e.FontSize = Clamp(e.FontSize, 8, 200);
        e.Align = e.Align is "center" or "right" ? e.Align : "left";
        e.Spacing = Clamp(e.Spacing, -5, 40);
        e.LineHeight = Clamp(e.LineHeight, 0.8f, 3);
        e.MaxLines = Math.Clamp(e.MaxLines, 1, 6);
        e.Text = e.Text ?? "";
        if (e.Text.Length > AchievementCardTemplate.MaxText)
            e.Text = e.Text[..AchievementCardTemplate.MaxText];
        e.Glyph = (e.Glyph ?? "").Trim().ToLowerInvariant();
        if (e.Glyph != "none" && e.Glyph.Length > 0 && AchievementIcons.FindGlyph(e.Glyph) is null)
            e.Glyph = "";
        e.Url = LinkOr(e.Url);
        e.Fit = e.Fit is "contain" ? "contain" : "cover";
        e.FollowId ??= "";
        e.BesideId ??= "";
        e.Gap = Clamp(e.Gap, -200, 200);
    }

    private static string ColorOr(string? value, string fallback)
    {
        return !string.IsNullOrEmpty(value) && IsColor(value) ? value : fallback;
    }

    private static string LinkOr(string? value)
    {
        value = (value ?? "").Trim();
        if (value.Length == 0 || value.Length > AchievementIcons.MaxUrlLength)
            return "";
        if (value.StartsWith(AchievementIcons.UploadPrefix, StringComparison.Ordinal))
            return int.TryParse(value[AchievementIcons.UploadPrefix.Length..], out var id) && id > 0 ? value : "";
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? value : "";
    }

    private static float Clamp(float value, float min, float max)
    {
        return float.IsFinite(value) ? Math.Clamp(value, min, max) : min;
    }
}
