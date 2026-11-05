using System.IO;
using Mewdeko.Common.Cards;
using Mewdeko.Common.Palette;
using Mewdeko.Modules.Achievements.Common;
using SkiaSharp;

namespace Mewdeko.Modules.Achievements.Services;

/// <summary>
///     Draws an achievement card from a server's card template on the shared card engine, with colors resolved from
///     the server's dashboard palette and the achievement's grade.
/// </summary>
public sealed class AchievementUnlockRenderer(CardRenderer cards, AchievementIconService icons) : INService
{
    /// <summary>
    ///     Transparent space around the card.
    /// </summary>
    public const int Margin = CardRenderer.Margin;

    /// <summary>
    ///     Everything the card shows.
    /// </summary>
    public sealed class UnlockImageData
    {
        /// <summary>
        ///     The server the achievement belongs to.
        /// </summary>
        public required ulong GuildId { get; init; }

        /// <summary>
        ///     The server's dashboard palette.
        /// </summary>
        public required DashboardPalette Palette { get; init; }

        /// <summary>
        ///     The achievement.
        /// </summary>
        public required AchievementDefinition Definition { get; init; }

        /// <summary>
        ///     Its icon, already resolved to the category's when it has none.
        /// </summary>
        public required AchievementIconRef Icon { get; init; }

        /// <summary>
        ///     Its category's name.
        /// </summary>
        public required string CategoryName { get; init; }

        /// <summary>
        ///     Its category's icon.
        /// </summary>
        public required AchievementIconRef CategoryIcon { get; init; }

        /// <summary>
        ///     The state line, such as "Achievement unlocked".
        /// </summary>
        public required string Label { get; init; }

        /// <summary>
        ///     The points badge, such as "+50 points".
        /// </summary>
        public required string PointsLabel { get; init; }

        /// <summary>
        ///     Who earned it.
        /// </summary>
        public required string MemberName { get; init; }

        /// <summary>
        ///     Their avatar.
        /// </summary>
        public string? AvatarUrl { get; init; }

        /// <summary>
        ///     Where, shown after the member's name.
        /// </summary>
        public required string Scope { get; init; }

        /// <summary>
        ///     A badge for other unlocks at the same time, such as "+2 more", or null.
        /// </summary>
        public string? MoreLabel { get; init; }

        /// <summary>
        ///     Progress toward it from 0 to 1, or null for none.
        /// </summary>
        public float? Progress { get; init; }

        /// <summary>
        ///     Text beside the progress bar, such as "120 / 250".
        /// </summary>
        public string? ProgressLabel { get; init; }

        /// <summary>
        ///     Whether the achievement is not unlocked yet.
        /// </summary>
        public bool Locked { get; init; }
    }

    /// <summary>
    ///     Draws a card.
    /// </summary>
    /// <param name="data">What to show.</param>
    /// <param name="template">The design, or null for the default.</param>
    /// <returns>A PNG stream positioned at the start.</returns>
    public async Task<MemoryStream> RenderAsync(UnlockImageData data, CardTemplate? template = null)
    {
        return (await RenderWithLayoutAsync(data, template ?? AchievementCards.Default())).Image;
    }

    /// <summary>
    ///     Draws a card and reports where each element landed.
    /// </summary>
    /// <param name="data">What to show.</param>
    /// <param name="template">The design.</param>
    /// <param name="hiddenTextId">An element drawn without its text, as the designer shows one being typed over.</param>
    /// <returns>The image and layout.</returns>
    public async Task<CardRenderResult> RenderWithLayoutAsync(UnlockImageData data, CardTemplate template,
        string? hiddenTextId = null)
    {
        var content = new AchievementCardContent(data, cards, icons)
        {
            Palette = data.Palette
        };
        return await cards.RenderAsync(content, template, hiddenTextId);
    }

