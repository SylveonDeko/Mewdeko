using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Switch.Common;

/// <summary>
///     A single hit from the switchbrew.org wiki search API.
/// </summary>
public sealed class SwitchbrewSearchResult
{
    /// <summary>
    ///     Gets or sets the page title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the HTML search snippet, with the matched terms wrapped in <c>&lt;span&gt;</c> tags.
    /// </summary>
    [JsonPropertyName("snippet")]
    public string Snippet { get; set; } = string.Empty;
}

/// <summary>
///     Root response of a switchbrew.org <c>list=search</c> API call.
/// </summary>
public sealed class SwitchbrewSearchResponse
{
    /// <summary>
    ///     Gets or sets the query results.
    /// </summary>
    [JsonPropertyName("query")]
    public SwitchbrewSearchQuery? Query { get; set; }
}

/// <summary>
///     The <c>query</c> object of a switchbrew.org search response.
/// </summary>
public sealed class SwitchbrewSearchQuery
{
    /// <summary>
    ///     Gets or sets the list of search hits.
    /// </summary>
    [JsonPropertyName("search")]
    public List<SwitchbrewSearchResult> Search { get; set; } = [];
}