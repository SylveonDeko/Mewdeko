using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Moderation.Services;

namespace Mewdeko.Modules.Moderation;

public partial class Moderation
{
    /// <summary>
    /// Module for purging messages.
    /// </summary>
    /// <param name="client"></param>
    [Group]
    public class PurgeCommands(DiscordSocketClient client) : MewdekoSubmodule<PurgeService>
    {
        private static readonly TimeSpan TwoWeeks = TimeSpan.FromDays(14);

        /// <summary>
        /// Purges messages from the current channel.
        /// </summary>
        /// <param name="parameter">The parameters to use</param>
        [Cmd, Aliases, UserPerm(GuildPermission.ManageMessages),
         RequireContext(ContextType.Guild)]
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
        /// Purges messages from the current channel with the specific amount and parameters.
        ///
        /// The options are:
        /// <code>
        /// -s, --safe: Purge messages that are not pinned
        /// -nb, --nobots: Purge messages that are not from bots
        /// -ob, --onlybots: Purge messages that are from bots
        /// -b, --before: Purge messages before a specific time
        /// -a, --after: Purge messages after a specific time
        /// -he, --hasembed: Purge messages that have an embed
        /// -ne, --noembed: Purge messages that do not have an embed
        /// -c, --contains: Purge messages that contain a specific string
        /// </code>
        /// </summary>
        /// <param name="count">The amount of messages to purge</param>
        /// <param name="parameters">The parameters to use</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(ChannelPermission.ManageMessages), BotPerm(ChannelPermission.ManageMessages), Priority(1)]
        public async Task Purge(ulong count, string? parameters = null)
        {
            if (await client.Rest.GetUserAsync(count) is not null)
            {
                await Purge(count, 100, parameters);
                return;
            }

            if (parameters is null)
            {
                await Service.PurgeWhere(ctx.Channel as ITextChannel, count, x => x.Channel.Id == ctx.Channel.Id);
                return;
            }

            var options = new List<Func<IMessage, bool>>();

            var parts = parameters.Split(new[]
            {
                ' ', ','
            }, StringSplitOptions.RemoveEmptyEntries);
            string input = null;

            for (var i = 0; i < parts.Length; i++)
            {
                switch (parts[i])
                {
                    case "-s":
                    case "--safe":
                        options.Add(x => !x.IsPinned);
                        break;
                    case "-nb":
                    case "--nobots":
                        options.Add(x => !x.Author.IsBot);
                        break;
                    case "-ob":
                    case "--onlybots":
                        options.Add(x => x.Author.IsBot);
                        break;
                    case "-he":
                    case "--hasembed":
                        options.Add(x => x.Embeds.Count > 0);
                        break;
                    case "-ne":
                    case "--noembed":
                        options.Add(x => x.Embeds.Count == 0);
                        break;
                    case "-c":
                    case "--contains":
                        if (i + 1 < parts.Length) input = parts[++i];
                        if (!string.IsNullOrEmpty(input))
                            options.Add(x => x.Content.Contains(input, StringComparison.InvariantCultureIgnoreCase));
                        break;
                    case "-b":
                    case "--before":
                    case "-a":
                    case "--after":
                        if (i + 1 < parts.Length)
                        {
                            if (DateTimeOffset.TryParse(parts[++i], out var date))
                            {
                                if (parts[i - 1].Contains("before"))
                                    options.Add(x => x.Timestamp < date);
                                else
                                    options.Add(x => x.Timestamp > date);
                            }
                        }

                        break;
                }
            }

            await Service.PurgeWhere((ITextChannel)ctx.Channel, count, CombinedPredicate).ConfigureAwait(false);
            return;

            bool CombinedPredicate(IMessage m) => options.All(p => p(m));
        }

        //Purge @user [x]
        /// <summary>
        /// Purges messages from the current channel with the specific user and amount.
        /// </summary>
        /// <param name="user">The user to purge messages from</param>
        /// <param name="count">The amount of messages to purge</param>
        /// <param name="parameter">The parameter to use</param>
        /// <returns></returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(ChannelPermission.ManageMessages), BotPerm(ChannelPermission.ManageMessages), Priority(0)]
        public Task Purge(IGuildUser user, ulong count = 100, string? parameter = null) =>
            Purge(user.Id, count, parameter);

        //Purge userid [x]
        /// <summary>
        /// Purges messages from the current channel with the specific user id and amount.
        /// </summary>
        /// <param name="userId">The user id to purge messages from</param>
        /// <param name="count">The amount of messages to purge</param>
        /// <param name="parameter">The parameter to use</param>
        /// <returns></returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(ChannelPermission.ManageMessages), BotPerm(ChannelPermission.ManageMessages), Priority(0)]
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

            if (parameter is "-s" or "--safe")
            {
                return Service.PurgeWhere((ITextChannel)ctx.Channel, count,
                    m => m.Author.Id == userId && DateTime.UtcNow - m.CreatedAt < TwoWeeks && !m.IsPinned);
            }

            return Service.PurgeWhere((ITextChannel)ctx.Channel, count,
                m => m.Author.Id == userId && DateTime.UtcNow - m.CreatedAt < TwoWeeks);
        }

        /// <summary>
        /// Purges all messages from accesible channels for a user.
        /// </summary>
        /// <param name="user">The user who's messages to purge</param>
        /// <param name="messageCount">The count of messages to search</param>
        [Cmd, Aliases, UserPerm(GuildPermission.ManageMessages), BotPerm(GuildPermission.ManageMessages)]
        public async Task PurgeUser(IUser user, int messageCount)
        {
            if (messageCount is > 1000 or < 1)
            {
                await ctx.Channel.SendErrorAsync("Invalid amount specified. Max is 1000, Minimum is 1.", Config);
                return;
            }

            await ctx.Channel.SendConfirmAsync(
                $"Searching the last {messageCount} messages in all channels to purge messages from {user.Mention}....");

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
                        $"Deleted {deletedMessageCount} messages from {user.Mention} in {successCount} channels.");
                    break;
                case > 0 when failCount > 0:
                    await ctx.Channel.SendConfirmAsync(
                        $"Deleted {deletedMessageCount} messages from {user.Mention} in {successCount} channels." +
                        $"\n{failCount} channels were unable to be processed due to permission issues.");
                    break;
                case 0 when failCount > 0:
                    await ctx.Channel.SendErrorAsync(
                        "No messages were processed due to permission issues. Please make sure Mewdeko can see all channels.",
                        Config);
                    break;
                case 0 when failCount is 0:
                    await ctx.Channel.SendConfirmAsync($"There were no messages to delete from {user.Mention}");
                    break;
            }
        }
    }
}