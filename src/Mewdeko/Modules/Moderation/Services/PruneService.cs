using System.Text.RegularExpressions;
using System.Threading;
using Mewdeko.Common.Collections;

namespace Mewdeko.Modules.Moderation.Services;

/// <summary>
///     Service for purging messages.
/// </summary>
public class PurgeService : INService
{
    //channelids where Purges are currently occuring
    private readonly ConcurrentHashSet<ulong> pruningGuilds = [];

    private static readonly Regex LinkRegex = new(@"https?://\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EmojiRegex = new(
        @"<a?:\w+:\d+>|[©®‼-㊙]️?|[\uD83C-\uDBFF][\uDC00-\uDFFF]",
        RegexOptions.Compiled);

    private readonly TimeSpan twoWeeks = TimeSpan.FromDays(14);

    /// <summary>
    ///     Checks whether a message contains a URL.
    /// </summary>
    /// <param name="message">The message to check</param>
    /// <returns>True if the content has a link</returns>
    public static bool HasLink(IMessage message)
    {
        return LinkRegex.IsMatch(message.Content);
    }

    /// <summary>
    ///     Checks whether a message mentions a user or a role.
    /// </summary>
    /// <param name="message">The message to check</param>
    /// <returns>True if a user or role is mentioned</returns>
    public static bool HasMention(IMessage message)
    {
        return message.MentionedUserIds.Count > 0 || message.MentionedRoleIds.Count > 0;
    }

    /// <summary>
    ///     Checks whether a message has any attachment.
    /// </summary>
    /// <param name="message">The message to check</param>
    /// <returns>True if the message has an attachment</returns>
    public static bool HasFile(IMessage message)
    {
        return message.Attachments.Count > 0;
    }

    /// <summary>
    ///     Checks whether a message has an image attachment or an image embed.
    /// </summary>
    /// <param name="message">The message to check</param>
    /// <returns>True if the message has an image</returns>
    public static bool HasImage(IMessage message)
    {
        return message.Attachments.Any(a =>
                   a.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true ||
                   a.Width.HasValue)
               || message.Embeds.Any(e => e.Type is EmbedType.Image or EmbedType.Gifv || e.Image.HasValue);
    }

    /// <summary>
    ///     Checks whether a message content has a custom or unicode emoji.
    /// </summary>
    /// <param name="message">The message to check</param>
    /// <returns>True if the content has an emoji</returns>
    public static bool HasEmoji(IMessage message)
    {
        return EmojiRegex.IsMatch(message.Content);
    }

    /// <summary>
    ///     The filters a purge can apply to messages, optionally limited to one author.
    /// </summary>
    public sealed record PurgeFilters
    {
        /// <summary>Only messages from this user, limited to the last two weeks.</summary>
        public ulong? UserId { get; init; }

        /// <summary>Skip pinned messages.</summary>
        public bool Safe { get; init; }

        /// <summary>Skip messages from bots.</summary>
        public bool NoBots { get; init; }

        /// <summary>Only messages from bots.</summary>
        public bool OnlyBots { get; init; }

        /// <summary>Only messages with an embed.</summary>
        public bool HasEmbed { get; init; }

        /// <summary>Only messages without an embed.</summary>
        public bool NoEmbed { get; init; }

        /// <summary>Only messages with a link.</summary>
        public bool Links { get; init; }

        /// <summary>Only messages with a mention.</summary>
        public bool Mentions { get; init; }

        /// <summary>Only messages with an attachment.</summary>
        public bool Files { get; init; }

        /// <summary>Only messages with an image.</summary>
        public bool Images { get; init; }

        /// <summary>Only messages with an emoji.</summary>
        public bool Emoji { get; init; }

        /// <summary>Only messages containing this text.</summary>
        public string? Contains { get; init; }

        /// <summary>Only messages sent before this time.</summary>
        public DateTimeOffset? Before { get; init; }

        /// <summary>Only messages sent after this time.</summary>
        public DateTimeOffset? After { get; init; }

        /// <summary>Whether any filter or user is set.</summary>
        public bool IsEmpty => this == new PurgeFilters();
    }

