using System.IO;
using System.Text.RegularExpressions;

namespace Mewdeko.Extensions;

public static partial class UserExtensions
{
    /// <summary>
    /// Dictionary of special characters and their closest normal equivalents.
    /// </summary>
    private static readonly Dictionary<string, string> Replacements = new()
    {
        {
            "[áàäâãåāăąǎǟ]", "a"
        },
        {
            "[ÁÀÄÂÃÅĀĂĄǍǞ]", "A"
        },
        {
            "(æ|ǽ)", "ae"
        },
        {
            "(Æ|Ǽ)", "AE"
        },
        {
            "[çćĉċč]", "c"
        },
        {
            "[ÇĆĈĊČ]", "C"
        },
        {
            "[ðďđ]", "d"
        },
        {
            "[ÐĎĐ]", "D"
        },
        {
            "[éèëêēĕėęě]", "e"
        },
        {
            "[ÉÈËÊĒĔĖĘĚ]", "E"
        },
        {
            "[ƒ]", "f"
        },
        {
            "[Ƒ]", "F"
        },
        {
            "[ĝğġģ]", "g"
        },
        {
            "[ĜĞĠĢ]", "G"
        },
        {
            "[ĥħ]", "h"
        },
        {
            "[ĤĦ]", "H"
        },
        {
            "[íìïîīĭįıǐ]", "i"
        },
        {
            "[ÍÌÏÎĪĬĮİǏ]", "I"
        },
        {
            "[ĵ]", "j"
        },
        {
            "[Ĵ]", "J"
        },
        {
            "[ķ]", "k"
        },
        {
            "[Ķ]", "K"
        },
        {
            "[łĺļľŀ]", "l"
        },
        {
            "[ŁĹĻĽĿ]", "L"
        },
        {
            "[ñńņňŉŋ]", "n"
        },
        {
            "[ÑŃŅŇŊ]", "N"
        },
        {
            "[óòöôõøōŏőǒǫǿ]", "o"
        },
        {
            "[ÓÒÖÔÕØŌŎŐǑǪǾ]", "O"
        },
        {
            "(œ)", "oe"
        },
        {
            "(Œ)", "OE"
        },
        {
            "[ŕŗř]", "r"
        },
        {
            "[ŔŖŘ]", "R"
        },
        {
            "[śšşŝș]", "s"
        },
        {
            "[ŚŠŞŜȘ]", "S"
        },
        {
            "(ß)", "ss"
        },
        {
            "[ťţŧț]", "t"
        },
        {
            "[ŤŢŦȚ]", "T"
        },
        {
            "[úùüûūŭůűųǔǖǘǚǜ]", "u"
        },
        {
            "[ÚÙÜÛŪŬŮŰŲǓǕǗǙǛ]", "U"
        },
        {
            "[ṽ]", "v"
        },
        {
            "[Ṽ]", "V"
        },
        {
            "[ŵ]", "w"
        },
        {
            "[Ŵ]", "W"
        },
        {
            "[ýÿŷ]", "y"
        },
        {
            "[ÝŸŶ]", "Y"
        },
        {
            "[źżž]", "z"
        },
        {
            "[ŹŻŽ]", "Z"
        }
    };


