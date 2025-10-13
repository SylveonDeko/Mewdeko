using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Provides commands for managing message repeaters within the guild.
    ///     Allows for creating, modifying, and removing automated repeating messages.
    /// </summary>
    [Group]
    public class RepeatCommands(
        InteractiveService interactivity,
        ILogger<RepeatCommands> logger,
        GuildSettingsService gss,
        MessageCountService? messageCountService = null,
        GuildTimezoneService? guildTimezoneService = null,
        StickyConditionService? conditionService = null)
        : MewdekoSubmodule<MessageRepeaterService>
    {
        /// <summary>
        ///     Immediately triggers a repeater by its index number.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to trigger.</param>
        /// <remarks>
        ///     The repeater will execute immediately and then continue on its normal schedule.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatInvoke(int index)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.RepeatInvokeNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            repeater.Reset();
            await repeater.Trigger().ConfigureAwait(false);

            try
            {
                await ctx.Message.AddReactionAsync(new Emoji("🔄")).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        ///     Removes a repeater by its index number.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to remove.</param>
        /// <remarks>
        ///     This action is permanent and cannot be undone.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatRemove(int index)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var description = GetRepeaterInfoString(repeater);
            await Service.RemoveRepeater(repeater.Repeater);

            await ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepeaterRemoved(ctx.Guild.Id, index))
                .WithDescription(description)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles the redundancy check for a repeater.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to modify.</param>
        /// <remarks>
        ///     When redundancy is enabled, the repeater will not send a message if it's message is the last one in the channel.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatRedundant(int index)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var success = await Service.ToggleRepeaterRedundancyAsync(ctx.Guild.Id, repeater.Repeater.Id);
            if (!success)
            {
                await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (repeater.Repeater.NoRedundant)
                await ReplyConfirmAsync(Strings.RepeaterRedundantNo(ctx.Guild.Id, index)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.RepeaterRedundantYes(ctx.Guild.Id, index)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Creates a repeater with the specified message.
        /// </summary>
        /// <param name="message">The message to repeat.</param>
        /// <remarks>
        ///     Uses default interval of 5 minutes if not specified.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [Priority(-1)]
        public Task Repeat([Remainder] string? message)
        {
            return Repeat(null, null, message);
        }

        /// <summary>
        ///     Creates a repeater with specified interval and message.
        /// </summary>
        /// <param name="interval">The time interval between repeats.</param>
        /// <param name="message">The message to repeat.</param>
        /// <remarks>
        ///     Interval must be between 5 seconds and 25000 minutes.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [Priority(0)]
        public Task Repeat(StoopidTime interval, [Remainder] string? message)
        {
            return Repeat(null, interval, message);
        }

        /// <summary>
        ///     Creates a repeater that runs at a specific time each day.
        /// </summary>
        /// <param name="dt">The time of day to run the repeater.</param>
        /// <param name="message">The message to repeat.</param>
        /// <remarks>
        ///     The repeater will run once per day at the specified time.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [Priority(1)]
        public Task Repeat(GuildDateTime dt, [Remainder] string? message)
        {
            return Repeat(dt, null, message);
        }

        /// <summary>
        ///     Creates a repeater with optional start time and interval.
        /// </summary>
        /// <param name="dt">Optional time of day to start the repeater.</param>
        /// <param name="interval">Optional interval between repeats.</param>
        /// <param name="message">The message to repeat.</param>
        /// <remarks>
        ///     Most flexible repeat command allowing both time and interval specification.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [Priority(2)]
        public async Task Repeat(GuildDateTime? dt, StoopidTime? interval, [Remainder] string? message)
        {
            try
            {
                if (!Service.RepeaterReady)
                    return;

                if (string.IsNullOrWhiteSpace(message))
                {
                    await ReplyErrorAsync(Strings.MessageEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                var startTimeOfDay = dt?.InputTimeUtc.TimeOfDay;
                var realInterval = interval?.Time ?? (startTimeOfDay is null
                    ? TimeSpan.FromMinutes(5)
                    : TimeSpan.FromDays(1));

                if (interval != null)
                {
                    if (interval.Time > TimeSpan.FromMinutes(25000))
                    {
                        await ReplyErrorAsync(Strings.IntervalTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    if (interval.Time < TimeSpan.FromSeconds(5))
                    {
                        await ReplyErrorAsync(Strings.IntervalTooShort(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }
                }

                var runner = await Service.CreateRepeaterAsync(
                    ctx.Guild.Id,
                    ctx.Channel.Id,
                    realInterval,
                    message,
                    startTimeOfDay?.ToString(),
                    ((IGuildUser)ctx.User).GuildPermissions.MentionEveryone);

                if (runner == null)
                {
                    await ReplyErrorAsync(Strings.RepeatCreationFailed(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                var description = GetRepeaterInfoString(runner);
                await ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle(Strings.RepeaterCreated(ctx.Guild.Id))
                    .WithDescription(description)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating repeater");
                await ReplyErrorAsync(Strings.ErrorCreatingRepeater(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Lists all active repeaters in the guild.
        /// </summary>
        /// <remarks>
        ///     Shows index, channel, interval, and next execution time for each repeater.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatList()
        {
            if (!Service.RepeaterReady)
                return;

            var repeaters = Service.GetGuildRepeaters(ctx.Guild.Id);

            if (!repeaters.Any())
            {
                await ReplyErrorAsync(Strings.NoActiveRepeaters(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(repeaters.Count / 5)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);

                var pageBuilder = new PageBuilder()
                    .WithOkColor()
                    .WithTitle(Strings.ListOfRepeaters(ctx.Guild.Id));

                var pageRepeaters = repeaters.Skip(page * 5).Take(5);
                var i = page * 5;

                foreach (var repeater in pageRepeaters)
                {
                    var description = GetRepeaterInfoString(repeater);
                    pageBuilder.AddField(
                        $"#{Format.Code((i + 1).ToString())}",
                        description
                    );
                    i++;
                }

                return pageBuilder;
            }
        }

        /// <summary>
        ///     Updates the message of an existing repeater.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to update.</param>
        /// <param name="message">The new message for the repeater.</param>
        /// <remarks>
        ///     Only changes the message content, keeping all other settings the same.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatMessage(int index, [Remainder] string? message)
        {
            if (!Service.RepeaterReady)
                return;

            if (string.IsNullOrWhiteSpace(message))
            {
                await ReplyErrorAsync(Strings.MessageEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var success = await Service.UpdateRepeaterMessageAsync(
                ctx.Guild.Id,
                repeater.Repeater.Id,
                message,
                ((IGuildUser)ctx.User).GuildPermissions.MentionEveryone);

            if (!success)
            {
                await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.RepeaterMsgUpdate(ctx.Guild.Id, message)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Changes the channel where a repeater sends its messages.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to modify.</param>
        /// <param name="channel">The new channel for the repeater. Defaults to current channel if not specified.</param>
        /// <remarks>
        ///     The bot must have permission to send messages in the target channel.
        ///     Requires the Manage Messages permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatChannel(int index, [Remainder] IGuildChannel? channel = null)
        {
            if (!Service.RepeaterReady)
                return;

            channel ??= ctx.Channel as IGuildChannel;
            if (channel == null)
                return;

            // Validate channel type - must be text channel or forum channel
            if (channel is not (ITextChannel or IForumChannel))
            {
                await ReplyErrorAsync(Strings.StickyInvalidChannelType(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var success = await Service.UpdateRepeaterChannelAsync(
                ctx.Guild.Id,
                repeater.Repeater.Id,
                channel.Id);

            if (!success)
            {
                await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.RepeaterChannelUpdate(ctx.Guild.Id, $"<#{channel.Id}>"))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the trigger mode for a repeater.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to modify.</param>
        /// <param name="mode">The trigger mode (timeinterval, onactivity, onnoactivity, immediate, aftermessages).</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatTriggerMode(int index, string mode)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (!Enum.TryParse<StickyTriggerMode>(mode, true, out var triggerMode))
            {
                await ReplyErrorAsync(Strings.StickyInvalidTriggerMode(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            // Check if activity-based modes require message counting
            if (triggerMode is StickyTriggerMode.OnActivity or StickyTriggerMode.OnNoActivity
                    or StickyTriggerMode.AfterMessages && messageCountService != null)
            {
                var (_, enabled) = await messageCountService.GetAllCountsForEntity(
                    MessageCountService.CountQueryType.Guild, ctx.Guild.Id, ctx.Guild.Id);

                if (!enabled)
                {
                    await ReplyErrorAsync(Strings.StickyMessageCountRequired(ctx.Guild.Id,
                        await gss.GetPrefix(ctx.Guild))).ConfigureAwait(false);
                    return;
                }
            }

            var success = await Service.UpdateRepeaterTriggerModeAsync(ctx.Guild.Id, repeater.Repeater.Id, triggerMode);
            if (!success)
            {
                await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.StickyTriggerModeSet(ctx.Guild.Id, index, triggerMode.ToString()))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the activity threshold for a repeater.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to modify.</param>
        /// <param name="threshold">Number of messages needed to trigger.</param>
        /// <param name="timeWindow">Time window for activity detection (e.g., 5m, 10s).</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatActivity(int index, int threshold, StoopidTime timeWindow)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (threshold < 1)
            {
                await ReplyErrorAsync(Strings.StickyActivityThresholdTooLow(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (timeWindow.Time < TimeSpan.FromSeconds(30) || timeWindow.Time > TimeSpan.FromHours(6))
            {
                await ReplyErrorAsync(Strings.StickyActivityTimeWindowInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            // Check if message counting is enabled for activity features
            if (messageCountService != null)
            {
                var (_, enabled) = await messageCountService.GetAllCountsForEntity(
                    MessageCountService.CountQueryType.Guild, ctx.Guild.Id, ctx.Guild.Id);

                if (!enabled)
                {
                    await ReplyErrorAsync(Strings.StickyMessageCountRequired(ctx.Guild.Id,
                        await gss.GetPrefix(ctx.Guild))).ConfigureAwait(false);
                    return;
                }
            }

            var success = await Service.UpdateRepeaterActivityThresholdAsync(ctx.Guild.Id, repeater.Repeater.Id,
                threshold, timeWindow.Time);
            if (!success)
            {
                await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.StickyActivityThresholdSet(ctx.Guild.Id, index, threshold,
                    timeWindow.Time.ToPrettyStringHm()))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the priority for a repeater (0-100, higher = more important).
        /// </summary>
        /// <param name="index">The one-based index of the repeater to modify.</param>
        /// <param name="priority">Priority level (0-100).</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatPriority(int index, int priority)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (priority is < 0 or > 100)
            {
                await ReplyErrorAsync(Strings.StickyPriorityInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var success = await Service.UpdateRepeaterPriorityAsync(ctx.Guild.Id, repeater.Repeater.Id, priority);
            if (!success)
            {
                await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.StickyPrioritySet(ctx.Guild.Id, index, priority))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets time-based scheduling for a repeater.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to modify.</param>
        /// <param name="preset">Preset time condition (business, evening, weekend) or 'custom' for manual setup.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatSchedule(int index, string preset)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            string? timeConditionsJson = null;
            var presetLower = preset.ToLowerInvariant();

            if (conditionService != null)
            {
                timeConditionsJson = presetLower switch
                {
                    "business" => conditionService.CreateBusinessHoursCondition(),
                    "evening" => conditionService.CreateEveningHoursCondition(),
                    "weekend" => conditionService.CreateWeekendCondition(),
                    _ => null
                };

                if (timeConditionsJson == null && presetLower != "none" && presetLower != "disable")
                {
                    await ReplyErrorAsync(Strings.StickyInvalidTimePreset(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }
            }

            var success =
                await Service.UpdateRepeaterTimeConditionsAsync(ctx.Guild.Id, repeater.Repeater.Id, timeConditionsJson);
            if (!success)
            {
                await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (timeConditionsJson == null)
            {
                await ReplyConfirmAsync(Strings.StickyTimeScheduleDisabled(ctx.Guild.Id, index))
                    .ConfigureAwait(false);
            }
            else
            {
                var timezone = guildTimezoneService?.GetTimeZoneOrUtc(ctx.Guild.Id)?.Id ?? "UTC";
                await ReplyConfirmAsync(Strings.StickyTimeScheduleSet(ctx.Guild.Id, index, preset, timezone))
                    .ConfigureAwait(false);

                // Remind about timezone if not set
                if (timezone == "UTC" && guildTimezoneService != null)
                {
                    await ReplyAsync(Strings.StickyTimezoneReminder(ctx.Guild.Id, await gss.GetPrefix(ctx.Guild)))
                        .ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        ///     Toggles conversation detection for a repeater.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to modify.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatConversation(int index)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var success = await Service.ToggleRepeaterConversationDetectionAsync(ctx.Guild.Id, repeater.Repeater.Id);
            if (!success)
            {
                await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (repeater.Repeater.ConversationDetection)
                await ReplyConfirmAsync(Strings.StickyConversationDetectionEnabled(ctx.Guild.Id, index))
                    .ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.StickyConversationDetectionDisabled(ctx.Guild.Id, index))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles the enabled state of a repeater.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to toggle.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatToggle(int index)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var success = await Service.ToggleRepeaterEnabledAsync(ctx.Guild.Id, repeater.Repeater.Id);
            if (!success)
            {
                await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (repeater.Repeater.IsEnabled)
                await ReplyConfirmAsync(Strings.StickyEnabled(ctx.Guild.Id, index)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.StickyDisabled(ctx.Guild.Id, index)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles thread auto-sticky feature for a repeater.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to modify.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatThreadAutoSticky(int index)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var success = await Service.ToggleRepeaterThreadAutoStickyAsync(ctx.Guild.Id, repeater.Repeater.Id);
            if (!success)
            {
                await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (repeater.Repeater.ThreadAutoSticky)
                await ReplyConfirmAsync(Strings.StickyThreadAutoStickyEnabled(ctx.Guild.Id, index))
                    .ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.StickyThreadAutoStickyDisabled(ctx.Guild.Id, index))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles thread-only mode for a repeater.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to modify.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatThreadOnly(int index)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var success = await Service.ToggleRepeaterThreadOnlyModeAsync(ctx.Guild.Id, repeater.Repeater.Id);
            if (!success)
            {
                await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (repeater.Repeater.ThreadOnlyMode)
                await ReplyConfirmAsync(Strings.StickyThreadOnlyModeEnabled(ctx.Guild.Id, index)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.StickyThreadOnlyModeDisabled(ctx.Guild.Id, index))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets forum tag conditions for a repeater.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to modify.</param>
        /// <param name="action">Action to perform: add, remove, clear, or list.</param>
        /// <param name="tagType">Type of tag rule: required or excluded.</param>
        /// <param name="tags">Comma-separated list of tag names or IDs.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task RepeatForumTags(int index, string action, string? tagType = null,
            [Remainder] string? tags = null)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var actionLower = action.ToLowerInvariant();

            switch (actionLower)
            {
                case "clear":
                    var clearResult =
                        await Service.UpdateRepeaterForumTagConditionsAsync(ctx.Guild.Id, repeater.Repeater.Id, null);
                    if (clearResult)
                        await ReplyConfirmAsync(Strings.StickyForumTagsCleared(ctx.Guild.Id, index))
                            .ConfigureAwait(false);
                    else
                        await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
                    break;

                case "list":
                    var currentConditions =
                        conditionService?.ParseForumTagConditions(repeater.Repeater.ForumTagConditions);
                    if (currentConditions == null || !currentConditions.RequiredTags?.Any() == true &&
                        !currentConditions.ExcludedTags?.Any() == true)
                    {
                        await ReplyAsync(Strings.StickyForumTagsNone(ctx.Guild.Id, index)).ConfigureAwait(false);
                    }
                    else
                    {
                        var description = "";
                        if (currentConditions.RequiredTags?.Any() == true)
                            description +=
                                $"**Required Tags:** {string.Join(", ", currentConditions.RequiredTags.Select(t => $"<#{t}>"))}\n";
                        if (currentConditions.ExcludedTags?.Any() == true)
                            description +=
                                $"**Excluded Tags:** {string.Join(", ", currentConditions.ExcludedTags.Select(t => $"<#{t}>"))}\n";

                        await ReplyAsync(Strings.StickyForumTagsList(ctx.Guild.Id, index, description))
                            .ConfigureAwait(false);
                    }

                    break;

                case "add":
                case "remove":
                    if (string.IsNullOrWhiteSpace(tagType) || string.IsNullOrWhiteSpace(tags))
                    {
                        await ReplyErrorAsync(Strings.StickyForumTagsInvalidFormat(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    var typeLower = tagType.ToLowerInvariant();
                    if (typeLower != "required" && typeLower != "excluded")
                    {
                        await ReplyErrorAsync(Strings.StickyForumTagsInvalidType(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    // Parse tag IDs from input
                    var tagIds = new List<ulong>();
                    foreach (var tag in tags.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = tag.Trim();
                        if (ulong.TryParse(trimmed, out var tagId))
                        {
                            tagIds.Add(tagId);
                        }
                        else if (trimmed.StartsWith("<#") && trimmed.EndsWith(">"))
                        {
                            var idStr = trimmed.Substring(2, trimmed.Length - 3);
                            if (ulong.TryParse(idStr, out var channelTagId))
                                tagIds.Add(channelTagId);
                        }
                    }

                    if (!tagIds.Any())
                    {
                        await ReplyErrorAsync(Strings.StickyForumTagsNoValidTags(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    var newConditionsJson = await Service.UpdateRepeaterForumTagConditionsAsync(
                        ctx.Guild.Id, repeater.Repeater.Id, actionLower, typeLower, tagIds);

                    if (newConditionsJson != null)
                    {
                        var verb = actionLower == "add"
                            ? Strings.StickyForumTagsAdded(ctx.Guild.Id)
                            : Strings.StickyForumTagsRemoved(ctx.Guild.Id);
                        await ReplyConfirmAsync($"{verb} {tagIds.Count} {typeLower} tag(s) for sticky #{index}")
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
                    }

                    break;

                default:
                    await ReplyErrorAsync(Strings.StickyForumTagsInvalidAction(ctx.Guild.Id)).ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        ///     Formats repeater information into a human-readable string.
        /// </summary>
        /// <param name="runner">The repeater runner to get information from.</param>
        /// <returns>A formatted string containing the repeater's details.</returns>
        private string GetRepeaterInfoString(RepeatRunner runner)
        {
            var intervalString = Format.Bold(TimeSpan.Parse(runner.Repeater.Interval).ToPrettyStringHm());
            var executesIn = runner.NextDateTime - DateTime.UtcNow;
            var executesInString = Format.Bold(executesIn.ToPrettyStringHm());
            var message = Format.Sanitize(runner.Repeater.Message.TrimTo(50));
            var triggerMode = (StickyTriggerMode)runner.Repeater.TriggerMode;

            var description = "";

            // Status indicators
            if (!runner.Repeater.IsEnabled)
                description += $"⚠️ {Format.Bold(Strings.StickyDisabledStatus(ctx.Guild.Id))}\n\n";

            if (runner.Repeater.NoRedundant)
                description += $"{Format.Underline(Format.Bold(Strings.NoRedundant(ctx.Guild.Id)))}\n\n";

            if (runner.Repeater.ConversationDetection)
                description += $"🗣️ {Format.Bold(Strings.StickyConversationDetectionStatus(ctx.Guild.Id))}\n\n";

            // Basic info
            description += $"<#{runner.Repeater.ChannelId}>\n";
            description += $"`{Strings.StickyTriggerModeLabel(ctx.Guild.Id)}` {Format.Bold(triggerMode.ToString())}\n";

            // Priority if not default
            if (runner.Repeater.Priority != 50)
                description +=
                    $"`{Strings.StickyPriorityLabel(ctx.Guild.Id)}` {Format.Bold(runner.Repeater.Priority.ToString())}\n";

            // Mode-specific timing info
            switch (triggerMode)
            {
                case StickyTriggerMode.OnActivity:
                case StickyTriggerMode.OnNoActivity:
                case StickyTriggerMode.AfterMessages:
                    var activityWindow = TimeSpan.Parse(runner.Repeater.ActivityTimeWindow ?? "00:05:00");
                    description +=
                        $"`{Strings.StickyActivityThresholdLabel(ctx.Guild.Id)}` {Format.Bold($"{runner.Repeater.ActivityThreshold} / {activityWindow.ToPrettyStringHm()}")}\n";
                    break;
                case StickyTriggerMode.Immediate:
                    description +=
                        $"`{Strings.StickyImmediateModeLabel(ctx.Guild.Id)}` {Format.Bold(Strings.StickyImmediateModeDescription(ctx.Guild.Id))}\n";
                    break;
                case StickyTriggerMode.TimeInterval:
                default:
                    description +=
                        $"`{Strings.Interval(ctx.Guild.Id)}` {intervalString}\n`{Strings.ExecutesIn(ctx.Guild.Id)}` {executesInString}\n";
                    break;
            }

            // Display stats
            description +=
                $"`{Strings.StickyDisplayCountLabel(ctx.Guild.Id)}` {Format.Bold(runner.Repeater.DisplayCount.ToString())}\n";

            // Thread settings
            if (runner.Repeater.ThreadAutoSticky)
                description += $"🧵 {Format.Bold(Strings.StickyThreadAutoStickyStatus(ctx.Guild.Id))}\n";

            if (runner.Repeater.ThreadOnlyMode)
                description += $"📱 {Format.Bold(Strings.StickyThreadOnlyModeStatus(ctx.Guild.Id))}\n";

            // Time conditions
            if (!string.IsNullOrWhiteSpace(runner.Repeater.TimeConditions))
            {
                description += $"⏰ {Format.Bold(Strings.StickyTimeConditionsActive(ctx.Guild.Id))}\n";
            }

            // Forum tag conditions
            if (!string.IsNullOrWhiteSpace(runner.Repeater.ForumTagConditions))
            {
                description += $"🏷️ {Format.Bold(Strings.StickyForumTagConditionsActive(ctx.Guild.Id))}\n";
            }

            description += $"`{Strings.Message(ctx.Guild.Id)}` {message}";

            return description;
        }
    }
}