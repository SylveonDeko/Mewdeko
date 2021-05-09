using AngleSharp;
using AngleSharp.Html.Dom;
using Discord;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko.Common.Replacements;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.CustomReactions.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mewdeko.Modules.CustomReactions.Extensions
{
    public static class Extensions
    {
        private static readonly Regex imgRegex = new Regex("%(img|image):(?<tag>.*?)%", RegexOptions.Compiled);

        private static Dictionary<Regex, Func<Match, Task<string>>> regexPlaceholders { get; } = new Dictionary<Regex, Func<Match, Task<string>>>()
        {
            { imgRegex, async (match) => {
                var tag = match.Groups["tag"].ToString();
                if(string.IsNullOrWhiteSpace(tag))
                    return "";

                var fullQueryLink = $"http://imgur.com/search?q={ tag }";
                var config = Configuration.Default.WithDefaultLoader();
                using(var document = await BrowsingContext.New(config).OpenAsync(fullQueryLink).ConfigureAwait(false))
                {
                    var elems = document.QuerySelectorAll("a.image-list-link").ToArray();

                    if (!elems.Any())
                        return "";

                    var img = (elems.ElementAtOrDefault(new MewdekoRandom().Next(0, elems.Length))?.Children?.FirstOrDefault() as IHtmlImageElement);

                    if (img?.Source == null)
                        return "";

                    return " " + img.Source.Replace("b.", ".", StringComparison.InvariantCulture) + " ";
                }
            } }
        };

        private static string ResolveTriggerString(this string str, IUserMessage ctx, DiscordSocketClient client)
        {
            var rep = new ReplacementBuilder()
                .WithUser(ctx.Author)
                .WithMention(client)
                .Build();

            str = rep.Replace(str.ToLowerInvariant());

            return str;
        }

        private static async Task<string> ResolveResponseStringAsync(this string str, IUserMessage ctx, DiscordSocketClient client, string resolvedTrigger, bool containsAnywhere)
        {
            var substringIndex = resolvedTrigger.Length;
            if (containsAnywhere)
            {
                var pos = ctx.Content.GetWordPosition(resolvedTrigger);
                if (pos == WordPosition.Start)
                    substringIndex += 1;
                else if (pos == WordPosition.End)
                    substringIndex = ctx.Content.Length;
                else if (pos == WordPosition.Middle)
                    substringIndex += ctx.Content.IndexOf(resolvedTrigger, StringComparison.InvariantCulture);
            }

            var rep = new ReplacementBuilder()
                .WithDefault(ctx.Author, ctx.Channel, (ctx.Channel as ITextChannel)?.Guild as SocketGuild, client)
                .WithOverride("%target%", () => ctx.Content.Substring(substringIndex).Trim())
                .Build();

            str = rep.Replace(str);
#if !GLOBAL_Mewdeko
            foreach (var ph in regexPlaceholders)
            {
                str = await ph.Key.ReplaceAsync(str, ph.Value).ConfigureAwait(false);
            }
#endif
            return str;
        }

        public static string TriggerWithContext(this CustomReaction cr, IUserMessage ctx, DiscordSocketClient client)
            => cr.Trigger.ResolveTriggerString(ctx, client);

        public static Task<string> ResponseWithContextAsync(this CustomReaction cr, IUserMessage ctx, DiscordSocketClient client, bool containsAnywhere)
            => cr.Response.ResolveResponseStringAsync(ctx, client, cr.Trigger.ResolveTriggerString(ctx, client), containsAnywhere);

        public static async Task<IUserMessage> Send(this CustomReaction cr, IUserMessage ctx, DiscordSocketClient client, CustomReactionsService crs)
        {
            var channel = cr.DmResponse ? await ctx.Author.GetOrCreateDMChannelAsync().ConfigureAwait(false) : ctx.Channel;

            if (CREmbed.TryParse(cr.Response, out CREmbed crembed))
            {
                var trigger = cr.Trigger.ResolveTriggerString(ctx, client);
                var substringIndex = trigger.Length;
                if (cr.ContainsAnywhere)
                {
                    var pos = ctx.Content.GetWordPosition(trigger);
                    if (pos == WordPosition.Start)
                        substringIndex += 1;
                    else if (pos == WordPosition.End)
                        substringIndex = ctx.Content.Length;
                    else if (pos == WordPosition.Middle)
                        substringIndex += ctx.Content.IndexOf(trigger, StringComparison.InvariantCulture);
                }

                var rep = new ReplacementBuilder()
                    .WithDefault(ctx.Author, ctx.Channel, (ctx.Channel as ITextChannel)?.Guild as SocketGuild, client)
                    .WithOverride("%target%", () => ctx.Content.Substring(substringIndex).Trim())
                    .Build();

                rep.Replace(crembed);

                return await channel.EmbedAsync(crembed.ToEmbed(), crembed.PlainText?.SanitizeMentions() ?? "").ConfigureAwait(false);
            }
            var mentionables = AllowedMentions.None;
            mentionables.AllowedTypes = AllowedMentionTypes.Users;
            return await channel.SendMessageAsync((await cr.ResponseWithContextAsync(ctx, client, cr.ContainsAnywhere).ConfigureAwait(false)).SanitizeMentions(), allowedMentions: mentionables).ConfigureAwait(false);

        }

        public static WordPosition GetWordPosition(this string str, string word)
        {
            var wordIndex = str.IndexOf(word, StringComparison.InvariantCulture);
            if (wordIndex == -1)
                return WordPosition.None;

            if (wordIndex == 0)
            {
                if (word.Length < str.Length && str.isValidWordDivider(word.Length))
                    return WordPosition.Start;
            }
            else if ((wordIndex + word.Length) == str.Length)
            {
                if (str.isValidWordDivider(wordIndex - 1))
                    return WordPosition.End;
            }
            else if (str.isValidWordDivider(wordIndex - 1) && str.isValidWordDivider(wordIndex + word.Length))
                return WordPosition.Middle;

            return WordPosition.None;
        }

        private static bool isValidWordDivider(this string str, int index)
        {
            var ch = str[index];
            if (ch >= 'a' && ch <= 'z')
                return false;
            if (ch >= 'A' && ch <= 'Z')
                return false;

            return true;
        }
    }

    public enum WordPosition
    {
        None,
        Start,
        Middle,
        End,
    }
}