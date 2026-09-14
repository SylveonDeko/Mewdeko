using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
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
        ITextChannel? channel = null)
    {
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
            await RespondWithModalAsync<ReminderModal>($"remind:{targetId},{(isPrivate ? 1 : 0)},{time};")
                .ConfigureAwait(false);
            return;
        }

        await DeferAsync(isPrivate);

        var (success, message) = await Service.CreateReminderAsync(
            targetId,
            isPrivate,
            time,
            reminder,
            ctx.User.Id,
            ctx.Guild?.Id,
            shouldSanitize
        );

        if (success)
        {
            await ReplyConfirmAsync(message).ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorAsync(Strings.RemindTooLong(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Handles the modal interaction for creating a reminder.
    /// </summary>
    /// <param name="sId">The target ID for the reminder, either a user or a channel.</param>
    /// <param name="sPri">Indicates if the reminder is private.</param>
    /// <param name="sTime">The time when the reminder should trigger.</param>
    /// <param name="modal">The modal containing the reminder text.</param>
    /// <returns>A task that represents the asynchronous operation of processing the reminder modal submission.</returns>
    [ModalInteraction("remind:*,*,*;", true)]
    public async Task ReminderModal(string sId, string sPri, string sTime, ReminderModal modal)
    {
        var id = ulong.Parse(sId);
        var pri = int.Parse(sPri) == 1;
        var time = TimeSpan.Parse(sTime);
        await DeferAsync(pri);

        var shouldSanitize = ctx.Guild != null &&
                             !((IGuildUser)ctx.User).GetPermissions((IGuildChannel)ctx.Channel).MentionEveryone;

        var (success, message) = await Service.CreateReminderAsync(
            id,
            pri,
            time,
            modal.Reminder,
            ctx.User.Id,
            ctx.Guild?.Id,
            shouldSanitize
        );

        if (success)
        {
            await ReplyConfirmAsync(message).ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorAsync(Strings.RemindTooLong(ctx.Guild.Id)).ConfigureAwait(false);
        }
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
                     `Target:` {(rem.IsPrivate ? "DM" : "Channel")}
                     `TargetId:` {rem.ChannelId}
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