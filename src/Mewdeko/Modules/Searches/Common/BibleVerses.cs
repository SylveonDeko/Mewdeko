using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches.Common;

/// <summary>
/// Represents a collection of Bible verses.
/// </summary>
public class BibleVerses
{
    /// <summary>
    /// Gets or sets the error message, if any.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the array of Bible verses.
    /// </summary>
    public BibleVerse[] Verses { get; set; }
}

/// <summary>
/// Represents a single Bible verse.
/// </summary>
public class BibleVerse
{
    /// <summary>
    /// Gets or sets the name of the book where the verse is located.
    /// </summary>
    [JsonProperty("book_name")]
    public string BookName { get; set; }

    /// <summary>
    /// Gets or sets the chapter number of the verse.
    /// </summary>
    public int Chapter { get; set; }

    /// <summary>
    /// Gets or sets the verse number.
    /// </summary>
    public int Verse { get; set; }

    /// <summary>
    /// Gets or sets the text of the verse.
    /// </summary>
    public string Text { get; set; }
}