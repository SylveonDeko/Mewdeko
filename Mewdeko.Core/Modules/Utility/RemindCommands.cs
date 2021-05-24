using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Common.TypeReaders.Models;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility
{
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

            private readonly DbService _db;
            private readonly GuildTimezoneService _tz;

            public RemindCommands(DbService db, GuildTimezoneService tz)
            {
                _db = db;
                _tz = tz;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [Priority(1)]
            public async Task Remind(MeOrHere meorhere, StoopidTime time, [Remainder] string message)
            {
                ulong target;
                target = meorhere == MeOrHere.Me ? ctx.User.Id : ctx.Channel.Id;
                if (!await RemindInternal(target, meorhere == MeOrHere.Me || ctx.Guild == null, time.Time, message)
                    .ConfigureAwait(false)) await ReplyErrorLocalizedAsync("remind_too_long").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            [Priority(0)]
            public async Task Remind(ITextChannel channel, StoopidTime time, [Remainder] string message)
            {
                var perms = ((IGuildUser) ctx.User).GetPermissions(channel);
                if (!perms.SendMessages || !perms.ViewChannel)
                {
                    await ReplyErrorLocalizedAsync("cant_read_or_send").ConfigureAwait(false);
                }
                else
                {
                    if (!await RemindInternal(channel.Id, false, time.Time, message).ConfigureAwait(false))
                        await ReplyErrorLocalizedAsync("remind_too_long").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task RemindList(int page = 1)
            {
                if (--page < 0)
                    return;

                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle(GetText("reminder_list"));

                List<Reminder> rems;
                using (var uow = _db.GetDbContext())
                {
                    rems = uow.Reminders.RemindersFor(ctx.User.Id, page)
                        .ToList();
                }

                if (rems.Any())
                {
                    var i = 0;
                    foreach (var rem in rems)
                    {
                        var when = rem.When;
                        var diff = when - DateTime.UtcNow;
                        embed.AddField(
                            $"#{++i + page * 10} {rem.When:HH:mm yyyy-MM-dd} UTC (in {(int) diff.TotalHours}h {diff.Minutes}m)",
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

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task RemindDelete(int index)
            {
                if (--index < 0)
                    return;

                var embed = new EmbedBuilder();

                Reminder rem = null;
                using (var uow = _db.GetDbContext())
                {
                    var rems = uow.Reminders.RemindersFor(ctx.User.Id, index / 10)
                        .ToList();
                    var pageIndex = index % 10;
                    if (rems.Count > pageIndex)
                    {
                        rem = rems[pageIndex];
                        uow.Reminders.Remove(rem);
                        uow.SaveChanges();
                    }
                }

                if (rem == null)
                {
                    await ReplyErrorLocalizedAsync("reminder_not_exist").ConfigureAwait(false);
                }
                else
                {
                    _service.RemoveReminder(rem.Id);
                    await ReplyErrorLocalizedAsync("reminder_deleted", index + 1).ConfigureAwait(false);
                }
            }

            public async Task<bool> RemindInternal(ulong targetId, bool isPrivate, TimeSpan ts,
                [Remainder] string message)
            {
                var time = DateTime.UtcNow + ts;

                if (ts > TimeSpan.FromDays(60))
                    return false;

                if (ctx.Guild != null)
                {
                    var perms = ((IGuildUser) ctx.User).GetPermissions((IGuildChannel) ctx.Channel);
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

                using (var uow = _db.GetDbContext())
                {
                    uow.Reminders.Add(rem);
                    await uow.SaveChangesAsync();
                }

                var gTime = ctx.Guild == null
                    ? time
                    : TimeZoneInfo.ConvertTime(time, _tz.GetTimeZoneOrUtc(ctx.Guild.Id));
                try
                {
                    await ctx.Channel.SendConfirmAsync(
                        "⏰ " + GetText("remind",
                            Format.Bold(!isPrivate ? $"<#{targetId}>" : ctx.User.Username),
                            Format.Bold(message.SanitizeMentions()),
                            $"{ts.Days}d {ts.Hours}h {ts.Minutes}min",
                            gTime, gTime)).ConfigureAwait(false);
                }
                catch
                {
                }

                _service.StartReminder(rem);
                return true;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task RemindTemplate([Remainder] string arg)
            {
                if (string.IsNullOrWhiteSpace(arg))
                    return;

                using (var uow = _db.GetDbContext())
                {
                    uow.BotConfig.GetOrCreate(set => set).RemindMessageFormat = arg.Trim();
                    await uow.SaveChangesAsync();
                }

                await ReplyConfirmLocalizedAsync("remind_template").ConfigureAwait(false);
            }
        }
    }
}