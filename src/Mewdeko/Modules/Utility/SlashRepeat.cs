using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

/// <summary>
///     Provides slash commands for managing message repeaters (sticky messages) within the guild.
///     Allows for creating, modifying, and removing automated repeating messages.
/// </summary>
[Group("repeat", "Manage repeating and sticky messages")]
public class SlashRepeat(
    InteractiveService interactivity,
    ILogger<SlashRepeat> logger,
    GuildSettingsService gss,
    MessageCountService? messageCountService = null,
    GuildTimezoneService? guildTimezoneService = null,
    StickyConditionService? conditionService = null)
    : MewdekoSlashModuleBase<MessageRepeaterService>
{
    /// <summary>
    ///     Creates a repeater with the given message, optional interval, optional time of day and optional channel.
    /// </summary>
    /// <param name="message">The message to repeat.</param>
    /// <param name="interval">Optional interval between repeats. Must be between 5 seconds and 25000 minutes.</param>
    /// <param name="timeOfDay">Optional time of day, in the server timezone, at which the repeater runs.</param>
    /// <param name="channel">Optional channel to repeat in. Defaults to the current channel.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("create", "Creates a repeating message")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task Repeat(
        [Summary("message", "The message to repeat")]
        string message,
        [Summary("interval", "Interval between repeats, for example 30m or 2h")]
        TimeSpan? interval = null,
        [Summary("time-of-day", "Time of day to run at, for example 14:30")]
        string? timeOfDay = null,
        [Summary("channel", "Channel to repeat in. Defaults to this channel")]
        IGuildChannel? channel = null)
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

            channel ??= ctx.Channel as IGuildChannel;
            if (channel == null)
                return;

            if (channel is not (ITextChannel or IForumChannel))
            {
                await ReplyErrorAsync(Strings.StickyInvalidChannelType(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            GuildDateTime? dt = null;
            if (!string.IsNullOrWhiteSpace(timeOfDay))
            {
                if (!DateTime.TryParse(timeOfDay, out var parsed))
                {
                    await ReplyErrorAsync(Strings.RepeatInvalidTimeOfDay(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                var tz = guildTimezoneService?.GetTimeZoneOrUtc(ctx.Guild.Id);
                dt = new GuildDateTime(tz ?? TimeZoneInfo.Local, parsed);
            }

            var startTimeOfDay = dt?.InputTimeUtc.TimeOfDay;
            var realInterval = interval ?? (startTimeOfDay is null
                ? TimeSpan.FromMinutes(5)
                : TimeSpan.FromDays(1));

            if (interval != null)
            {
                if (interval.Value > TimeSpan.FromMinutes(25000))
                {
                    await ReplyErrorAsync(Strings.IntervalTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                if (interval.Value < TimeSpan.FromSeconds(5))
                {
                    await ReplyErrorAsync(Strings.IntervalTooShort(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }
            }

            var runner = await Service.CreateRepeaterAsync(
                ctx.Guild.Id,
                channel.Id,
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
            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepeaterCreated(ctx.Guild.Id))
                .WithDescription(description).Build()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating repeater");
            await ReplyErrorAsync(Strings.ErrorCreatingRepeater(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Immediately triggers a repeater by its index number. The repeater then continues on its normal schedule.
    /// </summary>
    /// <param name="index">The one-based index of the repeater to trigger.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("invoke", "Immediately triggers a repeater")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task RepeatInvoke([Summary("index", "The repeater number from the list")] int index)
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

        await EphemeralReplyConfirmAsync(Strings.RepeatInvoked(ctx.Guild.Id, index)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a repeater by its index number. This action is permanent.
    /// </summary>
    /// <param name="index">The one-based index of the repeater to remove.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("remove", "Removes a repeater")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task RepeatRemove([Summary("index", "The repeater number from the list")] int index)
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

        await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.RepeaterRemoved(ctx.Guild.Id, index))
            .WithDescription(description).Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles the redundancy check for a repeater. When enabled, the repeater will not send a message if its
    ///     message is the last one in the channel.
    /// </summary>
    /// <param name="index">The one-based index of the repeater to modify.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("redundant", "Toggles skipping the repeat when its message is already the last one")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task RepeatRedundant([Summary("index", "The repeater number from the list")] int index)
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
    ///     Lists all active repeaters in the guild, showing index, channel, interval, and next execution time.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("list", "Lists all repeaters in this server")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
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

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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
    ///     Opens a modal to update the message of an existing repeater. Only the message content changes.
    /// </summary>
    /// <param name="index">The one-based index of the repeater to update.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("message", "Changes the message of a repeater")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task RepeatMessage([Summary("index", "The repeater number from the list")] int index)
    {
        if (!Service.RepeaterReady)
            return;

        var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
        if (repeater == null)
        {
            await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await RespondWithModalAsync<RepeatMessageModal>($"repeat_message:{index}");
    }

    /// <summary>
    ///     Handles the repeater message modal and updates the message of the repeater.
    /// </summary>
    /// <param name="indexStr">The one-based index of the repeater, as submitted with the modal.</param>
    /// <param name="modal">The modal containing the new message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ModalInteraction("repeat_message:*", true)]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task RepeatMessageSubmitted(string indexStr, RepeatMessageModal modal)
    {
        if (!Service.RepeaterReady)
            return;

        if (!int.TryParse(indexStr, out var index))
            return;

        var message = modal.Message;
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
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("channel", "Changes the channel of a repeater")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task RepeatChannel(
        [Summary("index", "The repeater number from the list")]
        int index,
        [Summary("channel", "The new channel. Defaults to this channel")]
        IGuildChannel? channel = null)
    {
        if (!Service.RepeaterReady)
            return;

        channel ??= ctx.Channel as IGuildChannel;
        if (channel == null)
            return;

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
    /// <param name="triggerMode">The trigger mode (TimeInterval, OnActivity, OnNoActivity, Immediate, AfterMessages).</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("trigger-mode", "Sets how a repeater is triggered")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task RepeatTriggerMode(
        [Summary("index", "The repeater number from the list")]
        int index,
        [Summary("mode", "The trigger mode")] StickyTriggerMode triggerMode)
    {
        if (!Service.RepeaterReady)
            return;

        var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
        if (repeater == null)
        {
            await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

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
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("activity", "Sets the activity threshold of a repeater")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task RepeatActivity(
        [Summary("index", "The repeater number from the list")]
        int index,
        [Summary("threshold", "Number of messages needed to trigger")]
        int threshold,
        [Summary("time-window", "Time window for activity detection, for example 5m")]
        TimeSpan timeWindow)
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

        if (timeWindow < TimeSpan.FromSeconds(30) || timeWindow > TimeSpan.FromHours(6))
        {
            await ReplyErrorAsync(Strings.StickyActivityTimeWindowInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

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
            threshold, timeWindow);
        if (!success)
        {
            await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ReplyConfirmAsync(Strings.StickyActivityThresholdSet(ctx.Guild.Id, index, threshold,
                timeWindow.ToPrettyStringHm()))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the priority for a repeater (0-100, higher = more important).
    /// </summary>
    /// <param name="index">The one-based index of the repeater to modify.</param>
    /// <param name="priority">Priority level (0-100).</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("priority", "Sets the priority of a repeater, 0 to 100")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task RepeatPriority(
        [Summary("index", "The repeater number from the list")]
        int index,
        [Summary("priority", "Priority level, 0 to 100")]
        int priority)
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
    /// <param name="preset">Preset time condition (business, evening, weekend) or none to disable.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("schedule", "Sets time based scheduling for a repeater")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task RepeatSchedule(
        [Summary("index", "The repeater number from the list")]
        int index,
        [Summary("preset", "The schedule preset")]
        [Choice("business", "business")]
        [Choice("evening", "evening")]
        [Choice("weekend", "weekend")]
        [Choice("none", "none")]
        string preset)
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

            if (timezone == "UTC" && guildTimezoneService != null)
            {
                await ctx.Interaction.FollowupAsync(
                        Strings.StickyTimezoneReminder(ctx.Guild.Id, await gss.GetPrefix(ctx.Guild)))
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Toggles conversation detection for a repeater.
    /// </summary>
    /// <param name="index">The one-based index of the repeater to modify.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("conversation", "Toggles conversation detection for a repeater")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task RepeatConversation([Summary("index", "The repeater number from the list")] int index)
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
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("toggle", "Enables or disables a repeater")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task RepeatToggle([Summary("index", "The repeater number from the list")] int index)
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
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("thread-auto-sticky", "Toggles auto sticky in new threads for a repeater")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task RepeatThreadAutoSticky([Summary("index", "The repeater number from the list")] int index)
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
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("thread-only", "Toggles thread only mode for a repeater")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task RepeatThreadOnly([Summary("index", "The repeater number from the list")] int index)
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
    ///     Toggles notification suppression for a repeater. When enabled, the repeater messages will not send push or
    ///     desktop notifications to users.
    /// </summary>
    /// <param name="index">The one-based index of the repeater to modify.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("suppress-notifications", "Toggles notification suppression for a repeater")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task RepeatSuppressNotifications([Summary("index", "The repeater number from the list")] int index)
    {
        if (!Service.RepeaterReady)
            return;

        var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
        if (repeater == null)
        {
            await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var success = await Service.ToggleRepeaterSuppressNotificationsAsync(ctx.Guild.Id, repeater.Repeater.Id);
        if (!success)
        {
            await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (repeater.Repeater.SuppressNotifications)
            await ReplyConfirmAsync(Strings.RepeaterSuppressNotificationsEnabled(ctx.Guild.Id, index))
                .ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.RepeaterSuppressNotificationsDisabled(ctx.Guild.Id, index))
                .ConfigureAwait(false);
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

        if (!runner.Repeater.IsEnabled)
            description += $"{Format.Bold(Strings.StickyDisabledStatus(ctx.Guild.Id))}\n\n";

        if (runner.Repeater.NoRedundant)
            description += $"{Format.Underline(Format.Bold(Strings.NoRedundant(ctx.Guild.Id)))}\n\n";

        if (runner.Repeater.ConversationDetection)
            description += $"{Format.Bold(Strings.StickyConversationDetectionStatus(ctx.Guild.Id))}\n\n";

        description += $"<#{runner.Repeater.ChannelId}>\n";
        description += $"`{Strings.StickyTriggerModeLabel(ctx.Guild.Id)}` {Format.Bold(triggerMode.ToString())}\n";

        if (runner.Repeater.Priority != 50)
            description +=
                $"`{Strings.StickyPriorityLabel(ctx.Guild.Id)}` {Format.Bold(runner.Repeater.Priority.ToString())}\n";

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

        description +=
            $"`{Strings.StickyDisplayCountLabel(ctx.Guild.Id)}` {Format.Bold(runner.Repeater.DisplayCount.ToString())}\n";

        if (runner.Repeater.ThreadAutoSticky)
            description += $"{Format.Bold(Strings.StickyThreadAutoStickyStatus(ctx.Guild.Id))}\n";

        if (runner.Repeater.ThreadOnlyMode)
            description += $"{Format.Bold(Strings.StickyThreadOnlyModeStatus(ctx.Guild.Id))}\n";

        if (runner.Repeater.SuppressNotifications)
            description += $"{Format.Bold(Strings.RepeaterSuppressNotificationsStatus(ctx.Guild.Id))}\n";

        if (!string.IsNullOrWhiteSpace(runner.Repeater.TimeConditions))
        {
            description += $"{Format.Bold(Strings.StickyTimeConditionsActive(ctx.Guild.Id))}\n";
        }

        if (!string.IsNullOrWhiteSpace(runner.Repeater.ForumTagConditions))
        {
            description += $"{Format.Bold(Strings.StickyForumTagConditionsActive(ctx.Guild.Id))}\n";
        }

        description += $"`{Strings.Message(ctx.Guild.Id)}` {message}";

        return description;
    }

    /// <summary>
    ///     Manages forum tag conditions for repeaters.
    /// </summary>
    [Group("forum-tags", "Forum tag conditions for repeaters")]
    public class RepeatForumTagCommands(StickyConditionService? conditionService = null)
        : MewdekoSlashSubmodule<MessageRepeaterService>
    {
        /// <summary>
        ///     Adds forum tag conditions to a repeater.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to modify.</param>
        /// <param name="tagType">Type of tag rule: required or excluded.</param>
        /// <param name="tags">Comma-separated list of tag ids.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("add", "Adds required or excluded forum tags to a repeater")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageMessages)]
        public Task RepeatForumTags(
            [Summary("index", "The repeater number from the list")]
            int index,
            [Summary("type", "Whether the tags are required or excluded")]
            [Choice("required", "required")]
            [Choice("excluded", "excluded")]
            string tagType,
            [Summary("tags", "Comma separated tag ids")]
            string tags)
        {
            return ApplyTags(index, "add", tagType, tags);
        }

        /// <summary>
        ///     Removes forum tag conditions from a repeater.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to modify.</param>
        /// <param name="tagType">Type of tag rule: required or excluded.</param>
        /// <param name="tags">Comma-separated list of tag ids.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("remove", "Removes required or excluded forum tags from a repeater")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageMessages)]
        public Task Remove(
            [Summary("index", "The repeater number from the list")]
            int index,
            [Summary("type", "Whether the tags are required or excluded")]
            [Choice("required", "required")]
            [Choice("excluded", "excluded")]
            string tagType,
            [Summary("tags", "Comma separated tag ids")]
            string tags)
        {
            return ApplyTags(index, "remove", tagType, tags);
        }

        /// <summary>
        ///     Clears all forum tag conditions of a repeater.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to modify.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("clear", "Clears all forum tag conditions of a repeater")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageMessages)]
        public async Task Clear([Summary("index", "The repeater number from the list")] int index)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var clearResult =
                await Service.UpdateRepeaterForumTagConditionsAsync(ctx.Guild.Id, repeater.Repeater.Id, null);
            if (clearResult)
                await ReplyConfirmAsync(Strings.StickyForumTagsCleared(ctx.Guild.Id, index))
                    .ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists the forum tag conditions of a repeater.
        /// </summary>
        /// <param name="index">The one-based index of the repeater to inspect.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("list", "Lists the forum tag conditions of a repeater")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageMessages)]
        public async Task List([Summary("index", "The repeater number from the list")] int index)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var currentConditions =
                conditionService?.ParseForumTagConditions(repeater.Repeater.ForumTagConditions);
            if (currentConditions == null || !currentConditions.RequiredTags?.Any() == true &&
                !currentConditions.ExcludedTags?.Any() == true)
            {
                await ctx.Interaction.RespondAsync(Strings.StickyForumTagsNone(ctx.Guild.Id, index))
                    .ConfigureAwait(false);
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

                await ctx.Interaction.RespondAsync(Strings.StickyForumTagsList(ctx.Guild.Id, index, description))
                    .ConfigureAwait(false);
            }
        }

        private async Task ApplyTags(int index, string actionLower, string tagType, string tags)
        {
            if (!Service.RepeaterReady)
                return;

            var repeater = Service.GetRepeaterByIndex(ctx.Guild.Id, index - 1);
            if (repeater == null)
            {
                await ReplyErrorAsync(Strings.IndexOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

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
                await ReplyConfirmAsync(Strings.StickyForumTagsApplied(ctx.Guild.Id, verb, tagIds.Count, typeLower,
                        index))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.RepeatActionFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }
    }
}