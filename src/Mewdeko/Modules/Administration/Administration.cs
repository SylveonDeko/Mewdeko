using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Humanizer.Localisation;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Services.Settings;
using Serilog;

namespace Mewdeko.Modules.Administration;

public partial class Administration : MewdekoModuleBase<AdministrationService>
{
    public enum Channel
    {
        Channel,
        Ch,
        Chnl,
        Chan
    }

    public enum List
    {
        List = 0,
        Ls = 0
    }

    public enum Server
    {
        Server
    }

    public enum State
    {
        Enable,
        Disable,
        Inherit
    }

    private readonly InteractiveService interactivity;
    private readonly GuildSettingsService guildSettingsService;
    private readonly BotConfigService configService;
    private readonly DownloadTracker downloadTracker;

    public Administration(InteractiveService serv, GuildSettingsService guildSettingsService, BotConfigService configService, DownloadTracker downloadTracker)
    {
        interactivity = serv;
        this.guildSettingsService = guildSettingsService;
        this.configService = configService;
        this.downloadTracker = downloadTracker;
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator)]
    public async Task GuildStatsOptOut()
    {
        var optout = await Service.ToggleOptOut(ctx.Guild);
        if (!optout)
            await ctx.Channel.SendConfirmAsync("Succesfully enabled command stats collection! (This does ***not*** collect message contents!***)");
        else
            await ctx.Channel.SendConfirmAsync("Succesfully disable command stats collection.");
    }

    [Cmd, Aliases, Ratelimit(3600), UserPerm(GuildPermission.Administrator)]
    public async Task DeleteGuildStatsData()
    {
        if (await PromptUserConfirmAsync("Are you sure you want to delete your command stats? This action is irreversible!", ctx.User.Id))
        {
            if (await Service.DeleteStatsData(ctx.Guild))
                await ctx.Channel.SendErrorAsync("Command Stats deleted.");
            else
                await ctx.Channel.SendErrorAsync("There was no data to delete.");
        }
    }

    [Cmd, BotPerm(GuildPermission.ManageNicknames), UserPerm(GuildPermission.ManageNicknames), Priority(1)]
    public async Task SetNick(IGuildUser gu, [Remainder] string? newNick = null)
    {
        var sg = (SocketGuild)Context.Guild;
        if (sg.OwnerId == gu.Id || gu.GetRoles().Max(r => r.Position) >= sg.CurrentUser.GetRoles().Max(r => r.Position))
        {
            await ReplyErrorLocalizedAsync("insuf_perms_i").ConfigureAwait(false);
            return;
        }

        await gu.ModifyAsync(u => u.Nickname = newNick).ConfigureAwait(false);

        await ReplyConfirmLocalizedAsync("user_nick", Format.Bold(gu.ToString()), Format.Bold(newNick) ?? "-").ConfigureAwait(false);
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageNicknames), BotPerm(GuildPermission.ChangeNickname), Priority(0)]
    public async Task SetNick([Remainder] string? newNick = null)
    {
        if (string.IsNullOrWhiteSpace(newNick))
            return;
        var curUser = await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false);
        await curUser.ModifyAsync(u => u.Nickname = newNick).ConfigureAwait(false);

        await ReplyConfirmLocalizedAsync("bot_nick", Format.Bold(newNick) ?? "-").ConfigureAwait(false);
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), BotPerm(GuildPermission.BanMembers)]
    public async Task NameBan([Remainder] string name)
    {
        var regex = new Regex(name, RegexOptions.Compiled, matchTimeout: TimeSpan.FromMilliseconds(200));
        var users = await ctx.Guild.GetUsersAsync();
        users = users.Where(x => regex.IsMatch(x.Username.ToLower())).ToList();
        if (!users.Any())
        {
            await ctx.Channel.SendErrorAsync($"{configService.Data.ErrorEmote} No users with that name found! Please try again with a different query.");
            return;
        }

        var components = new ComponentBuilder().WithButton("Preview", "previewbans").WithButton("Execute", "executeorder66");
        var eb = new EmbedBuilder()
            .WithDescription("Preview bans or Execute bans?")
            .WithOkColor();
        var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build(), components: components.Build());
        var input = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id);
        switch (input)
        {
            case "previewbans":
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(users.Count / 20)
                    .WithDefaultCanceledPage()
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage).Build();
                await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    return new PageBuilder().WithTitle($"Previewing {users.Count} users who's names contain {name.ToLower()}")
                        .WithDescription(string.Join("\n", users.Skip(page * 20).Take(20)));
                }

                break;
            case "executeorder66":
                if (await PromptUserConfirmAsync($"Are you sure you want to ban {users.Count} users?", ctx.User.Id))
                {
                    int failedUsers = 0;
                    await ctx.Channel.SendConfirmAsync($"{configService.Data.LoadingEmote} executing order 66 on {users.Count} users, this may take a bit...");
                    foreach (var i in users)
                    {
                        try
                        {
                            await ctx.Guild.AddBanAsync(i, 0, "", options: new RequestOptions
                            {
                                AuditLogReason = $"Mass ban requested by {ctx.User}."
                            });
                        }
                        catch
                        {
                            failedUsers++;
                        }
                    }

                    await ctx.Channel.SendConfirmAsync(
                        $"{configService.Data.SuccessEmote} executed order 66 on {users.Count - failedUsers}. Failed to ban {failedUsers} users (Probably due to bad role heirarchy).");
                }

                break;
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), BotPerm(GuildPermission.BanMembers)]
    public async Task BanUnder(StoopidTime time, string? option = null, StoopidTime? time1 = null)
    {
        try
        {
            await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
            IEnumerable<IUser> users;
            if (option is not null && option.ToLower() == "-accage" && time1 is not null)
            {
                users = ((SocketGuild)ctx.Guild).Users.Where(c =>
                    c.JoinedAt != null
                    && DateTimeOffset.Now.Subtract(c.JoinedAt.Value).TotalSeconds <= time.Time.TotalSeconds
                    && DateTimeOffset.Now.Subtract(c.CreatedAt).TotalSeconds <= time1.Time.TotalSeconds);
            }
            else
            {
                users = ((SocketGuild)ctx.Guild).Users.Where(c => c.JoinedAt != null && DateTimeOffset.Now.Subtract(c.JoinedAt.Value).TotalSeconds <= time.Time.TotalSeconds);
            }

            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync("No users at or under that server join age!").ConfigureAwait(false);
                return;
            }

            if (option is not null && option.ToLower() == "-p")
            {
                var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory).WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(users.Count() / 20).WithDefaultCanceledPage().WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage).Build();
                await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    return new PageBuilder().WithTitle($"Previewing {users.Count()} users who's accounts joined under {time.Time.Humanize(maxUnit: TimeUnit.Year)} ago")
                        .WithDescription(string.Join("\n", users.Skip(page * 20).Take(20)));
                }
            }

            var banned = 0;
            var errored = 0;
            var msg = await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder().WithErrorColor()
                .WithDescription(
                    $"Are you sure you want to ban {users.Count()} users that are under that server join age? Say `yes` to continue.")
                .Build());
            var text = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            await msg.DeleteAsync();
            if (!text.ToLower().Contains("yes")) return;
            var message = await ctx.Channel.SendConfirmAsync($"Banning {users.Count()} users..").ConfigureAwait(false);
            foreach (var i in users)
            {
                try
                {
                    await ctx.Guild.AddBanAsync(i, options: new RequestOptions
                    {
                        AuditLogReason = $"{ctx.User}|| Banning users under specified server join age."
                    }).ConfigureAwait(false);
                    banned++;
                }
                catch
                {
                    errored++;
                }
            }

            var eb = new EmbedBuilder()
                .WithDescription(
                    $"Banned {banned} users under that server join age, and was unable to ban {errored} users.\nIf there were any failed bans please check the bots top role and try again.")
                .WithOkColor();
            await message.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex.ToString());
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), BotPerm(GuildPermission.KickMembers)]
    public async Task KickUnder(StoopidTime time, string? option = null)
    {
        await ctx.Guild.DownloadUsersAsync().ConfigureAwait(false);
        var users = ((SocketGuild)ctx.Guild).Users.Where(c =>
            c.JoinedAt != null && DateTimeOffset.Now.Subtract(c.JoinedAt.Value).TotalSeconds <= time.Time.TotalSeconds);
        var guildUsers = users as SocketGuildUser[] ?? users.ToArray();
        if (guildUsers.Length == 0)
        {
            await ctx.Channel.SendErrorAsync("No users at or under that account age!").ConfigureAwait(false);
            return;
        }

        if (option is not null && option.ToLower() == "-p")
        {
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(guildUsers.Length / 20)
                .WithDefaultCanceledPage()
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();
            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder()
                    .WithTitle(
                        $"Previewing {guildUsers.Length} users who's accounts joined under {time.Time.Humanize(maxUnit: TimeUnit.Year)} ago")
                    .WithDescription(string.Join("\n", guildUsers.Skip(page * 20).Take(20)));
            }
        }

        var banned = 0;
        var errored = 0;
        var msg = await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder().WithErrorColor()
            .WithDescription(
                $"Are you sure you want to kick {users.Count()} users that are under that server join age? Say `yes` to continue.")
            .Build());
        var text = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
        await msg.DeleteAsync();
        if (!text.ToLower().Contains("yes")) return;
        var message = await ctx.Channel.SendConfirmAsync($"Kicking {guildUsers.Length} users..").ConfigureAwait(false);
        foreach (var i in guildUsers)
        {
            try
            {
                await i.KickAsync($"{ctx.User}|| Kicking users under specified join time.").ConfigureAwait(false);
                banned++;
            }
            catch
            {
                errored++;
            }
        }

        var eb = new EmbedBuilder()
            .WithDescription(
                $"Kicked {banned} users under that server join age, and was unable to ban {errored} users.\nIf there were any failed kicks please check the bots top role and try again.")
            .WithOkColor();
        await message.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator), BotPerm(GuildPermission.ManageGuild)]
    public async Task PruneMembers(StoopidTime time, string e = "no")
    {
        try
        {
            await ctx.Channel.SendConfirmAsync("This command may take a bit to complete depending on server size, please wait...");
            if (e == "no")
            {
                var toprune = await ctx.Guild.PruneUsersAsync(time.Time.Days, true);
                if (toprune == 0)
                {
                    await ctx.Channel.SendErrorAsync(
                            $"No users to prune, if you meant to prune users inyour member role please set it with {await guildSettingsService.GetPrefix(ctx.Guild)}memberrole role, and rerun the command but specify -y after the time. You can also specify which roles you want to prune in by rerunning this with a role list at the end.")
                        .ConfigureAwait(false);
                    return;
                }

                var eb = new EmbedBuilder
                {
                    Description = $"Are you sure you want to prune {toprune} Members?", Color = Mewdeko.OkColor
                };
                if (!await PromptUserConfirmAsync(eb, ctx.User.Id).ConfigureAwait(false))
                {
                    await ctx.Channel.SendConfirmAsync(
                            $"Canceled prune. As a reminder if you meant to prune members in your members role, set it with {await guildSettingsService.GetPrefix(ctx.Guild)}memberrole role and run this with -y at the end of the command. You can also specify which roles you want to prune in by rerunning this with a role list at the end.")
                        .ConfigureAwait(false);
                }
                else
                {
                    var msg = await ctx.Channel.SendConfirmAsync($"Pruning {toprune} members...").ConfigureAwait(false);
                    await ctx.Guild.PruneUsersAsync(time.Time.Days).ConfigureAwait(false);
                    var ebi = new EmbedBuilder
                    {
                        Description = $"Pruned {toprune} members.", Color = Mewdeko.OkColor
                    };
                    await msg.ModifyAsync(x => x.Embed = ebi.Build()).ConfigureAwait(false);
                }
            }
            else
            {
                ctx.Guild.GetRole(await Service.GetMemberRole(ctx.Guild.Id));
                var toprune = await ctx.Guild.PruneUsersAsync(time.Time.Days, true,
                    includeRoleIds: new[]
                    {
                        await Service.GetMemberRole(ctx.Guild.Id)
                    }).ConfigureAwait(false);
                if (toprune == 0)
                {
                    await ctx.Channel.SendErrorAsync("No users to prune.").ConfigureAwait(false);
                    return;
                }

                var eb = new EmbedBuilder
                {
                    Description = $"Are you sure you want to prune {toprune} Members?", Color = Mewdeko.OkColor
                };
                if (!await PromptUserConfirmAsync(eb, ctx.User.Id).ConfigureAwait(false))
                {
                    await ctx.Channel.SendConfirmAsync("Canceled prune.").ConfigureAwait(false);
                }
                else
                {
                    var msg = await ctx.Channel.SendConfirmAsync($"Pruning {toprune} members...").ConfigureAwait(false);
                    await ctx.Guild.PruneUsersAsync(time.Time.Days,
                        includeRoleIds: new[]
                        {
                            await Service.GetMemberRole(ctx.Guild.Id)
                        });
                    var ebi = new EmbedBuilder
                    {
                        Description = $"Pruned {toprune} members.", Color = Mewdeko.OkColor
                    };
                    await msg.ModifyAsync(x => x.Embed = ebi.Build()).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception)
        {
            Log.Error("Error in prunemembers: \n{0}", exception);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
    public async Task MemberRole(IRole? role)
    {
        var rol = await Service.GetMemberRole(ctx.Guild.Id);
        if (rol is 0 && role != null)
        {
            await Service.MemberRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Member role has been set to {role.Mention}").ConfigureAwait(false);
        }

        if (rol != 0 && role != null && rol == role.Id)
        {
            await ctx.Channel.SendErrorAsync("This is already your Member role!").ConfigureAwait(false);
            return;
        }

        if (rol is 0 && role == null)
        {
            await ctx.Channel.SendErrorAsync("No Member role set!").ConfigureAwait(false);
            return;
        }

        if (rol != 0 && role is null)
        {
            var r = ctx.Guild.GetRole(rol);
            await ctx.Channel.SendConfirmAsync($"Your current Member role is {r.Mention}").ConfigureAwait(false);
            return;
        }

        if (role != null && rol is not 0)
        {
            var oldrole = ctx.Guild.GetRole(rol);
            await Service.MemberRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                $"Your Member role has been switched from {oldrole.Mention} to {role.Mention}").ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
    public async Task StaffRole([Remainder] IRole? role = null)
    {
        var rol = await Service.GetStaffRole(ctx.Guild.Id);
        if (rol is 0 && role != null)
        {
            await Service.StaffRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Staff role has been set to {role.Mention}").ConfigureAwait(false);
        }

        if (rol != 0 && role != null && rol == role.Id)
        {
            await ctx.Channel.SendErrorAsync("This is already your staff role!").ConfigureAwait(false);
            return;
        }

        if (rol is 0 && role == null)
        {
            await ctx.Channel.SendErrorAsync("No staff role set!").ConfigureAwait(false);
            return;
        }

        if (rol != 0 && role is null)
        {
            var r = ctx.Guild.GetRole(rol);
            await ctx.Channel.SendConfirmAsync($"Your current staff role is {r.Mention}").ConfigureAwait(false);
            return;
        }

        if (role != null && rol is not 0)
        {
            var oldrole = ctx.Guild.GetRole(rol);
            await Service.StaffRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                $"Your staff role has been switched from {oldrole.Mention} to {role.Mention}").ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
    public async Task StaffRoleDisable()
    {
        var r = await Service.GetStaffRole(ctx.Guild.Id);
        if (r == 0)
        {
            await ctx.Channel.SendErrorAsync("No staff role set!").ConfigureAwait(false);
        }
        else
        {
            await Service.StaffRoleSet(ctx.Guild, 0).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("Staff role disabled!").ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator), BotPerm(GuildPermission.ManageMessages), Priority(2)]
    public async Task Delmsgoncmd(List _)
    {
        var guild = (SocketGuild)ctx.Guild;
        var (enabled, channels) = await Service.GetDelMsgOnCmdData(ctx.Guild.Id);

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(GetText("server_delmsgoncmd"))
            .WithDescription(enabled ? "✅" : "❌");

        var str = string.Join("\n", channels
            .Select(x =>
            {
                var ch = guild.GetChannel(x.ChannelId)?.ToString()
                         ?? x.ChannelId.ToString();
                var prefix = x.State ? "✅ " : "❌ ";
                return prefix + ch;
            }));

        if (string.IsNullOrWhiteSpace(str))
            str = "-";

        embed.AddField(GetText("channel_delmsgoncmd"), str);

        await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator), BotPerm(GuildPermission.ManageMessages), Priority(1)]
    public async Task Delmsgoncmd(Server _ = Server.Server)
    {
        if (await Service.ToggleDeleteMessageOnCommand(ctx.Guild.Id))
        {
            await ReplyConfirmLocalizedAsync("delmsg_on").ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("delmsg_off").ConfigureAwait(false);
        }
    }

    [Cmd, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator), BotPerm(GuildPermission.ManageMessages), Priority(0)]
    public Task Delmsgoncmd(Channel _, State s, ITextChannel ch) => Delmsgoncmd(_, s, ch.Id);

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.Administrator), BotPerm(GuildPermission.ManageMessages), Priority(1)]
    public async Task Delmsgoncmd(Channel _, State s, ulong? chId = null)
    {
        var actualChId = chId ?? ctx.Channel.Id;
        await Service.SetDelMsgOnCmdState(ctx.Guild.Id, actualChId, s).ConfigureAwait(false);

        switch (s)
        {
            case State.Disable:
                await ReplyConfirmLocalizedAsync("delmsg_channel_off").ConfigureAwait(false);
                break;
            case State.Enable:
                await ReplyConfirmLocalizedAsync("delmsg_channel_on").ConfigureAwait(false);
                break;
            default:
                await ReplyConfirmLocalizedAsync("delmsg_channel_inherit").ConfigureAwait(false);
                break;
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.DeafenMembers), BotPerm(GuildPermission.DeafenMembers)]
    public async Task Deafen(params IGuildUser[] users)
    {
        await AdministrationService.DeafenUsers(true, users).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("deafen").ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.DeafenMembers), BotPerm(GuildPermission.DeafenMembers)]
    public async Task UnDeafen(params IGuildUser[] users)
    {
        await AdministrationService.DeafenUsers(false, users).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("undeafen").ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageChannels), BotPerm(GuildPermission.ManageChannels)]
    public async Task DelVoiChanl([Remainder] IVoiceChannel voiceChannel)
    {
        await voiceChannel.DeleteAsync().ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("delvoich", Format.Bold(voiceChannel.Name)).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageChannels), BotPerm(GuildPermission.ManageChannels)]
    public async Task CreatVoiChanl([Remainder] string channelName)
    {
        var ch = await ctx.Guild.CreateVoiceChannelAsync(channelName).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("createvoich", Format.Bold(ch.Name)).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageChannels), BotPerm(GuildPermission.ManageChannels)]
    public async Task DelTxtChanl([Remainder] ITextChannel toDelete)
    {
        await toDelete.DeleteAsync().ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("deltextchan", Format.Bold(toDelete.Name)).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageChannels), BotPerm(GuildPermission.ManageChannels)]
    public async Task CreaTxtChanl([Remainder] string channelName)
    {
        var txtCh = await ctx.Guild.CreateTextChannelAsync(channelName).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("createtextchan", Format.Bold(txtCh.Name)).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageChannels), BotPerm(GuildPermission.ManageChannels)]
    public async Task SetTopic([Remainder] string? topic = null)
    {
        var channel = (ITextChannel)ctx.Channel;
        topic ??= "";
        await channel.ModifyAsync(c => c.Topic = topic).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("set_topic").ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageChannels), BotPerm(GuildPermission.ManageChannels)]
    public async Task SetChanlName([Remainder] string name)
    {
        var channel = (ITextChannel)ctx.Channel;
        await channel.ModifyAsync(c => c.Name = name).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("set_channel_name").ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(GuildPermission.ManageChannels), BotPerm(GuildPermission.ManageChannels)]
    public async Task NsfwToggle()
    {
        var channel = (ITextChannel)ctx.Channel;
        var isEnabled = channel.IsNsfw;

        await channel.ModifyAsync(c => c.IsNsfw = !isEnabled).ConfigureAwait(false);

        if (isEnabled)
            await ReplyConfirmLocalizedAsync("nsfw_set_false").ConfigureAwait(false);
        else
            await ReplyConfirmLocalizedAsync("nsfw_set_true").ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(ChannelPermission.ManageMessages), Priority(0)]
    public Task Edit(ulong messageId, [Remainder] string? text) => Edit((ITextChannel)ctx.Channel, messageId, text);

    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(1)]
    public async Task Edit(ITextChannel channel, ulong messageId, [Remainder] string? text)
    {
        var userPerms = ((SocketGuildUser)ctx.User).GetPermissions(channel);
        var botPerms = ((SocketGuild)ctx.Guild).CurrentUser.GetPermissions(channel);
        if (!userPerms.Has(ChannelPermission.ManageMessages))
        {
            await ReplyErrorLocalizedAsync("insuf_perms_u").ConfigureAwait(false);
            return;
        }

        if (!botPerms.Has(ChannelPermission.ViewChannel))
        {
            await ReplyErrorLocalizedAsync("insuf_perms_i").ConfigureAwait(false);
            return;
        }

        await AdministrationService.EditMessage(ctx, channel, messageId, text).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild),
     UserPerm(ChannelPermission.ManageMessages), BotPerm(ChannelPermission.ManageMessages)]
    public Task Delete(ulong messageId, StoopidTime? time = null) => Delete((ITextChannel)ctx.Channel, messageId, time);

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Delete(ITextChannel channel, ulong messageId, StoopidTime? time = null) => await InternalMessageAction(channel, messageId, time).ConfigureAwait(false);

    private async Task InternalMessageAction(ITextChannel channel, ulong messageId, StoopidTime? time)
    {
        var userPerms = ((SocketGuildUser)ctx.User).GetPermissions(channel);
        var botPerms = ((SocketGuild)ctx.Guild).CurrentUser.GetPermissions(channel);
        if (!userPerms.Has(ChannelPermission.ManageMessages))
        {
            await ReplyErrorLocalizedAsync("insuf_perms_u").ConfigureAwait(false);
            return;
        }

        if (!botPerms.Has(ChannelPermission.ManageMessages))
        {
            await ReplyErrorLocalizedAsync("insuf_perms_i").ConfigureAwait(false);
            return;
        }

        var msg = await channel.GetMessageAsync(messageId).ConfigureAwait(false);
        if (msg == null)
        {
            await ReplyErrorLocalizedAsync("msg_not_found").ConfigureAwait(false);
            return;
        }

        if (time == null)
        {
            await msg.DeleteAsync().ConfigureAwait(false);
        }
        else if (time.Time <= TimeSpan.FromDays(7))
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(time.Time).ConfigureAwait(false);
                await msg.DeleteAsync().ConfigureAwait(false);
            });
        }
        else
        {
            await ReplyErrorLocalizedAsync("time_too_long").ConfigureAwait(false);
            return;
        }

        await ctx.OkAsync().ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageChannels)]
    public async Task RenameChannel(IGuildChannel channel, [Remainder] string name)
    {
        await channel.ModifyAsync(x => x.Name = name).ConfigureAwait(false);
        await ConfirmLocalizedAsync("channel_renamed").ConfigureAwait(false);
    }
}