using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Provides commands for managing reminders.
    /// </summary>
    [Group]
    public class RemindCommands(InteractiveService interactivity) : MewdekoSubmodule<RemindService>
    {
        /// <summary>
        ///     Determines whether the reminder should be sent to the user directly or to the channel.
        /// </summary>
        public enum MeOrHere
        {
            /// <summary>
            ///     Sends the reminder to the user directly.
            /// </summary>
            Me,

            /// <summary>
            ///     Sends the reminder to the channel.
            /// </summary>
            Here
        }

        /// <summary>
        ///     Creates a reminder for the user or the current channel.
        /// </summary>
        /// <param name="meorhere">Specifies whether to send the reminder to the user or the channel.</param>
        /// <param name="remindString">The reminder message and timing information. Add every and an interval after the time to repeat it, such as 1h every 1d to drink water.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [Priority(1)]
        public async Task Remind(MeOrHere meorhere, [Remainder] string remindString)
        {
            if (!Service.TryParseRemindMessage(remindString, out var remindData))
            {
                await ReplyErrorAsync(Strings.RemindInvalidFormat(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var target = meorhere == MeOrHere.Me ? ctx.User.Id : ctx.Channel.Id;
            var isPrivate = meorhere == MeOrHere.Me || ctx.Guild == null;
            var shouldSanitize = ctx.Guild != null &&
                                 !((IGuildUser)ctx.User).GetPermissions((IGuildChannel)ctx.Channel).MentionEveryone;

            var (success, message, id) = await Service.CreateReminderAsync(
                target,
                isPrivate,
                remindData.Time,
                remindData.What,
                ctx.User.Id,
                ctx.Guild?.Id,
                shouldSanitize,
                remindData.Repeat
            );

            if (success)
                await ctx.Channel
                    .SendConfirmAsync(message, isPrivate ? null : Service.SubscribeButton(id, ctx.Guild?.Id ?? 0))
                    .ConfigureAwait(false);
            else
                await ReplyErrorAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        ///     Creates a reminder for a specific text channel.
        /// </summary>
        /// <param name="channel">The text channel to send the reminder to.</param>
        /// <param name="remindString">The reminder message and timing information. Add every and an interval after the time to repeat it, such as 1h every 1d to drink water.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [Priority(0)]
        public async Task Remind(ITextChannel channel, [Remainder] string remindString)
        {
            var perms = ((IGuildUser)ctx.User).GetPermissions(channel);
            if (!perms.SendMessages || !perms.ViewChannel)
            {
                await ReplyErrorAsync(Strings.CantReadOrSend(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (!Service.TryParseRemindMessage(remindString, out var remindData))
            {
                await ReplyErrorAsync(Strings.RemindInvalidFormat(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var shouldSanitize = !perms.MentionEveryone;
            var (success, message, id) = await Service.CreateReminderAsync(
                channel.Id,
                false,
                remindData.Time,
                remindData.What,
                ctx.User.Id,
                ctx.Guild.Id,
                shouldSanitize,
                remindData.Repeat
            );

            if (success)
                await ctx.Channel.SendConfirmAsync(message, Service.SubscribeButton(id, ctx.Guild.Id))
                    .ConfigureAwait(false);
            else
                await ReplyErrorAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        ///     Makes one of your reminders repeat on an interval, or stops it repeating.
        /// </summary>
        /// <param name="index">The number of the reminder in your reminder list.</param>
        /// <param name="interval">How often to repeat, at least one hour. Use off to stop repeating.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public async Task RemindRepeat(int index, string interval)
        {
            var stop = interval.Equals("off", StringComparison.OrdinalIgnoreCase);
            var time = TimeSpan.Zero;
            if (!stop)
            {
                try
                {
                    time = StoopidTime.FromInput(interval).Time;
                }
                catch (ArgumentException)
                {
                }

                if (time < RemindService.MinRepeat || time > TimeSpan.FromDays(60))
                {
                    await ReplyErrorAsync(Strings.RemindRepeatRange(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }
            }

            if (!await Service.SetRepeatAsync(ctx.User.Id, index - 1, stop ? null : time))
            {
                await ReplyErrorAsync(Strings.ReminderNotExist(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(stop
                ? Strings.RemindRepeatCleared(ctx.Guild.Id, index)
                : Strings.RemindRepeatSet(ctx.Guild.Id, index, time.Humanize())).ConfigureAwait(false);
        }

        /// <summary>
        ///     Subscribes you to someone's reminder so you get pinged with it.
        /// </summary>
        /// <param name="id">The reminder id shown in the reminder list.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public async Task RemindSubscribe(int id)
        {
            var result = await Service.SubscribeAsync(ctx.User.Id, id);
            await ReplyConfirmAsync(SubscribeText(result)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes your subscription from a reminder.
        /// </summary>
        /// <param name="id">The reminder id shown in the reminder list.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public async Task RemindUnsubscribe(int id)
        {
            var removed = await Service.UnsubscribeAsync(ctx.User.Id, id);
            await ReplyConfirmAsync(removed
                ? Strings.RemindUnsubscribed(ctx.Guild.Id)
                : Strings.RemindNotSubscribed(ctx.Guild.Id)).ConfigureAwait(false);
        }

        private string SubscribeText(RemindService.SubscribeResult result)
        {
            return result switch
            {
                RemindService.SubscribeResult.Subscribed => Strings.RemindSubscribed(ctx.Guild.Id),
                RemindService.SubscribeResult.AlreadySubscribed => Strings.RemindAlreadySubscribed(ctx.Guild.Id),
                RemindService.SubscribeResult.Owner => Strings.RemindOwnReminder(ctx.Guild.Id),
                _ => Strings.ReminderNotExist(ctx.Guild.Id)
            };
        }

        /// <summary>
        ///     Lists all reminders for the current user.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public async Task RemindList()
        {
            var reminders = await Service.GetUserRemindersAsync(ctx.User.Id);

            if (reminders.Count == 0)
            {
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

            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);
            return;

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
        /// <param name="index">The index of the reminder to delete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public async Task RemindDelete(int index)
        {
            if (--index < 0)
                return;

            var deleted = await Service.DeleteReminderAsync(ctx.User.Id, index);

            if (!deleted)
                await ReplyErrorAsync(Strings.ReminderNotExist(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.ReminderDeleted(ctx.Guild.Id, index + 1)).ConfigureAwait(false);
        }
    }
}