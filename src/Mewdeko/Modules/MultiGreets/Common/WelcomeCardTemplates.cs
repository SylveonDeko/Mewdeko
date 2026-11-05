using DataModel;
using Mewdeko.Common.Cards;
using SkiaSharp;

namespace Mewdeko.Modules.MultiGreets.Common;

/// <summary>
///     Why a change to a welcome card was refused.
/// </summary>
public enum WelcomeCardError
{
    /// <summary>
    ///     Nothing is wrong.
    /// </summary>
    None,

    /// <summary>
    ///     A colour is not a hex colour.
    /// </summary>
    InvalidColor,

    /// <summary>
    ///     The overlay opacity is outside 0 to 100.
    /// </summary>
    InvalidOpacity,

    /// <summary>
    ///     The font is not one of the bundled fonts.
    /// </summary>
    InvalidFont,

    /// <summary>
    ///     The title or subtitle is empty or too long.
    /// </summary>
    InvalidText,

    /// <summary>
    ///     The card's design has no element the change applies to.
    /// </summary>
    MissingElement,

    /// <summary>
    ///     The image could not be read.
    /// </summary>
    InvalidImage,

    /// <summary>
    ///     The guild keeps as many images as it can.
    /// </summary>
    TooManyImages,

    /// <summary>
    ///     There is no starter layout with that name.
    /// </summary>
    UnknownStarter
}

/// <summary>
///     A ready made welcome card layout a server can start from.
/// </summary>
/// <param name="Id">Its short name.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Build">Makes a fresh copy of the layout.</param>
public sealed record WelcomeCardStarter(string Id, string Name, Func<CardTemplate> Build);

/// <summary>
///     Basic changes to a welcome card that the text and slash commands make. Anything left null stays as it is.
/// </summary>
public sealed class WelcomeCardBasics
{
    /// <summary>
    ///     The background colour as hex.
    /// </summary>
    public string? BackgroundColor { get; init; }

    /// <summary>
    ///     The colour of every line of text as hex.
    /// </summary>
    public string? TextColor { get; init; }

    /// <summary>
    ///     How dark the overlay on the background is, from 0 to 100.
    /// </summary>
    public int? OverlayOpacity { get; init; }

    /// <summary>
    ///     The first line of text.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    ///     The second line of text, or empty for none.
    /// </summary>
    public string? Subtitle { get; init; }

    /// <summary>
    ///     The font: noto-bold, noto or unisans.
    /// </summary>
    public string? FontName { get; init; }

    /// <summary>
    ///     The shape the avatar is cut to.
    /// </summary>
    public WelcomeCardAvatarShape? AvatarShape { get; init; }

    /// <summary>
    ///     Checks the values that are set.
    /// </summary>
    /// <returns>The first problem, or none.</returns>
    public WelcomeCardError Validate()
    {
        if (BackgroundColor is not null && !SKColor.TryParse(BackgroundColor, out _) ||
            TextColor is not null && !SKColor.TryParse(TextColor, out _))
            return WelcomeCardError.InvalidColor;
        if (OverlayOpacity is < 0 or > 100)
            return WelcomeCardError.InvalidOpacity;
        if (FontName is not null && !WelcomeCardTemplates.Fonts.ContainsKey(FontName))
            return WelcomeCardError.InvalidFont;
        if (Title is not null && (string.IsNullOrWhiteSpace(Title) || Title.Length > 100) ||
            Subtitle is { Length: > 150 })
            return WelcomeCardError.InvalidText;
        return WelcomeCardError.None;
    }
}

/// <summary>
///     What welcome cards allow on the shared card engine, the layouts to start from, and the conversion of cards that
///     were set up before designs existed.
/// </summary>
public static class WelcomeCardTemplates
{
    /// <summary>
    ///     The id of the layout that reproduces the original fixed card.
    /// </summary>
    public const string ClassicId = "classic";

