using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common.Attributes;
using System.Threading.Tasks;
using System;
using Newtonsoft.Json;
using Mewdeko.Extensions;
using Mewdeko.Modules.ServerManagement.Services;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using Discord.Webhook;
using Google.Apis.YouTube.v3.Data;
using Mewdeko.Common;

namespace Mewdeko.Modules.ServerManagement
{
    public partial class ServerManagement
    {
        [Group]
        public class ChannelCommands : MewdekoSubmodule<ServerManagementService>
        {
            private readonly IHttpClientFactory _httpFactory;

            public ChannelCommands(IHttpClientFactory httpfact)
            {
                _httpFactory = httpfact;
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task Nuke(ITextChannel chan3 = null)
            {
                ITextChannel chan;
                if (chan3 is null)
                {
                    chan = ctx.Channel as ITextChannel;
                }
                else
                    chan = chan3;

                await chan.DeleteAsync();
                var chan2 = await ctx.Guild.CreateTextChannelAsync(chan.Name, x =>
                {
                    x.Position = chan.Position;
                    x.Topic = chan.Topic;
                    x.PermissionOverwrites = new Discord.Optional<IEnumerable<Overwrite>>(chan.PermissionOverwrites);
                    x.IsNsfw = chan.IsNsfw;
                    x.CategoryId = chan.CategoryId;
                    x.SlowModeInterval = chan.SlowModeInterval;
                });
                await chan2.SendMessageAsync(
                    "https://pa1.narvii.com/6463/6494fab512c8f2ac0d652c44dae78be4cb644569_hq.gif");
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Ticket()
            {
                ulong TickChat = base.TTicketCategory;
                var chnls = await ctx.Guild.GetChannelsAsync();
                if (chnls.Select(c => c.Name).Contains($"{ctx.User.Username.ToLower().Replace(" ", "-")}s-ticket"))
                {
                    await ctx.Channel.SendErrorAsync("You already have a ticket channel open!");
                    return;
                }

                ITextChannel idfk = null;
                if (TickChat == 0)
                {
                    idfk = await ctx.Guild.CreateTextChannelAsync($"{ctx.User.Username}s-ticket");
                }
                else
                {
                    idfk = await ctx.Guild.CreateTextChannelAsync($"{ctx.User.Username}s-ticket",
                        T => { T.CategoryId = TickChat; });
                }

                await idfk.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                    new OverwritePermissions(viewChannel: PermValue.Deny));
                await idfk.AddPermissionOverwriteAsync(ctx.User,
                    new OverwritePermissions(viewChannel: PermValue.Allow));
                var roles = ctx.Guild.Roles.Where(x => x.Permissions.ManageMessages).Where(x => x.Tags == null);
                foreach (var i in roles)
                {
                    await idfk.AddPermissionOverwriteAsync(i, new OverwritePermissions(viewChannel: PermValue.Allow));
                }

                var msg = await ctx.Channel.SendConfirmAsync(":tickets: Ticket created in " + idfk.Mention);
                await ctx.Message.DeleteAsync();
                msg.DeleteAfter(5);
                await idfk.EmbedAsync(new EmbedBuilder()
                    .WithTitle(":tickets: Ticket Created! ")
                    .WithDescription("A Moderator will be with you shortly!"));
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [Priority(0)]
            public async Task TicketCategory([Remainder] ICategoryChannel channel)
            {
                if (string.IsNullOrWhiteSpace(channel.Name))
                    return;

                if (base.TTicketCategory == channel.Id)
                {
                    await ctx.Channel.SendErrorAsync("This is already your ticket category!");
                    return;
                }

                if (base.TTicketCategory == 0)
                {
                    await CmdHandler.SetTicketCategoryId(ctx.Guild, channel);
                    var TicketCategory = ((SocketGuild) ctx.Guild).GetCategoryChannel(base.TTicketCategory);
                    await ctx.Channel.SendConfirmAsync("Your ticket category has been set to " + TicketCategory);
                    return;
                }

                var oldTicketCategory = ((SocketGuild) ctx.Guild).GetCategoryChannel(base.TTicketCategory);
                await CmdHandler.SetTicketCategoryId(ctx.Guild, channel);
                var newTicketCategory = ((SocketGuild) ctx.Guild).GetCategoryChannel(base.TTicketCategory);
                await ctx.Channel.SendConfirmAsync("Your ticket category has been changed from " + oldTicketCategory +
                                                   " to " + newTicketCategory);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            [Priority(0)]
            public async Task Close()
            {
                var chn = ctx.Channel;

                if (chn.Name.EndsWith("s-ticket"))
                {
                    var msgs = new List<IMessage>(9999999);
                    await ctx.Channel.GetMessagesAsync(99999999).ForEachAsync(dled => msgs.AddRange(dled))
                        .ConfigureAwait(false);

                    var title = $"Ticket Log from {ctx.Guild.Name} in {ctx.Channel.Name} at {DateTime.Now}.txt";
                    var grouping = msgs.GroupBy(x => $"{x.CreatedAt.Date:dd.MM.yyyy}")
                        .Select(g => new
                        {
                            date = g.Key,
                            messages = g.OrderBy(x => x.CreatedAt).Select(s =>
                            {
                                var msg = $"【{s.Timestamp:HH:mm:ss}】{s.Author}:";
                                if (string.IsNullOrWhiteSpace(s.ToString()))
                                {
                                    if (s.Attachments.Any())
                                    {
                                        msg += "FILES_UPLOADED: " + string.Join("\n", s.Attachments.Select(x => x.Url));
                                    }
                                    else if (s.Embeds.Any())
                                    {
                                        msg += "EMBEDS: " + string.Join("\n--------\n",
                                            s.Embeds.Select(x => $"Description: {x.Description}"));
                                    }
                                }
                                else
                                {
                                    msg += s.ToString();
                                }

                                return msg;
                            })
                        });
                    using (var stream = await JsonConvert.SerializeObject(grouping, Formatting.Indented).ToStream()
                        .ConfigureAwait(false))
                    {
                        await ctx.User.SendFileAsync(stream, title, title, false).ConfigureAwait(false);
                    }

                    await (chn as ITextChannel).DeleteAsync();
                    return;
                }
            }


            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            [Priority(1)]
            public async Task Close(ITextChannel chn)
            {
                if (chn.Name.EndsWith("s-ticket"))
                {
                    var msgs = new List<IMessage>(9999999);
                    await chn.GetMessagesAsync(99999999).ForEachAsync(dled => msgs.AddRange(dled))
                        .ConfigureAwait(false);

                    var title = $"Ticket Log from {ctx.Guild.Name} in {chn.Name} at {DateTime.Now}.txt";
                    var grouping = msgs.GroupBy(x => $"{x.CreatedAt.Date:dd.MM.yyyy}")
                        .Select(g => new
                        {
                            date = g.Key,
                            messages = g.OrderBy(x => x.CreatedAt).Select(s =>
                            {
                                var msg = $"【{s.Timestamp:HH:mm:ss}】{s.Author}:";
                                if (string.IsNullOrWhiteSpace(s.ToString()))
                                {
                                    if (s.Attachments.Any())
                                    {
                                        msg += "FILES_UPLOADED: " + string.Join("\n", s.Attachments.Select(x => x.Url));
                                    }
                                    else if (s.Embeds.Any())
                                    {
                                        msg += "EMBEDS: " + string.Join("\n--------\n",
                                            s.Embeds.Select(x => $"Description: {x.Description}"));
                                    }
                                }
                                else
                                {
                                    msg += s.ToString();
                                }

                                return msg;
                            })
                        });
                    using (var stream = await JsonConvert.SerializeObject(grouping, Formatting.Indented).ToStream()
                        .ConfigureAwait(false))
                    {
                        try
                        {
                            await ctx.User.SendFileAsync(stream, title, title, false).ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            await ctx.Channel.SendErrorAsync(
                                "It looks like your DMs are closed so I could not send the ticket log to you!");
                        }

                    }

                    await chn.DeleteAsync();
                    return;
                }
                else
                {
                    await ctx.Channel.SendErrorAsync("This != a ticket channel!");
                }
            }


            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            [BotPerm(GuildPerm.ManageMessages)]
            public async Task Lock(SocketTextChannel channel = null)
            {
                if (channel == null)
                {
                    var tch = ctx.Channel as SocketTextChannel;
                    OverwritePermissions currentPerms = tch.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                                        new OverwritePermissions();
                    await tch.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                        currentPerms.Modify(sendMessages: PermValue.Deny));
                    await ctx.Channel.SendMessageAsync("<:greentick:784535639717707776> Locked down " + tch.Mention);

                }
                else
                {
                    OverwritePermissions currentPerms = channel.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                                        new OverwritePermissions();
                    await channel.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                        currentPerms.Modify(sendMessages: PermValue.Deny));
                    await ctx.Channel.SendMessageAsync("<:greentick:784535639717707776> Locked down " +
                                                       channel.Mention);
                }
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task CreateCatAndTxtChannels(string CatName, params string[] Channels)
            {
                var eb = new EmbedBuilder();
                eb.WithOkColor();
                eb.WithDescription(
                    $"<a:loading:834915210967253013> Creating the Category {CatName} with {Channels.Count()} Text Channels!");
                var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build());
                ICategoryChannel cat = await ctx.Guild.CreateCategoryAsync(CatName);
                foreach (var i in Channels)
                {
                    await ctx.Guild.CreateTextChannelAsync(i, x => { x.CategoryId = cat.Id; });
                }

                var eb2 = new EmbedBuilder();
                eb2.WithDescription($"Created the category {CatName} with {Channels.Count()} Text Channels!");
                eb2.WithOkColor();
                await msg.ModifyAsync(x => { x.Embed = eb2.Build(); });
            }

            [MewdekoCommand, Usage, Description, Aliases]
            public async Task CatId(ICategoryChannel chan)
            {
                await ctx.Channel.SendConfirmAsync(
                    $"The ID of {Format.Bold(chan.Name)} is {Format.Code(chan.Id.ToString())}");
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task CreateCatAndVcChannels(string CatName, params string[] Channels)
            {
                var eb = new EmbedBuilder();
                eb.WithOkColor();
                eb.WithDescription(
                    $"<a:loading:834915210967253013> Creating the Category {CatName} with {Channels.Count()} Voice Channels");
                var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build());
                ICategoryChannel cat = await ctx.Guild.CreateCategoryAsync(CatName);
                foreach (var i in Channels)
                {
                    await ctx.Guild.CreateVoiceChannelAsync(i, x => { x.CategoryId = cat.Id; });
                }

                var eb2 = new EmbedBuilder();
                eb2.WithDescription($"Created the category {CatName} with {Channels.Count()} Voice Channels!");
                eb2.WithOkColor();
                await msg.ModifyAsync(x => { x.Embed = eb2.Build(); });
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task CreateCatVcChans(ICategoryChannel chan, params string[] Channels)
            {
                var eb = new EmbedBuilder();
                eb.WithOkColor();
                eb.WithDescription(
                    $"<a:loading:834915210967253013> Adding {Channels.Length} Voice Channels to {chan.Name}");
                var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build());
                foreach (var i in Channels)
                {
                    await ctx.Guild.CreateVoiceChannelAsync(i, x => { x.CategoryId = chan.Id; });
                }

                var eb2 = new EmbedBuilder();
                eb2.WithDescription($"Added {Channels.Length} Voice Channels to {chan.Name}!");
                eb2.WithOkColor();
                await msg.ModifyAsync(x => { x.Embed = eb2.Build(); });
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task CreateCatTxtChans(ICategoryChannel chan, params string[] Channels)
            {
                var eb = new EmbedBuilder();
                eb.WithOkColor();
                eb.WithDescription(
                    $"<a:loading:834915210967253013> Adding {Channels.Length} Text Channels to {chan.Name}");
                var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build());
                foreach (var i in Channels)
                {
                    await ctx.Guild.CreateTextChannelAsync(i, x => { x.CategoryId = chan.Id; });
                }

                var eb2 = new EmbedBuilder();
                eb2.WithDescription($"Added {Channels.Length} Text Channels to {chan.Name}!");
                eb2.WithOkColor();
                await msg.ModifyAsync(x => { x.Embed = eb2.Build(); });
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            [BotPerm(GuildPerm.ManageMessages)]
            public async Task Unlock(SocketTextChannel channel = null)
            {
                if (channel == null)
                {
                    var tch = ctx.Channel as SocketTextChannel;
                    OverwritePermissions currentPerms = tch.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                                        new OverwritePermissions();
                    await tch.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                        currentPerms.Modify(sendMessages: PermValue.Inherit));
                    await ctx.Channel.SendMessageAsync("<:greentick:784535639717707776> Unlocked " + tch.Mention);
                }
                else
                {
                    OverwritePermissions currentPerms = channel.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                                        new OverwritePermissions();
                    await channel.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                        currentPerms.Modify(sendMessages: PermValue.Inherit));
                    await ctx.Channel.SendMessageAsync("<:greentick:784535639717707776> Unlocked " + channel.Mention);
                }
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(0)]
            public async Task Slowmode(int interval, ITextChannel channel)
            {
                {
                    await channel.ModifyAsync(x => { x.SlowModeInterval = interval; });
                    if (interval != 0)
                        await ctx.Channel.SendMessageAsync(
                            $"Slowmode has been enabled in {channel.Mention} for {TimeSpan.FromSeconds(interval)}");
                    else
                        await ctx.Channel.SendMessageAsync($"Slowmode has been disabled in {channel.Mention}");
                }
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(1)]
            public async Task Slowmode(int interval)
                => await Slowmode(interval, (ITextChannel) ctx.Channel);

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(2)]
            public async Task Slowmode(ITextChannel channel)
            {
                if (channel.SlowModeInterval != 0)
                    await Slowmode(0, channel);
                else
                    await Slowmode(60, channel);
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(4)]
            public async Task Slowmode()
                => await Slowmode((ITextChannel) ctx.Channel);

