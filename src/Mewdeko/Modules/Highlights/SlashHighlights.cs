using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.Highlights.Services;

namespace Mewdeko.Modules.Highlights;

/// <summary>
///     Slash module for managing highlights.
/// </summary>
[Group("highlights", "Set or manage highlights")]
public class SlashHighlights : MewdekoSlashModuleBase<HighlightsService>
{
    /// <summary>
    ///     The actions available for the highlight command.
    /// </summary>
    public enum HighlightAction
    {
        /// <summary>
        ///     Adds a highlight.
        /// </summary>
        Add,

        /// <summary>
        ///     Lists current highlights.
        /// </summary>
        List,

        /// <summary>
        ///     Deletes a highlight.
        /// </summary>
        Delete,

        /// <summary>
        ///     Attempts to match a highlight to a phrase.
        /// </summary>
        Match,

        /// <summary>
        ///     Toggles whether highlights ignore a user.
        /// </summary>
        ToggleUser,

        /// <summary>
        ///     Toggles whether highlights ignore a channel.
        /// </summary>
        ToggleChannel,

        /// <summary>
        ///     Toggles whether highlights are enabled.
        /// </summary>
        ToggleGlobal
    }

    private readonly IDataConnectionFactory dbFactory;
    private readonly InteractiveService interactivity;

    /// <summary>
    ///     Initializes a new instance of <see cref="SlashHighlights" />.
    /// </summary>
    /// <param name="interactivity">The embed pagination service</param>
    /// <param name="dbFactory">The db context provider</param>
    public SlashHighlights(InteractiveService interactivity, IDataConnectionFactory dbFactory)
    {
        this.interactivity = interactivity;
        this.dbFactory = dbFactory;
    }