    /// <summary>
    ///     The bundled fonts the commands offer, by name.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Fonts = new Dictionary<string, string>
    {
        ["noto-bold"] = "data/fonts/NotoSans-Bold.ttf",
        ["noto"] = "data/fonts/NotoSans-Regular.ttf",
        ["unisans"] = "data/fonts/Uni Sans.ttf"
    };

    /// <summary>
    ///     The placeholders text elements can use.
    /// </summary>
    public static readonly IReadOnlyList<string> Placeholders =
    [
        "%user.name%", "%user.fullname%", "%user.id%", "%user.discrim%", "%server.name%", "%server.members%",
        "%server.id%", "%server.boostcount%", "%server.boostlevel%", "%bot.name%"
    ];

    /// <summary>
    ///     Every kind of element a welcome card can hold any number of.
    /// </summary>
    public static readonly IReadOnlyList<string> Custom =
    [
        CardElementType.Avatar, CardElementType.Username, CardElementType.ServerName, CardElementType.ServerIcon,
        CardElementType.MemberCount, CardElementType.Text, CardElementType.Image, CardElementType.Rectangle,
        CardElementType.Ellipse, CardElementType.Glyph
    ];

    /// <summary>
    ///     What welcome cards allow on the card engine.
    /// </summary>
    public static readonly CardProfile Profile = new()
    {
        BuiltIn = [],
        Custom = Custom,
        Placeholders = Placeholders,
        Default = Classic
    };

    /// <summary>
    ///     The layouts a server can start from, the original card first.
    /// </summary>
    public static readonly IReadOnlyList<WelcomeCardStarter> Starters =
    [
        new(ClassicId, "Classic", Classic),
        new("centered", "Centered", Centered),
        new("banner", "Banner", Banner),
        new("minimal", "Minimal", Minimal)
    ];

    /// <summary>
    ///     Reads a stored design, or the classic layout when there is none or it can't be read.
    /// </summary>
    /// <param name="json">The stored JSON.</param>
    /// <returns>The design.</returns>
    public static CardTemplate Parse(string? json)
    {
        return CardTemplate.Parse(json, Profile);
    }

    /// <summary>
    ///     Brings a design within limits.
    /// </summary>
    /// <param name="template">The design as given.</param>
    /// <returns>The same design, corrected.</returns>
    public static CardTemplate Sanitize(CardTemplate template)
    {
        return CardRules.Sanitize(template, Profile);
    }

    /// <summary>
    ///     The classic layout: the original fixed card with its original colours and text.
    /// </summary>
    /// <returns>A fresh copy.</returns>
    public static CardTemplate Classic()
    {
        return FromLegacy(new WelcomeCardSetting(), null);
    }

    /// <summary>
    ///     A design that draws the card exactly as the fixed layout did from a card's original columns.
    /// </summary>
    /// <param name="setting">The card as stored before designs existed.</param>
    /// <param name="backgroundUpload">The stored image the card's background picture was moved to, if it has one.</param>
    /// <returns>The design.</returns>
    public static CardTemplate FromLegacy(WelcomeCardSetting setting, int? backgroundUpload)
    {
        var text = ToToken(setting.TextColor, "#ffffff");
        var bold = setting.FontName != "noto";
        var font = setting.FontName == CardRenderer.UniSans ? CardRenderer.UniSans : "";
        var radius = setting.AvatarShape switch
        {
            WelcomeCardAvatarShape.Circle => 110f,
            WelcomeCardAvatarShape.Rounded => 40f,
            _ => 0f
        };
        var overlay = Math.Clamp(setting.OverlayOpacity, 0, 100) * 255 / 100;

        return new CardTemplate
        {
            Width = 1000,
            Height = 360,
            Radius = 0,
            BorderWidth = 0,
            Shadow = false,
            Background = backgroundUpload is { } id
                ? new CardBackground
                {
                    Kind = "image", Url = $"upload:{id}", Fit = "cover", Dim = 0,
                    Color = ToToken(setting.BackgroundColor, "#23272a")
                }
                : new CardBackground
                {
                    Kind = "solid", Color = ToToken(setting.BackgroundColor, "#23272a")
                },
            Elements =
            [
                new CardElement
                {
                    Id = "overlay", Type = CardElementType.Rectangle, Name = "Overlay", X = 0, Y = 0, W = 1000,
                    H = 360, Fill = $"#000000{overlay:x2}"
                },
                new CardElement
                {
                    Id = "avatar", Type = CardElementType.Avatar, X = 60, Y = 70, W = 220, H = 220, Radius = radius,
                    Fill = "#7289da"
                },
                new CardElement
                {
                    Id = "title", Type = CardElementType.Text, Name = "Title", X = 310, Y = 110, W = 650, H = 80,
                    FontSize = 64, Shrink = true, Bold = bold, Font = font, Color = text, Text = setting.Title,
                    ShadowColor = "#00000078", ShadowX = 2, ShadowY = 2
                },
                new CardElement
                {
                    Id = "subtitle", Type = CardElementType.Text, Name = "Subtitle", X = 310, Y = 200, W = 650,
                    H = 43, FontSize = 34, Shrink = true, Bold = bold, Font = font, Color = text,
                    Text = setting.Subtitle,
                    ShadowColor = "#00000078", ShadowX = 2, ShadowY = 2
                }
            ]
        };
    }