    /// <summary>
    ///     Parses the flag text of the purge commands into filters.
    /// </summary>
    /// <param name="parameters">The flags, such as "-s -l -c word"</param>
    /// <param name="userId">The user whose messages should be purged, if any</param>
    /// <returns>The parsed filters</returns>
    public static PurgeFilters ParseFilters(string? parameters, ulong? userId = null)
    {
        var filters = new PurgeFilters
        {
            UserId = userId
        };
        if (string.IsNullOrWhiteSpace(parameters))
            return filters;

        var parts = parameters.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            switch (parts[i])
            {
                case "-s" or "--safe":
                    filters = filters with { Safe = true };
                    break;
                case "-nb" or "--nobots":
                    filters = filters with { NoBots = true };
                    break;
                case "-ob" or "--onlybots":
                    filters = filters with { OnlyBots = true };
                    break;
                case "-he" or "--hasembed":
                    filters = filters with { HasEmbed = true };
                    break;
                case "-ne" or "--noembed":
                    filters = filters with { NoEmbed = true };
                    break;
                case "-l" or "--links":
                    filters = filters with { Links = true };
                    break;
                case "-m" or "--mentions":
                    filters = filters with { Mentions = true };
                    break;
                case "-f" or "--files":
                    filters = filters with { Files = true };
                    break;
                case "-i" or "--images":
                    filters = filters with { Images = true };
                    break;
                case "-e" or "--emoji":
                    filters = filters with { Emoji = true };
                    break;
                case "-c" or "--contains":
                    if (i + 1 < parts.Length)
                        filters = filters with { Contains = parts[++i] };
                    break;
                case "-b" or "--before":
                    if (i + 1 < parts.Length && DateTimeOffset.TryParse(parts[++i], out var before))
                        filters = filters with { Before = before };
                    break;
                case "-a" or "--after":
                    if (i + 1 < parts.Length && DateTimeOffset.TryParse(parts[++i], out var after))
                        filters = filters with { After = after };
                    break;
            }
        }

