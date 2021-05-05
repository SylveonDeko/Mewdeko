using Discord;
using System;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Services;
using System.Linq;
using System.Threading.Tasks;
using Mewdeko.Core.Common.TypeReaders.Models;
using Mewdeko.Core.Services;
namespace Mewdeko.Modules.Administration
{
    public partial class Administration : MewdekoTopLevelModule<AdministrationService>
    {
        public Administration(DbService db)
        {
            _db = db;
        }
#pragma warning disable CA1069 // Enums values should not be duplicated
        public enum List { List = 0, Ls = 0 }
#pragma warning restore CA1069 // Enums values should not be duplicated
        private readonly DbService _db;

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.Administrator)]
        [BotPerm(GuildPerm.ManageMessages)]
        [Priority(2)]
        public async Task Delmsgoncmd(List _)
        {
            var guild = (SocketGuild)ctx.Guild;
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

        public enum Server { Server }
        [MewdekoCommand, Usage, Description, Aliases]
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

        public enum Channel { Channel, Ch, Chnl, Chan }
        public enum State { Enable, Disable, Inherit }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.Administrator)]
        [BotPerm(GuildPerm.ManageMessages)]
        [Priority(0)]
        public Task Delmsgoncmd(Channel _, State s, ITextChannel ch)
            => Delmsgoncmd(_, s, ch.Id);
        
        [MewdekoCommand, Usage, Description, Aliases]
        [UserPerm(GuildPerm.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task myroles()
        {
            var user = ctx.User as IGuildUser;
            var roles = user.GetRoles();
            await ctx.Channel.SendConfirmAsync(String.Join<String>(" ", roles.Select(x => x.Mention)));
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.Administrator)]
        [BotPerm(GuildPerm.ManageMessages)]
        [Priority(1)]
        public async Task Delmsgoncmd(Channel _, State s, ulong? chId = null)
        {
            var actualChId = chId ?? ctx.Channel.Id;
            await _service.SetDelMsgOnCmdState(ctx.Guild.Id, actualChId, s).ConfigureAwait(false);

            if (s == State.Disable)
            {
                await ReplyConfirmLocalizedAsync("delmsg_channel_off").ConfigureAwait(false);
            }
            else if (s == State.Enable)
            {
                await ReplyConfirmLocalizedAsync("delmsg_channel_on").ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("delmsg_channel_inherit").ConfigureAwait(false);
            }
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.DeafenMembers)]
        [BotPerm(GuildPerm.DeafenMembers)]
        public async Task Deafen(params IGuildUser[] users)
        {
            await _service.DeafenUsers(true, users).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("deafen").ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.DeafenMembers)]
        [BotPerm(GuildPerm.DeafenMembers)]
        public async Task UnDeafen(params IGuildUser[] users)
        {
            await _service.DeafenUsers(false, users).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("undeafen").ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageChannels)]
        [BotPerm(GuildPerm.ManageChannels)]
        public async Task DelVoiChanl([Remainder] IVoiceChannel voiceChannel)
        {
            await voiceChannel.DeleteAsync().ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("delvoich", Format.Bold(voiceChannel.Name)).ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageChannels)]
        [BotPerm(GuildPerm.ManageChannels)]
        
        public async Task CreatVoiChanl([Remainder] string channelName = null)
        {
            var ch = await ctx.Guild.CreateVoiceChannelAsync(channelName).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("createvoich", Format.Bold(ch.Name)).ConfigureAwait(false);
        }
        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageChannels)]
        [BotPerm(GuildPerm.ManageChannels)]
        
        public async Task CreatVoiCatChanl(ICategoryChannel catid = null, [Remainder] string channelName = null)
        {
            var chcat = await ctx.Guild.CreateVoiceChannelAsync(channelName, x => { x.CategoryId =  catid.Id;});
                await ReplyConfirmLocalizedAsync("createvoichcat", Format.Bold(chcat.Name), Format.Bold(catid.Name)).ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageChannels)]
        [BotPerm(GuildPerm.ManageChannels)]
        public async Task DelTxtChanl([Remainder] ITextChannel toDelete)
        {
            await toDelete.DeleteAsync().ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("deltextchan", Format.Bold(toDelete.Name)).ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageChannels)]
        [BotPerm(GuildPerm.ManageChannels)]
        public async Task CreaTxtChanl([Remainder] string channelName)
        {
            var txtCh = await ctx.Guild.CreateTextChannelAsync(channelName).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("createtextchan", Format.Bold(txtCh.Name)).ConfigureAwait(false);
        }
        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageChannels)]
        [BotPerm(GuildPerm.ManageChannels)]
        
        public async Task CreatTxtCatChanl(ICategoryChannel catid = null, [Remainder] string channelName = null)
        {
            var chcat = await ctx.Guild.CreateTextChannelAsync(channelName, x => { x.CategoryId =  catid.Id;});
                await ReplyConfirmLocalizedAsync("createtextchancat", Format.Bold(chcat.Name), Format.Bold(catid.Name)).ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageChannels)]
        [BotPerm(GuildPerm.ManageChannels)]
        public async Task SetTopic([Remainder] string topic = null)
        {
            var channel = (ITextChannel)ctx.Channel;
            topic = topic ?? "";
            await channel.ModifyAsync(c => c.Topic = topic).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("set_topic").ConfigureAwait(false);

        }
        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageChannels)]
        [BotPerm(GuildPerm.ManageChannels)]
        public async Task SetChanlName([Remainder] string name)
        {
            var channel = (ITextChannel)ctx.Channel;
            await channel.ModifyAsync(c => c.Name = name).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("set_channel_name").ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(ChannelPerm.ManageMessages)]
        [Priority(0)]
        public Task Edit(ulong messageId, [Remainder] string text)
            => Edit((ITextChannel)ctx.Channel, messageId, text);

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public async Task Edit(ITextChannel channel, ulong messageId, [Remainder] string text)
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

            await _service.EditMessage(ctx, channel, messageId, text);
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(ChannelPerm.ManageMessages)]
        [BotPerm(ChannelPerm.ManageMessages)]
        public Task Delete(ulong messageId, StoopidTime time = null)
            => Delete((ITextChannel)ctx.Channel, messageId, time);

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Delete(ITextChannel channel, ulong messageId, StoopidTime time = null)
        {
            await InternalMessageAction(channel, messageId, time, (msg) => msg.DeleteAsync());
        }

        private async Task InternalMessageAction(ITextChannel channel, ulong messageId, StoopidTime time,
            Func<IMessage, Task> func)
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
