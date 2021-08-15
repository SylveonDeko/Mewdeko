using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Humanizer;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Common.TypeReaders.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Utility.Services;
using Newtonsoft.Json;

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
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task RegisterAfkCommands()
            {
                try
                {
                    var guildcommands = new SlashCommandBuilder();
                    guildcommands.WithName("afk");
                    guildcommands.WithDescription("Allows you to enable and disable your afk.");
                    guildcommands.AddOption(new SlashCommandOptionBuilder()
                    {
                        Name = "message",
                        Description = "Set an optional afk message",
                        Type = ApplicationCommandOptionType.String
                    });
                    var client = ctx.Client as DiscordSocketClient;
                    await client.Rest.CreateGuildCommand(guildcommands.Build(), ctx.Guild.Id);
                    await ctx.Channel.SendConfirmAsync("Succesfully added afk slash commands");
                }
                catch
                {
                    await ctx.Channel.SendErrorAsync("The bot does not have permission to add slash commands!, Please reauthrorize it using the link at https://mewdeko.tech/invite");
                }
            }
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [Priority(0)]
            public async Task Afk([Remainder] string message)
            {
                if (message.Length != 0 && message.Length > _service.GetAfkLength(ctx.Guild.Id))
                {
                    await ctx.Channel.SendErrorAsync(
                        $"Thats too long! The length for afk on this server is set to {_service.GetAfkLength(ctx.Guild.Id)} characters.");
                    return;
                }

                if (_service.GetAfkMessageType(ctx.Guild.Id) == 2 || _service.GetAfkMessageType(ctx.Guild.Id) == 4)
                    if (ctx.Message.MentionedUserIds.Count >= 3)
                    {
                        await ctx.Channel.SendErrorAsync(
                            "Nice try there, but you cant mention more then 3 users with afk type 2 or 4 to prevent raids.");
                        return;
                    }

                await _service.AFKSet(ctx.Guild, (IGuildUser) ctx.User, message, 0);
                await ctx.Channel.SendConfirmAsync($"AFK Message set to:\n{message}");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [Priority(0)]
            public async Task TimedAfk(StoopidTime time, [Remainder] string message)
            {
                if (_service.IsAfk(ctx.Guild, ctx.User as IGuildUser))
                {
                    await ctx.Channel.SendErrorAsync(
                        $"You already have a regular afk set! Please disable it by doing {Prefix}afk anf try again");
                    return;
                }

                await ctx.Channel.SendConfirmAsync(
                    $"AFK Message set to:\n{message}\n\nAFK will unset in {time.Time.Humanize()}");
                await _service.TimedAfk(ctx.Guild, ctx.User, message, time.Time);
                if (_service.IsAfk(ctx.Guild, ctx.User as IGuildUser))
                    await ctx.Channel.SendMessageAsync(
                        $"Welcome back {ctx.User.Mention} I have removed your timed AFK.");
            }

            [Priority(0)]
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task AfkDisabledList()
            {
                var mentions = new List<string>();
                var chans = _service.GetDisabledAfkChannels(ctx.Guild.Id);
                var e = chans.Split(",");
                if (e.Length == 1 && e.Contains("0"))
                {
                    await ctx.Channel.SendErrorAsync("You don't have any disabled Afk channels.");
                    return;
                }

                foreach (var i in e)
                {
                    var role = await ctx.Guild.GetTextChannelAsync(Convert.ToUInt64(i));
                    mentions.Add(role.Mention);
                }

                await ctx.SendPaginatedConfirmAsync(0, cur =>
                {
                    return new EmbedBuilder().WithOkColor()
                        .WithTitle(Format.Bold("Disabled Afk Channels") + $" - {mentions.ToArray().Length}")
                        .WithDescription(string.Join("\n", mentions.ToArray().Skip(cur * 20).Take(20)));
                }, mentions.ToArray().Length, 20).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [Priority(0)]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task AfkMessageType(int num)
            {
                if (num > 4) return;
                await _service.AfkMessageTypeSet(ctx.Guild, num);
                await ctx.Channel.SendConfirmAsync($"Sucessfully set AfkMessageType to {num}");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [Priority(0)]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task AfkLength(int num)
            {
                if (num > 4096)
                {
                    await ctx.Channel.SendErrorAsync(
                        "The Maximum Length is 4096 per Discord limits. Please put a number lower than that.");
                }
                else
                {
                    await _service.AfkLengthSet(ctx.Guild, num);
                    await ctx.Channel.SendConfirmAsync($"AFK Length Sucessfully Set To {num} Characters");
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [Priority(0)]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task AfkType(string ehm)
            {
                switch (ehm.ToLower())
                {
                    case "onmessage":
                    {
                        await _service.AfkTypeSet(ctx.Guild, 3);
                        await ctx.Channel.SendConfirmAsync("Afk will be disabled when a user sends a message.");
                    }
                        break;
                    case "ontype":
                    {
                        await _service.AfkTypeSet(ctx.Guild, 2);
                        await ctx.Channel.SendConfirmAsync("Afk messages will be disabled when a user starts typing.");
                    }
                        break;
                    case "selfdisable":
                    {
                        await _service.AfkTypeSet(ctx.Guild, 1);
                        await ctx.Channel.SendConfirmAsync(
                            "Afk will only be disableable by the user themselves (unless an admin uses the afkrm command)");
                    }
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [Priority(1)]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task AfkType(int ehm)
            {
                switch (ehm)
                {
                    case 3:
                        await AfkType("onmessage");
                        break;
                    case 2:
                        await AfkType("ontype");
                        break;
                    case 1:
                        await AfkType("selfdisable");
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task AfkTimeout(StoopidTime time)
            {
                if (time.Time < TimeSpan.FromSeconds(1) || time.Time > TimeSpan.FromHours(2))
                {
                    await ctx.Channel.SendErrorAsync("The maximum Afk timeout is 2 Hours. Minimum is 1 Second.");
                    return;
                }

                await _service.AfkTimeoutSet(ctx.Guild, time.Time.Seconds);
                await ctx.Channel.SendConfirmAsync($"Your AFK Timeout has been set to {time.Time.Humanize()}");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireUserPermission(GuildPermission.ManageChannels)]
            public async Task AfkUndisable(params ITextChannel[] chan)
            {
                var list = new List<string>();
                var mentions = new List<string>();
                var toremove = new List<string>();
                var chans = _service.GetDisabledAfkChannels(ctx.Guild.Id);
                var e = chans.Split(",");
                foreach (var i in e) list.Add(i);
                foreach (var i in chan)
                    if (e.Contains(i.Id.ToString()))
                    {
                        toremove.Add(i.Id.ToString());
                        mentions.Add(i.Mention);
                    }

                if (!mentions.Any())
                {
                    await ctx.Channel.SendErrorAsync("The channels you have specifed are not set to ignore Afk!");
                    return;
                }

                if (!list.Except(toremove).Any())
                {
                    await _service.AfkDisabledSet(ctx.Guild, "0");
                    await ctx.Channel.SendConfirmAsync("Mewdeko will no longer ignore afk in any channel.");
                    return;
                }

                await _service.AfkDisabledSet(ctx.Guild, string.Join(",", list.Except(toremove)));
                await ctx.Channel.SendConfirmAsync(
                    $"Succesfully removed the channels {string.Join(",", mentions)} from the list of ignored Afk channels.");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireUserPermission(GuildPermission.ManageChannels)]
            public async Task AfkDisable(params ITextChannel[] chan)
            {
                var list = new HashSet<string>();
                var newchans = new HashSet<string>();
                var mentions = new HashSet<string>();
                if (_service.GetDisabledAfkChannels(ctx.Guild.Id) == "0")
                {
                    foreach (var i in chan)
                    {
                        list.Add(i.Id.ToString());
                        mentions.Add(i.Mention);
                    }

                    await _service.AfkDisabledSet(ctx.Guild, string.Join(",", list));
                    await ctx.Channel.SendConfirmAsync(
                        $"Afk has been disabled in the channels {string.Join(",", mentions)}");
                }
                else
                {
                    var e = _service.GetDisabledAfkChannels(ctx.Guild.Id);
                    var w = e.Split(",");
                    foreach (var i in w) list.Add(i);

                    foreach (var i in chan)
                    {
                        if (!w.Contains(i.Id.ToString()))
                        {
                            list.Add(i.Id.ToString());
                            mentions.Add(i.Mention);
                        }

                        newchans.Add(i.Id.ToString());
                    }

                    if (mentions.Count() == 0)
                    {
                        await ctx.Channel.SendErrorAsync(
                            "No channels were added because the channels you specified are already in the list.");
                        return;
                    }

                    await _service.AfkDisabledSet(ctx.Guild, string.Join(",", list));
                    await ctx.Channel.SendConfirmAsync(
                        $"Added {string.Join(",", mentions)} to the list of channels AFK ignores.");
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [Priority(1)]
            public async Task Afk()
            {
                var afkmsg = _service.AfkMessage(ctx.Guild.Id, ctx.User.Id).Select(x => x.Message);
                if (!afkmsg.Any() || afkmsg.Last() == "")
                {
                    await _service.AFKSet(ctx.Guild, (IGuildUser) ctx.User, "_ _", 0);
                    await ctx.Channel.SendConfirmAsync("Afk message enabled!");
                }
                else
                {
                    await _service.AFKSet(ctx.Guild, (IGuildUser) ctx.User, "", 0);
                    await ctx.Channel.SendConfirmAsync("AFK Message has been disabled!");
                }
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
                        await _service.AFKSet(ctx.Guild, i, "", 0);
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

                await _service.AFKSet(ctx.Guild, user, "", 0);
                await ctx.Channel.SendConfirmAsync($"AFK Message for {user.Mention} has been disabled!");
            }
        }
    }
}