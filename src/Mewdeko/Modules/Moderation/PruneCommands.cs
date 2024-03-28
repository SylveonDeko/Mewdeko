using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
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
        /// <param name="parameter">The parameter to use</param>
        /// <param name="input">The extra input for a parameter if needed</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(ChannelPermission.ManageMessages), BotPerm(ChannelPermission.ManageMessages), Priority(1)]
        public async Task Purge(ulong count, string? parameter = null, string? input = null)
        {
            if (await client.Rest.GetUserAsync(count) is not null)
            {
                await Purge(count, 100, parameter);
                return;
            }

            StoopidTime? time = null;
            try
            {
                time = StoopidTime.FromInput(input);
            }
            catch (ArgumentException)
            {
                //ignore
            }

            count++;
            switch (count)
            {
                case < 1:
                    return;
                case > 1000:
                    count = 1000;
                    break;
            }

            switch (parameter)
            {
                case "-s":
                case "--safe":
                    await Service.PurgeWhere((ITextChannel)ctx.Channel, count, x => !x.IsPinned)
                        .ConfigureAwait(false);
                    return;
                case "-nb":
                case "--nobots":
                    await Service.PurgeWhere((ITextChannel)ctx.Channel, count, x => !x.Author.IsBot)
                        .ConfigureAwait(false);
                    return;
                case "-ob":
                case "--onlybots":
                    await Service.PurgeWhere((ITextChannel)ctx.Channel, count, x => x.Author.IsBot)
                        .ConfigureAwait(false);
                    break;
                case "-b":
                case "--before":
                    if (time is null)
                        return;
                    if (time.Time > TwoWeeks)
                        return;
                    await Service.PurgeWhere((ITextChannel)ctx.Channel, count,
                            x => DateTimeOffset.Now.Subtract(x.Timestamp).TotalSeconds <= time.Time.TotalSeconds)
                        .ConfigureAwait(false);
                    break;
                case "-a":
                case "--after":
                    if (time is null)
                        return;
                    if (time.Time > TwoWeeks)
                        return;
                    await Service.PurgeWhere((ITextChannel)ctx.Channel, count,
                            x => DateTimeOffset.Now.Subtract(x.Timestamp).TotalSeconds >= time.Time.TotalSeconds)
                        .ConfigureAwait(false);
                    break;
                case "-he":
                case "--hasembed":
                    await Service.PurgeWhere((ITextChannel)ctx.Channel, count, x => x.Embeds.Count > 0)
                        .ConfigureAwait(false);
                    break;
                case "-ne":
                case "--noembed":
                    await Service.PurgeWhere((ITextChannel)ctx.Channel, count, x => x.Embeds.Count == 0)
                        .ConfigureAwait(false);
                    break;
                case "-c":
                case "--contains":
                    if (input is null)
                        return;
                    await Service.PurgeWhere((ITextChannel)ctx.Channel, count,
                        x => x.Content.ToLowerInvariant().Contains(input)).ConfigureAwait(false);
                    //     break;
                    // case "-u":
                    // case "--until":
                    //     if (input is null)
                    //         return;
                    //     if (!ulong.TryParse(input, out var messageId))
                    //         return;
                    //     await Service.PurgeWhere((ITextChannel)ctx.Channel, 0, _ => true, messageId);
                    break;
                default:
                    await Service.PurgeWhere((ITextChannel)ctx.Channel, count, _ => true).ConfigureAwait(false);
                    break;
            }
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

        /// <summary>
        /// Purges messages from the current channel with the specific parameters.
        /// </summary>
        /// <param name="parameter">The parameter to use</param>
        /// <param name="input">The extra input for a parameter if needed</param>
        /// <returns></returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(ChannelPermission.ManageMessages), BotPerm(ChannelPermission.ManageMessages), Priority(0)]
        public Task Purge(string? parameter = null, string input = null) => Purge(0, parameter, input);

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
    }
}