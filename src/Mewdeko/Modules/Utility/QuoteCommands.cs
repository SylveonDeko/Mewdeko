using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    [Group]
    public class QuoteCommands : MewdekoSubmodule
    {
        private readonly DbService db;

        public QuoteCommands(DbService db) => this.db = db;

        [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(1)]
        public Task ListQuotes(OrderType order = OrderType.Keyword) => ListQuotes(1, order);

        [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(0)]
        public async Task ListQuotes(int page = 1, OrderType order = OrderType.Keyword)
        {
            page--;
            if (page < 0)
                return;

            IEnumerable<Quote> quotes;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                quotes = uow.Quotes.GetGroup(ctx.Guild.Id, page, order);
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

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task QuotePrint([Remainder] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return;

            keyword = keyword.ToUpperInvariant();

            Quote quote;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                quote = await uow.Quotes.GetRandomQuoteByKeywordAsync(ctx.Guild.Id, keyword).ConfigureAwait(false);
            }

            if (quote == null)
                return;

            var rep = new ReplacementBuilder()
                .WithDefault(Context)
                .Build();

            if (SmartEmbed.TryParse(rep.Replace(quote.Text), ctx.Guild?.Id, out var embed, out var plainText, out var components))
            {
                await ctx.Channel.SendMessageAsync($"`#{quote.Id}` 📣 {plainText?.SanitizeAllMentions()}",
                    embeds: embed, components: components?.Build()).ConfigureAwait(false);
                return;
            }

            await ctx.Channel
                .SendMessageAsync($"`#{quote.Id}` 📣 {rep.Replace(quote.Text)?.SanitizeAllMentions()}")
                .ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task QuoteShow(int id)
        {
            Quote quote;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                quote = await uow.Quotes.GetById(id);
                if (quote.GuildId != Context.Guild.Id)
                    quote = null;
            }

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

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task QuoteSearch(string keyword, [Remainder] string text)
        {
            if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(text))
                return;

            keyword = keyword.ToUpperInvariant();

            Quote keywordquote;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                keywordquote = await uow.Quotes.SearchQuoteKeywordTextAsync(ctx.Guild.Id, keyword, text).ConfigureAwait(false);
            }

            if (keywordquote == null)
                return;

            await ctx.Channel.SendMessageAsync(
                $"`#{keywordquote.Id}` 💬 {keyword.ToLowerInvariant()}:  {keywordquote.Text.SanitizeAllMentions()}").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task QuoteId(int id)
        {
            if (id < 0)
                return;

            Quote quote;

            var rep = new ReplacementBuilder()
                .WithDefault(Context)
                .Build();

            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                quote = await uow.Quotes.GetById(id);
            }

            if (quote is null || quote.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync(GetText("quotes_notfound")).ConfigureAwait(false);
                return;
            }

            var infoText =
                $"`#{quote.Id} added by {quote.AuthorName.SanitizeAllMentions()}` 🗯️ {quote.Keyword.ToLowerInvariant().SanitizeAllMentions()}:\n";

            if (SmartEmbed.TryParse(rep.Replace(quote.Text), ctx.Guild?.Id, out var embed, out var plainText, out var components))
            {
                await ctx.Channel.SendMessageAsync(infoText + plainText.SanitizeMentions(), embeds: embed, components: components?.Build())
                    .ConfigureAwait(false);
            }
            else
            {
                await ctx.Channel.SendMessageAsync(infoText + rep.Replace(quote.Text)?.SanitizeAllMentions())
                    .ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task QuoteAdd(string keyword, [Remainder] string text)
        {
            if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(text))
                return;

            keyword = keyword.ToUpperInvariant();

            Quote q;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                uow.Quotes.Add(q = new Quote
                {
                    AuthorId = ctx.Message.Author.Id,
                    AuthorName = ctx.Message.Author.Username,
                    GuildId = ctx.Guild.Id,
                    Keyword = keyword,
                    Text = text
                });
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }

            await ReplyConfirmLocalizedAsync("quote_added_new", Format.Code(q.Id.ToString())).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task QuoteDelete(int id)
        {
            var isAdmin = ((IGuildUser)ctx.Message.Author).GuildPermissions.Administrator;

            var success = false;
            string? response;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                var q = await uow.Quotes.GetById(id);

                if (q?.GuildId != ctx.Guild.Id || (!isAdmin && q.AuthorId != ctx.Message.Author.Id))
                {
                    response = GetText("quotes_remove_none");
                }
                else
                {
                    uow.Quotes.Remove(q);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                    success = true;
                    response = GetText("quote_deleted", id);
                }
            }

            if (success)
                await ctx.Channel.SendConfirmAsync(response).ConfigureAwait(false);
            else
                await ctx.Channel.SendErrorAsync(response).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task DelAllQuotes([Remainder] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return;

            keyword = keyword.ToUpperInvariant();

            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                uow.Quotes.RemoveAllByKeyword(ctx.Guild.Id, keyword.ToUpperInvariant());

                await uow.SaveChangesAsync().ConfigureAwait(false);
            }

            await ReplyConfirmLocalizedAsync("quotes_deleted", Format.Bold(keyword.SanitizeAllMentions()))
                .ConfigureAwait(false);
        }
    }
}