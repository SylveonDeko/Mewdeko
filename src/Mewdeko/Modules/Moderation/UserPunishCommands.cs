using System;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Humanizer;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Extensions.Interactive;
using Mewdeko.Common.Extensions.Interactive.Entities.Page;
using Mewdeko.Common.Extensions.Interactive.Pagination;
using Mewdeko.Common.Extensions.Interactive.Pagination.Lazy;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using Serilog;

namespace Mewdeko.Modules.Moderation;

public partial class Moderation : MewdekoModule
{
    [Group]
    public class UserPunishCommands : MewdekoSubmodule<UserPunishService>
    {
        public enum AddRole
        {
            AddRole
        }

        private readonly MuteService _mute;
        private readonly InteractiveService Interactivity;
        public DbService _db;

        public UserPunishCommands(MuteService mute, DbService db,
            InteractiveService serv)
        {
            Interactivity = serv;
            _mute = mute;
            _db = db;
        }

        private async Task<bool> CheckRoleHierarchy(IGuildUser target)
        {
            var curUser = ((SocketGuild) ctx.Guild).CurrentUser;
            var ownerId = Context.Guild.OwnerId;
            var modMaxRole = ((IGuildUser) ctx.User).GetRoles().Max(r => r.Position);
            var targetMaxRole = target.GetRoles().Max(r => r.Position);
            var botMaxRole = curUser.GetRoles().Max(r => r.Position);
            // bot can't punish a user who is higher in the hierarchy. Discord will return 403
            // moderator can be owner, in which case role hierarchy doesn't matter
            // otherwise, moderator has to have a higher role
            if (botMaxRole <= targetMaxRole || Context.User.Id != ownerId && targetMaxRole >= modMaxRole ||
                target.Id == ownerId)
            {
                await ReplyErrorLocalizedAsync("hierarchy");
                return false;
            }

            return true;
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [Priority(0)]
        public async Task SetWarnChannel([Remainder] ITextChannel channel)
        {
            if (string.IsNullOrWhiteSpace(channel.Name))
                return;

            if (WarnlogChannel == channel.Id)
            {
                await ctx.Channel.SendErrorAsync("This is already your warnlog channel!");
                return;
            }

            if (WarnlogChannel == 0)
            {
                await Service.SetWarnlogChannelId(ctx.Guild, channel);
                var WarnChannel = await ctx.Guild.GetTextChannelAsync(WarnlogChannel);
                await ctx.Channel.SendConfirmAsync("Your warnlog channel has been set to " + WarnChannel.Mention);
                return;
            }

            var oldWarnChannel = await ctx.Guild.GetTextChannelAsync(WarnlogChannel);
            await Service.SetWarnlogChannelId(ctx.Guild, channel);
            var newWarnChannel = await ctx.Guild.GetTextChannelAsync(WarnlogChannel);
            await ctx.Channel.SendConfirmAsync("Your warnlog channel has been changed from " +
                                               oldWarnChannel.Mention + " to " + newWarnChannel.Mention);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        public async Task Warn(IGuildUser user, [Remainder] string reason = null)
        {
            if (!await CheckRoleHierarchy(user))
                return;

            var dmFailed = false;
            try
            {
                await (await user.CreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription(GetText("warned_on", ctx.Guild.ToString()))
                        .AddField(efb => efb.WithName(GetText("moderator")).WithValue(ctx.User.ToString()))
                        .AddField(efb => efb.WithName(GetText("reason")).WithValue(reason ?? "-")))
                    .ConfigureAwait(false);
            }
            catch
            {
                dmFailed = true;
            }

            WarningPunishment punishment;
            try
            {
                punishment = await Service.Warn(ctx.Guild, user.Id, ctx.User, reason).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex.Message);
                var errorEmbed = new EmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(GetText("cant_apply_punishment"));

                if (dmFailed) errorEmbed.WithFooter("⚠️ " + GetText("unable_to_dm_user"));

                await ctx.Channel.EmbedAsync(errorEmbed);
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor();
            if (punishment == null)
                embed.WithDescription(GetText("user_warned",
                    Format.Bold(user.ToString())));
            else
                embed.WithDescription(GetText("user_warned_and_punished", Format.Bold(user.ToString()),
                    Format.Bold(punishment.Punishment.ToString())));

            if (dmFailed) embed.WithFooter("⚠️ " + GetText("unable_to_dm_user"));

            await ctx.Channel.EmbedAsync(embed);
            if (WarnlogChannel != 0)
            {
                var uow = _db.GetDbContext();
                var warnings = uow.Warnings
                    .ForId(ctx.Guild.Id, user.Id)
                    .Count(w => !w.Forgiven && w.UserId == user.Id);
                var condition = punishment != null;
                var punishtime = condition ? TimeSpan.FromMinutes(punishment.Time).ToString() : " ";
                var punishaction = condition ? punishment.Punishment.Humanize() : "None";
                var channel = await ctx.Guild.GetTextChannelAsync(WarnlogChannel);
                await channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithThumbnailUrl(user.RealAvatarUrl().ToString())
                    .WithTitle("Warned by: " + ctx.User)
                    .WithCurrentTimestamp()
                    .WithDescription("Username: " + user.Username + "#" + user.Discriminator + "\n" +
                                     "ID of Warned User: " + user.Id + "\n" + "Warn Number: " + warnings +
                                     "\nPunishment: " + punishaction + " " + punishtime + "\n\n" + "Reason: " +
                                     reason + "\n\n" + "[Click Here For Context]" +
                                     "(https://discord.com/channels/" + ctx.Guild.Id + "/" + ctx.Channel.Id + "/" +
                                     ctx.Message.Id + ")"));
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [MewdekoOptions(typeof(WarnExpireOptions))]
        [Priority(2)]
        public async Task WarnExpire(int days, params string[] args)
        {
            if (days < 0 || days > 366)
                return;

            var opts = OptionsParser.ParseFrom<WarnExpireOptions>(args);

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

            await Service.WarnExpireAsync(ctx.Guild.Id, days, opts.Delete).ConfigureAwait(false);
            if (days == 0)
            {
                await ReplyConfirmLocalizedAsync("warn_expire_reset").ConfigureAwait(false);
                return;
            }

            if (opts.Delete)
                await ReplyConfirmLocalizedAsync("warn_expire_set_delete", Format.Bold(days.ToString()))
                    .ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("warn_expire_set_clear", Format.Bold(days.ToString()))
                    .ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(3)]
        public Task Warnlog(IGuildUser user = null)
        {
            if (user == null)
                user = (IGuildUser) ctx.User;
            return ctx.User.Id == user.Id || ((IGuildUser) ctx.User).GuildPermissions.BanMembers
                ? Warnlog(user.Id)
                : Task.CompletedTask;
        }


        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        [Priority(1)]
        public Task Warnlog(ulong userId) => InternalWarnlog(userId);

        private async Task InternalWarnlog(ulong userId)
        {
            var warnings = Service.UserWarnings(ctx.Guild.Id, userId);
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(warnings.Length / 9)
                .WithDefaultCanceledPage()
                .WithDefaultEmotes()
                .Build();
            await Interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

            Task<PageBuilder> PageFactory(int page)
            {
                warnings = warnings.Skip(page)
                    .Take(9)
                    .ToArray();

                var embed = new PageBuilder().WithOkColor()
                    .WithTitle(GetText("warnlog_for",
                        (ctx.Guild as SocketGuild)?.GetUser(userId)?.ToString() ?? userId.ToString()))
                    .WithFooter(efb => efb.WithText(GetText("page", page + 1)));

                if (!warnings.Any())
                {
                    embed.WithDescription(GetText("warnings_none"));
                }
                else
                {
                    var i = page * 9;
                    foreach (var w in warnings)
                    {
                        i++;
                        var name = GetText("warned_on_by", w.DateAdded.Value.ToString("dd.MM.yyy"),
                            w.DateAdded.Value.ToString("HH:mm"), w.Moderator);
                        if (w.Forgiven)
                            name = Format.Strikethrough(name) + " " + GetText("warn_cleared_by", w.ForgivenBy);

                        embed.AddField(x => x
                            .WithName($"#`{i}` " + name)
                            .WithValue(w.Reason.TrimTo(1020)));
                    }
                }

                return Task.FromResult(embed);
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        public async Task WarnlogAll(int page = 1)
        {
            if (--page < 0)
                return;
            var warnings = Service.WarnlogAll(ctx.Guild.Id);

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(warnings.Length / 15)
                .WithDefaultEmotes()
                .Build();

            await Interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

            Task<PageBuilder> PageFactory(int page)
            {
                {
                    var ws = warnings.Skip(page * 15)
                        .Take(15)
                        .ToArray()
                        .Select(x =>
                        {
                            var all = x.Count();
                            var forgiven = x.Count(y => y.Forgiven);
                            var total = all - forgiven;
                            var usr = ((SocketGuild) ctx.Guild).GetUser(x.Key);
                            return (usr?.ToString() ?? x.Key.ToString()) + $" | {total} ({all} - {forgiven})";
                        });

                    return Task.FromResult(new PageBuilder().WithOkColor()
                        .WithTitle(GetText("warnings_list"))
                        .WithDescription(string.Join("\n", ws)));
                }
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        public Task Warnclear(IGuildUser user, int index = 0) => Warnclear(user.Id, index);

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        public async Task Warnclear(ulong userId, int index = 0)
        {
            if (index < 0)
                return;
            var success = await Service.WarnClearAsync(ctx.Guild.Id, userId, index, ctx.User.ToString());
            var userStr = Format.Bold((ctx.Guild as SocketGuild)?.GetUser(userId)?.ToString() ?? userId.ToString());
            if (index == 0)
            {
                await ReplyConfirmLocalizedAsync("warnings_cleared", userStr).ConfigureAwait(false);
            }
            else
            {
                if (success)
                    await ReplyConfirmLocalizedAsync("warning_cleared", Format.Bold(index.ToString()), userStr)
                        .ConfigureAwait(false);
                else
                    await ReplyErrorLocalizedAsync("warning_clear_fail").ConfigureAwait(false);
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        [Priority(1)]
        public async Task WarnPunish(int number, AddRole _, IRole role, StoopidTime time = null)
        {
            var punish = PunishmentAction.AddRole;
            var success = Service.WarnPunish(ctx.Guild.Id, number, punish, time, role);

            if (!success)
                return;

            if (time is null)
                await ReplyConfirmLocalizedAsync("warn_punish_set",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString())).ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("warn_punish_set_timed",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString()),
                    Format.Bold(time.Input)).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        public async Task WarnPunish(int number, PunishmentAction punish, StoopidTime time = null)
        {
            // this should never happen. Addrole has its own method with higher priority
            if (punish == PunishmentAction.AddRole)
                return;

            var success = Service.WarnPunish(ctx.Guild.Id, number, punish, time);

            if (!success)
                return;

            if (time is null)
                await ReplyConfirmLocalizedAsync("warn_punish_set",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString())).ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("warn_punish_set_timed",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString()),
                    Format.Bold(time.Input)).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        public async Task WarnPunish(int number)
        {
            if (!Service.WarnPunishRemove(ctx.Guild.Id, number)) return;

            await ReplyConfirmLocalizedAsync("warn_punish_rem",
                Format.Bold(number.ToString())).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task WarnPunishList()
        {
            var ps = Service.WarnPunishList(ctx.Guild.Id);

            string list;
            if (ps.Any())
                list = string.Join("\n",
                    ps.Select(x =>
                        $"{x.Count} -> {x.Punishment} {(x.Punishment == PunishmentAction.AddRole ? $"<@&{x.RoleId}>" : "")} {(x.Time <= 0 ? "" : x.Time + "m")} "));
            else
                list = GetText("warnpl_none");
            await ctx.Channel.SendConfirmAsync(
                GetText("warn_punish_list"),
                list).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        [BotPerm(GuildPermission.BanMembers)]
        [Priority(1)]
        public async Task Ban(StoopidTime time, IUser user, [Remainder] string msg = null)
        {
            if (time.Time > TimeSpan.FromDays(49))
                return;

            var guildUser =
                await ((DiscordSocketClient) Context.Client).Rest.GetGuildUserAsync(Context.Guild.Id, user.Id);

            if (guildUser != null && !await CheckRoleHierarchy(guildUser))
                return;

            var dmFailed = false;

            if (guildUser != null)
                try
                {
                    var defaultMessage = GetText("bandm", Format.Bold(ctx.Guild.Name), msg);
                    var embed = Service.GetBanUserDmEmbed(Context, guildUser, defaultMessage, msg, time.Time);
                    if (embed is not null)
                    {
                        var userChannel = await guildUser.CreateDMChannelAsync();
                        await userChannel.EmbedAsync(embed);
                    }
                }
                catch
                {
                    dmFailed = true;
                }

            await _mute.TimedBan(Context.Guild, user, time.Time, $"{ctx.User} | {msg}").ConfigureAwait(false);
            var toSend = new EmbedBuilder().WithOkColor()
                .WithTitle("⛔️ " + GetText("banned_user"))
                .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
                .AddField(efb =>
                    efb.WithName(GetText("duration"))
                        .WithValue($"{time.Time.Days}d {time.Time.Hours}h {time.Time.Minutes}m")
                        .WithIsInline(true));

            if (dmFailed) toSend.WithFooter("⚠️ " + GetText("unable_to_dm_user"));

            await ctx.Channel.EmbedAsync(toSend)
                .ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        [BotPerm(GuildPermission.BanMembers)]
        [Priority(0)]
        public async Task Ban(ulong userId, [Remainder] string msg = null)
        {
            var user = await ((DiscordSocketClient) Context.Client).Rest.GetGuildUserAsync(Context.Guild.Id,
                userId);
            if (user is null)
            {
                await ctx.Guild.AddBanAsync(userId, 7, ctx.User + " | " + msg);

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle("⛔️ " + GetText("banned_user"))
                        .AddField(efb => efb.WithName("ID").WithValue(userId.ToString()).WithIsInline(true)))
                    .ConfigureAwait(false);
            }
            else
            {
                await Ban(user, msg);
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        [BotPerm(GuildPermission.BanMembers)]
        [Priority(2)]
        public async Task Ban(IGuildUser user, [Remainder] string msg = null)
        {
            if (!await CheckRoleHierarchy(user))
                return;

            var dmFailed = false;

            try
            {
                var defaultMessage = GetText("bandm", Format.Bold(ctx.Guild.Name), msg);
                var embed = Service.GetBanUserDmEmbed(Context, user, defaultMessage, msg, null);
                if (embed is not null)
                {
                    var userChannel = await user.CreateDMChannelAsync();
                    await userChannel.EmbedAsync(embed);
                }
            }
            catch
            {
                dmFailed = true;
            }

            await ctx.Guild.AddBanAsync(user, 7, ctx.User + " | " + msg).ConfigureAwait(false);

            var toSend = new EmbedBuilder().WithOkColor()
                .WithTitle("⛔️ " + GetText("banned_user"))
                .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true));

            if (dmFailed) toSend.WithFooter("⚠️ " + GetText("unable_to_dm_user"));

            await ctx.Channel.EmbedAsync(toSend)
                .ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        [BotPerm(GuildPermission.BanMembers)]
        public async Task BanMessage([Remainder] string message = null)
        {
            if (message is null)
            {
                var template = Service.GetBanTemplate(Context.Guild.Id);
                if (template is null)
                {
                    await ReplyConfirmLocalizedAsync("banmsg_default");
                    return;
                }

                await Context.Channel.SendConfirmAsync(template);
                return;
            }

            Service.SetBanTemplate(Context.Guild.Id, message);
            await ctx.OkAsync();
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        [BotPerm(GuildPermission.BanMembers)]
        public async Task BanMsgReset()
        {
            Service.SetBanTemplate(Context.Guild.Id, null);
            await ctx.OkAsync();
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        [BotPerm(GuildPermission.BanMembers)]
        [Priority(0)]
        public Task BanMessageTest([Remainder] string reason = null) => InternalBanMessageTest(reason, null);

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        [BotPerm(GuildPermission.BanMembers)]
        [Priority(1)]
        public Task BanMessageTest(StoopidTime duration, [Remainder] string reason = null) => InternalBanMessageTest(reason, duration.Time);

        private async Task InternalBanMessageTest(string reason, TimeSpan? duration)
        {
            var dmChannel = await ctx.User.CreateDMChannelAsync();
            var defaultMessage = GetText("bandm", Format.Bold(ctx.Guild.Name), reason);
            var crEmbed = Service.GetBanUserDmEmbed(Context,
                (IGuildUser) Context.User,
                defaultMessage,
                reason,
                duration);

            if (crEmbed is null)
            {
                await ConfirmLocalizedAsync("bandm_disabled");
            }
            else
            {
                try
                {
                    await dmChannel.EmbedAsync(crEmbed);
                }
                catch (Exception)
                {
                    await ReplyErrorLocalizedAsync("unable_to_dm_user");
                    return;
                }

                await Context.OkAsync();
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        [BotPerm(GuildPermission.BanMembers)]
        public async Task Unban([Remainder] string user)
        {
            var bans = await ctx.Guild.GetBansAsync().ConfigureAwait(false);

            var bun = bans.FirstOrDefault(x => x.User.ToString().ToLowerInvariant() == user.ToLowerInvariant());

            if (bun == null)
            {
                await ReplyErrorLocalizedAsync("user_not_found").ConfigureAwait(false);
                return;
            }

            await UnbanInternal(bun.User).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        [BotPerm(GuildPermission.BanMembers)]
        public async Task Unban(ulong userId)
        {
            var bans = await ctx.Guild.GetBansAsync().ConfigureAwait(false);

            var bun = bans.FirstOrDefault(x => x.User.Id == userId);

            if (bun == null)
            {
                await ReplyErrorLocalizedAsync("user_not_found").ConfigureAwait(false);
                return;
            }

            await UnbanInternal(bun.User).ConfigureAwait(false);
        }

        private async Task UnbanInternal(IUser user)
        {
            await ctx.Guild.RemoveBanAsync(user).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("unbanned_user", Format.Bold(user.ToString())).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.KickMembers | GuildPermission.ManageMessages)]
        [BotPerm(GuildPermission.BanMembers)]
        public Task Softban(IGuildUser user, [Remainder] string msg = null) => SoftbanInternal(user, msg);

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.KickMembers | GuildPermission.ManageMessages)]
        [BotPerm(GuildPermission.BanMembers)]
        public async Task Softban(ulong userId, [Remainder] string msg = null)
        {
            var user = await ((DiscordSocketClient) Context.Client).Rest.GetGuildUserAsync(Context.Guild.Id,
                userId);
            if (user is null)
                return;

            await SoftbanInternal(user);
        }

        private async Task SoftbanInternal(IGuildUser user, [Remainder] string msg = null)
        {
            if (!await CheckRoleHierarchy(user))
                return;

            var dmFailed = false;

            try
            {
                await user.SendErrorAsync(GetText("sbdm", Format.Bold(ctx.Guild.Name), msg)).ConfigureAwait(false);
            }
            catch
            {
                dmFailed = true;
            }

            await ctx.Guild.AddBanAsync(user, 7, "Softban | " + ctx.User + " | " + msg).ConfigureAwait(false);
            try
            {
                await ctx.Guild.RemoveBanAsync(user).ConfigureAwait(false);
            }
            catch
            {
                await ctx.Guild.RemoveBanAsync(user).ConfigureAwait(false);
            }

            var toSend = new EmbedBuilder().WithOkColor()
                .WithTitle("☣ " + GetText("sb_user"))
                .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true));

            if (dmFailed) toSend.WithFooter("⚠️ " + GetText("unable_to_dm_user"));

            await ctx.Channel.EmbedAsync(toSend)
                .ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.KickMembers)]
        [BotPerm(GuildPermission.KickMembers)]
        [Priority(1)]
        public Task Kick(IGuildUser user, [Remainder] string msg = null) => KickInternal(user, msg);

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.KickMembers)]
        [BotPerm(GuildPermission.KickMembers)]
        [Priority(0)]
        public async Task Kick(ulong userId, [Remainder] string msg = null)
        {
            var user = await ((DiscordSocketClient) Context.Client).Rest.GetGuildUserAsync(Context.Guild.Id,
                userId);
            if (user is null)
                return;

            await KickInternal(user, msg);
        }

        public async Task KickInternal(IGuildUser user, string msg = null)
        {
            if (!await CheckRoleHierarchy(user))
                return;

            var dmFailed = false;

            try
            {
                await user.SendErrorAsync(GetText("kickdm", Format.Bold(ctx.Guild.Name), msg))
                    .ConfigureAwait(false);
            }
            catch
            {
                dmFailed = true;
            }

            await user.KickAsync(ctx.User + " | " + msg).ConfigureAwait(false);

            var toSend = new EmbedBuilder().WithOkColor()
                .WithTitle(GetText("kicked_user"))
                .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true));

            if (dmFailed) toSend.WithFooter("⚠️ " + GetText("unable_to_dm_user"));

            await ctx.Channel.EmbedAsync(toSend)
                .ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        [BotPerm(GuildPermission.BanMembers)]
        [OwnerOnly]
        public async Task MassKill([Remainder] string people)
        {
            if (string.IsNullOrWhiteSpace(people))
                return;

            var (bans, missing) = Service.MassKill((SocketGuild) ctx.Guild, people);

            var missStr = string.Join("\n", missing);
            if (string.IsNullOrWhiteSpace(missStr))
                missStr = "-";

            //send a message but don't wait for it
            var banningMessageTask = ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithDescription(GetText("mass_kill_in_progress", bans.Count()))
                .AddField(GetText("invalid", missing), missStr)
                .WithOkColor());

            //do the banning
            await Task.WhenAll(bans
                    .Where(x => x.Id.HasValue)
                    .Select(x => ctx.Guild.AddBanAsync(x.Id.Value, 7, x.Reason, new RequestOptions
                    {
                        RetryMode = RetryMode.AlwaysRetry
                    })))
                .ConfigureAwait(false);

            //wait for the message and edit it
            var banningMessage = await banningMessageTask.ConfigureAwait(false);

            await banningMessage.ModifyAsync(x => x.Embed = new EmbedBuilder()
                .WithDescription(GetText("mass_kill_completed", bans.Count()))
                .AddField(GetText("invalid", missing), missStr)
                .WithOkColor()
                .Build()).ConfigureAwait(false);
        }

        public class WarnExpireOptions : IMewdekoCommandOptions
        {
            [Option('d', "delete", Default = false, HelpText = "Delete warnings instead of clearing them.")]
            public bool Delete { get; set; } = false;

            public void NormalizeOptions()
            {
            }
        }
    }
}