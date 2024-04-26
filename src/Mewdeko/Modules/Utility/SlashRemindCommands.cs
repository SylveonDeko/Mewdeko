using Discord.Interactions;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Utility.Services;
using Swan;

namespace Mewdeko.Modules.Utility;

/// <summary>
/// Handles commands for setting, viewing, and managing reminders.
/// </summary>
[Group("remind", "remind")]
public class SlashRemindCommands(DbService db, GuildTimezoneService tz) : MewdekoSlashModuleBase<RemindService>
{
    /// <summary>
    /// Sends a reminder to the user invoking the command.
    /// </summary>
    /// <param name="time">When the reminder should trigger.</param>
    /// <param name="reminder">The message for the reminder. If empty, prompts the user to input the reminder text.</param>
    /// <returns>A task that represents the asynchronous operation of adding a personal reminder.</returns>
    [SlashCommand("me", "Send a reminder to yourself.")]
    // ReSharper disable once MemberCanBePrivate.Global
    public async Task Me
    (
        [Summary("time", "When should the reminder respond.")]
        TimeSpan time,
        [Summary("reminder", "(optional) what should the reminder message be")]
        string? reminder = ""
    )
    {
        if (string.IsNullOrEmpty(reminder))
        {
            await RespondWithModalAsync<ReminderModal>($"remind:{ctx.User.Id},1,{time};").ConfigureAwait(false);
            return;
        }

        await RemindInternal(ctx.User.Id, true, time, reminder).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a reminder to the channel where the command was invoked.
    /// </summary>
    /// <param name="time">When the reminder should trigger.</param>
    /// <param name="reminder">The message for the reminder. If empty, prompts the user to input the reminder text.</param>
    /// <returns>A task that represents the asynchronous operation of adding a channel reminder.</returns>
    [SlashCommand("here", "Send a reminder to this channel.")]
    public async Task Here
    (
        [Summary("time", "When should the reminder respond.")]
        TimeSpan time,
        [Summary("reminder", "(optional) what should the reminder message be")]
        string? reminder = ""
    )
    {
        if (ctx.Guild is null)
        {
            await Me(time, reminder).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrEmpty(reminder))
        {
            await RespondWithModalAsync<ReminderModal>($"remind:{ctx.Channel.Id},0,{time};").ConfigureAwait(false);
            return;
        }

        await RemindInternal(ctx.Channel.Id, false, time, reminder).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a reminder to a specified channel.
    /// </summary>
    /// <param name="channel">The target channel for the reminder.</param>
    /// <param name="time">When the reminder should trigger.</param>
    /// <param name="reminder">The message for the reminder. If empty, prompts the user to input the reminder text.</param>
    /// <returns>A task that represents the asynchronous operation of adding a reminder to a specific channel.</returns>
    [SlashCommand("channel", "Send a reminder to this channel."),
     UserPerm(ChannelPermission.ManageMessages)]
    public async Task Channel
    (
        [Summary("channel", "where should the reminder be sent?")]
        ITextChannel channel,
        [Summary("time", "When should the reminder respond.")]
        TimeSpan time,
        [Summary("reminder", "(optional) what should the reminder message be")]
        string? reminder = ""
    )
    {
        var perms = ((IGuildUser)ctx.User).GetPermissions(channel);
        if (!perms.SendMessages || !perms.ViewChannel)
        {
            await ReplyErrorLocalizedAsync("cant_read_or_send").ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrEmpty(reminder))
        {
            await RespondWithModalAsync<ReminderModal>($"remind:{channel.Id},0,{time};").ConfigureAwait(false);
            return;
        }

        if (!await RemindInternal(channel.Id, false, time, reminder)
                .ConfigureAwait(false))
        {
            await ReplyErrorLocalizedAsync("remind_too_long").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles the modal interaction for creating a reminder.
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

        await RemindInternal(id, pri, time, modal.Reminder).ConfigureAwait(false);
    }

    private async Task<bool> RemindInternal(ulong targetId, bool isPrivate, TimeSpan ts, string? message)
    {
        if (ts > TimeSpan.FromDays(60))
            return false;

        var time = DateTime.UtcNow + ts;

        if (ctx.Guild is not null)
        {
            var perms = (ctx.User as IGuildUser).GetPermissions(ctx.Channel as IGuildChannel);
            if (!perms.MentionEveryone) message = message.SanitizeAllMentions();
        }

        var rem = new Reminder
        {
            ChannelId = targetId,
            IsPrivate = isPrivate,
            When = time,
            Message = message,
            UserId = ctx.User.Id,
            ServerId = ctx.Guild?.Id ?? 0
        };

        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            uow.Reminders.Add(rem);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        var gTime = ctx.Guild == null
            ? time
            : TimeZoneInfo.ConvertTime(time, tz.GetTimeZoneOrUtc(ctx.Guild.Id));

        try
        {
            var unixTime = time.ToUnixEpochDate();
            await RespondAsync(
                $"⏰ {GetText("remind", Format.Bold(!isPrivate ? $"<#{targetId}>" : ctx.User.Username), Format.Bold(message), $"<t:{unixTime}:R>", gTime, gTime)}",
                ephemeral: isPrivate).ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }

        return true;
    }

    /// <summary>
    /// Lists the current reminders set by the user.
    /// </summary>
    /// <param name="page">The page of reminders to display, starting at 1.</param>
    /// <returns>A task that represents the asynchronous operation of listing reminders.</returns>
    [SlashCommand("list", "List your current reminders")]
    public async Task List(
        [Summary("page", "What page of reminders do you want to load.")]
        int page = 1)
    {
        if (--page < 0)
            return;

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(GetText("reminder_list"));

        List<Reminder> rems;
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            rems = uow.Reminders.RemindersFor(ctx.User.Id, page)
                .ToList();
        }

        if (rems.Count > 0)
        {
            var i = 0;
            foreach (var rem in rems)
            {
                var when = rem.When;
                var diff = when - DateTime.UtcNow;
                embed.AddField(
                    $"#{++i + (page * 10)} {rem.When:HH:mm yyyy-MM-dd} UTC (in {(int)diff.TotalHours}h {diff.Minutes}m)",
                    $"""
                     `Target:` {((rem.IsPrivate) ? "DM" : "Channel")}
                     `TargetId:` {rem.ChannelId}
                     `Message:` {rem.Message?.TrimTo(50)}
                     """);
            }
        }
        else
        {
            embed.WithDescription(GetText("reminders_none"));
        }

        embed.AddPaginatedFooter(page + 1, null);
        await RespondAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a specific reminder.
    /// </summary>
    /// <param name="index">The index of the reminder to delete, as displayed in the reminder list.</param>
    /// <returns>A task that represents the asynchronous operation of deleting a reminder.</returns>
    [SlashCommand("delete", "Delete a reminder")]
    public async Task RemindDelete([Summary("index", "The reminders index (from /remind list)")] int index)
    {
        if (--index < 0)
            return;

        Reminder? rem = null;
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var rems = uow.Reminders.RemindersFor(ctx.User.Id, index / 10)
                .ToList();
            var pageIndex = index % 10;
            if (rems.Count > pageIndex)
            {
                rem = rems[pageIndex];
                uow.Reminders.Remove(rem);
                await uow.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        if (rem == null)
            await ReplyErrorLocalizedAsync("reminder_not_exist").ConfigureAwait(false);
        else
            await ReplyErrorLocalizedAsync("reminder_deleted", index + 1).ConfigureAwait(false);
    }
}