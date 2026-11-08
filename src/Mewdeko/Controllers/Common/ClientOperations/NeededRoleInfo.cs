using System.Text.Json.Serialization;

namespace Mewdeko.Controllers.Common.ClientOperations;

/// <summary>
///     To avoid stupid errors
/// </summary>
public class NeededRoleInfo
{
    /// <summary>
    ///     Name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    ///     And badge number
    /// </summary>
    [JsonPropertyName("id")]
    public ulong Id { get; set; }

    /// <summary>
    ///     The role colour as an RGB integer, 0 when uncoloured.
    /// </summary>
    [JsonPropertyName("color")]
    public uint Color { get; set; }

    /// <summary>
    ///     The role icon, if it has one.
    /// </summary>
    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }

    /// <summary>
    ///     The role's position in the role list.
    /// </summary>
    [JsonPropertyName("position")]
    public int Position { get; set; }
}