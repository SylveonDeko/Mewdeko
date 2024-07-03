using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Database.DbContextStuff;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    /// Provides commands for managing and displaying quotes within a guild. I dont know why you would use this when chat triggers exist.
    /// </summary>
    [Group]
    public class QuoteCommands(DbContextProvider dbProvider) : MewdekoSubmodule
    {
        /// <summary>
        /// Lists quotes in the guild. Quotes can be ordered by keyword or date added.
        /// </summary>
        /// <param name="order">Determines the order in which quotes are listed.</param>
        /// <returns>A task that represents the asynchronous operation of listing quotes.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(1)]
        public Task ListQuotes(OrderType order = OrderType.Keyword) => ListQuotes(1, order);

        /// <summary>
        /// Lists quotes in the guild on a specific page. Quotes can be ordered by keyword or date added.
        /// </summary>
        /// <param name="page">The page number of quotes to display.</param>
        /// <param name="order">Determines the order in which quotes are listed.</param>
        /// <returns>A task that represents the asynchronous operation of listing quotes.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(0)]
        public async Task ListQuotes(int page = 1, OrderType order = OrderType.Keyword)
        {
            page--;
            if (page < 0)
                return;

            IEnumerable<Quote> quotes;

            await using var dbContext = await dbProvider.GetContextAsync();
            {
                quotes = dbContext.Quotes.GetGroup(ctx.Guild.Id, page, order);
            }

            var enumerable = quotes as Quote[] ?? quotes.ToArray();
            if (enumerable.Length > 0)
            {
                await ctx.Channel.SendConfirmAsync(GetText("quotes_page", page + 1),
                        string.Join("\n",
                            enumerable.Select(q =>
                                $"`#{q.Id}` {Format.Bold(q.Keyword.SanitizeAllMentions()),-20} by {q.AuthorName.SanitizeAllMentions()}")))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorLocalizedAsync("quotes_page_none").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Displays a random quote matching the specified keyword.
        /// </summary>
        /// <param name="keyword">The keyword to search for in quotes.</param>
        /// <returns>A task that represents the asynchronous operation of displaying a quote.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task QuotePrint([Remainder] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return;

            keyword = keyword.ToUpperInvariant();

            await using var dbContext = await dbProvider.GetContextAsync();
            var quote = await dbContext.Quotes.GetRandomQuoteByKeywordAsync(ctx.Guild.Id, keyword).ConfigureAwait(false);

            if (quote == null)
                return;

            var rep = new ReplacementBuilder()
                .WithDefault(Context)
                .Build();

            if (SmartEmbed.TryParse(rep.Replace(quote.Text), ctx.Guild?.Id, out var embed, out var plainText,
                    out var components))
            {
                await ctx.Channel.SendMessageAsync($"`#{quote.Id}` 📣 {plainText?.SanitizeAllMentions()}",
                    embeds: embed, components: components?.Build()).ConfigureAwait(false);
                return;
            }

            await ctx.Channel
                .SendMessageAsync($"`#{quote.Id}` 📣 {rep.Replace(quote.Text)?.SanitizeAllMentions()}")
                .ConfigureAwait(false);
        }


        /// <summary>
        /// Displays the quote with the specified ID.
        /// </summary>
        /// <param name="id">The ID of the quote to display.</param>
        /// <returns>A task that represents the asynchronous operation of displaying a quote.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task QuoteShow(int id)
        {
            await using var dbContext = await dbProvider.GetContextAsync();
            var quote = await dbContext.Quotes.GetById(id);
            if (quote.GuildId != Context.Guild.Id)
                    quote = null;

            if (quote is null)
            {
                await ReplyErrorLocalizedAsync("quote_no_found_id").ConfigureAwait(false);
                return;
            }

            await ShowQuoteData(quote).ConfigureAwait(false);
        }

        private async Task ShowQuoteData(Quote data) =>
            await ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithOkColor()
                .WithTitle(GetText("quote_id", $"#{data.Id}"))
                .AddField(efb => efb.WithName(GetText("trigger")).WithValue(data.Keyword))
                .AddField(efb => efb.WithName(GetText("response")).WithValue(data.Text.Length > 1000
                    ? GetText("redacted_too_long")
                    : Format.Sanitize(data.Text)))
                .WithFooter(GetText("created_by", $"{data.AuthorName} ({data.AuthorId})"))
            ).ConfigureAwait(false);

        /// <summary>
        /// Searches for and displays a quote that matches both a keyword and a text query.
        /// </summary>
        /// <param name="keyword">The keyword to match in the quotes.</param>
        /// <param name="text">The text to match in the quotes.</param>
        /// <returns>A task that represents the asynchronous operation of searching for and displaying a quote.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task QuoteSearch(string keyword, [Remainder] string text)
        {
            if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(text))
                return;

            keyword = keyword.ToUpperInvariant();

            await using var dbContext = await dbProvider.GetContextAsync();
            var keywordquote = await dbContext.Quotes.SearchQuoteKeywordTextAsync(ctx.Guild.Id, keyword, text)
                .ConfigureAwait(false);

            if (keywordquote == null)
                return;

            await ctx.Channel.SendMessageAsync(
                    $"`#{keywordquote.Id}` 💬 {keyword.ToLowerInvariant()}:  {keywordquote.Text.SanitizeAllMentions()}")
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Displays who added a quote with the specified ID.
        /// </summary>
        /// <param name="id">The ID of the quote to display.</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task QuoteId(int id)
        {
            if (id < 0)
                return;

            var rep = new ReplacementBuilder()
                .WithDefault(Context)
                .Build();


            await using var dbContext = await dbProvider.GetContextAsync();
            var quote = await dbContext.Quotes.GetById(id);

            if (quote is null || quote.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync(GetText("quotes_notfound"), Config).ConfigureAwait(false);
                return;
            }

            var infoText =
                $"`#{quote.Id} added by {quote.AuthorName.SanitizeAllMentions()}` 🗯️ {quote.Keyword.ToLowerInvariant().SanitizeAllMentions()}:\n";

            if (SmartEmbed.TryParse(rep.Replace(quote.Text), ctx.Guild?.Id, out var embed, out var plainText,
                    out var components))
            {
                await ctx.Channel.SendMessageAsync(infoText + plainText.SanitizeMentions(), embeds: embed,
                        components: components?.Build())
                    .ConfigureAwait(false);
            }
            else
            {
                await ctx.Channel.SendMessageAsync(infoText + rep.Replace(quote.Text)?.SanitizeAllMentions())
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Adds a new quote with the specified keyword and text.
        /// </summary>
        /// <param name="keyword">The keyword associated with the quote.</param>
        /// <param name="text">The text of the quote.</param>
        /// <returns>A task that represents the asynchronous operation of adding a new quote.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task QuoteAdd(string keyword, [Remainder] string text)
        {
            if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(text))
                return;

            keyword = keyword.ToUpperInvariant();

            Quote q;

            await using var dbContext = await dbProvider.GetContextAsync();
            dbContext.Quotes.Add(q = new Quote
                {
                    AuthorId = ctx.Message.Author.Id,
                    AuthorName = ctx.Message.Author.Username,
                    GuildId = ctx.Guild.Id,
                    Keyword = keyword,
                    Text = text
                });
            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("quote_added_new", Format.Code(q.Id.ToString())).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes a quote with the specified ID.
        /// </summary>
        /// <param name="id">The ID of the quote to delete.</param>
        /// <returns>A task that represents the asynchronous operation of deleting a quote.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task QuoteDelete(int id)
        {
            var isAdmin = ((IGuildUser)ctx.Message.Author).GuildPermissions.Administrator;

            var success = false;
            string? response;

            await using var dbContext = await dbProvider.GetContextAsync();
            var q = await dbContext.Quotes.GetById(id);

            if (q?.GuildId != ctx.Guild.Id || (!isAdmin && q.AuthorId != ctx.Message.Author.Id))
            {
                response = GetText("quotes_remove_none");
            }
            else
            {
                dbContext.Quotes.Remove(q);
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
                success = true;
                response = GetText("quote_deleted", id);
            }

            if (success)
                await ctx.Channel.SendConfirmAsync(response).ConfigureAwait(false);
            else
                await ctx.Channel.SendErrorAsync(response, Config).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes all quotes associated with the specified keyword.
        /// </summary>
        /// <param name="keyword">The keyword whose associated quotes will be deleted.</param>
        /// <returns>A task that represents the asynchronous operation of deleting quotes.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task DelAllQuotes([Remainder] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return;

            keyword = keyword.ToUpperInvariant();


            await using var dbContext = await dbProvider.GetContextAsync();
            dbContext.Quotes.RemoveAllByKeyword(ctx.Guild.Id, keyword.ToUpperInvariant());

            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("quotes_deleted", Format.Bold(keyword.SanitizeAllMentions()))
                .ConfigureAwait(false);
        }
    }
}