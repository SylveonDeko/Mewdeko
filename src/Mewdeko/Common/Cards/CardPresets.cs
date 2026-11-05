using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mewdeko.Common.Cards;

/// <summary>
///     A group of background presets.
/// </summary>
public sealed class CardPresetCategory
{
    /// <summary>
    ///     The category's id, lowercase letters, digits and dashes.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The name shown on the category's tab.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Where the category sits among the others, lowest first.
    /// </summary>
    public int Order { get; set; }
}

/// <summary>
///     One background preset a server can pick instead of uploading an image.
/// </summary>
public sealed class CardPreset
{
    /// <summary>
    ///     The preset's id, as used in <c>preset:id</c>.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     The id of the <see cref="CardPresetCategory" /> it belongs to.
    /// </summary>
    public string Category { get; set; } = "";

    /// <summary>
    ///     The name shown under the thumbnail.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The full size image, relative to the catalog's folder.
    /// </summary>
    public string Image { get; set; } = "";

    /// <summary>
    ///     The thumbnail, relative to the catalog's folder.
    /// </summary>
    public string Thumb { get; set; } = "";

    /// <summary>
    ///     Width of the full size image in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     Height of the full size image in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    ///     Who made the image, when it needs crediting.
    /// </summary>
    public string? Credit { get; set; }
}

/// <summary>
///     The list of background presets hosted on the CDN.
/// </summary>
public sealed class CardPresetCatalog
{
    /// <summary>
    ///     A catalog with nothing in it.
    /// </summary>
    public static CardPresetCatalog Empty => new();

    /// <summary>
    ///     The catalog format version.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    ///     The categories, in display order.
    /// </summary>
    public List<CardPresetCategory> Categories { get; set; } = [];

    /// <summary>
    ///     Every preset.
    /// </summary>
    public List<CardPreset> Presets { get; set; } = [];

    /// <summary>
    ///     Finds a preset by id.
    /// </summary>
    /// <param name="id">The preset id.</param>
    /// <returns>The preset, or null when there is none.</returns>
    public CardPreset? Find(string id)
    {
        return Presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
    }
}

/// <summary>
///     Reads the preset catalog and the <c>preset:id</c> references that point into it.
/// </summary>
public static partial class CardPresets
{
    /// <summary>
    ///     The prefix of a stored reference to a preset.
    /// </summary>
    public const string Prefix = "preset:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    ///     Whether a value is a usable preset or category id.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>True when it is.</returns>
    public static bool IsValidId(string? id)
    {
        return id is not null && IdPattern().IsMatch(id);
    }

    /// <summary>
    ///     Reads a <c>preset:id</c> reference.
    /// </summary>
    /// <param name="reference">The stored value or typed input.</param>
    /// <param name="id">The preset id.</param>
    /// <returns>True when it is a well formed reference.</returns>
    public static bool TryParse(string? reference, out string id)
    {
        id = "";
        var value = reference?.Trim();
        if (value is null || !value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var candidate = value[Prefix.Length..].Trim().ToLowerInvariant();
        if (!IsValidId(candidate))
            return false;

        id = candidate;
        return true;
    }

    /// <summary>
    ///     Writes a preset id as a reference.
    /// </summary>
    /// <param name="id">The preset id.</param>
    /// <returns>The <c>preset:id</c> reference.</returns>
    public static string Reference(string id)
    {
        return Prefix + id;
    }

    /// <summary>
    ///     Reads a catalog. Entries that are unusable are skipped, so one bad entry doesn't hide the rest.
    /// </summary>
    /// <param name="json">The catalog JSON.</param>
    /// <returns>The catalog, or null when the text isn't a catalog at all.</returns>
    public static CardPresetCatalog? ParseCatalog(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        CardPresetCatalog? raw;
        try
        {
            raw = JsonSerializer.Deserialize<CardPresetCatalog>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (raw is null)
            return null;

        var categories = new List<CardPresetCategory>();
        foreach (var category in raw.Categories ?? [])
        {
            if (category is null)
                continue;
            var id = (category.Id ?? "").Trim().ToLowerInvariant();
            var name = (category.Name ?? "").Trim();
            if (!IsValidId(id) || name.Length == 0 || categories.Any(c => c.Id == id))
                continue;
            categories.Add(new CardPresetCategory
            {
                Id = id, Name = name.Length > 40 ? name[..40] : name, Order = category.Order
            });
        }

        var presets = new List<CardPreset>();
        foreach (var preset in raw.Presets ?? [])
        {
            if (preset is null)
                continue;
            var id = (preset.Id ?? "").Trim().ToLowerInvariant();
            var category = (preset.Category ?? "").Trim().ToLowerInvariant();
            var name = (preset.Name ?? "").Trim();
            var image = CleanPath(preset.Image);
            var thumb = CleanPath(preset.Thumb);
            if (!IsValidId(id) || presets.Any(p => p.Id == id) || categories.All(c => c.Id != category) ||
                image.Length == 0 || thumb.Length == 0)
                continue;
            presets.Add(new CardPreset
            {
                Id = id,
                Category = category,
                Name = name.Length == 0 ? id : name.Length > 60 ? name[..60] : name,
                Image = image,
                Thumb = thumb,
                Width = Math.Clamp(preset.Width, 0, 20000),
                Height = Math.Clamp(preset.Height, 0, 20000),
                Credit = string.IsNullOrWhiteSpace(preset.Credit) ? null : preset.Credit.Trim()
            });
        }

        return new CardPresetCatalog
        {
            Version = raw.Version,
            Categories = categories.OrderBy(c => c.Order).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Presets = presets
        };
    }

    /// <summary>
    ///     Joins a catalog path onto the catalog's folder URL.
    /// </summary>
    /// <param name="baseUrl">The folder URL.</param>
    /// <param name="path">A relative path from the catalog.</param>
    /// <returns>The full URL.</returns>
    public static string Url(string baseUrl, string path)
    {
        return $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
    }

    private static string CleanPath(string? path)
    {
        var value = (path ?? "").Trim().Replace('\\', '/');
        if (value.Length == 0 || value.Length > 200 || value.StartsWith('/') || value.Contains("..") ||
            value.Contains("://") || value.Contains('?') || value.Contains('#'))
            return "";
        return value;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex IdPattern();
}
