using System.Text.Json;
using Mewdeko.Common.Cards;

namespace Mewdeko.Modules.Achievements.Common;

/// <summary>
///     What achievement cards allow on the shared card engine: the built in elements that show the achievement, the
///     placeholders, and the built in design.
/// </summary>
public static class AchievementCards
{
    /// <summary>
    ///     Every built in kind, in default order.
    /// </summary>
    public static readonly IReadOnlyList<string> BuiltIn =
    [
        CardElementType.Icon, CardElementType.Grade, CardElementType.Label, CardElementType.Title,
        CardElementType.Description, CardElementType.Progress, CardElementType.Avatar, CardElementType.Member,
        CardElementType.Category, CardElementType.Points, CardElementType.More
    ];

    /// <summary>
    ///     Every kind a server can add any number of.
    /// </summary>
    public static readonly IReadOnlyList<string> Custom =
    [
        CardElementType.Rectangle, CardElementType.Ellipse, CardElementType.Text, CardElementType.Image,
        CardElementType.Glyph
    ];

    /// <summary>
    ///     The placeholders text elements can use.
    /// </summary>
    public static readonly IReadOnlyList<string> Placeholders =
    [
        "%achievement.name%", "%achievement.description%", "%achievement.grade%", "%achievement.points%",
        "%achievement.category%", "%user.name%", "%server.name%", "%label%", "%points%", "%progress%",
        "%progress.percent%", "%more%"
    ];

    /// <summary>
    ///     What achievement cards allow on the card engine.
    /// </summary>
    public static readonly CardProfile Profile = new()
    {
        BuiltIn = BuiltIn,
        Custom = Custom,
        Placeholders = Placeholders,
        Default = Default,
        AllowShow = true
    };

    /// <summary>
    ///     Reads a stored achievement card design, or the built in design when there is none or it can't be read.
    /// </summary>
    /// <param name="json">The stored JSON.</param>
    /// <returns>The design.</returns>
    public static CardTemplate Parse(string? json)
    {
        return CardTemplate.Parse(json, Profile);
    }

    /// <summary>
    ///     Brings an achievement card design within limits.
    /// </summary>
    /// <param name="template">The design as given.</param>
    /// <returns>The same design, corrected.</returns>
    public static CardTemplate Sanitize(CardTemplate template)
    {
        return CardRules.Sanitize(template, Profile);
    }

    /// <summary>
    ///     The built in design: a dashboard card in the server's palette with the icon on a tile of the grade's color.
    /// </summary>
    /// <returns>A fresh copy of the default template.</returns>
    public static CardTemplate Default()
    {
        return new CardTemplate
        {
            Elements =
            [
                new CardElement
                {
                    Id = "footer", Type = CardElementType.Rectangle, Name = "Member row",
                    X = 56, Y = 304, W = 1088, H = 68, Radius = 26, Fill = "primary@08"
                },
                new CardElement
                {
                    Id = "icon", Type = CardElementType.Icon, X = 56, Y = 56, W = 176, H = 176,
                    Radius = 48, Fill = "grade@20", Stroke = "grade@50", StrokeWidth = 2, Color = "grade"
                },
                new CardElement
                {
                    Id = "grade", Type = CardElementType.Grade, X = 56, Y = 248, W = 176, H = 44,
                    Radius = 22, Fill = "grade@20", Stroke = "grade@40", StrokeWidth = 2, Color = "grade",
                    Color2 = "grade", FontSize = 21, Align = "center", AutoWidth = false
                },
                new CardElement
                {
                    Id = "label", Type = CardElementType.Label, X = 276, Y = 62, W = 600, H = 28,
                    Color = "muted", Color2 = "primary", Color3 = "secondary", FontSize = 20, Uppercase = true,
                    Spacing = 1.6f
                },
                new CardElement
                {
                    Id = "more", Type = CardElementType.More, X = 744, Y = 56, W = 400, H = 40,
                    Radius = 18, Fill = "primary@20", Color = "primary", FontSize = 21, Align = "right",
                    Show = CardShow.Unlocked
                },
                new CardElement
                {
                    Id = "title", Type = CardElementType.Title, X = 276, Y = 101, W = 868, H = 112,
                    Color = "text", FontSize = 50, LineHeight = 1.12f, MaxLines = 2
                },
                new CardElement
                {
                    Id = "description", Type = CardElementType.Description, X = 276, Y = 161, W = 868,
                    H = 95, Color = "muted", FontSize = 25, Bold = false, LineHeight = 1.52f, MaxLines = 2,
                    FollowId = "title"
                },
                new CardElement
                {
                    Id = "progress", Type = CardElementType.Progress, X = 276, Y = 270, W = 868, H = 10,
                    Fill = "primary@15", Color = "primary", Color2 = "muted", FontSize = 21,
                    Show = CardShow.Locked
                },
                new CardElement
                {
                    Id = "avatar", Type = CardElementType.Avatar, X = 70, Y = 316, W = 44, H = 44,
                    Radius = 22, Fill = "primary@20", Stroke = "primary@50", StrokeWidth = 2
                },
                new CardElement
                {
                    Id = "member", Type = CardElementType.Member, X = 128, Y = 324, W = 560, H = 28,
                    Color = "text", Color2 = "muted", FontSize = 21
                },
                new CardElement
                {
                    Id = "points", Type = CardElementType.Points, X = 730, Y = 318, W = 400, H = 40,
                    Radius = 18, Fill = "primary@20", Color = "primary", FontSize = 21, Align = "right"
                },
                new CardElement
                {
                    Id = "category", Type = CardElementType.Category, X = 520, Y = 318, W = 400, H = 40,
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
public sealed record AchievementCardDesignInfo(int Id, string Name, CardTemplate Template, DateTime DateUpdated);

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
            var parsed = JsonSerializer.Deserialize<AchievementCardAssignments>(json, CardTemplate.Json);
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
            : JsonSerializer.Serialize(this, CardTemplate.Json);
    }
}
