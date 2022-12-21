using System.Threading.Tasks;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Humanizer.Localisation;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Moderation.Services;
using NekosBestApiNet;
using Serilog;
using Swan;

namespace Mewdeko.Modules.Moderation;

[Group("moderation", "Do all your moderation stuffs here!"), CheckPermissions]
public class SlashPunishCommands : MewdekoSlashSubmodule<UserPunishService>
{
    public enum AddRole
    {
        AddRole
    }

    private readonly InteractiveService interactivity;

    private readonly DbService db;
    private readonly NekosBestApi nekos;

    public SlashPunishCommands(DbService db,
        InteractiveService serv,
        NekosBestApi nekos)
    {
        interactivity = serv;
        this.nekos = nekos;
        this.db = db;
    }

    [SlashCommand("setwarnchannel", "Set the channel where warns are logged!"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task SetWarnChannel(ITextChannel channel)
    {
        var warnlogChannel = await Service.GetWarnlogChannel(ctx.Guild.Id);
        if (warnlogChannel == channel.Id)
        {
            await ctx.Interaction.SendErrorAsync("This is already your warnlog channel!").ConfigureAwait(false);
            return;
        }

        if (warnlogChannel == 0)
        {
            await Service.SetWarnlogChannelId(ctx.Guild, channel).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync($"Your warnlog channel has been set to {channel.Mention}").ConfigureAwait(false);
            return;
        }

        var oldWarnChannel = await ctx.Guild.GetTextChannelAsync(warnlogChannel).ConfigureAwait(false);
        await Service.SetWarnlogChannelId(ctx.Guild, channel).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync(
            $"Your warnlog channel has been changed from {oldWarnChannel.Mention} to {channel.Mention}").ConfigureAwait(false);
    }

    [SlashCommand("timeout", "Time a user out."), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.ModerateMembers), BotPerm(GuildPermission.ModerateMembers),
     CheckPermissions]
    public async Task Timeout(string inputTime, IGuildUser user, string? reason = null)
    {
        if (!await CheckRoleHierarchy(user))
            return;

        StoopidTime time;
        try
        {
            time = StoopidTime.FromInput(inputTime);
        }
        catch
        {
            await ctx.Interaction.SendErrorAsync("Invalid time specified. Please follow the format `4d3h2m1s`");
            return;
        }

        reason ??= $"{ctx.User} || None Specified";
        if (time.Time.Days > 28)
        {
            await ReplyErrorLocalizedAsync("timeout_length_too_long").ConfigureAwait(false);
            return;
        }

        await user.SetTimeOutAsync(time.Time, new RequestOptions
        {
            AuditLogReason = reason
        }).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("timeout_set", user.Mention, time.Time.Humanize(maxUnit: TimeUnit.Day)).ConfigureAwait(false);
    }

    [SlashCommand("untimeout", "Remove a users timeout."), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.ModerateMembers),
     BotPerm(GuildPermission.ModerateMembers), CheckPermissions]
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

    [SlashCommand("warn", "Warn a user with an optional reason"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.BanMembers), CheckPermissions]
    public async Task Warn(IGuildUser user, string? reason = null)
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

            await ctx.Interaction.RespondAsync(embed: errorEmbed.Build());
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

