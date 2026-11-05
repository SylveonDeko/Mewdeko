using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Moderation.Services;

namespace Mewdeko.Modules.Moderation;

public partial class Moderation
{
    /// <summary>
    ///     Module for purging messages.
    /// </summary>
    /// <param name="client"></param>
    [Group]
    public class PurgeCommands(DiscordShardedClient client) : MewdekoSubmodule<PurgeService>
    {
        /// <summary>
        ///     Purges messages from the current channel.
        /// </summary>
        /// <param name="parameter">The parameters to use</param>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.ManageMessages)]
        [RequireContext(ContextType.Guild)]
        public async Task Purge(string? parameter = null)
        {
            var user = await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false);

            if (parameter is "-s" or "--safe")
            {
                await Service
                    .PurgeWhere((ITextChannel)ctx.Channel, 100, x => x.Author.Id == user.Id && !x.IsPinned)
                    .ConfigureAwait(false);
            }
            else
            {
                await Service.PurgeWhere((ITextChannel)ctx.Channel, 100, x => x.Author.Id == user.Id)
                    .ConfigureAwait(false);
            }

            ctx.Message.DeleteAfter(3);
        }

        // Purge x
        /// <summary>
        ///     Purges messages from the current channel with the specific amount and parameters.
        ///     The options are:
        ///     <code>
        /// -s, --safe: Purge messages that are not pinned
        /// -nb, --nobots: Purge messages that are not from bots
        /// -ob, --onlybots: Purge messages that are from bots
        /// -b, --before: Purge messages before a specific time
        /// -a, --after: Purge messages after a specific time
        /// -he, --hasembed: Purge messages that have an embed
        /// -ne, --noembed: Purge messages that do not have an embed
        /// -c, --contains: Purge messages that contain a specific string
        /// -l, --links: Purge messages that have a link
        /// -m, --mentions: Purge messages that mention a user or role
        /// -f, --files: Purge messages that have an attachment
        /// -i, --images: Purge messages that have an image attachment or embed
        /// -e, --emoji: Purge messages that contain an emoji
        /// </code>
        /// </summary>
        /// <param name="count">The amount of messages to purge</param>
        /// <param name="parameters">The parameters to use</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(ChannelPermission.ManageMessages)]
        [BotPerm(ChannelPermission.ManageMessages)]
        [Priority(1)]
        public async Task Purge(ulong count, string? parameters = null)
        {
            if (await client.Rest.GetUserAsync(count) is not null)
            {
                await Purge(count, 100, parameters);
                return;
            }

            if (parameters is null)
            {
                await Service.PurgeWhere(ctx.Channel as ITextChannel, count + 1, x => x.Channel.Id == ctx.Channel.Id);
                return;
            }

            var predicate = PurgeService.BuildPredicate(PurgeService.ParseFilters(parameters));
            await Service.PurgeWhere((ITextChannel)ctx.Channel, count, predicate).ConfigureAwait(false);
        }

        //Purge @user [x]
        /// <summary>
        ///     Purges messages from the current channel with the specific user and amount.
        /// </summary>
        /// <param name="user">The user to purge messages from</param>
        /// <param name="count">The amount of messages to purge</param>
        /// <param name="parameter">The parameter to use</param>
        /// <returns></returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(ChannelPermission.ManageMessages)]
        [BotPerm(ChannelPermission.ManageMessages)]
        [Priority(0)]
        public Task Purge(IGuildUser user, ulong count = 100, string? parameter = null)
        {
            return Purge(user.Id, count, parameter);
        }

        //Purge userid [x]
        /// <summary>
        ///     Purges messages from the current channel with the specific user id and amount.
        /// </summary>
        /// <param name="userId">The user id to purge messages from</param>
        /// <param name="count">The amount of messages to purge</param>
        /// <param name="parameter">The parameter to use</param>
        /// <returns></returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(ChannelPermission.ManageMessages)]
        [BotPerm(ChannelPermission.ManageMessages)]
        [Priority(0)]
        public Task Purge(ulong userId, ulong count = 100, string? parameter = null)
        {
            if (userId == ctx.User.Id)
                count++;

            switch (count)
            {
                case < 1:
                    return Task.CompletedTask;
                case > 1000:
                    count = 1000;
                    break;
            }

            var predicate = PurgeService.BuildPredicate(PurgeService.ParseFilters(parameter, userId));
            return Service.PurgeWhere((ITextChannel)ctx.Channel, count, predicate);
        }

        /// <summary>
        ///     Removes reactions from the last messages in the current channel, optionally only one emoji.
        /// </summary>
        /// <param name="count">How many recent messages to look through</param>
        /// <param name="emoji">Only remove this emoji</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(ChannelPermission.ManageMessages)]
        [BotPerm(ChannelPermission.ManageMessages)]
        public async Task PurgeReactions(int count = 100, string? emoji = null)
        {
            count = Math.Clamp(count, 1, 1000);
            IEmote? emote = null;
            if (emoji is not null && !PurgeService.TryParseEmote(emoji, out emote))
            {
                await ReplyErrorAsync(Strings.PurgeReactionsInvalidEmoji(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var cleared = await Service.ClearReactions((ITextChannel)ctx.Channel, count, emote).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.PurgeReactionsCompleted(ctx.Guild.Id, cleared)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Purges all messages from accesible channels for a user.
        /// </summary>
        /// <param name="user">The user who's messages to purge</param>
        /// <param name="messageCount">The count of messages to search</param>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.ManageMessages)]
        [BotPerm(GuildPermission.ManageMessages)]
        public async Task PurgeUser(IUser user, int messageCount)
        {
            if (messageCount is > 1000 or < 1)
            {
                await ctx.Channel.SendErrorAsync(Strings.PurgeInvalidAmount(ctx.Guild.Id), Config);
                return;
            }

            await ctx.Channel.SendConfirmAsync(
                Strings.PurgeUserSearching(ctx.Guild.Id, messageCount, user.Mention));

            var successCount = 0;
            var failCount = 0;
            var deletedMessageCount = 0;
            var channels = await ctx.Guild.GetTextChannelsAsync();
            foreach (var i in channels)
            {
                try
                {
                    var messages = await i.GetMessagesAsync(messageCount).FlattenAsync();
                    messages = messages.Where(x => x.Author == user);

                    if (messages is null || !messages.Any())
                        continue;

                    await i.DeleteMessagesAsync(messages);
                    successCount++;
                    deletedMessageCount += messages.Count();
                }
                catch
                {
                    failCount++;
                }
            }

            switch (successCount)
            {
                case > 0 when failCount is 0:
                    await ctx.Channel.SendConfirmAsync(
                        Strings.PurgeUserSuccess(ctx.Guild.Id, deletedMessageCount, user.Mention, successCount));
                    break;
                case > 0 when failCount > 0:
                    await ctx.Channel.SendConfirmAsync(
                        Strings.PurgeUserPartial(ctx.Guild.Id, deletedMessageCount, user.Mention, successCount,
                            failCount));
                    break;
                case 0 when failCount > 0:
                    await ctx.Channel.SendErrorAsync(
                        Strings.PurgeUserFail(ctx.Guild.Id), Config);
                    break;
                case 0 when failCount is 0:
                    await ctx.Channel.SendConfirmAsync(
                        Strings.PurgeUserNone(ctx.Guild.Id, user.Mention));
                    break;
            }
        }
    }
}