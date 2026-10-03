using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Achievements.Common;

/// <summary>
///     What an icon value points at.
/// </summary>
public enum AchievementIconKind
{
    /// <summary>
    ///     No icon; the category's is used.
    /// </summary>
    None,

    /// <summary>
    ///     A Font Awesome Utility Duo glyph, stored as <c>fa:name</c>.
    /// </summary>
    Glyph,

    /// <summary>
    ///     An image at an https URL.
    /// </summary>
    Url,

    /// <summary>
    ///     A Discord custom emoji, stored as <c>&lt;:name:id&gt;</c> or <c>&lt;a:name:id&gt;</c>.
    /// </summary>
    Emoji,

    /// <summary>
    ///     An image the server uploaded, stored as <c>upload:id</c>.
    /// </summary>
    Upload
}

/// <summary>
///     A parsed icon value.
/// </summary>
/// <param name="Kind">What it points at.</param>
/// <param name="Value">The glyph name, URL, emoji text, or upload ID.</param>
public sealed record AchievementIconRef(AchievementIconKind Kind, string Value)
{
    /// <summary>
    ///     No icon.
    /// </summary>
    public static readonly AchievementIconRef None = new(AchievementIconKind.None, "");

    /// <summary>
    ///     The stored form.
    /// </summary>
    public string Stored => Kind switch
    {
        AchievementIconKind.Glyph => AchievementIcons.GlyphPrefix + Value,
        AchievementIconKind.Upload => AchievementIcons.UploadPrefix + Value,
        AchievementIconKind.None => "",
        _ => Value
    };
}

/// <summary>
///     One Font Awesome glyph the icon picker offers.
/// </summary>
/// <param name="Name">The canonical name.</param>
/// <param name="Codepoint">The primary layer's code point; the secondary layer is this plus 0x100000.</param>
/// <param name="Aliases">Other names for it.</param>
public sealed record AchievementGlyph(string Name, int Codepoint, IReadOnlyList<string> Aliases);

/// <summary>
///     Achievement icons: the Font Awesome glyph table, parsing, and validation.
/// </summary>
public static partial class AchievementIcons
{
    /// <summary>
    ///     Prefix of glyph icons.
    /// </summary>
    public const string GlyphPrefix = "fa:";

    /// <summary>
    ///     Prefix of uploaded icons.
    /// </summary>
    public const string UploadPrefix = "upload:";

    /// <summary>
    ///     The icon grades and ranks use.
    /// </summary>
    public const string GradeGlyph = "trophy";

    /// <summary>
    ///     The icon server made categories use until they pick one.
    /// </summary>
    public const string FolderGlyph = "folder";

    /// <summary>
    ///     The longest accepted icon URL.
    /// </summary>
    public const int MaxUrlLength = 512;

    private const string GlyphPath = "data/achievements/fa-glyphs.json";

    private static readonly Lazy<(IReadOnlyList<AchievementGlyph> List, Dictionary<string, AchievementGlyph> ByName)>
        Table = new(LoadTable);

    /// <summary>
    ///     Every glyph, sorted by name.
    /// </summary>
    public static IReadOnlyList<AchievementGlyph> Glyphs => Table.Value.List;

    /// <summary>
    ///     Looks up a glyph by its name or an alias.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The glyph, or null.</returns>
    public static AchievementGlyph? FindGlyph(string name)
    {
        return Table.Value.ByName.GetValueOrDefault(name.Trim().ToLowerInvariant());
    }

    /// <summary>
    ///     Reads a stored icon value.
    /// </summary>
    /// <param name="stored">The stored value.</param>
    /// <returns>The parsed icon.</returns>
    public static AchievementIconRef Parse(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return AchievementIconRef.None;
        var value = stored.Trim();
        if (value.StartsWith(GlyphPrefix, StringComparison.OrdinalIgnoreCase))
            return new AchievementIconRef(AchievementIconKind.Glyph, value[GlyphPrefix.Length..]);
        if (value.StartsWith(UploadPrefix, StringComparison.OrdinalIgnoreCase))
            return new AchievementIconRef(AchievementIconKind.Upload, value[UploadPrefix.Length..]);
        if (CustomEmoji().IsMatch(value))
            return new AchievementIconRef(AchievementIconKind.Emoji, value);
        if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return new AchievementIconRef(AchievementIconKind.Url, value);
        return AchievementIconRef.None;
    }

