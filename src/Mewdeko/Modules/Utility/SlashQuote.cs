using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DataModel;
using Discord.Interactions;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.Utility;

/// <summary>
///     Provides slash commands for managing and displaying quotes within a guild.
/// </summary>
[Group("quote", "Store and recall quotes by keyword")]
public class SlashQuote(IDataConnectionFactory dbFactory, HttpClient httpClient) : MewdekoSlashCommandModule
{
    /// <summary>
    ///     Lists quotes in the guild on a specific page. Quotes can be ordered by keyword or date added.
    /// </summary>
    /// <param name="page">The page number of quotes to display.</param>
    /// <param name="order">Determines the order in which quotes are listed.</param>
    /// <returns>A task that represents the asynchronous operation of listing quotes.</returns>
    [SlashCommand("list", "Lists the quotes of this server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task ListQuotes(
        [Summary("page", "The page to show")] int page = 1,
        [Summary("order", "Order by keyword or by date added")]
        OrderType order = OrderType.Keyword)
    {
        page--;
        if (page < 0)
            return;

        IEnumerable<Quote> quotes;

        await using var db = await dbFactory.CreateConnectionAsync();
        {
            var query = db.Quotes.Where(x => x.GuildId == ctx.Guild.Id);

            if (order == OrderType.Keyword)
                query = query.OrderBy(x => x.Keyword);
            else
                query = query.OrderBy(x => x.Id);

            quotes = await query.Skip(15 * page).Take(15).ToListAsync();
        }

        var enumerable = quotes as Quote[] ?? quotes.ToArray();
        if (enumerable.Length > 0)
        {
            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.QuotesPage(ctx.Guild.Id, page + 1))
                .WithDescription(string.Join("\n",
                    enumerable.Select(q =>
                        $"`#{q.Id}` {Format.Bold(q.Keyword.SanitizeAllMentions()),-20} by {q.AuthorName.SanitizeAllMentions()}")))
                .Build()).ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorAsync(Strings.QuotesPageNone(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Displays a random quote matching the specified keyword.
    /// </summary>
    /// <param name="keyword">The keyword to search for in quotes.</param>
    /// <returns>A task that represents the asynchronous operation of displaying a quote.</returns>
    [SlashCommand("print", "Shows a random quote with the given keyword")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task QuotePrint([Summary("keyword", "The keyword of the quote")] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return;

        keyword = keyword.ToUpperInvariant();

        await using var db = await dbFactory.CreateConnectionAsync();

        var matchingQuotes = await db.Quotes
            .Where(q => q.GuildId == ctx.Guild.Id && q.Keyword == keyword)
            .ToListAsync();

        var quote = matchingQuotes.Any()
            ? matchingQuotes.MinBy(_ => new Random().Next())
            : null;

        if (quote == null)
        {
            await ReplyErrorAsync(Strings.QuotesNotfound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var rep = new ReplacementBuilder()
            .WithDefault(Context)
            .Build();

        if (SmartEmbed.TryParse(rep.Replace(quote.Text), ctx.Guild?.Id, out var embed, out var plainText,
                out var components))
        {
            await ctx.Interaction.RespondAsync($"`#{quote.Id}` {plainText?.SanitizeAllMentions()}",
                embed, components: components?.Build()).ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.RespondAsync($"`#{quote.Id}` {rep.Replace(quote.Text)?.SanitizeAllMentions()}")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Displays the quote with the specified ID.
    /// </summary>
    /// <param name="id">The ID of the quote to display.</param>
    /// <returns>A task that represents the asynchronous operation of displaying a quote.</returns>
    [SlashCommand("show", "Shows the details of a quote by id")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task QuoteShow([Summary("id", "The id of the quote")] int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id);

        if (quote?.GuildId != Context.Guild.Id)
            quote = null;

        if (quote is null)
        {
            await ReplyErrorAsync(Strings.QuotesNotfound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ShowQuoteData(quote).ConfigureAwait(false);
    }

    private Task ShowQuoteData(Quote data)
    {
        return ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.QuoteId(ctx.Guild.Id, $"#{data.Id}"))
            .AddField(efb => efb.WithName(Strings.Trigger(ctx.Guild.Id)).WithValue(data.Keyword))
            .AddField(efb => efb.WithName(Strings.Response(ctx.Guild.Id)).WithValue(data.Text.Length > 1000
                ? Strings.RedactedTooLong(ctx.Guild.Id)
                : Format.Sanitize(data.Text)))
            .WithFooter(Strings.CreatedBy(ctx.Guild.Id, $"{data.AuthorName} ({data.AuthorId})"))
            .Build());
    }

    /// <summary>
    ///     Searches for and displays a quote that matches both a keyword and a text query.
    /// </summary>
    /// <param name="keyword">The keyword to match in the quotes.</param>
    /// <param name="text">The text to match in the quotes.</param>
    /// <returns>A task that represents the asynchronous operation of searching for and displaying a quote.</returns>
    [SlashCommand("search", "Searches quotes with a keyword for the given text")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task QuoteSearch(
        [Summary("keyword", "The keyword of the quote")]
        string keyword,
        [Summary("text", "Text the quote must contain")]
        string text)
    {
        if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(text))
            return;

        keyword = keyword.ToUpperInvariant();

        await using var db = await dbFactory.CreateConnectionAsync();

        var matchingQuotes = await db.Quotes
            .Where(q => q.GuildId == ctx.Guild.Id &&
                        q.Keyword == keyword &&
                        q.Text.ToUpper().Contains(text.ToUpper()))
            .ToListAsync();

        var keywordquote = matchingQuotes.Any()
            ? matchingQuotes.MinBy(_ => new Random().Next())
            : null;

        if (keywordquote == null)
        {
            await ReplyErrorAsync(Strings.QuotesNotfound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.RespondAsync(
                $"`#{keywordquote.Id}` {keyword.ToLowerInvariant()}:  {keywordquote.Text.SanitizeAllMentions()}")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Displays who added a quote with the specified ID together with its content.
    /// </summary>
    /// <param name="id">The ID of the quote to display.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("id", "Shows a quote by id, including who added it")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task QuoteId([Summary("id", "The id of the quote")] int id)
    {
        if (id < 0)
            return;

        var rep = new ReplacementBuilder()
            .WithDefault(Context)
            .Build();

        await using var db = await dbFactory.CreateConnectionAsync();
        var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id);

        if (quote is null || quote.GuildId != ctx.Guild.Id)
        {
            await ErrorAsync(Strings.QuotesNotfound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var infoText =
            $"`#{quote.Id} added by {quote.AuthorName.SanitizeAllMentions()}` {quote.Keyword.ToLowerInvariant().SanitizeAllMentions()}:\n";

        if (SmartEmbed.TryParse(rep.Replace(quote.Text), ctx.Guild?.Id, out var embed, out var plainText,
                out var components))
        {
            await ctx.Interaction.RespondAsync(infoText + plainText.SanitizeMentions(), embed,
                    components: components?.Build())
                .ConfigureAwait(false);
        }
        else
        {
            await ctx.Interaction.RespondAsync(infoText + rep.Replace(quote.Text)?.SanitizeAllMentions())
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Opens a modal to add a new quote with a keyword and text.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("add", "Adds a new quote")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public Task QuoteAdd()
    {
        return RespondWithModalAsync<QuoteAddModal>("quote_add");
    }

    /// <summary>
    ///     Handles the quote add modal and stores the new quote.
    /// </summary>
    /// <param name="modal">The modal containing the keyword and text of the quote.</param>
    /// <returns>A task that represents the asynchronous operation of adding a new quote.</returns>
    [ModalInteraction("quote_add", true)]
    [RequireContext(ContextType.Guild)]
    public async Task QuoteAddSubmitted(QuoteAddModal modal)
    {
        var keyword = modal.Keyword;
        var text = modal.Text;
        if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(text))
            return;

        keyword = keyword.ToUpperInvariant();

        var q = new Quote
        {
            AuthorId = ctx.User.Id,
            AuthorName = ctx.User.Username,
            GuildId = ctx.Guild.Id,
            Keyword = keyword,
            Text = text
        };

        await using var db = await dbFactory.CreateConnectionAsync();
        q.Id = await db.InsertWithInt32IdentityAsync(q);

        await ReplyConfirmAsync(Strings.QuoteAddedNew(ctx.Guild.Id, Format.Code(q.Id.ToString())))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes a quote with the specified ID. Only the author or an administrator can delete a quote.
    /// </summary>
    /// <param name="id">The ID of the quote to delete.</param>
    /// <returns>A task that represents the asynchronous operation of deleting a quote.</returns>
    [SlashCommand("delete", "Deletes a quote by id")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task QuoteDelete([Summary("id", "The id of the quote")] int id)
    {
        var isAdmin = ((IGuildUser)ctx.User).GuildPermissions.Administrator;

        var success = false;
        string? response;

        await using var db = await dbFactory.CreateConnectionAsync();
        var q = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id);

        if (q?.GuildId != ctx.Guild.Id || !isAdmin && q.AuthorId != ctx.User.Id)
        {
            response = Strings.QuotesRemoveNone(ctx.Guild.Id);
        }
        else
        {
            await db.DeleteAsync(q);
            success = true;
            response = Strings.QuoteDeleted(ctx.Guild.Id, id);
        }

        if (success)
            await ConfirmAsync(response).ConfigureAwait(false);
        else
            await ErrorAsync(response).ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes all quotes associated with the specified keyword.
    /// </summary>
    /// <param name="keyword">The keyword whose associated quotes will be deleted.</param>
    /// <returns>A task that represents the asynchronous operation of deleting quotes.</returns>
    [SlashCommand("delete-all", "Deletes all quotes with the given keyword")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task DelAllQuotes([Summary("keyword", "The keyword whose quotes are deleted")] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return;

        keyword = keyword.ToUpperInvariant();

        await using var db = await dbFactory.CreateConnectionAsync();

        await db.Quotes
            .Where(x => x.GuildId == ctx.Guild.Id && x.Keyword.ToUpper() == keyword)
            .DeleteAsync();

        await ReplyConfirmAsync(Strings.QuotesDeleted(ctx.Guild.Id, Format.Bold(keyword.SanitizeAllMentions())))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Exports all quotes from the guild in JSON format.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation of exporting quotes.</returns>
    [SlashCommand("export", "Exports all quotes of this server as json")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task QuoteExport()
    {
        await DeferAsync();
        await using var db = await dbFactory.CreateConnectionAsync();
        var quotes = await db.Quotes
            .Where(q => q.GuildId == ctx.Guild.Id)
            .OrderBy(q => q.Keyword)
            .ThenBy(q => q.Id)
            .ToListAsync();

        if (!quotes.Any())
        {
            await ReplyErrorAsync(Strings.QuotesPageNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var exportData = quotes.Select(q => new Utility.QuoteExportData
        {
            Keyword = q.Keyword,
            Text = q.Text,
            AuthorName = q.AuthorName,
            AuthorId = q.AuthorId,
            DateAdded = q.DateAdded,
            UseCount = q.UseCount
        }).ToList();

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var jsonContent = JsonSerializer.Serialize(exportData, jsonOptions);
        var fileName = $"quotes-{ctx.Guild.Name.Replace(" ", "-")}-{DateTime.UtcNow:yyyy-MM-dd}.json";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent));
        var fileAttachment = new FileAttachment(stream, fileName);

        await ctx.Interaction.FollowupWithFileAsync(fileAttachment,
                Strings.QuoteExportSuccess(ctx.Guild.Id, quotes.Count))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Imports quotes from a JSON file attachment. Duplicate quotes are allowed.
    /// </summary>
    /// <param name="file">The json file previously exported with the export command.</param>
    /// <returns>A task that represents the asynchronous operation of importing quotes.</returns>
    [SlashCommand("import", "Imports quotes from an exported json file")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task QuoteImport([Summary("file", "The json file to import")] IAttachment file)
    {
        var attachment = file;
        if (attachment == null)
        {
            await ReplyErrorAsync(Strings.QuoteImportNoFile(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (!attachment.Filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            await ReplyErrorAsync(Strings.QuoteImportInvalidFormat(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync();
        try
        {
            var content = await httpClient.GetStringAsync(attachment.Url);
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true
            };

            var importData = JsonSerializer.Deserialize<List<Utility.QuoteExportData>>(content, jsonOptions);

            if (importData == null || !importData.Any())
            {
                await ReplyErrorAsync(Strings.QuoteImportEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await using var db = await dbFactory.CreateConnectionAsync();

            var validQuotes = importData
                .Where(data => !string.IsNullOrWhiteSpace(data.Keyword) && !string.IsNullOrWhiteSpace(data.Text))
                .Select(data => new Quote
                {
                    GuildId = ctx.Guild.Id,
                    Keyword = data.Keyword.ToUpperInvariant(),
                    Text = data.Text,
                    AuthorName = string.IsNullOrWhiteSpace(data.AuthorName) ? ctx.User.Username : data.AuthorName,
                    AuthorId = data.AuthorId == 0 ? ctx.User.Id : data.AuthorId,
                    UseCount = data.UseCount,
                    DateAdded = data.DateAdded ?? DateTime.UtcNow
                })
                .ToList();

            var errorCount = importData.Count - validQuotes.Count;

            if (validQuotes.Any())
            {
                await db.BulkCopyAsync(validQuotes);
                var successCount = validQuotes.Count;

                await ReplyConfirmAsync(Strings.QuoteImportSuccess(ctx.Guild.Id, successCount, errorCount))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.QuoteImportFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await ReplyErrorAsync(Strings.QuoteImportError(ctx.Guild.Id, ex.Message)).ConfigureAwait(false);
        }
    }
}