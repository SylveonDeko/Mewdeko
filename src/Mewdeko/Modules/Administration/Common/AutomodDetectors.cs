using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Administration.Common;

/// <summary>
///     Counts what the emoji, spoiler, zalgo and media-only automod rules look for in a message.
/// </summary>
public static class AutomodDetectors
{
    private static readonly Regex CustomEmoji = new(@"<a?:\w+:\d+>", RegexOptions.Compiled);

    private static readonly Regex Spoiler = new(@"\|\|.+?\|\|", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex LinkPattern = new(@"https?://[^\s<>]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] ImageExtensions = ["png", "jpg", "jpeg", "gif", "webp", "bmp", "avif", "tiff"];

    private static readonly string[] VideoExtensions = ["mp4", "mov", "webm", "mkv", "avi", "m4v", "wmv", "flv"];

    private static readonly string[] ImageHosts = ["tenor.com", "giphy.com", "imgur.com", "gyazo.com", "prnt.sc"];

    private static readonly string[] VideoHosts =
    [
        "youtube.com", "youtu.be", "twitch.tv", "vimeo.com", "streamable.com", "tiktok.com", "medal.tv"
    ];

    /// <summary>
    ///     Counts the custom and unicode emojis in a message.
    /// </summary>
    /// <param name="content">The message text.</param>
    /// <returns>The number of emojis.</returns>
    public static int CountEmojis(string content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;

        var count = CustomEmoji.Matches(content).Count;
        var elements = StringInfo.GetTextElementEnumerator(CustomEmoji.Replace(content, ""));
        while (elements.MoveNext())
        {
            if (IsEmoji((string)elements.Current))
                count++;
        }

        return count;
    }

    /// <summary>
    ///     Counts the spoiler tags in a message plus the attachments sent as spoilers.
    /// </summary>
    /// <param name="content">The message text.</param>
    /// <param name="filenames">The names of the message's attachments.</param>
    /// <returns>The number of spoilers.</returns>
    public static int CountSpoilers(string content, IEnumerable<string> filenames)
    {
        var tags = string.IsNullOrEmpty(content) ? 0 : Spoiler.Matches(content).Count;
        return tags + filenames.Count(x => x.StartsWith("SPOILER_", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Finds the most combining marks stacked on one character.
    /// </summary>
    /// <param name="content">The message text.</param>
    /// <returns>The highest number of marks on a single character.</returns>
    public static int MaxCombiningMarks(string content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;

        var max = 0;
        var elements = StringInfo.GetTextElementEnumerator(content);
        while (elements.MoveNext())
        {
            var marks = 0;
            foreach (var rune in ((string)elements.Current).EnumerateRunes())
            {
                if (IsVariationSelector(rune))
                    continue;

                if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark)
                    marks++;
            }

            max = Math.Max(max, marks);
        }

        return max;
    }

    /// <summary>
    ///     Checks whether a file name is an image.
    /// </summary>
    /// <param name="name">The file name.</param>
    public static bool IsImageName(string name)
    {
        return HasExtension(name, ImageExtensions);
    }

    /// <summary>
    ///     Checks whether a file name is a video.
    /// </summary>
    /// <param name="name">The file name.</param>
    public static bool IsVideoName(string name)
    {
        return HasExtension(name, VideoExtensions);
    }

    /// <summary>
    ///     Checks whether a message links to an image or a known image host.
    /// </summary>
    /// <param name="content">The message text.</param>
    public static bool HasImageLink(string content)
    {
        return HasLink(content, ImageExtensions, ImageHosts);
    }

    /// <summary>
    ///     Checks whether a message links to a video or a known video host.
    /// </summary>
    /// <param name="content">The message text.</param>
    public static bool HasVideoLink(string content)
    {
        return HasLink(content, VideoExtensions, VideoHosts);
    }

    private static bool HasExtension(string name, string[] extensions)
    {
        var dot = name.LastIndexOf('.');
        return dot >= 0 && extensions.Contains(name[(dot + 1)..], StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasLink(string content, string[] extensions, string[] hosts)
    {
        foreach (Match match in LinkPattern.Matches(content))
        {
            if (!Uri.TryCreate(match.Value, UriKind.Absolute, out var uri))
                continue;

            if (HasExtension(uri.AbsolutePath, extensions) ||
                hosts.Any(x => uri.Host.Equals(x, StringComparison.OrdinalIgnoreCase) ||
                               uri.Host.EndsWith("." + x, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private static bool IsVariationSelector(Rune rune)
    {
        return rune.Value is >= 0xFE00 and <= 0xFE0F or >= 0xE0100 and <= 0xE01EF;
    }

    private static bool IsEmoji(string element)
    {
        if (element.Contains('️') || element.Contains('⃣'))
            return true;

        var first = element.EnumerateRunes().First().Value;
        return first is >= 0x1F000 and <= 0x1FAFF or >= 0x2600 and <= 0x27BF or 0x2B50 or 0x2B55 or 0x2B1B or 0x2B1C
            or 0x231A or 0x231B or >= 0x23E9 and <= 0x23F3 or >= 0x23F8 and <= 0x23FA or 0x3297 or 0x3299;
    }
}
