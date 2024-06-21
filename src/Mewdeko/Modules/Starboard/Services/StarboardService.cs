using Mewdeko.Common.ModuleBehaviors;
using Serilog;

namespace Mewdeko.Modules.Starboard.Services;

/// <summary>
/// Service responsible for managing the starboard feature in a Discord server.
/// </summary>
public class StarboardService : INService, IReadyExecutor
{
    private readonly DiscordShardedClient client;
    private readonly DbService db;
    private readonly GuildSettingsService guildSettings;

    private List<StarboardPosts> starboardPosts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="StarboardService"/> class.
    /// </summary>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="db">The database service.</param>
    /// <param name="bot">The guild settings service.</param>
    /// <param name="eventHandler">The event handler.</param>
    public StarboardService(DiscordShardedClient client, DbService db,
        GuildSettingsService bot, EventHandler eventHandler)
    {
        this.client = client;
        this.db = db;
        guildSettings = bot;
        eventHandler.ReactionAdded += OnReactionAddedAsync;
        eventHandler.MessageDeleted += OnMessageDeletedAsync;
        eventHandler.ReactionRemoved += OnReactionRemoveAsync;
        eventHandler.ReactionsCleared += OnAllReactionsClearedAsync;
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        Log.Information($"Starting {this.GetType()} Cache");
        _ = Task.Run(async () =>
        {
            await using var uow = db.GetDbContext();
            var all = (await uow.Starboard.All()).ToList();
            starboardPosts = all.Count!=0 ? all : [];
            Log.Information("Starboard Posts Cached");
        });
        return Task.CompletedTask;
    }