    /// <summary>
    ///     Makes the changes the commands offer to a design.
    /// </summary>
    /// <param name="template">The design, changed in place.</param>
    /// <param name="basics">The changes.</param>
    /// <returns>The error, or none.</returns>
    public static WelcomeCardError Apply(CardTemplate template, WelcomeCardBasics basics)
    {
        var error = basics.Validate();
        if (error != WelcomeCardError.None)
            return error;

        var texts = template.Elements.Where(e => CardElementType.TextKinds.Contains(e.Type)).ToList();
        var title = texts.FirstOrDefault(e => e.Id == "title") ?? texts.FirstOrDefault();
        var subtitle = texts.FirstOrDefault(e => e.Id == "subtitle") ??
                       texts.FirstOrDefault(e => e != title);
        var avatar = template.Elements.FirstOrDefault(e => e.Id == "avatar") ??
                     template.Elements.FirstOrDefault(e => e.Type == CardElementType.Avatar);

        if (basics.Title is not null && title is null || basics.Subtitle is not null && subtitle is null ||
            basics.TextColor is not null && texts.Count == 0 || basics.FontName is not null && texts.Count == 0 ||
            basics.AvatarShape is not null && avatar is null)
            return WelcomeCardError.MissingElement;

        if (basics.BackgroundColor is { } background)
        {
            if (template.Background.Kind != "image")
                template.Background.Kind = "solid";
            template.Background.Color = ToToken(background, "#23272a");
        }

        if (basics.OverlayOpacity is { } opacity)
        {
            var fill = $"#000000{opacity * 255 / 100:x2}";
            var overlay = template.Elements.FirstOrDefault(e => e.Id == "overlay");
            if (overlay is null)
            {
                template.Elements.Insert(0, new CardElement
                {
                    Id = "overlay", Type = CardElementType.Rectangle, Name = "Overlay", X = 0, Y = 0,
                    W = template.Width, H = template.Height, Fill = fill
                });
            }
            else
            {
                overlay.Fill = fill;
            }
        }

        if (basics.TextColor is { } color)
        {
            foreach (var text in texts)
                text.Color = ToToken(color, "#ffffff");
        }

        if (basics.FontName is { } name)
        {
            foreach (var text in texts)
            {
                text.Bold = name != "noto";
                text.Font = name == CardRenderer.UniSans ? CardRenderer.UniSans : "";
            }
        }

        if (basics.Title is { } titleText)
            title!.Text = titleText;
        if (basics.Subtitle is { } subtitleText)
            subtitle!.Text = subtitleText;

        if (basics.AvatarShape is { } shape)
        {
            avatar!.Radius = shape switch
            {
                WelcomeCardAvatarShape.Circle => Math.Min(avatar.W, avatar.H) / 2,
                WelcomeCardAvatarShape.Rounded => Math.Min(40, Math.Min(avatar.W, avatar.H) / 2),
                _ => 0
            };
        }

        return WelcomeCardError.None;
    }

