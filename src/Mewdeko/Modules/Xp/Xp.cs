using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Gambling.Services;
using Mewdeko.Modules.Xp.Common;
using Mewdeko.Modules.Xp.Services;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Xp;

public partial class Xp : MewdekoModuleBase<XpService>
{
    public enum Channel
    {
        Channel
    }

    public enum NotifyPlace
    {
        Server = 0,
        Guild = 0,
        Global = 1
    }

    public enum Role
    {
        Role
    }

    public enum Server
    {
        Server
    }
    private readonly DownloadTracker _tracker;
    private readonly XpConfigService _xpConfig;
    private readonly InteractiveService _interactivity;
    private readonly GamblingConfigService _gss;

    public Xp(DownloadTracker tracker, XpConfigService xpconfig, InteractiveService serv,
        GamblingConfigService gss)
    {
        _xpConfig = xpconfig;
        _tracker = tracker;
        _interactivity = serv;
        _gss = gss;
    }

    private async Task SendXpSettings(ITextChannel chan)
    {
        var list = new List<XpStuffs>();
        if (Service.GetTxtXpRate(ctx.Guild.Id) == 0)
        {
            var toadd = new XpStuffs
            {
                Setting = "xptextrate",
                Value = $"{_xpConfig.Data.XpPerMessage} (Global Default)"
            };
            list.Add(toadd);
        }
        else
        {
            var toadd = new XpStuffs
            {
                Setting = "xptextrate",
                Value = $"{Service.GetTxtXpRate(ctx.Guild.Id)} (Server Set)"
            };
            list.Add(toadd);
        }

        if (Service.GetVoiceXpRate(ctx.Guild.Id) == 0)
        {
            var toadd = new XpStuffs
            {
                Setting = "voicexprate",
                Value = $"{_xpConfig.Data.VoiceXpPerMinute} (Global Default)"
            };
            list.Add(toadd);
        }
        else
        {
            var toadd = new XpStuffs
            {
                Setting = "xpvoicerate",
                Value = $"{Service.GetVoiceXpRate(ctx.Guild.Id)} (Server Set)"
            };
            list.Add(toadd);
        }

        if (Service.GetXpTimeout(ctx.Guild.Id) == 0)
        {
            var toadd = new XpStuffs
            {
                Setting = "txtxptimeout",
                Value = $"{_xpConfig.Data.MessageXpCooldown} (Global Default)"
            };
            list.Add(toadd);
        }
        else
        {
            var toadd = new XpStuffs
            {
                Setting = "txtxptimeout",
                Value = $"{Service.GetXpTimeout(ctx.Guild.Id)} (Server Set)"
            };
            list.Add(toadd);
        }

        if (Service.GetVoiceXpTimeout(ctx.Guild.Id) == 0)
        {
            var toadd = new XpStuffs
            {
                Setting = "voiceminutestimeout",
                Value = $"{_xpConfig.Data.VoiceMaxMinutes} (Global Default)"
            };
            list.Add(toadd);
        }
        else
        {
            var toadd = new XpStuffs
            {
                Setting = "voiceminutestimeout",
                Value = $"{Service.GetXpTimeout(ctx.Guild.Id)} (Server Set)"
            };
            list.Add(toadd);
        }

        var strings = new List<string>();
        foreach (var i in list) strings.Add($"{i.Setting,-25} = {i.Value}\n");

        await chan.SendConfirmAsync(Format.Code(string.Concat(strings), "hs")).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageGuild)]
    public async Task XpSetting(string? setting = null, int value = 999999999)
    {
        if (value < 0) return;
        if (setting is null)
        {
            await SendXpSettings(ctx.Channel as ITextChannel).ConfigureAwait(false);
            return;
        }

        if (setting != null && setting.ToLower() == "xptextrate")
        {
            if (value is not 999999999 and not 0)
            {
                await Service.XpTxtRateSet(ctx.Guild, value).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync($"Users will now recieve {value} xp per message.").ConfigureAwait(false);
            }

            if (value is 999999999 or 0)
            {
                await Service.XpTxtRateSet(ctx.Guild, 0).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync("User xp per message will now be the global default.").ConfigureAwait(false);
            }

            return;
        }

        if (setting != null && setting.ToLower() == "txtxptimeout")
        {
            if (value is not 999999999 and not 0)
            {
                await Service.XpTxtTimeoutSet(ctx.Guild, value).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync($"Message XP will be given every {value} minutes.").ConfigureAwait(false);
            }

            if (value is 999999999 or 0)
            {
                await Service.XpTxtTimeoutSet(ctx.Guild, 0).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync("XP Timeout will now follow the global default.").ConfigureAwait(false);
            }

            return;
        }

        if (setting != null && setting.ToLower() == "xpvoicerate")
        {
            if (value is not 999999999 and not 0)
            {
                await Service.XpVoiceRateSet(ctx.Guild, value).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync(
                    $"Users will now recieve {value} every minute they are in voice. Make sure to set voiceminutestimeout or this is usless.").ConfigureAwait(false);
            }

            if (value is 999999999 or 0)
            {
                await Service.XpVoiceRateSet(ctx.Guild, 0).ConfigureAwait(false);
                await Service.XpVoiceTimeoutSet(ctx.Guild, 0).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync("Voice XP Disabled.").ConfigureAwait(false);
            }

            return;
        }

        if (setting != null && setting.ToLower() == "voiceminutestimeout")
        {
            if (value is not 999999999 and not 0)
            {
                await Service.XpVoiceTimeoutSet(ctx.Guild, value).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync(
                    $"XP will now stop being given in vc after {value} minutes. Make sure to set voicexprate or this is useless.").ConfigureAwait(false);
            }

            if (value is 999999999 or 0)
            {
                await Service.XpVoiceRateSet(ctx.Guild, 0).ConfigureAwait(false);
                await Service.XpVoiceTimeoutSet(ctx.Guild, 0).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync("Voice XP Disabled.").ConfigureAwait(false);
            }
        }
        else
        {
            await ctx.Channel.SendErrorAsync(
                "The setting name you provided does not exist! The available settings and their descriptions:\n\n" +
                "`xptextrate`: Alows you to set the xp per message rate.\n" +
                "`txtxptimeout`: Allows you to set after how many minutes xp is given so users cant spam for xp.\n" +
                "`xpvoicerate`: Allows you to set how much xp a person gets in vc per minute.\n" +
                "`voiceminutestimeout`: Allows you to set the maximum time a user can remain in vc while gaining xp.").ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Experience([Remainder] IGuildUser? user = null)
    {
        user ??= ctx.User as IGuildUser;
        await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
        var (img, fmt) = await Service.GenerateXpImageAsync(user).ConfigureAwait(false);
        await using (img.ConfigureAwait(false))
        {
            await ctx.Channel.SendFileAsync(img,
                    $"{ctx.Guild.Id}_{user.Id}_xp.{fmt.FileExtensions.FirstOrDefault()}")
                .ConfigureAwait(false);
            await img.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task XpLevelUpRewards()
    {
        var allRewards = (await Service.GetRoleRewards(ctx.Guild.Id))
            .OrderBy(x => x.Level)
            .Select(x =>
            {
                var str = ctx.Guild.GetRole(x.RoleId)?.ToString();
                if (str != null)
                    str = GetText("role_reward", Format.Bold(str));
                return (x.Level, RoleStr: str);
            })
            .Where(x => x.RoleStr != null)
            .GroupBy(x => x.Level)
            .OrderBy(x => x.Key)
            .ToList();

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(allRewards.Count / 9)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await _interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var embed = new PageBuilder()
                .WithTitle(GetText("level_up_rewards"))
                .WithOkColor();

            var localRewards = allRewards
                .Skip(page * 9)
                .Take(9)
                .ToList();

            if (localRewards.Count == 0)
                return embed.WithDescription(GetText("no_level_up_rewards"));

            foreach (var reward in localRewards)
            {
                embed.AddField(GetText("level_x", reward.Key),
                    string.Join("\n", reward.Select(y => y.Item2)));
            }

            return embed;
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator),
     RequireContext(ContextType.Guild)]
    public async Task XpRoleReward(int level, [Remainder] IRole? role = null)
    {
        if (level < 1)
            return;

        Service.SetRoleReward(ctx.Guild.Id, level, role?.Id);

        if (role == null)
        {
            await ReplyConfirmLocalizedAsync("role_reward_cleared", level).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("role_reward_added", level, Format.Bold(role.ToString()))
                        .ConfigureAwait(false);
        }
    }

    private string? GetNotifLocationString(XpNotificationLocation loc)
    {
        if (loc == XpNotificationLocation.Channel) return GetText("xpn_notif_channel");

        if (loc == XpNotificationLocation.Dm) return GetText("xpn_notif_dm");

        return GetText("xpn_notif_disabled");
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task XpNotify()
    {
        var serverSetting = await Service.GetNotificationType(ctx.User.Id, ctx.Guild.Id);

        var embed = new EmbedBuilder()
            .WithOkColor()
            .AddField(GetText("xpn_setting_server"), GetNotifLocationString(serverSetting));

        await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task XpNotify(NotifyPlace place, XpNotificationLocation type)
    {
        if (place == NotifyPlace.Guild)
            await Service.ChangeNotificationType(ctx.User.Id, ctx.Guild.Id, type).ConfigureAwait(false);
        else
            await Service.ChangeNotificationType(ctx.User, type).ConfigureAwait(false);

        await ctx.OkAsync().ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator)]
    public async Task XpExclude(Server _)
    {
        var ex = await Service.ToggleExcludeServer(ctx.Guild.Id);

        await ReplyConfirmLocalizedAsync(ex ? "excluded" : "not_excluded", Format.Bold(ctx.Guild.ToString()))
            .ConfigureAwait(false);
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageRoles),
     RequireContext(ContextType.Guild)]
    public async Task XpExclude(Role _, [Remainder] IRole role)
    {
        var ex = await Service.ToggleExcludeRole(ctx.Guild.Id, role.Id);

        await ReplyConfirmLocalizedAsync(ex ? "excluded" : "not_excluded", Format.Bold(role.ToString()))
            .ConfigureAwait(false);
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels),
     RequireContext(ContextType.Guild)]
    public async Task XpExclude(Channel _, [Remainder] IChannel? channel = null)
    {
        if (channel == null)
            channel = ctx.Channel;

        var ex = await Service.ToggleExcludeChannel(ctx.Guild.Id, channel.Id);

        await ReplyConfirmLocalizedAsync(ex ? "excluded" : "not_excluded", Format.Bold(channel.ToString()))
            .ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task XpExclusionList()
    {
        var serverExcluded = Service.IsServerExcluded(ctx.Guild.Id);
        var roles = Service.GetExcludedRoles(ctx.Guild.Id)
            .Select(x => ctx.Guild.GetRole(x))
            .Where(x => x != null)
            .Select(x => $"`role`   {x.Mention}")
            .ToList();

        var chans = (await Task.WhenAll(Service.GetExcludedChannels(ctx.Guild.Id)
                    .Select(x => ctx.Guild.GetChannelAsync(x)))
                .ConfigureAwait(false))
            .Where(x => x != null)
            .Select(x => $"`channel` {x.Name}")
            .ToList();

        var rolesStr = roles.Count > 0 ? $"{string.Join("\n", roles)}\n" : string.Empty;
        var chansStr = chans.Count > 0 ? $"{string.Join("\n", chans)}\n" : string.Empty;
        var desc = Format.Code(serverExcluded
            ? GetText("server_is_excluded")
            : GetText("server_is_not_excluded"));

        desc += $"\n\n{rolesStr}{chansStr}";

        var lines = desc.Split('\n');
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(lines.Length / 15)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await _interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder()
                .WithTitle(GetText("exclusion_list"))
                .WithDescription(string.Join('\n', lines.Skip(15 * page).Take(15)))
                .WithOkColor();
        }
    }

    [Cmd, Aliases, MewdekoOptions(typeof(LbOpts)), Priority(1),
     RequireContext(ContextType.Guild)]
    public async Task XpLeaderboard(params string[] args)
    {
        var (opts, _) = OptionsParser.ParseFrom(new LbOpts(), args);

        await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

        var socketGuild = (SocketGuild)ctx.Guild;
        List<UserXpStats> allUsers;
        if (opts.Clean)
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            await _tracker.EnsureUsersDownloadedAsync(ctx.Guild).ConfigureAwait(false);

            allUsers = (await Service.GetTopUserXps(ctx.Guild.Id))
                .Where(user => socketGuild.GetUser(user.UserId) is not null)
                .ToList();
        }
        else
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            await _tracker.EnsureUsersDownloadedAsync(ctx.Guild).ConfigureAwait(false);
            allUsers = (await Service.GetTopUserXps(ctx.Guild.Id)).ToList();
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(allUsers.Count / 9)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await _interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var embed = new PageBuilder()
                .WithTitle(GetText("server_leaderboard"))
                .WithOkColor();

            List<UserXpStats> users;
            if (opts.Clean)
                users = allUsers.Skip(page * 9).Take(9).ToList();
            else
                users = await Service.GetUserXps(ctx.Guild.Id, page);

            if (users.Count == 0) return embed.WithDescription("-");

            for (var i = 0; i < users.Count; i++)
            {
                var levelStats = new LevelStats(users[i].Xp + users[i].AwardedXp);
                var user = ((SocketGuild)ctx.Guild).GetUser(users[i].UserId);

                var userXpData = users[i];

                var awardStr = "";
                if (userXpData.AwardedXp > 0)
                    awardStr = $"(+{userXpData.AwardedXp})";
                else if (userXpData.AwardedXp < 0)
                    awardStr = $"({userXpData.AwardedXp})";

                embed.AddField(
                    $"#{i + 1 + (page * 9)} {user?.ToString() ?? users[i].UserId.ToString()}",
                    $"{GetText("level_x", levelStats.Level)} - {levelStats.TotalXp}xp {awardStr}");
            }

            return embed;
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator)]
    public async Task XpAdd(int amount, ulong userId)
    {
        if (amount == 0)
            return;

        await Service.AddXp(userId, ctx.Guild.Id, amount);
        var usr = ((SocketGuild)ctx.Guild).GetUser(userId)?.ToString()
                  ?? userId.ToString();
        await ReplyConfirmLocalizedAsync("modified", Format.Bold(usr), Format.Bold(amount.ToString()))
            .ConfigureAwait(false);
    }
    [Cmd, Aliases, RequireContext(ContextType.Guild), OwnerOnly]
    public async Task XpCurrencyReward(int level, int amount = 0)
    {
        if (level < 1 || amount < 0)
            return;

        Service.SetCurrencyReward(ctx.Guild.Id, level, amount);
        var config = _gss.Data;

        if (amount == 0)
        {
            await ReplyConfirmLocalizedAsync("cur_reward_cleared", level, config.Currency.Sign)
                .ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("cur_reward_added",
                    level, Format.Bold(amount + config.Currency.Sign))
                .ConfigureAwait(false);
        }
    }
    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator)]
    public Task XpAdd(int amount, [Remainder] IGuildUser user) => XpAdd(amount, user.Id);

    [Cmd, Aliases, RequireContext(ContextType.Guild), OwnerOnly]
    public async Task XpTemplateReload()
    {
        Service.ReloadXpTemplate();
        await Task.Delay(1000).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("template_reloaded").ConfigureAwait(false);
    }

    private class XpStuffs
    {
        public string Setting { get; set; }
        public string Value { get; set; }
    }
}