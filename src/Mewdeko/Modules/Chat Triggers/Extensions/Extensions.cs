using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Html.Dom;
using LinqToDB.Async;
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
    ///     Dictionary containing regular expressions and corresponding functions to generate string replacements.
    /// </summary>
    private static readonly Dictionary<Regex, Func<Match, Task<string>>> RegexPlaceholders = new()
    {
        {
            ImgRegex, async match =>
            {
                var tag = match.Groups["tag"].ToString();
                if (string.IsNullOrWhiteSpace(tag))
                    return "";

                var fullQueryLink = $"https://imgur.com/search?q={Uri.EscapeDataString(tag)}";
                var config = Configuration.Default.WithDefaultLoader();
                try
                {
                    using var document =
                        await BrowsingContext.New(config).OpenAsync(fullQueryLink).ConfigureAwait(false);
                    var elems = document.QuerySelectorAll("a.image-list-link").ToArray();
                    if (elems.Length == 0) return "";
                    var img = elems.ElementAtOrDefault(new Random().Next(0, elems.Length))?.Children
                        ?.FirstOrDefault() as IHtmlImageElement;
                    return img?.Source == null
                        ? ""
                        : $" {img.Source.Replace("b.", ".", StringComparison.InvariantCulture)} ";
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error retrieving Imgur image for tag: {Tag}", tag);
                    return "";
                }
            }
        }
    };


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
                var count = await db.CommandStats
                    .CountAsync(x => x.NameOrId == $"{triggerId}")
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
    /// <returns>The response string with context.</returns>
    public static Task<string?> ResponseWithContextAsync(this ChatTrigger cr, IUserMessage ctx,
        DiscordShardedClient client, bool containsAnywhere, IDataConnectionFactory? dbFactory = null)
    {
        return cr.Response.ResolveResponseStringAsync(ctx, client, cr.Trigger.ResolveTriggerString(client),
            containsAnywhere, dbFactory, cr.Id);
    }


    /// <summary>
    ///     Sends a message based on the provided chat trigger asynchronously.
    /// </summary>
    /// <param name="ct">The chat trigger model.</param>
    /// <param name="ctx">The message context.</param>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="sanitize">Boolean value indicating whether to sanitize mentions in the response.</param>
    /// <param name="dbProvider">Optional: The database context. Default is null.</param>
    /// <returns>The sent user message or null if no response is required.</returns>
    public static async Task<IUserMessage>? Send(this ChatTrigger ct, IUserMessage ctx,
        DiscordShardedClient client, bool sanitize, IDataConnectionFactory dbProvider = null)
    {
        var channel = ct.DmResponse
            ? await ctx.Author.CreateDMChannelAsync().ConfigureAwait(false)
            : ctx.Channel;

        if (SmartEmbed.TryParse(ct.Response, ct.GuildId, out var crembed, out var plainText, out var components))
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
                .WithOverride("%usecount%",
                    () => dbContext.CommandStats.Count(x => x.NameOrId == $"{ct.Id}").ToString())
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

            SmartEmbed.TryParse(rep.Replace(ct.Response), ct.GuildId, out crembed, out plainText, out components);
            if (sanitize)
                plainText = plainText.SanitizeMentions();

            await ct.SendCrosspostAsync(client, plainText, crembed).ConfigureAwait(false);

            if (ct.NoRespond)
                return null;
            return await channel.SendMessageAsync(plainText, embeds: crembed, components: components?.Build())
                .ConfigureAwait(false);
        }

        var context = (await ct.ResponseWithContextAsync(ctx, client, ct.ContainsAnywhere, dbProvider)
                .ConfigureAwait(false))
            .SanitizeMentions(sanitize);
        await ct.SendCrosspostAsync(client, context).ConfigureAwait(false);

        if (ct.NoRespond)
            return null;
        return await channel.SendMessageAsync(context).ConfigureAwait(false);
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
    /// <returns>The sent user message or null if no response is required.</returns>
    public static async Task<IUserMessage>? SendInteraction(this ChatTrigger ct,
        SocketInteraction inter,
        DiscordShardedClient client, bool sanitize, IUserMessage fakeMsg, bool ephemeral = false,
        IDataConnectionFactory dbProvider = null, bool followup = false)
    {
        await using var dbContext = await dbProvider.CreateConnectionAsync();

        var rep = new ReplacementBuilder()
            .WithDefault(inter.User, inter.Channel, (inter.Channel as ITextChannel)?.Guild as SocketGuild, client)
            .WithOverride("%target%", () => inter switch
            {
                IMessageCommandInteraction mData => mData.Data.Message.Content.SanitizeAllMentions(),
                IUserCommandInteraction uData => uData.Data.User.Mention,
                _ => "%target%"
            })
            .WithOverride("%usecount%", dbContext.CommandStats.Count(x => x.NameOrId == $"{ct.Id}").ToString)
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
        if (SmartEmbed.TryParse(ct.Response, ct.GuildId, out var crembed, out var plainText, out var components))
        {
            SmartEmbed.TryParse(rep.Replace(ct.Response), ct.GuildId, out crembed, out plainText, out components);
            if (sanitize)
                plainText = plainText.SanitizeMentions();
            await ct.SendCrosspostAsync(client, plainText, crembed, components).ConfigureAwait(false);

            if (ct.NoRespond)
                return null;
            return await SendInteractionResponseAsync(inter, plainText, ephemeral, followup, crembed, components)
                .ConfigureAwait(false);
        }


        var context = rep
            .Replace(await ct.ResponseWithContextAsync(fakeMsg, client, ct.ContainsAnywhere, dbProvider)
                .ConfigureAwait(false))
            .SanitizeMentions(sanitize);
        await ct.SendCrosspostAsync(client, context).ConfigureAwait(false);

        if (ct.NoRespond)
            return null;
        return await SendInteractionResponseAsync(inter, context, ephemeral, followup).ConfigureAwait(false);
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