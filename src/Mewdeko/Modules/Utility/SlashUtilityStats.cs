using System.IO;
using System.Text;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.Utility.Services;
using SkiaSharp;
using Color = System.Drawing.Color;

namespace Mewdeko.Modules.Utility;

public partial class SlashUtility
{
    /// <summary>
    ///     Server activity statistics such as join and leave graphs and message counts.
    /// </summary>
    [Group("activity", "Join, leave and message count statistics")]
    public class UtilityStats(
        JoinLeaveLoggerService joinLeaveService,
        GuildSettingsService guildSettingsService,
        ILogger<UtilityStats> logger) : MewdekoSlashSubmodule<MessageCountService>
    {
        /// <summary>
        ///     Generates and sends a graph displaying the join statistics of the server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("join", "Shows a graph of server joins")]
        [RequireContext(ContextType.Guild)]
        [InteractionRatelimit(10)]
        [RequireDragon]
        public async Task JoinStats()
        {
            await DeferAsync();
            try
            {
                var (stream, embed) = await joinLeaveService.GenerateJoinGraphAsync(ctx.Guild.Id);
                await ctx.Interaction.FollowupWithFileAsync(stream, "joingraph.png", embed: embed);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error generating join stats:");
            }
        }

        /// <summary>
        ///     Generates and sends a graph displaying the leave statistics of the server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("leave", "Shows a graph of server leaves")]
        [RequireContext(ContextType.Guild)]
        [InteractionRatelimit(10)]
        [RequireDragon]
        public async Task LeaveStats()
        {
            await DeferAsync();
            try
            {
                var (stream, embed) = await joinLeaveService.GenerateLeaveGraphAsync(ctx.Guild.Id);
                await ctx.Interaction.FollowupWithFileAsync(stream, "leavegraph.png", embed: embed);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error generating leave stats:");
            }
        }

        /// <summary>
        ///     Sets the color for the join statistics graph. Leaving all components empty resets the color to gold.
        /// </summary>
        /// <param name="r">Red component of the color.</param>
        /// <param name="g">Green component of the color.</param>
        /// <param name="b">Blue component of the color.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("join-color", "Sets the join graph color. Leave empty to reset to gold")]
        [RequireContext(ContextType.Guild)]
        [RequireDragon]
        public async Task JoinStatsColor(
            [Summary("red", "Red component, 0 to 255")]
            int? r = null,
            [Summary("green", "Green component, 0 to 255")]
            int? g = null,
            [Summary("blue", "Blue component, 0 to 255")]
            int? b = null)
        {
            uint color;
            if (r is null && g is null && b is null)
            {
                color = (uint)Color.FromArgb(255, 215, 0).ToArgb();
            }
            else
            {
                if (r is null or < 0 or > 255 || g is null or < 0 or > 255 || b is null or < 0 or > 255)
                {
                    await ErrorAsync(Strings.ColorInvalid(ctx.Guild.Id));
                    return;
                }

                color = (uint)Color.FromArgb(r.Value, g.Value, b.Value).ToArgb();
            }

            await joinLeaveService.SetJoinColorAsync(color, ctx.Guild.Id);
            await ConfirmAsync(Strings.ColorSet(ctx.Guild.Id));
        }

        /// <summary>
        ///     Sets the color for the leave statistics graph. Leaving all components empty resets the color to gold.
        /// </summary>
        /// <param name="r">Red component of the color, between 0 and 255.</param>
        /// <param name="g">Green component of the color, between 0 and 255.</param>
        /// <param name="b">Blue component of the color, between 0 and 255.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("leave-color", "Sets the leave graph color. Leave empty to reset to gold")]
        [RequireContext(ContextType.Guild)]
        [RequireDragon]
        public async Task LeaveStatsColor(
            [Summary("red", "Red component, 0 to 255")]
            int? r = null,
            [Summary("green", "Green component, 0 to 255")]
            int? g = null,
            [Summary("blue", "Blue component, 0 to 255")]
            int? b = null)
        {
            uint color;
            if (r is null && g is null && b is null)
            {
                color = (uint)Color.FromArgb(255, 215, 0).ToArgb();
            }
            else
            {
                if (r is null or < 0 or > 255 || g is null or < 0 or > 255 || b is null or < 0 or > 255)
                {
                    await ErrorAsync(Strings.ColorInvalid(ctx.Guild.Id));
                    return;
                }

                color = (uint)Color.FromArgb(r.Value, g.Value, b.Value).ToArgb();
            }

            await joinLeaveService.SetLeaveColorAsync(color, ctx.Guild.Id);
            await ConfirmAsync(Strings.ColorSet(ctx.Guild.Id));
        }

        /// <summary>
        ///     Retrieves message statistics for a specific user.
        /// </summary>
        /// <param name="user">The user to get message statistics for. If null, uses the command invoker.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("messages-user", "Shows message count statistics for a user")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task UserMessages(IUser? user = null)
        {
            user ??= ctx.User;
            var (cnt, enabled) = await Service.GetAllCountsForEntity(MessageCountService.CountQueryType.User,
                user.Id, ctx.Guild.Id);

            if (!enabled)
            {
                await ReplyErrorAsync(Strings.MessageCountDisabled(ctx.Guild.Id));
                return;
            }

            var mostActive = cnt.MaxBy(x => x.Count);
            var leastActive = cnt.MinBy(x => x.Count);

            var eb = new EmbedBuilder()
                .WithTitle(Strings.UserMessageCountTitle(ctx.Guild.Id, user))
                .WithDescription(Strings.UserMessageCountDescription(ctx.Guild.Id,
                    cnt.SumUlong(x => x.Count),
                    mostActive.ChannelId, mostActive.Count,
                    leastActive.ChannelId, leastActive.Count))
                .WithOkColor();

            await ctx.Interaction.RespondAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Retrieves message statistics for a specific channel.
        /// </summary>
        /// <param name="channel">The channel to get message statistics for. If null, uses the current channel.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("messages-channel", "Shows message count statistics for a channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task ChannelMessages(IGuildChannel? channel = null)
        {
            channel ??= ctx.Channel as IGuildChannel;
            var (cnt, enabled) = await Service.GetAllCountsForEntity(MessageCountService.CountQueryType.Channel,
                channel.Id,
                ctx.Guild.Id);

            if (!enabled)
            {
                await ReplyErrorAsync(Strings.MessageCountDisabled(ctx.Guild.Id));
                return;
            }

            var mostActive = cnt.MaxBy(x => x.Count);
            var leastActive = cnt.MinBy(x => x.Count);

            var eb = new EmbedBuilder()
                .WithTitle(Strings.ChannelMessageCountTitle(ctx.Guild.Id, channel.Name))
                .WithDescription(Strings.ChannelMessageCountDescription(ctx.Guild.Id,
                    cnt.SumUlong(x => x.Count),
                    mostActive.UserId, mostActive.Count,
                    leastActive.UserId, leastActive.Count))
                .WithOkColor();

            await ctx.Interaction.RespondAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Retrieves message statistics for the entire server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("messages-server", "Shows message count statistics for the server")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task ServerMessages()
        {
            var (cnt, enabled) = await Service.GetAllCountsForEntity(MessageCountService.CountQueryType.Guild,
                ctx.Guild.Id,
                ctx.Guild.Id);

            if (!enabled)
            {
                await ReplyErrorAsync(Strings.MessageCountDisabled(ctx.Guild.Id));
                return;
            }

            var userGroups = cnt.GroupBy(x => x.UserId)
                .Select(g => new
                {
                    UserId = g.Key, Count = g.SumUlong(x => x.Count)
                })
                .ToList();

            var channelGroups = cnt.GroupBy(x => x.ChannelId)
                .Select(g => new
                {
                    ChannelId = g.Key, Count = g.SumUlong(x => x.Count)
                })
                .ToList();

            var mostActiveUser = userGroups.MaxBy(x => x.Count);
            var leastActiveUser = userGroups.MinBy(x => x.Count);
            var mostActiveChannel = channelGroups.MaxBy(x => x.Count);
            var leastActiveChannel = channelGroups.MinBy(x => x.Count);

            var totalMessages = channelGroups.SumUlong(x => x.Count);

            var eb = new EmbedBuilder()
                .WithTitle(Strings.ServerMessageStatsTitle(ctx.Guild.Id, ctx.Guild.Name))
                .WithDescription(Strings.ServerMessageStatsDescription(ctx.Guild.Id,
                    totalMessages,
                    mostActiveUser.UserId, mostActiveUser.Count,
                    leastActiveUser.UserId, leastActiveUser.Count,
                    mostActiveChannel.ChannelId, mostActiveChannel.Count,
                    leastActiveChannel.ChannelId, leastActiveChannel.Count))
                .WithOkColor();

            await ctx.Interaction.RespondAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Displays a leaderboard of the top 10 users by message count.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("top-users", "Shows the top 10 users by message count")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task TopUsers()
        {
            var (cnt, enabled) = await Service.GetAllCountsForEntity(MessageCountService.CountQueryType.Guild,
                ctx.Guild.Id,
                ctx.Guild.Id);

            if (!enabled)
            {
                await ReplyErrorAsync(Strings.MessageCountDisabled(ctx.Guild.Id));
                return;
            }

            var userGroups = cnt.GroupBy(x => x.UserId)
                .Select(g => new
                {
                    UserId = g.Key, Count = g.SumUlong(x => x.Count)
                })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList();

            var totalMessages = cnt.SumUlong(x => x.Count);

            var eb = new EmbedBuilder()
                .WithTitle(Strings.TopUsersTitle(ctx.Guild.Id, ctx.Guild.Name))
                .WithOkColor();

            var description = new StringBuilder();
            for (var i = 0; i < userGroups.Count; i++)
            {
                var user = userGroups[i];
                var userMention = MentionUtils.MentionUser(user.UserId);
                var percentage = (user.Count * 100.0 / totalMessages).ToString("F2");
                description.AppendLine(Strings.TopUsersEntry(ctx.Guild.Id, i + 1, userMention, user.Count,
                    percentage));
            }

            eb.WithDescription(description.ToString());

            await ctx.Interaction.RespondAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Sets the minimum length for a message to count. Leaving the length at 0 shows the current setting.
        /// </summary>
        /// <param name="minLength">The minimum message length.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("min-length", "Sets or shows the minimum message length that counts")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task MinMessageCountLength(
            [Summary("length", "The minimum length. Leave at 0 to show the current setting")]
            int minLength = 0)
        {
            var config = await guildSettingsService.GetGuildConfig(ctx.Guild.Id);

            switch (minLength)
            {
                case > 4098:
                    await ReplyErrorAsync(Strings.MaxCountReached(ctx.Guild.Id));
                    return;
                case 0:
                    await ReplyConfirmAsync(Strings.CurrentMinMessageSetting(ctx.Guild.Id, config.MinMessageLength));
                    break;
                default:
                    config.MinMessageLength = minLength;
                    await guildSettingsService.UpdateGuildConfig(ctx.Guild.Id, config);
                    await ReplyConfirmAsync(Strings.MinMessageLengthSet(ctx.Guild.Id, minLength));
                    break;
            }
        }

        /// <summary>
        ///     Displays a graph of the busiest hours or days in the server.
        /// </summary>
        /// <param name="graphType">Whether to graph days or hours.</param>
        /// <param name="timezone">The timezone used for the hours graph. Defaults to UTC.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("activity-graph", "Shows a graph of the busiest days or hours")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task ActivityGraph(
            [Summary("type", "Graph the busiest days or hours")]
            Utility.MessageCountCommands.GraphType graphType = Utility.MessageCountCommands.GraphType.Days,
            [Summary("timezone", "Timezone for the hours graph. Defaults to UTC")]
            [Autocomplete(typeof(TimeZoneAutocompleter))]
            string? timezone = null)
        {
            await DeferAsync();
            switch (graphType)
            {
                case Utility.MessageCountCommands.GraphType.Days:
                    await GenerateBusiestDaysGraph();
                    break;
                case Utility.MessageCountCommands.GraphType.Hours:
                    await GenerateBusiestHoursGraph(timezone);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(graphType), graphType, null);
            }
        }

        /// <summary>
        ///     Toggles message counting in the server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("toggle-counting", "Enables or disables message counting")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task ToggleMessageCount()
        {
            var toggled = await Service.ToggleGuildMessageCount(ctx.Guild.Id);

            if (toggled)
                await ReplyConfirmAsync(Strings.MessageCountingEnabled(ctx.Guild.Id));
            else
                await ReplyConfirmAsync(Strings.MessageCountingDisabled(ctx.Guild.Id));
        }

        /// <summary>
        ///     Resets message counts for a user, channel, or both, with confirmation.
        /// </summary>
        /// <param name="user">Optional: The user to reset message counts for.</param>
        /// <param name="channel">Optional: The channel to reset message counts in.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("reset-counts", "Resets message counts for the server, a user or a channel")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageMessages)]
        public async Task ResetMessageCounts(IUser? user = null, ITextChannel? channel = null)
        {
            var confirmMessage = (user, channel) switch
            {
                (null, null) => Strings.ConfirmResetMessageCountGuild(ctx.Guild.Id),
                (not null, null) => Strings.ConfirmResetMessageCountUser(ctx.Guild.Id, user.Mention),
                (null, not null) => Strings.ConfirmResetMessageCountChannel(ctx.Guild.Id, channel.Mention),
                (not null, not null) => Strings.ConfirmResetMessageCountUserChannel(ctx.Guild.Id, user.Mention,
                    channel.Mention)
            };

            if (!await PromptUserConfirmAsync(confirmMessage, ctx.User.Id))
                return;

            var result = await Service.ResetCount(ctx.Guild.Id, user?.Id ?? 0, channel?.Id ?? 0);

            var responseMessage = (user, channel, result) switch
            {
                (null, null, true) => Strings.ResetMessageCountSuccessGuild(ctx.Guild.Id),
                (not null, null, true) => Strings.ResetMessageCountSuccessUser(ctx.Guild.Id, user.Mention),
                (null, not null, true) => Strings.ResetMessageCountSuccessChannel(ctx.Guild.Id, channel.Mention),
                (not null, not null, true) => Strings.ResetMessageCountSuccessUserChannel(ctx.Guild.Id,
                    user.Mention, channel.Mention),
                (null, null, false) => Strings.ResetMessageCountFailGuild(ctx.Guild.Id),
                (not null, null, false) => Strings.ResetMessageCountFailUser(ctx.Guild.Id, user.Mention),
                (null, not null, false) => Strings.ResetMessageCountFailChannel(ctx.Guild.Id, channel.Mention),
                (not null, not null, false) => Strings.ResetMessageCountFailUserChannel(ctx.Guild.Id,
                    user.Mention, channel.Mention)
            };

            await (result
                ? ConfirmAsync(responseMessage)
                : ErrorAsync(responseMessage));
        }

        private async Task GenerateBusiestDaysGraph()
        {
            var busiestDays = await Service.GetBusiestDays(ctx.Guild.Id);

            if (busiestDays == null || busiestDays.Count() < 7)
            {
                await ReplyErrorAsync(Strings.InsufficientDayData(ctx.Guild.Id, busiestDays?.Count() ?? 0));
                return;
            }

            using var graphImage = GenerateDaysGraph(busiestDays);
            using var ms = new MemoryStream();
            graphImage.Encode(SKEncodedImageFormat.Png, 100).SaveTo(ms);
            ms.Position = 0;

            await ctx.Interaction.FollowupWithFileAsync(ms, "busiest_days.png",
                Strings.BusiestDaysGraphTitle(ctx.Guild.Id, ctx.Guild.Name));
        }

        private async Task GenerateBusiestHoursGraph(string? timezone)
        {
            var busiestHours = await Service.GetBusiestHours(ctx.Guild.Id);

            if (busiestHours == null || busiestHours.Count() < 24)
            {
                await ReplyErrorAsync(Strings.InsufficientHourData(ctx.Guild.Id, busiestHours?.Count() ?? 0));
                return;
            }

            TimeZoneInfo? userTimezone;
            try
            {
                userTimezone = string.IsNullOrWhiteSpace(timezone)
                    ? TimeZoneInfo.Utc
                    : TimeZoneInfo.FindSystemTimeZoneById(timezone);
            }
            catch
            {
                userTimezone = null;
            }

            if (userTimezone == null)
            {
                await ReplyErrorAsync(Strings.TimezoneNotSelected(ctx.Guild.Id));
                return;
            }

            var adjustedHours = AdjustHoursToTimezone(busiestHours, userTimezone);
            using var graphImage = GenerateHoursGraph(adjustedHours);
            using var ms = new MemoryStream();
            graphImage.Encode(SKEncodedImageFormat.Png, 100).SaveTo(ms);
            ms.Position = 0;

            await ctx.Interaction.FollowupWithFileAsync(ms, "busiest_hours.png",
                Strings.BusiestHoursGraphTitle(ctx.Guild.Id, ctx.Guild.Name, userTimezone.Id));
        }

        private static IEnumerable<(int Hour, int Count)> AdjustHoursToTimezone(
            IEnumerable<(int Hour, int Count)> utcHours, TimeZoneInfo timezone)
        {
            return utcHours.Select(h =>
            {
                var utcTime = DateTime.UtcNow.Date.AddHours(h.Hour);
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, timezone);
                return (localTime.Hour, h.Count);
            }).OrderBy(h => h.Hour);
        }

        private static SKImage GenerateDaysGraph(IEnumerable<(DayOfWeek Day, int Count)> busiestDays)
        {
            const int width = 800;
            const int height = 600;
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;

            canvas.Clear(new SKColor(30, 30, 30));

            using var paint = new SKPaint
            {
                Color = SKColors.White, IsAntialias = true
            };
            using var font = new SKFont(SKTypeface.Default);

            var data = busiestDays.Select(d => (d.Day.ToString()[..3], d.Count)).ToList();
            DrawBarGraph(canvas, data, "Busiest Days of the Week", 0, 0, width, height, paint, font);

            return surface.Snapshot();
        }

        private static SKImage GenerateHoursGraph(IEnumerable<(int Hour, int Count)> busiestHours)
        {
            const int width = 800;
            const int height = 600;
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;

            canvas.Clear(new SKColor(30, 30, 30));

            using var paint = new SKPaint
            {
                Color = SKColors.White, IsAntialias = true
            };
            using var font = new SKFont(SKTypeface.Default);

            var data = busiestHours.Select(h => (h.Hour.ToString("D2") + ":00", h.Count)).ToList();
            DrawBarGraph(canvas, data, "Busiest Hours of the Day", 0, 0, width, height, paint, font);

            return surface.Snapshot();
        }

        private static void DrawBarGraph(SKCanvas canvas, List<(string Label, int Value)> data, string title,
            float x, float y, float width, float height, SKPaint paint, SKFont font)
        {
            using (var titleFont = new SKFont(font.Typeface, font.Size * 1.5f))
            {
                canvas.DrawText(title, x + width / 2, y + 40, SKTextAlign.Center, titleFont, paint);
            }

            var barWidth = (width - 100) / data.Count;
            float spacing = 5;
            float maxValue = data.Max(d => d.Value);

            for (var i = 0; i < data.Count; i++)
            {
                var barHeight = data[i].Value / maxValue * (height - 120);
                var barX = x + 50 + i * (barWidth + spacing);
                var barY = y + height - 60 - barHeight;

                using (var barPaint = new SKPaint
                       {
                           Color = new SKColor(66, 135, 245)
                       })
                {
                    canvas.DrawRect(barX, barY, barWidth, barHeight, barPaint);
                }

                canvas.DrawText(data[i].Value.ToString(), barX + barWidth / 2, barY - 5, SKTextAlign.Center, font,
                    paint);

                canvas.DrawText(data[i].Label, barX + barWidth / 2, y + height - 25, SKTextAlign.Center, font, paint);
            }

            canvas.DrawLine(x + 45, y + 60, x + 45, y + height - 40, paint);
            canvas.DrawLine(x + 45, y + height - 40, x + width - 5, y + height - 40, paint);
        }
    }
}