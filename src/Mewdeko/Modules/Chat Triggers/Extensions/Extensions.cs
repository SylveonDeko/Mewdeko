using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Html.Dom;
using Mewdeko.Database.DbContextStuff;

namespace Mewdeko.Modules.Chat_Triggers.Extensions;

/// <summary>
///     Extension methods for chat triggers.
/// </summary>
public static class Extensions
{
    private static readonly Regex ImgRegex = new("%(img|image):(?<tag>.*?)%", RegexOptions.Compiled);

    /// <summary>
    ///     Dictionary containing regular expressions and corresponding functions to generate string replacements.
    /// </summary>
    private static readonly Dictionary<Regex, Func<Match, Task<string>>> RegexPlaceholders = new()
    {
        {
            ImgRegex, async match =>
            {
                // Extract the tag from the match
                var tag = match.Groups["tag"].ToString();

                // If the tag is empty or whitespace, return an empty string
                if (string.IsNullOrWhiteSpace(tag))
                    return "";

                // Construct the full query link for imgur search
                var fullQueryLink = $"https://imgur.com/search?q={tag}";

                // Configure the browsing context
                var config = Configuration.Default.WithDefaultLoader();

                // Open the document asynchronously
                using var document = await BrowsingContext.New(config).OpenAsync(fullQueryLink).ConfigureAwait(false);

                // Query all elements matching the image-list-link selector
                var elems = document.QuerySelectorAll("a.image-list-link").ToArray();

                // If no elements found, return empty string
                if (elems.Length == 0)
                    return "";

                // Get a random image element from the list
                var img = elems.ElementAtOrDefault(new MewdekoRandom().Next(0, elems.Length))?.Children
                    ?.FirstOrDefault() as IHtmlImageElement;

                // If img source is null, return empty string; otherwise, return the source URL
                return img?.Source == null
                    ? ""
                    : $" {img.Source.Replace("b.", ".", StringComparison.InvariantCulture)} ";
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
        return str.Replace("%bot.mention%", client.CurrentUser.Mention, StringComparison.Ordinal);
    }


    /// <summary>
    ///     Resolves the response string asynchronously by replacing placeholders with dynamic values.
    /// </summary>
    /// <param name="str">The response string containing placeholders.</param>
    /// <param name="ctx">The message context.</param>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="resolvedTrigger">The resolved trigger string.</param>
    /// <param name="containsAnywhere">Boolean value indicating whether the trigger is contained anywhere in the message.</param>
    /// <param name="dbContext">Optional: The database context. Default is null.</param>
    /// <param name="triggerId">Optional: The ID of the trigger. Default is 0.</param>
    /// <returns>The resolved response string.</returns>
    private static async Task<string?> ResolveResponseStringAsync(this string? str, IUserMessage ctx,
        DiscordShardedClient client, string resolvedTrigger, bool containsAnywhere, DbContextProvider dbProvider = null,
        int triggerId = 0)
    {
        // Calculate the index where the substring begins
        var substringIndex = resolvedTrigger.Length;
        if (containsAnywhere)
        {
            switch (ctx.Content.AsSpan().GetWordPosition(resolvedTrigger))
            {
                case WordPosition.Start:
                    substringIndex++;
                    break;
                case WordPosition.End:
                    substringIndex = ctx.Content.Length;
                    break;
                case WordPosition.Middle:
                    substringIndex += ctx.Content.IndexOf(resolvedTrigger, StringComparison.InvariantCulture);
                    break;
            }
        }

        // Check if mentioning everyone is allowed
        var canMentionEveryone = (ctx.Author as IGuildUser)?.GuildPermissions.MentionEveryone ?? true;
        await using var dbContext = await dbProvider.GetContextAsync();

        // Build the replacement dictionary
        var rep = new ReplacementBuilder()
            .WithDefault(ctx.Author, ctx.Channel, (ctx.Channel as ITextChannel)?.Guild as SocketGuild, client)
            .WithOverride("%target%", () =>
                canMentionEveryone
                    ? ctx.Content[substringIndex..].Trim()
                    : ctx.Content[substringIndex..].Trim().SanitizeMentions(true))
            .WithOverride("%usecount%",
                () => dbContext.CommandStats.Count(x => x.NameOrId == $"{triggerId}").ToString())
            .WithOverride("%targetuser%", () =>
            {
                var mention = ctx.MentionedUserIds.FirstOrDefault();
                return mention is 0 ? "" : mention.ToString();
            })
            .WithOverride("%targetuser.id%", () =>
            {
                var mention = ctx.Content.GetUserMentions().FirstOrDefault();
                if (mention is 0)
                    return "";
                var user = client.GetUser(mention);
                return user is null ? "" : user.Id.ToString();
            })
            .WithOverride("%targetuser.avatar%", () =>
            {
                var mention = ctx.Content.GetUserMentions().FirstOrDefault();
                if (mention is 0)
                    return "";
                var user = client.GetUser(mention);
                return user is null ? "" : user.RealAvatarUrl().ToString();
            })
            .WithOverride("%targetusers%", () =>
            {
                var mention = ctx.Content.GetUserMentions();
                return !mention.Any() ? "" : string.Join(", ", mention.Select(x => $"<@{x}>"));
            })
            .WithOverride("%targetusers.id%", () =>
            {
                var mention = ctx.Content.GetUserMentions();
                if (mention.Any())
                    return "";
                return !mention.Any() ? "" : string.Join(", ", mention);
            })
            .Build();

        // Replace placeholders with dynamic values
        str = rep.Replace(str);
        foreach (var ph in RegexPlaceholders)
        {
            str = await ph.Key.ReplaceAsync(str, ph.Value).ConfigureAwait(false);
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
    /// <param name="context">Optional: The database context. Default is null.</param>
    /// <returns>The response string with context.</returns>
    public static Task<string?> ResponseWithContextAsync(this Database.Models.ChatTriggers cr, IUserMessage ctx,
        DiscordShardedClient client, bool containsAnywhere, DbContextProvider provider = null)
    {
        return cr.Response.ResolveResponseStringAsync(ctx, client, cr.Trigger.ResolveTriggerString(client),
            containsAnywhere, provider, cr.Id);
    }


    /// <summary>
    ///     Sends a message based on the provided chat trigger asynchronously.
    /// </summary>
    /// <param name="ct">The chat trigger model.</param>
    /// <param name="ctx">The message context.</param>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="sanitize">Boolean value indicating whether to sanitize mentions in the response.</param>
    /// <param name="dbContext">Optional: The database context. Default is null.</param>
    /// <returns>The sent user message or null if no response is required.</returns>
    public static async Task<IUserMessage>? Send(this Database.Models.ChatTriggers ct, IUserMessage ctx,
        DiscordShardedClient client, bool sanitize, DbContextProvider dbProvider = null)
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
                if (pos == WordPosition.Start)
                    substringIndex++;
                else if (pos == WordPosition.End)
                    substringIndex = ctx.Content.Length;
                else if (pos == WordPosition.Middle)
                    substringIndex += ctx.Content.IndexOf(trigger, StringComparison.InvariantCulture);
            }

            var canMentionEveryone = (ctx.Author as IGuildUser)?.GuildPermissions.MentionEveryone ?? true;
            await using var dbContext = await dbProvider.GetContextAsync();

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
                .Build();

            SmartEmbed.TryParse(rep.Replace(ct.Response), ct.GuildId, out crembed, out plainText, out components);
            if (sanitize)
                plainText = plainText.SanitizeMentions();

            if (ct.CrosspostingChannelId != 0 && ct.GuildId is not null or 0)
                await client.GetGuild(ct.GuildId ?? 0).GetTextChannel(ct.CrosspostingChannelId)
                    .SendMessageAsync(plainText, embeds: crembed).ConfigureAwait(false);
            else if (!ct.CrosspostingWebhookUrl.IsNullOrWhiteSpace())
            {
                try
                {
                    using var whClient = new DiscordWebhookClient(ct.CrosspostingWebhookUrl);
                    await whClient.SendMessageAsync(plainText,
                        embeds: crembed).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    /* ignored */
                }
            }

            if (ct.NoRespond)
                return null;
            return await channel.SendMessageAsync(plainText, embeds: crembed, components: components?.Build())
                .ConfigureAwait(false);
        }

        var context = (await ct.ResponseWithContextAsync(ctx, client, ct.ContainsAnywhere, dbProvider)
                .ConfigureAwait(false))
            .SanitizeMentions(sanitize);
        if (ct.CrosspostingChannelId != 0 && ct.GuildId is not null or 0)
            await client.GetGuild(ct.GuildId ?? 0).GetTextChannel(ct.CrosspostingChannelId).SendMessageAsync(context)
                .ConfigureAwait(false);
        else if (!ct.CrosspostingWebhookUrl.IsNullOrWhiteSpace())
        {
            try
            {
                using var whClient = new DiscordWebhookClient(ct.CrosspostingWebhookUrl);
                await whClient.SendMessageAsync(context).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                /* ignored */
            }
        }

        if (ct.NoRespond)
            return null;
        return await channel.SendMessageAsync(context).ConfigureAwait(false);
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
    /// <param name="dbContext">Optional: The database context. Default is null.</param>
    /// <param name="followup">Boolean value indicating whether to send a follow-up response. Default is false.</param>
    /// <returns>The sent user message or null if no response is required.</returns>
    public static async Task<IUserMessage>? SendInteraction(this Database.Models.ChatTriggers ct,
        SocketInteraction inter,
        DiscordShardedClient client, bool sanitize, IUserMessage fakeMsg, bool ephemeral = false,
        DbContextProvider dbProvider = null, bool followup = false)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

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
            if (ct.CrosspostingChannelId != 0 && ct.GuildId is not null or 0)
                await client.GetGuild(ct.GuildId ?? 0).GetTextChannel(ct.CrosspostingChannelId)
                    .SendMessageAsync(plainText, embeds: crembed, components: components?.Build())
                    .ConfigureAwait(false);
            else if (!ct.CrosspostingWebhookUrl.IsNullOrWhiteSpace())
            {
                try
                {
                    using var whClient = new DiscordWebhookClient(ct.CrosspostingWebhookUrl);
                    await whClient.SendMessageAsync(plainText,
                        embeds: crembed).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    /* ignored */
                }
            }

            if (ct.NoRespond)
                return null;
            if (!followup)
            {
                await inter.RespondAsync(plainText, crembed, ephemeral: ephemeral,
                    components: components?.Build()).ConfigureAwait(false);
                return await inter.GetOriginalResponseAsync().ConfigureAwait(false);
            }

            return await inter
                .FollowupAsync(plainText, crembed, ephemeral: ephemeral, components: components?.Build())
                .ConfigureAwait(false);
        }


        var context = rep
            .Replace(await ct.ResponseWithContextAsync(fakeMsg, client, ct.ContainsAnywhere, dbProvider)
                .ConfigureAwait(false))
            .SanitizeMentions(sanitize);
        if (ct.CrosspostingChannelId != 0 && ct.GuildId is not null or 0)
            await client.GetGuild(ct.GuildId ?? 0).GetTextChannel(ct.CrosspostingChannelId).SendMessageAsync(context)
                .ConfigureAwait(false);
        else if (!ct.CrosspostingWebhookUrl.IsNullOrWhiteSpace())
        {
            try
            {
                using var whClient = new DiscordWebhookClient(ct.CrosspostingWebhookUrl);
                await whClient.SendMessageAsync(context).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                /* ignored */
            }
        }

        if (ct.NoRespond)
            return null;
        if (followup)
            return await inter.FollowupAsync(context, ephemeral: ephemeral).ConfigureAwait(false);
        await inter.RespondAsync(context, ephemeral: ephemeral).ConfigureAwait(false);
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
        var wordIndex = str.IndexOf(word, StringComparison.OrdinalIgnoreCase);
        switch (wordIndex)
        {
            case -1:
                return WordPosition.None;
            case 0:
            {
                if (word.Length < str.Length && str.IsValidWordDivider(word.Length))
                    return WordPosition.Start;
                break;
            }
            default:
            {
                if (wordIndex + word.Length == str.Length)
                {
                    if (str.IsValidWordDivider(wordIndex - 1))
                        return WordPosition.End;
                }
                else if (str.IsValidWordDivider(wordIndex - 1) && str.IsValidWordDivider(wordIndex + word.Length))
                {
                    return WordPosition.Middle;
                }

                break;
            }
        }

        return WordPosition.None;
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
        switch (str[index])
        {
            case >= 'a' and <= 'z':
            case >= 'A' and <= 'Z':
            case >= '1' and <= '9':
                return false;
            default:
                return true;
        }
    }
}

/// <summary>
///     Enumerates the positions of a word within a string.
/// </summary>
public enum WordPosition
{
    /// <summary>
    ///     The word is not found or does not have a valid position within the string.
    /// </summary>
    None,

    /// <summary>
    ///     The word is found at the start of the string.
    /// </summary>
    Start,

    /// <summary>
    ///     The word is found in the middle of the string.
    /// </summary>
    Middle,

    /// <summary>
    ///     The word is found at the end of the string.
    /// </summary>
    End
}