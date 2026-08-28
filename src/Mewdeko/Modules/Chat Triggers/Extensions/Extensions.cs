using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using AngleSharp;
using AngleSharp.Html.Dom;
using LinqToDB.Async;
using Mewdeko.Common.DiscordImplementations;
using Mewdeko.Common.TriggerPlaceholders;
using Serilog;
using ChatTrigger = DataModel.ChatTrigger;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Chat_Triggers.Extensions;

/// <summary>
///     Extension methods for chat triggers.
/// </summary>
public static class Extensions
{
    private static readonly Regex ImgRegex = new("%(img|image):(?<tag>.*?)%",
        RegexOptions.Compiled | RegexOptions.IgnoreCase); // Added IgnoreCase

    private static readonly Regex
        RandomRegex = new("%random:(?<min>\\d+),(?<max>\\d+)%", RegexOptions.Compiled); // Added for %random%

    /// <summary>
    ///     Matches %regex.n% and %regex.name% placeholders, which expose the capture groups of a regex trigger.
    /// </summary>
    private static readonly Regex RegexGroupRegex = new(@"%regex\.(?<group>[\w]+)%",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    ///     Dictionary containing regular expressions and corresponding functions to generate string replacements.
    /// </summary>
    private static readonly Dictionary<Regex, Func<Match, Task<string>>> RegexPlaceholders = new()
    {
        {
            ImgRegex, async match =>
            {
                var tag = match.Groups["tag"].ToString();
                return string.IsNullOrWhiteSpace(tag) ? "" : await GetImgurImageAsync(tag).ConfigureAwait(false);
            }
        }
    };


    /// <summary>
    ///     Results of recent Imgur searches, so a popular trigger does not scrape once per fire.
    /// </summary>
    private static readonly ConcurrentDictionary<string, (DateTime Fetched, string[] Urls)> ImgurCache = new();

    /// <summary>
    ///     How long a cached Imgur search stays usable.
    /// </summary>
    private static readonly TimeSpan ImgurCacheDuration = TimeSpan.FromMinutes(30);

    /// <summary>
    ///     How long a single Imgur search may take before it is abandoned.
    /// </summary>
    private static readonly TimeSpan ImgurTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Picks a random Imgur image for a tag, caching the search results.
    /// </summary>
    /// <param name="tag">The search tag from the %img:tag% placeholder.</param>
    /// <returns>A padded image URL, or an empty string when nothing could be resolved.</returns>
    /// <remarks>
    ///     This runs inside message handling, so it is bounded by a timeout and served from a short-lived cache. A
    ///     failed or slow lookup resolves to an empty string rather than delaying or failing the response.
    /// </remarks>
    private static async Task<string> GetImgurImageAsync(string tag)
    {
        try
        {
            if (!ImgurCache.TryGetValue(tag, out var cached) ||
                DateTime.UtcNow - cached.Fetched > ImgurCacheDuration)
            {
                var fullQueryLink = $"https://imgur.com/search?q={Uri.EscapeDataString(tag)}";
                var config = Configuration.Default.WithDefaultLoader();

                using var cts = new CancellationTokenSource(ImgurTimeout);
                using var document = await BrowsingContext.New(config)
                    .OpenAsync(fullQueryLink, cts.Token)
                    .ConfigureAwait(false);

                var urls = document.QuerySelectorAll("a.image-list-link")
                    .Select(x => (x.Children?.FirstOrDefault() as IHtmlImageElement)?.Source)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!.Replace("b.", ".", StringComparison.InvariantCulture))
                    .ToArray();

                cached = (DateTime.UtcNow, urls);
                ImgurCache[tag] = cached;
            }

            return cached.Urls.Length == 0
                ? ""
                : $" {cached.Urls[Random.Shared.Next(0, cached.Urls.Length)]} ";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error retrieving Imgur image for tag: {Tag}", tag);
            return "";
        }
    }

