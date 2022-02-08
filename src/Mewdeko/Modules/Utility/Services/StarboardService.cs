using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Services.Database.Models;
using SixLabors.ImageSharp.Drawing;
using System.Collections.Generic;

namespace Mewdeko.Modules.Utility.Services;

public class StarboardService : INService
{
    private readonly DiscordSocketClient _client;
    private readonly DbService _db;
    public Mewdeko.Services.Mewdeko Bot;
    public CommandHandler CmdHandler;

    public StarboardService(DiscordSocketClient client, CommandHandler cmdhandler, DbService db,
        Mewdeko.Services.Mewdeko bot)
    {
        Bot = bot;
        _client = client;
        CmdHandler = cmdhandler;
        _db = db;
        _client.ReactionAdded += OnReactionAddedAsync;
        // _client.MessageDeleted += OnMessageDeletedAsync;
        _client.ReactionRemoved += OnReactionRemoveAsync;
        Starboardchannels = bot.AllGuildConfigs
            .ToDictionary(x => x.GuildId, x => x.StarboardChannel)
            .ToConcurrent();
        Starcount = bot.AllGuildConfigs
            .ToDictionary(x => x.GuildId, x => x.Stars)
            .ToConcurrent();
        Starboardstar = bot.AllGuildConfigs
            .ToDictionary(x => x.GuildId, x => x.Star2)
            .ToConcurrent();
        repostThresholdDictionary = bot.AllGuildConfigs
            .ToDictionary(x => x.GuildId, x => x.RepostThreshold)
            .ToConcurrent();
        _ = CacheStarboardPosts();


        //_client.ReactionsCleared += OnAllReactionsClearedAsync;
    }

    private ConcurrentDictionary<ulong, int> Starcount { get; }
    private ConcurrentDictionary<ulong, int> repostThresholdDictionary { get; }
    private ConcurrentDictionary<ulong, ulong> Starboardchannels { get; }
    private ConcurrentDictionary<ulong, string> Starboardstar { get; }

    private List<Starboard> starboardPosts;

    public Task CacheStarboardPosts() =>
        _ = Task.Run(() =>
        {
            using var uow = _db.GetDbContext();
            var all = uow.Starboard.GetAll().ToList();
            starboardPosts = all.Any() ? all : new List<Starboard>();
        });

    public async Task AddStarboardPost(ulong messageId, ulong postId)
    {
        using var uow = _db.GetDbContext();
        var post = starboardPosts.FirstOrDefault(x => x.MessageId == messageId);
        if (post is null)
        {
            var toadd = new Starboard {MessageId = messageId, PostId = postId};
            starboardPosts.Add(toadd);
            uow.Starboard.Add(toadd);
            await uow.SaveChangesAsync();
            return;
        }

        if (post is not null && post.PostId == postId)
            return;
        
        starboardPosts.Remove(post);
        post.PostId = postId;
        uow.Starboard.Update(post);
        await uow.SaveChangesAsync();
    }

    public async Task RemoveStarboardPost(ulong messageId)
    {
        var toremove = starboardPosts.FirstOrDefault(x => x.MessageId == messageId);
        using var uow = _db.GetDbContext();
        uow.Starboard.Remove(toremove);
        starboardPosts.Remove(toremove);
        await uow.SaveChangesAsync();
    }
    
    public async Task SetStarboardChannel(IGuild guild, ulong channel)
    {
        using (var uow = _db.GetDbContext())
        {
            var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
            gc.StarboardChannel = channel;
            await uow.SaveChangesAsync();
        }

        Starboardchannels.AddOrUpdate(guild.Id, channel, (_, _) => channel);
    }

    public async Task SetStarCount(IGuild guild, int num)
    {
        using (var uow = _db.GetDbContext())
        {
            var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
            gc.Stars = num;
            await uow.SaveChangesAsync();
        }

        Starcount.AddOrUpdate(guild.Id, num, (_, _) => num);
    }

    public int GetStarCount(ulong? id)
    {
        if (id == null || !Starcount.TryGetValue(id.Value, out var invw))
            return 0;

        return invw;
    }

