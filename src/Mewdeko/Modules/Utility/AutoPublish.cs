using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    /// Commands for managing auto-publishing of messages in announcement channels.
    /// </summary>
    /// <param name="interactiveService">The interactive service.</param>
    public class AutoPublish(InteractiveService interactiveService) : MewdekoSubmodule<AutoPublishService>
    {
        /// <summary>
        /// Enables auto-publishing for a specified news channel within the guild.
        /// </summary>
        /// <param name="channel">The news channel to enable auto-publishing for.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task AddAutoPublish(ITextChannel channel)
        {
            if (channel is not INewsChannel chan)
            {
                await ReplyErrorLocalizedAsync("channel_not_news_channel");
                return;
            }

            if (!await Service.PermCheck(chan))
            {
                await ReplyErrorLocalizedAsync("missing_managed_messages");
                return;
            }

            var added = await Service.AddAutoPublish(ctx.Guild.Id, channel.Id);
            if (!added)
                await ReplyErrorLocalizedAsync("auto_publish_already_set", channel.Mention);
            else
                await ReplyConfirmLocalizedAsync("auto_publish_set", channel.Mention);
        }

        /// <summary>
        /// Adds a user to the auto-publish blacklist for a specified channel.
        /// </summary>
        /// <param name="user">The user to blacklist.</param>
        /// <param name="channel">The channel for which to apply the blacklist.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task AddPublishBlacklist(IUser user, ITextChannel channel)
        {
            if (await Service.CheckIfExists(channel.Id))
            {
                await ReplyErrorLocalizedAsync("channel_not_auto_publish");
                return;
            }

            if (channel is not INewsChannel chan)
            {
                await ReplyErrorLocalizedAsync("channel_not_news_channel");
                return;
            }

            if (!await Service.PermCheck(chan))
            {
                await ReplyErrorLocalizedAsync("missing_managed_messages");
                return;
            }

            var added = await Service.AddUserToBlacklist(channel.Id, user.Id);
            if (!added)
                await ReplyErrorLocalizedAsync("user_already_blacklisted_autopub", user.Mention);
            else
                await ReplyConfirmLocalizedAsync("user_publish_blacklisted", user.Mention, channel.Mention);
        }

        /// <summary>
        /// Adds a word to the auto-publish blacklist for a specified channel.
        /// </summary>
        /// <param name="channel">The channel for which to apply the blacklist.</param>
        /// <param name="word">The word to blacklist.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task AddPublishBlacklist(ITextChannel channel, [Remainder] string word)
        {
            if (await Service.CheckIfExists(channel.Id))
            {
                await ReplyErrorLocalizedAsync("channel_not_auto_publish");
                return;
            }

            if (channel is not INewsChannel chan)
            {
                await ReplyErrorLocalizedAsync("channel_not_news_channel");
                return;
            }

            if (!await Service.PermCheck(chan))
            {
                await ReplyErrorLocalizedAsync("missing_managed_messages");
                return;
            }

            if (word.Length > 40)
            {
                await ReplyErrorLocalizedAsync("word_publish_max_length");
                return;
            }

            var added = await Service.AddWordToBlacklist(channel.Id, word.ToLower());
            if (!added)
                await ReplyErrorLocalizedAsync("word_already_blacklisted_autopub", word.ToLower());
            else
                await ReplyConfirmLocalizedAsync("word_publish_blacklisted", word.ToLower(), channel.Mention);
        }

        /// <summary>
        /// Removes a user from the auto-publish blacklist for a specified channel.
        /// </summary>
        /// <param name="user">The user to unblacklist.</param>
        /// <param name="channel">The channel for which to remove the blacklist.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task RemovePublishBlacklist(IUser user, ITextChannel channel)
        {
            if (await Service.CheckIfExists(channel.Id))
            {
                await ReplyErrorLocalizedAsync("channel_not_auto_publish");
                return;
            }

            var removed = await Service.RemoveUserFromBlacklist(channel.Id, user.Id);

            if (!removed)
                await ReplyErrorLocalizedAsync("user_not_blacklisted_autopub", user.Mention);
            else
                await ReplyConfirmLocalizedAsync("user_publish_unblacklisted", user.Mention, channel.Mention);
        }

        /// <summary>
        /// Removes a word from the auto-publish blacklist for a specified channel.
        /// </summary>
        /// <param name="channel">The channel for which to remove the blacklist.</param>
        /// <param name="word">The word to unblacklist.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task RemovePublishBlacklist(ITextChannel channel, [Remainder] string word)
        {
            if (await Service.CheckIfExists(channel.Id))
            {
                await ReplyErrorLocalizedAsync("channel_not_auto_publish");
                return;
            }

            var removed = await Service.RemoveWordFromBlacklist(channel.Id, word);

            if (!removed)
                await ReplyErrorLocalizedAsync("word_not_blacklisted_autopub", word);
            else
                await ReplyConfirmLocalizedAsync("user_publish_unblacklisted", word, channel.Mention);
        }

        /// <summary>
        /// Disables auto-publishing for a specified news channel within the guild.
        /// </summary>
        /// <param name="channel">The news channel to disable auto-publishing for.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task RemoveAutoPublish(ITextChannel channel)
        {
            if (channel is not INewsChannel)
            {
                await ReplyErrorLocalizedAsync("channel_not_news_channel");
                return;
            }

            var removed = await Service.RemoveAutoPublish(ctx.Guild.Id, channel.Id);
            if (!removed)
                await ReplyErrorLocalizedAsync("auto_publish_not_set", channel.Mention);
            else
                await ReplyConfirmLocalizedAsync("auto_publish_removed", channel.Mention);
        }

        /// <summary>
        /// Displays a list of channels with auto-publish enabled and their associated blacklists.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task GetAutoPublishes()
        {
            var autoPublishes = await Service.GetAutoPublishes(ctx.Guild.Id);
            if (autoPublishes.Count == 0)
            {
                await ReplyErrorLocalizedAsync("auto_publish_not_enabled");
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
                    eb.AddField(GetText("blacklisted_users"),
                        string.Join(",", userBlacklists.Select(x => $"<@{x.User}>")));
                else
                    eb.AddField("blacklisted_users", GetText("none"));

                if (wordBlacklists.Any())
                    eb.AddField(GetText("blacklisted_words"),
                        string.Join("\n", wordBlacklists.Select(x => x.Word.ToLower())));
                else
                    eb.AddField(GetText("blacklisted_words"), GetText("none"));
                return eb;
            }
        }
    }
}