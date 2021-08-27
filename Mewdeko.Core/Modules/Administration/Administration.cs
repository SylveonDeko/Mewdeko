using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Common.TypeReaders.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Services;
using Humanizer;

namespace Mewdeko.Modules.Administration
{
    public partial class Administration : MewdekoModule<AdministrationService>
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
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [UserPerm(GuildPerm.Administrator)]
        [BotPerm(GuildPerm.BanMembers)]
        public async Task BanUnder(StoopidTime time, string option = null)
        {
            var users = ((SocketGuild)ctx.Guild).Users.Where(c =>
                    DateTimeOffset.Now.Subtract(c.JoinedAt.Value).TotalSeconds <= time.Time.TotalSeconds);
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync("No users at or under that server join age!");
                return;
            }
            if (option is not null && option.ToLower() == "-p")
            {
                await ctx.SendPaginatedConfirmAsync(0, cur =>
                {
                    return new EmbedBuilder().WithOkColor()
                        .WithTitle($"Previewing {users.Count()} users who's accounts joined under {time.Time.Humanize(maxUnit: Humanizer.Localisation.TimeUnit.Year)} ago")
                        .WithDescription(string.Join("\n", users.Skip(cur * 20).Take(20)));
                }, users.Count(), 20).ConfigureAwait(false);
            }
            int banned = 0;
            int errored = 0;
            var embed = new EmbedBuilder().WithErrorColor().WithDescription($"Are you sure you want to ban {users.Count()} users that are under that server join age?");
            if (!await PromptUserConfirmAsync(embed).ConfigureAwait(false)) return;
            var message = await ctx.Channel.SendConfirmAsync($"Banning {users.Count()} users..");
            foreach (var i in users)
            {
                try
                {
                    await i.BanAsync(reason: $"{ctx.User}|| Banning users under specified server join age.");
                    banned++;
                }
                catch
                {
                    errored++;
                }
            }
            var eb = new EmbedBuilder().WithDescription($"Banned {banned} users under that server join age, and was unable to ban {errored} users.\nIf there were any failed bans please check the bots top role and try again.").WithOkColor();
            await message.ModifyAsync(x => x.Embed = eb.Build());
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [UserPerm(GuildPerm.Administrator)]
        [BotPerm(GuildPerm.KickMembers)]
        public async Task KickUnder(StoopidTime time, string option = null)
        {
            var users = ((SocketGuild)ctx.Guild).Users.Where(c =>
                    DateTimeOffset.Now.Subtract(c.JoinedAt.Value).TotalSeconds <= time.Time.TotalSeconds);
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync("No users at or under that account age!");
                return;
            }
            if (option is not null && option.ToLower() == "-p")
            {
                await ctx.SendPaginatedConfirmAsync(0, cur =>
                {
                    return new EmbedBuilder().WithOkColor()
                        .WithTitle($"Previewing {users.Count()} users who joined {time.Time.Humanize(maxUnit: Humanizer.Localisation.TimeUnit.Year)} ago")
                        .WithDescription(string.Join("\n", users.Skip(cur * 20).Take(20)));
                }, users.Count(), 20).ConfigureAwait(false);
            }
            int banned = 0;
            int errored = 0;
            var embed = new EmbedBuilder().WithErrorColor().WithDescription($"Are you sure you want to kick {users.Count()} users that joined under that time?");
            if (!await PromptUserConfirmAsync(embed).ConfigureAwait(false)) return;
            var message = await ctx.Channel.SendConfirmAsync($"Kicking {users.Count()} users..");
            foreach (var i in users)
            {
                try
                {
                    await i.KickAsync(reason: $"{ctx.User}|| Kicking users under specified join time.");
                    banned++;
                }
                catch
                {
                    errored++;
                }
            }
            var eb = new EmbedBuilder().WithDescription($"Kicked {banned} users under that server join age, and was unable to ban {errored} users.\nIf there were any failed kicks please check the bots top role and try again.").WithOkColor();
            await message.ModifyAsync(x => x.Embed = eb.Build());
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.Administrator)]
        [BotPerm(GuildPerm.ManageGuild)]
        public async Task PruneMembers(StoopidTime time, string e = "no")
        {
            if (e == "no")
            {
                var toprune = await ctx.Guild.PruneUsersAsync(time.Time.Days, true);
                if (toprune == 0)
                {
                    await ctx.Channel.SendErrorAsync($"No users to prune, if you meant to prune users inyour member role please set it with {Prefix}memberrole role, and rerun the command but specify -y after the time. You can also specify which roles you want to prune in by rerunning this with a role list at the end.");
                    return;
                }
                var eb = new EmbedBuilder()
                {
                    Description = $"Are you sure you want to prune {toprune} Members?",
                    Color = Mewdeko.OkColor
                };
                if(!await PromptUserConfirmAsync(eb))
                {
                    await ctx.Channel.SendConfirmAsync($"Canceled prune. As a reminder if you meant to prune members in your members role, set it with {Prefix}memberrole role and run this with -y at the end of the command. You can also specify which roles you want to prune in by rerunning this with a role list at the end.");
                }
                else
                {
                    var msg = await ctx.Channel.SendConfirmAsync($"Pruning {toprune} members...");
                    await ctx.Guild.PruneUsersAsync(time.Time.Days);
                    var ebi = new EmbedBuilder()
                    {
                        Description = $"Pruned {toprune} members.",
                        Color = Mewdeko.OkColor
                    };
                    await msg.ModifyAsync(x => x.Embed = ebi.Build());
                }
            }
            else
            {
                    var role = ctx.Guild.GetRole(_service.GetMemberRole(ctx.Guild.Id));
                    var toprune = await ctx.Guild.PruneUsersAsync(time.Time.Days, true, includeRoleIds: new ulong[] {_service.GetMemberRole(ctx.Guild.Id)});
                    if (toprune == 0)
                    {
                        await ctx.Channel.SendErrorAsync($"No users to prune.");
                        return;
                    }
                    var eb = new EmbedBuilder()
                    {
                        Description = $"Are you sure you want to prune {toprune} Members?",
                        Color = Mewdeko.OkColor
                    };
                    if (!await PromptUserConfirmAsync(eb))
                    {
                        await ctx.Channel.SendConfirmAsync($"Canceled prune.");
                    }
                    else
                    {
                        var msg = await ctx.Channel.SendConfirmAsync($"Pruning {toprune} members...");
                        await ctx.Guild.PruneUsersAsync(time.Time.Days, includeRoleIds: new ulong[] { _service.GetMemberRole(ctx.Guild.Id) });
                        var ebi = new EmbedBuilder()
                        {
                            Description = $"Pruned {toprune} members.",
                            Color = Mewdeko.OkColor
                        };
                        await msg.ModifyAsync(x => x.Embed = ebi.Build());
                    }
            }
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.Administrator)]
        public async Task MemberRole(IRole role)
        {
            var rol = _service.GetMemberRole(ctx.Guild.Id);
            if (rol is 0 && role != null)
            {
                await _service.MemberRoleSet(ctx.Guild, role.Id);
                await ctx.Channel.SendConfirmAsync($"Member role has been set to {role.Mention}");
            }

            if (rol != 0 && role != null && rol == role.Id)
            {
                await ctx.Channel.SendErrorAsync("This is already your Member role!");
                return;
            }

            if (rol is 0 && role == null)
            {
                await ctx.Channel.SendErrorAsync("No Member role set!");
                return;
            }

            if (rol != 0 && role is null)
            {
                var r = ctx.Guild.GetRole(rol);
                await ctx.Channel.SendConfirmAsync($"Your current Member role is {r.Mention}");
                return;
            }

            if (role != null && rol is not 0)
            {
                var oldrole = ctx.Guild.GetRole(rol);
                await _service.MemberRoleSet(ctx.Guild, role.Id);
                await ctx.Channel.SendConfirmAsync(
                    $"Your Member role has been switched from {oldrole.Mention} to {role.Mention}");
            }
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.Administrator)]
        public async Task StaffRole([Remainder] IRole role = null)
        {
            var rol = _service.GetStaffRole(ctx.Guild.Id);
            if (rol is 0 && role != null)
            {
                await _service.StaffRoleSet(ctx.Guild, role.Id);
                await ctx.Channel.SendConfirmAsync($"Staff role has been set to {role.Mention}");
            }

            if (rol != 0 && role != null && rol == role.Id)
            {
                await ctx.Channel.SendErrorAsync("This is already your staff role!");
                return;
            }

            if (rol is 0 && role == null)
            {
                await ctx.Channel.SendErrorAsync("No staff role set!");
                return;
            }

            if (rol != 0 && role is null)
            {
                var r = ctx.Guild.GetRole(rol);
                await ctx.Channel.SendConfirmAsync($"Your current staff role is {r.Mention}");
                return;
            }

            if (role != null && rol is not 0)
            {
                var oldrole = ctx.Guild.GetRole(rol);
                await _service.StaffRoleSet(ctx.Guild, role.Id);
                await ctx.Channel.SendConfirmAsync(
                    $"Your staff role has been switched from {oldrole.Mention} to {role.Mention}");
            }
        }
        //[MewdekoCommand]
        //[Usage]
        //[Description]
        //[Aliases]
        //public async Task Test()
        //{

        //    var e = guild.DefaultChannel;
        //    var eb = new EmbedBuilder();
        //    eb.Description = "Hi, thanks for inviting Mewdeko! I hope you like the bot, and discover all its features! The default prefix is `.` This can be changed with the prefix command.";
        //    eb.AddField("How to look for commands", "1) Use the .cmds command to see all the categories\n2) use .cmds with the category name to glance at what commands it has. ex: `.cmds mod`\n3) Use .h with a command name to view its help. ex: `.h purge`");
        //    eb.AddField("Have any questions, or need my invite link?", "Support Server: https://discord.gg/6n3aa9Xapf \nInvite Link:https://mewdeko.tech/invite");
        //    eb.WithThumbnailUrl("https://media.discordapp.net/attachments/866308739334406174/869220206101282896/nekoha_shizuku_original_drawn_by_amashiro_natsuki__df72ed2f8d84038f83c4d1128969d407.png");
        //    eb.WithOkColor();
        //    await ctx.Channel.SendMessageAsync(embed: eb.Build());
        //}
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.Administrator)]
        public async Task StaffRoleDisable()
        {
            var r = _service.GetStaffRole(ctx.Guild.Id);
            if (r == 0)
            {
                await ctx.Channel.SendErrorAsync("No staff role set!");
            }
            else
            {
                await _service.StaffRoleSet(ctx.Guild, 0);
                await ctx.Channel.SendConfirmAsync("Staff role disabled!");
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(ChannelPerm.ManageChannel)]
        [BotPerm(ChannelPerm.ManageChannel)]
        public async Task Slowmode(StoopidTime time = null)
        {
            var seconds = (int?) time?.Time.TotalSeconds ?? 0;
            if (!(time is null) && (time.Time < TimeSpan.FromSeconds(0) || time.Time > TimeSpan.FromHours(6)))
                return;


            await ((ITextChannel) Context.Channel).ModifyAsync(tcp => { tcp.SlowModeInterval = seconds; });

            await Context.OkAsync();
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.Administrator)]
        [BotPerm(GuildPerm.ManageMessages)]
        [Priority(2)]
        public async Task Delmsgoncmd(List _)
        {
            var guild = (SocketGuild) ctx.Guild;
            var (enabled, channels) = _service.GetDelMsgOnCmdData(ctx.Guild.Id);

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

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.Administrator)]
        [BotPerm(GuildPerm.ManageMessages)]
        [Priority(1)]
        public async Task Delmsgoncmd(Server _ = Server.Server)
        {
            if (_service.ToggleDeleteMessageOnCommand(ctx.Guild.Id))
            {
                _service.DeleteMessagesOnCommand.Add(ctx.Guild.Id);
                await ReplyConfirmLocalizedAsync("delmsg_on").ConfigureAwait(false);
            }
            else
            {
                _service.DeleteMessagesOnCommand.TryRemove(ctx.Guild.Id);
                await ReplyConfirmLocalizedAsync("delmsg_off").ConfigureAwait(false);
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.Administrator)]
        [BotPerm(GuildPerm.ManageMessages)]
        [Priority(0)]
        public Task Delmsgoncmd(Channel _, State s, ITextChannel ch)
        {
            return Delmsgoncmd(_, s, ch.Id);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.Administrator)]
        [BotPerm(GuildPerm.ManageMessages)]
        [Priority(1)]
        public async Task Delmsgoncmd(Channel _, State s, ulong? chId = null)
        {
            var actualChId = chId ?? ctx.Channel.Id;
            await _service.SetDelMsgOnCmdState(ctx.Guild.Id, actualChId, s).ConfigureAwait(false);

            if (s == State.Disable)
                await ReplyConfirmLocalizedAsync("delmsg_channel_off").ConfigureAwait(false);
            else if (s == State.Enable)
                await ReplyConfirmLocalizedAsync("delmsg_channel_on").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("delmsg_channel_inherit").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.DeafenMembers)]
        [BotPerm(GuildPerm.DeafenMembers)]
        public async Task Deafen(params IGuildUser[] users)
        {
            await _service.DeafenUsers(true, users).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("deafen").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.DeafenMembers)]
        [BotPerm(GuildPerm.DeafenMembers)]
        public async Task UnDeafen(params IGuildUser[] users)
        {
            await _service.DeafenUsers(false, users).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("undeafen").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageChannels)]
        [BotPerm(GuildPerm.ManageChannels)]
        public async Task DelVoiChanl([Leftover] IVoiceChannel voiceChannel)
        {
            await voiceChannel.DeleteAsync().ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("delvoich", Format.Bold(voiceChannel.Name)).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageChannels)]
        [BotPerm(GuildPerm.ManageChannels)]
        public async Task CreatVoiChanl([Leftover] string channelName)
        {
            var ch = await ctx.Guild.CreateVoiceChannelAsync(channelName).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("createvoich", Format.Bold(ch.Name)).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageChannels)]
        [BotPerm(GuildPerm.ManageChannels)]
        public async Task DelTxtChanl([Leftover] ITextChannel toDelete)
        {
            await toDelete.DeleteAsync().ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("deltextchan", Format.Bold(toDelete.Name)).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageChannels)]
        [BotPerm(GuildPerm.ManageChannels)]
        public async Task CreaTxtChanl([Leftover] string channelName)
        {
            var txtCh = await ctx.Guild.CreateTextChannelAsync(channelName).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("createtextchan", Format.Bold(txtCh.Name)).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageChannels)]
        [BotPerm(GuildPerm.ManageChannels)]
        public async Task SetTopic([Leftover] string topic = null)
        {
            var channel = (ITextChannel) ctx.Channel;
            topic = topic ?? "";
            await channel.ModifyAsync(c => c.Topic = topic).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("set_topic").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageChannels)]
        [BotPerm(GuildPerm.ManageChannels)]
        public async Task SetChanlName([Leftover] string name)
        {
            var channel = (ITextChannel) ctx.Channel;
            await channel.ModifyAsync(c => c.Name = name).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("set_channel_name").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageChannels)]
        [BotPerm(GuildPerm.ManageChannels)]
        public async Task NsfwToggle()
        {
            var channel = (ITextChannel) ctx.Channel;
            var isEnabled = channel.IsNsfw;

            await channel.ModifyAsync(c => c.IsNsfw = !isEnabled).ConfigureAwait(false);

            if (isEnabled)
                await ReplyConfirmLocalizedAsync("nsfw_set_false").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("nsfw_set_true").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(ChannelPerm.ManageMessages)]
        [Priority(0)]
        public Task Edit(ulong messageId, [Leftover] string text)
        {
            return Edit((ITextChannel) ctx.Channel, messageId, text);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public async Task Edit(ITextChannel channel, ulong messageId, [Leftover] string text)
        {
            var userPerms = ((SocketGuildUser) ctx.User).GetPermissions(channel);
            var botPerms = ((SocketGuild) ctx.Guild).CurrentUser.GetPermissions(channel);
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

            await _service.EditMessage(ctx, channel, messageId, text);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(ChannelPerm.ManageMessages)]
        [BotPerm(ChannelPerm.ManageMessages)]
        public Task Delete(ulong messageId, StoopidTime time = null)
        {
            return Delete((ITextChannel) ctx.Channel, messageId, time);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Delete(ITextChannel channel, ulong messageId, StoopidTime time = null)
        {
            await InternalMessageAction(channel, messageId, time, msg => msg.DeleteAsync());
        }

        private async Task InternalMessageAction(ITextChannel channel, ulong messageId, StoopidTime time,
            Func<IMessage, Task> func)
        {
            var userPerms = ((SocketGuildUser) ctx.User).GetPermissions(channel);
            var botPerms = ((SocketGuild) ctx.Guild).CurrentUser.GetPermissions(channel);
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
                var _ = Task.Run(async () =>
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

            await ctx.OkAsync();
        }
    }
}