    /// <summary>
    ///     The basics of a design, as the commands show them.
    /// </summary>
    /// <param name="Background">The background colour, or the kind of background when it isn't a colour.</param>
    /// <param name="HasImage">Whether the background is a picture.</param>
    /// <param name="Overlay">How dark the overlay is, from 0 to 100.</param>
    /// <param name="Title">The first line of text, or empty.</param>
    /// <param name="Subtitle">The second line of text, or empty.</param>
    /// <param name="Font">The font of the first line of text.</param>
    /// <param name="TextColor">The colour of the first line of text.</param>
    /// <param name="Avatar">The shape of the avatar, or empty when the design has none.</param>
    public sealed record WelcomeCardSummary(string Background, bool HasImage, int Overlay, string Title,
        string Subtitle, string Font, string TextColor, string Avatar);

    /// <summary>
    ///     Reads the basics of a design for the commands to show.
    /// </summary>
    /// <param name="template">The design.</param>
    /// <returns>The basics.</returns>
    public static WelcomeCardSummary Summarize(CardTemplate template)
    {
        var texts = template.Elements.Where(e => CardElementType.TextKinds.Contains(e.Type)).ToList();
        var title = texts.FirstOrDefault(e => e.Id == "title") ?? texts.FirstOrDefault();
        var subtitle = texts.FirstOrDefault(e => e.Id == "subtitle") ?? texts.FirstOrDefault(e => e != title);
        var avatar = template.Elements.FirstOrDefault(e => e.Id == "avatar") ??
                     template.Elements.FirstOrDefault(e => e.Type == CardElementType.Avatar);
        var overlay = template.Elements.FirstOrDefault(e => e.Id == "overlay");
        var opacity = overlay is not null && overlay.Fill.Length == 9
            ? (int)Math.Round(Convert.ToInt32(overlay.Fill[7..], 16) * 100 / 255d)
            : 0;
        var background = template.Background;
        return new WelcomeCardSummary(
            background.Kind is "solid" or "image" ? background.Color : background.Kind,
            background.Kind == "image" && background.Url.Length > 0,
            opacity,
            title?.Text ?? "",
            subtitle?.Text ?? "",
            title is null ? "" : title.Font == CardRenderer.UniSans ? "unisans" : title.Bold ? "noto-bold" : "noto",
            title?.Color ?? "",
            avatar is null
                ? ""
                : avatar.Radius >= Math.Min(avatar.W, avatar.H) / 2 - 0.5f
                    ? nameof(WelcomeCardAvatarShape.Circle)
                    : avatar.Radius > 0
                        ? nameof(WelcomeCardAvatarShape.Rounded)
                        : nameof(WelcomeCardAvatarShape.Square));
    }

