using System.Text.RegularExpressions;
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
    private readonly BotConfigService configService;

    public Administration(InteractiveService serv, BotConfigService configService)
    {
        interactivity = serv;
        this.configService = configService;
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator)]
    public async Task GuildStatsOptOut()
    {
        var optout = await Service.ToggleOptOut(ctx.Guild);
        if (!optout)
            await ctx.Channel.SendConfirmAsync(GetText("command_stats_enabled"));
        else
            await ctx.Channel.SendConfirmAsync(GetText("command_stats_disabled"));
    }

    [Cmd, Aliases, Ratelimit(3600), UserPerm(GuildPermission.Administrator)]
    public async Task DeleteGuildStatsData()
    {
        if (await PromptUserConfirmAsync(GetText("command_stats_delete_confirm"), ctx.User.Id))
        {
            if (await Service.DeleteStatsData(ctx.Guild))
                await ctx.Channel.SendErrorAsync(GetText("command_stats_delete_success"));
            else
                await ctx.Channel.SendErrorAsync(GetText("command_stats_delete_fail"));
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
            await ctx.Channel.SendErrorAsync($"{configService.Data.ErrorEmote} {GetText("no_users_found_nameban")}");
            return;
        }

        await ctx.Channel.SendConfirmAsync(GetText("nameban_message_delete"));
        var deleteString = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
        if (deleteString == null)
        {
            await ctx.Channel.SendErrorAsync($"{configService.Data.ErrorEmote} {GetText("nameban_cancelled")}");
            return;
        }

        if (!int.TryParse(deleteString, out var _))
        {
            await ctx.Channel.SendErrorAsync($"{configService.Data.ErrorEmote} {GetText("invalid_input_number")}");
            return;
        }

        var deleteCount = int.Parse(deleteString);
        var components = new ComponentBuilder()
            .WithButton(GetText("preview"), "previewbans")
            .WithButton(GetText("execute"), "executeorder66", ButtonStyle.Success)
            .WithButton(GetText("cancel"), "cancel", ButtonStyle.Danger);
        var eb = new EmbedBuilder()
            .WithDescription(GetText("preview_or_execute"))
            .WithOkColor();
        var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build(), components: components.Build());
        var input = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id);
        switch (input)
        {
            case "cancel":
                await ctx.Channel.SendErrorAsync($"{configService.Data.ErrorEmote} {GetText("nameban_cancelled")}");
                break;
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
                    return new PageBuilder().WithTitle(GetText("nameban_preview_count", users.Count, name.ToLower()))
                        .WithDescription(string.Join("\n", users.Skip(page * 20).Take(20)));
                }

                break;
            case "executeorder66":
                if (await PromptUserConfirmAsync(GetText("nameban_confirm", users.Count), ctx.User.Id))
                {
                    var failedUsers = 0;
                    await ctx.Channel.SendConfirmAsync($"{configService.Data.LoadingEmote} executing order 66 on {users.Count} users, this may take a bit...");
                    foreach (var i in users)
                    {
                        try
                        {
                            await ctx.Guild.AddBanAsync(i, deleteCount, options: new RequestOptions
                            {
                                AuditLogReason = GetText("mass_ban_requested_by", ctx.User)
                            });
                        }
                        catch
                        {
                            failedUsers++;
                        }
                    }

                    await ctx.Channel.SendConfirmAsync(
                        $"{configService.Data.SuccessEmote} executed order 66 on {users.Count - failedUsers} users. Failed to ban {failedUsers} users (Probably due to bad role heirarchy).");
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
                await ctx.Channel.SendErrorAsync(GetText("banunder_no_users")).ConfigureAwait(false);
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
                    return new PageBuilder()
                        .WithTitle(GetText("banunder_preview", users.Count(), time.Time.Humanize(maxUnit: TimeUnit.Year)))
                        .WithDescription(string.Join("\n", users.Skip(page * 20).Take(20)));
                }
            }

            var banned = 0;
            var errored = 0;
            var msg = await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder().WithErrorColor()
                .WithDescription(GetText("banunder_confirm", users.Count()))
                .Build());
            var text = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            await msg.DeleteAsync();
            if (!text.ToLower().Contains("yes"))
                return;
            var message = await ConfirmLocalizedAsync("banunder_banning").ConfigureAwait(false);
            foreach (var i in users)
            {
                try
                {
                    await ctx.Guild.AddBanAsync(i, options: new RequestOptions
                    {
                        AuditLogReason = GetText("banunder_starting", ctx.User)
                    }).ConfigureAwait(false);
                    banned++;
                }
                catch
                {
                    errored++;
                }
            }

            var eb = new EmbedBuilder()
                .WithDescription(GetText("banunder_kicked", banned, errored))
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
            await ErrorLocalizedAsync("kickunder_no_users").ConfigureAwait(false);
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
                    .WithTitle(GetText("kickunder_preview", guildUsers.Length, time.Time.Humanize(maxUnit: TimeUnit.Year)))
                    .WithDescription(string.Join("\n", guildUsers.Skip(page * 20).Take(20)));
            }
        }

        var banned = 0;
        var errored = 0;
        var msg = await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder().WithErrorColor()
            .WithDescription(GetText("kickunder_confirm", users.Count()))
            .Build());
        var text = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
        await msg.DeleteAsync();
        if (!text.ToLower().Contains("yes"))
            return;
        var message = await ConfirmLocalizedAsync("kickunder_kicking").ConfigureAwait(false);
        foreach (var i in guildUsers)
        {
            try
            {
                await i.KickAsync(GetText("kickunder_starting", ctx.User)).ConfigureAwait(false);
                banned++;
            }
            catch
            {
                errored++;
            }
        }

        var eb = new EmbedBuilder()
            .WithDescription(GetText("kickunder_kicked", banned, errored))
            .WithOkColor();
        await message.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator), BotPerm(GuildPermission.ManageGuild)]
    public async Task PruneMembers(StoopidTime time, string e = "no")
    {
        try
        {
            await ConfirmLocalizedAsync("command_expected_latency_server_size");
            if (e == "no")
            {
                var toprune = await ctx.Guild.PruneUsersAsync(time.Time.Days, true);
                if (toprune == 0)
                {
                    await ErrorLocalizedAsync("prune_no_members_upsell").ConfigureAwait(false);
                    return;
                }

                var eb = new EmbedBuilder
                {
                    Description = $"Are you sure you want to prune {toprune} Members?", Color = Mewdeko.OkColor
                };
                if (!await PromptUserConfirmAsync(eb, ctx.User.Id).ConfigureAwait(false))
                {
                    await ConfirmLocalizedAsync("prune_canceled_member_upsell").ConfigureAwait(false);
                }
                else
                {
                    var msg = await ConfirmLocalizedAsync("pruning_members", toprune).ConfigureAwait(false);
                    await ctx.Guild.PruneUsersAsync(time.Time.Days).ConfigureAwait(false);
                    var ebi = new EmbedBuilder
                    {
                        Description = GetText("pruned_members", toprune), Color = Mewdeko.OkColor
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
                    await ErrorLocalizedAsync("prune_no_members").ConfigureAwait(false);
                    return;
                }

                var eb = new EmbedBuilder
                {
                    Description = GetText("prune_confirm", toprune), Color = Mewdeko.OkColor
                };
                if (!await PromptUserConfirmAsync(eb, ctx.User.Id).ConfigureAwait(false))
                {
                    await ConfirmLocalizedAsync("prune_canceled").ConfigureAwait(false);
                }
                else
                {
                    var msg = await ConfirmLocalizedAsync("pruning_members", toprune).ConfigureAwait(false);
                    await ctx.Guild.PruneUsersAsync(time.Time.Days,
                        includeRoleIds: new[]
                        {
                            await Service.GetMemberRole(ctx.Guild.Id)
                        });
                    var ebi = new EmbedBuilder
                    {
                        Description = GetText("pruned_members", toprune), Color = Mewdeko.OkColor
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
            await ConfirmLocalizedAsync("member_role_set", role.Id).ConfigureAwait(false);
        }

        if (rol != 0 && role != null && rol == role.Id)
        {
            await ErrorLocalizedAsync("member_role_already_set").ConfigureAwait(false);
            return;
        }

        if (rol is 0 && role == null)
        {
            await ErrorLocalizedAsync("member_role_missing").ConfigureAwait(false);
            return;
        }

        if (rol != 0 && role is null)
        {
            var r = ctx.Guild.GetRole(rol);
            await ConfirmLocalizedAsync("member_role_current", r.Id).ConfigureAwait(false);
            return;
        }

        if (role != null && rol is not 0)
        {
            var oldrole = ctx.Guild.GetRole(rol);
            await Service.MemberRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ConfirmLocalizedAsync("member_role_updated", oldrole.Id, role.Id).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
    public async Task StaffRole([Remainder] IRole? role = null)
    {
        var rol = await Service.GetStaffRole(ctx.Guild.Id);
        if (rol is 0 && role != null)
        {
            await Service.StaffRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ConfirmLocalizedAsync("staff_role_set", role.Id).ConfigureAwait(false);
        }

        if (rol != 0 && role != null && rol == role.Id)
        {
            await ErrorLocalizedAsync("staff_role_already_set").ConfigureAwait(false);
            return;
        }

        if (rol is 0 && role == null)
        {
            await ErrorLocalizedAsync("staff_role_missing").ConfigureAwait(false);
            return;
        }

        if (rol != 0 && role is null)
        {
            var r = ctx.Guild.GetRole(rol);
            await ConfirmLocalizedAsync("staff_role_current", r.Id).ConfigureAwait(false);
            return;
        }

        if (role != null && rol is not 0)
        {
            var oldrole = ctx.Guild.GetRole(rol);
            await Service.StaffRoleSet(ctx.Guild, role.Id).ConfigureAwait(false);
            await ConfirmLocalizedAsync("staff_role_updated", oldrole.Id, role.Id).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
    public async Task StaffRoleDisable()
    {
        var r = await Service.GetStaffRole(ctx.Guild.Id);
        if (r == 0)
        {
            await ctx.Channel.SendErrorAsync(GetText("staff_role_missing")).ConfigureAwait(false);
        }
        else
        {
            await Service.StaffRoleSet(ctx.Guild, 0).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(GetText("staff_role_disabled")).ConfigureAwait(false);
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
                var prefix = x.State == 1 ? "✅ " : "❌ ";
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