        await ctx.Interaction.RespondAsync(embed: embed.Build());
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
                    $"Username: {user.Username}#{user.Discriminator}\nID of Warned User: {user.Id}\nWarn Number: {warnings}\nPunishment: {punishaction} {punishtime}\n\nReason: {reason}\n\n[Click Here For Context]({ctx.Interaction.GetOriginalResponseAsync().GetAwaiter().GetResult().GetJumpUrl()})"));
        }
    }

    [SlashCommand("setwarnexpire", "Set when warns expire in days"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task WarnExpire(int days, [Summary("todelete", "Set whether warns are deleted instead of cleared.")] bool delete)
    {
        if (days is < 0 or > 366)
            return;

        await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

        await Service.WarnExpireAsync(ctx.Guild.Id, days, delete).ConfigureAwait(false);
        if (days == 0)
        {
            await ReplyConfirmLocalizedAsync("warn_expire_reset").ConfigureAwait(false);
            return;
        }

        if (delete)
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

    [SlashCommand("warnlog", "Check a users warn amount"), RequireContext(ContextType.Guild)]
    public async Task Warnlog(IGuildUser? user = null)
    {
        user ??= (IGuildUser)ctx.User;
        if (ctx.User.Id == user.Id || ((IGuildUser)ctx.User).GuildPermissions.BanMembers)
            await InternalWarnlog(user.Id);
        else
            await ctx.Interaction.SendEphemeralErrorAsync("You are missing the permissions to view another user's warns.");
    }

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
        await interactivity.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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

    [SlashCommand("warnlogall", "Show the warn count of all users in the server."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.BanMembers), CheckPermissions]
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

        await interactivity.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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

    [SlashCommand("warnclear", "Clear all or a specific warn for a user."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.BanMembers), CheckPermissions]
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

    [SlashCommand("warnpunish", "Set what each warn count does."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.BanMembers), CheckPermissions]
    public async Task WarnPunish(int number, PunishmentAction punish = PunishmentAction.None, string? input = null)
    {
        var time = StoopidTime.FromInput("0s");
        if (input is not null)
        {
            try
            {
                time = StoopidTime.FromInput(input);
            }
            catch
            {
                await ctx.Interaction.SendErrorAsync("Invalid time specified. Please follow the format `4d3h2m1s`");
                return;
            }
        }

        switch (punish)
        {
            // this should never happen. Addrole has its own method with higher
            case PunishmentAction.AddRole:
            case PunishmentAction.Warn:
                return;
        }

        if (punish == PunishmentAction.None)
        {
            if (!await Service.WarnPunishRemove(ctx.Guild.Id, number)) return;

            await ReplyConfirmLocalizedAsync("warn_punish_rem",
                Format.Bold(number.ToString())).ConfigureAwait(false);
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
            case PunishmentAction.Timeout when time.Time.TotalSeconds is 0:
                await ReplyErrorLocalizedAsync("timeout_needs_time").ConfigureAwait(false);
                return;
        }

        if (time.Time.TotalSeconds is 0)
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

    [SlashCommand("warnpunishlist", "See how many warns does what"), RequireContext(ContextType.Guild), CheckPermissions]
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

        await ctx.Interaction.SendConfirmAsync(
            GetText("warn_punish_list"), list).ConfigureAwait(false);
    }


    [SlashCommand("hackban", "Bans a user by their ID"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers)]
    public async Task Ban(IUser userId, string? msg = null, string time = null)
    {
        if (time is not null)
        {
            StoopidTime stoopid;
            try
            {
                stoopid = StoopidTime.FromInput(time);
            }
            catch
            {
                await ctx.Interaction.SendErrorAsync("Invalid time specified. Please follow the format `4d3h2m1s`");
                return;
            }

            await InternalBanAsync(userId.Id, reason: msg, time: stoopid.Time, hackBan: true);
        }
        else
        {
            await InternalBanAsync(userId.Id, reason: msg, hackBan: true);
        }
    }

    [SlashCommand("ban", "Bans a user by their ID"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers)]
    public async Task Ban(IGuildUser user, string reason = null, string time = null)
    {
        if (time is not null)
        {
            StoopidTime stoopid;
            try
            {
                stoopid = StoopidTime.FromInput(time);
            }
            catch
            {
                await ctx.Interaction.SendErrorAsync("Invalid time specified. Please follow the format `4d3h2m1s`");
                return;
            }

            await InternalBanAsync(user: user, reason: reason, time: stoopid.Time);
        }
        else
        {
            await InternalBanAsync(user: user, reason: reason);
        }
    }

    private async Task InternalBanAsync(
        ulong userId = 0,
        bool hackBan = false,
        string reason = null,
        TimeSpan time = default,
        IGuildUser user = null)
    {
        if (hackBan)
        {
            if (time != default)
            {
                await ctx.Guild.AddBanAsync(userId, time.Days, options: new RequestOptions
                {
                    AuditLogReason = $"{ctx.User} | {reason}"
                }).ConfigureAwait(false);

                await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor().WithTitle($"⛔️ {GetText("banned_user")}")
                        .AddField(efb => efb.WithName("ID").WithValue(userId.ToString()).WithIsInline(true)).Build())
                    .ConfigureAwait(false);
            }
            else
            {
                await ctx.Guild.AddBanAsync(userId, 7, options: new RequestOptions
                {
                    AuditLogReason = $"{ctx.User} | {reason}"
                }).ConfigureAwait(false);

                await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                        .WithTitle($"⛔️ {GetText("banned_user")}")
                        .AddField(efb => efb.WithName("ID").WithValue(userId.ToString()).WithIsInline(true)).Build())
                    .ConfigureAwait(false);
            }
        }
        else
        {
            if (time != default)
            {
                var dmFailed = false;

                try
                {
                    var defaultMessage = GetText("bandm", Format.Bold(ctx.Guild.Name), reason);
                    var (embedBuilder, message, components) = await Service.GetBanUserDmEmbed(Context, user, defaultMessage, reason, null).ConfigureAwait(false);
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

                await ctx.Guild.AddBanAsync(user, time.Days, options: new RequestOptions
                {
                    AuditLogReason = $"{ctx.User} | {reason}"
                }).ConfigureAwait(false);

                var toSend = new EmbedBuilder().WithOkColor()
                    .WithTitle($"⛔️ {GetText("banned_user")}")
                    .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
                    .WithImageUrl((await nekos.ActionsApi.Kick().ConfigureAwait(false)).Results.First().Url);

                if (dmFailed) toSend.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

                await ctx.Interaction.RespondAsync(embed: toSend.Build())
                    .ConfigureAwait(false);
            }
            else
            {
                var dmFailed = false;

                try
                {
                    var defaultMessage = GetText("bandm", Format.Bold(ctx.Guild.Name), reason);
                    var (embedBuilder, message, components) = await Service.GetBanUserDmEmbed(ctx, user, defaultMessage, reason, null).ConfigureAwait(false);
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
                    AuditLogReason = $"{ctx.User} | {reason}"
                }).ConfigureAwait(false);

                var toSend = new EmbedBuilder().WithOkColor()
                    .WithTitle($"⛔️ {GetText("banned_user")}")
                    .AddField(efb => efb.WithName(GetText("username")).WithValue(user.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
                    .WithImageUrl((await nekos.ActionsApi.Kick().ConfigureAwait(false)).Results.First().Url);

                if (dmFailed) toSend.WithFooter($"⚠️ {GetText("unable_to_dm_user")}");

                await ctx.Interaction.RespondAsync(embed: toSend.Build())
                    .ConfigureAwait(false);
            }
        }
    }


    [SlashCommand("unban", "Unban a user."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.BanMembers), BotPerm(GuildPermission.BanMembers), CheckPermissions]
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

    [SlashCommand("softban", "Bans then unbans a user."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.KickMembers | GuildPermission.ManageMessages), BotPerm(GuildPermission.BanMembers), CheckPermissions]
    public async Task Softban(IGuildUser user, string? msg = null) => await SoftbanInternal(user, msg);

    private async Task SoftbanInternal(IGuildUser user, string? msg = null)
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

        await ctx.Interaction.RespondAsync(embed: toSend.Build())
            .ConfigureAwait(false);
    }

    [SlashCommand("kick", "Kicks a user with an optional reason"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.KickMembers), BotPerm(GuildPermission.KickMembers)]
    public async Task Kick(IGuildUser user, string? msg = null) => await KickInternal(user, msg);


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

        await ctx.Interaction.RespondAsync(embed: toSend.Build())
            .ConfigureAwait(false);
    }
}