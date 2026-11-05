using System.Globalization;
using System.Text.RegularExpressions;
using Mewdeko.Common.Palette;
using Mewdeko.Modules.Achievements.Common;
using SkiaSharp;

namespace Mewdeko.Common.Cards;

/// <summary>
///     Validation and color resolution for card templates.
/// </summary>
public static partial class CardRules
{
    /// <summary>
    ///     The palette tokens a color can name, besides <c>grade</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> Tokens = ["primary", "secondary", "accent", "text", "muted", "grade"];

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
    /// <param name="grade">The color the <c>grade</c> token stands for.</param>
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
    /// <param name="profile">What the feature allows on its cards.</param>
    /// <returns>The same template, corrected.</returns>
    public static CardTemplate Sanitize(CardTemplate template, CardProfile profile)
    {
        template.Width = Math.Clamp(template.Width, CardTemplate.MinWidth, CardTemplate.MaxWidth);
        template.Height = Math.Clamp(template.Height, CardTemplate.MinHeight, CardTemplate.MaxHeight);
        template.Radius = Clamp(template.Radius, 0, Math.Min(template.Width, template.Height) / 2f);
        template.BorderColor = ColorOr(template.BorderColor, "");
        template.BorderWidth = Clamp(template.BorderWidth, 0, 20);

        var bg = template.Background ??= new CardBackground();
        bg.Kind = bg.Kind is "palette" or "solid" or "gradient" or "image" ? bg.Kind : "palette";
        bg.Color = ColorOr(bg.Color, "#1a202c");
        bg.Color2 = ColorOr(bg.Color2, "primary@40");
        bg.Angle = Clamp(bg.Angle, -360, 360);
        bg.Url = LinkOr(bg.Url);
        bg.Fit = bg.Fit is "contain" ? "contain" : "cover";
        bg.Dim = Clamp(bg.Dim, 0, 1);

        var seenIds = new HashSet<string>();
        var seenBuiltIns = new HashSet<string>();
        var elements = new List<CardElement>();
        foreach (var element in template.Elements ?? [])
        {
            if (element is null || elements.Count >= CardTemplate.MaxElements)
                continue;
            var type = element.Type ?? "";
            var builtIn = profile.IsBuiltIn(type);
            if (!builtIn && !profile.Custom.Contains(type))
                continue;
            if (builtIn && !seenBuiltIns.Add(type))
                continue;

            var id = builtIn ? type : element.Id ?? "";
            if (!IdPattern().IsMatch(id) || profile.IsBuiltIn(id) && !builtIn || !seenIds.Add(id))
            {
                var n = 1;
                while (!seenIds.Add($"{type}-{n}"))
                    n++;
                id = $"{type}-{n}";
            }

            element.Id = id;
            element.Type = type;
            Clean(element, template, profile);
            elements.Add(element);
        }

        if (profile.BuiltIn.Count > 0)
        {
            foreach (var missing in profile.Default().Elements.Where(d =>
                         profile.IsBuiltIn(d.Type) && !seenBuiltIns.Contains(d.Type)))
            {
                missing.Visible = false;
                elements.Add(missing);
                seenIds.Add(missing.Id);
            }
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
    public static IEnumerable<int> Uploads(CardTemplate template)
    {
        return template.Elements.Select(e => e.Url).Append(template.Background.Url)
            .Select(u => AchievementIcons.UploadId(AchievementIcons.Parse(u)))
            .OfType<int>();
    }

    private static void Clean(CardElement e, CardTemplate template, CardProfile profile)
    {
        e.Name = (e.Name ?? "").Trim();
        if (e.Name.Length > 40)
            e.Name = e.Name[..40];
        e.Show = profile.AllowShow && e.Show is CardShow.Unlocked or CardShow.Locked ? e.Show : CardShow.Always;
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
        e.Font = e.Font is CardRenderer.UniSans ? e.Font : "";
        e.Align = e.Align is "center" or "right" ? e.Align : "left";
        e.Spacing = Clamp(e.Spacing, -5, 40);
        e.LineHeight = Clamp(e.LineHeight, 0.8f, 3);
        e.MaxLines = Math.Clamp(e.MaxLines, 1, 6);
        e.Text = Percent(e.Text ?? "", profile);
        if (e.Text.Length > CardTemplate.MaxText)
            e.Text = e.Text[..CardTemplate.MaxText];
        e.LockedText = Percent(e.LockedText ?? "", profile);
        if (e.LockedText.Length > CardTemplate.MaxText)
            e.LockedText = e.LockedText[..CardTemplate.MaxText];
        e.Glyph = (e.Glyph ?? "").Trim().ToLowerInvariant();
        if (e.Glyph != "none" && e.Glyph.Length > 0 && AchievementIcons.FindGlyph(e.Glyph) is null)
            e.Glyph = "";
        e.Url = LinkOr(e.Url);
        e.Fit = e.Fit is "contain" ? "contain" : "cover";
        e.FollowId ??= "";
        e.BesideId ??= "";
        e.Gap = Clamp(e.Gap, -200, 200);
    }

    private static string Percent(string text, CardProfile profile)
    {
        if (!text.Contains('{'))
            return text;
        foreach (var placeholder in profile.Placeholders)
            text = text.Replace($"{{{placeholder[1..^1]}}}", placeholder);
        return text;
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
        if (value.StartsWith(CardPresets.Prefix, StringComparison.OrdinalIgnoreCase))
            return CardPresets.TryParse(value, out var preset) ? CardPresets.Reference(preset) : "";
        if (value.StartsWith(AchievementIcons.UploadPrefix, StringComparison.Ordinal))
            return int.TryParse(value[AchievementIcons.UploadPrefix.Length..], out var id) && id > 0 ? value : "";
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? value : "";
    }

    private static float Clamp(float value, float min, float max)
    {
        return float.IsFinite(value) ? Math.Clamp(value, min, max) : min;
    }
}
