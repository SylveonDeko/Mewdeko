using System.Text.Json.Serialization;

namespace Mewdeko.Controllers.Common.ClientOperations;

/// <summary>
///     A lightweight, typed description of a single guild channel, used by clients that need to
///     build channel pickers without pulling in threads or losing the channel's category and type.
/// </summary>
public class GuildChannelLiteInfo
{
    /// <summary>
    ///     The channel id.
    /// </summary>
    [JsonPropertyName("id")]
    public ulong Id { get; set; }

    /// <summary>
    ///     The channel name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     The concrete channel type: "text", "voice", "stage", "announcement", "forum", "category" or "thread".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    ///     The id of the category this channel sits under, if any.
    /// </summary>
    [JsonPropertyName("categoryId")]
    public ulong? CategoryId { get; set; }

    /// <summary>
    ///     The name of the category this channel sits under, if any.
    /// </summary>
    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }

    /// <summary>
    ///     The channel's position, used for stable client-side sorting within a category.
    /// </summary>
    [JsonPropertyName("position")]
    public int Position { get; set; }
}
