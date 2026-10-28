using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Achievements.Common;

/// <summary>
///     What a piece of card text is.
/// </summary>
public enum AchievementTextPieceKind
{
    /// <summary>
    ///     Letters with no space in them.
    /// </summary>
    Text,

    /// <summary>
    ///     A space between words.
    /// </summary>
    Space,

    /// <summary>
    ///     A line break.
    /// </summary>
    Break,

    /// <summary>
    ///     A Font Awesome glyph, written <c>{icon:name}</c>.
    /// </summary>
    Glyph,

    /// <summary>
    ///     A custom or Unicode emoji, drawn as an image.
    /// </summary>
    Emoji
}

/// <summary>
///     One piece of card text.
/// </summary>
/// <param name="Kind">What it is.</param>
/// <param name="Text">The letters, or what an emoji is drawn as when its image doesn't load.</param>
/// <param name="Glyph">The glyph of glyph pieces.</param>
/// <param name="Url">The image of emoji pieces.</param>
public sealed record AchievementTextPiece(
    AchievementTextPieceKind Kind,
    string Text,
    AchievementGlyph? Glyph = null,
    string? Url = null);

/// <summary>
///     Splits card text into words, spaces, line breaks, glyphs, and emoji.
/// </summary>
public static partial class AchievementCardText
{
    /// <summary>
    ///     Where Unicode emoji images come from: Twemoji, the set Discord draws.
    /// </summary>
    public const string TwemojiBase = "https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/72x72/";

    /// <summary>
    ///     A space between words.
    /// </summary>
    public static readonly AchievementTextPiece Space = new(AchievementTextPieceKind.Space, " ");

    private static readonly AchievementTextPiece Break = new(AchievementTextPieceKind.Break, "\n");

    private static readonly (int From, int To)[] EmojiPresentation =
    [
        (0x231A, 0x231B), (0x23E9, 0x23EC), (0x23F0, 0x23F0), (0x23F3, 0x23F3), (0x25FD, 0x25FE),
        (0x2614, 0x2615), (0x2648, 0x2653), (0x267F, 0x267F), (0x2693, 0x2693), (0x26A1, 0x26A1),
        (0x26AA, 0x26AB), (0x26BD, 0x26BE), (0x26C4, 0x26C5), (0x26CE, 0x26CE), (0x26D4, 0x26D4),
        (0x26EA, 0x26EA), (0x26F2, 0x26F3), (0x26F5, 0x26F5), (0x26FA, 0x26FA), (0x26FD, 0x26FD),
        (0x2705, 0x2705), (0x270A, 0x270B), (0x2728, 0x2728), (0x274C, 0x274C), (0x274E, 0x274E),
        (0x2753, 0x2755), (0x2757, 0x2757), (0x2795, 0x2797), (0x27B0, 0x27B0), (0x27BF, 0x27BF),
        (0x2B1B, 0x2B1C), (0x2B50, 0x2B50), (0x2B55, 0x2B55), (0x1F000, 0x1FAFF)
    ];

    [GeneratedRegex(@"<a?:([A-Za-z0-9_~]{2,32}):(\d{17,20})>|\{icon:([A-Za-z0-9-]{1,40})\}")]
    private static partial Regex Inline();

    /// <summary>
    ///     Splits text into pieces. Unknown glyph names are dropped.
    /// </summary>
    /// <param name="text">The text, with placeholders already filled in.</param>
    /// <returns>The pieces in order.</returns>
    public static List<AchievementTextPiece> Parse(string text)
    {
        var pieces = new List<AchievementTextPiece>();
        var at = 0;
        foreach (Match match in Inline().Matches(text))
        {
            AddPlain(pieces, text[at..match.Index]);
            at = match.Index + match.Length;
            if (match.Groups[3].Success)
            {
                if (AchievementIcons.FindGlyph(match.Groups[3].Value) is { } glyph)
                    pieces.Add(new AchievementTextPiece(AchievementTextPieceKind.Glyph, "", glyph));
                continue;
            }

            pieces.Add(new AchievementTextPiece(AchievementTextPieceKind.Emoji, $":{match.Groups[1].Value}:", null,
                $"https://cdn.discordapp.com/emojis/{match.Groups[2].Value}.png?size=96"));
        }

        AddPlain(pieces, text[at..]);
        return pieces;
    }

    /// <summary>
    ///     The Twemoji file name of one text element, such as <c>1f44b-1f3fd</c>, or null when it is not an emoji.
    /// </summary>
    /// <param name="element">One text element, as <see cref="StringInfo" /> splits text.</param>
    /// <returns>The file name without extension, or null.</returns>
    public static string? TwemojiName(string element)
    {
        var runes = element.EnumerateRunes().Select(r => r.Value).ToList();
        if (runes.Count == 0)
            return null;
        var first = runes[0];
        var joined = runes.Contains(0x200D);
        var emoji = joined || runes.Contains(0x20E3) || runes.Contains(0xFE0F) && first > 0x7F ||
                    EmojiPresentation.Any(r => first >= r.From && first <= r.To);
        if (!emoji)
            return null;
        return string.Join('-', runes.Where(r => joined || r != 0xFE0F).Select(r => r.ToString("x")));
    }

    private static void AddPlain(List<AchievementTextPiece> pieces, string text)
    {
        var word = new StringBuilder();
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            switch (element)
            {
                case " " or "\t":
                    Flush();
                    pieces.Add(Space);
                    break;
                case "\n" or "\r\n":
                    Flush();
                    pieces.Add(Break);
                    break;
                case "\r":
                    break;
                default:
                    if (TwemojiName(element) is { } name)
                    {
                        Flush();
                        pieces.Add(new AchievementTextPiece(AchievementTextPieceKind.Emoji, element, null,
                            $"{TwemojiBase}{name}.png"));
                    }
                    else
                    {
                        word.Append(element);
                    }

                    break;
            }
        }

        Flush();
        return;

        void Flush()
        {
            if (word.Length == 0)
                return;
            pieces.Add(new AchievementTextPiece(AchievementTextPieceKind.Text, word.ToString()));
            word.Clear();
        }
    }
}
