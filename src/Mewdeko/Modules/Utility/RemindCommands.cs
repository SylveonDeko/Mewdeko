using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Utility.Services;
using Swan;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    [Group]
    public class RemindCommands : MewdekoSubmodule<RemindService>
    {
        public enum MeOrHere
        {
            Me,
            Here
        }

        private readonly DbService db;
        private readonly GuildTimezoneService tz;

        public RemindCommands(DbService db, GuildTimezoneService tz)
        {
            this.db = db;
            this.tz = tz;
        }

        [Cmd, Aliases, Priority(1)]
        public async Task Remind(MeOrHere meorhere, [Remainder] string remindString)
        {
            if (!Service.TryParseRemindMessage(remindString, out var remindData))
            {
                await ReplyErrorLocalizedAsync("remind_invalid").ConfigureAwait(false);
                return;
            }

            var target = meorhere == MeOrHere.Me ? ctx.User.Id : ctx.Channel.Id;
            if (!await RemindInternal(target, meorhere == MeOrHere.Me || ctx.Guild == null, remindData.Time,
                        remindData.What)
                    .ConfigureAwait(false))
            {
                await ReplyErrorLocalizedAsync("remind_too_long").ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageMessages), Priority(0)]
        public async Task Remind(ITextChannel channel, [Remainder] string remindString)
        {
            var perms = ((IGuildUser)ctx.User).GetPermissions(channel);
            if (!perms.SendMessages || !perms.ViewChannel)
            {
                await ReplyErrorLocalizedAsync("cant_read_or_send").ConfigureAwait(false);
                return;
            }

            if (!Service.TryParseRemindMessage(remindString, out var remindData))
            {
                await ReplyErrorLocalizedAsync("remind_invalid").ConfigureAwait(false);
                return;
            }

            if (!await RemindInternal(channel.Id, false, remindData.Time, remindData.What)
                    .ConfigureAwait(false))
            {
                await ReplyErrorLocalizedAsync("remind_too_long").ConfigureAwait(false);
            }
        }

        [Cmd, Aliases]
        public async Task RemindList(int page = 1)
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
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task RemindDelete(int index)
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

        private async Task<bool> RemindInternal(ulong targetId, bool isPrivate, TimeSpan ts, string? message)
        {
            if (ts > TimeSpan.FromDays(367))
                return false;

            var time = DateTime.UtcNow + ts;

            if (ctx.Guild != null)
            {
                var perms = ((IGuildUser)ctx.User).GetPermissions((IGuildChannel)ctx.Channel);
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
                await ctx.Channel.SendConfirmAsync(
                        $"⏰ {GetText("remind", Format.Bold(!isPrivate ? $"<#{targetId}>" : ctx.User.Username), Format.Bold(message), $"<t:{unixTime}:R>", gTime, gTime)}")
                    .ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            return true;
        }
    }
}