    public int GetThreshold(ulong? id)
    {
        if (id == null || !repostThresholdDictionary.TryGetValue(id.Value, out var hold))
            return 0;

        return hold;
    }
    public async Task SetStar(IGuild guild, string emote)
    {
        using (var uow = _db.GetDbContext())
        {
            var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
            gc.Star2 = emote;
            await uow.SaveChangesAsync();
        }

        Starboardstar.AddOrUpdate(guild.Id, emote, (_, _) => emote);
    }
    
    public async Task SetRepostThreshold(IGuild guild, int threshold)
    {
        using (var uow = _db.GetDbContext())
        {
            var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
            gc.RepostThreshold = threshold;
            await uow.SaveChangesAsync();
        }

        repostThresholdDictionary.AddOrUpdate(guild.Id, threshold, (_, _) => threshold);
    }

    public string GetStar(ulong? id)
    {
        if (id == null || !Starboardstar.TryGetValue(id.Value, out var star))
            return null;

        return star;
    }

    public ulong GetStarboardChannel(ulong? id)
    {
        if (id == null || !Starboardchannels.TryGetValue(id.Value, out var invw))
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
    
    private async Task OnReactionAddedAsync(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        
        if (reaction.User.Value.IsBot 
            || !channel.HasValue 
            || channel.Value is not ITextChannel textChannel 
            || GetStarCount(textChannel.GuildId) == 0)
            return;

        var star = GetStar(textChannel.GuildId).ToIEmote();
        
        if (star.Name == null)
            return;
        
        if (!Equals(reaction.Emote, star))
            return;
        
        var starboardChannelSetting = GetStarboardChannel(textChannel.GuildId);
        
        if (starboardChannelSetting == 0)
            return;
        
        var starboardChannel = await textChannel.Guild.GetTextChannelAsync(starboardChannelSetting);

        if (starboardChannel is null)
            return;
        
        var cGuser = await textChannel.Guild.GetUserAsync(_client.CurrentUser.Id);
        
        var botperms = cGuser.GetPermissions(starboardChannel);
        
        if (!botperms.Has(ChannelPermission.SendMessages))
            return;
        IUserMessage newmessage;
        if (!message.HasValue)
            newmessage = await message.GetOrDownloadAsync();
        else
            newmessage = message.Value as IUserMessage;

        var count = await newmessage.GetReactionUsersAsync(star, Int32.MaxValue).FlattenAsync();

        if (count.Count() != GetStarCount(textChannel.GuildId))
            return;
        
        var maybePost = starboardPosts.FirstOrDefault(x => x.MessageId == newmessage.Id);
        if (maybePost is not null)
        {
            if (GetThreshold(textChannel.GuildId) > 0)
            {
                var messages = await starboardChannel.GetMessagesAsync(GetThreshold(textChannel.GuildId)).FlattenAsync();
                var post = messages.FirstOrDefault(x => x.Id == maybePost.PostId);
                if (post is not null)
                {
                    var post2 = post as IUserMessage;
                    var eb1 = new EmbedBuilder().WithOkColor().WithAuthor(newmessage.Author)
                                               .WithDescription(newmessage.Content)
                                               .AddField("**Source**", $"[Jump!]({newmessage.GetJumpUrl()})")
                                               .WithFooter(message.Id.ToString())
                                               .WithTimestamp(newmessage.Timestamp);
                    if (newmessage.Attachments.Any())
                        eb1.WithImageUrl(newmessage.Attachments.FirstOrDefault().Url);

                    await post2.ModifyAsync(x =>
                    {
                        x.Content = $"{star} **{count.Count()}** {textChannel.Mention}";
                        x.Embed = eb1.Build();
                    });

                }
                else
                {
                    var tryGetOldPost = await starboardChannel.GetMessageAsync(post.Id);
                    if (tryGetOldPost is not null)
                        try
                        {
                            await tryGetOldPost.DeleteAsync();
                        }
                        catch 
                        {
                            // ignored
                        }
                    var eb2 = new EmbedBuilder().WithOkColor().WithAuthor(newmessage.Author)
                                               .WithDescription(newmessage.Content)
                                               .AddField("**Source**", $"[Jump!]({newmessage.GetJumpUrl()})")
                                               .WithFooter(message.Id.ToString())
                                               .WithTimestamp(newmessage.Timestamp);
                    if (newmessage.Attachments.Any())
                        eb2.WithImageUrl(newmessage.Attachments.FirstOrDefault().Url);

                    var msg1 = await starboardChannel.SendMessageAsync($"{star} **{count.Count()}** {textChannel.Mention}", embed: eb2.Build());
                    await AddStarboardPost(message.Id, msg1.Id);

                }
            }
            else
            {
                var tryGetOldPost = await starboardChannel.GetMessageAsync(maybePost.PostId);
                if (tryGetOldPost is not null)
                {
                    var toModify = tryGetOldPost as IUserMessage;
                    var eb1 = new EmbedBuilder().WithOkColor().WithAuthor(newmessage.Author)
                                                .WithDescription(newmessage.Content)
                                                .AddField("**Source**", $"[Jump!]({newmessage.GetJumpUrl()})")
                                                .WithFooter(message.Id.ToString())
                                                .WithTimestamp(newmessage.Timestamp);
                    if (newmessage.Attachments.Any())
                        eb1.WithImageUrl(newmessage.Attachments.FirstOrDefault().Url);

                    await toModify.ModifyAsync(x =>
                    {
                        x.Content = $"{star} **{count.Count()}** {textChannel.Mention}";
                        x.Embed = eb1.Build();
                    });
                }
                else
                {
                    var eb2 = new EmbedBuilder().WithOkColor().WithAuthor(newmessage.Author)
                                               .WithDescription(newmessage.Content)
                                               .AddField("**Source**", $"[Jump!]({newmessage.GetJumpUrl()})")
                                               .WithFooter(message.Id.ToString())
                                               .WithTimestamp(newmessage.Timestamp);
                    if (newmessage.Attachments.Any())
                        eb2.WithImageUrl(newmessage.Attachments.FirstOrDefault().Url);

                    var msg1 = await starboardChannel.SendMessageAsync($"{star} **{count.Count()}** {textChannel.Mention}", embed: eb2.Build());
                    await AddStarboardPost(message.Id, msg1.Id);
                }
            }
        }
        else
        {
            var eb = new EmbedBuilder().WithOkColor().WithAuthor(newmessage.Author)
                                       .WithDescription(newmessage.Content)
                                       .AddField("**Source**", $"[Jump!]({newmessage.GetJumpUrl()})")
                                       .WithFooter(message.Id.ToString())
                                       .WithTimestamp(newmessage.Timestamp);
            if (newmessage.Attachments.Any())
                eb.WithImageUrl(newmessage.Attachments.FirstOrDefault().Url);

            var msg = await starboardChannel.SendMessageAsync($"{star} **{count.Count()}** {textChannel.Mention}", embed: eb.Build());
            await AddStarboardPost(message.Id, msg.Id);
        }

    }

    private async Task OnReactionRemoveAsync(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        /*if (channel.Value is IGuildChannel chane)
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
            var chanId = GetStarboardChannel(guild.Id);
            var chan = guild.GetTextChannel(chanId);
            if (Convert.ToUInt64(reactions) < stars)
            {
                IUserMessage message2 = null;
                if (e.Length == 0)
                    message2 = null;
                else
                    message2 = await chan.GetMessageAsync(e.OrderByDescending(e => e.DateAdded).FirstOrDefault()
                        .PostId) as IUserMessage;

                if (msg.Channel.Id == chanId)
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
                Color = Mewdeko.Services.Mewdeko.OkColor,
                Footer = new EmbedFooterBuilder
                {
                    Text = "Message Posted Date"
                }
            };
            if (msg.Content.Any()) em.Description = $"{msg.Content}\n\n{em.Description}";

            if (msg.Attachments.Any()) em.ImageUrl = msg.Attachments.FirstOrDefault().Url;

            if (e.Any())
                if (await chan.GetMessageAsync(e.OrderByDescending(e => e.DateAdded).FirstOrDefault().PostId) is
                    IUserMessage message2)
                    await message2.ModifyAsync(x =>
                    {
                        x.Embed = em.WithTimestamp(msg.Timestamp).Build();
                        x.Content = $"{reactions} {star}{star1}";
                    });

            //do some epic jeff
        }*/
    }

    public Starboard GetMessage(ulong id)
    {
        using var uow = _db.GetDbContext();
        return uow.Starboard.ForMsgId(id);
    }
}