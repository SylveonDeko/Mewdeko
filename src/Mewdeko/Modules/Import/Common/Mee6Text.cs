using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Import.Common;

/// <summary>
///     Where a MEE6 message is used, which decides what its placeholders mean.
/// </summary>
public enum Mee6TextContext
{
    /// <summary>
    ///     Welcome, goodbye, custom command and other member messages.
    /// </summary>
    Member,

    /// <summary>
    ///     Level-up announcements.
    /// </summary>
    LevelUp,

    /// <summary>
    ///     Twitch live announcements.
    /// </summary>
    Stream,

    /// <summary>
    ///     Birthday wishes.
    /// </summary>
    Birthday
}

/// <summary>
///     Turns MEE6 message text and embeds into Mewdeko messages: placeholders become Mewdeko placeholders, and
///     <c>:emoji:</c> and <c>#channel</c> shorthand become real mentions once the server is known.
/// </summary>
public static partial class Mee6Text
{
    private static readonly Dictionary<string, string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        ["user"] = "%user.mention%",
        ["user.mention"] = "%user.mention%",
        ["user.name"] = "%user.name%",
        ["user.idname"] = "%user.fullname%",
        ["user.id"] = "%user.id%",
        ["user.avatar"] = "%user.avatar%",
        ["username"] = "%user.name%",
        ["server"] = "%server.name%",
        ["server.name"] = "%server.name%",
        ["server.id"] = "%server.id%",
        ["server.icon"] = "%server.icon%",
        ["server.member_count"] = "%server.members%",
        ["channel"] = "%channel.mention%"
    };

    private static readonly Dictionary<string, string> LevelUp = new(StringComparer.OrdinalIgnoreCase)
    {
        ["user"] = "%xp.user.mention%",
        ["player"] = "%xp.user.mention%",
        ["user.mention"] = "%xp.user.mention%",
        ["user.name"] = "%xp.user.name%",
        ["level"] = "%xp.level.new%",
        ["rank"] = "%xp.rank%"
    };

    private static readonly Dictionary<string, string> Stream = new(StringComparer.OrdinalIgnoreCase)
    {
        ["streamer"] = "%stream.name%",
        ["link"] = "%stream.url%",
        ["title"] = "%stream.title%",
        ["game"] = "%stream.game%"
    };

    private static readonly Dictionary<string, string> Birthday = new(StringComparer.OrdinalIgnoreCase)
    {
        ["age"] = "%birthday.age%"
    };

    /// <summary>
    ///     Replaces MEE6 placeholders with Mewdeko ones. Unknown placeholders are left as written.
    /// </summary>
    /// <param name="text">The MEE6 text.</param>
    /// <param name="context">Where the text is used.</param>
    public static string Translate(string? text, Mee6TextContext context = Mee6TextContext.Member)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var specific = context switch
        {
            Mee6TextContext.LevelUp => LevelUp,
            Mee6TextContext.Stream => Stream,
            Mee6TextContext.Birthday => Birthday,
            _ => null
        };

        if (context == Mee6TextContext.Member)
            text = text.Replace("{...}", "%target%", StringComparison.Ordinal);

        return Placeholder().Replace(text, match =>
        {
            var key = match.Groups[1].Value;
            if (specific is not null && specific.TryGetValue(key, out var mapped))
                return mapped;
            return Common.TryGetValue(key, out var common) ? common : match.Value;
        });
    }

    /// <summary>
    ///     Builds a Mewdeko message from MEE6 content and an optional embed. Plain text stays plain; anything with an
    ///     embed becomes embed builder JSON.
    /// </summary>
    /// <param name="content">The message text.</param>
    /// <param name="embed">The MEE6 embed, or default when there is none.</param>
    /// <param name="useEmbed">Whether MEE6 sends the embed.</param>
    /// <param name="context">Where the message is used.</param>
    public static string BuildMessage(string? content, JsonElement embed, bool useEmbed,
        Mee6TextContext context = Mee6TextContext.Member)
    {
        var text = Translate(content, context);
        if (!useEmbed || embed.ValueKind != JsonValueKind.Object)
            return text;

        var built = BuildEmbed(embed, context);
        if (built is null)
            return text;

        return new JsonObject
        {
            ["content"] = "", ["embeds"] = new JsonArray(built)
        }.ToJsonString();
    }

    /// <summary>
    ///     Builds a Mewdeko message from a MEE6 message object with <c>content</c> and <c>embeds</c>.
    /// </summary>
    /// <param name="message">The MEE6 message.</param>
    /// <param name="context">Where the message is used.</param>
    public static string BuildFromMessageObject(JsonElement message, Mee6TextContext context = Mee6TextContext.Member)
    {
        var content = message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
            ? Translate(c.GetString(), context)
            : "";

        var embeds = new JsonArray();
        if (message.TryGetProperty("embeds", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var embed in list.EnumerateArray())
            {
                if (BuildEmbed(embed, context) is { } built)
                    embeds.Add(built);
            }
        }

        if (embeds.Count == 0)
            return content;

        return new JsonObject
        {
            ["content"] = content, ["embeds"] = embeds
        }.ToJsonString();
    }

    /// <summary>
    ///     Fills in <c>:Name:</c> emoji shorthand and <c>#channel-name</c> references using the server's own emojis and
    ///     channels, since MEE6 resolves those when it sends and Mewdeko does not.
    /// </summary>
    /// <param name="text">Translated text.</param>
    /// <param name="emojis">The server's custom emojis by name.</param>
    /// <param name="channels">The server's text channels by name.</param>
    public static string ResolveMentions(string text, IReadOnlyDictionary<string, string> emojis,
        IReadOnlyDictionary<string, ulong> channels)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        text = EmojiShorthand().Replace(text, match =>
        {
            var start = match.Index;
            if (start > 0 && text[start - 1] is '<' or 'a')
                return match.Value;
            return emojis.TryGetValue(match.Groups[1].Value, out var emote) ? emote : match.Value;
        });

        foreach (var (name, id) in channels.OrderByDescending(x => x.Key.Length))
        {
            if (name.Length > 0)
                text = text.Replace("#" + name, $"<#{id}>", StringComparison.Ordinal);
        }

        return text;
    }

    private static JsonObject? BuildEmbed(JsonElement embed, Mee6TextContext context)
    {
        if (embed.ValueKind != JsonValueKind.Object)
            return null;

        var result = new JsonObject();
        if (Str(embed, "title") is { } title)
            result["title"] = Translate(title, context);
        if (Str(embed, "description") is { } description)
            result["description"] = Translate(description, context);
        if (Str(embed, "url") is { } url)
            result["url"] = url;
        if (embed.TryGetProperty("color", out var color) && color.TryGetInt32(out var colorValue))
            result["color"] = $"#{colorValue & 0xFFFFFF:X6}";

        if (embed.TryGetProperty("author", out var author) && author.ValueKind == JsonValueKind.Object &&
            Str(author, "name") is { } authorName)
        {
            result["author"] = new JsonObject
            {
                ["name"] = Translate(authorName, context),
                ["url"] = Str(author, "url"),
                ["icon_url"] = Translate(Str(author, "icon_url"), context)
            };
        }

        foreach (var key in new[] { "thumbnail", "image" })
        {
            if (embed.TryGetProperty(key, out var media) && media.ValueKind == JsonValueKind.Object &&
                Str(media, "url") is { } mediaUrl)
                result[key] = new JsonObject { ["url"] = Translate(mediaUrl, context) };
        }

        if (embed.TryGetProperty("footer", out var footer) && footer.ValueKind == JsonValueKind.Object &&
            Str(footer, "text") is { } footerText)
        {
            result["footer"] = new JsonObject
            {
                ["text"] = Translate(footerText, context), ["icon_url"] = Str(footer, "icon_url")
            };
        }

        if (embed.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Array)
        {
            var array = new JsonArray();
            foreach (var field in fields.EnumerateArray())
            {
                if (Str(field, "name") is not { } name || Str(field, "value") is not { } value)
                    continue;
                array.Add(new JsonObject
                {
                    ["name"] = Translate(name, context),
                    ["value"] = Translate(value, context),
                    ["inline"] = field.TryGetProperty("inline", out var inline) && inline.ValueKind == JsonValueKind.True
                });
            }

            if (array.Count > 0)
                result["fields"] = array;
        }

        return result.Count == 0 ? null : result;
    }

    private static string? Str(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;
    }

    [GeneratedRegex(@"\{([a-zA-Z_]+(?:\.[a-zA-Z_]+)*)\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@":([A-Za-z0-9_]{2,32}):")]
    private static partial Regex EmojiShorthand();
}
