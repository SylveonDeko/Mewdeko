using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

/// <summary>
///     Contains slash commands for managing auto-publish settings in announcement channels.
/// </summary>
[Group("autopublish", "Auto publish stuff in announcement channels!")]
public class SlashAutoPublish(InteractiveService interactiveService) : MewdekoSlashSubmodule<AutoPublishService>
{
    /// <summary>
    ///     Adds a channel to the list of channels where messages are auto-published.
    ///     Requires the command invoker to have Administrator permissions in the guild.
    /// </summary>
    /// <param name="channel">The news channel to be added for auto-publishing.</param>
    /// <returns>A task that represents the asynchronous operation of adding a channel to auto-publish.</returns>
    [SlashCommand("add", "Adds a channel to be used with auto publish")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task AddAutoPublish(INewsChannel channel)
    {
        if (!await Service.PermCheck(channel))
        {
            await ReplyErrorAsync(Strings.MissingManageMessages(ctx.Guild.Id, ctx.Guild));
            return;
        }

        var added = await Service.AddAutoPublish(ctx.Guild.Id, channel.Id);
        if (!added)
            await ReplyErrorAsync(Strings.AutoPublishAlreadySet(ctx.Guild.Id, channel.Mention));
        else
            await ReplyConfirmAsync(Strings.AutoPublishSet(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Blacklists a user from having their messages auto-published in a specified channel.
    ///     Requires the command invoker to have Administrator permissions in the guild.
    /// </summary>
    /// <param name="user">The user to be blacklisted.</param>
    /// <param name="channel">The news channel from which the user is blacklisted.</param>
    /// <returns>A task that represents the asynchronous operation of adding a user to the blacklist.</returns>
    [SlashCommand("blacklist-user", "Blacklist a user from getting their message auto published")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task AddPublishBlacklist(IUser user, INewsChannel channel)
    {
        if (await Service.CheckIfExists(channel.Id))
        {
            await ReplyErrorAsync(Strings.AutopublishChannelNotSetup(ctx.Guild.Id));
            return;
        }

        if (!await Service.PermCheck(channel))
        {
            await ReplyErrorAsync(Strings.MissingManageMessages(ctx.Guild.Id, ctx.Guild));
            return;
        }

        var added = await Service.AddUserToBlacklist(channel.Id, user.Id);
        if (!added)
            await ReplyErrorAsync(Strings.AutopublishUserAlreadyBlacklisted(ctx.Guild.Id, user.Mention));
        else
            await ReplyConfirmAsync(Strings.AutopublishUserBlacklisted(ctx.Guild.Id, user.Mention, channel.Mention));
    }

    /// <summary>
    ///     Blacklists a word, preventing messages containing this word from being auto-published in a specified channel.
    ///     Requires the command invoker to have Administrator permissions in the guild.
    /// </summary>
    /// <param name="word">The word to be blacklisted.</param>
    /// <param name="channel">The news channel where the word is to be blacklisted.</param>
    /// <returns>A task that represents the asynchronous operation of adding a word to the blacklist.</returns>
    [SlashCommand("blacklist-word", "Blacklist a word to stop a message containing this word getting auto published")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task AddPublishBlacklist(string word, INewsChannel channel)
    {
        if (await Service.CheckIfExists(channel.Id))
        {
            await ReplyErrorAsync(Strings.AutopublishChannelNotSetup(ctx.Guild.Id));
            return;
        }

        if (!await Service.PermCheck(channel))
        {
            await ReplyErrorAsync(Strings.MissingManageMessages(ctx.Guild.Id, ctx.Guild));
            return;
        }

        if (word.Length > 40)
        {
            await ReplyErrorAsync(Strings.WordPublishMaxLength(ctx.Guild.Id));
            return;
        }

        var added = await Service.AddWordToBlacklist(channel.Id, word);
        if (!added)
            await ReplyErrorAsync(Strings.WordAlreadyBlacklistedAutopub(ctx.Guild.Id, word));
        else
            await ReplyConfirmAsync(Strings.WordPublishBlacklisted(ctx.Guild.Id, word, channel.Mention));
    }

    /// <summary>
    ///     Removes a user from the blacklist, allowing their messages to be auto-published again in a specified channel.
    ///     Requires the command invoker to have Administrator permissions in the guild.
    /// </summary>
    /// <param name="user">The user to be removed from the blacklist.</param>
    /// <param name="channel">The news channel from which the user is removed.</param>
    /// <returns>A task that represents the asynchronous operation of removing a user from the blacklist.</returns>
    [SlashCommand("unblacklist-user", "Removes a user from the blacklist")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task RemovePublishBlacklist(IUser user, INewsChannel channel)
    {
        if (await Service.CheckIfExists(channel.Id))
        {
            await ReplyErrorAsync(Strings.AutopublishChannelNotSetup(ctx.Guild.Id));
            return;
        }

        var removed = await Service.RemoveUserFromBlacklist(channel.Id, user.Id);

        if (!removed)
            await ReplyErrorAsync(Strings.AutopublishUserNotBlacklisted(ctx.Guild.Id, user.Mention));
        else
            await ReplyConfirmAsync(Strings.AutopublishUserUnblacklisted(ctx.Guild.Id, user.Mention, channel.Mention));
    }


    /// <summary>
    ///     Removes a word from the blacklist, allowing messages containing this word to be auto-published again in a specified
    ///     channel.
    ///     Requires the command invoker to have Administrator permissions in the guild.
    /// </summary>
    /// <param name="word">The word to be removed from the blacklist.</param>
    /// <param name="channel">The news channel from which the word is removed.</param>
    /// <returns>A task that represents the asynchronous operation of removing a word from the blacklist.</returns>
    [SlashCommand("unblacklist-word", "Removes a word from the blacklist")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task RemovePublishBlacklist(string word, INewsChannel channel)
    {
        if (await Service.CheckIfExists(channel.Id))
        {
            await ReplyErrorAsync(Strings.AutopublishChannelNotSetup(ctx.Guild.Id));
            return;
        }

        var removed = await Service.RemoveWordFromBlacklist(channel.Id, word.ToLower());

        if (!removed)
            await ReplyErrorAsync(Strings.WordNotBlacklistedAutopub(ctx.Guild.Id, word.ToLower()));
        else
            await ReplyConfirmAsync(Strings.WordPublishUnBlacklisted(ctx.Guild.Id, word.ToLower(), channel.Mention));
    }

    /// <summary>
    ///     Removes a channel from the list of auto-publish channels.
    ///     Requires the command invoker to have Administrator permissions in the guild.
    /// </summary>
    /// <param name="channel">The news channel to be removed from auto-publishing.</param>
    /// <returns>A task that represents the asynchronous operation of removing a channel from auto-publish.</returns>
    [SlashCommand("remove", "Removes a channel from auto publish")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task RemoveAutoPublish(INewsChannel channel)
    {
        var removed = await Service.RemoveAutoPublish(ctx.Guild.Id, channel.Id);
        if (!removed)
            await ReplyErrorAsync(Strings.AutoPublishNotSet(ctx.Guild.Id, channel.Mention));
        else
            await ReplyConfirmAsync(Strings.AutoPublishRemoved(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Lists all channels and settings related to auto-publishing.
    ///     Requires the command invoker to have Administrator permissions in the guild.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation of listing all auto-publish channels and settings.</returns>
    [SlashCommand("list", "Lists all auto publish channels and settings")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task GetAutoPublishes()
    {
        var autoPublishes = await Service.GetAutoPublishes(ctx.Guild.Id);
        if (autoPublishes.Count == 0)
        {
            await ReplyErrorAsync(Strings.AutoPublishNotEnabled(ctx.Guild.Id));
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(autoPublishes.Count - 1)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var (autoPublish, userBlacklists, wordBlacklists) = autoPublishes[page];
            var channel = await ctx.Guild.GetChannelAsync(autoPublish.ChannelId);

            var eb = new PageBuilder()
                .WithTitle($"Auto Publish - {channel.Name.TrimTo(20)}");

            if (userBlacklists.Any())
                eb.AddField(Strings.BlacklistedUsers(ctx.Guild.Id),
                    string.Join(",", userBlacklists.Select(x => $"<@{x.User}>")));
            else
                eb.AddField("blacklisted_users", Strings.None(ctx.Guild.Id));

            if (wordBlacklists.Any())
                eb.AddField(Strings.BlacklistedWords(ctx.Guild.Id),
                    string.Join("\n", wordBlacklists.Select(x => x.Word.ToLower())));
            else
                eb.AddField(Strings.BlacklistedWords(ctx.Guild.Id), Strings.None(ctx.Guild.Id));
            return eb;
        }
    }
}