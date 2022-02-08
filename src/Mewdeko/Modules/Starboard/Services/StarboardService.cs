using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Services.Database.Models;
using System.Collections.Generic;

namespace Mewdeko.Modules.Starboard.Services;

// ReSharper disable once ClassNeverInstantiated.Global
public class StarboardService : INService
{
    private readonly DiscordSocketClient _client;
    private readonly DbService _db;

    // ReSharper disable once PublicConstructorInAbstractClass
    public StarboardService(DiscordSocketClient client,  DbService db,
        Mewdeko bot)
    {
        _client = client;
        _db = db;
        _client.ReactionAdded += OnReactionAddedAsync;
        // _client.MessageDeleted += OnMessageDeletedAsync;
        _client.ReactionRemoved += OnReactionRemoveAsync;
        StarboardChannels = bot.AllGuildConfigs
            .ToDictionary(x => x.GuildId, x => x.StarboardChannel)
            .ToConcurrent();
        StarCounts = bot.AllGuildConfigs
            .ToDictionary(x => x.GuildId, x => x.Stars)
            .ToConcurrent();
        StarboardStars = bot.AllGuildConfigs
            .ToDictionary(x => x.GuildId, x => x.Star2)
            .ToConcurrent();
        RepostThresholdDictionary = bot.AllGuildConfigs
            .ToDictionary(x => x.GuildId, x => x.RepostThreshold)
            .ToConcurrent();
        _ = CacheStarboardPosts();


        //_client.ReactionsCleared += OnAllReactionsClearedAsync;
    }

    private ConcurrentDictionary<ulong, int> StarCounts { get; }
    private ConcurrentDictionary<ulong, int> RepostThresholdDictionary { get; }
    private ConcurrentDictionary<ulong, ulong> StarboardChannels { get; }
    private ConcurrentDictionary<ulong, string> StarboardStars { get; }

    private List<StarboardPosts> starboardPosts;

    private Task CacheStarboardPosts() =>
        _ = Task.Run(() =>
        {
            using var uow = _db.GetDbContext();
            var all = uow.Starboard.All().ToList();
            starboardPosts = all.Any() ? all : new List<StarboardPosts>();
        });

    private async Task AddStarboardPost(ulong messageId, ulong postId)
    {
        using var uow = _db.GetDbContext();
        var post = starboardPosts.FirstOrDefault(x => x.MessageId == messageId);
        if (post is null)
        {
            var toAdd = new StarboardPosts {MessageId = messageId, PostId = postId};
            starboardPosts.Add(toAdd);
            uow.Starboard.Add(toAdd);
            await uow.SaveChangesAsync();
            return;
        }

        if (post.PostId == postId)
            return;
        
        starboardPosts.Remove(post);
        post.PostId = postId;
        uow.Starboard.Update(post);
        starboardPosts.Add(post);
        await uow.SaveChangesAsync();
    }

    private async Task RemoveStarboardPost(ulong messageId)
    {
        var toRemove = starboardPosts.FirstOrDefault(x => x.MessageId == messageId);
        using var uow = _db.GetDbContext();
        uow.Starboard.Remove(toRemove);
        starboardPosts.Remove(toRemove);
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

        StarboardChannels.AddOrUpdate(guild.Id, channel, (_, _) => channel);
    }

    public async Task SetStarCount(IGuild guild, int num)
    {
        using (var uow = _db.GetDbContext())
        {
            var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
            gc.Stars = num;
            await uow.SaveChangesAsync();
        }

        StarCounts.AddOrUpdate(guild.Id, num, (_, _) => num);
    }

    public int GetStarCount(ulong? id)
    {
        if (id == null || !StarCounts.TryGetValue(id.Value, out var starCount))
            return 0;

        return starCount;
    }

    private int GetThreshold(ulong? id)
    {
        if (id == null || !RepostThresholdDictionary.TryGetValue(id.Value, out var hold))
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

        StarboardStars.AddOrUpdate(guild.Id, emote, (_, _) => emote);
    }
    
    public async Task SetRepostThreshold(IGuild guild, int threshold)
    {
        using (var uow = _db.GetDbContext())
        {
            var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
            gc.RepostThreshold = threshold;
            await uow.SaveChangesAsync();
        }

        RepostThresholdDictionary.AddOrUpdate(guild.Id, threshold, (_, _) => threshold);
    }

    public string GetStar(ulong? id)
    {
        if (id == null || !StarboardStars.TryGetValue(id.Value, out var star))
            return null;

        return star;
    }

