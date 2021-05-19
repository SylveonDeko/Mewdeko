using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Core.Services;
using Mewdeko.Extensions;
using Mewdeko.Core.Services.Database.Models;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore.Migrations;
using NLog.LayoutRenderers.Wrappers;


namespace Mewdeko.Modules.Utility.Services
{
    public class StarboardService : INService
    {
        private DiscordSocketClient _client;
        public CommandHandler _CmdHandler;
        private readonly DbService _db;
        public Mewdeko _bot;
        private ConcurrentDictionary<ulong, ulong> _starcount { get; } = new ConcurrentDictionary<ulong, ulong>();
        private ConcurrentDictionary<ulong, ulong> _starboardchannels { get; } = new ConcurrentDictionary<ulong, ulong>();
        private ConcurrentDictionary<ulong, string> _starboardstar { get; } = new ConcurrentDictionary<ulong, string>();

        public StarboardService(DiscordSocketClient client, CommandHandler cmdhandler, DbService db, Mewdeko bot)
        {
            _bot = bot;
            _client = client;
            _CmdHandler = cmdhandler;
            _db = db;
            _client.ReactionAdded += OnReactionAddedAsync;
            // _client.MessageDeleted += OnMessageDeletedAsync;
            _client.ReactionRemoved += OnReactionRemoveAsync;
            _starboardchannels = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.StarboardChannel)
                .ToConcurrent();
            _starcount = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.Stars)
                .ToConcurrent();
            _starboardstar = bot.AllGuildConfigs
                .ToDictionary(x => x.GuildId, x => x.Star)
                .ToConcurrent();
            //_client.ReactionsCleared += OnAllReactionsClearedAsync;
        }
        public async Task SetStarboardChannel(IGuild guild, ITextChannel channel)
        {

            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.StarboardChannel = channel.Id;
                await uow.SaveChangesAsync();
            }
            _starboardchannels.AddOrUpdate(guild.Id, channel.Id, (key, old) => channel.Id);
        }
        public async Task SetStarCount(IGuild guild, ulong num)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.Stars = num;
                await uow.SaveChangesAsync();
            }
            _starcount.AddOrUpdate(guild.Id, num, (key, old) => num);
        }
        public ulong GetStarSetting(ulong? id)
        {
            if (id == null || !_starcount.TryGetValue(id.Value, out var invw))
                return 0;

            return invw;
        }
        public async Task SetStar(IGuild guild, string emote)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.Star = emote;
                await uow.SaveChangesAsync();
            }
            _starboardstar.AddOrUpdate(guild.Id, emote, (key, old) => emote);
        }
        public string GetStar(ulong? id)
        {
            if (id == null || !_starboardstar.TryGetValue(id.Value, out string star))
                return null;

            return star;
        }
        public ulong GetStarboardChannel(ulong? id)
        {
            if (id == null || !_starboardchannels.TryGetValue(id.Value, out var invw))
                return 0;

            return invw;
        }
        private async Task Starboard(ulong msg2, ulong msg)
        {
            var starboard = new Starboard()
            {
                PostId = msg,
                MessageId = msg2
            };
            using var uow = _db.GetDbContext();
            uow.Starboard.Add(starboard);

            await uow.SaveChangesAsync();
        }

        // all code here was used by Builderb's old Starboat source code (pls give him love <3)
        private async Task OnReactionAddedAsync(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            
            if (reaction.User.Value.IsBot) return;
            var msg = await message.GetOrDownloadAsync();
            var e = StarboardIds(message.Id);
            var guild = ((SocketGuildChannel)channel).Guild;
            Emoji star1 = null;
            Emote star = null;
            if (GetStar(guild.Id) != null && GetStar(guild.Id) != "none")
            {
                star = Emote.Parse(GetStar(guild.Id));
            }
            else
                star1 = new Emoji("⭐");

            if (star != null)
            {
                if (reaction.Emote.Name != star.Name) return;
            }

            if (star1 != null)
            {
                if (reaction.Emote.Name != star1.Name) return;
            }

            int reactions = 0;
            if (star != null)
            { reactions = msg.Reactions[star].ReactionCount; }
            if (star1 != null)
            { reactions = msg.Reactions[star1].ReactionCount; }
            var chanID = GetStarboardChannel(guild.Id);
            if (chanID == 0) return;
            var chan = guild.GetTextChannel(chanID);
            var stars = GetStarSetting(guild.Id);
            if (Convert.ToUInt64(reactions) >= stars)
            {
                IUserMessage message2 = null;
                if (e.Length == 0)
                {
                    message2 = null;
                }
                else
                {
                    message2 = await chan.GetMessageAsync(e.OrderByDescending(e => e.DateAdded).FirstOrDefault().PostId) as IUserMessage;
                }

                if (message2 != null)
                {
                    if (message2.Id == message.Id) return;
                }

                var em = new EmbedBuilder
                {
                    Author =
                    new EmbedAuthorBuilder
                    {
                        Name = msg.Author.ToString(),
                        IconUrl = msg.Author.GetAvatarUrl(ImageFormat.Auto, size: 2048),
                    },
                    Description = $"[Jump to message]({msg.GetJumpUrl()})",
                    Color = Mewdeko.OkColor,
                    Footer = new EmbedFooterBuilder
                    {
                        Text = "Message Posted Date"
                    }
                };
                if (msg.Author.IsBot is true && msg.Embeds.Any())
                {
                    if (msg.Embeds.FirstOrDefault().Fields.Any())
                        foreach (var i in msg.Embeds.FirstOrDefault().Fields)
                            em.AddField(i.Name, i.Value);

                    if (msg.Embeds.FirstOrDefault().Description.Any())
                        em.Description = msg.Embeds.FirstOrDefault().Description;

                    if (msg.Embeds.FirstOrDefault().Footer.HasValue)
                        em.AddField("Footer Text", msg.Embeds.FirstOrDefault().Footer.Value);
                }

                if (msg.Content.Any())
                {
                    em.Description = $"{msg.Content}\n\n{em.Description}";
                }
                if (msg.Attachments.Any())
                {
                    em.ImageUrl = msg.Attachments.FirstOrDefault().Url;
                }
                if (e.Any() && message2 != null)
                {
                    await message2.ModifyAsync(x =>
                    {
                        x.Embed = em.WithTimestamp(msg.Timestamp).Build();
                        x.Content = $"{reactions} {star}";
                    });
                }
                else
                {

                    var msg2 = await chan.SendMessageAsync($"{reactions} {star} {star1}", embed: em.WithTimestamp(msg.Timestamp).Build());
                    await Starboard(msg.Id, msg2.Id);
                }
            }
           // do some epic jeff
        }
        private async Task OnReactionRemoveAsync(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            var msg = await message.GetOrDownloadAsync();
            var guild = ((SocketGuildChannel)channel).Guild;
            Emoji star1 = null;
            Emote star = null;
            if (GetStar(guild.Id) != null && GetStar(guild.Id) != "none")
            {
                star = Emote.Parse(GetStar(guild.Id));
            }
            else
                star1 = new Emoji("⭐");

            if (star is null && star1 is null) return;
            if (star != null)
            {
                if (reaction.Emote.Name != star.Name) return;
            }

            if (star1 != null)
            {
                if (reaction.Emote.Name != star1.Name) return;
            }
            if (star != null)
            {
                if (reaction.Emote.Name != star.Name) return;
            }

            if (star1 != null)
            {
                if (reaction.Emote.Name != star1.Name) return;
            }
            int reactions;
            if (star != null && ((IUserMessage)msg).Reactions.ContainsKey(star))
            {
                reactions = ((IUserMessage)msg).Reactions[star].ReactionCount;
            }
            if (star1 != null && ((IUserMessage)msg).Reactions.ContainsKey(star1))
            {
                reactions = ((IUserMessage)msg).Reactions[star1].ReactionCount;
            }
            else { reactions = 0; };
            var e = StarboardIds(message.Id);
            var stars = GetStarSetting(guild.Id);
            //get the values before doing anything
            var chanID = GetStarboardChannel(guild.Id);
            var chan = guild.GetTextChannel(chanID);
            if (Convert.ToUInt64(reactions) < stars)
            {
                IUserMessage message2 = null;
                if (e.Length == 0)
                {
                    message2 = null;
                }
                else
                {
                    message2 = await chan.GetMessageAsync(e.OrderByDescending(e => e.DateAdded).FirstOrDefault().PostId) as IUserMessage;
                }
                if (message2 != null)
                {
                    if (message2.Id == message.Id) return;
                }
                if (message2 != null) await message2.DeleteAsync();
                return;
            }

            var em = new EmbedBuilder
            {
                Author =
                new EmbedAuthorBuilder
                {
                    Name = msg.Author.ToString(),
                    IconUrl = msg.Author.GetAvatarUrl(ImageFormat.Auto, size: 2048),
                },
                Description = $"[Jump to message]({msg.GetJumpUrl()})",
                Color = Mewdeko.OkColor,
                Footer = new EmbedFooterBuilder
                {
                    Text = "Message Posted Date"
                }
            };
            if (msg.Content.Any())
            {
                em.Description = $"{msg.Content}\n\n{em.Description}";
            }
            if (msg.Attachments.Any())
            {
                em.ImageUrl = msg.Attachments.FirstOrDefault().Url;
            }
            if (e.Any())
            {
                var message2 = await chan.GetMessageAsync(e.OrderByDescending(e => e.DateAdded).FirstOrDefault().PostId) as IUserMessage;
                if (message2 != null)
                {
                    await message2.ModifyAsync(x =>
                    {
                        x.Embed = em.WithTimestamp(msg.Timestamp).Build();
                        x.Content = $"{reactions} {star}{star1}";
                    });
                }
            }
            //do some epic jeff
        }
        public Starboard[] StarboardIds(ulong msgid)
        {
            using var uow = _db.GetDbContext();
            return uow.Starboard.ForMsgId(msgid);
        }
    }
}