    /// <summary>
    ///     Resolves trigger string by replacing %bot.mention% placeholder with the current user's mention.
    /// </summary>
    /// <param name="str">The trigger string containing the placeholder.</param>
    /// <param name="client">The Discord socket client.</param>
    /// <returns>The trigger string with the placeholder replaced.</returns>
    private static string ResolveTriggerString(this string str, DiscordShardedClient client)
    {
        return str.Replace("%bot.mention%", client.CurrentUser?.Mention ?? "",
            StringComparison.Ordinal); // Handle potential null CurrentUser
    }

    /// <summary>
    ///     Resolves the response string asynchronously by replacing placeholders with dynamic values.
    /// </summary>
    /// <param name="str">The response string containing placeholders.</param>
    /// <param name="ctx">The message context.</param>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="resolvedTrigger">The resolved trigger string.</param>
    /// <param name="containsAnywhere">Boolean value indicating whether the trigger is contained anywhere in the message.</param>
    /// <param name="dbFactory">Optional: The LinqToDB database factory. Default is null.</param>
    /// <param name="triggerId">Optional: The ID of the trigger. Default is 0.</param>
    /// <returns>The resolved response string.</returns>
    private static async Task<string?> ResolveResponseStringAsync(this string? str, IUserMessage ctx,
        DiscordShardedClient client, string resolvedTrigger, bool containsAnywhere,
        IDataConnectionFactory? dbFactory = null,
        int triggerId = 0)
    {
        if (string.IsNullOrWhiteSpace(str)) return str; // Return early if string is empty

        var substringIndex = resolvedTrigger.Length;
        if (containsAnywhere && !string.IsNullOrEmpty(ctx.Content)) // Ensure content exists
        {
            var index = ctx.Content.IndexOf(resolvedTrigger, StringComparison.OrdinalIgnoreCase); // Use IgnoreCase
            if (index != -1)
            {
                var pos = ctx.Content.AsSpan().GetWordPosition(resolvedTrigger.AsSpan()); // Use span overload
                switch (pos)
                {
                    case WordPosition.Start:
                        substringIndex = index + resolvedTrigger.Length + 1;
                        break; // Adjust index based on actual position
                    case WordPosition.End: substringIndex = index; break; // Take content before trigger
                    case WordPosition.Middle:
                        substringIndex = index + resolvedTrigger.Length + 1;
                        break; // Adjust index based on actual position
                    default:
                        substringIndex = ctx.Content.Length;
                        break; // If not a whole word match, take everything? Or just from end? Let's take from end.
                }
            }
            else
            {
                substringIndex = 0; // Safer default for %target% if trigger isn't found with ContainsAnywhere
            }
        }

        var canMentionEveryone = (ctx.Author as IGuildUser)?.GuildPermissions.MentionEveryone ?? true;
        var textChannel = ctx.Channel as ITextChannel;
        var guild = textChannel?.Guild as SocketGuild;
        var repliedMessage = await GetRepliedMessageAsync(ctx).ConfigureAwait(false);

        var useCountStr = "0"; // Default use count
        if (dbFactory != null && triggerId > 0)
        {
            try
            {
                await using var db = await dbFactory.CreateConnectionAsync();
                // Only trigger fires count, and only in this guild: a plain command whose name happens to be the
                // same number would otherwise inflate the total
                var guildId = (ctx.Channel as ITextChannel)?.GuildId ?? 0;
                var count = await db.CommandStats
                    .CountAsync(x => x.Trigger && x.GuildId == guildId && x.NameOrId == $"{triggerId}")
                    .ConfigureAwait(false);
                useCountStr = count.ToString();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to get use count for trigger {TriggerId}", triggerId);
            }
        }

        var rep = new ReplacementBuilder()
            .WithDefault(ctx.Author, ctx.Channel, guild, client)
            .WithOverride("%target%", () =>
            {
                var content = ctx.Content ?? "";
                var targetText = substringIndex <= content.Length ? content[substringIndex..].Trim() : "";
                return canMentionEveryone ? targetText : targetText.SanitizeMentions(true);
            })
            .WithOverride("%usecount%", () => useCountStr)
            .WithOverride("%targetuser%",
                () => ctx.MentionedUserIds.FirstOrDefault() is var userId && userId != 0 ? $"<@{userId}>" : "")
            .WithOverride("%targetuser.id%", () => ctx.MentionedUserIds.FirstOrDefault().ToString())
            .WithOverride("%targetuser.name%",
                () => client.GetUser(ctx.MentionedUserIds.FirstOrDefault())?.Username ?? "")
            .WithOverride("%targetuser.avatar%",
                () => client.GetUser(ctx.MentionedUserIds.FirstOrDefault())?.RealAvatarUrl().ToString() ?? "")
            .WithOverride("%targetusers%", () => string.Join(", ", ctx.MentionedUserIds.Select(x => $"<@{x}>")))
            .WithOverride("%targetusers.id%", () => string.Join(", ", ctx.MentionedUserIds))
            .WithOverride("%replied.content%", () => repliedMessage?.Content ?? "")
            .WithOverride("%replied.author%", () => repliedMessage?.Author.Mention ?? "")
            .WithOverride("%replied.author.id%", () => repliedMessage?.Author.Id.ToString() ?? "")
            .Build();

        str = rep.Replace(str);
        foreach (var ph in RegexPlaceholders)
        {
            try
            {
                str = await ph.Key.ReplaceAsync(str ?? "", ph.Value).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error processing Regex Placeholder {Regex}", ph.Key);
            }
        }

        return str;
    }

