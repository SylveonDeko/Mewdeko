using DataModel;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Common;
using Mewdeko.Modules.Moderation.Services;
using NekosBestApiNet;
using Swan;

namespace Mewdeko.Modules.Moderation;

/// <summary>
///     Slash commands for moderation.
/// </summary>
[Group("moderation", "Do all your moderation stuffs here!")]
[CheckPermissions]
public class SlashPunishCommands : MewdekoSlashSubmodule<UserPunishService>
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly InteractiveService interactivity;
    private readonly ILogger<SlashPunishCommands> logger;
    private readonly NekosBestApi nekos;


    /// <summary>
    ///     Initializes a new instance of <see cref="SlashPunishCommands" />.
    /// </summary>
    /// <param name="dbFactory">The database provider</param>
    /// <param name="serv">The service used for embed pagination</param>
    /// <param name="nekos">The service used to get anime gifs from the nekos.best api</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public SlashPunishCommands(IDataConnectionFactory dbFactory,
        InteractiveService serv,
        NekosBestApi nekos, ILogger<SlashPunishCommands> logger)
    {
        interactivity = serv;
        this.nekos = nekos;
        this.logger = logger;
        this.dbFactory = dbFactory;
    }

    /// <summary>
    ///     Sets the channel to log warns in
    /// </summary>
    /// <param name="channel">The channel to log warns in</param>
    [SlashCommand("setwarnchannel", "Set the channel where warns are logged!")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task SetWarnChannel(ITextChannel channel)
    {
        var warnlogChannel = await Service.GetWarnlogChannel(ctx.Guild.Id);
        if (warnlogChannel == channel.Id)
        {
            await ctx.Interaction.SendErrorAsync(Strings.WarnlogChannelExists(ctx.Guild.Id), Config);
            return;
        }

        if (warnlogChannel == 0)
        {
            await Service.SetWarnlogChannelId(ctx.Guild, channel).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.WarnlogChannelSet(ctx.Guild.Id, channel.Mention));

            return;
        }

        var oldWarnChannel = await ctx.Guild.GetTextChannelAsync(warnlogChannel).ConfigureAwait(false);
        await Service.SetWarnlogChannelId(ctx.Guild, channel).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync(Strings.WarnlogChannelChanged(ctx.Guild.Id, oldWarnChannel.Mention,
            channel.Mention));
    }

    /// <summary>
    ///     Times out a user for a specified time
    /// </summary>
    /// <param name="inputTime">The time to time the user out for, max of 28d at the current moment</param>
    /// <param name="user">The user to time out</param>
    /// <param name="reason">The reason for timing out the user</param>
    [SlashCommand("timeout", "Time a user out.")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ModerateMembers)]
    [BotPerm(GuildPermission.ModerateMembers)]
    [CheckPermissions]
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
            await ctx.Interaction.SendErrorAsync(Strings.InvalidTimeFormat(ctx.Guild.Id), Config);
            return;
        }

        reason ??= $"{ctx.User} || None Specified";
        if (time.Time.Days > 28)
        {
            await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await user.SetTimeOutAsync(time.Time, new RequestOptions
        {
            AuditLogReason = reason
        }).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.TimeoutSet(ctx.Guild.Id, user.Mention,
                time.Time.Humanize(maxUnit: TimeUnit.Day)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a users timeout
    /// </summary>
    /// <param name="user">The user to remove the timeout from</param>
    [SlashCommand("untimeout", "Remove a users timeout.")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ModerateMembers)]
    [BotPerm(GuildPermission.ModerateMembers)]
    [CheckPermissions]
    public async Task UnTimeOut(IGuildUser user)
    {
        if (!await CheckRoleHierarchy(user))
            return;
        await user.RemoveTimeOutAsync(new RequestOptions
        {
            AuditLogReason = $"Removal requested by {ctx.User}"
        }).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.TimeoutRemoved(ctx.Guild.Id, user.Mention)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Warns a user with an optional reason
    /// </summary>
    /// <param name="user">The user to warn</param>
    /// <param name="reason">The reason for the warn</param>
    [SlashCommand("warn", "Warn a user with an optional reason")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.BanMembers)]
    [CheckPermissions]
    public async Task Warn(IGuildUser user, string? reason = null)
    {
        if (!await CheckRoleHierarchy(user))
            return;

        var dmFailed = false;
        try
        {
            await (await user.CreateDMChannelAsync().ConfigureAwait(false)).EmbedAsync(new EmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(Strings.WarnedOn(ctx.Guild.Id, ctx.Guild.ToString()))
                    .AddField(efb => efb.WithName(Strings.Moderator(ctx.Guild.Id)).WithValue(ctx.User.ToString()))
                    .AddField(efb => efb.WithName(Strings.Reason(ctx.Guild.Id)).WithValue(reason ?? "-")))
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
            logger.LogWarning(ex.Message);
            var errorEmbed = new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(Strings.CantApplyPunishment(ctx.Guild.Id));

            if (dmFailed) errorEmbed.WithFooter($"⚠️ {Strings.UnableToDmUser(ctx.Guild.Id)}");

            await ctx.Interaction.RespondAsync(embed: errorEmbed.Build());
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor();
        if (punishment is null || punishment.Id is 0)
        {
            embed.WithDescription(Strings.UserWarned(ctx.Guild.Id,
                Format.Bold(user.ToString())));
        }
        else
        {
            embed.WithDescription(Strings.UserWarnedAndPunished(ctx.Guild.Id, Format.Bold(user.ToString()),
                Format.Bold(punishment.Punishment.ToString())));
        }

        if (dmFailed) embed.WithFooter($"⚠️ {Strings.UnableToDmUser(ctx.Guild.Id)}");

        await ctx.Interaction.RespondAsync(embed: embed.Build());
        if (await Service.GetWarnlogChannel(ctx.Guild.Id) != 0)
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var warnings = await db.Warnings
                .Where(w => w.GuildId == ctx.Guild.Id && w.UserId == user.Id && !w.Forgiven)
                .CountAsync();

            var condition = punishment != null;
            var punishtime = condition ? TimeSpan.FromMinutes(punishment.Time).ToString() : " ";
            var punishaction = condition ? punishment.Punishment.Humanize() : "None";
            var channel = await ctx.Guild.GetTextChannelAsync(await Service.GetWarnlogChannel(ctx.Guild.Id));
            var originalResponse = await ctx.Interaction.GetOriginalResponseAsync().ConfigureAwait(false);
            await channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                .WithThumbnailUrl(user.RealAvatarUrl().ToString())
                .WithTitle(Strings.WarnLogTitle(ctx.Guild.Id, ctx.User))
                .WithDescription(Strings.WarnLogDescription(
                    ctx.Guild.Id,
                    user.Username,
                    user.Discriminator,
                    user.Id,
                    warnings,
                    punishaction,
                    punishtime,
                    reason,
                    originalResponse.GetJumpUrl())));
        }
    }

    /// <summary>
    ///     Sets the amount of days before warns expire
    /// </summary>
    /// <param name="days">The days (max of 366) a warn should expire</param>
    /// <param name="action">Whether to delete warns instead of clearing them</param>
    [SlashCommand("setwarnexpire", "Set when warns expire in days")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task WarnExpire(int days,
        [Summary("todelete", "Set whether warns are or cleared.")]
        WarnExpireAction action)
    {
        if (days is < 0 or > 366)
            return;

        await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

        await Service.WarnExpireAsync(ctx.Guild.Id, days, action).ConfigureAwait(false);
        if (days == 0)
        {
            await ReplyConfirmAsync(Strings.WarnExpireReset(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (action == WarnExpireAction.Delete)
        {
            await ReplyConfirmAsync(Strings.WarnExpireSetDelete(ctx.Guild.Id, Format.Bold(days.ToString())))
                .ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.WarnExpireSetClear(ctx.Guild.Id, Format.Bold(days.ToString())))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Checks the amount of warns a user has
    /// </summary>
    /// <param name="user">The user to check the warns of</param>
    /// <returns></returns>
    [SlashCommand("warnlog", "Check a users warn amount")]
    [RequireContext(ContextType.Guild)]
    public async Task Warnlog(IGuildUser? user = null)
    {
        user ??= (IGuildUser)ctx.User;
        if (ctx.User.Id == user.Id || ((IGuildUser)ctx.User).GuildPermissions.BanMembers)
            await InternalWarnlog(user.Id);
        await ctx.Interaction.SendEphemeralErrorAsync(
            Strings.MissingPermissionsViewWarns(ctx.Guild.Id), Config);
    }

    private async Task InternalWarnlog(ulong userId)
    {
        var warnings = await Service.UserWarnings(ctx.Guild.Id, userId);
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(warnings.Length / 9)
            .WithDefaultCanceledPage()
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();
        await interactivity.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            warnings = warnings.Skip(page)
                .Take(9)
                .ToArray();

            var embed = new PageBuilder().WithOkColor()
                .WithTitle(Strings.WarnlogFor(ctx.Guild.Id,
                    (ctx.Guild as SocketGuild)?.GetUser(userId)?.ToString() ?? userId.ToString()))
                .WithFooter(efb => efb.WithText(Strings.Page(ctx.Guild.Id, page + 1)));

            if (warnings.Length == 0)
            {
                embed.WithDescription(Strings.WarningsNone(ctx.Guild.Id));
            }
            else
            {
                var i = page * 9;
                foreach (var w in warnings)
                {
                    i++;
                    var name = Strings.WarnedOnBy(ctx.Guild.Id, $"<t:{w.DateAdded.Value.ToUnixEpochDate()}:D>",
                        $"<t:{w.DateAdded.Value.ToUnixEpochDate()}:T>", w.Moderator);
                    if (w.Forgiven)
                        name = $"{Format.Strikethrough(name)} {Strings.WarnClearedBy(ctx.Guild.Id, w.ForgivenBy)}";

                    embed.AddField(x => x
                        .WithName($"#`{i}` {name}")
                        .WithValue(w.Reason.TrimTo(1020)));
                }
            }

            return embed;
        }
    }

    /// <summary>
    ///     Checks the amount of warns all users have
    /// </summary>
    [SlashCommand("warnlogall", "Show the warn count of all users in the server.")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.BanMembers)]
    [CheckPermissions]
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

        await interactivity.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

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
                    .WithTitle(Strings.WarningsList(ctx.Guild.Id))
                    .WithDescription(string.Join("\n", ws));
            }
        }
    }

    /// <summary>
    ///     Clears all or a specific warn for a user
    /// </summary>
    /// <param name="user">The user to clear the warn for</param>
    /// <param name="index">The index of the warn to clear</param>
    [SlashCommand("warnclear", "Clear all or a specific warn for a user.")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.BanMembers)]
    [CheckPermissions]
    public async Task Warnclear(IGuildUser user, int index = 0)
    {
        if (index < 0)
            return;
        if (!await CheckRoleHierarchy(user))
            return;
        var success = await Service.WarnClearAsync(ctx.Guild.Id, user.Id, index, ctx.User.ToString())
            .ConfigureAwait(false);
        var userStr = user.ToString();
        if (index == 0)
        {
            await ReplyConfirmAsync(Strings.WarningsCleared(ctx.Guild.Id, userStr)).ConfigureAwait(false);
        }
        else
        {
            if (success)
            {
                await ReplyConfirmAsync(Strings.WarningCleared(ctx.Guild.Id, Format.Bold(index.ToString()), userStr))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.WarningClearFail(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Sets what each warn count does
    /// </summary>
    /// <param name="number">The number of warns to set the punishment for</param>
    /// <param name="punish">The punishment to set</param>
    /// <param name="input">The time to set the punishment for</param>
    [SlashCommand("warnpunish", "Set what each warn count does.")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.BanMembers)]
    [CheckPermissions]
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
                await ctx.Interaction.SendErrorAsync(Strings.InvalidTimeFormat(ctx.Guild.Id),
                    Config);
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

            await ReplyConfirmAsync(Strings.WarnPunishRem(ctx.Guild.Id,
                Format.Bold(number.ToString()))).ConfigureAwait(false);
            return;
        }

        var success = await Service.WarnPunish(ctx.Guild.Id, number, (int)punish, time);

        if (!success)
            return;
        switch (punish)
        {
            case PunishmentAction.Timeout when time?.Time.Days > 28:
                await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            case PunishmentAction.Timeout when time.Time.TotalSeconds is 0:
                await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                return;
        }

        if (time.Time.TotalSeconds is 0)
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
    ///     Shows the warn punishment list
    /// </summary>
    [SlashCommand("warnpunishlist", "See how many warns does what")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task WarnPunishList()
    {
        var ps = await Service.WarnPunishList(ctx.Guild.Id);

        string? list;
        if (ps.Length > 0)
        {
            list = string.Join("\n",
                ps.Select(x =>
                    $"{x.Count} -> {x.Punishment} {(x.Punishment == (int)PunishmentAction.AddRole ? $"<@&{x.RoleId}>" : "")} {(x.Time <= 0 ? "" : $"{x.Time}m")} "));
        }
        else
        {
            list = Strings.WarnplNone(ctx.Guild.Id);
        }

        await ctx.Interaction.SendConfirmAsync(
            Strings.WarnPunishList(ctx.Guild.Id), list).ConfigureAwait(false);
    }


    /// <summary>
    ///     Bans a user by their ID if they are not in the server
    /// </summary>
    /// <param name="userId">The user or user ID to ban</param>
    /// <param name="msg">The reason for the ban</param>
    /// <param name="time">The duration of the ban</param>
    [SlashCommand("hackban", "Bans a user by their ID")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.BanMembers)]
    [BotPerm(GuildPermission.BanMembers)]
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
                await ctx.Interaction.SendErrorAsync(Strings.InvalidTimeFormat(ctx.Guild.Id),
                    Config);
                return;
            }

            await InternalBanAsync(userId.Id, reason: msg, time: stoopid.Time, hackBan: true);
        }
        else
        {
            await InternalBanAsync(userId.Id, reason: msg, hackBan: true);
        }
    }

    /// <summary>
    ///     Bans a user in the server with an optional time and reason
    /// </summary>
    /// <param name="user">The user to ban</param>
    /// <param name="reason">The reason for the ban</param>
    /// <param name="time">The duration of the ban</param>
    [SlashCommand("ban", "Bans a user by their ID")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.BanMembers)]
    [BotPerm(GuildPermission.BanMembers)]
    public async Task Ban(IGuildUser? user, string reason = null, string time = null)
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
                await ctx.Interaction.SendErrorAsync(Strings.InvalidTimeFormat(ctx.Guild.Id),
                    Config);
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
        IGuildUser? user = null)
    {
        if (hackBan)
        {
            if (time != default)
            {
                await ctx.Guild.AddBanAsync(userId, time.Days, options: new RequestOptions
                {
                    AuditLogReason = $"{ctx.User} | {reason}"
                }).ConfigureAwait(false);

                await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                        .WithTitle($"⛔️ {Strings.BannedUser(ctx.Guild.Id)}")
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
                        .WithTitle($"⛔️ {Strings.BannedUser(ctx.Guild.Id)}")
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
                    var defaultMessage = Strings.Bandm(ctx.Guild.Id, Format.Bold(ctx.Guild.Name), reason);
                    var (embedBuilder, message, components) = await Service
                        .GetBanUserDmEmbed(Context, user, defaultMessage, reason, null).ConfigureAwait(false);
                    if (embedBuilder is not null || message is not null)
                    {
                        var userChannel = await user.CreateDMChannelAsync().ConfigureAwait(false);
                        await userChannel
                            .SendMessageAsync(message, embeds: embedBuilder, components: components?.Build())
                            .ConfigureAwait(false);
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
                    .WithTitle($"⛔️ {Strings.BannedUser(ctx.Guild.Id)}")
                    .AddField(efb =>
                        efb.WithName(Strings.Username(ctx.Guild.Id)).WithValue(user.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
                    .WithImageUrl((await nekos.ActionsApi.Kick().ConfigureAwait(false)).Results.First().Url);

                if (dmFailed) toSend.WithFooter($"⚠️ {Strings.UnableToDmUser(ctx.Guild.Id)}");

                await ctx.Interaction.RespondAsync(embed: toSend.Build())
                    .ConfigureAwait(false);
            }
            else
            {
                var dmFailed = false;

                try
                {
                    var defaultMessage = Strings.Bandm(ctx.Guild.Id, Format.Bold(ctx.Guild.Name), reason);
                    var (embedBuilder, message, components) = await Service
                        .GetBanUserDmEmbed(ctx, user, defaultMessage, reason, null).ConfigureAwait(false);
                    if (embedBuilder is not null || message is not null)
                    {
                        var userChannel = await user.CreateDMChannelAsync().ConfigureAwait(false);
                        await userChannel
                            .SendMessageAsync(message, embeds: embedBuilder, components: components?.Build())
                            .ConfigureAwait(false);
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
                    .WithTitle($"⛔️ {Strings.BannedUser(ctx.Guild.Id)}")
                    .AddField(efb =>
                        efb.WithName(Strings.Username(ctx.Guild.Id)).WithValue(user.ToString()).WithIsInline(true))
                    .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
                    .WithImageUrl((await nekos.ActionsApi.Kick().ConfigureAwait(false)).Results.First().Url);

                if (dmFailed) toSend.WithFooter($"⚠️ {Strings.UnableToDmUser(ctx.Guild.Id)}");

                await ctx.Interaction.RespondAsync(embed: toSend.Build())
                    .ConfigureAwait(false);
            }
        }
    }


    /// <summary>
    ///     Unbans a user by their ID
    /// </summary>
    /// <param name="userId">The user ID to unban</param>
    [SlashCommand("unban", "Unban a user.")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.BanMembers)]
    [BotPerm(GuildPermission.BanMembers)]
    [CheckPermissions]
    public async Task Unban(ulong userId)
    {
        var bun = await Context.Guild.GetBanAsync(userId);

        if (bun == null)
        {
            await ReplyErrorAsync(Strings.UserNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await UnbanInternal(bun.User).ConfigureAwait(false);
    }

    private async Task UnbanInternal(IUser user)
    {
        await ctx.Guild.RemoveBanAsync(user).ConfigureAwait(false);

        await ReplyConfirmAsync(Strings.UnbannedUser(ctx.Guild.Id, Format.Bold(user.ToString()))).ConfigureAwait(false);
    }

    /// <summary>
    ///     Bans then unbans a user, usually used to remove messages and just kick the user
    /// </summary>
    /// <param name="user">The user to softban</param>
    /// <param name="msg">The reason for the softban</param>
    /// <returns></returns>
    [SlashCommand("softban", "Bans then unbans a user.")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.KickMembers | GuildPermission.ManageMessages)]
    [BotPerm(GuildPermission.BanMembers)]
    [CheckPermissions]
    public Task Softban(IGuildUser user, string? msg = null)
    {
        return SoftbanInternal(user, msg);
    }

    private async Task SoftbanInternal(IGuildUser user, string? msg = null)
    {
        if (!await CheckRoleHierarchy(user).ConfigureAwait(false))
            return;

        var dmFailed = false;

        try
        {
            await user.SendErrorAsync(Strings.Sbdm(ctx.Guild.Id, Format.Bold(ctx.Guild.Name), msg))
                .ConfigureAwait(false);
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
            .WithTitle($"☣ {Strings.SbUser(ctx.Guild.Id)}")
            .AddField(efb => efb.WithName(Strings.Username(ctx.Guild.Id)).WithValue(user.ToString()).WithIsInline(true))
            .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
            .WithImageUrl((await nekos.ActionsApi.Kick().ConfigureAwait(false)).Results.First().Url);

        if (dmFailed) toSend.WithFooter($"⚠️ {Strings.UnableToDmUser(ctx.Guild.Id)}");

        await ctx.Interaction.RespondAsync(embed: toSend.Build())
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Kicks a user with an optional reason
    /// </summary>
    /// <param name="user">The user to kick</param>
    /// <param name="msg">The reason for the kick</param>
    /// <returns></returns>
    [SlashCommand("kick", "Kicks a user with an optional reason")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.KickMembers)]
    [BotPerm(GuildPermission.KickMembers)]
    public Task Kick(IGuildUser user, string? msg = null)
    {
        return KickInternal(user, msg);
    }


    private async Task KickInternal(IGuildUser user, string? msg = null)
    {
        if (!await CheckRoleHierarchy(user).ConfigureAwait(false))
            return;

        var dmFailed = false;

        try
        {
            await user.SendErrorAsync(Strings.Kickdm(ctx.Guild.Id, Format.Bold(ctx.Guild.Name), msg))
                .ConfigureAwait(false);
        }
        catch
        {
            dmFailed = true;
        }

        await user.KickAsync($"{ctx.User} | {msg}").ConfigureAwait(false);

        var toSend = new EmbedBuilder().WithOkColor()
            .WithTitle(Strings.KickedUser(ctx.Guild.Id))
            .AddField(efb => efb.WithName(Strings.Username(ctx.Guild.Id)).WithValue(user.ToString()).WithIsInline(true))
            .AddField(efb => efb.WithName("ID").WithValue(user.Id.ToString()).WithIsInline(true))
            .WithImageUrl((await nekos.ActionsApi.Kick().ConfigureAwait(false)).Results.First().Url);

        if (dmFailed) toSend.WithFooter($"⚠️ {Strings.UnableToDmUser(ctx.Guild.Id)}");

        await ctx.Interaction.RespondAsync(embed: toSend.Build())
            .ConfigureAwait(false);
    }
}