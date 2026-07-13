using DataModel;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Common;
using Mewdeko.Modules.Moderation.Services;
using Swan;

namespace Mewdeko.Modules.Moderation;

public partial class Moderation
{
    /// <summary>
    ///     Module for managing mini warnings.
    /// </summary>
    /// <param name="dbFactory">The db provider</param>
    /// <param name="serv">Fergun.Interactive paginator builder</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    [Group]
    public class UserPunishCommands2(
        IDataConnectionFactory dbFactory,
        InteractiveService serv,
        ILogger<UserPunishCommands2> logger)
        : MewdekoSubmodule<UserPunishService2>
    {
        /// <summary>
        ///     The addrole thing used for punishments
        /// </summary>
        public enum AddRole
        {
            /// <summary>
            ///     Add a role
            /// </summary>
            AddRole
        }

        /// <summary>
        ///     Sets the mini warnlog channel.
        /// </summary>
        /// <param name="channel">The channel to set</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(0)]
        public async Task SetMWarnChannel([Remainder] ITextChannel channel)
        {
            if (string.IsNullOrWhiteSpace(channel.Name))
                return;
            var mWarnlogChannel = await Service.GetMWarnlogChannel(ctx.Guild.Id);
            if (mWarnlogChannel == channel.Id)
            {
                await ctx.Channel.SendErrorAsync(
                    Strings.MiniWarnlogChannelAlreadySet(ctx.Guild.Id),
                    Config
                );
                return;
            }

            if (mWarnlogChannel == 0)
            {
                await Service.SetMWarnlogChannelId(ctx.Guild, channel).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync(
                    Strings.MiniWarnlogChannelSet(ctx.Guild.Id, channel.Mention)
                );
                return;
            }

            var oldWarnChannel = await ctx.Guild.GetTextChannelAsync(mWarnlogChannel).ConfigureAwait(false);
            await Service.SetMWarnlogChannelId(ctx.Guild, channel).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                Strings.MiniWarnlogChannelChanged(ctx.Guild.Id, oldWarnChannel?.Mention ?? mWarnlogChannel.ToString(),
                    channel.Mention)
            );
        }