    /// <summary>
    ///     What an achievement card shows: the achievement's values and images, with its grade as the accent color.
    /// </summary>
    private sealed class AchievementCardContent(UnlockImageData data, CardRenderer cards, AchievementIconService icons)
        : CardContent
    {
        public override SKColor Accent => data.Locked
            ? Palette.MutedColor
            : ToColor(AchievementCatalog.GetGrade(data.Definition.Grade).Color);

        public override bool Dimmed => data.Locked;

        public override string? Scope => data.Scope;

        public override float? Progress => data.Progress;

        public override string? ProgressLabel => data.ProgressLabel;

        public override AchievementGlyph? IconGlyph => data.Icon.Kind == AchievementIconKind.Glyph
            ? AchievementIcons.FindGlyph(data.Icon.Value)
            : AchievementIcons.FindGlyph(AchievementIcons.GradeGlyph);

        public override IEnumerable<string> ImageKeys => ["icon", "avatar"];

        public override bool IsShown(CardElement element)
        {
            return element.Show switch
            {
                CardShow.Unlocked => !data.Locked,
                CardShow.Locked => data.Locked,
                _ => true
            };
        }

        public override string TextOf(CardElement e)
        {
            if (e.Type == CardElementType.More && data.MoreLabel is null)
                return "";
            var custom = e.Type switch
            {
                CardElementType.Member => "",
                CardElementType.Label when data.Locked => e.LockedText,
                _ => e.Text
            };
            if (custom.Trim().Length > 0)
                return Resolve(custom);

            var def = data.Definition;
            return e.Type switch
            {
                CardElementType.Label => data.Label,
                CardElementType.Title => def.Name,
                CardElementType.Description => def.Description,
                CardElementType.Member => data.MemberName,
                CardElementType.Grade => AchievementCatalog.GetGrade(def.Grade).Name,
                CardElementType.Category => data.CategoryName,
                CardElementType.Points => data.PointsLabel,
                CardElementType.More => data.MoreLabel ?? "",
                _ => ""
            };
        }

        public override AchievementGlyph? DefaultGlyph(CardElement e)
        {
            return e.Type switch
            {
                CardElementType.Label => AchievementIcons.FindGlyph(data.Locked ? "lock" : AchievementIcons.GradeGlyph),
                CardElementType.Grade => AchievementIcons.FindGlyph(AchievementIcons.GradeGlyph),
                CardElementType.Category => data.CategoryIcon.Kind == AchievementIconKind.Glyph
                    ? AchievementIcons.FindGlyph(data.CategoryIcon.Value)
                    : AchievementIcons.FindGlyph(AchievementIcons.GradeGlyph),
                _ => null
            };
        }

        public override async Task<SKBitmap?> LoadImageAsync(string key)
        {
            return key switch
            {
                "icon" => await icons.LoadImageAsync(data.GuildId, data.Icon),
                "avatar" when data.AvatarUrl is not null => await cards.LoadBitmapAsync(data.AvatarUrl),
                _ => null
            };
        }

        public override Task<SKBitmap?> LoadLinkAsync(string link)
        {
            return icons.LoadImageAsync(data.GuildId, AchievementIcons.Parse(link));
        }

        private string Resolve(string text)
        {
            var def = data.Definition;
            var percent = data.Progress is { } p ? $"{Math.Round(Math.Clamp(p, 0, 1) * 100)}%" : "";
            return text
                .Replace("%achievement.name%", def.Name)
                .Replace("%achievement.description%", def.Description)
                .Replace("%achievement.grade%", AchievementCatalog.GetGrade(def.Grade).Name)
                .Replace("%achievement.points%", def.Points.ToString("N0"))
                .Replace("%achievement.category%", data.CategoryName)
                .Replace("%user.name%", data.MemberName)
                .Replace("%server.name%", data.Scope)
                .Replace("%label%", data.Label)
                .Replace("%points%", data.PointsLabel)
                .Replace("%progress%", data.ProgressLabel ?? "")
                .Replace("%progress.percent%", percent)
                .Replace("%more%", data.MoreLabel ?? "");
        }

        private static SKColor ToColor(uint rgb)
        {
            return new SKColor((byte)(rgb >> 16 & 0xFF), (byte)(rgb >> 8 & 0xFF), (byte)(rgb & 0xFF));
        }
    }
}
