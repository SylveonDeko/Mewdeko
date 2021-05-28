using System;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Common;
using Mewdeko.Core.Common.TypeReaders.Models;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class UserPunishCommands : MewdekoSubmodule<UserPunishService>
        {
            public enum AddRole
            {
                AddRole
            }

            private readonly MuteService _mute;
            public DbService _db;

            public UserPunishCommands(MuteService mute, DiscordSocketClient client, DbService db)
            {
                _mute = mute;
                Client = client;
                _db = db;
            }

            public DiscordSocketClient Client { get; }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.MuteMembers)]
            public async Task STFU(IUser user)
            {
                var channel = ctx.Channel as SocketGuildChannel;
                var currentPerms = channel.GetPermissionOverwrite(user) ?? new OverwritePermissions();
                await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(sendMessages: PermValue.Deny));
                await ctx.Channel.SendConfirmAsync($"{user} has been muted in this channel!");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.MuteMembers)]
            public async Task UNSTFU(IUser user)
            {
                var channel = ctx.Channel as SocketGuildChannel;
                var currentPerms = channel.GetPermissionOverwrite(user) ?? new OverwritePermissions();
                await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(sendMessages: PermValue.Inherit));
                await ctx.Channel.SendConfirmAsync($"{user} has been unmuted in this channel!");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
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
                    await _service.SetWarnlogChannelId(ctx.Guild, channel);
                    var WarnChannel = await ctx.Guild.GetTextChannelAsync(WarnlogChannel);
                    await ctx.Channel.SendConfirmAsync("Your warnlog channel has been set to " + WarnChannel.Mention);
                    return;
                }

                var oldWarnChannel = await ctx.Guild.GetTextChannelAsync(WarnlogChannel);
                await _service.SetWarnlogChannelId(ctx.Guild, channel);
                var newWarnChannel = await ctx.Guild.GetTextChannelAsync(WarnlogChannel);
                await ctx.Channel.SendConfirmAsync("Your warnlog channel has been changed from " +
                                                   oldWarnChannel.Mention + " to " + newWarnChannel.Mention);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.MuteMembers)]
            public async Task Warn(IGuildUser user, [Remainder] string reason = null)
            {
                if (ctx.User.Id != user.Guild.OwnerId
                    && user.GetRoles().Select(r => r.Position).Max() >=
                    ((IGuildUser) ctx.User).GetRoles().Select(r => r.Position).Max())
                {
                    await ReplyErrorLocalizedAsync("hierarchy").ConfigureAwait(false);
                    return;
                }

                try
                {
                    await (await user.CreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(new EmbedBuilder()
                            .WithErrorColor()
                            .WithDescription("Warned in " + ctx.Guild)
                            .AddField(efb => efb.WithName(GetText("moderator")).WithValue(ctx.User.ToString()))
                            .AddField(efb => efb.WithName(GetText("reason")).WithValue(reason ?? "-")))
                        .ConfigureAwait(false);
                }
                catch
                {
                }

                WarningPunishment punishment;
                try
                {
                    punishment = await _service.Warn(ctx.Guild, user.Id, ctx.User, reason).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.Warn(ex.Message);
                    await ReplyErrorLocalizedAsync("cant_apply_punishment").ConfigureAwait(false);
                    return;
                }

                if (punishment == null)
                    await ReplyConfirmLocalizedAsync("user_warned", Format.Bold(user.ToString())).ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("user_warned_and_punished", Format.Bold(user.ToString()),
                        Format.Bold(punishment.Punishment.ToString())).ConfigureAwait(false);
                if (WarnlogChannel != 0)
                {
                    var uow = _db.GetDbContext();
                    var warnings = uow.Warnings
                        .ForId(ctx.Guild.Id, user.Id)
                        .Count(w => !w.Forgiven && w.UserId == user.Id);
                    var condition = punishment != null;
                    var punishtime = condition ? TimeSpan.FromMinutes(punishment.Time).ToString() : " ";
                    var punishaction = condition ? punishment.Punishment.ToString() : "None";
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
            [UserPerm(GuildPerm.Administrator)]
            [MewdekoOptions(typeof(WarnExpireOptions))]
            [Priority(2)]
            public async Task WarnExpire(int days, params string[] args)
            {
                if (days < 0 || days > 366)
                    return;

                var opts = OptionsParser.ParseFrom<WarnExpireOptions>(args);

                await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

                await _service.WarnExpireAsync(ctx.Guild.Id, days, opts.Delete).ConfigureAwait(false);
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
            [UserPerm(GuildPerm.MuteMembers)]
            [Priority(2)]
            public Task Warnlog(int page, IGuildUser user)
            {
                return Warnlog(page, user.Id);
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
                return ctx.User.Id == user.Id || ((IGuildUser) ctx.User).GuildPermissions.MuteMembers
                    ? Warnlog(user.Id)
                    : Task.CompletedTask;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.MuteMembers)]
            [Priority(0)]
            public Task Warnlog(int page, ulong userId)
            {
                return InternalWarnlog(userId, page - 1);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.MuteMembers)]
            [Priority(1)]
            public Task Warnlog(ulong userId)
            {
                return InternalWarnlog(userId, 0);
            }

            private async Task InternalWarnlog(ulong userId, int page)
            {
                if (page < 0)
                    return;
                var warnings = _service.UserWarnings(ctx.Guild.Id, userId);

                warnings = warnings.Skip(page * 9)
                    .Take(9)
                    .ToArray();

                var embed = new EmbedBuilder().WithOkColor()
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

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.MuteMembers)]
            public async Task WarnlogAll(int page = 1)
            {
                if (--page < 0)
                    return;
                var warnings = _service.WarnlogAll(ctx.Guild.Id);

                await ctx.SendPaginatedConfirmAsync(page, curPage =>
                {
                    var ws = warnings.Skip(curPage * 15)
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

                    return new EmbedBuilder().WithOkColor()
                        .WithTitle(GetText("warnings_list"))
                        .WithDescription(string.Join("\n", ws));
                }, warnings.Length, 15).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public Task Warnclear(IGuildUser user, int index = 0)
            {
                return Warnclear(user.Id, index);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public async Task Warnclear(ulong userId, int index = 0)
            {
                if (index < 0)
                    return;
                var success = await _service.WarnClearAsync(ctx.Guild.Id, userId, index, ctx.User.ToString());
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
            [UserPerm(GuildPerm.Administrator)]
            [Priority(1)]
            public async Task WarnPunish(int number, AddRole _, IRole role, StoopidTime time = null)
            {
                var punish = PunishmentAction.AddRole;
                var success = _service.WarnPunish(ctx.Guild.Id, number, punish, time, role);

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
            [UserPerm(GuildPerm.Administrator)]
            public async Task WarnPunish(int number, PunishmentAction punish, StoopidTime time = null)
            {
                // this should never happen. Addrole has its own method with higher priority
                if (punish == PunishmentAction.AddRole)
                    return;

                var success = _service.WarnPunish(ctx.Guild.Id, number, punish, time);

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
            [UserPerm(GuildPerm.Administrator)]
            public async Task WarnPunish(int number)
            {
                if (!_service.WarnPunishRemove(ctx.Guild.Id, number)) return;

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
                var ps = _service.WarnPunishList(ctx.Guild.Id);

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
            [UserPerm(GuildPerm.BanMembers)]
            [BotPerm(GuildPerm.BanMembers)]
            [Priority(0)]
            public async Task Ban(StoopidTime time, IGuildUser user, [Remainder] string msg = null)
            {
                if (time.Time > TimeSpan.FromDays(0))
                    return;
                if (ctx.User.Id != user.Guild.OwnerId && user.GetRoles().Select(r => r.Position).Max() >=
                    ((IGuildUser) ctx.User).GetRoles().Select(r => r.Position).Max())
                {
                    await ReplyErrorLocalizedAsync("hierarchy").ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(msg))
                    try
                    {
                        await user.SendErrorAsync(GetText("bandm", Format.Bold(ctx.Guild.Name), msg))
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }

                await _mute.TimedBan(user, time.Time, ctx.User + " | " + msg).ConfigureAwait(false);
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle("⛔️ " + GetText("banned_user"))
                        .AddField(
                            efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                        .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
                        .WithImageUrl(
                            "https://cdn.discordapp.com/attachments/732021633719730236/769426176099614720/IMG_20201024_010137.jpg")
                        .WithFooter($"{time.Time.Days}d {time.Time.Hours}h {time.Time.Minutes}m"))
                    .ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.BanMembers)]
            [BotPerm(GuildPerm.BanMembers)]
            [Priority(2)]
            public async Task Ban(ulong userId, [Remainder] string msg = null)
            {
                var user = await ctx.Guild.GetUserAsync(userId);
                if (user is null)
                {
                    await ctx.Guild.AddBanAsync(userId, 0, ctx.User + " | " + msg);
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle("⛔️ " + GetText("banned_user"))
                            .WithImageUrl(
                                "https://cdn.discordapp.com/attachments/732021633719730236/769426176099614720/IMG_20201024_010137.jpg")
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
            [UserPerm(GuildPerm.BanMembers)]
            [BotPerm(GuildPerm.BanMembers)]
            [Priority(1)]
            public async Task Ban(IGuildUser user, [Remainder] string msg = null)
            {
                if (ctx.User.Id != user.Guild.OwnerId && user.GetRoles().Select(r => r.Position).Max() >=
                    ((IGuildUser) ctx.User).GetRoles().Select(r => r.Position)
                    .Max())
                {
                    await ReplyErrorLocalizedAsync("hierarchy").ConfigureAwait(false);
                    return;
                }

                var dmFailed = false;

                try
                {
                    var defaultMessage = GetText("bandm", Format.Bold(ctx.Guild.Name), msg);
                    var toDmUser = _service.GetBanUserDmEmbed(Context, user, defaultMessage, msg, null);
                    if (!(toDmUser is null))
                    {
                        var userChannel = await user.CreateDMChannelAsync();
                        await userChannel.EmbedAsync(toDmUser);
                    }
                }
                catch
                {
                    dmFailed = true;
                }

                await ctx.Guild.AddBanAsync(user, 7, ctx.User + " | " + msg).ConfigureAwait(false);

                var toSend = new EmbedBuilder().WithOkColor()
                    .WithTitle("⛔️ " + GetText("banned_user"))
                    .WithImageUrl(
                        "https://cdn.discordapp.com/attachments/707730610650873916/743625540812013586/ezgif.com-optimize_1.gif")
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
            [UserPerm(GuildPerm.BanMembers)]
            [BotPerm(GuildPerm.BanMembers)]
            public async Task BanMessage([Remainder] string message = null)
            {
                if (message is null)
                {
                    var template = _service.GetBanTemplate(Context.Guild.Id);
                    if (template is null)
                    {
                        await ReplyConfirmLocalizedAsync("banmsg_default");
                        return;
                    }

                    await Context.Channel.SendConfirmAsync(template);
                    return;
                }

                _service.SetBanTemplate(Context.Guild.Id, message);
                await ctx.Channel.SendConfirmAsync("👌");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.BanMembers)]
            [BotPerm(GuildPerm.BanMembers)]
            public async Task BanMsgReset()
            {
                _service.SetBanTemplate(Context.Guild.Id, null);
                await ctx.Channel.SendConfirmAsync("👌");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.BanMembers)]
            [BotPerm(GuildPerm.BanMembers)]
            [Priority(0)]
            public Task BanMessageTest([Remainder] string reason = null)
            {
                return InternalBanMessageTest(reason, null);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.BanMembers)]
            [BotPerm(GuildPerm.BanMembers)]
            [Priority(1)]
            public Task BanMessageTest(StoopidTime duration, [Remainder] string reason = null)
            {
                return InternalBanMessageTest(reason, duration.Time);
            }

            private async Task InternalBanMessageTest(string reason, TimeSpan? duration)
            {
                var dmChannel = await ctx.User.CreateDMChannelAsync();
                var defaultMessage = GetText("bandm", Format.Bold(ctx.Guild.Name), reason);
                var embed = _service.GetBanUserDmEmbed(Context,
                    (IGuildUser) Context.User,
                    defaultMessage,
                    reason,
                    duration);

                if (embed is null)
                {
                    await ConfirmLocalizedAsync("bandm_disabled");
                }
                else
                {
                    try
                    {
                        await dmChannel.EmbedAsync(embed);
                    }
                    catch (Exception)
                    {
                        await ReplyErrorLocalizedAsync("unable_to_dm_user");
                        return;
                    }

                    var confirmMessage = await Context.Channel.SendConfirmAsync("👌");
                    confirmMessage.DeleteAfter(3);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.BanMembers)]
            [BotPerm(GuildPerm.BanMembers)]
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
            [UserPerm(GuildPerm.BanMembers)]
            [BotPerm(GuildPerm.BanMembers)]
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
            [UserPerm(GuildPerm.KickMembers)]
            [BotPerm(GuildPerm.BanMembers)]
            public async Task Softban(IGuildUser user, [Remainder] string msg = null)
            {
                if (ctx.User.Id != user.Guild.OwnerId && user.GetRoles().Select(r => r.Position).Max() >=
                    ((IGuildUser) ctx.User).GetRoles().Select(r => r.Position).Max())
                {
                    await ReplyErrorLocalizedAsync("hierarchy").ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(msg))
                    try
                    {
                        await user.SendErrorAsync(GetText("sbdm", Format.Bold(ctx.Guild.Name), msg))
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }

                await ctx.Guild.AddBanAsync(user, 0, ctx.User + " | " + msg).ConfigureAwait(false);
                try
                {
                    await ctx.Guild.RemoveBanAsync(user).ConfigureAwait(false);
                }
                catch
                {
                    await ctx.Guild.RemoveBanAsync(user).ConfigureAwait(false);
                }

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle("☣ " + GetText("sb_user"))
                        .AddField(
                            efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                        .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true)))
                    .ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.KickMembers)]
            [BotPerm(GuildPerm.KickMembers)]
            public async Task Kick(IGuildUser user, [Remainder] string msg = null)
            {
                if (ctx.Message.Author.Id != user.Guild.OwnerId && user.GetRoles().Select(r => r.Position).Max() >=
                    ((IGuildUser) ctx.User).GetRoles().Select(r => r.Position).Max())
                {
                    await ReplyErrorLocalizedAsync("hierarchy").ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(msg))
                    try
                    {
                        await user.SendErrorAsync(GetText("kickdm", Format.Bold(ctx.Guild.Name), msg))
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                    }

                await user.KickAsync(ctx.User + " | " + msg).ConfigureAwait(false);
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle(GetText("kicked_user"))
                        .AddField(
                            efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                        .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true)))
                    .ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.KickMembers)]
            [BotPerm(GuildPerm.KickMembers)]
            public async Task Yeet(IGuildUser user, [Remainder] string msg = null)
            {
                if (ctx.Message.Author.Id != user.Guild.OwnerId && user.GetRoles().Select(r => r.Position).Max() >=
                    ((IGuildUser) ctx.User).GetRoles().Select(r => r.Position).Max())
                {
                    await ReplyErrorLocalizedAsync("hierarchy").ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(msg))
                    try
                    {
                        await user.SendErrorAsync(GetText("yeetdm", Format.Bold(ctx.Guild.Name), msg))
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                    }

                await user.KickAsync(ctx.User + " | " + msg).ConfigureAwait(false);
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle("User Yeeted")
                        .WithImageUrl(
                            "https://cdn.discordapp.com/attachments/728758254796275782/744406804200685618/tenor_6.gif")
                        .AddField(
                            efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                        .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true)))
                    .ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.BanMembers)]
            [BotPerm(GuildPerm.BanMembers)]
            [Priority(1)]
            public async Task PermYeet(ulong userId, [Remainder] string msg = null)
            {
                var user = await ctx.Guild.GetUserAsync(userId);
                if (user is null)
                {
                    await ctx.Guild.AddBanAsync(userId, 0, ctx.User + " | " + msg);

                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle("⛔️ " + "User Permanently Yeeted")
                            .WithImageUrl(
                                "https://cdn.discordapp.com/attachments/728758254796275782/744406804200685618/tenor_6.gif")
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
            [UserPerm(GuildPerm.BanMembers)]
            [BotPerm(GuildPerm.BanMembers)]
            [Priority(2)]
            public async Task PermYeet(IGuildUser user, [Remainder] string msg = null)
            {
                if (ctx.User.Id != user.Guild.OwnerId && user.GetRoles().Select(r => r.Position).Max() >=
                    ((IGuildUser) ctx.User).GetRoles().Select(r => r.Position).Max())
                {
                    await ReplyErrorLocalizedAsync("hierarchy").ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(msg))
                    try
                    {
                        await user.SendErrorAsync("You have been permanently yeeted from ", Format.Bold(ctx.Guild.Name),
                            msg).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }

                await ctx.Guild.AddBanAsync(user, 0, ctx.User + " | " + msg).ConfigureAwait(false);
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle("User Permanently Yeeted")
                        .WithImageUrl(
                            "https://media.discordapp.net/attachments/707730610650873916/758453543836450816/ezgif.com-gif-maker.gif")
                        .AddField(
                            efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                        .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true)))
                    .ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [BotPerm(GuildPerm.BanMembers)]
            [OwnerOnly]
            public async Task MassKill([Remainder] string people)
            {
                if (string.IsNullOrWhiteSpace(people))
                    return;

                var (bans, missing) = _service.MassKill((SocketGuild) ctx.Guild, people);

                var missStr = string.Join("\n", missing);
                if (string.IsNullOrWhiteSpace(missStr))
                    missStr = "-";

                //send a message but don't wait for it
                var banningMessageTask = ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithDescription(GetText("mass_kill_in_progress", bans.Count()))
                    .AddField(GetText("invalid", missing), missStr)
                    .WithOkColor());

                Bc.Reload();

                //do the banning
                await Task.WhenAll(bans
                        .Where(x => x.Id.HasValue)
                        .Select(x => ctx.Guild.AddBanAsync(x.Id.Value, 0, x.Reason, new RequestOptions
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
}