        /// <summary>
        ///     Mini Warns a user.
        /// </summary>
        /// <param name="user">The user to warn</param>
        /// <param name="reason">The reason for the warning</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.MuteMembers)]
        public async Task MWarn(IGuildUser user, [Remainder] string? reason = null)
        {
            if (ctx.User.Id != user.Guild.OwnerId
                && user.GetRoles().Select(r => r.Position).Max() >=
                ((IGuildUser)ctx.User).GetRoles().Select(r => r.Position).Max())
            {
                await ReplyErrorAsync(Strings.Hierarchy(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            try
            {
                await (await user.CreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(new EmbedBuilder()
                        .WithDescription(Strings.MiniWarnedInGuild(ctx.Guild.Id, ctx.Guild))
                        .AddField(efb => efb.WithName(Strings.MiniWarnModerator(ctx.Guild.Id))
                            .WithValue(ctx.User.ToString()))
                        .AddField(efb => efb.WithName(Strings.MiniWarnReason(ctx.Guild.Id))
                            .WithValue(reason ?? "-")))
                    .ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            WarningPunishment2 punishment;
            try
            {
                punishment = await Service.Warn(ctx.Guild, user.Id, ctx.User, reason).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex.Message);
                await ReplyErrorAsync(Strings.CantApplyPunishment(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(
                punishment == null
                    ? Strings.UserMiniWarned(ctx.Guild.Id, Format.Bold(user.ToString()))
                    : Strings.UserMiniWarnedAndPunished(ctx.Guild.Id, Format.Bold(user.ToString()),
                        Format.Bold(punishment.Punishment.ToString())));

            if (await Service.GetMWarnlogChannel(ctx.Guild.Id) != 0)
            {
                await using var dbContext = await dbFactory.CreateConnectionAsync();

                var warnings = await dbContext.Warnings2s
                    .Where(x => x.UserId == user.Id && x.GuildId == ctx.Guild.Id)
                    .CountAsync(w => !w.Forgiven && w.UserId == user.Id);
                var condition = punishment != null;
                var punishtime = condition ? TimeSpan.FromMinutes(punishment.Time).Humanize() : " ";
                var punishaction = condition ? punishment.Punishment.ToString() : "None";
                var channel = await ctx.Guild.GetTextChannelAsync(await Service.GetMWarnlogChannel(ctx.Guild.Id))
                    .ConfigureAwait(false);
                await channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithThumbnailUrl(user.RealAvatarUrl().ToString())
                    .WithTitle(Strings.MiniWarnLogTitle(ctx.Guild.Id, ctx.User))
                    .WithDescription(Strings.MiniWarnLogDescription(
                        ctx.Guild.Id,
                        user.Username,
                        user.Discriminator,
                        user.Id,
                        warnings,
                        punishaction,
                        punishtime,
                        reason,
                        $"https://discord.com/channels/{ctx.Guild.Id}/{ctx.Channel.Id}/{ctx.Message.Id}"
                    )));
            }
        }

        /// <summary>
        ///     Sets the mini warn expire time.
        /// </summary>
        /// <param name="days">The number of days to set</param>
        /// <param name="action">The action to take when a warn expires</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(2)]
        public async Task MWarnExpire(int days, WarnExpireAction action = WarnExpireAction.Clear)
        {
            if (days is < 0 or > 366)
                return;

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

            await Service.WarnExpireAsync(ctx.Guild.Id, days, action).ConfigureAwait(false);
            await ReplyConfirmAsync(
                days == 0
                    ? Strings.MiniWarnExpireReset(ctx.Guild.Id)
                    : action == WarnExpireAction.Delete
                        ? Strings.MiniWarnExpireSetDelete(ctx.Guild.Id, Format.Bold(days.ToString()))
                        : Strings.MiniWarnExpireSetClear(ctx.Guild.Id, Format.Bold(days.ToString()))
            );
        }

        /// <summary>
        ///     Gets the mini warnlog for a user.
        /// </summary>
        /// <param name="page">The page number</param>
        /// <param name="user">The user to get the warnlog for</param>
        /// <returns></returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.MuteMembers)]
        [Priority(2)]
        public Task MWarnlog(int page, IGuildUser user)
        {
            return MWarnlog(page, user.Id);
        }

        /// <summary>
        ///     Gets the mini warnlog for a user.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(3)]
        public Task MWarnlog(IGuildUser? user = null)
        {
            if (user == null)
                user = (IGuildUser)ctx.User;
            return ctx.User.Id == user.Id || ((IGuildUser)ctx.User).GuildPermissions.MuteMembers
                ? MWarnlog(user.Id)
                : Task.CompletedTask;
        }

        /// <summary>
        ///     Gets the mini warnlog for a user.
        /// </summary>
        /// <param name="page">The page number</param>
        /// <param name="userId">The user id to get the warnlog for</param>
        /// <returns></returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.MuteMembers)]
        [Priority(0)]
        public Task MWarnlog(int page, ulong userId)
        {
            return InternalWarnlog(userId, page - 1);
        }

        /// <summary>
        ///     Gets the mini warnlog for a user.
        /// </summary>
        /// <param name="userId">The user id to get the warnlog for</param>
        /// <returns></returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.MuteMembers)]
        [Priority(1)]
        public Task MWarnlog(ulong userId)
        {
            return InternalWarnlog(userId, 0);
        }

        private async Task InternalWarnlog(ulong userId, int page)
        {
            if (page < 0)
                return;
            var warnings = await Service.UserWarnings(ctx.Guild.Id, userId);

            warnings = warnings.Skip(page * 9)
                .Take(9)
                .ToArray();

            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.MiniWarnlogFor(ctx.Guild.Id,
                    (ctx.Guild as SocketGuild)?.GetUser(userId)?.ToString() ?? userId.ToString()))
                .WithFooter(efb => efb.WithText(Strings.MiniWarnlogPage(ctx.Guild.Id, page + 1)));

            if (warnings.Length == 0)
            {
                embed.WithDescription(Strings.MiniWarningsNone(ctx.Guild.Id));
            }
            else
            {
                var i = page * 9;
                foreach (var w in warnings)
                {
                    i++;
                    var name = Strings.MiniWarnedOnBy(
                        ctx.Guild.Id,
                        $"<t:{w.DateAdded.Value.ToUnixEpochDate()}:D>",
                        $"<t:{w.DateAdded.Value.ToUnixEpochDate()}:T>",
                        w.Moderator
                    );
                    if (w.Forgiven)
                        name = $"{Format.Strikethrough(name)} {Strings.MiniWarnClearedBy(ctx.Guild.Id, w.ForgivenBy)}";

                    embed.AddField(x => x
                        .WithName($"#`{i}` {name}")
                        .WithValue(w.Reason.TrimTo(1020)));
                }
            }

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Gets the mini warnlog for all users.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.MuteMembers)]
        public async Task MWarnlogAll()
        {
            var warnings = await Service.WarnlogAll(ctx.Guild.Id);

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(warnings.Length / 15)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                {
                    var ws = warnings.Skip(page * 15)
                        .Take(15)
                        .ToArray()
                        .Select(x =>
                        {
                            var all = x.Count();
                            var forgiven = x.Count(y => y.Forgiven);
                            var total = all - forgiven;
                            var usr = ((SocketGuild)ctx.Guild).GetUser(x.Key);
                            return $"{usr?.ToString() ?? x.Key.ToString()} | {total} ({all} - {forgiven})";
                        });

                    return new PageBuilder().WithOkColor()
                        .WithTitle(Strings.MiniWarningsList(ctx.Guild.Id))
                        .WithDescription(string.Join("\n", ws));
                }
            }
        }

        /// <summary>
        ///     Clears a user's mini warnings. If index is specified, clears only that warning.
        /// </summary>
        /// <param name="user">The user to clear the warnings for</param>
        /// <param name="index">The index of the warning to clear</param>
        /// <returns></returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task MWarnclear(IGuildUser user, int index = 0)
        {
            return MWarnclear(user.Id, index);
        }

        /// <summary>
        ///     Clears a user's mini warnings. If index is specified, clears only that warning.
        /// </summary>
        /// <param name="userId">The user id to clear the warnings for</param>
        /// <param name="index">The index of the warning to clear</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task MWarnclear(ulong userId, int index = 0)
        {
            if (index < 0)
                return;
            var success = await Service.WarnClearAsync(ctx.Guild.Id, userId, index, ctx.User.ToString())
                .ConfigureAwait(false);
            var userStr = Format.Bold((ctx.Guild as SocketGuild)?.GetUser(userId)?.ToString() ?? userId.ToString());
            await ReplyConfirmAsync(
                index == 0
                    ? Strings.MiniWarningsCleared(ctx.Guild.Id, userStr)
                    : success
                        ? Strings.MiniWarningCleared(ctx.Guild.Id, Format.Bold(index.ToString()), userStr)
                        : Strings.MiniWarningClearFail(ctx.Guild.Id)
            );
        }

        /// <summary>
        ///     Sets the mini warn punishment.
        /// </summary>
        /// <param name="number">The number of warnings</param>
        /// <param name="_">The addrole thing</param>
        /// <param name="role">The role to add (used only if addrole is specified)</param>
        /// <param name="time">The time to do the punishment for</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(1)]
        public async Task MWarnPunish(int number, AddRole _, IRole role, StoopidTime? time = null)
        {
            const PunishmentAction punish = PunishmentAction.AddRole;
            var success = await Service.WarnPunish(ctx.Guild.Id, number, (int)punish, time, role);

            if (!success)
                return;

            await ReplyConfirmAsync(
                time is null
                    ? Strings.MiniWarnPunishSet(ctx.Guild.Id, Format.Bold(punish.ToString()),
                        Format.Bold(number.ToString()))
                    : Strings.MiniWarnPunishSetTimed(ctx.Guild.Id, Format.Bold(punish.ToString()),
                        Format.Bold(number.ToString()), Format.Bold(time.Input))
            );
        }

        /// <summary>
        ///     Sets the mini warn punishment.
        /// </summary>
        /// <param name="number">The number of warnings</param>
        /// <param name="punish">The punishment to set</param>
        /// <param name="time">The time to do the punishment for</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task MWarnPunish(int number, PunishmentAction punish, StoopidTime? time = null)
        {
            // this should never happen. Addrole has its own method with higher priority
            if (punish == PunishmentAction.AddRole)
                return;

            var success = await Service.WarnPunish(ctx.Guild.Id, number, (int)punish, time);

            if (!success)
                return;

            if (time is null)
            {
                await ReplyConfirmAsync(Strings.WarnPunishSet(ctx.Guild.Id,
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString()))).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmAsync(Strings.WarnPunishSetTimed(ctx.Guild.Id,
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString()),
                    Format.Bold(time.Input))).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes a mini warn punishment.
        /// </summary>
        /// <param name="number">The number of warnings</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task MWarnPunish(int number)
        {
            if (!await Service.WarnPunishRemove(ctx.Guild.Id, number)) return;

            await ReplyConfirmAsync(
                Strings.MiniWarnPunishRemoved(ctx.Guild.Id, Format.Bold(number.ToString()))
            );
        }

        /// <summary>
        ///     Lists mini warn punishments.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task MWarnPunishList()
        {
            var ps = await Service.WarnPunishList(ctx.Guild.Id);

            await ctx.Channel.SendConfirmAsync(
                Strings.MiniWarnPunishListTitle(ctx.Guild.Id),
                ps.Length > 0
                    ? string.Join("\n", ps.Select(x =>
                        $"{x.Count} -> {x.Punishment} {(x.Punishment == (int)PunishmentAction.AddRole ? $"<@&{x.RoleId}>" : "")} {(x.Time <= 0 ? "" : $"{x.Time}m")} "))
                    : Strings.MiniWarnPunishListNone(ctx.Guild.Id)
            );
        }
    }
}