    private async Task AddStarboardPost(ulong messageId, ulong postId)
    {
        await using var uow = db.GetDbContext();
        var post = starboardPosts.Find(x => x.MessageId == messageId);
        if (post is null)
        {
            var toAdd = new StarboardPosts
            {
                MessageId = messageId, PostId = postId
            };
            starboardPosts.Add(toAdd);
            uow.Starboard.Add(toAdd);
            await uow.SaveChangesAsync().ConfigureAwait(false);
            return;
        }

        if (post.PostId == postId)
            return;

        starboardPosts.Remove(post);
        post.PostId = postId;
        uow.Starboard.Update(post);
        starboardPosts.Add(post);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task RemoveStarboardPost(ulong messageId)
    {
        var toRemove = starboardPosts.Find(x => x.MessageId == messageId);
        await using var uow = db.GetDbContext();
        uow.Starboard.Remove(toRemove);
        starboardPosts.Remove(toRemove);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the starboard channel for a guild.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="channel">The ID of the starboard channel.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task SetStarboardChannel(IGuild guild, ulong channel)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.StarboardChannel = channel;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets whether bots are allowed to be starred on the starboard.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="enabled">A value indicating whether bots are allowed.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task SetStarboardAllowBots(IGuild guild, bool enabled)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.StarboardAllowBots = enabled;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets whether to remove starred messages on deletion of the original message.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="removeOnDelete">A value indicating whether to remove starred messages on deletion.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task SetRemoveOnDelete(IGuild guild, bool removeOnDelete)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.StarboardRemoveOnDelete = removeOnDelete;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets whether to remove starred messages on clearing all reactions from the original message.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="removeOnClear">A value indicating whether to remove starred messages on reactions clear.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task SetRemoveOnClear(IGuild guild, bool removeOnClear)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.StarboardRemoveOnReactionsClear = removeOnClear;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets whether to remove starred messages when they fall below the star count threshold.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="removeOnBelowThreshold">A value indicating whether to remove starred messages below threshold.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task SetRemoveOnBelowThreshold(IGuild guild, bool removeOnBelowThreshold)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.StarboardRemoveOnBelowThreshold = removeOnBelowThreshold;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets whether to use blacklist mode for checking starboard channels.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="checkmode">A value indicating whether to use blacklist mode.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task SetCheckMode(IGuild guild, bool checkmode)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.UseStarboardBlacklist = checkmode;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Toggles the starboard check for a specific channel in the guild.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="id">The ID of the channel to toggle.</param>
    /// <returns><c>true</c> if the channel is added to the check list; otherwise, <c>false</c>.</returns>
    public async Task<bool> ToggleChannel(IGuild guild, string id)
    {
        await using var uow = db.GetDbContext();
        var channels = (await GetCheckedChannels(guild.Id)).Split(" ").ToList();
        if (!channels.Contains(id))
        {
            channels.Add(id);
            var joinedchannels = string.Join(" ", channels);
            var gc = await uow.ForGuildId(guild.Id, set => set);
            gc.StarboardCheckChannels = joinedchannels;
            await guildSettings.UpdateGuildConfig(guild.Id, gc);
            return false;
        }

        channels.Remove(id);
        var joinedchannels1 = string.Join(" ", channels);

        var gc1 = await uow.ForGuildId(guild.Id, set => set);
        gc1.StarboardCheckChannels = joinedchannels1;
        await guildSettings.UpdateGuildConfig(guild.Id, gc1);
        return true;
    }

    /// <summary>
    /// Sets the star count required to trigger starboard for the guild.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="num">The number of stars required.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task SetStarCount(IGuild guild, int num)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.Stars = num;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Gets the star count required to trigger starboard for the guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The number of stars required.</returns>
    public async Task<int> GetStarCount(ulong id)
        => (await guildSettings.GetGuildConfig(id)).Stars;

    /// <summary>
    /// Gets the list of channels where starboard is enabled for the guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>A string containing the list of channel IDs.</returns>
    public async Task<string> GetCheckedChannels(ulong id)
        => (await guildSettings.GetGuildConfig(id)).StarboardCheckChannels;

    /// <summary>
    /// Gets whether bots are allowed to trigger starboard for the guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns><c>true</c> if bots are allowed to trigger starboard; otherwise, <c>false</c>.</returns>
    public async Task<bool> GetAllowBots(ulong id)
        => (await guildSettings.GetGuildConfig(id)).StarboardAllowBots;

    /// <summary>
    /// Gets the current check mode for starboard channels in the guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns><c>true</c> if blacklist mode is enabled; otherwise, <c>false</c>.</returns>
    public async Task<bool> GetCheckMode(ulong id)
        => (await guildSettings.GetGuildConfig(id)).UseStarboardBlacklist;

    private async Task<int> GetThreshold(ulong id)
        => (await guildSettings.GetGuildConfig(id)).RepostThreshold;

    private async Task<bool> GetRemoveOnBelowThreshold(ulong id)
        => (await guildSettings.GetGuildConfig(id)).StarboardRemoveOnBelowThreshold;

    private async Task<bool> GetRemoveOnDelete(ulong id)
        => (await guildSettings.GetGuildConfig(id)).StarboardRemoveOnDelete;

    private async Task<bool> GetRemoveOnReactionsClear(ulong id)
        => (await guildSettings.GetGuildConfig(id)).StarboardRemoveOnReactionsClear;

    /// <summary>
    /// Sets the star emote for the guild.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="emote">The emote to use as a star.</param>
    public async Task SetStar(IGuild guild, string emote)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.Star2 = emote;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Sets after how many messages a starboard post should be reposted when a star is added to it.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="threshold">The message threshold</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetRepostThreshold(IGuild guild, int threshold)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.RepostThreshold = threshold;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Gets the star emote for the guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns></returns>
    public async Task<string> GetStar(ulong id)
        => (await guildSettings.GetGuildConfig(id)).Star2;

    private async Task<ulong> GetStarboardChannel(ulong id)
        => (await guildSettings.GetGuildConfig(id)).StarboardChannel;

    private async Task OnReactionAddedAsync(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        if (!reaction.User.IsSpecified
            || reaction.User.Value.IsBot
            || !channel.HasValue
            || channel.Value is not ITextChannel textChannel
            || await GetStarCount(textChannel.GuildId) == 0)
        {
            return;
        }

        if (await GetStarboardChannel(textChannel.Guild.Id) is 0)
            return;

        IUserMessage newMessage;
        if (!message.HasValue)
            newMessage = await message.GetOrDownloadAsync().ConfigureAwait(false);
        else
            newMessage = message.Value;

        var star = (await GetStar(textChannel.GuildId)).ToIEmote();

        if (star.Name == null)
            return;

        if (!Equals(reaction.Emote, star))
            return;

        var starboardChannelSetting = await GetStarboardChannel(textChannel.GuildId);

        if (starboardChannelSetting == 0)
            return;

        var starboardChannel =
            await textChannel.Guild.GetTextChannelAsync(starboardChannelSetting).ConfigureAwait(false);

        if (starboardChannel == null)
            return;
        var gUser = await textChannel.Guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false);

        var checkedChannels = await GetCheckedChannels(starboardChannel.GuildId);
        if (await GetCheckMode(gUser.GuildId))
        {
            if (checkedChannels.Split(" ").Contains(newMessage.Channel.Id.ToString()))
                return;
        }
        else
        {
            if (!checkedChannels.Split(" ").Contains(newMessage.Channel.ToString()))
                return;
        }

        var botPerms = gUser.GetPermissions(starboardChannel);

        if (!botPerms.Has(ChannelPermission.SendMessages))
            return;
        string content;
        string imageurl;
        switch (newMessage.Author.IsBot)
        {
            case true when !await GetAllowBots(textChannel.GuildId):
                return;
            case true:
                content = newMessage.Embeds.Count > 0
                    ? newMessage.Embeds.Select(x => x.Description).FirstOrDefault()
                    : newMessage.Content;
                imageurl = newMessage.Attachments.Count > 0
                    ? newMessage.Attachments.FirstOrDefault().ProxyUrl
                    : newMessage.Embeds?.Select(x => x.Image).FirstOrDefault()?.ProxyUrl;
                break;
            default:
                content = newMessage.Content;
                imageurl = newMessage.Attachments?.FirstOrDefault()?.ProxyUrl;
                break;
        }

        if (content is null && imageurl is null)
            return;

        var emoteCount =
            await newMessage.GetReactionUsersAsync(star, int.MaxValue).FlattenAsync().ConfigureAwait(false);
        var count = emoteCount.Where(x => !x.IsBot);
        var enumerable = count as IUser[] ?? count.ToArray();
        if (enumerable.Length < await GetStarCount(textChannel.GuildId))
            return;
        var component = new ComponentBuilder()
            .WithButton(url: newMessage.GetJumpUrl(), style: ButtonStyle.Link, label: "Jump To Message").Build();
        var maybePost = starboardPosts.Find(x => x.MessageId == newMessage.Id);
        if (maybePost != null)
        {
            if (await GetThreshold(textChannel.GuildId) > 0)
            {
                var messages = await starboardChannel.GetMessagesAsync(await GetThreshold(textChannel.GuildId))
                    .FlattenAsync().ConfigureAwait(false);
                var post = messages.FirstOrDefault(x => x.Id == maybePost.PostId);
                if (post is not null)
                {
                    var post2 = post as IUserMessage;
                    var eb1 = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author).WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);
                    if (imageurl is not null)
                        eb1.WithImageUrl(imageurl);

                    await post2!.ModifyAsync(x =>
                    {
                        x.Content = $"{star} **{enumerable.Length}** {textChannel.Mention}";
                        x.Components = component;
                        x.Embed = eb1.Build();
                    }).ConfigureAwait(false);
                }
                else
                {
                    var tryGetOldPost = await starboardChannel.GetMessageAsync(maybePost.PostId).ConfigureAwait(false);
                    if (tryGetOldPost is not null)
                    {
                        try
                        {
                            await tryGetOldPost.DeleteAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    var eb2 = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author).WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);
                    if (imageurl is not null)
                        eb2.WithImageUrl(imageurl);

                    var msg1 = await starboardChannel
                        .SendMessageAsync($"{star} **{enumerable.Length}** {textChannel.Mention}", embed: eb2.Build(),
                            components: component)
                        .ConfigureAwait(false);
                    await AddStarboardPost(message.Id, msg1.Id).ConfigureAwait(false);
                }
            }
            else
            {
                var tryGetOldPost = await starboardChannel.GetMessageAsync(maybePost.PostId).ConfigureAwait(false);
                if (tryGetOldPost is not null)
                {
                    var toModify = tryGetOldPost as IUserMessage;
                    var eb1 = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author).WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);
                    if (imageurl is not null)
                        eb1.WithImageUrl(imageurl);

                    await toModify!.ModifyAsync(x =>
                    {
                        x.Content = $"{star} **{enumerable.Length}** {textChannel.Mention}";
                        x.Components = component;
                        x.Embed = eb1.Build();
                    }).ConfigureAwait(false);
                }
                else
                {
                    var eb2 = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author).WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);
                    if (newMessage.Attachments.Count > 0)
                        eb2.WithImageUrl(newMessage.Attachments.FirstOrDefault()!.Url);

                    var msg1 = await starboardChannel
                        .SendMessageAsync($"{star} **{enumerable.Length}** {textChannel.Mention}", embed: eb2.Build(),
                            components: component)
                        .ConfigureAwait(false);
                    await AddStarboardPost(message.Id, msg1.Id).ConfigureAwait(false);
                }
            }
        }
        else
        {
            var eb = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author).WithDescription(content)
                .WithFooter(message.Id.ToString()).WithTimestamp(newMessage.Timestamp);
            if (imageurl is not null)
                eb.WithImageUrl(imageurl);

            var msg = await starboardChannel.SendMessageAsync($"{star} **{enumerable.Length}** {textChannel.Mention}",
                    embed: eb.Build(), components: component)
                .ConfigureAwait(false);
            await AddStarboardPost(message.Id, msg.Id).ConfigureAwait(false);
        }
    }

    private async Task OnReactionRemoveAsync(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        if (!reaction.User.IsSpecified
            || reaction.User.Value.IsBot
            || !channel.HasValue
            || channel.Value is not ITextChannel textChannel
            || await GetStarCount(textChannel.GuildId) == 0)
        {
            return;
        }

        if (await GetStarboardChannel(textChannel.Guild.Id) is 0)
            return;
        IUserMessage newMessage;
        if (!message.HasValue)
            newMessage = await message.GetOrDownloadAsync().ConfigureAwait(false);
        else
            newMessage = message.Value;
        var star = (await GetStar(textChannel.GuildId)).ToIEmote();
        if (star.Name == null)
            return;

        if (!Equals(reaction.Emote, star))
            return;

        var starboardChannelSetting = await GetStarboardChannel(textChannel.GuildId);

        if (starboardChannelSetting == 0)
            return;

        var starboardChannel =
            await textChannel.Guild.GetTextChannelAsync(starboardChannelSetting).ConfigureAwait(false);

        if (starboardChannel == null)
            return;
        var checkedChannels = await GetCheckedChannels(starboardChannel.GuildId);
        var gUser = await textChannel.Guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false);
        if (await GetCheckMode(gUser.GuildId))
        {
            if (checkedChannels.Split(" ").Contains(newMessage.Channel.Id.ToString()))
                return;
        }
        else
        {
            if (!checkedChannels.Split(" ").Contains(newMessage.Channel.ToString()))
                return;
        }

        var botPerms = gUser.GetPermissions(starboardChannel);

        if (!botPerms.Has(ChannelPermission.SendMessages))
            return;

        string content;
        string imageurl;
        var component = new ComponentBuilder()
            .WithButton(url: newMessage.GetJumpUrl(), style: ButtonStyle.Link, label: "Jump To Message").Build();
        switch (newMessage.Author.IsBot)
        {
            case true when !await GetAllowBots(textChannel.GuildId):
                return;
            case true:
                content = newMessage.Embeds.Count > 0
                    ? newMessage.Embeds.Select(x => x.Description).FirstOrDefault()
                    : newMessage.Content;
                imageurl = newMessage.Attachments.Count > 0
                    ? newMessage.Attachments.FirstOrDefault().ProxyUrl
                    : newMessage.Embeds?.Select(x => x.Image).FirstOrDefault()?.ProxyUrl;
                break;
            default:
                content = newMessage.Content;
                imageurl = newMessage.Attachments?.FirstOrDefault()?.ProxyUrl;
                break;
        }

        if (content is null && imageurl is null)
            return;

        var emoteCount =
            await newMessage.GetReactionUsersAsync(star, int.MaxValue).FlattenAsync().ConfigureAwait(false);
        var maybePost = starboardPosts.Find(x => x.MessageId == newMessage.Id);
        if (maybePost == null)
            return;
        var count = emoteCount.Where(x => !x.IsBot);
        var enumerable = count as IUser[] ?? count.ToArray();
        if (enumerable.Length < await GetStarCount(textChannel.GuildId) &&
            await GetRemoveOnBelowThreshold(gUser.GuildId))
        {
            await RemoveStarboardPost(newMessage.Id).ConfigureAwait(false);
            try
            {
                var post = await starboardChannel.GetMessageAsync(maybePost.PostId).ConfigureAwait(false);
                await post.DeleteAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }
        else
        {
            if (await GetThreshold(textChannel.GuildId) > 0)
            {
                var messages = await starboardChannel.GetMessagesAsync(await GetThreshold(textChannel.GuildId))
                    .FlattenAsync().ConfigureAwait(false);
                var post = messages.FirstOrDefault(x => x.Id == maybePost.PostId);
                if (post is not null)
                {
                    var post2 = post as IUserMessage;
                    var eb1 = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author).WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);
                    if (imageurl is not null)
                        eb1.WithImageUrl(imageurl);

                    await post2!.ModifyAsync(x =>
                    {
                        x.Content = $"{star} **{enumerable.Length}** {textChannel.Mention}";
                        x.Components = component;
                        x.Embed = eb1.Build();
                    }).ConfigureAwait(false);
                }
                else
                {
                    var tryGetOldPost = await starboardChannel.GetMessageAsync(maybePost.PostId).ConfigureAwait(false);
                    if (tryGetOldPost is not null)
                    {
                        try
                        {
                            await tryGetOldPost.DeleteAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    var eb2 = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author).WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);
                    if (imageurl is not null)
                        eb2.WithImageUrl(imageurl);

                    var msg1 = await starboardChannel
                        .SendMessageAsync($"{star} **{enumerable.Length}** {textChannel.Mention}", embed: eb2.Build(),
                            components: component)
                        .ConfigureAwait(false);
                    await AddStarboardPost(newMessage.Id, msg1.Id).ConfigureAwait(false);
                }
            }
            else
            {
                var tryGetOldPost = await starboardChannel.GetMessageAsync(maybePost.PostId).ConfigureAwait(false);
                if (tryGetOldPost is not null)
                {
                    var toModify = tryGetOldPost as IUserMessage;
                    var eb1 = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author).WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);
                    if (imageurl is not null)
                        eb1.WithImageUrl(imageurl);

                    await toModify!.ModifyAsync(x =>
                    {
                        x.Content = $"{star} **{enumerable.Length}** {textChannel.Mention}";
                        x.Components = component;
                        x.Embed = eb1.Build();
                    }).ConfigureAwait(false);
                }
                else
                {
                    var eb2 = new EmbedBuilder().WithOkColor().WithAuthor(newMessage.Author).WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);
                    if (imageurl is not null)
                        eb2.WithImageUrl(imageurl);

                    var msg1 = await starboardChannel
                        .SendMessageAsync($"{star} **{enumerable.Length}** {textChannel.Mention}", embed: eb2.Build(),
                            components: component)
                        .ConfigureAwait(false);
                    await AddStarboardPost(message.Id, msg1.Id).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task OnMessageDeletedAsync(Cacheable<IMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        if (!arg1.HasValue || !arg2.HasValue)
            return;

        var msg = arg1.Value;
        var chan = arg2.Value;
        if (chan is not ITextChannel channel)
            return;
        var permissions =
            (await channel.Guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false)).GetPermissions(channel);
        if (!permissions.ManageMessages)
            return;
        var maybePost = starboardPosts.FirstOrDefault(x => x.MessageId == msg.Id);
        if (maybePost is null)
            return;

        if (!await GetRemoveOnDelete(channel.GuildId))
            return;

        var starboardChannel = await channel.Guild.GetTextChannelAsync(await GetStarboardChannel(channel.GuildId))
            .ConfigureAwait(false);
        if (starboardChannel is null)
            return;

        var post = await starboardChannel.GetMessageAsync(maybePost.PostId).ConfigureAwait(false);
        if (post is null)
            return;

        await starboardChannel.DeleteMessageAsync(post).ConfigureAwait(false);
        await RemoveStarboardPost(msg.Id).ConfigureAwait(false);
    }

    private async Task OnAllReactionsClearedAsync(Cacheable<IUserMessage, ulong> arg1,
        Cacheable<IMessageChannel, ulong> arg2)
    {
        IUserMessage msg;
        if (!arg1.HasValue)
            msg = await arg1.GetOrDownloadAsync().ConfigureAwait(false);
        else
            msg = arg1.Value;

        if (msg is null)
            return;

        var maybePost = starboardPosts.Find(x => x.MessageId == msg.Id);

        if (maybePost is null || !arg2.HasValue || arg2.Value is not ITextChannel channel ||
            !await GetRemoveOnReactionsClear(channel.GuildId))
            return;

        var permissions =
            (await channel.Guild.GetUserAsync(client.CurrentUser.Id).ConfigureAwait(false)).GetPermissions(channel);
        if (!permissions.ManageMessages)
            return;

        var starboardChannel = await channel.Guild.GetTextChannelAsync(await GetStarboardChannel(channel.GuildId))
            .ConfigureAwait(false);
        if (starboardChannel is null)
            return;

        await starboardChannel.DeleteMessageAsync(maybePost.PostId).ConfigureAwait(false);
        await RemoveStarboardPost(msg.Id).ConfigureAwait(false);
    }
}