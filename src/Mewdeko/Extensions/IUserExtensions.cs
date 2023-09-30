using System.IO;
using System.Text.RegularExpressions;

namespace Mewdeko.Extensions;

public static partial class UserExtensions
{
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


    public static async Task SendConfirmAsync(this IUser user, string text) =>
        await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendMessageAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(text).Build())
            .ConfigureAwait(false);

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

    public static string ReplaceSpecialChars(string input)
    {
        return Replacements.Aggregate(input,
            (current, replacement) => Regex.Replace(current, replacement.Key, replacement.Value));
    }

    public static async Task<IUserMessage> SendConfirmAsync(this IUser user, string title, string text,
        string? url = null)
    {
        var eb = new EmbedBuilder().WithOkColor().WithDescription(text).WithTitle(title);
        if (url != null && Uri.IsWellFormedUriString(url, UriKind.Absolute))
            eb.WithUrl(url);
        return await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    public static async Task<IUserMessage> SendErrorAsync(this IUser user, string title, string error,
        string? url = null)
    {
        var eb = new EmbedBuilder().WithErrorColor().WithDescription(error).WithTitle(title);
        if (url != null && Uri.IsWellFormedUriString(url, UriKind.Absolute))
            eb.WithUrl(url);

        return await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    public static async Task<IUserMessage> SendErrorAsync(this IUser user, string? error) =>
        await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendMessageAsync(embed: new EmbedBuilder().WithErrorColor().WithDescription(error).Build())
            .ConfigureAwait(false);

    public static async Task<IUserMessage> SendFileAsync(this IUser user, string filePath, string? caption = null,
        string? text = null, bool isTts = false)
    {
        var file = File.Open(filePath, FileMode.Open);
        await using var _ = file.ConfigureAwait(false);
        return await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendFileAsync(file, caption ?? "x", text, isTts).ConfigureAwait(false);
    }

    public static async Task<IUserMessage> SendFileAsync(this IUser user, Stream fileStream, string fileName,
        string? caption = null, bool isTts = false) =>
        await (await user.CreateDMChannelAsync().ConfigureAwait(false))
            .SendFileAsync(fileStream, fileName, caption, isTts).ConfigureAwait(false);

    // This method is used by everything that fetches the avatar from a user
    public static Uri RealAvatarUrl(this IUser usr, ushort size = 2048) =>
        usr.AvatarId == null
            ? new Uri(usr.GetDefaultAvatarUrl())
            : new Uri(usr.GetAvatarUrl(ImageFormat.Auto, size));

    // This method is only used for the xp card
    public static Uri? RealAvatarUrl(this DiscordUser usr) =>
        usr.AvatarId == null
            ? null
            : new Uri(usr.AvatarId.StartsWith("a_", StringComparison.InvariantCulture)
                ? $"{DiscordConfig.CDNUrl}avatars/{usr.UserId}/{usr.AvatarId}.gif?size=2048"
                : $"{DiscordConfig.CDNUrl}avatars/{usr.UserId}/{usr.AvatarId}.png?size=2048");

    [GeneratedRegex("[^\\w\\s]")]
    private static partial Regex AlphaWhiteRegex();

    [GeneratedRegex("<@!?[0-9]+>")]
    private static partial Regex MentionRegex();
}