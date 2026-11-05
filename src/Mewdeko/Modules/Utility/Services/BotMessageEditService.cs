using System.Text.Json;
using System.Text.RegularExpressions;
using Mewdeko.Common;
using Mewdeko.Services.Strings;
using BuilderAuthor = Mewdeko.Common.Author;
using BuilderEmbed = Mewdeko.Common.Embed;
using BuilderField = Mewdeko.Common.Field;
using BuilderFooter = Mewdeko.Common.Footer;
using BuilderImage = Mewdeko.Common.Image;
using BuilderThumbnail = Mewdeko.Common.Thumbnail;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Outcome of looking up or editing a message the bot posted.
/// </summary>
public enum BotMessageEditResult
{
    /// <summary>
    ///     The message was found or edited.
    /// </summary>
    Ok,

    /// <summary>
    ///     The link is not a message link.
    /// </summary>
    BadLink,

    /// <summary>
    ///     The link points at another server.
    /// </summary>
    WrongGuild,

    /// <summary>
    ///     The channel or message does not exist or the bot cannot see it.
    /// </summary>
    NotFound,

    /// <summary>
    ///     The message was not posted by the bot.
    /// </summary>
    NotMine,

    /// <summary>
    ///     The new content would leave the message empty.
    /// </summary>
    Empty
}

/// <summary>
///     Finds and edits messages the bot itself posted, by message link.
/// </summary>
public partial class BotMessageEditService(DiscordShardedClient client) : INService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [GeneratedRegex(@"channels/(?<guild>\d+)/(?<channel>\d+)/(?<message>\d+)")]
    private static partial Regex LinkPattern();

    /// <summary>
    ///     Explains a failed lookup or edit.
    /// </summary>
    /// <param name="strings">The localized strings.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="result">The outcome.</param>
    /// <returns>The error text.</returns>
    public static string Explain(GeneratedBotStrings strings, ulong guildId, BotMessageEditResult result)
    {
        return result switch
        {
            BotMessageEditResult.BadLink => strings.BotMsgBadLink(guildId),
            BotMessageEditResult.WrongGuild => strings.BotMsgWrongGuild(guildId),
            BotMessageEditResult.NotMine => strings.BotMsgNotMine(guildId),
            BotMessageEditResult.Empty => strings.BotMsgEmpty(guildId),
            _ => strings.BotMsgNotFound(guildId)
        };
    }

    /// <summary>
    ///     Reads the IDs out of a message link.
    /// </summary>
    /// <param name="link">The message link.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="messageId">The message ID.</param>
    /// <returns>Whether the link was a message link.</returns>
    public static bool TryParseLink(string? link, out ulong guildId, out ulong channelId, out ulong messageId)
    {
        guildId = channelId = messageId = 0;
        var match = LinkPattern().Match(link ?? "");
        return match.Success
               && ulong.TryParse(match.Groups["guild"].Value, out guildId)
               && ulong.TryParse(match.Groups["channel"].Value, out channelId)
               && ulong.TryParse(match.Groups["message"].Value, out messageId);
    }

    /// <summary>
    ///     Finds a message the bot posted.
    /// </summary>
    /// <param name="guildId">The guild the caller works in.</param>
    /// <param name="link">The message link.</param>
    /// <returns>The outcome and the message when it was found.</returns>
    public async Task<(BotMessageEditResult Result, IUserMessage? Message)> FindAsync(ulong guildId, string? link)
    {
        if (!TryParseLink(link, out var linkGuild, out var channelId, out var messageId))
            return (BotMessageEditResult.BadLink, null);

        if (linkGuild != guildId)
            return (BotMessageEditResult.WrongGuild, null);

        var channel = client.GetGuild(guildId)?.GetChannel(channelId) as IMessageChannel;
        if (channel == null)
            return (BotMessageEditResult.NotFound, null);

        IMessage? message;
        try
        {
            message = await channel.GetMessageAsync(messageId);
        }
        catch (Exception)
        {
            return (BotMessageEditResult.NotFound, null);
        }

        if (message == null)
            return (BotMessageEditResult.NotFound, null);

        return message is IUserMessage userMessage && message.Author.Id == client.CurrentUser.Id
            ? (BotMessageEditResult.Ok, userMessage)
            : (BotMessageEditResult.NotMine, null);
    }

    /// <summary>
    ///     Writes a message's text and embeds as embed json that can be edited and applied again.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The plain text when the message has no embeds, otherwise embed json.</returns>
    public static string ToEditableContent(IUserMessage message)
    {
        if (message.Embeds.Count == 0)
            return message.Content;

        var embeds = message.Embeds.Select(e => new BuilderEmbed
        {
            Title = e.Title,
            Description = e.Description,
            Url = e.Url,
            Timestamp = e.Timestamp?.UtcDateTime,
            Color = e.Color,
            Author = e.Author is { } a ? new BuilderAuthor { Name = a.Name, Url = a.Url, IconUrl = a.IconUrl } : null,
            Thumbnail = e.Thumbnail is { } t ? new BuilderThumbnail { Url = t.Url } : null,
            Image = e.Image is { } i ? new BuilderImage { Url = i.Url } : null,
            Footer = e.Footer is { } f ? new BuilderFooter { Text = f.Text, IconUrl = f.IconUrl } : null,
            Fields = e.Fields.Select(x => new BuilderField { Name = x.Name, Value = x.Value, Inline = x.Inline })
                .ToList()
        }).ToList();

        return JsonSerializer.Serialize(new NewEmbed
        {
            Content = string.IsNullOrEmpty(message.Content) ? null : message.Content,
            Embeds = embeds
        }, JsonOptions);
    }

    /// <summary>
    ///     Replaces a bot message's text and embeds with new text or embed json.
    /// </summary>
    /// <param name="message">The message to edit.</param>
    /// <param name="guildId">The guild the message is in.</param>
    /// <param name="content">The new text or embed json.</param>
    /// <returns>The outcome.</returns>
    public async Task<BotMessageEditResult> EditAsync(IUserMessage message, ulong guildId, string content)
    {
        if (SmartEmbed.TryParse(content, guildId, out var embeds, out var plainText, out var components))
        {
            var text = plainText?.SanitizeMentions();
            var hasComponents = components?.ActionRows is { Count: > 0 };
            if (string.IsNullOrWhiteSpace(text) && (embeds == null || embeds.Length == 0) && !hasComponents)
                return BotMessageEditResult.Empty;

            await message.ModifyAsync(x =>
            {
                x.Embeds = embeds ?? [];
                x.Content = text ?? "";
                if (hasComponents)
                    x.Components = components!.Build();
            }).ConfigureAwait(false);
            return BotMessageEditResult.Ok;
        }

        if (string.IsNullOrWhiteSpace(content))
            return BotMessageEditResult.Empty;

        await message.ModifyAsync(x =>
        {
            x.Content = content.SanitizeMentions();
            x.Embeds = Array.Empty<Discord.Embed>();
        }).ConfigureAwait(false);
        return BotMessageEditResult.Ok;
    }
}