    private ulong GetStarboardChannel(ulong? id)
    {
        if (id == null || !StarboardChannels.TryGetValue(id.Value, out var starboardChannel))
            return 0;

        return starboardChannel;
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

        if (starboardChannel == null)
            return;
        
        var gUser = await textChannel.Guild.GetUserAsync(_client.CurrentUser.Id);
        
        var botPerms = gUser.GetPermissions(starboardChannel);
        
        if (!botPerms.Has(ChannelPermission.SendMessages))
            return;
        
        IUserMessage newMessage;
        if (!message.HasValue)
            newMessage = await message.GetOrDownloadAsync();
        else
            newMessage = message.Value;

        var emoteCount = await newMessage.GetReactionUsersAsync(star, int.MaxValue).FlattenAsync();
        var count = emoteCount.Where(x => !x.IsBot);
        var enumerable = count as IUser[] ?? count.ToArray();
        if (enumerable.Length < GetStarCount(textChannel.GuildId))
            return;
        
        var maybePost = starboardPosts.FirstOrDefault(x => x.MessageId == newMessage.Id);
        if (maybePost != null)
        {
            if (GetThreshold(textChannel.GuildId) > 0)
            {
                var messages = await starboardChannel.GetMessagesAsync(GetThreshold(textChannel.GuildId)).FlattenAsync();
                var post = messages.FirstOrDefault(x => x.Id == maybePost.PostId);
                if (post is not null)
                {
                    var post2 = post as IUserMessage;
                    var eb1 = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author)
                                               .WithDescription(newMessage.Content)
                                               .AddField("**Source**", $"[Jump!]({newMessage.GetJumpUrl()})")
                                               .WithFooter(message.Id.ToString())
                                               .WithTimestamp(newMessage.Timestamp);
                    if (newMessage.Attachments.Any())
                        eb1.WithImageUrl(newMessage.Attachments.FirstOrDefault()!.Url);

                    await post2!.ModifyAsync(x =>
                    {
                        x.Content = $"{star} **{enumerable.Length}** {textChannel.Mention}";
                        x.Embed = eb1.Build();
                    });

                }
                else
                {
                    var tryGetOldPost = await starboardChannel.GetMessageAsync(maybePost.PostId);
                    if (tryGetOldPost is not null)
                        try
                        {
                            await tryGetOldPost.DeleteAsync();
                        }
                        catch 
                        {
                            // ignored
                        }
                    var eb2 = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author)
                                               .WithDescription(newMessage.Content)
                                               .AddField("**Source**", $"[Jump!]({newMessage.GetJumpUrl()})")
                                               .WithFooter(message.Id.ToString())
                                               .WithTimestamp(newMessage.Timestamp);
                    if (newMessage.Attachments.Any())
                        eb2.WithImageUrl(newMessage.Attachments.FirstOrDefault()!.Url);

                    var msg1 = await starboardChannel.SendMessageAsync($"{star} **{enumerable.Length}** {textChannel.Mention}", embed: eb2.Build());
                    await AddStarboardPost(message.Id, msg1.Id);

                }
            }
            else
            {
                var tryGetOldPost = await starboardChannel.GetMessageAsync(maybePost.PostId);
                if (tryGetOldPost is not null)
                {
                    var toModify = tryGetOldPost as IUserMessage;
                    var eb1 = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author)
                                                .WithDescription(newMessage.Content)
                                                .AddField("**Source**", $"[Jump!]({newMessage.GetJumpUrl()})")
                                                .WithFooter(message.Id.ToString())
                                                .WithTimestamp(newMessage.Timestamp);
                    if (newMessage.Attachments.Any())
                        eb1.WithImageUrl(newMessage.Attachments.FirstOrDefault()!.Url);

                    await toModify!.ModifyAsync(x =>
                    {
                        x.Content = $"{star} **{enumerable.Length}** {textChannel.Mention}";
                        x.Embed = eb1.Build();
                    });
                }
                else
                {
                    var eb2 = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author)
                                               .WithDescription(newMessage.Content)
                                               .AddField("**Source**", $"[Jump!]({newMessage.GetJumpUrl()})")
                                               .WithFooter(message.Id.ToString())
                                               .WithTimestamp(newMessage.Timestamp);
                    if (newMessage.Attachments.Any())
                        eb2.WithImageUrl(newMessage.Attachments.FirstOrDefault()!.Url);

