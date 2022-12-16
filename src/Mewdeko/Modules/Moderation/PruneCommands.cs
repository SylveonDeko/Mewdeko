using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Moderation.Services;

namespace Mewdeko.Modules.Moderation;

public partial class Moderation
{
    [Group]
    public class PurgeCommands : MewdekoSubmodule<PurgeService>
    {
        private static readonly TimeSpan TwoWeeks = TimeSpan.FromDays(14);

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
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(ChannelPermission.ManageMessages), BotPerm(ChannelPermission.ManageMessages), Priority(1)]
        public async Task Purge(ulong count, string? parameter = null, string? input = null)
        {
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
                        x => DateTimeOffset.Now.Subtract(x.Timestamp).TotalSeconds <= time.Time.TotalSeconds).ConfigureAwait(false);
                    break;
                case "-a":
                case "--after":
                    if (time is null)
                        return;
                    if (time.Time > TwoWeeks)
                        return;
                    await Service.PurgeWhere((ITextChannel)ctx.Channel, count,
                        x => DateTimeOffset.Now.Subtract(x.Timestamp).TotalSeconds >= time.Time.TotalSeconds).ConfigureAwait(false);
                    break;
                case "-he":
                case "--hasembed":
                    await Service.PurgeWhere((ITextChannel)ctx.Channel, count, x => x.Embeds.Count > 0).ConfigureAwait(false);
                    break;
                case "-ne":
                case "--noembed":
                    await Service.PurgeWhere((ITextChannel)ctx.Channel, count, x => x.Embeds.Count == 0).ConfigureAwait(false);
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
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(ChannelPermission.ManageMessages), BotPerm(ChannelPermission.ManageMessages), Priority(0)]
        public Task Purge(IGuildUser user, ulong count = 100, string? parameter = null) => Purge(user.Id, count, parameter);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(ChannelPermission.ManageMessages), BotPerm(ChannelPermission.ManageMessages), Priority(0)]
        public Task Purge(string? parameter = null, string input = null) => Purge(0, parameter, input);

        //Purge userid [x]
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(ChannelPermission.ManageMessages), BotPerm(ChannelPermission.ManageMessages), Priority(0)]
        public async Task Purge(ulong userId, ulong count = 100, string? parameter = null)
        {
            if (userId == ctx.User.Id)
                count++;

            switch (count)
            {
                case < 1:
                    return;
                case > 1000:
                    count = 1000;
                    break;
            }

            if (parameter is "-s" or "--safe")
            {
                await Service.PurgeWhere((ITextChannel)ctx.Channel, count,
                        m => m.Author.Id == userId && DateTime.UtcNow - m.CreatedAt < TwoWeeks && !m.IsPinned)
                    .ConfigureAwait(false);
            }
            else
            {
                await Service.PurgeWhere((ITextChannel)ctx.Channel, count,
                    m => m.Author.Id == userId && DateTime.UtcNow - m.CreatedAt < TwoWeeks).ConfigureAwait(false);
            }
        }
    }
}