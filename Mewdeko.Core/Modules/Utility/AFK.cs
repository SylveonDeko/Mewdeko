using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility
{
    public partial class Utility

    {
        public class AFK : MewdekoSubmodule<AFKService>
        {
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [Priority(0)]
            public async Task Afk([Remainder] string message)
            {
                if (message.Length > 250)
                {
                    await ctx.Channel.SendErrorAsync("Thats too long!");
                    return;
                }

                await _service.AFKSet(ctx.Guild, (IGuildUser) ctx.User, message);
                await ctx.Channel.SendConfirmAsync($"AFK Message set to:\n{message}");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [Priority(1)]
            public async Task Afk()
            {
                var afkmsg = _service.AfkMessage(ctx.Guild.Id, ctx.User.Id).Select(x => x.Message).Last();
                if (afkmsg == "") return;
                await _service.AFKSet(ctx.Guild, (IGuildUser) ctx.User, "");
                await ctx.Channel.SendConfirmAsync("AFK Message has been disabled!");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [UserPerm(GuildPerm.ManageMessages)]
            [Priority(0)]
            public async Task AfkRemove(params IGuildUser[] user)
            {
                var users = 0;
                foreach (var i in user)
                    try
                    {
                        var afkmsg = _service.AfkMessage(ctx.Guild.Id, i.Id).Select(x => x.Message).Last();
                        await _service.AFKSet(ctx.Guild, i, "");
                        users++;
                    }
                    catch (Exception)
                    {
                    }

                await ctx.Channel.SendConfirmAsync($"AFK Message for {users} users has been disabled!");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [UserPerm(GuildPerm.ManageMessages)]
            [Priority(1)]
            public async Task AfkRemove(IGuildUser user)
            {
                var afkmsg = _service.AfkMessage(ctx.Guild.Id, user.Id).Select(x => x.Message).Last();
                if (afkmsg == "")
                {
                    await ctx.Channel.SendErrorAsync("The mentioned user does not have an afk status set!");
                    return;
                }

                await _service.AFKSet(ctx.Guild, user, "");
                await ctx.Channel.SendConfirmAsync($"AFK Message for {user.Mention} has been disabled!");
            }
        }
    }
}