                    var msg1 = await starboardChannel.SendMessageAsync($"{star} **{enumerable.Length}** {textChannel.Mention}", embed: eb2.Build());
                    await AddStarboardPost(message.Id, msg1.Id);
                }
            }
        }
        else
        {
            var eb = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author)
                                       .WithDescription(newMessage.Content)
                                       .AddField("**Source**", $"[Jump!]({newMessage.GetJumpUrl()})")
                                       .WithFooter(message.Id.ToString())
                                       .WithTimestamp(newMessage.Timestamp);
            if (newMessage.Attachments.Any())
                eb.WithImageUrl(newMessage.Attachments.FirstOrDefault()!.Url);

            var msg = await starboardChannel.SendMessageAsync($"{star} **{enumerable.Length}** {textChannel.Mention}", embed: eb.Build());
            await AddStarboardPost(message.Id, msg.Id);
        }

    }

    private async Task OnReactionRemoveAsync(Cacheable<IUserMessage, ulong> message,
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

        if (starboardChannel == null)
            return;
        
        var gUser = await textChannel.Guild.GetUserAsync(_client.CurrentUser.Id);
        
        var botPerms = gUser.GetPermissions(starboardChannel);
        
        if (!botPerms.Has(ChannelPermission.SendMessages))
            return;
        
        IUserMessage newMessage;
        if (!message.HasValue)
            newMessage = await message.GetOrDownloadAsync();
        else
            newMessage = message.Value;

        var emoteCount = await newMessage.GetReactionUsersAsync(star, int.MaxValue).FlattenAsync();
        var maybePost = starboardPosts.FirstOrDefault(x => x.MessageId == newMessage.Id);
        if (maybePost == null)
            return;
        var count = emoteCount.Where(x => !x.IsBot);
        var enumerable = count as IUser[] ?? count.ToArray();
        if (enumerable.Length < GetStarCount(textChannel.GuildId))
        {
            await RemoveStarboardPost(newMessage.Id);
            try
            {
                var post = await starboardChannel.GetMessageAsync(maybePost.PostId);
                await post.DeleteAsync();
            }
            catch
            {
                // ignored
            }
        }
        else
        {
            if (GetThreshold(textChannel.GuildId) > 0)
            {
                var messages = await starboardChannel.GetMessagesAsync(GetThreshold(textChannel.GuildId)).FlattenAsync();
                var post = messages.FirstOrDefault(x => x.Id == maybePost.PostId);
                if (post is not null)
                {
                    var post2 = post as IUserMessage;
                    var eb1 = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author)
                                               .WithDescription(newMessage.Content)
                                               .AddField("**Source**", $"[Jump!]({newMessage.GetJumpUrl()})")
                                               .WithFooter(message.Id.ToString())
                                               .WithTimestamp(newMessage.Timestamp);
                    if (newMessage.Attachments.Any())
                        eb1.WithImageUrl(newMessage.Attachments.FirstOrDefault()!.Url);

                    await post2!.ModifyAsync(x =>
                    {
                        x.Content = $"{star} **{enumerable.Length}** {textChannel.Mention}";
                        x.Embed = eb1.Build();
                    });

                }
                else
                {
                    var tryGetOldPost = await starboardChannel.GetMessageAsync(maybePost.PostId);
                    if (tryGetOldPost is not null)
                        try
                        {
                            await tryGetOldPost.DeleteAsync();
                        }
                        catch 
                        {
                            // ignored
                        }
                    var eb2 = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author)
                                               .WithDescription(newMessage.Content)
                                               .AddField("**Source**", $"[Jump!]({newMessage.GetJumpUrl()})")
                                               .WithFooter(message.Id.ToString())
                                               .WithTimestamp(newMessage.Timestamp);
                    if (newMessage.Attachments.Any())
                        eb2.WithImageUrl(newMessage.Attachments.FirstOrDefault()!.Url);

                    var msg1 = await starboardChannel.SendMessageAsync($"{star} **{enumerable.Length}** {textChannel.Mention}", embed: eb2.Build());
                    await AddStarboardPost(newMessage.Id, msg1.Id);

                }
            }
            else
            {
                var tryGetOldPost = await starboardChannel.GetMessageAsync(maybePost.PostId);
                if (tryGetOldPost is not null)
                {
                    var toModify = tryGetOldPost as IUserMessage;
                    var eb1 = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author)
                                                .WithDescription(newMessage.Content)
                                                .AddField("**Source**", $"[Jump!]({newMessage.GetJumpUrl()})")
                                                .WithFooter(message.Id.ToString()).WithTimestamp(newMessage.Timestamp);
                    if (newMessage.Attachments.Any())
                        eb1.WithImageUrl(newMessage.Attachments.FirstOrDefault()!.Url);

                    await toModify!.ModifyAsync(x =>
                    {
                        x.Content = $"{star} **{enumerable.Length}** {textChannel.Mention}";
                        x.Embed = eb1.Build();
                    });
                }
                else
                {
                    var eb2 = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author)
                                                .WithDescription(newMessage.Content)
                                                .AddField("**Source**", $"[Jump!]({newMessage.GetJumpUrl()})")
                                                .WithFooter(message.Id.ToString()).WithTimestamp(newMessage.Timestamp);
                    if (newMessage.Attachments.Any())
                        eb2.WithImageUrl(newMessage.Attachments.FirstOrDefault()!.Url);

                    var msg1 = await starboardChannel.SendMessageAsync(
                        $"{star} **{enumerable.Length}** {textChannel.Mention}", embed: eb2.Build());
                    await AddStarboardPost(message.Id, msg1.Id);
                }
            }
        }
    }

    public StarboardPosts GetMessage(ulong id)
    {
        using var uow = _db.GetDbContext();
        return uow.Starboard.ForMsgId(id);
    }
}