    /// <summary>
    /// Sends a confirmation message to the specified user asynchronously.
    /// </summary>
    /// <param name="user">The user to send the confirmation message to.</param>
    /// <param name="text">The text of the confirmation message.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task SendConfirmAsync(this IUser user, string text) =>
        await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendMessageAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(text).Build())
            .ConfigureAwait(false);

    /// <summary>
    /// Sanitizes the username of the specified user by removing mentions, replacing special characters, and trimming to a maximum length.
    /// </summary>
    /// <param name="user">The user whose username is to be sanitized.</param>
    /// <returns>The sanitized username.</returns>
    public static string SanitizeUserName(this IUser user)
    {
        var userName = user.Username;

        // Remove mentions
        userName = MentionRegex().Replace(userName, string.Empty);

        // Replace special characters with their closest normal equivalents
        userName = ReplaceSpecialChars(userName);

        // Remove any remaining non-alphanumeric and non-whitespace characters
        userName = AlphaWhiteRegex().Replace(userName, string.Empty);

        // Trim whitespace from both ends and limit the length to a maximum of 32 characters (Discord name limit)
        userName = userName.TrimTo(32);

        return userName;
    }

    /// <summary>
    /// Replaces special characters in the input string with their closest normal equivalents.
    /// </summary>
    /// <param name="input">The input string to sanitize.</param>
    /// <returns>The sanitized string with special characters replaced.</returns>
    public static string ReplaceSpecialChars(string input)
    {
        return Replacements.Aggregate(input,
            (current, replacement) => Regex.Replace(current, replacement.Key, replacement.Value));
    }


    /// <summary>
    /// Sends a confirmation message with the specified title, text, and URL to the user asynchronously.
    /// </summary>
    /// <param name="user">The user to send the confirmation message to.</param>
    /// <param name="title">The title of the confirmation message.</param>
    /// <param name="text">The text of the confirmation message.</param>
    /// <param name="url">The URL to include in the confirmation message.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the sent message.</returns>
    public static async Task<IUserMessage> SendConfirmAsync(this IUser user, string title, string text,
        string? url = null)
    {
        var eb = new EmbedBuilder().WithOkColor().WithDescription(text).WithTitle(title);
        if (url != null && Uri.IsWellFormedUriString(url, UriKind.Absolute))
            eb.WithUrl(url);
        return await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends an error message with the specified title, error description, and URL to the user asynchronously.
    /// </summary>
    /// <param name="user">The user to send the error message to.</param>
    /// <param name="title">The title of the error message.</param>
    /// <param name="error">The error description.</param>
    /// <param name="url">The URL to include in the error message.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the sent message.</returns>
    public static async Task<IUserMessage> SendErrorAsync(this IUser user, string title, string error,
        string? url = null)
    {
        var eb = new EmbedBuilder().WithErrorColor().WithDescription(error).WithTitle(title);
        if (url != null && Uri.IsWellFormedUriString(url, UriKind.Absolute))
            eb.WithUrl(url);

        return await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends an error message to the user asynchronously.
    /// </summary>
    /// <param name="user">The user to send the error message to.</param>
    /// <param name="error">The error description.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the sent message.</returns>
    public static async Task<IUserMessage> SendErrorAsync(this IUser user, string? error) =>
        await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendMessageAsync(embed: new EmbedBuilder().WithErrorColor().WithDescription(error).Build())
            .ConfigureAwait(false);


    /// <summary>
    /// Sends a file asynchronously to the user.
    /// </summary>
    /// <param name="user">The user to send the file to.</param>
    /// <param name="filePath">The path to the file to send.</param>
    /// <param name="caption">The caption to include with the file.</param>
    /// <param name="text">The text to include with the file.</param>
    /// <param name="isTts">Specifies whether the message is text-to-speech.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the sent message.</returns>
    public static async Task<IUserMessage> SendFileAsync(this IUser user, string filePath, string? caption = null,
        string? text = null, bool isTts = false)
    {
        var file = File.Open(filePath, FileMode.Open);
        await using var _ = file.ConfigureAwait(false);
        return await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendFileAsync(file, caption ?? "x", text, isTts).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a file stream asynchronously to the user.
    /// </summary>
    /// <param name="user">The user to send the file to.</param>
    /// <param name="fileStream">The file stream to send.</param>
    /// <param name="fileName">The name of the file to send.</param>
    /// <param name="caption">The caption to include with the file.</param>
    /// <param name="isTts">Specifies whether the message is text-to-speech.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the sent message.</returns>
    public static async Task<IUserMessage> SendFileAsync(this IUser user, Stream fileStream, string fileName,
        string? caption = null, bool isTts = false) =>
        await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendFileAsync(fileStream, fileName, caption, isTts).ConfigureAwait(false);

    /// <summary>
    /// Gets the real avatar URL of the user.
    /// </summary>
    /// <param name="usr">The user to get the avatar URL for.</param>
    /// <param name="size">The desired size of the avatar image.</param>
    /// <returns>The URL of the user's avatar.</returns>
    public static Uri RealAvatarUrl(this IUser usr, ushort size = 2048) =>
        usr.AvatarId == null
            ? new Uri(usr.GetDefaultAvatarUrl())
            : new Uri(usr.GetAvatarUrl(ImageFormat.Auto, size));

    /// <summary>
    /// Gets the real avatar URL of the Discord user.
    /// </summary>
    /// <param name="usr">The Discord user to get the avatar URL for.</param>
    /// <returns>The URL of the user's avatar.</returns>
    public static Uri? RealAvatarUrl(this DiscordUser usr) =>
        usr.AvatarId == null
            ? null
            : new Uri(usr.AvatarId.StartsWith("a_", StringComparison.InvariantCulture)
                ? $"{DiscordConfig.CDNUrl}avatars/{usr.UserId}/{usr.AvatarId}.gif?size=2048"
                : $"{DiscordConfig.CDNUrl}avatars/{usr.UserId}/{usr.AvatarId}.png?size=2048");


    /// <summary>
    /// Gets the regular expression for matching non-alphanumeric and non-whitespace characters.
    /// </summary>
    /// <returns>The regular expression for non-alphanumeric and non-whitespace characters.</returns>
    [GeneratedRegex("[^\\w\\s]")]
    private static partial Regex AlphaWhiteRegex();

    /// <summary>
    /// Gets the regular expression for matching mentions in a string.
    /// </summary>
    /// <returns>The regular expression for mentions.</returns>
    [GeneratedRegex("<@!?[0-9]+>")]
    private static partial Regex MentionRegex();
}