    /// <summary>
    ///     Turns user input into a stored icon value. Accepts <c>fa:name</c> or a bare glyph name, a custom
    ///     emoji, an https image URL, or <c>upload:id</c>. Blank clears the icon.
    /// </summary>
    /// <param name="input">What was typed or picked.</param>
    /// <param name="stored">The value to store, or null to clear.</param>
    /// <returns>True when the input is usable.</returns>
    public static bool TryNormalize(string? input, out string? stored)
    {
        stored = null;
        if (string.IsNullOrWhiteSpace(input))
            return true;

        var value = input.Trim();
        var bare = value.StartsWith(GlyphPrefix, StringComparison.OrdinalIgnoreCase) ? value[GlyphPrefix.Length..] : value;
        if (FindGlyph(bare) is { } glyph)
        {
            stored = GlyphPrefix + glyph.Name;
            return true;
        }

        var parsed = Parse(value);
        switch (parsed.Kind)
        {
            case AchievementIconKind.Emoji:
                stored = parsed.Value;
                return true;
            case AchievementIconKind.Upload when int.TryParse(parsed.Value, out var id) && id > 0:
                stored = UploadPrefix + id;
                return true;
            case AchievementIconKind.Url when value.Length <= MaxUrlLength &&
                                              Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                                              uri.Scheme == Uri.UriSchemeHttps:
                stored = uri.ToString();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    ///     The upload ID of an icon, when it is an upload.
    /// </summary>
    /// <param name="icon">The icon.</param>
    /// <returns>The ID, or null.</returns>
    public static int? UploadId(AchievementIconRef icon)
    {
        return icon.Kind == AchievementIconKind.Upload && int.TryParse(icon.Value, out var id) ? id : null;
    }

    /// <summary>
    ///     The CDN image of a custom emoji icon.
    /// </summary>
    /// <param name="icon">The icon.</param>
    /// <returns>The image URL, or null when the icon is not an emoji.</returns>
    public static string? EmojiUrl(AchievementIconRef icon)
    {
        if (icon.Kind != AchievementIconKind.Emoji)
            return null;
        var match = CustomEmoji().Match(icon.Value);
        if (!match.Success)
            return null;
        var animated = match.Groups[1].Value == "a";
        return $"https://cdn.discordapp.com/emojis/{match.Groups[2].Value}.{(animated ? "gif" : "png")}?size=128";
    }

    private static (IReadOnlyList<AchievementGlyph>, Dictionary<string, AchievementGlyph>) LoadTable()
    {
        var list = new List<AchievementGlyph>();
        if (File.Exists(GlyphPath))
        {
            using var stream = File.OpenRead(GlyphPath);
            var entries = JsonSerializer.Deserialize<List<GlyphEntry>>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            list.AddRange(entries.Select(e => new AchievementGlyph(e.Name, e.Codepoint, e.Aliases ?? [])));
        }

        var byName = new Dictionary<string, AchievementGlyph>(StringComparer.Ordinal);
        foreach (var glyph in list)
        {
            byName[glyph.Name] = glyph;
            foreach (var alias in glyph.Aliases)
                byName.TryAdd(alias, glyph);
        }

        return (list, byName);
    }

    [GeneratedRegex(@"^<(a?):[A-Za-z0-9_~]{2,32}:(\d{17,20})>$")]
    private static partial Regex CustomEmoji();

    private sealed class GlyphEntry
    {
        public string Name { get; set; } = "";
        public int Codepoint { get; set; }
        public List<string>? Aliases { get; set; }
    }
}
