using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;
using Swan;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    /// Commands for message counts
    /// </summary>
    public class MessageCountCommands(GuildSettingsService guildSettingsService) : MewdekoSubmodule<MessageCountService>
    {
        /// <summary>
        /// Retrieves message statistics for a specific user.
        /// </summary>
        /// <param name="user">The user to get message statistics for. If null, uses the command invoker.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task UserMessages(IUser? user = null)
        {
            user ??= ctx.User;
            var (cnt, enabled) = await Service.GetAllCountsForEntity(MessageCountService.CountQueryType.User, user.Id,
                ctx.Guild.Id);

            if (!enabled)
            {
                await ReplyErrorLocalizedAsync("message_count_disabled");
                return;
            }

            var mostActive = cnt.MaxBy(x => x.Count);
            var leastActive = cnt.MinBy(x => x.Count);

            var eb = new EmbedBuilder()
                .WithTitle(GetText("user_message_count_title", user))
                .WithDescription(GetText("user_message_count_description",
                    cnt.SumUlong(x => x.Count),
                    mostActive.ChannelId, mostActive.Count,
                    leastActive.ChannelId, leastActive.Count))
                .WithOkColor();

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }

        /// <summary>
        /// Retrieves message statistics for a specific channel.
        /// </summary>
        /// <param name="channel">The channel to get message statistics for. If null, uses the current channel.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task ChannelMessages(IGuildChannel? channel = null)
        {
            channel ??= ctx.Channel as IGuildChannel;
            var (cnt, enabled) = await Service.GetAllCountsForEntity(MessageCountService.CountQueryType.Channel, channel.Id,
                ctx.Guild.Id);

            if (!enabled)
            {
                await ReplyErrorLocalizedAsync("message_count_disabled");
                return;
            }

            var mostActive = cnt.MaxBy(x => x.Count);
            var leastActive = cnt.MinBy(x => x.Count);

            var eb = new EmbedBuilder()
                .WithTitle(GetText("channel_message_count_title", channel.Name))
                .WithDescription(GetText("channel_message_count_description",
                    cnt.SumUlong(x => x.Count),
                    mostActive.UserId, mostActive.Count,
                    leastActive.UserId, leastActive.Count))
                .WithOkColor();

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }

        /// <summary>
        /// Retrieves message statistics for the entire server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task ServerMessages()
        {
            var (cnt, enabled) = await Service.GetAllCountsForEntity(MessageCountService.CountQueryType.Guild, ctx.Guild.Id,
                ctx.Guild.Id);

            if (!enabled)
            {
                await ReplyErrorLocalizedAsync("message_count_disabled");
                return;
            }

            var userGroups = cnt.GroupBy(x => x.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Count = g.SumUlong(x => x.Count)
                })
                .ToList();

            var channelGroups = cnt.GroupBy(x => x.ChannelId)
                .Select(g => new
                {
                    ChannelId = g.Key,
                    Count = g.SumUlong(x => x.Count)
                })
                .ToList();

            var mostActiveUser = userGroups.MaxBy(x => x.Count);
            var leastActiveUser = userGroups.MinBy(x => x.Count);
            var mostActiveChannel = channelGroups.MaxBy(x => x.Count);
            var leastActiveChannel = channelGroups.MinBy(x => x.Count);

            var totalMessages = channelGroups.SumUlong(x => x.Count);

            var eb = new EmbedBuilder()
                .WithTitle(GetText("server_message_stats_title", ctx.Guild.Name))
                .WithDescription(GetText("server_message_stats_description",
                    totalMessages,
                    mostActiveUser.UserId, mostActiveUser.Count,
                    leastActiveUser.UserId, leastActiveUser.Count,
                    mostActiveChannel.ChannelId, mostActiveChannel.Count,
                    leastActiveChannel.ChannelId, leastActiveChannel.Count))
                .WithOkColor();

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }

        /// <summary>
        /// Sets the minimum length for a message to count
        /// </summary>
        /// <param name="minLength"></param>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageGuild)]
        public async Task MinMessageCountLength(int minLength = 0)
        {
            var config = await guildSettingsService.GetGuildConfig(ctx.Guild.Id);

            switch (minLength)
            {
                case > 4098:
                    await ReplyErrorLocalizedAsync("max_count_reached");
                    return;
                case 0:
                    await ReplyConfirmLocalizedAsync("current_min_message_setting", config.MinMessageLength);
                    break;
                default:
                    config.MinMessageLength = minLength;
                    await guildSettingsService.UpdateGuildConfig(ctx.Guild.Id, config);
                    await ReplyConfirmLocalizedAsync("min_message_length_set", minLength);
                    break;
            }
        }

        /// <summary>
        /// Toggles message counting in the server
        /// </summary>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
        public async Task ToggleMessageCount()
        {
            var toggled = await Service.ToggleGuildMessageCount(ctx.Guild.Id);

            if (toggled)
                await ReplyConfirmLocalizedAsync("message_counting_enabled");
            else
                await ReplyConfirmLocalizedAsync("message_counting_disabled");
        }
    }
}