    /// <summary>
    ///     Generates a response string with context asynchronously based on the provided parameters.
    /// </summary>
    /// <param name="cr">The chat trigger model.</param>
    /// <param name="ctx">The message context.</param>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="containsAnywhere">Boolean value indicating whether the trigger is contained anywhere in the message.</param>
    /// <param name="dbFactory">Optional: The LinqToDB database factory. Default is null.</param>
    /// <param name="response">
    ///     Optional: a response that has already had contextual placeholders resolved. Defaults to the trigger's stored
    ///     response.
    /// </param>
    /// <returns>The response string with context.</returns>
    public static Task<string?> ResponseWithContextAsync(this ChatTrigger cr, IUserMessage ctx,
        DiscordShardedClient client, bool containsAnywhere, IDataConnectionFactory? dbFactory = null,
        string? response = null)
    {
        return (response ?? cr.Response).ResolveResponseStringAsync(ctx, client,
            cr.Trigger.ResolveTriggerString(client), containsAnywhere, dbFactory, cr.Id);
    }


    /// <summary>
    ///     Sends a message based on the provided chat trigger asynchronously.
    /// </summary>
    /// <param name="ct">The chat trigger model.</param>
    /// <param name="ctx">The message context.</param>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="sanitize">Boolean value indicating whether to sanitize mentions in the response.</param>
    /// <param name="dbProvider">Optional: The database context. Default is null.</param>
    /// <param name="placeholders">
    ///     Optional: the contextual placeholder service, used to resolve placeholders contributed by other modules such
    ///     as XP or currency.
    /// </param>
    /// <param name="regexMatch">
    ///     Optional: the regex match that fired the trigger, whose capture groups are exposed as %regex.n% and
    ///     %regex.name% placeholders.
    /// </param>
    /// <param name="responseOverride">
    ///     Optional: the response to send instead of the trigger's primary response, used when a trigger defines
    ///     several responses.
    /// </param>
    /// <returns>The sent user message or null if no response is required.</returns>
    public static async Task<IUserMessage>? Send(this ChatTrigger ct, IUserMessage ctx,
        DiscordShardedClient client, bool sanitize, IDataConnectionFactory dbProvider = null,
        TriggerPlaceholderService? placeholders = null, Match? regexMatch = null,
        string? responseOverride = null)
    {
        var channel = ct.DmResponse
            ? await ctx.Author.CreateDMChannelAsync().ConfigureAwait(false)
            : ctx.Channel;

        var response = await ct.ResolveContextualPlaceholdersAsync(placeholders, client,
            (ctx.Channel as IGuildChannel)?.Guild, ctx.Channel, ctx.Author,
            ctx.MentionedUserIds.FirstOrDefault(), regexMatch, responseOverride).ConfigureAwait(false);

        if (SmartEmbed.TryParse(response, ct.GuildId, out var crembed, out var plainText, out var components))
        {
            var trigger = ct.Trigger.ResolveTriggerString(client);
            var substringIndex = trigger.Length;
            if (ct.ContainsAnywhere)
            {
                var pos = ctx.Content.AsSpan().GetWordPosition(trigger);
                switch (pos)
                {
                    case WordPosition.Start:
                        substringIndex++;
                        break;
                    case WordPosition.End:
                        substringIndex = ctx.Content.Length;
                        break;
                    case WordPosition.Middle:
                        substringIndex += ctx.Content.IndexOf(trigger, StringComparison.InvariantCulture);
                        break;
                }
            }

            var canMentionEveryone = (ctx.Author as IGuildUser)?.GuildPermissions.MentionEveryone ?? true;
            await using var dbContext = await dbProvider.CreateConnectionAsync();
            var repliedMessage = await GetRepliedMessageAsync(ctx).ConfigureAwait(false);

            var rep = new ReplacementBuilder()
                .WithDefault(ctx.Author, ctx.Channel, (ctx.Channel as ITextChannel)?.Guild as SocketGuild, client)
                .WithOverride("%target%", () => canMentionEveryone
                    ? ctx.Content[substringIndex..].Trim()
                    : ctx.Content[substringIndex..].Trim().SanitizeMentions(true))
                .WithOverride("%usecount%", () => dbContext.CommandStats
                    .Count(x => x.Trigger && x.GuildId == (ct.GuildId ?? 0) && x.NameOrId == $"{ct.Id}")
                    .ToString())
                .WithOverride("%targetuser%", () =>
                {
                    var mention = ctx.MentionedUserIds.FirstOrDefault();
                    if (mention is 0)
                        return "";
                    var user = client.GetUser(mention);
                    return user is null ? "" : user.Mention;
                })
                .WithOverride("%targetuser.id%", () =>
                {
                    var mention = ctx.MentionedUserIds.FirstOrDefault();
                    if (mention is 0)
                        return "";
                    var user = client.GetUser(mention);
                    return user is null ? "" : user.Id.ToString();
                })
                .WithOverride("%targetuser.avatar%", () =>
                {
                    var mention = ctx.MentionedUserIds.FirstOrDefault();
                    if (mention is 0)
                        return "";
                    var user = client.GetUser(mention);
                    return user is null ? "" : user.RealAvatarUrl().ToString();
                })
                .WithOverride("%replied.content%", () => repliedMessage?.Content ?? "")
                .WithOverride("%replied.author%", () => repliedMessage?.Author.Mention ?? "")
                .Build();

            SmartEmbed.TryParse(rep.Replace(response), ct.GuildId, out crembed, out plainText, out components);
            if (sanitize)
                plainText = plainText.SanitizeMentions();

            await ct.SendCrosspostAsync(client, plainText, crembed).ConfigureAwait(false);

            if (ct.NoRespond)
                return null;

            var sent = await channel.SendMessageAsync(plainText, embeds: crembed,
                    components: components?.Build(), messageReference: ct.BuildReference(ctx, channel))
                .ConfigureAwait(false);

            ct.QueueResponseDeletion(sent);
            return sent;
        }

        var context = (await ct.ResponseWithContextAsync(ctx, client, ct.ContainsAnywhere, dbProvider, response)
                .ConfigureAwait(false))
            .SanitizeMentions(sanitize);
        await ct.SendCrosspostAsync(client, context).ConfigureAwait(false);

        if (ct.NoRespond)
            return null;

        var sentMsg = await channel
            .SendMessageAsync(context, messageReference: ct.BuildReference(ctx, channel))
            .ConfigureAwait(false);

        ct.QueueResponseDeletion(sentMsg);
        return sentMsg;
    }

