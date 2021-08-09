using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Mewdeko.Extensions
{
    public static class IMessageChannelExtensions
    {
        private static readonly IEmote arrow_left = new Emoji("⬅");
        private static readonly IEmote arrow_right = new Emoji("➡");
        private static readonly IEmote stop = new Emoji("🛑");
        private static readonly IEmote fast_foward = new Emoji("⏩");
        private static readonly IEmote rewind = new Emoji("⏪");

        public static Task<IUserMessage> EmbedAsync(this IMessageChannel ch, EmbedBuilder embed, string msg = "")
        {
            return ch.SendMessageAsync(msg, embed: embed.Build(),
                options: new RequestOptions {RetryMode = RetryMode.AlwaysRetry});
        }

        public static Task<IUserMessage> SendErrorAsync(this IMessageChannel ch, string title, string error,
            string url = null, string footer = null)
        {
            var eb = new EmbedBuilder().WithErrorColor().WithDescription(error)
                .WithTitle(title);
            if (url != null && Uri.IsWellFormedUriString(url, UriKind.Absolute))
                eb.WithUrl(url);
            if (!string.IsNullOrWhiteSpace(footer))
                eb.WithFooter(efb => efb.WithText(footer));
            return ch.SendMessageAsync("", embed: eb.Build());
        }

        public static Task<IUserMessage> SendErrorAsync(this IMessageChannel ch, string error)
        {
            return ch.SendMessageAsync("", embed: new EmbedBuilder().WithErrorColor().WithDescription(error).Build());
        }

        public static Task<IUserMessage> SendConfirmAsync(this IMessageChannel ch, string title, string text,
            string url = null, string footer = null)
        {
            var eb = new EmbedBuilder().WithOkColor().WithDescription(text)
                .WithTitle(title);
            if (url != null && Uri.IsWellFormedUriString(url, UriKind.Absolute))
                eb.WithUrl(url);
            if (!string.IsNullOrWhiteSpace(footer))
                eb.WithFooter(efb => efb.WithText(footer));
            return ch.SendMessageAsync("", embed: eb.Build());
        }

        public static Task<IUserMessage> SendConfirmAsync(this IMessageChannel ch, string text)
        {
            return ch.SendMessageAsync("", embed: new EmbedBuilder().WithOkColor().WithDescription(text).Build());
        }

        public static Task<IUserMessage> SendTableAsync<T>(this IMessageChannel ch, string seed, IEnumerable<T> items,
            Func<T, string> howToPrint, int columns = 3)
        {
            var i = 0;
            return ch.SendMessageAsync($@"{seed}```css
{string.Join("\n", items.GroupBy(item => i++ / columns)
    .Select(ig => string.Concat(ig.Select(el => howToPrint(el)))))}
```");
        }

        public static Task<IUserMessage> SendTableAsync<T>(this IMessageChannel ch, IEnumerable<T> items,
            Func<T, string> howToPrint, int columns = 3)
        {
            return ch.SendTableAsync("", items, howToPrint, columns);
        }

        public static Task SendPaginatedConfirmAsync(this ICommandContext ctx,
            int currentPage, Func<int, EmbedBuilder> pageFunc, int totalElements,
            int itemsPerPage, bool addPaginatedFooter = true)
        {
            return ctx.SendPaginatedConfirmAsync(currentPage,
                x => Task.FromResult(pageFunc(x)), totalElements, itemsPerPage, addPaginatedFooter);
        }

        /// <summary>
        ///     danny kamisama
        /// </summary>
        public static async Task SendPaginatedConfirmAsync(this ICommandContext ctx, int currentPage,
            Func<int, Task<EmbedBuilder>> pageFunc, int totalElements, int itemsPerPage, bool addPaginatedFooter = true)
        {
            var embed = await pageFunc(currentPage).ConfigureAwait(false);

            var lastPage = (totalElements - 1) / itemsPerPage;

            var canPaginate = true;
            var sg = ctx.Guild as SocketGuild;
            embed.AddPaginatedFooter(currentPage, lastPage);
            var builder = new ComponentBuilder()
                .WithButton(label: " ", customId: "rewind", emote: rewind)
                .WithButton(label: " ", customId: "left", emote: arrow_left)
                .WithButton(label: " ", customId: "stop", emote: stop)
                .WithButton(label: " ", customId: "right", emote: arrow_right)
                .WithButton(label: " ", customId: "fastforward", emote: fast_foward);

            var msg = await ctx.Channel.SendMessageAsync(embed: embed.Build(), component: builder.Build()).ConfigureAwait(false);

            if (lastPage == 0 || !canPaginate)
                return;

            var lastPageChange = DateTime.MinValue;

            async Task changePage(SocketInteraction r)
            {
                try
                {
                    if (r is SocketMessageComponent e)
                    {
                        if (e.User.Id != ctx.User.Id)
                            return;
                        if (DateTime.UtcNow - lastPageChange < TimeSpan.FromSeconds(1))
                            return;
                        if (e.Data.CustomId == "stop")
                        {
                            await msg.DeleteAsync().ConfigureAwait(false);
                            return;
                        }
                        if (e.Data.CustomId == "fastforward")
                        {
                            if (currentPage == lastPage)
                                return;
                            lastPageChange = DateTime.UtcNow;
                            var toSend = await pageFunc(lastPage).ConfigureAwait(false);
                            currentPage = lastPage;
                            if (addPaginatedFooter)
                                toSend.AddPaginatedFooter(currentPage, lastPage);
                            await e.UpdateAsync(x => x.Embed = toSend.Build()).ConfigureAwait(false);
                            return;
                        }
                        if (e.Data.CustomId == "rewind")
                        {
                            if (currentPage == 0)
                                return;
                            lastPageChange = DateTime.UtcNow;
                            var toSend = await pageFunc(0).ConfigureAwait(false);
                            currentPage = 0;
                            if (addPaginatedFooter)
                                toSend.AddPaginatedFooter(currentPage, lastPage);
                            await e.UpdateAsync(x => x.Embed = toSend.Build()).ConfigureAwait(false);
                            return;
                        }
                        if (e.Data.CustomId == "left")
                        {
                            if (currentPage == 0)
                                return;
                            lastPageChange = DateTime.UtcNow;
                            var toSend = await pageFunc(--currentPage).ConfigureAwait(false);
                            if (addPaginatedFooter)
                                toSend.AddPaginatedFooter(currentPage, lastPage);
                            await e.UpdateAsync(x => x.Embed = toSend.Build()).ConfigureAwait(false);
                        }
                        else if (e.Data.CustomId == "right")
                        {
                            if (lastPage > currentPage)
                            {
                                lastPageChange = DateTime.UtcNow;
                                var toSend = await pageFunc(++currentPage).ConfigureAwait(false);
                                if (addPaginatedFooter)
                                    toSend.AddPaginatedFooter(currentPage, lastPage);
                                await e.UpdateAsync(x => x.Embed = toSend.Build()).ConfigureAwait(false);
                                return;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    //ignored
                }
            }
            

            using (msg.OnClick((DiscordSocketClient) ctx.Client, changePage))
            {
                await Task.Delay(3000000).ConfigureAwait(false);
            }
        }

        public static Task OkAsync(this ICommandContext ctx)
        {
            return ctx.Message.AddReactionAsync(new Emoji("✅"));
        }

        public static Task ErrorAsync(this ICommandContext ctx)
        {
            return ctx.Message.AddReactionAsync(new Emoji("❌"));
        }

        public static Task WarningAsync(this ICommandContext ctx)
        {
            return ctx.Message.AddReactionAsync(new Emoji("⚠️"));
        }
    }
}