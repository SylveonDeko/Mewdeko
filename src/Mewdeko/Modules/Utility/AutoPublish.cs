using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Commands for managing auto-publishing of messages in announcement channels.
    /// </summary>
    /// <param name="interactiveService">The interactive service.</param>
    public class AutoPublish(InteractiveService interactiveService) : MewdekoSubmodule<AutoPublishService>
    {
        /// <summary>
        ///     Enables auto-publishing for a specified news channel within the guild.
        /// </summary>
        /// <param name="channel">The news channel to enable auto-publishing for.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AddAutoPublish(ITextChannel channel)
        {
            if (channel is not INewsChannel chan)
            {
                await ReplyErrorAsync(Strings.AutopublishChannelNotNews(ctx.Guild.Id));
                return;
            }

            if (!await Service.PermCheck(chan))
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
        ///     Adds a user to the auto-publish blacklist for a specified channel.
        /// </summary>
        /// <param name="user">The user to blacklist.</param>
        /// <param name="channel">The channel for which to apply the blacklist.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AddPublishBlacklist(IUser user, ITextChannel channel)
        {
            if (await Service.CheckIfExists(channel.Id))
            {
                await ReplyErrorAsync(Strings.AutopublishChannelNotSetup(ctx.Guild.Id));
                return;
            }

            if (channel is not INewsChannel chan)
            {
                await ReplyErrorAsync(Strings.AutopublishChannelNotNews(ctx.Guild.Id));
                return;
            }

            if (!await Service.PermCheck(chan))
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
        ///     Adds a word to the auto-publish blacklist for a specified channel.
        /// </summary>
        /// <param name="channel">The channel for which to apply the blacklist.</param>
        /// <param name="word">The word to blacklist.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AddPublishBlacklist(ITextChannel channel, [Remainder] string word)
        {
            if (await Service.CheckIfExists(channel.Id))
            {
                await ReplyErrorAsync(Strings.AutopublishChannelNotSetup(ctx.Guild.Id));
                return;
            }

            if (channel is not INewsChannel chan)
            {
                await ReplyErrorAsync(Strings.AutopublishChannelNotNews(ctx.Guild.Id));
                return;
            }

            if (!await Service.PermCheck(chan))
            {
                await ReplyErrorAsync(Strings.MissingManageMessages(ctx.Guild.Id, ctx.Guild));
                return;
            }

            if (word.Length > 40)
            {
                await ReplyErrorAsync(Strings.WordPublishMaxLength(ctx.Guild.Id));
                return;
            }

            var added = await Service.AddWordToBlacklist(channel.Id, word.ToLower());
            if (!added)
                await ReplyErrorAsync(Strings.WordAlreadyBlacklistedAutopub(ctx.Guild.Id, word.ToLower()));
            else
                await ReplyConfirmAsync(Strings.WordPublishBlacklisted(ctx.Guild.Id, word.ToLower(), channel.Mention));
        }

        /// <summary>
        ///     Removes a user from the auto-publish blacklist for a specified channel.
        /// </summary>
        /// <param name="user">The user to unblacklist.</param>
        /// <param name="channel">The channel for which to remove the blacklist.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RemovePublishBlacklist(IUser user, ITextChannel channel)
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
        ///     Removes a word from the auto-publish blacklist for a specified channel.
        /// </summary>
        /// <param name="channel">The channel for which to remove the blacklist.</param>
        /// <param name="word">The word to unblacklist.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RemovePublishBlacklist(ITextChannel channel, [Remainder] string word)
        {
            if (await Service.CheckIfExists(channel.Id))
            {
                await ReplyErrorAsync(Strings.AutopublishChannelNotSetup(ctx.Guild.Id));
                return;
            }

            var removed = await Service.RemoveWordFromBlacklist(channel.Id, word);

            if (!removed)
                await ReplyErrorAsync(Strings.WordNotBlacklistedAutopub(ctx.Guild.Id, word));
            else
                await ReplyConfirmAsync(Strings.AutopublishUserUnblacklisted(ctx.Guild.Id, word, channel.Mention));
        }

        /// <summary>
        ///     Disables auto-publishing for a specified news channel within the guild.
        /// </summary>
        /// <param name="channel">The news channel to disable auto-publishing for.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RemoveAutoPublish(ITextChannel channel)
        {
            if (channel is not INewsChannel)
            {
                await ReplyErrorAsync(Strings.AutopublishChannelNotNews(ctx.Guild.Id));
                return;
            }

            var removed = await Service.RemoveAutoPublish(ctx.Guild.Id, channel.Id);
            if (!removed)
                await ReplyErrorAsync(Strings.AutoPublishNotSet(ctx.Guild.Id, channel.Mention));
            else
                await ReplyConfirmAsync(Strings.AutoPublishRemoved(ctx.Guild.Id, channel.Mention));
        }

        /// <summary>
        ///     Displays a list of channels with auto-publish enabled and their associated blacklists.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
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

            await interactiveService.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
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
}