        return filters;
    }

    /// <summary>
    ///     Builds one predicate from every filter that is set, so they all have to match.
    /// </summary>
    /// <param name="filters">The filters to combine</param>
    /// <returns>A predicate matching messages that pass every filter</returns>
    public static Func<IMessage, bool> BuildPredicate(PurgeFilters filters)
    {
        var options = new List<Func<IMessage, bool>>();

        if (filters.UserId is { } userId)
        {
            var cutoff = TimeSpan.FromDays(14);
            options.Add(x => x.Author.Id == userId && DateTime.UtcNow - x.CreatedAt < cutoff);
        }

        if (filters.Safe)
            options.Add(x => !x.IsPinned);
        if (filters.NoBots)
            options.Add(x => !x.Author.IsBot);
        if (filters.OnlyBots)
            options.Add(x => x.Author.IsBot);
        if (filters.HasEmbed)
            options.Add(x => x.Embeds.Count > 0);
        if (filters.NoEmbed)
            options.Add(x => x.Embeds.Count == 0);
        if (filters.Links)
            options.Add(HasLink);
        if (filters.Mentions)
            options.Add(HasMention);
        if (filters.Files)
            options.Add(HasFile);
        if (filters.Images)
            options.Add(HasImage);
        if (filters.Emoji)
            options.Add(HasEmoji);
        if (!string.IsNullOrEmpty(filters.Contains))
            options.Add(x => x.Content.Contains(filters.Contains, StringComparison.InvariantCultureIgnoreCase));
        if (filters.Before is { } before)
            options.Add(x => x.Timestamp < before);
        if (filters.After is { } after)
            options.Add(x => x.Timestamp > after);

        return m => options.All(p => p(m));
    }

    /// <summary>
    ///     Parses a custom or unicode emoji.
    /// </summary>
    /// <param name="input">The emoji text</param>
    /// <param name="emote">The parsed emoji</param>
    /// <returns>True if the text was a valid emoji</returns>
    public static bool TryParseEmote(string input, out IEmote? emote)
    {
        emote = null;
        if (Emote.TryParse(input, out var custom))
            emote = custom;
        else if (Emoji.TryParse(input, out var unicode))
            emote = unicode;
        else if (EmojiRegex.IsMatch(input))
            emote = new Emoji(input);

        return emote is not null;
    }

    /// <summary>
    ///     Removes reactions from the most recent messages in a channel.
    /// </summary>
    /// <param name="channel">The channel to clear reactions in</param>
    /// <param name="count">How many recent messages to look through</param>
    /// <param name="emote">Only remove this emoji, or every reaction when null</param>
    /// <returns>The number of messages that had reactions removed</returns>
    public async Task<int> ClearReactions(ITextChannel channel, int count, IEmote? emote = null)
    {
        channel.ThrowIfNull(nameof(channel));
        if (!pruningGuilds.Add(channel.GuildId))
            return 0;

        var cleared = 0;
        try
        {
            var msgs = (await channel.GetMessagesAsync(count).FlattenAsync().ConfigureAwait(false))
                .Where(x => emote is null
                    ? x.Reactions.Count > 0
                    : x.Reactions.ContainsKey(emote))
                .ToArray();

            foreach (var group in msgs.Chunk(5))
            {
                await Task.WhenAll(Task.Delay(1000), Task.WhenAll(group.Select(async x =>
                {
                    try
                    {
                        if (emote is null)
                            await x.RemoveAllReactionsAsync().ConfigureAwait(false);
                        else
                            await x.RemoveAllReactionsForEmoteAsync(emote).ConfigureAwait(false);
                        Interlocked.Increment(ref cleared);
                    }
                    catch
                    {
                        //ignore
                    }
                }))).ConfigureAwait(false);
            }
        }
        catch
        {
            //ignore
        }
        finally
        {
            pruningGuilds.TryRemove(channel.GuildId);
        }

        return cleared;
    }

    /// <summary>
    ///     Purges messages from a channel using a predicate
    /// </summary>
    /// <param name="channel">The channel to purge messages from</param>
    /// <param name="amount">The amount of messages to purge</param>
    /// <param name="predicate">The predicate to filter messages</param>
    /// <param name="messageId">The message id to start purging from</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public async Task PurgeWhere(ITextChannel channel, ulong amount, Func<IMessage, bool> predicate,
        ulong messageId = 0)
    {
        channel.ThrowIfNull(nameof(channel));
        if (amount <= 0 && messageId is 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        if (!pruningGuilds.Add(channel.GuildId))
            return;
        if (amount > int.MaxValue)
            return;
        try
        {
            IMessage[] msgs;
            if (messageId is 0)
                msgs = (await channel.GetMessagesAsync().FlattenAsync().ConfigureAwait(false)).Where(predicate)
                    .Take((int)amount).ToArray();
            else
            {
                var msg = await channel.GetMessageAsync(messageId);
                if (msg.Timestamp.Subtract(DateTimeOffset.Now).Days == 14)
                    return;
                msgs = (await channel.GetMessagesAsync(messageId, Direction.After).FlattenAsync()).ToArray();
                amount = Convert.ToUInt64(msgs.Length);
            }

            while (amount > 0 && msgs.Length > 0)
            {
                var lastMessage = msgs[^1];

                var bulkDeletable = new List<IMessage>();
                var singleDeletable = new List<IMessage>();
                foreach (var x in msgs)
                {
                    if (DateTime.UtcNow - x.CreatedAt < twoWeeks)
                        bulkDeletable.Add(x);
                    else
                        singleDeletable.Add(x);
                }

                if (bulkDeletable.Count > 0)
                {
                    await Task.WhenAll(Task.Delay(1000), channel.DeleteMessagesAsync(bulkDeletable))
                        .ConfigureAwait(false);
                }

                var i = 0;
                foreach (var group in singleDeletable.GroupBy(_ => ++i / (singleDeletable.Count / 5)))
                {
                    await Task.WhenAll(Task.Delay(1000), Task.WhenAll(group.Select(x => x.DeleteAsync())))
                        .ConfigureAwait(false);
                }

                //this isn't good, because this still work as if i want to remove only specific user's messages from the last
                //100 messages, Maybe this needs to be reduced by msgs.Length instead of 100
                amount -= (ulong)msgs.Length;
                if (amount > 0)
                {
                    msgs = (await channel.GetMessagesAsync(lastMessage, Direction.Before).FlattenAsync()
                        .ConfigureAwait(false)).Where(predicate).Take((int)amount).ToArray();
                }
            }
        }
        catch
        {
            //ignore
        }
        finally
        {
            pruningGuilds.TryRemove(channel.GuildId);
        }
    }
}