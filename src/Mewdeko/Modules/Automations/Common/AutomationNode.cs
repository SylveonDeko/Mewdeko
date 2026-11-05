using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Automations.Common;

/// <summary>
///     A button an automation posts. Clicking it can start automations that use the button trigger.
/// </summary>
/// <param name="CustomId">The ID automations match on, without the prefix Discord sees.</param>
/// <param name="Label">The text on the button.</param>
/// <param name="Style">Primary, Secondary, Success or Danger.</param>
/// <param name="Emoji">An optional emoji shown on the button.</param>
public sealed record AutomationButton(string CustomId, string Label, string Style, string? Emoji);

/// <summary>
///     One condition or action of an automation: a type and the fields that type takes.
/// </summary>
public sealed class AutomationNode
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    ///     Gets or sets the condition or action type, such as <c>SendMessage</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>
    ///     Gets or sets the fields of the node, keyed by name.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Builds a node from a type and its field values.
    /// </summary>
    /// <param name="type">The condition or action type.</param>
    /// <param name="fields">The field names and values. Null values are left out.</param>
    public static AutomationNode Create(string type, params (string Name, object? Value)[] fields)
    {
        var node = new AutomationNode
        {
            Type = type
        };

        foreach (var (name, value) in fields)
        {
            if (value is not null)
                node.Fields[name] = JsonSerializer.SerializeToElement(value, Options);
        }

        return node;
    }

    /// <summary>
    ///     Reads a JSON array of nodes, or an empty list when it is blank or not valid.
    /// </summary>
    /// <param name="json">The stored JSON array.</param>
    public static List<AutomationNode> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<AutomationNode>>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    ///     Writes nodes as a JSON array.
    /// </summary>
    /// <param name="nodes">The nodes.</param>
    public static string ToJson(IEnumerable<AutomationNode> nodes) => JsonSerializer.Serialize(nodes, Options);

    /// <summary>
    ///     Gets a field if it is present and not null.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <param name="value">The value.</param>
    public bool TryGet(string name, out JsonElement value)
    {
        foreach (var (key, element) in Fields)
        {
            if (!key.Equals(name, StringComparison.OrdinalIgnoreCase) || element.ValueKind == JsonValueKind.Null)
                continue;

            value = element;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    ///     Gets a text field, or null when it is missing or blank.
    /// </summary>
    /// <param name="name">The field name.</param>
    public string? GetString(string name) =>
        TryGet(name, out var value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    /// <summary>
    ///     Gets a whole number field.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <param name="fallback">The value when the field is missing.</param>
    public long GetLong(string name, long fallback = 0)
    {
        if (!TryGet(name, out var value))
            return fallback;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), out var number) => number,
            _ => fallback
        };
    }

    /// <summary>
    ///     Gets a true or false field.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <param name="fallback">The value when the field is missing.</param>
    public bool GetBool(string name, bool fallback = false)
    {
        if (!TryGet(name, out var value))
            return fallback;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback
        };
    }

    /// <summary>
    ///     Gets an ID field, or 0 when it is missing.
    /// </summary>
    /// <param name="name">The field name.</param>
    public ulong GetId(string name) => ReadId(TryGet(name, out var value) ? value : default);

    /// <summary>
    ///     Gets a list of IDs.
    /// </summary>
    /// <param name="name">The field name.</param>
    public List<ulong> GetIds(string name) =>
        TryGet(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(ReadId).Where(x => x != 0).Distinct().ToList()
            : [];

    /// <summary>
    ///     Gets a list of text values, without blank ones.
    /// </summary>
    /// <param name="name">The field name.</param>
    public List<string> GetStrings(string name) =>
        TryGet(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()))
                .Select(x => x.GetString()!.Trim())
                .ToList()
            : [];

    /// <summary>
    ///     Gets the buttons of a message action.
    /// </summary>
    /// <param name="name">The field name.</param>
    public List<AutomationButton> GetButtons(string name = "buttons")
    {
        var buttons = new List<AutomationButton>();
        if (!TryGet(name, out var value) || value.ValueKind != JsonValueKind.Array)
            return buttons;

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var node = new AutomationNode();
            foreach (var property in item.EnumerateObject())
                node.Fields[property.Name] = property.Value;

            buttons.Add(new AutomationButton(node.GetString("customId") ?? "", node.GetString("label") ?? "",
                node.GetString("style") ?? "Primary", node.GetString("emoji")));
        }

        return buttons;
    }

    private static ulong ReadId(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number when value.TryGetUInt64(out var id) => id,
        JsonValueKind.String when ulong.TryParse(value.GetString(), out var id) => id,
        _ => 0
    };
}
