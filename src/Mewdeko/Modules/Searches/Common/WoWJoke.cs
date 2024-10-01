namespace Mewdeko.Modules.Searches.Common;

/// <summary>
///     Represents a World of Warcraft joke.
/// </summary>
public class WoWJoke
{
    /// <summary>
    ///     Gets or sets the question part of the joke.
    /// </summary>
    public string? Question { get; set; }

    /// <summary>
    ///     Gets or sets the answer part of the joke.
    /// </summary>
    public string? Answer { get; set; }

    /// <summary>
    ///     Generates a formatted string representation of the joke.
    /// </summary>
    /// <returns>A formatted string containing the question and answer of the joke.</returns>
    public override string ToString()
    {
        return $"`{Question}`\n\n**{Answer}**";
    }
}