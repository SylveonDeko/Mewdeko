using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
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
            private readonly DbService _db;
            private readonly GuildTimezoneService _tz;

            public RemindCommands(DbService db, GuildTimezoneService tz)
            {
                _db = db;
                _tz = tz;
            }

            public enum MeOrHere
            {
                Me,
                Here
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public async Task Remind(MeOrHere meorhere, [Leftover] string remindString)
            {
                if (!_service.TryParseRemindMessage(remindString, out var remindData))
                {
                    await ReplyErrorLocalizedAsync("remind_invalid");
                    return;
                }
                
                ulong target;
                target = meorhere == MeOrHere.Me ? ctx.User.Id : ctx.Channel.Id;
                if (!await RemindInternal(target, meorhere == MeOrHere.Me || ctx.Guild == null, remindData.Time, remindData.What)
                    .ConfigureAwait(false))
                {
                    await ReplyErrorLocalizedAsync("remind_too_long").ConfigureAwait(false);
                }
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            [Priority(0)]
            public async Task Remind(ITextChannel channel, [Leftover] string remindString)
            {
                var perms = ((IGuildUser) ctx.User).GetPermissions(channel);
                if (!perms.SendMessages || !perms.ViewChannel)
                {
                    await ReplyErrorLocalizedAsync("cant_read_or_send").ConfigureAwait(false);
                    return;
                }

                if (!_service.TryParseRemindMessage(remindString, out var remindData))
                {
                    await ReplyErrorLocalizedAsync("remind_invalid");
                    return;
                }


                if (!await RemindInternal(channel.Id, false, remindData.Time, remindData.What)
                    .ConfigureAwait(false))
                {
                    await ReplyErrorLocalizedAsync("remind_too_long").ConfigureAwait(false);
                }
            }

            [MewdekoCommand, Usage, Description, Aliases]
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
                            $"#{++i + (page * 10)} {rem.When:HH:mm yyyy-MM-dd} UTC (in {(int) diff.TotalHours}h {(int) diff.Minutes}m)",
                            $@"`Target:` {(rem.IsPrivate ? "DM" : "Channel")}
`TargetId:` {rem.ChannelId}
`Message:` {rem.Message?.TrimTo(50)}", false);
                    }
                }
                else
                {
                    embed.WithDescription(GetText("reminders_none"));
                }

                embed.AddPaginatedFooter(page + 1, null);
                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [MewdekoCommand, Usage, Description, Aliases]
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
                    await ReplyErrorLocalizedAsync("reminder_deleted", index + 1).ConfigureAwait(false);
                }
            }

            private async Task<bool> RemindInternal(ulong targetId, bool isPrivate, TimeSpan ts, string message)
            {
                var time = DateTime.UtcNow + ts;

                if (ts > TimeSpan.FromDays(60))
                    return false;

                if (ctx.Guild != null)
                {
                    var perms = ((IGuildUser) ctx.User).GetPermissions((IGuildChannel) ctx.Channel);
                    if (!perms.MentionEveryone)
                    {
                        message = message.SanitizeAllMentions();
                    }
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
                            Format.Bold(message),
                            $"{ts.Days}d {ts.Hours}h {ts.Minutes}min",
                            gTime, gTime)).ConfigureAwait(false);
                }
                catch
                {
                }

                return true;
            }
        }
    }
}