    /// <summary>
    ///     Builds the message reference that makes a response reply to the message that fired it.
    /// </summary>
    /// <param name="ct">The trigger being sent.</param>
    /// <param name="ctx">The message that fired the trigger.</param>
    /// <param name="channel">The channel the response is going to.</param>
    /// <returns>The reference to reply with, or null when the trigger should not reply.</returns>
    /// <remarks>
    ///     A reply only makes sense when the response lands in the same channel as the trigger, so a DM response or a
    ///     response driven by an event never replies. The reference is built as a soft reply: if the original message
    ///     is gone, Discord sends it as a normal message rather than failing.
    /// </remarks>
    private static MessageReference? BuildReference(this ChatTrigger ct, IUserMessage ctx, IMessageChannel channel)
    {
        if (!ct.ReplyToTrigger || ct.DmResponse || ctx is MewdekoUserMessage || ctx.Channel?.Id != channel.Id)
            return null;

        return new MessageReference(ctx.Id, channel.Id, failIfNotExists: false);
    }

    /// <summary>
    ///     Schedules a response for deletion when the trigger is configured to clean up after itself.
    /// </summary>
    /// <param name="ct">The trigger that was sent.</param>
    /// <param name="msg">The response message.</param>
    private static void QueueResponseDeletion(this ChatTrigger ct, IUserMessage? msg)
    {
        if (msg is null || ct.DeleteResponseAfter <= 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(ct.DeleteResponseAfter)).ConfigureAwait(false);
                await msg.DeleteAsync().ConfigureAwait(false);
            }
            catch
            {
                // The response may already be gone, or the bot may have lost access to the channel
            }
        });
    }

    private static async Task<IMessage?> GetRepliedMessageAsync(IUserMessage ctx)
    {
        return ctx.Reference?.MessageId.IsSpecified == true
            ? await ctx.Channel.GetMessageAsync(ctx.Reference.MessageId.Value).ConfigureAwait(false)
            : null;
    }

    /// <summary>
    ///     Sends a message based on the provided chat trigger and interaction asynchronously.
    /// </summary>
    /// <param name="ct">The chat trigger model.</param>
    /// <param name="inter">The socket interaction.</param>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="sanitize">Boolean value indicating whether to sanitize mentions in the response.</param>
    /// <param name="fakeMsg">The fake user message for context.</param>
    /// <param name="ephemeral">Boolean value indicating whether the response should be ephemeral. Default is false.</param>
    /// <param name="dbProvider">Optional: The database context. Default is null.</param>
    /// <param name="followup">Boolean value indicating whether to send a follow-up response. Default is false.</param>
    /// <param name="placeholders">
    ///     Optional: the contextual placeholder service, used to resolve placeholders contributed by other modules such
    ///     as XP or currency.
    /// </param>
    /// <param name="responseOverride">
    ///     Optional: the response to send instead of the trigger's primary response, used when a trigger defines
    ///     several responses.
    /// </param>
    /// <returns>The sent user message or null if no response is required.</returns>
    public static async Task<IUserMessage>? SendInteraction(this ChatTrigger ct,
        SocketInteraction inter,
        DiscordShardedClient client, bool sanitize, IUserMessage fakeMsg, bool ephemeral = false,
        IDataConnectionFactory dbProvider = null, bool followup = false,
        TriggerPlaceholderService? placeholders = null, string? responseOverride = null)
    {
        await using var dbContext = await dbProvider.CreateConnectionAsync();

        var interactionTarget = inter switch
        {
            IMessageCommandInteraction mData => mData.Data.Message.Author.Id,
            IUserCommandInteraction uData => uData.Data.User.Id,
            _ => 0ul
        };

        var response = await ct.ResolveContextualPlaceholdersAsync(placeholders, client,
            (inter.Channel as IGuildChannel)?.Guild, inter.Channel, inter.User, interactionTarget, null,
            responseOverride).ConfigureAwait(false);

        var rep = new ReplacementBuilder()
            .WithDefault(inter.User, inter.Channel, (inter.Channel as ITextChannel)?.Guild as SocketGuild, client)
            .WithOverride("%target%", () => inter switch
            {
                IMessageCommandInteraction mData => mData.Data.Message.Content.SanitizeAllMentions(),
                IUserCommandInteraction uData => uData.Data.User.Mention,
                _ => "%target%"
            })
            .WithOverride("%usecount%", () => dbContext.CommandStats
                .Count(x => x.Trigger && x.GuildId == (ct.GuildId ?? 0) && x.NameOrId == $"{ct.Id}")
                .ToString())
            .WithOverride("%targetuser%", () => inter switch
            {
                IMessageCommandInteraction mData => $"{mData.Data.Message.Author.Mention}",
                IUserCommandInteraction uData => $"{uData.Data.User.Mention}",
                _ => "%targetuser%"
            })
            .WithOverride("%targetuser.id%", () => inter switch
            {
                IMessageCommandInteraction mData => $"{mData.Data.Message.Author.Id}",
                IUserCommandInteraction uData => $"{uData.Data.User.Id}",
                _ => "%targetuser.id%"
            })
            .WithOverride("%targetuser.avatar%", () => inter switch
            {
                IMessageCommandInteraction mData => $"{mData.Data.Message.Author.RealAvatarUrl()}",
                IUserCommandInteraction uData => $"{uData.Data.User.RealAvatarUrl()}",
                _ => "%targetuser.avatar%"
            })
            .Build();
        if (SmartEmbed.TryParse(response, ct.GuildId, out var crembed, out var plainText, out var components))
        {
            SmartEmbed.TryParse(rep.Replace(response), ct.GuildId, out crembed, out plainText, out components);
            if (sanitize)
                plainText = plainText.SanitizeMentions();
            await ct.SendCrosspostAsync(client, plainText, crembed, components).ConfigureAwait(false);

            if (ct.NoRespond)
                return null;
            return await SendInteractionResponseAsync(inter, plainText, ephemeral, followup, crembed, components)
                .ConfigureAwait(false);
        }


        var context = rep
            .Replace(await ct.ResponseWithContextAsync(fakeMsg, client, ct.ContainsAnywhere, dbProvider, response)
                .ConfigureAwait(false))
            .SanitizeMentions(sanitize);
        await ct.SendCrosspostAsync(client, context).ConfigureAwait(false);

        if (ct.NoRespond)
            return null;
        return await SendInteractionResponseAsync(inter, context, ephemeral, followup).ConfigureAwait(false);
    }

    /// <summary>
    ///     Resolves placeholders contributed by other modules against the invocation context.
    /// </summary>
    /// <param name="ct">The chat trigger whose response is being resolved.</param>
    /// <param name="placeholders">The contextual placeholder service, or null to skip the pass.</param>
    /// <param name="client">The Discord socket client, used to resolve the targeted user.</param>
    /// <param name="guild">The guild the trigger fired in, if any.</param>
    /// <param name="channel">The channel the trigger fired in.</param>
    /// <param name="user">The user that fired the trigger.</param>
    /// <param name="targetUserId">The id of the user the invocation targets, or zero when there is none.</param>
    /// <param name="regexMatch">The regex match that fired the trigger, if it was a regex trigger.</param>
    /// <param name="responseOverride">The response to resolve instead of the trigger's primary response.</param>
    /// <returns>The response with contextual placeholders replaced.</returns>
    private static async Task<string?> ResolveContextualPlaceholdersAsync(this ChatTrigger ct,
        TriggerPlaceholderService? placeholders, DiscordShardedClient client, IGuild? guild, IMessageChannel? channel,
        IUser user, ulong targetUserId, Match? regexMatch = null, string? responseOverride = null)
    {
        var response = (responseOverride ?? ct.Response).ApplyRegexGroups(regexMatch);

        if (placeholders is null || string.IsNullOrWhiteSpace(response))
            return response;

        var target = targetUserId == 0 ? null : client.GetUser(targetUserId);
        var context = new TriggerPlaceholderContext(guild, channel, user, target);

        return await placeholders.ReplaceAsync(response, context).ConfigureAwait(false);
    }

    /// <summary>
    ///     Replaces %regex.n% and %regex.name% placeholders with the corresponding capture groups of a regex match.
    /// </summary>
    /// <param name="response">The response to substitute into.</param>
    /// <param name="match">The match whose groups should be substituted, or null to strip the placeholders.</param>
    /// <returns>The response with regex group placeholders replaced.</returns>
    /// <remarks>
    ///     Placeholders that name a group the pattern does not define resolve to an empty string rather than being left
    ///     in the response, so a response never leaks its own placeholder syntax to chat.
    /// </remarks>
    private static string? ApplyRegexGroups(this string? response, Match? match)
    {
        if (string.IsNullOrWhiteSpace(response) || !response.Contains("%regex.", StringComparison.OrdinalIgnoreCase))
            return response;

        return RegexGroupRegex.Replace(response, m =>
        {
            if (match is null || !match.Success)
                return "";

            var group = match.Groups[m.Groups["group"].Value];
            return group.Success ? group.Value : "";
        });
    }

    private static async Task SendCrosspostAsync(this ChatTrigger ct, DiscordShardedClient client, string? plainText,
        Embed[]? embeds = null, ComponentBuilder? components = null)
    {
        if (ct.CrosspostingChannelId != 0 && ct.GuildId is > 0)
        {
            await client.GetGuild(ct.GuildId ?? 0).GetTextChannel(ct.CrosspostingChannelId)
                .SendMessageAsync(plainText, embeds: embeds, components: components?.Build())
                .ConfigureAwait(false);
            return;
        }

        if (ct.CrosspostingWebhookUrl.IsNullOrWhiteSpace())
            return;

        try
        {
            using var whClient = new DiscordWebhookClient(ct.CrosspostingWebhookUrl);
            await whClient.SendMessageAsync(plainText, embeds: embeds).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            /* ignored */
        }
    }

    private static async Task<IUserMessage> SendInteractionResponseAsync(SocketInteraction inter, string? plainText,
        bool ephemeral, bool followup, Embed[]? embeds = null, ComponentBuilder? components = null)
    {
        if (followup)
            return await inter.FollowupAsync(plainText, embeds, ephemeral: ephemeral, components: components?.Build())
                .ConfigureAwait(false);

        await inter.RespondAsync(plainText, embeds, ephemeral: ephemeral, components: components?.Build())
            .ConfigureAwait(false);
        return await inter.GetOriginalResponseAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets the position of a word within a string.
    /// </summary>
    /// <param name="str">The input string.</param>
    /// <param name="word">The word to search for.</param>
    /// <returns>The position of the word within the string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WordPosition GetWordPosition(this ReadOnlySpan<char> str, in ReadOnlySpan<char> word)
    {
        var wordIndex = str.IndexOf(word, StringComparison.OrdinalIgnoreCase); // Use IgnoreCase for matching
        switch (wordIndex)
        {
            case -1: return WordPosition.None;
            case 0:
                return word.Length == str.Length || word.Length < str.Length && str.IsValidWordDivider(word.Length)
                    ? WordPosition.Start
                    : WordPosition.None;
            default:
            {
                var endReached = wordIndex + word.Length == str.Length;
                var startValid = str.IsValidWordDivider(wordIndex - 1);
                if (endReached) return startValid ? WordPosition.End : WordPosition.None;
                return startValid && str.IsValidWordDivider(wordIndex + word.Length)
                    ? WordPosition.Middle
                    : WordPosition.None;
            }
        }
    }

    /// <summary>
    ///     Determines whether the character at the specified index is a valid word divider.
    /// </summary>
    /// <param name="str">The input string.</param>
    /// <param name="index">The index of the character to check.</param>
    /// <returns>
    ///     <see langword="true" /> if the character at the specified index is a valid word divider; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    private static bool IsValidWordDivider(this in ReadOnlySpan<char> str, int index)
    {
        if ((uint)index >= (uint)str.Length) return true; // Treat bounds as dividers

        var ch = str[index];
        // Check common punctuation, whitespace, or CJK characters as dividers
        return !char.IsLetterOrDigit(ch);
    }
}

/// <summary>
///     Enumerates the positions of a word within a string.
/// </summary>
public enum WordPosition
{
    /// <summary>The word is not found or not separated by word dividers.</summary>
    None,

    /// <summary>The word is found at the start of the string, followed by a divider.</summary>
    Start,

    /// <summary>The word is found surrounded by dividers.</summary>
    Middle,

    /// <summary>The word is found at the end of the string, preceded by a divider.</summary>
    End
}