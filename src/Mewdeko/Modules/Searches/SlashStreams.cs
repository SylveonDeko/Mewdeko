using DataModel;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches;

/// <summary>
///     Slash commands for managing stream notifications within a guild.
/// </summary>
/// <param name="dbFactory">The database connection factory.</param>
/// <param name="interactivity">The interactive service used for pagination.</param>
[Group("stream", "Manage stream notifications")]
public class SlashStreams(IDataConnectionFactory dbFactory, InteractiveService interactivity)
    : MewdekoSlashModuleBase<StreamNotificationService>
{
    /// <summary>
    ///     Adds a new stream to the notification list for the current guild.
    /// </summary>
    /// <param name="link">The link to the stream to be added.</param>
    [SlashCommand("add", "Follow a stream and get notified when it goes live")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task StreamAdd([Summary("link", "The stream link")] string link)
    {
        await DeferAsync().ConfigureAwait(false);
        var data = await Service.FollowStream(ctx.Guild.Id, ctx.Channel.Id, link).ConfigureAwait(false);
        if (data is null)
        {
            await ReplyErrorAsync(Strings.StreamNotAdded(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var embed = Service.GetEmbed(ctx.Guild.Id, data);
        await ctx.Interaction.FollowupAsync(Strings.StreamTracked(ctx.Guild.Id), embed: embed.Build())
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a stream from the notification list based on its index.
    /// </summary>
    /// <param name="index">The 1-based index of the stream in the notification list to be removed.</param>
    [SlashCommand("remove", "Unfollow a stream by its list number")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task StreamRemove([Summary("index", "The stream number from the list")] int index)
    {
        if (--index < 0)
        {
            await ReplyErrorAsync(Strings.StreamNo(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var fs = await Service.UnfollowStreamAsync(ctx.Guild.Id, index).ConfigureAwait(false);
        if (fs is null)
        {
            await ReplyErrorAsync(Strings.StreamNo(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ReplyConfirmAsync(Strings.StreamRemoved(ctx.Guild.Id,
            Format.Bold(fs.Username),
            fs.Type)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears all streams from the guild's notification list.
    /// </summary>
    [SlashCommand("clear", "Unfollow every stream in this server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task StreamsClear()
    {
        await DeferAsync().ConfigureAwait(false);
        var count = await Service.ClearAllStreams(ctx.Guild.Id).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.StreamsCleared(ctx.Guild.Id, count)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all streams currently followed by the guild.
    /// </summary>
    [SlashCommand("list", "List the followed streams")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task StreamList()
    {
        await DeferAsync().ConfigureAwait(false);
        var streams = new List<FollowedStream>();

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var all = await dbContext.FollowedStreams
            .Where(fs => fs.GuildId == ctx.Guild.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();

        for (var index = all.Count - 1; index >= 0; index--)
        {
            var fs = all[index];
            if (((SocketGuild)ctx.Guild).GetTextChannel(fs.ChannelId) is null)
                await Service.UnfollowStreamAsync(fs.GuildId, index).ConfigureAwait(false);
            else
                streams.Insert(0, fs);
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(streams.Count / 12)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var elements = streams.Skip(page * 12).Take(12)
                .ToList();

            if (elements.Count == 0)
            {
                return new PageBuilder()
                    .WithDescription(Strings.StreamsNone(ctx.Guild.Id))
                    .WithErrorColor();
            }

            var eb = new PageBuilder()
                .WithTitle(Strings.StreamsFollowTitle(ctx.Guild.Id))
                .WithOkColor();
            for (var index = 0; index < elements.Count; index++)
            {
                var elem = elements[index];
                eb.AddField(
                    $"**#{index + 1 + 12 * page}** {elem.Username.ToLower()}",
                    $"{(FType)elem.Type}\n<#{elem.ChannelId}>");
            }

            return eb;
        }
    }

    /// <summary>
    ///     Toggles the setting for notifying the guild when a followed stream goes offline.
    /// </summary>
    [SlashCommand("offline", "Toggle notifications when streams go offline")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task StreamOffline()
    {
        var newValue = await Service.ToggleStreamOffline(ctx.Guild.Id);
        if (newValue)
            await ReplyConfirmAsync(Strings.StreamOffEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.StreamOffDisabled(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets a custom notification message for a specific stream in the guild's notification list.
    /// </summary>
    /// <param name="index">The 1-based index of the stream to set the message for.</param>
    /// <param name="message">The custom message to be sent when the stream goes live. Leave empty to reset.</param>
    [SlashCommand("message", "Set the live message for a followed stream")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task StreamMessage([Summary("index", "The stream number from the list")] int index,
        [Summary("message", "The message, leave empty to reset")]
        string? message = null)
    {
        if (--index < 0)
        {
            await ReplyErrorAsync(Strings.StreamNo(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        message ??= string.Empty;

        var (followed, fs) = await Service.SetStreamMessage(ctx.Guild.Id, index, message);

        if (!followed)
        {
            await ReplyConfirmAsync(Strings.StreamNotFollowing(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            await ReplyConfirmAsync(Strings.StreamMessageReset(ctx.Guild.Id, Format.Bold(fs.Username)))
                .ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.StreamMessageSet(ctx.Guild.Id, Format.Bold(fs.Username)))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets the offline message for a specific stream.
    /// </summary>
    /// <param name="index">The 1-based index of the stream.</param>
    /// <param name="message">The offline message template with placeholders. Leave empty to reset.</param>
    [SlashCommand("offline-message", "Set the offline message for a followed stream")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task StreamOfflineMessage([Summary("index", "The stream number from the list")] int index,
        [Summary("message", "The message, leave empty to reset")]
        string? message = null)
    {
        if (--index < 0)
        {
            await ReplyErrorAsync(Strings.StreamNo(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        message ??= string.Empty;

        var (followed, fs) = await Service.SetStreamOfflineMessage(ctx.Guild.Id, index, message);

        if (!followed)
        {
            await ReplyConfirmAsync(Strings.StreamNotFollowing(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            await ReplyConfirmAsync(Strings.StreamOfflineMessageReset(ctx.Guild.Id, Format.Bold(fs.Username)))
                .ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.StreamOfflineMessageSet(ctx.Guild.Id, Format.Bold(fs.Username)))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Checks the live status of a stream by its URL.
    /// </summary>
    /// <param name="url">The URL of the stream to check.</param>
    [SlashCommand("check", "Check whether a stream is live")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task StreamCheck([Summary("url", "The stream url")] string url)
    {
        await DeferAsync().ConfigureAwait(false);
        try
        {
            var data = await Service.GetStreamDataAsync(url).ConfigureAwait(false);
            if (data is null)
            {
                await ReplyErrorAsync(Strings.NoChannelFound(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (data.IsLive)
            {
                await ReplyConfirmAsync(Strings.StreamerOnline(ctx.Guild.Id,
                        Format.Bold(data.Name),
                        Format.Bold(data.Viewers.ToString())))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmAsync(Strings.StreamerOffline(ctx.Guild.Id, data.Name))
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            await ReplyErrorAsync(Strings.NoChannelFound(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets or shows the custom stream notification message template for all streams in the guild. Opens a modal
    ///     to enter the template, or shows the current template and available placeholders.
    /// </summary>
    /// <param name="show">Whether to show the current template and placeholders instead of opening the editor.</param>
    [SlashCommand("template", "Set or show the stream notification template")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task StreamTemplate(
        [Summary("show", "Show the current template and placeholders instead of editing")]
        bool show = false)
    {
        if (!show)
        {
            await RespondWithModalAsync<StreamTemplateModal>("stream_template").ConfigureAwait(false);
            return;
        }

        await DeferAsync().ConfigureAwait(false);
        var currentTemplate = await Service.GetCustomStreamMessageAsync(ctx.Guild.Id);

        var embed = new EmbedBuilder()
            .WithTitle(Strings.StreamTemplateTitle(ctx.Guild.Id))
            .WithColor(Mewdeko.OkColor);

        if (string.IsNullOrWhiteSpace(currentTemplate))
        {
            embed.WithDescription(Strings.StreamTemplateNotSet(ctx.Guild.Id));
        }
        else
        {
            embed.AddField(Strings.StreamTemplateCurrent(ctx.Guild.Id),
                Format.Code(currentTemplate.TrimTo(1000)));
        }

        var placeholders = StreamNotificationService.GetStreamPlaceholders();
        foreach (var category in placeholders)
        {
            var placeholderText = string.Join("\n",
                category.Value.Select(p => $"`{p.Placeholder}`: {p.Description}"));
            embed.AddField(category.Key, placeholderText.TrimTo(1000));
        }

        embed.WithFooter(Strings.StreamTemplateFooter(ctx.Guild.Id));
        await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles the stream template modal submission, saving or resetting the guild's stream notification template.
    /// </summary>
    /// <param name="modal">The submitted modal containing the template.</param>
    [ModalInteraction("stream_template", true)]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task StreamTemplateSubmitted(StreamTemplateModal modal)
    {
        var template = modal.Template;

        if (string.IsNullOrWhiteSpace(template) || template.Trim().ToLowerInvariant() == "reset")
        {
            await Service.SetCustomStreamMessageAsync(ctx.Guild.Id, null);
            await ReplyConfirmAsync(Strings.StreamTemplateReset(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.SetCustomStreamMessageAsync(ctx.Guild.Id, template);
        await ReplyConfirmAsync(Strings.StreamTemplateSet(ctx.Guild.Id)).ConfigureAwait(false);
    }
}