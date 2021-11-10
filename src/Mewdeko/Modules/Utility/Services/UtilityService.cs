using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Utility.Services
{
    public class UtilityService : INService
    {
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;
        private readonly Mewdeko.Services.Mewdeko bot;

        public UtilityService(DiscordSocketClient client, DbService db, Mewdeko.Services.Mewdeko _bot)
        {
            _ = StartSnipeCleanupLoop();
            bot = _bot;
            _client = client;
            client.MessageDeleted += MsgStore;
            client.MessageUpdated += MsgStore2;
            client.MessageReceived += MsgReciev;
            client.MessageReceived += MsgReciev2;
            _db = db;
            _snipeset = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.snipeset)
                .ToConcurrent();
            _plinks = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.PreviewLinks)
                .ToConcurrent();
            _reactchans = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.ReactChannel)
                .ToConcurrent();
        }

        private ConcurrentDictionary<ulong, ulong> _snipeset { get; } = new();
        private ConcurrentDictionary<ulong, int> _plinks { get; } = new();
        private ConcurrentDictionary<ulong, ulong> _reactchans { get; } = new();
        

        private async Task StartSnipeCleanupLoop()
        {
            while (true)
            {
                await Task.Delay(5);
                try
                {
                    var now = DateTime.UtcNow - TimeSpan.FromDays(30);
                    var reminders = await GetOldSnipes(now);
                    if (reminders.Length == 0)
                        continue;

                    Log.Information($"Cleaning up {reminders.Length} old snipes");

                    // make groups of 5, with 1.5 second inbetween each one to ensure against ratelimits
                    var i = 0;
                    foreach (var group in reminders
                        .GroupBy(_ => ++i / (reminders.Length / 5 + 1)))
                    {
                        var executedReminders = group.ToList();
                        await RemoveOldSnipes(executedReminders);
                        await Task.Delay(1500);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"Error in reminder loop: {ex.Message}");
                    Log.Warning(ex.ToString());
                }
            }
        }

        private Task RemoveOldSnipes(List<SnipeStore> list)
        {
            foreach (var i in list)
            {
                _db.GetDbContext().SnipeStore.Remove(i.Id);
                Console.WriteLine(i.Id);
            }
            return Task.CompletedTask;
        }
        private  Task<SnipeStore[]> GetOldSnipes(DateTime now)
        {
            using (var uow = _db.GetDbContext())
            {
                return uow._context.SnipeStore
                    .FromSqlInterpolated(
                        $"select * from SnipeStore where \"DateAdded\" < {now};")
                    .ToArrayAsync();
            }
        }
        public int GetPLinks(ulong? id)
        {
            if (id == null || !_plinks.TryGetValue(id.Value, out var invw))
                return 0;

            return invw;
        }

        public ulong GetReactChans(ulong? id)
        {
            if (id == null || !_reactchans.TryGetValue(id.Value, out var invw))
                return 0;

            return invw;
        }

        public async Task SetReactChan(IGuild guild, ulong yesnt)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.ReactChannel = yesnt;
                await uow.SaveChangesAsync();
            }

            _reactchans.AddOrUpdate(guild.Id, yesnt, (key, old) => yesnt);
        }

        public async Task PreviewLinks(IGuild guild, string yesnt)
        {
            var yesno = -1;
            using (var uow = _db.GetDbContext())
            {
                switch (yesnt)
                {
                    case "y":
                        yesno = 1;
                        break;
                    case "n":
                        yesno = 0;
                        break;
                }
            }

            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.PreviewLinks = yesno;
                await uow.SaveChangesAsync();
            }

            _plinks.AddOrUpdate(guild.Id, yesno, (key, old) => yesno);
        }

        public ulong GetSnipeSet(ulong? id)
        {
            _snipeset.TryGetValue(id.Value, out var snipeset);
            return snipeset;
        }

        public async Task SnipeSet(IGuild guild, string endis)
        {
            var yesno = (ulong)(endis == "enable" ? 1 : 0);
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.snipeset = yesno;
                await uow.SaveChangesAsync();
            }

            _snipeset.AddOrUpdate(guild.Id, yesno, (key, old) => yesno);
        }

        private Task MsgStore(Cacheable<IMessage, ulong> optMsg, Cacheable<IMessageChannel, ulong> ch)
        {
            _ = Task.Run(async () =>
            {
                if (GetSnipeSet(((SocketTextChannel)ch.Value).Guild.Id) == 0) return;

                if ((optMsg.HasValue ? optMsg.Value : null) is not IUserMessage msg || msg.Author.IsBot) return;
                var user = await msg.Channel.GetUserAsync(optMsg.Value.Author.Id);
                if (user is null) return;
                if (!user.IsBot)
                {
                    var snipemsg = new SnipeStore
                    {
                        GuildId = ((SocketTextChannel)ch.Value).Guild.Id,
                        ChannelId = ch.Id,
                        Message = msg.Content,
                        UserId = msg.Author.Id,
                        Edited = 0
                    };
                    using var uow = _db.GetDbContext();
                    uow.SnipeStore.Add(snipemsg);

                    await uow.SaveChangesAsync();
                }
            });
            return Task.CompletedTask;
        }

        private Task MsgStore2(Cacheable<IMessage, ulong> optMsg, SocketMessage imsg2,
            ISocketMessageChannel ch)
        {
            _ = Task.Run(async () =>
            {
                if (GetSnipeSet(((SocketTextChannel)ch).Guild.Id) == 0) return;

                if ((optMsg.HasValue ? optMsg.Value : null) is not IUserMessage msg || msg.Author.IsBot) return;
                var user = await msg.Channel.GetUserAsync(msg.Author.Id);
                if (user is null) return;
                if (!user.IsBot)
                {
                    var snipemsg = new SnipeStore
                    {
                        GuildId = ((SocketTextChannel)ch).Guild.Id,
                        ChannelId = ch.Id,
                        Message = msg.Content,
                        UserId = msg.Author.Id,
                        Edited = 1
                    };
                    using var uow = _db.GetDbContext();
                    uow.SnipeStore.Add(snipemsg);

                    _ = await uow.SaveChangesAsync();
                }
            });
            return Task.CompletedTask;
        }

        public SnipeStore[] Snipemsg(ulong gid, ulong chanid)
        {
            using var uow = _db.GetDbContext();
            return uow.SnipeStore.ForChannel(gid, chanid);
        }
        public SnipeStore[] AllSnipes()
        {
            using var uow = _db.GetDbContext();
            return uow.SnipeStore.All();
        }

        public async Task MsgReciev2(SocketMessage msg)
        {
            if (msg.Author.IsBot) return;
            if (msg.Channel is SocketDMChannel) return;
            var guild = ((SocketGuildChannel)msg.Channel).Guild.Id;
            var id = GetReactChans(guild);
            if (msg.Channel.Id == id)
            {
                Emote.TryParse("<:upvote:863122283283742791>", out var emote);
                Emote.TryParse("<:D_downvote:863122244527980613>", out var emote2);
                await Task.Delay(200);
                await msg.AddReactionAsync(emote);
                await Task.Delay(200);
                await msg.AddReactionAsync(emote2);
            }
        }

        public async Task MsgReciev(SocketMessage msg)
        {
            if (msg.Channel is SocketTextChannel t)
            {
                SocketGuild gid;
                gid = t.Guild;
                if (GetPLinks(gid.Id) == 1)
                {
                    var linkParser =
                        new Regex(
                            @"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)",
                            RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    foreach (Match m in linkParser.Matches(msg.Content))
                    {
                        var e = new Uri(m.Value);
                        var en = e.Host.Split(".");
                        if (!en.Contains("discord")) continue;
                        var eb = string.Join("", e.Segments).Split("/");
                        if (!eb.Contains("channels")) continue;
                        SocketGuild guild;
                        if (gid.Id != Convert.ToUInt64(eb[2]))
                        {
                            guild = _client.GetGuild(Convert.ToUInt64(eb[2]));
                            if (guild is null) return;
                        }
                        else
                        {
                            guild = gid;
                        }

                        if (guild != t.Guild)
                            return;
                        var em = await ((IGuild)guild).GetTextChannelAsync(Convert.ToUInt64(eb[3]));
                        if (em == null) return;
                        var msg2 = await em.GetMessageAsync(Convert.ToUInt64(eb[4]));
                        if (msg2 is null) return;
                        var en2 = new EmbedBuilder
                        {
                            Color = Mewdeko.Services.Mewdeko.OkColor,
                            Author = new EmbedAuthorBuilder
                            {
                                Name = msg2.Author.Username,
                                IconUrl = msg2.Author.GetAvatarUrl(size: 2048)
                            },
                            Footer = new EmbedFooterBuilder
                            {
                                IconUrl = ((IGuild)guild).IconUrl,
                                Text = $"{((IGuild)guild).Name}: {em.Name}"
                            }
                        };
                        if (msg2.Embeds.Any())
                        {
                            en2.AddField("Embed Content:", msg2.Embeds.FirstOrDefault()?.Description);
                            if (msg2.Embeds.FirstOrDefault()!.Image != null)
                                en2.ImageUrl = msg2.Embeds.FirstOrDefault()? .Image.Value.Url;
                        }

                        if (msg2.Content.Any()) en2.Description = msg2.Content;

                        if (msg2.Attachments.Any()) en2.ImageUrl = msg2.Attachments.FirstOrDefault().Url;

                        await msg.Channel.SendMessageAsync("",
                            embed: en2.WithTimestamp(msg2.Timestamp).Build());
                    }
                }
            }
        }
    }
}