using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Starboard.Common;

/// <summary>
///     The values a starboard post design can show for a starred message.
/// </summary>
/// <param name="GuildId">The ID of the guild.</param>
/// <param name="GuildName">The name of the guild.</param>
/// <param name="MessageId">The ID of the starred message.</param>
/// <param name="AuthorId">The ID of the message author.</param>
/// <param name="AuthorName">The display name of the message author.</param>
/// <param name="AuthorAvatarUrl">The avatar URL of the message author.</param>
/// <param name="Content">The text of the starred message.</param>
/// <param name="ChannelId">The ID of the channel the message is in.</param>
/// <param name="ChannelName">The name of the channel the message is in.</param>
/// <param name="JumpUrl">The link to the starred message.</param>
/// <param name="StarCount">The number of stars the message has.</param>
/// <param name="Emote">The first emote of the starboard.</param>
/// <param name="StarsText">The stars line, one emote and count per emote that has stars.</param>
/// <param name="AttachmentUrls">The URLs of the message attachments.</param>
/// <param name="ImageUrl">The URL of the first image on the message, or null.</param>
/// <param name="Timestamp">When the message was sent.</param>
public record StarboardPostData(
    ulong GuildId,
    string GuildName,
    ulong MessageId,
    ulong AuthorId,
    string AuthorName,
    string AuthorAvatarUrl,
    string Content,
    ulong ChannelId,
    string ChannelName,
    string JumpUrl,
    int StarCount,
    string Emote,
    string StarsText,
    IReadOnlyList<string> AttachmentUrls,
    string? ImageUrl,
    DateTime Timestamp);

/// <summary>
///     Which of a starboard's emotes the bot reacts with on its own posts.
/// </summary>
public enum StarboardAutoReact
{
    /// <summary>
    ///     The bot adds no reactions.
    /// </summary>
    None = 0,

    /// <summary>
    ///     The bot reacts with the first emote.
    /// </summary>
    First = 1,

    /// <summary>
    ///     The bot reacts with every emote.
    /// </summary>
    All = 2
}

/// <summary>
///     The outcome of setting a starboard post design.
/// </summary>
public enum StarboardDesignResult
{
    /// <summary>
    ///     The design was saved.
    /// </summary>
    Saved,

    /// <summary>
    ///     The starboard does not exist.
    /// </summary>
    NotFound,

    /// <summary>
    ///     The design is not valid embed json or shows nothing.
    /// </summary>
    Invalid
}

/// <summary>
///     Renders a starboard post design, stored as embed json with starboard placeholders, into a message.
/// </summary>
public static partial class StarboardPostDesign
{
    /// <summary>
    ///     The placeholders a design can use.
    /// </summary>
    public static readonly string[] Placeholders =
    [
        "%star.author%", "%star.author.mention%", "%star.author.id%", "%star.author.avatar%", "%star.content%",
        "%star.channel%", "%star.channel.mention%", "%star.channel.id%", "%star.link%", "%star.id%", "%star.count%",
        "%star.emote%", "%star.emotes%", "%star.attachments%", "%star.attachments.count%", "%star.image%",
        "%star.timestamp%", "%server.name%", "%server.id%"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [GeneratedRegex("%(?:star|server)\\.[a-z.]+%")]
    private static partial Regex PlaceholderRegex();

    /// <summary>
    ///     Renders a design for a starred message.
    /// </summary>
    /// <param name="design">The embed json of the design.</param>
    /// <param name="data">The values to fill the placeholders with.</param>
    /// <param name="text">The plain message text, or null.</param>
    /// <param name="embeds">The embeds, or null.</param>
    /// <param name="components">The link buttons, or null.</param>
    /// <returns>True when the design is valid and shows something.</returns>
    public static bool TryRender(string design, StarboardPostData data, out string? text,
        out Discord.Embed[]? embeds, out ComponentBuilder? components)
    {
        text = null;
        embeds = null;
        components = null;

        var values = BuildValues(data);
        var filled = PlaceholderRegex().Replace(design, match =>
            values.TryGetValue(match.Value, out var value)
                ? JsonEncodedText.Encode(value).ToString()
                : match.Value);

        NewEmbed? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<NewEmbed>(filled, JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (parsed == null)
            return false;

        parsed.Components = parsed.Components?
            .Where(x => !x.IsSelect && !string.IsNullOrWhiteSpace(x.DisplayName) &&
                        x.Url?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true &&
                        Uri.IsWellFormedUriString(x.Url, UriKind.Absolute))
            .Take(5)
            .ToList();
        if (parsed.Components is { Count: 0 })
            parsed.Components = null;

        if (!SmartEmbed.TryBuild(parsed, data.GuildId, out embeds, out text, out components, true))
            return false;

        if (string.IsNullOrWhiteSpace(text))
            text = null;
        if (embeds is { Length: 0 })
            embeds = null;

        return text != null || embeds != null;
    }

    private static Dictionary<string, string> BuildValues(StarboardPostData data)
    {
        return new Dictionary<string, string>
        {
            ["%star.author%"] = data.AuthorName,
            ["%star.author.mention%"] = $"<@{data.AuthorId}>",
            ["%star.author.id%"] = data.AuthorId.ToString(),
            ["%star.author.avatar%"] = data.AuthorAvatarUrl,
            ["%star.content%"] = data.Content,
            ["%star.channel%"] = data.ChannelName,
            ["%star.channel.mention%"] = $"<#{data.ChannelId}>",
            ["%star.channel.id%"] = data.ChannelId.ToString(),
            ["%star.link%"] = data.JumpUrl,
            ["%star.id%"] = data.MessageId.ToString(),
            ["%star.count%"] = data.StarCount.ToString(),
            ["%star.emote%"] = data.Emote,
            ["%star.emotes%"] = data.StarsText,
            ["%star.attachments%"] = string.Join('\n', data.AttachmentUrls),
            ["%star.attachments.count%"] = data.AttachmentUrls.Count.ToString(),
            ["%star.image%"] = data.ImageUrl ?? "",
            ["%star.timestamp%"] = data.Timestamp.ToUniversalTime().ToString("O"),
            ["%server.name%"] = data.GuildName,
            ["%server.id%"] = data.GuildId.ToString()
        };
    }
}
