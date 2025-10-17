namespace Mewdeko.Modules.Games.Common.Kaladont;

/// <summary>
///     Helper for handling Serbian digraphs (nj, lj, dž) which count as single letters.
/// </summary>
public static class SerbianDigraphHelper
{
    private static readonly string[] Digraphs = ["nj", "lj", "dž", "NJ", "Nj", "LJ", "Lj", "DŽ", "Dž"];

    /// <summary>
    ///     Gets the last 2 letters from a Serbian word, treating digraphs as single letters.
    /// </summary>
    /// <param name="word">The word to process.</param>
    /// <returns>A string containing the last 2 letters.</returns>
    public static string GetLastTwoLetters(string word)
    {
        if (string.IsNullOrEmpty(word))
            return "";

        var letters = SplitIntoLetters(word);
        if (letters.Count < 2)
            return word;

        return letters[^2] + letters[^1];
    }

    /// <summary>
    ///     Gets the first 2 letters from a Serbian word, treating digraphs as single letters.
    /// </summary>
    /// <param name="word">The word to process.</param>
    /// <returns>A string containing the first 2 letters.</returns>
    public static string GetFirstTwoLetters(string word)
    {
        if (string.IsNullOrEmpty(word))
            return "";

        var letters = SplitIntoLetters(word);
        if (letters.Count < 2)
            return word;

        return letters[0] + letters[1];
    }

    /// <summary>
    ///     Splits a Serbian word into letters, treating digraphs as single letters.
    /// </summary>
    /// <param name="word">The word to split.</param>
    /// <returns>A list of letters (digraphs are single entries).</returns>
    public static List<string> SplitIntoLetters(string word)
    {
        var letters = new List<string>();
        var i = 0;

        while (i < word.Length)
        {
            // Check if current position starts a digraph (case-insensitive)
            if (i < word.Length - 1)
            {
                var twoChars = word.Substring(i, 2);
                var twoCharsLower = twoChars.ToLowerInvariant();

                if (twoCharsLower is "nj" or "lj" or "dž")
                {
                    letters.Add(twoCharsLower);
                    i += 2;
                    continue;
                }
            }

            // Regular single character
            letters.Add(word[i].ToString().ToLowerInvariant());
            i++;
        }

        return letters;
    }

    /// <summary>
    ///     Gets the count of letters in a Serbian word, treating digraphs as single letters.
    /// </summary>
    /// <param name="word">The word to count.</param>
    /// <returns>The number of letters.</returns>
    public static int GetLetterCount(string word)
    {
        return SplitIntoLetters(word).Count;
    }
}