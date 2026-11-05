namespace Mewdeko.Controllers.Common.CardPresets;

/// <summary>
///     The shared background presets.
/// </summary>
public class CardPresetCatalogResponse
{
    /// <summary>
    ///     The categories, in display order.
    /// </summary>
    public List<CardPresetCategoryResponse> Categories { get; set; } = [];

    /// <summary>
    ///     Every preset.
    /// </summary>
    public List<CardPresetResponse> Presets { get; set; } = [];
}

/// <summary>
///     A group of background presets.
/// </summary>
public class CardPresetCategoryResponse
{
    /// <summary>
    ///     The category's id.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     The name shown on the category's tab.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Where the category sits among the others.
    /// </summary>
    public int Order { get; set; }
}

/// <summary>
///     One background preset.
/// </summary>
public class CardPresetResponse
{
    /// <summary>
    ///     The preset's id.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     The id of the category it belongs to.
    /// </summary>
    public required string Category { get; set; }

    /// <summary>
    ///     The name shown under the thumbnail.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     The value a design stores to use it, <c>preset:id</c>.
    /// </summary>
    public required string Reference { get; set; }

    /// <summary>
    ///     The full size image's URL.
    /// </summary>
    public required string ImageUrl { get; set; }

    /// <summary>
    ///     The thumbnail's URL.
    /// </summary>
    public required string ThumbUrl { get; set; }

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
