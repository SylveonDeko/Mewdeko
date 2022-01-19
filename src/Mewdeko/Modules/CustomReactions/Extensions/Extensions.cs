using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Html.Dom;
using Discord;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Replacements;
using Mewdeko.Services.Database.Models;

namespace Mewdeko.Modules.CustomReactions.Extensions;

public static class Extensions
{
    private static readonly Regex _imgRegex = new("%(img|image):(?<tag>.*?)%", RegexOptions.Compiled);

    private static Dictionary<Regex, Func<Match, Task<string>>> RegexPlaceholders { get; } = new()
    {
        {
            _imgRegex, async match =>
            {
                var tag = match.Groups["tag"].ToString();
                if (string.IsNullOrWhiteSpace(tag))
                    return "";

                var fullQueryLink = $"http://imgur.com/search?q={tag}";
                var config = Configuration.Default.WithDefaultLoader();
                using var document =
                    await BrowsingContext.New(config).OpenAsync(fullQueryLink).ConfigureAwait(false);
                var elems = document.QuerySelectorAll("a.image-list-link").ToArray();

                if (!elems.Any())
                    return "";

                var img = elems.ElementAtOrDefault(new MewdekoRandom().Next(0, elems.Length))?.Children
                    ?.FirstOrDefault() as IHtmlImageElement;

                if (img?.Source == null)
                    return "";

                return " " + img.Source.Replace("b.", ".", StringComparison.InvariantCulture) + " ";
            }
        }
    };

    private static string ResolveTriggerString(this string str, DiscordSocketClient client) => str.Replace("%bot.mention%", client.CurrentUser.Mention, StringComparison.Ordinal);

    private static async Task<string> ResolveResponseStringAsync(this string str, IUserMessage ctx,
        DiscordSocketClient client, string resolvedTrigger, bool containsAnywhere)
    {
        var substringIndex = resolvedTrigger.Length;
        if (containsAnywhere)
        {
            var pos = ctx.Content.AsSpan().GetWordPosition(resolvedTrigger);
            if (pos == WordPosition.Start)
                substringIndex += 1;
            else if (pos == WordPosition.End)
                substringIndex = ctx.Content.Length;
            else if (pos == WordPosition.Middle)
                substringIndex += ctx.Content.IndexOf(resolvedTrigger, StringComparison.InvariantCulture);
        }

        var canMentionEveryone = (ctx.Author as IGuildUser)?.GuildPermissions.MentionEveryone ?? true;

        var rep = new ReplacementBuilder()
            .WithDefault(ctx.Author, ctx.Channel, (ctx.Channel as ITextChannel)?.Guild as SocketGuild, client)
            .WithOverride("%target%", () =>
                canMentionEveryone
                    ? ctx.Content[substringIndex..].Trim()
                    : ctx.Content[substringIndex..].Trim().SanitizeMentions(true))
            .Build();

        str = rep.Replace(str);
        foreach (var ph in RegexPlaceholders)
        {
            str = await ph.Key.ReplaceAsync(str, ph.Value).ConfigureAwait(false);
        }
        return str;
    }

    public static Task<string> ResponseWithContextAsync(this CustomReaction cr, IUserMessage ctx,
        DiscordSocketClient client, bool containsAnywhere) =>
        cr.Response.ResolveResponseStringAsync(ctx, client, cr.Trigger.ResolveTriggerString(client),
            containsAnywhere);

    public static async Task<IUserMessage> Send(this CustomReaction cr, IUserMessage ctx,
        DiscordSocketClient client, bool sanitize)
    {
        var channel = cr.DmResponse
            ? await ctx.Author.CreateDMChannelAsync().ConfigureAwait(false)
            : ctx.Channel;

        if (CrEmbed.TryParse(cr.Response, out var crembed))
        {
            var trigger = cr.Trigger.ResolveTriggerString(client);
            var substringIndex = trigger.Length;
            if (cr.ContainsAnywhere)
            {
                var pos = ctx.Content.AsSpan().GetWordPosition(trigger);
                if (pos == WordPosition.Start)
                    substringIndex += 1;
                else if (pos == WordPosition.End)
                    substringIndex = ctx.Content.Length;
                else if (pos == WordPosition.Middle)
                    substringIndex += ctx.Content.IndexOf(trigger, StringComparison.InvariantCulture);
            }

            var canMentionEveryone = (ctx.Author as IGuildUser)?.GuildPermissions.MentionEveryone ?? true;

            var rep = new ReplacementBuilder()
                .WithDefault(ctx.Author, ctx.Channel, (ctx.Channel as ITextChannel)?.Guild as SocketGuild, client)
                .WithOverride("%target%", () => canMentionEveryone
                    ? ctx.Content[substringIndex..].Trim()
                    : ctx.Content[substringIndex..].Trim().SanitizeMentions(true))
                .Build();

            rep.Replace(crembed);

            return await channel.EmbedAsync(crembed, sanitize).ConfigureAwait(false);
        }

        return await channel
            .SendMessageAsync(
                (await cr.ResponseWithContextAsync(ctx, client, cr.ContainsAnywhere).ConfigureAwait(false))
                .SanitizeMentions(sanitize)).ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WordPosition GetWordPosition(this ReadOnlySpan<char> str, in ReadOnlySpan<char> word)
    {
        var wordIndex = str.IndexOf(word, StringComparison.OrdinalIgnoreCase);
        if (wordIndex == -1)
            return WordPosition.None;

        if (wordIndex == 0)
        {
            if (word.Length < str.Length && str.IsValidWordDivider(word.Length))
                return WordPosition.Start;
        }
        else if (wordIndex + word.Length == str.Length)
        {
            if (str.IsValidWordDivider(wordIndex - 1))
                return WordPosition.End;
        }
        else if (str.IsValidWordDivider(wordIndex - 1) && str.IsValidWordDivider(wordIndex + word.Length))
        {
            return WordPosition.Middle;
        }

        return WordPosition.None;
    }

    private static bool IsValidWordDivider(this in ReadOnlySpan<char> str, int index)
    {
        var ch = str[index];
        if (ch is >= 'a' and <= 'z')
            return false;
        if (ch is >= 'A' and <= 'Z')
            return false;
        if (ch is >= '1' and <= '9')
            return false;

        return true;
    }
}

public enum WordPosition
{
    None,
    Start,
    Middle,
    End
}