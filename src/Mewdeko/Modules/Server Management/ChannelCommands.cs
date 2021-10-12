using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Webhook;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Server_Management.Services;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Server_Management
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

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public async Task LockCheck()
            {
                var msg = await ctx.Channel.SendMessageAsync(
                    "<a:loading:847706744741691402> Making sure role permissions don't get in the way of lockdown...");
                var roles = Context.Guild.Roles.ToList().FindAll(x =>
                    x.Id != Context.Guild.Id && x.Permissions.SendMessages && x.Position <
                    ((SocketGuild)ctx.Guild).CurrentUser.GetRoles().Max(r => r.Position));
                if (roles.Any())
                {
                    foreach (var i in roles)
                    {
                        var perms = i.Permissions;
                        var newperms = perms.Modify(sendMessages: false);
                        await i.ModifyAsync(x => { x.Permissions = newperms; });
                    }

                    await msg.ModifyAsync(x =>
                    {
                        x.Content =
                            "<a:checkfragutil:854536148411744276> Roles checked! You may now run the lockdown command.";
                    });
                }
                else
                {
                    await msg.ModifyAsync(x =>
                    {
                        x.Content =
                            "<a:checkfragutil:854536148411744276> Roles checked! No roles are in the way of the lockdown command.";
                    });
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task LockDown()
            {
                var roles = Context.Guild.Roles.ToList().FindAll(x =>
                    x.Id != Context.Guild.Id && x.Permissions.SendMessages && x.Position <
                    ((SocketGuild)ctx.Guild).CurrentUser.GetRoles().Max(r => r.Position));
                if (roles.Any())
                {
                    await ctx.Channel.SendErrorAsync(
                        "<a:crossfragutil:854536474098663434> Please run the Lockcheck command as you have roles that will get in the way of lockdown");
                    return;
                }

                if (ctx.Guild.EveryoneRole.Permissions.SendMessages == false)
                {
                    await ctx.Channel.SendErrorAsync(
                        "<a:crossfragutil:854536474098663434> Server is already in lockdown!");
                }
                else
                {
                    var everyonerole = ctx.Guild.EveryoneRole;
                    var newperms = everyonerole.Permissions.Modify(sendMessages: false);
                    await everyonerole.ModifyAsync(x => { x.Permissions = newperms; });
                    await ctx.Channel.SendConfirmAsync("Server has been locked down!");
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task MoveTo(IVoiceChannel channel)
            {
                var use = ctx.User as IGuildUser;
                if (use.VoiceChannel == null)
                {
                    await ctx.Channel.SendErrorAsync(
                        "<a:checkfragutil:854536148411744276> You need to be in a voice channel for this!");
                    return;
                }

                await use.ModifyAsync(x => { x.Channel = new Optional<IVoiceChannel>(channel); });
                await ctx.Channel.SendConfirmAsync($"Succesfully moved you to {Format.Bold(channel.Name)}");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task MoveUserTo(IGuildUser user, IVoiceChannel channel)
            {
                if (user.VoiceChannel == null)
                {
                    await ctx.Channel.SendErrorAsync("The user must be in a voice channel for this!");
                    return;
                }

                await user.ModifyAsync(x => { x.Channel = new Optional<IVoiceChannel>(channel); });
                await ctx.Channel.SendConfirmAsync($"Succesfully moved {user.Mention} to {Format.Bold(channel.Name)}");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Grab(IGuildUser user)
            {
                var vc = ((IGuildUser)ctx.User).VoiceChannel;
                if (vc == null)
                {
                    await ctx.Channel.SendErrorAsync("You need to be in a voice channel to use this!");
                    return;
                }

                if (user.VoiceChannel == null)
                {
                    await ctx.Channel.SendErrorAsync(
                        $"{user.Mention} needs to be in a voice channel for this to work!");
                    return;
                }

                await user.ModifyAsync(x => x.Channel = new Optional<IVoiceChannel>(vc));
                await ctx.Channel.SendConfirmAsync($"Grabbed {user.Mention} from {user.VoiceChannel.Name} to your VC!");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task Unlockdown()
            {
                if (ctx.Guild.EveryoneRole.Permissions.SendMessages)
                {
                    await ctx.Channel.SendErrorAsync("Server is not locked down!");
                    return;
                }

                var everyonerole = ctx.Guild.EveryoneRole;
                var newperms = everyonerole.Permissions.Modify(sendMessages: true);
                await everyonerole.ModifyAsync(x => { x.Permissions = newperms; });
                await ctx.Channel.SendConfirmAsync("Server has been unlocked!");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task Nuke(ITextChannel chan3 = null)
            {
                var embed = new EmbedBuilder();
                embed.Color = Mewdeko.Services.Mewdeko.ErrorColor;
                embed.Description =
                    "Are you sure you want to nuke this channel? This will delete the entire channel and remake it.";
                if (!await PromptUserConfirmAsync(embed, ctx.User.Id).ConfigureAwait(false)) return;
                ITextChannel chan;
                if (chan3 is null)
                    chan = ctx.Channel as ITextChannel;
                else
                    chan = chan3;

                await chan.DeleteAsync();
                var chan2 = await ctx.Guild.CreateTextChannelAsync(chan.Name, x =>
                {
                    x.Position = chan.Position;
                    x.Topic = chan.Topic;
                    x.PermissionOverwrites = new Optional<IEnumerable<Overwrite>>(chan.PermissionOverwrites);
                    x.IsNsfw = chan.IsNsfw;
                    x.CategoryId = chan.CategoryId;
                    x.SlowModeInterval = chan.SlowModeInterval;
                });
                await chan2.SendMessageAsync(
                    "https://pa1.narvii.com/6463/6494fab512c8f2ac0d652c44dae78be4cb644569_hq.gif");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Ticket()
            {
                var TickChat = TTicketCategory;
                var chnls = await ctx.Guild.GetChannelsAsync();
                if (chnls.Select(c => c.Name).Contains($"{ctx.User.Username.ToLower().Replace(" ", "-")}s-ticket"))
                {
                    await ctx.Channel.SendErrorAsync("You already have a ticket channel open!");
                    return;
                }

                ITextChannel idfk = null;
                if (TickChat == 0)
                    idfk = await ctx.Guild.CreateTextChannelAsync($"{ctx.User.Username}s-ticket");
                else
                    idfk = await ctx.Guild.CreateTextChannelAsync($"{ctx.User.Username}s-ticket",
                        T => { T.CategoryId = TickChat; });

                await idfk.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                    new OverwritePermissions(viewChannel: PermValue.Deny));
                await idfk.AddPermissionOverwriteAsync(ctx.User,
                    new OverwritePermissions(viewChannel: PermValue.Allow));
                var roles = ctx.Guild.Roles.Where(x => x.Permissions.ManageMessages).Where(x => x.Tags == null);
                foreach (var i in roles)
                    await idfk.AddPermissionOverwriteAsync(i, new OverwritePermissions(viewChannel: PermValue.Allow));

                var msg = await ctx.Channel.SendConfirmAsync(":tickets: Ticket created in " + idfk.Mention);
                await ctx.Message.DeleteAsync();
                msg.DeleteAfter(5);
                await idfk.EmbedAsync(new EmbedBuilder()
                    .WithTitle(":tickets: Ticket Created! ")
                    .WithDescription("A Moderator will be with you shortly!"));
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [Priority(0)]
            public async Task TicketCategory([Remainder] ICategoryChannel channel)
            {
                if (string.IsNullOrWhiteSpace(channel.Name))
                    return;

                if (TTicketCategory == channel.Id)
                {
                    await ctx.Channel.SendErrorAsync("This is already your ticket category!");
                    return;
                }

                if (TTicketCategory == 0)
                {
                    await _service.SetTicketCategoryId(ctx.Guild, channel);
                    var TicketCategory = ((SocketGuild)ctx.Guild).GetCategoryChannel(TTicketCategory);
                    await ctx.Channel.SendConfirmAsync("Your ticket category has been set to " + TicketCategory);
                    return;
                }

                var oldTicketCategory = ((SocketGuild)ctx.Guild).GetCategoryChannel(TTicketCategory);
                await _service.SetTicketCategoryId(ctx.Guild, channel);
                var newTicketCategory = ((SocketGuild)ctx.Guild).GetCategoryChannel(TTicketCategory);
                await ctx.Channel.SendConfirmAsync("Your ticket category has been changed from " + oldTicketCategory +
                                                   " to " + newTicketCategory);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
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
                                        msg += "FILES_UPLOADED: " + string.Join("\n", s.Attachments.Select(x => x.Url));
                                    else if (s.Embeds.Any())
                                        msg += "EMBEDS: " + string.Join("\n--------\n",
                                            s.Embeds.Select(x => $"Description: {x.Description}"));
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
                }
            }


            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
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
                                        msg += "FILES_UPLOADED: " + string.Join("\n", s.Attachments.Select(x => x.Url));
                                    else if (s.Embeds.Any())
                                        msg += "EMBEDS: " + string.Join("\n--------\n",
                                            s.Embeds.Select(x => $"Description: {x.Description}"));
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
                }
                else
                {
                    await ctx.Channel.SendErrorAsync("This != a ticket channel!");
                }
            }


            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            [BotPerm(GuildPerm.ManageMessages)]
            public async Task Lock(SocketTextChannel channel = null)
            {
                if (channel == null)
                {
                    var tch = ctx.Channel as SocketTextChannel;
                    var currentPerms = tch.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                       new OverwritePermissions();
                    await tch.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                        currentPerms.Modify(sendMessages: PermValue.Deny));
                    await ctx.Channel.SendMessageAsync(
                        "<a:checkfragutil:854536148411744276> Locked down " + tch.Mention);
                }
                else
                {
                    var currentPerms = channel.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                       new OverwritePermissions();
                    await channel.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                        currentPerms.Modify(sendMessages: PermValue.Deny));
                    await ctx.Channel.SendMessageAsync("<a:checkfragutil:854536148411744276> Locked down " +
                                                       channel.Mention);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task CreateCatAndTxtChannels(string CatName, params string[] Channels)
            {
                var eb = new EmbedBuilder();
                eb.WithOkColor();
                eb.WithDescription(
                    $"<a:loading:847706744741691402> Creating the Category {CatName} with {Channels.Count()} Text Channels!");
                var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build());
                var cat = await ctx.Guild.CreateCategoryAsync(CatName);
                foreach (var i in Channels) await ctx.Guild.CreateTextChannelAsync(i, x => { x.CategoryId = cat.Id; });

                var eb2 = new EmbedBuilder();
                eb2.WithDescription($"Created the category {CatName} with {Channels.Count()} Text Channels!");
                eb2.WithOkColor();
                await msg.ModifyAsync(x => { x.Embed = eb2.Build(); });
            }


            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task CreateCatAndVcChannels(string CatName, params string[] Channels)
            {
                var eb = new EmbedBuilder();
                eb.WithOkColor();
                eb.WithDescription(
                    $"<a:loading:847706744741691402> Creating the Category {CatName} with {Channels.Count()} Voice Channels");
                var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build());
                var cat = await ctx.Guild.CreateCategoryAsync(CatName);
                foreach (var i in Channels) await ctx.Guild.CreateVoiceChannelAsync(i, x => { x.CategoryId = cat.Id; });

                var eb2 = new EmbedBuilder();
                eb2.WithDescription($"Created the category {CatName} with {Channels.Count()} Voice Channels!");
                eb2.WithOkColor();
                await msg.ModifyAsync(x => { x.Embed = eb2.Build(); });
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task CreateCatVcChans(ICategoryChannel chan, params string[] Channels)
            {
                var eb = new EmbedBuilder();
                eb.WithOkColor();
                eb.WithDescription(
                    $"<a:loading:847706744741691402> Adding {Channels.Length} Voice Channels to {chan.Name}");
                var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build());
                foreach (var i in Channels)
                    await ctx.Guild.CreateVoiceChannelAsync(i, x => { x.CategoryId = chan.Id; });

                var eb2 = new EmbedBuilder();
                eb2.WithDescription($"Added {Channels.Length} Voice Channels to {chan.Name}!");
                eb2.WithOkColor();
                await msg.ModifyAsync(x => { x.Embed = eb2.Build(); });
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task CreateCatTxtChans(ICategoryChannel chan, params string[] Channels)
            {
                var eb = new EmbedBuilder();
                eb.WithOkColor();
                eb.WithDescription(
                    $"<a:loading:847706744741691402> Adding {Channels.Length} Text Channels to {chan.Name}");
                var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build());
                foreach (var i in Channels) await ctx.Guild.CreateTextChannelAsync(i, x => { x.CategoryId = chan.Id; });

                var eb2 = new EmbedBuilder();
                eb2.WithDescription($"Added {Channels.Length} Text Channels to {chan.Name}!");
                eb2.WithOkColor();
                await msg.ModifyAsync(x => { x.Embed = eb2.Build(); });
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            [BotPerm(GuildPerm.ManageMessages)]
            public async Task Unlock(SocketTextChannel channel = null)
            {
                if (channel == null)
                {
                    var tch = ctx.Channel as SocketTextChannel;
                    var currentPerms = tch.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                       new OverwritePermissions();
                    await tch.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                        currentPerms.Modify(sendMessages: PermValue.Inherit));
                    await ctx.Channel.SendMessageAsync("<a:checkfragutil:854536148411744276> Unlocked " + tch.Mention);
                }
                else
                {
                    var currentPerms = channel.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                       new OverwritePermissions();
                    await channel.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                        currentPerms.Modify(sendMessages: PermValue.Inherit));
                    await ctx.Channel.SendMessageAsync("<a:checkfragutil:854536148411744276> Unlocked " +
                                                       channel.Mention);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(0)]
            public async Task Slowmode(int interval, ITextChannel channel)
            {
                {
                    await channel.ModifyAsync(x => { x.SlowModeInterval = interval; });
                    if (interval != 0)
                        await ctx.Channel.SendMessageAsync(
                            $"<a:checkfragutil:854536148411744276> Slowmode has been enabled in {channel.Mention} for {TimeSpan.FromSeconds(interval)}");
                    else
                        await ctx.Channel.SendMessageAsync(
                            $"<a:checkfragutil:854536148411744276> Slowmode has been disabled in {channel.Mention}");
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(1)]
            public async Task Slowmode(int interval)
            {
                await Slowmode(interval, (ITextChannel)ctx.Channel);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
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

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(4)]
            public async Task Slowmode()
            {
                await Slowmode((ITextChannel)ctx.Channel);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
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
                            web = new DiscordWebhookClient(await Channel.CreateWebhookAsync(name, imgStream));
                        else
                            web = new DiscordWebhookClient(webhooks.FirstOrDefault(x => x.Name == name));

                        await web.SendMessageAsync(embeds: embeds);
                    }
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [UserPerm(GuildPerm.Administrator)]
            [Priority(1)]
            public async Task Webhook(ITextChannel Channel, string name, string imageurl, [Remainder] string urls)
            {
                var embeds = new List<Embed>();
                var splits = urls.Split(new[] { '\n', '\r', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var i in splits)
                {
                    var ur = new Uri(i);
                    var e = ur.Segments;
                    var wb = new WebClient();
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
                            web = new DiscordWebhookClient(await Channel.CreateWebhookAsync(name, imgStream));
                        else
                            web = new DiscordWebhookClient(webhooks.FirstOrDefault(x => x.Name == name));

                        await web.SendMessageAsync(embeds: embeds);
                    }
                }
            }
        }
    }
}