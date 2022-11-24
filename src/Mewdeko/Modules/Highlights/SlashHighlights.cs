using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.Highlights.Services;
using System.Threading.Tasks;
using Mewdeko.Common.Attributes.InteractionCommands;

namespace Mewdeko.Modules.Highlights;

[Group("highlights", "Set or manage highlights")]
public class SlashHighlights : MewdekoSlashModuleBase<HighlightsService>
{
    private readonly InteractiveService interactivity;
    private readonly DbService db;

    public SlashHighlights(InteractiveService interactivity, DbService db)
    {
        this.interactivity = interactivity;
        this.db = db;
    }

    [SlashCommand("add", "Add new highlights."), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task AddHighlight([Summary("words", "Words to highlight.")] string words)
    {
        await using var uow = db.GetDbContext();
        var highlights = uow.Highlights.ForUser(ctx.Guild.Id, ctx.User.Id).ToList();
        if (string.IsNullOrWhiteSpace(words))
        {
            await ctx.Interaction.SendErrorAsync("You need to specify a phrase to highlight.").ConfigureAwait(false);
            return;
        }

        if (highlights.Count > 0 && highlights.Select(x => x.Word.ToLower()).Contains(words.ToLower()))
        {
            await ctx.Interaction.SendErrorAsync("That's already in your highlights").ConfigureAwait(false);
        }
        else
        {
            await Service.AddHighlight(ctx.Guild.Id, ctx.User.Id, words).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync($"Added {Format.Code(words)} to your highlights!").ConfigureAwait(false);
        }
    }

    [SlashCommand("list", "List your current highlights."), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task ListHighlights()
    {
        await using var uow = db.GetDbContext();
        var highlightsForUser = uow.Highlights.ForUser(ctx.Guild.Id, ctx.User.Id).ToList();

        if (highlightsForUser.Count == 0)
        {
            await ctx.Interaction.SendErrorAsync("You have no highlights set.").ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(highlightsForUser.Count() / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, ctx.Interaction as SocketInteraction,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var highlightsEnumerable = highlightsForUser.Skip(page * 10).Take(10);
            return new PageBuilder().WithOkColor()
                             .WithTitle($"{highlightsForUser.Count()} Highlights")
                             .WithDescription(string.Join("\n", highlightsEnumerable.Select(x => $"{highlightsForUser.IndexOf(x) + 1}. {x.Word}")));
        }
    }

    [SlashCommand("delete", "Delete a highlight."), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task DeleteHighlight(
        [Autocomplete(typeof(HighlightAutocompleter)), Summary("words", "The highlight to delete.")] string words)
    {
        if (string.IsNullOrWhiteSpace(words))
        {
            await ctx.Interaction.SendErrorAsync("Cannot delete an empty highlight.").ConfigureAwait(false);
            return;
        }

        await using var uow = db.GetDbContext();
        var highlightsForUser = uow.Highlights.ForUser(ctx.Guild.Id, ctx.User.Id).ToList();

        if (highlightsForUser.Count == 0)
        {
            await ctx.Interaction.SendErrorAsync("Cannot delete because you have no highlights set!").ConfigureAwait(false);
            return;
        }

        if (int.TryParse(words, out var number))
        {
            var todelete = highlightsForUser.ElementAt(number - 1);
            if (todelete is null)
            {
                await ctx.Interaction.SendErrorAsync("That Highlight does not exist!").ConfigureAwait(false);
                return;
            }

            await Service.RemoveHighlight(todelete).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync($"Successfully removed {Format.Code(todelete.Word)} from your highlights.").ConfigureAwait(false);
            return;
        }
        if (!highlightsForUser.Select(x => x.Word).Contains(words))
        {
            await ctx.Interaction.SendErrorAsync("This is not in your highlights!").ConfigureAwait(false);
            return;
        }
        await Service.RemoveHighlight(highlightsForUser.Find(x => x.Word == words)).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Successfully removed {Format.Code(words)} from your highlights.").ConfigureAwait(false);
    }

    [SlashCommand("match", "Find a matching highlight."), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task MatchHighlight(
        [Autocomplete(typeof(HighlightAutocompleter)), Summary("words", "The highlight to find.")] string words)
    {
        if (string.IsNullOrWhiteSpace(words))
        {
            await ctx.Interaction.SendErrorAsync("Cannot match an empty highlight.").ConfigureAwait(false);
            return;
        }

        await using var uow = db.GetDbContext();
        var highlightsForUser = uow.Highlights.ForUser(ctx.Guild.Id, ctx.User.Id).ToList();

        var matched = highlightsForUser.Where(x => words.ToLower().Contains(x.Word.ToLower()));
        if (!matched.Any())
        {
            await ctx.Interaction.SendErrorAsync("No matches found.").ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory1)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(matched.Count() / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, Context.Channel,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory1(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var highlightsEnumerable = matched.Skip(page * 10).Take(10);
            return new PageBuilder().WithOkColor()
                            .WithTitle($"{highlightsForUser.Count()} Highlights")
                            .WithDescription(string.Join("\n", highlightsEnumerable.Select(x => $"{highlightsForUser.IndexOf(x) + 1}. {x.Word}")));
        }
    }

    [SlashCommand("toggle-user", "Ignore a specified user."), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task ToggleUser(IUser user)
    {
        if (await Service.ToggleIgnoredUser(ctx.Guild.Id, ctx.User.Id, user.Id.ToString()).ConfigureAwait(false))
        {
            await ctx.Interaction.SendConfirmAsync($"Added {user.Mention} to ignored users!").ConfigureAwait(false);
            return;
        }
        await ctx.Interaction.SendConfirmAsync($"Removed {user.Mention} from ignored users!").ConfigureAwait(false);
    }

    [SlashCommand("toggle-channel", "Ignore a specified channel."), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task ToggleChannel(ITextChannel channel)
    {
        if (await Service.ToggleIgnoredUser(ctx.Guild.Id, ctx.User.Id, channel.Id.ToString()).ConfigureAwait(false))
        {
            await ctx.Interaction.SendConfirmAsync($"Added {channel.Mention} to ignored channels!").ConfigureAwait(false);
            return;
        }
        await ctx.Interaction.SendConfirmAsync($"Removed {channel.Mention} from ignored channels!").ConfigureAwait(false);
    }

    [SlashCommand("toggle-global", "Enable or disable highlights globally."), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task ToggleGlobal([Summary("enabled", "Are highlights enabled globally?")] bool enabled)
    {
        if (enabled)
        {
            await Service.ToggleHighlights(ctx.Guild.Id, ctx.User.Id, enabled).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync("Highlights enabled!").ConfigureAwait(false);
            return;
        }

        await Service.ToggleHighlights(ctx.Guild.Id, ctx.User.Id, enabled).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("Highlights disabled.").ConfigureAwait(false);
    }
}
