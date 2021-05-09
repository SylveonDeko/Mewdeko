using Discord.WebSocket;
using Mewdeko.Core.Services;
using Mewdeko.Extensions;
using Discord;
using System;
using Mewdeko.Core.Services.Database.Models;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Utility.Services
{
    public class UtilityService : INService
    {
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;
        private readonly Mewdeko bot;
        private ConcurrentDictionary<ulong, ulong> _snipeset { get; } = new ConcurrentDictionary<ulong, ulong>();
        private ConcurrentDictionary<ulong, int> _plinks { get; } = new ConcurrentDictionary<ulong, int>();
        private ConcurrentDictionary<ulong, ulong> _reactchans { get; } = new ConcurrentDictionary<ulong, ulong>();
        public UtilityService(DiscordSocketClient client, DbService db, Mewdeko _bot)
        {
            bot = _bot;
            _client = client;
            client.MessageDeleted += MsgStore;
            client.MessageUpdated += MsgStore2;
            client.MessageReceived += MsgReciev;
            client.MessageReceived += MsgReciev2;
            //client.ReactionAdded += ReactionAdded;
            //client.ReactionRemoved += ReactionAdded;
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
            int yesno = -1;
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
            ulong yesno = (ulong)((endis == "enable") ? 1 : 0);
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.snipeset = yesno;
                await uow.SaveChangesAsync();
            }
            _snipeset.AddOrUpdate(guild.Id, yesno, (key, old) => yesno);
        }
        private Task MsgStore(Cacheable<IMessage, ulong> optMsg, ISocketMessageChannel ch)
        {
            _ = Task.Run(async () =>
            {
                if (GetSnipeSet(((SocketTextChannel) ch).Guild.Id) == 0)
                {
                    return;
                }
                
                var msg = (optMsg.HasValue ? optMsg.Value : null) as IUserMessage;
                if (msg.Author.IsBot) return;
                var user = await msg.Channel.GetUserAsync(optMsg.Value.Author.Id);
                if (!user.IsBot)
                {
                    var snipemsg = new SnipeStore()
                    {
                        GuildId = ((SocketTextChannel) ch).Guild.Id,
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
                if (GetSnipeSet(((SocketTextChannel) ch).Guild.Id) == 0)
                {
                    return;
                }

                var msg = (optMsg.HasValue ? optMsg.Value : null) as IUserMessage;
                if (msg.Author.IsBot) return;
                var user = await msg.Channel.GetUserAsync(msg.Author.Id);
                if (!user.IsBot)
                {


                    var snipemsg = new SnipeStore()
                    {
                        GuildId = ((SocketTextChannel) ch).Guild.Id,
                        ChannelId = ch.Id,
                        Message = msg.Content,
                        UserId = msg.Author.Id,
                        Edited = 1

                    };
                    using var uow = _db.GetDbContext();
                    uow.SnipeStore.Add(snipemsg);

                    _ = await uow.SaveChangesAsync();
                }
            }); return Task.CompletedTask;
        }
        public SnipeStore[] Snipemsg(ulong gid, ulong chanid)
        {
            using var uow = _db.GetDbContext();
            return uow.SnipeStore.ForChannel(gid, chanid);
        }
        public async Task MsgReciev2(SocketMessage msg)
        {
            if (msg.Author.IsBot) return;
            if (msg.Channel is SocketDMChannel) return;
            var guild = ((SocketGuildChannel) msg.Channel).Guild.Id;
            var id = GetReactChans(guild);
            if (msg.Channel.Id == id)
            {
                if (msg.Attachments.Any())
                {
                    Emote.TryParse("<:upvote:274492025678856192>", out var emote);
                    Emote.TryParse("<:downvote:274492025720537088>", out var emote2);
                    await msg.AddReactionAsync(emote);
                    await msg.AddReactionAsync(emote2);
                }
            }
        }
        public async Task MsgReciev(SocketMessage msg)
        {
            var gid = ((IGuildChannel) msg.Channel).Guild; 
            if (GetPLinks(gid.Id) == 1)
            { 
            var linkParser = new Regex(@"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                foreach (Match m in linkParser.Matches(msg.Content))
                {

                    var e = new Uri(m.Value);
                    var en = e.Host.Split(".");
                    if (en.Contains("discord"))
                    {
                        var eb = String.Join("", e.Segments).Split("/");
                        if (eb.Contains("channels"))
                        {
                            SocketGuild guild;
                            if (gid.Id != Convert.ToUInt64(eb[2]))
                            {
                                guild = _client.GetGuild(Convert.ToUInt64(eb[2]));
                                if (guild is null) return;
                            }
                            else
                            {
                                guild = gid as SocketGuild;
                            }
                            var em = await ((IGuild)guild).GetTextChannelAsync(Convert.ToUInt64(eb[3]));
                            if (em == null) return;
                            var msg2 = await em.GetMessageAsync(Convert.ToUInt64(eb[4]));
                            if (msg2 is null) return;
                            var en2 = new EmbedBuilder
                            {
                                Color = Mewdeko.OkColor,
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
                                en2.AddField("Embed Content:", msg2.Embeds.FirstOrDefault().Description);
                                if (msg2.Embeds.FirstOrDefault().Image != null)
                                {
                                    en2.ImageUrl = msg2.Embeds.FirstOrDefault().Image.Value.Url;
                                }
                            }
                            if (msg2.Content.Any())
                            {
                                en2.Description = msg2.Content;
                            }
                            if (msg2.Attachments.Any())
                            {
                                en2.ImageUrl = msg2.Attachments.FirstOrDefault().Url;
                            }
                            await msg.Channel.SendMessageAsync("", embed: en2.WithTimestamp(msg2.Timestamp).Build());
                        }
                    }
                }
            }
        }
    }
}
