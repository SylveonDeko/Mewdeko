using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mewdeko.Modules.StatChannels.Common;

/// <summary>
///     Per channel tuning for the selected <see cref="StatChannelDisplayStyle" />.
/// </summary>
public class StatChannelStyleOptions
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    ///     Number of cells in a progress bar.
    /// </summary>
    public int BarWidth { get; set; } = 10;

    /// <summary>
    ///     Glyph used for filled progress bar cells.
    /// </summary>
    public string BarFilled { get; set; } = "▰";

    /// <summary>
    ///     Glyph used for empty progress bar cells.
    /// </summary>
    public string BarEmpty { get; set; } = "▱";

    /// <summary>
    ///     Decimal places used by the compact and percent styles.
    /// </summary>
    public int Decimals { get; set; } = 1;

    /// <summary>
    ///     Total width used by the padded style.
    /// </summary>
    public int PadWidth { get; set; } = 4;

    /// <summary>
    ///     Text substituted for a true boolean value, for example a live indicator.
    /// </summary>
    public string TrueText { get; set; } = "🔴 LIVE";

    /// <summary>
    ///     Text substituted for a false boolean value.
    /// </summary>
    public string FalseText { get; set; } = "⚫ Offline";

    /// <summary>
    ///     Deserializes stored style options, falling back to defaults when absent or malformed.
    /// </summary>
    /// <param name="json">The serialized options.</param>
    /// <returns>The parsed options.</returns>
    public static StatChannelStyleOptions Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new StatChannelStyleOptions();

        try
        {
            return JsonSerializer.Deserialize<StatChannelStyleOptions>(json, SerializerOptions)
                   ?? new StatChannelStyleOptions();
        }
        catch (JsonException)
        {
            return new StatChannelStyleOptions();
        }
    }

    /// <summary>
    ///     Serializes these options for storage.
    /// </summary>
    /// <returns>The serialized options.</returns>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, SerializerOptions);
    }
}