            [MewdekoCommand, Usage, Description, Aliases]
            [UserPerm(GuildPerm.Administrator)]
            [Priority(0)]
            public async Task Webhook(ITextChannel Channel, string name, string imageurl)
            {
                var embeds = new List<Embed>();
                var attachment = ctx.Message.Attachments;
                foreach (var i in attachment)
                {
                    var client = new WebClient();
                    var stream = client.OpenRead(i.Url);
                    var reader = new StreamReader(stream);
                    var content = reader.ReadToEnd();
                    CREmbed.TryParse(content, out var embedData);
                    embeds.Add(embedData.ToEmbed().Build());
                }

                using var http = _httpFactory.CreateClient();
                var uri = new Uri(imageurl);
                using (var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false))
                {
                    var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    using (var imgStream = imgData.ToStream())
                    {
                        var webhooks = await Channel.GetWebhooksAsync();
                        DiscordWebhookClient web = null;
                        if (webhooks.FirstOrDefault(x => x.Name == name) is null)
                        {
                            web = new DiscordWebhookClient(await Channel.CreateWebhookAsync(name, imgStream));
                        }
                        else
                        {
                            web = new DiscordWebhookClient(webhooks.FirstOrDefault(x => x.Name == name));
                        }

                        await web.SendMessageAsync(embeds: embeds);
                    }
                }
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [UserPerm(GuildPerm.Administrator)]
            [Priority(1)]
            public async Task Webhook(ITextChannel Channel, string name, string imageurl, [Remainder] string urls)
            {
                var embeds = new List<Embed>();
                var splits = urls.Split(new[] {'\n', '\r', ' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
                foreach (var i in splits)
                {
                    var ur = new Uri(i);
                    var e = ur.Segments;
                    WebClient wb = new WebClient();
                    var Download = wb.DownloadString($"https://pastebin.com/raw/{e[1]}");
                    CREmbed.TryParse(Download, out var embedData);
                    embeds.Add(embedData.ToEmbed().Build());
                }

                using var http = _httpFactory.CreateClient();
                var uri = new Uri(imageurl);
                using (var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false))
                {
                    var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    using (var imgStream = imgData.ToStream())
                    {
                        var webhooks = await Channel.GetWebhooksAsync();
                        DiscordWebhookClient web = null;
                        if (webhooks.FirstOrDefault(x => x.Name == name) is null)
                        {
                            web = new DiscordWebhookClient(await Channel.CreateWebhookAsync(name, imgStream));
                        }
                        else
                        {
                            web = new DiscordWebhookClient(webhooks.FirstOrDefault(x => x.Name == name));
                        }

                        await web.SendMessageAsync(embeds: embeds);
                    }
                }
            }
        }
    }
}