    /// <summary>
    ///     A colour as a card colour token: <c>#rrggbb</c>, with the alpha after it when the colour isn't opaque.
    /// </summary>
    /// <param name="hex">The colour as hex.</param>
    /// <param name="fallback">What to use when it isn't a colour.</param>
    /// <returns>The token.</returns>
    public static string ToToken(string? hex, string fallback)
    {
        if (hex is null || !SKColor.TryParse(hex, out var color))
            return fallback;
        return color.Alpha == 255
            ? $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}"
            : $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}{color.Alpha:x2}";
    }

    private static CardTemplate Centered()
    {
        return new CardTemplate
        {
            Width = 1000,
            Height = 360,
            Background = new CardBackground
            {
                Kind = "gradient", Color = "#111827", Color2 = "primary@66", Angle = 135
            },
            Elements =
            [
                new CardElement
                {
                    Id = "avatar", Type = CardElementType.Avatar, X = 425, Y = 36, W = 150, H = 150, Radius = 75,
                    Fill = "primary@20", Stroke = "primary", StrokeWidth = 4
                },
                new CardElement
                {
                    Id = "title", Type = CardElementType.Text, Name = "Title", X = 60, Y = 206, W = 880, H = 66,
                    FontSize = 52, Shrink = true, Align = "center", Text = "Welcome, %user.name%"
                },
                new CardElement
                {
                    Id = "subtitle", Type = CardElementType.Text, Name = "Subtitle", X = 60, Y = 276, W = 880,
                    H = 44, FontSize = 28, Bold = false, Align = "center", Color = "muted",
                    Text = "You are member #%server.members% of %server.name%"
                }
            ]
        };
    }

    private static CardTemplate Banner()
    {
        return new CardTemplate
        {
            Width = 1000,
            Height = 360,
            Background = new CardBackground
            {
                Kind = "gradient", Color = "#0f172a", Color2 = "secondary@55", Angle = 120
            },
            Elements =
            [
                new CardElement
                {
                    Id = "server-icon", Type = CardElementType.ServerIcon, X = 56, Y = 40, W = 96, H = 96,
                    Radius = 24, Fill = "primary@20", Stroke = "primary@50", StrokeWidth = 2
                },
                new CardElement
                {
                    Id = "server-name", Type = CardElementType.ServerName, X = 172, Y = 66, W = 460, H = 44,
                    FontSize = 30, Color = "muted", Uppercase = true, Spacing = 1.6f
                },
                new CardElement
                {
                    Id = "members", Type = CardElementType.Text, Name = "Member count", X = 640, Y = 66, W = 304,
                    H = 44, FontSize = 26, Align = "right", Color = "primary", Text = "Member #%server.members%"
                },
                new CardElement
                {
                    Id = "divider", Type = CardElementType.Rectangle, Name = "Divider", X = 56, Y = 160, W = 888,
                    H = 2, Fill = "primary@30"
                },
                new CardElement
                {
                    Id = "avatar", Type = CardElementType.Avatar, X = 56, Y = 196, W = 128, H = 128, Radius = 64,
                    Fill = "primary@20", Stroke = "primary@50", StrokeWidth = 2
                },
                new CardElement
                {
                    Id = "title", Type = CardElementType.Text, Name = "Title", X = 212, Y = 206, W = 732, H = 66,
                    FontSize = 52, Shrink = true, Text = "Welcome, %user.name%"
                },
                new CardElement
                {
                    Id = "subtitle", Type = CardElementType.Text, Name = "Subtitle", X = 212, Y = 276, W = 732,
                    H = 44, FontSize = 28, Bold = false, Color = "muted", Text = "Glad to have you in %server.name%"
                }
            ]
        };
    }

    private static CardTemplate Minimal()
    {
        return new CardTemplate
        {
            Width = 1000,
            Height = 360,
            Radius = 24,
            Background = new CardBackground
            {
                Kind = "solid", Color = "#18181b"
            },
            Elements =
            [
                new CardElement
                {
                    Id = "accent", Type = CardElementType.Rectangle, Name = "Accent bar", X = 0, Y = 0, W = 12,
                    H = 360, Fill = "primary"
                },
                new CardElement
                {
                    Id = "avatar", Type = CardElementType.Avatar, X = 70, Y = 100, W = 160, H = 160, Radius = 28,
                    Fill = "primary@20"
                },
                new CardElement
                {
                    Id = "username", Type = CardElementType.Username, X = 270, Y = 100, W = 670, H = 72,
                    FontSize = 56, Shrink = true
                },
                new CardElement
                {
                    Id = "joined", Type = CardElementType.Text, Name = "Joined", X = 270, Y = 180, W = 670, H = 44,
                    FontSize = 30, Bold = false, Color = "muted", Text = "just joined %server.name%"
                },
                new CardElement
                {
                    Id = "members", Type = CardElementType.MemberCount, X = 270, Y = 236, W = 670, H = 40,
                    FontSize = 24, Color = "primary", Text = "Member #%server.members%"
                }
            ]
        };
    }
}
