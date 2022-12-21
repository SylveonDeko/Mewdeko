using System.Threading.Tasks;
using CommandLine;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Humanizer.Localisation;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Moderation.Services;
using NekosBestApiNet;
using Serilog;
using Swan;

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

        private readonly MuteService mute;
        private readonly InteractiveService interactivity;

        private readonly DbService db;
        private readonly NekosBestApi nekos;

        public UserPunishCommands(MuteService mute, DbService db,
            InteractiveService serv,
            NekosBestApi nekos)
        {
            interactivity = serv;
            this.nekos = nekos;
            this.mute = mute;
            this.db = db;
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator), Priority(0)]
        public async Task SetWarnChannel([Remainder] ITextChannel channel)
        {
            var warnlogChannel = await Service.GetWarnlogChannel(ctx.Guild.Id);
            if (warnlogChannel == channel.Id)
            {
                await ctx.Channel.SendErrorAsync("This is already your warnlog channel!").ConfigureAwait(false);
                return;
            }

            if (warnlogChannel == 0)
            {
                await Service.SetWarnlogChannelId(ctx.Guild, channel).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync($"Your warnlog channel has been set to {channel.Mention}").ConfigureAwait(false);
                return;
            }

            var oldWarnChannel = await ctx.Guild.GetTextChannelAsync(warnlogChannel).ConfigureAwait(false);
            await Service.SetWarnlogChannelId(ctx.Guild, channel).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Your warnlog channel has been changed from {oldWarnChannel.Mention} to {channel.Mention}").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ModerateMembers), BotPerm(GuildPermission.ModerateMembers)]
        public async Task Timeout(StoopidTime time, IGuildUser user, [Remainder] string? reason = null)
        {
            if (!await CheckRoleHierarchy(user))
                return;
            reason ??= $"{ctx.User} || None Specified";
            if (time.Time.Days > 28)
            {
                await ReplyErrorLocalizedAsync("timeout_length_too_long").ConfigureAwait(false);
                return;
            }

            await user.SetTimeOutAsync(time.Time, new RequestOptions
            {
                AuditLogReason = $"{ctx.User} | {reason}"
            }).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("timeout_set", user.Mention, time.Time.Humanize(maxUnit: TimeUnit.Day)).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ModerateMembers), BotPerm(GuildPermission.ModerateMembers)]
        public async Task UnTimeOut(IGuildUser user)
        {
            if (!await CheckRoleHierarchy(user))
                return;
            await user.RemoveTimeOutAsync(new RequestOptions
            {
                AuditLogReason = $"Removal requested by {ctx.User}"
            }).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("timeout_removed", user.Mention).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers)]
        public async Task Warn(IGuildUser user, [Remainder] string? reason = null)
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

                if (dmFailed) errorEmbed.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

                await ctx.Channel.EmbedAsync(errorEmbed);
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor();
            if (punishment is null || punishment.Id is 0)
            {
                embed.WithDescription(GetText("user_warned",
                    Format.Bold(user.ToString())));
            }
            else
            {
                embed.WithDescription(GetText("user_warned_and_punished", Format.Bold(user.ToString()),
                    Format.Bold(punishment.Punishment.ToString())));
            }

            if (dmFailed) embed.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

            if (dmFailed) embed.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

            await ctx.Channel.EmbedAsync(embed);
            if (await Service.GetWarnlogChannel(ctx.Guild.Id) != 0)
            {
                var uow = db.GetDbContext();
                var warnings = uow.Warnings
                    .ForId(ctx.Guild.Id, user.Id)
                    .Count(w => !w.Forgiven && w.UserId == user.Id);
                var condition = punishment != null;
                var punishtime = condition ? TimeSpan.FromMinutes(punishment.Time).ToString() : " ";
                var punishaction = condition ? punishment.Punishment.Humanize() : "None";
                var channel = await ctx.Guild.GetTextChannelAsync(await Service.GetWarnlogChannel(ctx.Guild.Id));
                await channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithThumbnailUrl(user.RealAvatarUrl().ToString())
                    .WithTitle($"Warned by: {ctx.User}")
                    .WithCurrentTimestamp()
                    .WithDescription(
                        $"Username: {user.Username}#{user.Discriminator}\nID of Warned User: {user.Id}\nWarn Number: {warnings}\nPunishment: {punishaction} {punishtime}\n\nReason: {reason}\n\n[Click Here For Context](https://discord.com/channels/{ctx.Message.GetJumpUrl()})"));
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator), MewdekoOptions(typeof(WarnExpireOptions)), Priority(2)]
        public async Task WarnExpire(int days, params string[] args)
        {
            if (days is < 0 or > 366)
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
            {
                await ReplyConfirmLocalizedAsync("warn_expire_set_delete", Format.Bold(days.ToString()))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("warn_expire_set_clear", Format.Bold(days.ToString()))
                    .ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(3), UserPerm(GuildPermission.BanMembers)]
        public async Task Warnlog(IGuildUser user) => await InternalWarnlog(user.Id);

        public async Task Warnlog() => await InternalWarnlog(ctx.User.Id);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), Priority(1)]
        public async Task Warnlog(ulong userId) => await InternalWarnlog(userId);

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
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();
            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                warnings = warnings.Skip(page)
                    .Take(9)
                    .ToArray();

                var embed = new PageBuilder().WithOkColor()
                    .WithTitle(GetText("warnlog_for",
                        (ctx.Guild as SocketGuild)?.GetUser(userId)?.ToString() ?? userId.ToString()))
                    .WithFooter(efb => efb.WithText(GetText("page", page + 1)));

                if (warnings.Length == 0)
                {
                    embed.WithDescription(GetText("warnings_none"));
                }
                else
                {
                    var i = page * 9;
                    foreach (var w in warnings)
                    {
                        i++;
                        var name = GetText("warned_on_by", $"<t:{w.DateAdded.Value.ToUnixEpochDate()}:D>",
                            $"<t:{w.DateAdded.Value.ToUnixEpochDate()}:T>", w.Moderator);
                        if (w.Forgiven)
                            name = $"{Format.Strikethrough(name)} {GetText("warn_cleared_by", w.ForgivenBy)}";

                        embed.AddField(x => x
                            .WithName($"#`{i}` {name}")
                            .WithValue(w.Reason.TrimTo(1020)));
                    }
                }

                return embed;
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers)]
        public async Task WarnlogAll()
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

            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                {
                    var ws = await warnings.Skip(page * 15)
                        .Take(15)
                        .ToArray()
                        .Select(async x =>
                        {
                            var all = x.Count();
                            var forgiven = x.Count(y => y.Forgiven);
                            var total = all - forgiven;
                            var usr = await ctx.Guild.GetUserAsync(x.Key).ConfigureAwait(false);
                            return $"{usr?.ToString() ?? x.Key.ToString()} | {total} ({all} - {forgiven})";
                        }).GetResults().ConfigureAwait(false);

                    return new PageBuilder().WithOkColor()
                        .WithTitle(GetText("warnings_list"))
                        .WithDescription(string.Join("\n", ws));
                }
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers)]
        public async Task Warnclear(IGuildUser user, int index = 0)
        {
            if (index < 0)
                return;
            if (!await CheckRoleHierarchy(user))
                return;
            var success = await Service.WarnClearAsync(ctx.Guild.Id, user.Id, index, ctx.User.ToString()).ConfigureAwait(false);
            var userStr = user.ToString();
            if (index == 0)
            {
                await ReplyConfirmLocalizedAsync("warnings_cleared", userStr).ConfigureAwait(false);
            }
            else
            {
                if (success)
                {
                    await ReplyConfirmLocalizedAsync("warning_cleared", Format.Bold(index.ToString()), userStr)
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalizedAsync("warning_clear_fail").ConfigureAwait(false);
                }
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), Priority(1)]
        public async Task WarnPunish(int number, AddRole _, IRole role, StoopidTime? time = null)
        {
            const PunishmentAction punish = PunishmentAction.AddRole;
            var success = await Service.WarnPunish(ctx.Guild.Id, number, punish, time, role);

            if (!success)
                return;

            if (time is null)
            {
                await ReplyConfirmLocalizedAsync("warn_punish_set",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString())).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("warn_punish_set_timed",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString()),
                    Format.Bold(time.Input)).ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers)]
        public async Task WarnPunish(int number, PunishmentAction punish, StoopidTime? time = null)
        {
            switch (punish)
            {
                // this should never happen. Addrole has its own method with higher priority
                case PunishmentAction.AddRole:
                case PunishmentAction.Warn:
                    return;
            }

            var success = await Service.WarnPunish(ctx.Guild.Id, number, punish, time);

            if (!success)
                return;
            switch (punish)
            {
                case PunishmentAction.Timeout when time?.Time.Days > 28:
                    await ReplyErrorLocalizedAsync("timeout_length_too_long").ConfigureAwait(false);
                    return;
                case PunishmentAction.Timeout when time is null:
                    await ReplyErrorLocalizedAsync("timeout_needs_time").ConfigureAwait(false);
                    return;
            }

            if (time is null)
            {
                await ReplyConfirmLocalizedAsync("warn_punish_set",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString())).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("warn_punish_set_timed",
                    Format.Bold(punish.ToString()),
                    Format.Bold(number.ToString()),
                    Format.Bold(time.Input)).ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers)]
        public async Task WarnPunish(int number)
        {
            if (!await Service.WarnPunishRemove(ctx.Guild.Id, number)) return;

            await ReplyConfirmLocalizedAsync("warn_punish_rem",
                Format.Bold(number.ToString())).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task WarnPunishList()
        {
            var ps = await Service.WarnPunishList(ctx.Guild.Id);

            string? list;
            if (ps.Length > 0)
            {
                list = string.Join("\n",
                    ps.Select(x =>
                        $"{x.Count} -> {x.Punishment} {(x.Punishment == PunishmentAction.AddRole ? $"<@&{x.RoleId}>" : "")} {(x.Time <= 0 ? "" : $"{x.Time}m")} "));
            }
            else
            {
                list = GetText("warnpl_none");
            }

            await ctx.Channel.SendConfirmAsync(
                GetText("warn_punish_list"),
                list).ConfigureAwait(false);
        }


        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), Priority(1)]
        public async Task Ban(StoopidTime pruneTime, StoopidTime time, IGuildUser user, [Remainder] string? msg = null)
        {
            if (time.Time > TimeSpan.FromDays(49))
                return;

            if (pruneTime.Time > TimeSpan.FromDays(49))
                return;


            if (user != null && !await CheckRoleHierarchy(user).ConfigureAwait(false))
                return;

            var dmFailed = false;

            if (user != null)
            {
                try
                {
                    var defaultMessage = GetText("bandm", Format.Bold(ctx.Guild.Name), msg);
                    var (embedBuilder, message, components) = await Service.GetBanUserDmEmbed(Context, user, defaultMessage, msg, time.Time).ConfigureAwait(false);
                    if (embedBuilder is not null || message is not null)
                    {
                        var userChannel = await user.CreateDMChannelAsync().ConfigureAwait(false);
                        await userChannel.SendMessageAsync(message, embeds: embedBuilder, components: components?.Build()).ConfigureAwait(false);
                    }
                }
                catch
                {
                    dmFailed = true;
                }
            }

            await mute.TimedBan(Context.Guild, user, time.Time, $"{ctx.User} | {msg}", pruneTime.Time).ConfigureAwait(false);
            var toSend = new EmbedBuilder().WithOkColor()
                .WithTitle($"⛔️ {GetText("banned_user")}")
                .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
                .AddField(efb =>
                    efb.WithName(GetText("duration"))
                        .WithValue($"{time.Time.Days}d {time.Time.Hours}h {time.Time.Minutes}m")
                        .WithIsInline(true));

            if (dmFailed) toSend.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

            await ctx.Channel.EmbedAsync(toSend)
                .ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), Priority(0)]
        public async Task Ban(ulong userId, [Remainder] string? msg = null)
        {
            var user = await ((DiscordSocketClient)Context.Client).Rest.GetGuildUserAsync(Context.Guild.Id,
                userId).ConfigureAwait(false);
            if (user is null)
            {
                await ctx.Guild.AddBanAsync(userId, 7, options: new RequestOptions
                {
                    AuditLogReason = $"{ctx.User} | {msg}"
                }).ConfigureAwait(false);

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle($"⛔️ {GetText("banned_user")}")
                        .AddField(efb => efb.WithName("ID").WithValue(userId.ToString()).WithIsInline(true)))
                    .ConfigureAwait(false);
            }
            else
            {
                await Ban(user, msg).ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), Priority(0)]
        public async Task Ban(StoopidTime time, ulong userId, [Remainder] string? msg = null)
        {
            await ctx.Guild.AddBanAsync(userId, time.Time.Days, options: new RequestOptions
            {
                AuditLogReason = $"{ctx.User} | {msg}"
            }).ConfigureAwait(false);

            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithTitle($"⛔️ {GetText("banned_user")}")
                    .AddField(efb => efb.WithName("ID").WithValue(userId.ToString()).WithIsInline(true)))
                .ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), Priority(2)]
        public async Task Ban(IGuildUser user, [Remainder] string? msg = null)
        {
            if (!await CheckRoleHierarchy(user).ConfigureAwait(false))
                return;

            var dmFailed = false;

            try
            {
                var defaultMessage = GetText("bandm", Format.Bold(ctx.Guild.Name), msg);
                var (embedBuilder, message, components) = await Service.GetBanUserDmEmbed(Context, user, defaultMessage, msg, null).ConfigureAwait(false);
                if (embedBuilder is not null || message is not null)
                {
                    var userChannel = await user.CreateDMChannelAsync().ConfigureAwait(false);
                    await userChannel.SendMessageAsync(message, embeds: embedBuilder, components: components?.Build()).ConfigureAwait(false);
                }
            }
            catch
            {
                dmFailed = true;
            }

            await ctx.Guild.AddBanAsync(user, 7, options: new RequestOptions
            {
                AuditLogReason = $"{ctx.User} | {msg}"
            }).ConfigureAwait(false);

            var toSend = new EmbedBuilder().WithOkColor()
                .WithTitle($"⛔️ {GetText("banned_user")}")
                .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
                .WithImageUrl((await nekos.ActionsApi.Kick().ConfigureAwait(false)).Results.First().Url);

            if (dmFailed) toSend.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

            await ctx.Channel.EmbedAsync(toSend)
                .ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), Priority(2)]
        public async Task Ban(StoopidTime time, IGuildUser user, [Remainder] string? msg = null)
        {
            if (!await CheckRoleHierarchy(user).ConfigureAwait(false))
                return;

            var dmFailed = false;

            try
            {
                var defaultMessage = GetText("bandm", Format.Bold(ctx.Guild.Name), msg);
                var (embedBuilder, message, components) = await Service.GetBanUserDmEmbed(Context, user, defaultMessage, msg, null).ConfigureAwait(false);
                if (embedBuilder is not null || message is not null)
                {
                    var userChannel = await user.CreateDMChannelAsync().ConfigureAwait(false);
                    await userChannel.SendMessageAsync(message, embeds: embedBuilder, components: components?.Build()).ConfigureAwait(false);
                }
            }
            catch
            {
                dmFailed = true;
            }

            await ctx.Guild.AddBanAsync(user, time.Time.Days, options: new RequestOptions
            {
                AuditLogReason = $"{ctx.User} | {msg}"
            }).ConfigureAwait(false);

            var toSend = new EmbedBuilder().WithOkColor()
                .WithTitle($"⛔️ {GetText("banned_user")}")
                .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
                .WithImageUrl((await nekos.ActionsApi.Kick().ConfigureAwait(false)).Results.First().Url);

            if (dmFailed) toSend.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

            await ctx.Channel.EmbedAsync(toSend)
                .ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers)]
        public async Task BanMessage([Remainder] string? message = null)
        {
            if (message is null)
            {
                var template = Service.GetBanTemplate(Context.Guild.Id);
                if (template is null)
                {
                    await ReplyConfirmLocalizedAsync("banmsg_default").ConfigureAwait(false);
                    return;
                }

                await Context.Channel.SendConfirmAsync(template).ConfigureAwait(false);
                return;
            }

            Service.SetBanTemplate(Context.Guild.Id, message);
            await ctx.OkAsync().ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers)]
        public async Task BanMsgReset()
        {
            Service.SetBanTemplate(Context.Guild.Id, null);
            await ctx.OkAsync().ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), Priority(0)]
        public Task BanMessageTest([Remainder] string? reason = null) => InternalBanMessageTest(reason, null);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), Priority(1)]
        public Task BanMessageTest(StoopidTime duration, [Remainder] string? reason = null) => InternalBanMessageTest(reason, duration.Time);

        private async Task InternalBanMessageTest(string? reason, TimeSpan? duration)
        {
            var dmChannel = await ctx.User.CreateDMChannelAsync().ConfigureAwait(false);
            var defaultMessage = GetText("bandm", Format.Bold(ctx.Guild.Name), reason);
            var crEmbed = await Service.GetBanUserDmEmbed(Context,
                (IGuildUser)Context.User,
                defaultMessage,
                reason,
                duration).ConfigureAwait(false);

            if (crEmbed.Item1 is null && crEmbed.Item2 is null)
            {
                await ConfirmLocalizedAsync("bandm_disabled").ConfigureAwait(false);
            }
            else
            {
                try
                {
                    await dmChannel.SendMessageAsync(crEmbed.Item2, embeds: crEmbed.Item1, components: crEmbed.Item3?.Build()).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    await ReplyErrorLocalizedAsync("unable_to_dm_user").ConfigureAwait(false);
                    return;
                }

                await Context.OkAsync().ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers)]
        public async Task Unban([Remainder] string user)
        {
            var bans = await ctx.Guild.GetBansAsync().FlattenAsync().ConfigureAwait(false);

            var bun = bans.FirstOrDefault(x => string.Equals(x.User.ToString(), user, StringComparison.InvariantCultureIgnoreCase));

            if (bun == null)
            {
                await ReplyErrorLocalizedAsync("user_not_found").ConfigureAwait(false);
                return;
            }

            await UnbanInternal(bun.User).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers)]
        public async Task Unban(ulong userId)
        {
            var bun = await Context.Guild.GetBanAsync(userId);

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

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.KickMembers | GuildPermission.ManageMessages), BotPerm(GuildPermission.BanMembers)]
        public Task Softban(IGuildUser user, [Remainder] string? msg = null) => SoftbanInternal(user, msg);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.KickMembers | GuildPermission.ManageMessages), BotPerm(GuildPermission.BanMembers)]
        public async Task Softban(ulong userId, [Remainder] string? msg = null)
        {
            var user = await ((DiscordSocketClient)Context.Client).Rest.GetGuildUserAsync(Context.Guild.Id,
                userId).ConfigureAwait(false);
            if (user is null)
                return;

            await SoftbanInternal(user).ConfigureAwait(false);
        }

        private async Task SoftbanInternal(IGuildUser user, [Remainder] string? msg = null)
        {
            if (!await CheckRoleHierarchy(user).ConfigureAwait(false))
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

            await ctx.Guild.AddBanAsync(user, 7, options: new RequestOptions
            {
                AuditLogReason = $"Softban: {ctx.User} | {msg}"
            }).ConfigureAwait(false);
            try
            {
                await ctx.Guild.RemoveBanAsync(user).ConfigureAwait(false);
            }
            catch
            {
                await ctx.Guild.RemoveBanAsync(user).ConfigureAwait(false);
            }

            var toSend = new EmbedBuilder().WithOkColor()
                .WithTitle($"☣ {GetText("sb_user")}")
                .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
                .WithImageUrl((await nekos.ActionsApi.Kick().ConfigureAwait(false)).Results.First().Url);

            if (dmFailed) toSend.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

            await ctx.Channel.EmbedAsync(toSend)
                .ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.KickMembers), BotPerm(GuildPermission.KickMembers), Priority(1)]
        public Task Kick(IGuildUser user, [Remainder] string? msg = null) => KickInternal(user, msg);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.KickMembers), BotPerm(GuildPermission.KickMembers), Priority(0)]
        public async Task Kick(ulong userId, [Remainder] string? msg = null)
        {
            var user = await ((DiscordSocketClient)Context.Client).Rest.GetGuildUserAsync(Context.Guild.Id,
                userId).ConfigureAwait(false);
            if (user is null)
                return;

            await KickInternal(user, msg).ConfigureAwait(false);
        }

        public async Task KickInternal(IGuildUser user, string? msg = null)
        {
            if (!await CheckRoleHierarchy(user).ConfigureAwait(false))
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

            await user.KickAsync($"{ctx.User} | {msg}").ConfigureAwait(false);

            var toSend = new EmbedBuilder().WithOkColor()
                .WithTitle(GetText("kicked_user"))
                .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
                .WithImageUrl((await nekos.ActionsApi.Kick().ConfigureAwait(false)).Results.First().Url);

            if (dmFailed) toSend.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

            await ctx.Channel.EmbedAsync(toSend)
                .ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), OwnerOnly]
        public async Task MassKill([Remainder] string people)
        {
            if (string.IsNullOrWhiteSpace(people))
                return;

            var (bans, missing) = Service.MassKill((SocketGuild)ctx.Guild, people);

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
                    .Where(x => x.id.HasValue)
                    .Select(x => ctx.Guild.AddBanAsync(x.id.Value, 7, "", new RequestOptions
                    {
                        RetryMode = RetryMode.AlwaysRetry, AuditLogReason = x.Reason
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