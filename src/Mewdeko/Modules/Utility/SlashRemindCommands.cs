using System.Threading.Tasks;
using Discord.Interactions;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Utility.Services;
using Swan;

namespace Mewdeko.Modules.Utility;

[Group("remind", "remind")]
public class SlashRemindCommands : MewdekoSlashModuleBase<RemindService>
{
    private readonly DbService db;
    private readonly GuildTimezoneService tz;

    public SlashRemindCommands(DbService db, GuildTimezoneService tz) => (this.db, this.tz) = (db, tz);

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
                    $@"`Target:` {(rem.IsPrivate ? "DM" : "Channel")}
`TargetId:` {rem.ChannelId}
`Message:` {rem.Message?.TrimTo(50)}");
            }
        }
        else
        {
            embed.WithDescription(GetText("reminders_none"));
        }

        embed.AddPaginatedFooter(page + 1, null);
        await RespondAsync(embed: embed.Build()).ConfigureAwait(false);
    }

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