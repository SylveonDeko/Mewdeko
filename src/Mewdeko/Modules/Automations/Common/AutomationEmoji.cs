using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Automations.Common;

/// <summary>
///     Reads the emojis written in automation settings, which may be a character, a custom emoji mention, name:id or an
///     ID.
/// </summary>
public static partial class AutomationEmoji
{
    /// <summary>
    ///     Gets the value two emojis are compared by: the ID of a custom emoji, or the character of a standard one.
    /// </summary>
    /// <param name="text">The emoji as written.</param>
    public static string Normalize(string? text)
    {
        text = text?.Trim() ?? "";
        if (text.Length == 0)
            return "";

        if (Emote.TryParse(text, out var emote))
            return emote.Id.ToString();

        if (ulong.TryParse(text, out _))
            return text;

        var named = NamedEmoji().Match(text);
        return named.Success ? named.Groups["id"].Value : text;
    }

    /// <summary>
    ///     Gets the value a reaction is compared by.
    /// </summary>
    /// <param name="emote">The emoji of the reaction.</param>
    public static string Normalize(IEmote emote) => emote is Emote custom ? custom.Id.ToString() : emote.Name;

    /// <summary>
    ///     Checks that the text is an emoji that can be used on a message.
    /// </summary>
    /// <param name="text">The emoji as written.</param>
    public static bool IsValid(string? text)
    {
        text = text?.Trim() ?? "";
        if (text.Length is 0 or > 100)
            return false;

        if (Emote.TryParse(text, out _) || ulong.TryParse(text, out _) || NamedEmoji().IsMatch(text))
            return true;

        return Emoji.TryParse(text, out _) || LooksLikeEmoji(text);
    }

    /// <summary>
    ///     Turns the text into an emoji that can be added as a reaction.
    /// </summary>
    /// <param name="text">The emoji as written.</param>
    /// <returns>The emoji, or null when it cannot be read.</returns>
    public static IEmote? Parse(string? text)
    {
        text = text?.Trim() ?? "";
        if (text.Length == 0)
            return null;

        if (Emote.TryParse(text, out var emote))
            return emote;

        if (ulong.TryParse(text, out var id))
            return new Emote(id, "emoji", false);

        var named = NamedEmoji().Match(text);
        if (named.Success)
            return new Emote(ulong.Parse(named.Groups["id"].Value), named.Groups["name"].Value, false);

        return Emoji.TryParse(text, out var emoji) ? emoji : LooksLikeEmoji(text) ? new Emoji(text) : null;
    }

    private static bool LooksLikeEmoji(string text) =>
        text.Length <= 16 && text.All(c => !char.IsWhiteSpace(c) && !(c < 128 && char.IsLetterOrDigit(c)));

    [GeneratedRegex(@"^(?<name>\w{2,32}):(?<id>\d{15,21})$")]
    private static partial Regex NamedEmoji();
}
