using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Humanizer;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Extensions.Interactive;
using Mewdeko.Common.Extensions.Interactive.Entities.Page;
using Mewdeko.Common.Extensions.Interactive.Pagination;
using Mewdeko.Common.Extensions.Interactive.Pagination.Lazy;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Afk.Services;

namespace Mewdeko.Modules.Afk
{
    public class AFK : MewdekoModule<AFKService>
        {
            private readonly InteractiveService Interactivity;

            public AFK(InteractiveService serv)
            {
                Interactivity = serv;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [Priority(0)]
            public async Task Afk([Remainder]string message = null)
            {
                if (message == null)
                {
                    var afkmsg = _service.AfkMessage(ctx.Guild.Id, ctx.User.Id).Select(x => x.Message);
                    if (!afkmsg.Any() || afkmsg.Last() == "")
                    {
                        await _service.AFKSet(ctx.Guild, (IGuildUser)ctx.User, "_ _", 0);
                        await ctx.Channel.SendConfirmAsync("Afk message enabled!");
                        await ctx.Guild.DownloadUsersAsync();
                        return;
                    }
                    else
                    {
                        await _service.AFKSet(ctx.Guild, (IGuildUser)ctx.User, "", 0);
                        await ctx.Channel.SendConfirmAsync("AFK Message has been disabled!");
                        await ctx.Guild.DownloadUsersAsync();
                        return;
                    }
                }
                if (message.Length != 0 && message.Length > _service.GetAfkLength(ctx.Guild.Id))
                {
                    await ctx.Channel.SendErrorAsync(
                        $"That's too long! The length for afk on this server is set to {_service.GetAfkLength(ctx.Guild.Id)} characters.");
                    return;
                }

                await _service.AFKSet(ctx.Guild, (IGuildUser)ctx.User, message, 0);
                await ctx.Channel.SendConfirmAsync($"AFK Message set to:\n{message}");
                await ctx.Guild.DownloadUsersAsync();
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
                        $"You already have a regular afk set! Please disable it by doing {Prefix}afk and try again");
                    return;
                }

                await ctx.Channel.SendConfirmAsync(
                    $"AFK Message set to:\n{message}\n\nAFK will unset in {time.Time.Humanize()}");
                await _service.TimedAfk(ctx.Guild, ctx.User, message, time.Time);
                if (_service.IsAfk(ctx.Guild, ctx.User as IGuildUser))
                    await ctx.Channel.SendMessageAsync(
                        $"Welcome back {ctx.User.Mention} I have removed your timed AFK.");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task CustomAfkMessage([Remainder] string embed)
            {
                CREmbed crEmbed;
                CREmbed.TryParse(embed, out crEmbed);
                if (embed == "-")
                {
                    await _service.SetCustomAfkMessage(ctx.Guild, embed);
                    await ctx.Channel.SendConfirmAsync("Afk messages will now have the default look.");
                    return;
                }

                if (crEmbed is not null && !crEmbed.IsValid || !embed.Contains("%afk"))
                {
                    await ctx.Channel.SendErrorAsync("The embed code you provided cannot be used for afk messages!");
                    return;
                }

                await _service.SetCustomAfkMessage(ctx.Guild, embed);
                var ebe = CREmbed.TryParse(_service.GetCustomAfkMessage(ctx.Guild.Id), out crEmbed);
                if (ebe is false)
                {
                    await _service.SetCustomAfkMessage(ctx.Guild, "-");
                    await ctx.Channel.SendErrorAsync(
                        "There was an error checking the embed, it may be invalid, so I set the afk message back to default. Please dont hesitate to ask for embed help in the support server at https://discord.gg/6n3aa9Xapf.");
                    return;
                }

                await ctx.Channel.SendConfirmAsync("Sucessfully updated afk message!");
            }
            [Priority(0)]
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task GetActiveAfks()
            {
                var afks = _service.GetAfkUsers(ctx.Guild);
                if(!afks.Any())
                {
                    await ctx.Channel.SendErrorAsync("There are no currently AFK users!");
                    return;
                }
                var paginator = new LazyPaginatorBuilder()
                   .AddUser(ctx.User)
                   .WithPageFactory(PageFactory)
                   .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                   .WithMaxPageIndex(afks.ToArray().Length / 20)
                   .WithDefaultEmotes()
                   .Build();

                await Interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

                Task<PageBuilder> PageFactory(int page)
                {
                    {
                        return Task.FromResult(new PageBuilder().WithOkColor()
                            .WithTitle(Format.Bold("Active AFKs") + $" - {afks.ToArray().Length}")
                            .WithDescription(string.Join("\n", afks.ToArray().Skip(page * 20).Take(20))));
                    }
                }
            }

            [Priority(0)]
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task AfkView(IGuildUser user)
            {
                if (_service.IsAfk(user.Guild, user))
                {
                    await ctx.Channel.SendErrorAsync("This user isn't afk!");
                    return;
                }
                var msg = _service.AfkMessage(user.Guild.Id, user.Id).Last();
                await ctx.Channel.SendConfirmAsync($"{user}'s Afk is:\n{msg.Message}");
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

                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(mentions.ToArray().Length / 20)
                    .WithDefaultEmotes()
                    .Build();

                await Interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

                Task<PageBuilder> PageFactory(int page)
                {
                    {
                        return Task.FromResult(new PageBuilder().WithOkColor()
                            .WithTitle(Format.Bold("Disabled Afk Channels") + $" - {mentions.ToArray().Length}")
                            .WithDescription(string.Join("\n", mentions.ToArray().Skip(page * 20).Take(20))));
                    }
                }
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

                await _service.AfkTimeoutSet(ctx.Guild, Convert.ToInt32(time.Time.TotalSeconds));
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