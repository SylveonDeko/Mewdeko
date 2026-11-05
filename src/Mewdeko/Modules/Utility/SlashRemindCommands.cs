using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

/// <summary>
///     Handles commands for setting, viewing, and managing reminders.
/// </summary>
[Group("remind", "remind")]
public class SlashRemindCommands(InteractiveService interactivity) : MewdekoSlashModuleBase<RemindService>
{
    /// <summary>
    ///     The targets a reminder can be sent to.
    /// </summary>
    public enum RemindTarget
    {
        /// <summary>
        ///     Sends the reminder to the user directly.
        /// </summary>
        Me,

        /// <summary>
        ///     Sends the reminder to the current channel.
        /// </summary>
        Here,

        /// <summary>
        ///     Sends the reminder to a specified channel.
        /// </summary>
        Channel
    }

    /// <summary>
    ///     Creates a reminder for the user, the current channel, or a specified channel.
    /// </summary>
    /// <param name="target">Whether to send the reminder to you, this channel, or another channel.</param>
    /// <param name="time">When the reminder should trigger.</param>
    /// <param name="reminder">The message for the reminder. If empty, prompts the user to input the reminder text.</param>
    /// <param name="channel">The target channel when the target is Channel.</param>
    /// <returns>A task that represents the asynchronous operation of adding a reminder.</returns>
    [SlashCommand("set", "Send a reminder to yourself, this channel, or another channel.")]
    public async Task Remind(
        [Summary("target", "Where the reminder should be sent.")]
        RemindTarget target,
        [Summary("time", "When should the reminder respond.")]
        TimeSpan time,
        [Summary("reminder", "(optional) what should the reminder message be")]
        string? reminder = "",
        [Summary("channel", "The channel to send the reminder to when the target is Channel.")]
        ITextChannel? channel = null,
        [Summary("repeat", "Repeat the reminder at this interval, at least one hour.")]
        TimeSpan? repeat = null)
    {
        if (repeat.HasValue && (repeat.Value < RemindService.MinRepeat || repeat.Value > TimeSpan.FromDays(60)))
        {
            await ReplyErrorAsync(Strings.RemindRepeatRange(ctx.Guild?.Id ?? 0)).ConfigureAwait(false);
            return;
        }

        if (target == RemindTarget.Here && ctx.Guild is null)
            target = RemindTarget.Me;

        if (target == RemindTarget.Channel && channel is null)
            target = ctx.Guild is null ? RemindTarget.Me : RemindTarget.Here;

        ulong targetId;
        bool isPrivate;
        bool shouldSanitize;

        switch (target)
        {
            case RemindTarget.Me:
                targetId = ctx.User.Id;
                isPrivate = true;
                shouldSanitize = false;
                break;
            case RemindTarget.Here:
                targetId = ctx.Channel.Id;
                isPrivate = false;
                shouldSanitize = !((IGuildUser)ctx.User).GetPermissions((IGuildChannel)ctx.Channel).MentionEveryone;
                break;
            default:
                var guildUser = (IGuildUser)ctx.User;
                if (!guildUser.GuildPermissions.ManageMessages)
                {
                    await ReplyErrorAsync(Strings.CantReadOrSend(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                var perms = guildUser.GetPermissions(channel!);
                if (!perms.SendMessages || !perms.ViewChannel)
                {
                    await ReplyErrorAsync(Strings.CantReadOrSend(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                targetId = channel!.Id;
                isPrivate = false;
                shouldSanitize = !perms.MentionEveryone;
                break;
        }

        if (string.IsNullOrEmpty(reminder))
        {
            await RespondWithModalAsync<ReminderModal>(
                    $"remind:{targetId},{(isPrivate ? 1 : 0)},{time},{repeat ?? TimeSpan.Zero};")
                .ConfigureAwait(false);
            return;
        }

        await DeferAsync(isPrivate);

        var (success, message, id) = await Service.CreateReminderAsync(
            targetId,
            isPrivate,
            time,
            reminder,
            ctx.User.Id,
            ctx.Guild?.Id,
            shouldSanitize,
            repeat
        );

        await RespondCreatedAsync(success, message, id, isPrivate).ConfigureAwait(false);
    }

    private async Task RespondCreatedAsync(bool success, string message, int id, bool isPrivate)
    {
        if (!success)
        {
            await ReplyErrorAsync(message).ConfigureAwait(false);
            return;
        }

        if (isPrivate)
        {
            await ReplyConfirmAsync(message).ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.FollowupAsync(
            embed: new EmbedBuilder().WithOkColor().WithDescription($"{Format.Bold(ctx.User.ToString())} {message}")
                .Build(),
            components: Service.SubscribeButton(id, ctx.Guild?.Id ?? 0).Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Subscribes the user who pressed the button to a reminder.
    /// </summary>
    /// <param name="sId">The id of the reminder.</param>
    /// <returns>A task that represents the asynchronous operation of subscribing.</returns>
    [ComponentInteraction("remindsub:*", true)]
    public async Task SubscribeButton(string sId)
    {
        var result = await Service.SubscribeAsync(ctx.User.Id, int.Parse(sId));
        var guildId = ctx.Guild?.Id ?? 0;
        var text = result switch
        {
            RemindService.SubscribeResult.Subscribed => Strings.RemindSubscribed(guildId),
            RemindService.SubscribeResult.AlreadySubscribed => Strings.RemindAlreadySubscribed(guildId),
            RemindService.SubscribeResult.Owner => Strings.RemindOwnReminder(guildId),
            _ => Strings.ReminderNotExist(guildId)
        };

        await EphemeralReplyConfirmAsync(text).ConfigureAwait(false);
    }

    /// <summary>
    ///     Subscribes the user to a reminder by id.
    /// </summary>
    /// <param name="id">The reminder id shown in the reminder list.</param>
    /// <returns>A task that represents the asynchronous operation of subscribing.</returns>
    [SlashCommand("subscribe", "Get pinged along with someone's reminder")]
    public Task Subscribe([Summary("id", "The reminder id (from /remind list)")] int id)
    {
        return SubscribeButton(id.ToString());
    }

    /// <summary>
    ///     Removes the user's subscription from a reminder.
    /// </summary>
    /// <param name="id">The reminder id shown in the reminder list.</param>
    /// <returns>A task that represents the asynchronous operation of unsubscribing.</returns>
    [SlashCommand("unsubscribe", "Stop getting pinged for someone's reminder")]
    public async Task Unsubscribe([Summary("id", "The reminder id (from /remind list)")] int id)
    {
        var removed = await Service.UnsubscribeAsync(ctx.User.Id, id);
        var guildId = ctx.Guild?.Id ?? 0;
        await EphemeralReplyConfirmAsync(removed
            ? Strings.RemindUnsubscribed(guildId)
            : Strings.RemindNotSubscribed(guildId)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Makes one of the user's reminders repeat, or stops it repeating.
    /// </summary>
    /// <param name="index">The number of the reminder in the reminder list.</param>
    /// <param name="interval">How often to repeat, at least one hour. Leave empty to stop repeating.</param>
    /// <returns>A task that represents the asynchronous operation of changing the repeat.</returns>
    [SlashCommand("repeat", "Make a reminder repeat, or stop it repeating")]
    public async Task Repeat(
        [Summary("index", "The reminders index (from /remind list)")]
        int index,
        [Summary("interval", "How often to repeat, at least one hour. Leave empty to stop.")]
        TimeSpan? interval = null)
    {
        var guildId = ctx.Guild?.Id ?? 0;
        if (interval.HasValue && (interval.Value < RemindService.MinRepeat || interval.Value > TimeSpan.FromDays(60)))
        {
            await ReplyErrorAsync(Strings.RemindRepeatRange(guildId)).ConfigureAwait(false);
            return;
        }

        if (!await Service.SetRepeatAsync(ctx.User.Id, index - 1, interval))
        {
            await ReplyErrorAsync(Strings.ReminderNotExist(guildId)).ConfigureAwait(false);
            return;
        }

        await ReplyConfirmAsync(interval.HasValue
            ? Strings.RemindRepeatSet(guildId, index, interval.Value.Humanize())
            : Strings.RemindRepeatCleared(guildId, index)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles the modal interaction for creating a reminder.
    /// </summary>
    /// <param name="sId">The target ID for the reminder, either a user or a channel.</param>
    /// <param name="sPri">Indicates if the reminder is private.</param>
    /// <param name="sTime">The time when the reminder should trigger.</param>
    /// <param name="modal">The modal containing the reminder text.</param>
    /// <returns>A task that represents the asynchronous operation of processing the reminder modal submission.</returns>
    [ModalInteraction("remind:*,*,*,*;", true)]
    public async Task ReminderModal(string sId, string sPri, string sTime, string sRepeat, ReminderModal modal)
    {
        var id = ulong.Parse(sId);
        var pri = int.Parse(sPri) == 1;
        var time = TimeSpan.Parse(sTime);
        var repeat = TimeSpan.Parse(sRepeat);
        await DeferAsync(pri);

        var shouldSanitize = ctx.Guild != null &&
                             !((IGuildUser)ctx.User).GetPermissions((IGuildChannel)ctx.Channel).MentionEveryone;

        var (success, message, reminderId) = await Service.CreateReminderAsync(
            id,
            pri,
            time,
            modal.Reminder,
            ctx.User.Id,
            ctx.Guild?.Id,
            shouldSanitize,
            repeat == TimeSpan.Zero ? null : repeat
        );

        await RespondCreatedAsync(success, message, reminderId, pri).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists the current reminders set by the user.
    /// </summary>
    /// <param name="page">The page of reminders to display, starting at 1.</param>
    /// <returns>A task that represents the asynchronous operation of listing reminders.</returns>
    [SlashCommand("list", "List your current reminders")]
    public async Task List(
        [Summary("page", "What page of reminders do you want to load.")]
        int page = 1)
    {
        await ctx.Interaction.SendConfirmAsync(Strings.Loading(ctx.Guild.Id)).ConfigureAwait(false);

        var reminders = await Service.GetUserRemindersAsync(ctx.User.Id);

        if (!reminders.Any())
        {
            await ctx.Interaction.DeleteOriginalResponseAsync().ConfigureAwait(false);
            await ReplyErrorAsync(Strings.RemindersNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(reminders.Count / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await ctx.Interaction.DeleteOriginalResponseAsync().ConfigureAwait(false);
        await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);

            var pageBuilder = new PageBuilder()
                .WithOkColor()
                .WithTitle(Strings.ReminderList(ctx.Guild.Id));

            var pageReminders = reminders.Skip(page * 10).Take(10);
            var i = page * 10;

            foreach (var rem in pageReminders)
            {
                var when = rem.When;
                var diff = when - DateTime.UtcNow;
                pageBuilder.AddField(
                    $"#{++i} {rem.When:HH:mm yyyy-MM-dd} UTC (in {(int)diff.TotalHours}h {diff.Minutes}m)",
                    $"""
                     `Id:` {rem.Id}
                     `Target:` {(rem.IsPrivate ? "DM" : "Channel")}
                     `TargetId:` {rem.ChannelId}
                     `Repeats:` {(rem.RepeatMinutes > 0 ? TimeSpan.FromMinutes(rem.RepeatMinutes).Humanize() : "No")}
                     `Message:` {rem.Message?.TrimTo(50)}
                     """);
            }

            return pageBuilder;
        }
    }

    /// <summary>
    ///     Deletes a specific reminder.
    /// </summary>
    /// <param name="index">The index of the reminder to delete, as displayed in the reminder list.</param>
    /// <returns>A task that represents the asynchronous operation of deleting a reminder.</returns>
    [SlashCommand("delete", "Delete a reminder")]
    public async Task RemindDelete([Summary("index", "The reminders index (from /remind list)")] int index)
    {
        if (--index < 0)
            return;

        var success = await Service.DeleteReminderAsync(ctx.User.Id, index);

        if (!success)
            await ReplyErrorAsync(Strings.ReminderNotExist(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.ReminderDeleted(ctx.Guild.Id, index + 1)).ConfigureAwait(false);
    }
}