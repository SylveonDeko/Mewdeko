using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;

namespace Mewdeko.Modules.Utility.Services
{
    public class StarboardService : INService
    {
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;
        public Mewdeko _bot;
        public CommandHandler _CmdHandler;

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

        private ConcurrentDictionary<ulong, ulong> _starcount { get; } = new();
        private ConcurrentDictionary<ulong, ulong> _starboardchannels { get; } = new();
        private ConcurrentDictionary<ulong, ulong> _starboardstar { get; } = new();

        public async Task SetStarboardChannel(IGuild guild, ulong channel)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.StarboardChannel = channel;
                await uow.SaveChangesAsync();
            }

            _starboardchannels.AddOrUpdate(guild.Id, channel, (key, old) => channel);
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

        public async Task SetStar(IGuild guild, ulong emote)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
                gc.Star = emote;
                await uow.SaveChangesAsync();
            }

            _starboardstar.AddOrUpdate(guild.Id, emote, (key, old) => emote);
        }

        public ulong GetStar(ulong? id)
        {
            if (id == null || !_starboardstar.TryGetValue(id.Value, out var star))
                return 0;

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
            var starboard = new Starboard
            {
                PostId = msg,
                MessageId = msg2
            };
            using var uow = _db.GetDbContext();
            uow.Starboard.Add(starboard);

            await uow.SaveChangesAsync();
        }

        // all code here was used by Builderb's old Starboat source code (pls give him love <3)
        private async Task OnReactionAddedAsync(Cacheable<IUserMessage, ulong> message,
            Cacheable<IMessageChannel, ulong> channel,
            SocketReaction reaction)
        {
            if (channel.Value is IGuildChannel chane)
            {
                var guild = (SocketGuild) chane.Guild;
                if (reaction.User.Value.IsBot) return;
                Emoji star1 = null;
                Emote star = null;
                if (reaction.Emote is Emote emote1)
                {
                    if (GetStar(guild.Id) != 0)
                    {
                        try
                        {
                            star = guild.GetEmoteAsync(GetStar(guild.Id)).Result;
                        }
                        catch
                        {
                            return;
                        }

                        if (emote1.Id != star.Id) return;
                    }
                    else
                    {
                        return;
                    }
                }

                if (reaction.Emote is Emoji eee)
                {
                    star1 = new Emoji("⭐");
                    if (eee.Name != star1.Name) return;
                }

                var msg = await message.GetOrDownloadAsync();
                var e = StarboardIds(message.Id);

                if (star != null)
                    if (reaction.Emote.Name != star.Name)
                        return;

                if (star1 != null)
                    if (reaction.Emote.Name != star1.Name)
                        return;

                var reactions = 0;
                if (star != null) reactions = msg.Reactions[star].ReactionCount;

                if (star1 != null) reactions = msg.Reactions[star1].ReactionCount;

                var chanID = GetStarboardChannel(guild.Id);
                if (chanID == 0) return;
                var chan = guild.GetTextChannel(chanID);
                var stars = GetStarSetting(guild.Id);
                if (Convert.ToUInt64(reactions) >= stars)
                {
                    IUserMessage message2 = null;
                    if (!e.Any())
                        message2 = null;
                    else
                        message2 =
                            await chan.GetMessageAsync(e.OrderByDescending(e => e.DateAdded).FirstOrDefault().PostId) as
                                IUserMessage;

                    if (msg.Channel.Id == chanID)
                        return;

                    var em = new EmbedBuilder
                    {
                        Author = new EmbedAuthorBuilder
                        {
                            Name = msg.Author.ToString(),
                            IconUrl = msg.Author.GetAvatarUrl(ImageFormat.Auto, 2048)
                        },
                        Description = $"[Jump to message]({msg.GetJumpUrl()})",
                        Color = Mewdeko.OkColor,
                        Footer = new EmbedFooterBuilder {Text = "Message Posted Date"}
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

                    if (msg.Content.Any()) em.Description = $"{msg.Content}\n\n{em.Description}";

                    if (msg.Attachments.Any()) em.ImageUrl = msg.Attachments.FirstOrDefault().Url;

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
                        var msg2 = await chan.SendMessageAsync($"{reactions} {star} {star1}",
                            embed: em.WithTimestamp(msg.Timestamp).Build());
                        await Starboard(msg.Id, msg2.Id);
                    }
                }
            }
            // do some epic jeff
        }

        private async Task OnReactionRemoveAsync(Cacheable<IUserMessage, ulong> message,
            Cacheable<IMessageChannel, ulong> channel,
            SocketReaction reaction)
        {
            if (channel.Value is IGuildChannel chane)
            {
                var guild = (SocketGuild) chane.Guild;
                if (reaction.User.Value.IsBot) return;
                Emoji star1 = null;
                Emote star = null;
                if (reaction.Emote is Emote emote1)
                {
                    if (GetStar(guild.Id) != 0)
                    {
                        try
                        {
                            star = guild.GetEmoteAsync(GetStar(guild.Id)).Result;
                        }
                        catch
                        {
                            return;
                        }

                        if (emote1.Id != star.Id) return;
                    }
                    else
                    {
                        return;
                    }
                }

                if (reaction.Emote is Emoji eee)
                {
                    star1 = new Emoji("⭐");
                    if (eee.Name != star1.Name) return;
                }

                var msg = await message.GetOrDownloadAsync();
                if (star is null && star1 is null) return;
                if (star != null)
                    if (reaction.Emote.Name != star.Name)
                        return;

                if (star1 != null)
                    if (reaction.Emote.Name != star1.Name)
                        return;

                if (star != null)
                    if (reaction.Emote.Name != star.Name)
                        return;

                if (star1 != null)
                    if (reaction.Emote.Name != star1.Name)
                        return;

                int reactions;
                if (star != null && msg.Reactions.ContainsKey(star)) reactions = msg.Reactions[star].ReactionCount;

                if (star1 != null && msg.Reactions.ContainsKey(star1))
                    reactions = msg.Reactions[star1].ReactionCount;
                else
                    reactions = 0;

                ;
                var e = StarboardIds(message.Id);
                var stars = GetStarSetting(guild.Id);
                //get the values before doing anything
                var chanID = GetStarboardChannel(guild.Id);
                var chan = guild.GetTextChannel(chanID);
                if (Convert.ToUInt64(reactions) < stars)
                {
                    IUserMessage message2 = null;
                    if (e.Length == 0)
                        message2 = null;
                    else
                        message2 = await chan.GetMessageAsync(e.OrderByDescending(e => e.DateAdded).FirstOrDefault()
                            .PostId) as IUserMessage;

                    if (msg.Channel.Id == chanID)
                        return;

                    if (message2 != null) await message2.DeleteAsync();
                    return;
                }

                var em = new EmbedBuilder
                {
                    Author =
                        new EmbedAuthorBuilder
                        {
                            Name = msg.Author.ToString(),
                            IconUrl = msg.Author.GetAvatarUrl(ImageFormat.Auto, 2048)
                        },
                    Description = $"[Jump to message]({msg.GetJumpUrl()})",
                    Color = Mewdeko.OkColor,
                    Footer = new EmbedFooterBuilder
                    {
                        Text = "Message Posted Date"
                    }
                };
                if (msg.Content.Any()) em.Description = $"{msg.Content}\n\n{em.Description}";

                if (msg.Attachments.Any()) em.ImageUrl = msg.Attachments.FirstOrDefault().Url;

                if (e.Any())
                {
                    var message2 =
                        await chan.GetMessageAsync(e.OrderByDescending(e => e.DateAdded).FirstOrDefault().PostId) as
                            IUserMessage;
                    if (message2 != null)
                        await message2.ModifyAsync(x =>
                        {
                            x.Embed = em.WithTimestamp(msg.Timestamp).Build();
                            x.Content = $"{reactions} {star}{star1}";
                        });
                }

                //do some epic jeff
            }
        }

        public Starboard[] StarboardIds(ulong msgid)
        {
            using var uow = _db.GetDbContext();
            return uow.Starboard.ForMsgId(msgid);
        }
    }
}