    /// <summary>
    ///     Adds, lists, removes, or matches highlights, and toggles ignored users, ignored channels, or highlights
    ///     globally.
    /// </summary>
    /// <param name="action">
    ///     <see cref="HighlightAction" />
    /// </param>
    /// <param name="words">The words for the selected action, or true/false for the global toggle.</param>
    /// <param name="user">The user to toggle ignoring when the action is ToggleUser.</param>
    /// <param name="channel">The channel to toggle ignoring when the action is ToggleChannel.</param>
    [SlashCommand("manage", "Add, list, delete, match or toggle highlights.")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Highlight(
        [Summary("action", "What to do with your highlights.")]
        HighlightAction action,
        [Autocomplete(typeof(HighlightAutocompleter))] [Summary("words", "The words for the action.")]
        string? words = null,
        [Summary("user", "The user to toggle ignoring.")]
        IUser? user = null,
        [Summary("channel", "The channel to toggle ignoring.")]
        ITextChannel? channel = null)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var highlights = await (dbContext.Highlights.Where(x => x.GuildId == ctx.Guild.Id && x.UserId == ctx.User.Id))
            .ToListAsync();

        switch (action)
        {
            case HighlightAction.Add:
                if (string.IsNullOrWhiteSpace(words))
                {
                    await ctx.Interaction.SendErrorAsync(Strings.HighlightPhraseRequired(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                if (highlights.Count > 0 && highlights.Select(x => x.Word.ToLower()).Contains(words.ToLower()))
                {
                    await ctx.Interaction.SendErrorAsync(Strings.HighlightAlreadyExists(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                }
                else
                {
                    await Service.AddHighlight(ctx.Guild.Id, ctx.User.Id, words).ConfigureAwait(false);
                    await ctx.Interaction.SendConfirmAsync(Strings.HighlightAdded(ctx.Guild.Id, Format.Code(words)))
                        .ConfigureAwait(false);
                }

                break;
            case HighlightAction.List:
                if (highlights.Count == 0)
                {
                    await ctx.Interaction.SendErrorAsync(Strings.HighlightNoHighlights(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(highlights.Count / 10)
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();

                await interactivity.SendPaginatorAsync(paginator, ctx.Interaction as SocketInteraction,
                    TimeSpan.FromMinutes(60)).ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    var highlightsEnumerable = highlights.Skip(page * 10).Take(10);
                    return new PageBuilder().WithOkColor()
                        .WithTitle(Strings.HighlightListTitle(ctx.Guild.Id, highlights.Count))
                        .WithDescription(string.Join("\n",
                            highlightsEnumerable.Select(x => $"{highlights.IndexOf(x) + 1}. {x.Word}")));
                }

                break;
            case HighlightAction.Delete:
                if (string.IsNullOrWhiteSpace(words))
                {
                    await ctx.Interaction.SendErrorAsync(Strings.HighlightEmptyDelete(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                if (highlights.Count == 0)
                {
                    await ctx.Interaction.SendErrorAsync(Strings.HighlightCannotDelete(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                if (int.TryParse(words, out var number))
                {
                    var todelete = highlights.ElementAtOrDefault(number - 1);
                    if (todelete is null)
                    {
                        await ctx.Interaction.SendErrorAsync(Strings.HighlightNotExist(ctx.Guild.Id), Config)
                            .ConfigureAwait(false);
                        return;
                    }

                    await Service.RemoveHighlight(todelete).ConfigureAwait(false);
                    await ctx.Interaction
                        .SendConfirmAsync(Strings.HighlightRemoved(ctx.Guild.Id, Format.Code(todelete.Word)))
                        .ConfigureAwait(false);
                    return;
                }

                if (!highlights.Select(x => x.Word).Contains(words))
                {
                    await ctx.Interaction.SendErrorAsync(Strings.HighlightNotExist(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                await Service.RemoveHighlight(highlights.Find(x => x.Word == words)).ConfigureAwait(false);
                await ctx.Interaction.SendConfirmAsync(Strings.HighlightRemoved(ctx.Guild.Id, Format.Code(words)))
                    .ConfigureAwait(false);
                break;
            case HighlightAction.Match:
                if (string.IsNullOrWhiteSpace(words))
                {
                    await ctx.Interaction.SendErrorAsync(Strings.HighlightEmptyMatch(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                if (highlights.Count == 0)
                {
                    await ctx.Interaction.SendErrorAsync(Strings.HighlightNoMatches(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                var matched = highlights.Where(x => words.ToLower().Contains(x.Word.ToLower())).ToList();
                if (matched.Count == 0)
                {
                    await ctx.Interaction.SendErrorAsync(Strings.HighlightNoMatchFound(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                var matchPaginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory1)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(matched.Count / 10)
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();

                await interactivity.SendPaginatorAsync(matchPaginator, ctx.Interaction as SocketInteraction,
                    TimeSpan.FromMinutes(60)).ConfigureAwait(false);

                async Task<PageBuilder> PageFactory1(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    var highlightsEnumerable = matched.Skip(page * 10).Take(10);
                    return new PageBuilder().WithOkColor()
                        .WithTitle(Strings.HighlightListTitle(ctx.Guild.Id, highlights.Count))
                        .WithDescription(string.Join("\n",
                            highlightsEnumerable.Select(x => $"{highlights.IndexOf(x) + 1}. {x.Word}")));
                }

                break;
            case HighlightAction.ToggleUser:
                if (user is null)
                {
                    await ctx.Interaction.SendErrorAsync(Strings.HighlightUserChannelNotFound(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                var userIgnored = await Service.ToggleIgnoredUser(ctx.Guild.Id, ctx.User.Id, user.Id.ToString())
                    .ConfigureAwait(false);

                await ctx.Interaction.SendConfirmAsync(
                        userIgnored
                            ? Strings.HighlightIgnoredUserAdded(ctx.Guild.Id, user.Mention)
                            : Strings.HighlightIgnoredUserRemoved(ctx.Guild.Id, user.Mention))
                    .ConfigureAwait(false);
                break;
            case HighlightAction.ToggleChannel:
                if (channel is null)
                {
                    await ctx.Interaction.SendErrorAsync(Strings.HighlightUserChannelNotFound(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                var channelIgnored = await Service
                    .ToggleIgnoredChannel(ctx.Guild.Id, ctx.User.Id, channel.Id.ToString())
                    .ConfigureAwait(false);

                await ctx.Interaction.SendConfirmAsync(
                        channelIgnored
                            ? Strings.HighlightIgnoredChannelAdded(ctx.Guild.Id, channel.Mention)
                            : Strings.HighlightIgnoredChannelRemoved(ctx.Guild.Id, channel.Mention))
                    .ConfigureAwait(false);
                break;
            case HighlightAction.ToggleGlobal:
                if (string.IsNullOrWhiteSpace(words) || !bool.TryParse(words, out var enabled))
                {
                    await ctx.Interaction.SendErrorAsync(Strings.HighlightToggleInvalid(ctx.Guild.Id), Config)
                        .ConfigureAwait(false);
                    return;
                }

                await Service.ToggleHighlights(ctx.Guild.Id, ctx.User.Id, enabled).ConfigureAwait(false);
                await ctx.Interaction.SendConfirmAsync(enabled
                        ? Strings.HighlightEnabled(ctx.Guild.Id)
                        : Strings.HighlightDisabled(ctx.Guild.Id))
                    .ConfigureAwait(false);
                break;
        }
    }
}