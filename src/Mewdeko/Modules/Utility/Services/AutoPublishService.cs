using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.DbContextStuff;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Provides functionality to automatically publish messages in Discord news channels
///     according to specific rules, including channel, user, and word blacklists.
/// </summary>
public class AutoPublishService : INService
{
    private readonly DiscordShardedClient client;
    private readonly DbContextProvider dbProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AutoPublishService" /> class.
    /// </summary>
    /// <param name="dbProvider">The database service for accessing and storing configuration.</param>
    /// <param name="handler">The event handler for subscribing to message received events.</param>
    /// <param name="client">The Discord client for interacting with the Discord API.</param>
    public AutoPublishService(DbContextProvider dbProvider, EventHandler handler, DiscordShardedClient client)
    {
        this.dbProvider = dbProvider;
        this.client = client;
        handler.MessageReceived += AutoPublish;
    }

    /// <summary>
    ///     Automatically publishes messages in news channels if they meet certain criteria,
    ///     checking against user and word blacklists.
    /// </summary>
    /// <param name="args">The message event arguments.</param>
    private async Task AutoPublish(SocketMessage args)
    {
        if (args.Channel is not INewsChannel channel || args is not IUserMessage msg)
            return;

        var currentUser = await channel.GetUserAsync(client.CurrentUser.Id);
        var permissions = currentUser.GetPermissions(channel);

        if (!permissions.Has(ChannelPermission.ManageMessages) &&
            currentUser.GuildPermissions.Has(GuildPermission.ManageMessages))
            return;

        await using var dbContext = await dbProvider.GetContextAsync();

        var autoPublishConfig = await dbContext.AutoPublish.FirstOrDefaultAsyncEF(x => x.ChannelId == channel.Id);
        if (autoPublishConfig is null)
            return;

        var blacklistedWords = await dbContext.PublishWordBlacklists
            .Where(x => x.ChannelId == channel.Id)
            .Select(x => x.Word.ToLower())
            .ToListAsync();

        if (blacklistedWords.Any(word => args.Content.Contains(word, StringComparison.CurrentCultureIgnoreCase)))
            return;

        var userBlacklists = await dbContext.PublishUserBlacklists
            .Where(x => x.ChannelId == channel.Id && x.User == args.Author.Id)
            .AnyAsync();

        if (userBlacklists)
            return;

        try
        {
            await msg.CrosspostAsync();
        }
        catch (Exception e)
        {
            Log.Error(e, "Unable to publish message:");
        }
    }

    /// <summary>
    ///     Adds a channel to the list of auto-publish channels.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the channel resides.</param>
    /// <param name="channelId">The ID of the channel to auto-publish.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> AddAutoPublish(ulong guildId, ulong channelId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var existingConfig = await dbContext.AutoPublish.FirstOrDefaultAsyncEF(x => x.ChannelId == channelId);
        if (existingConfig != null)
        {
            // AutoPublish already exists for the channel
            return false;
        }

        var autoPublish = new AutoPublish
        {
            GuildId = guildId, ChannelId = channelId
        };
        dbContext.AutoPublish.Add(autoPublish);
        await dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Retrieves the auto-publish configurations for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve configurations for.</param>
    /// <returns>A list of configurations for auto-publishing.</returns>
    public async Task<List<(AutoPublish? AutoPublish, List<PublishUserBlacklist?> UserBlacklists,
            List<PublishWordBlacklist?> WordBlacklists)>>
        GetAutoPublishes(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var autoPublishes = await dbContext.AutoPublish
            .Where(x => x.GuildId == guildId)
            .ToListAsyncEF();

        if (!autoPublishes.Any())
            return new List<(AutoPublish?, List<PublishUserBlacklist?>, List<PublishWordBlacklist?>)>
            {
                (null, [], [])
            };

        var result = new List<(AutoPublish?, List<PublishUserBlacklist?>, List<PublishWordBlacklist?>)>();

        foreach (var publish in autoPublishes)
        {
            var userBlacklists = await dbContext.PublishUserBlacklists
                .Where(x => x.ChannelId == publish.ChannelId)
                .ToListAsyncEF();

            var wordBlacklists = await dbContext.PublishWordBlacklists
                .Where(x => x.ChannelId == publish.ChannelId)
                .ToListAsyncEF();

            result.Add((publish, userBlacklists, wordBlacklists));
        }

        return result;
    }

    /// <summary>
    ///     Checks if the bot has the necessary permissions to auto-publish in a channel.
    /// </summary>
    /// <param name="channel">The news channel to check permissions for.</param>
    /// <returns>True if the bot has the necessary permissions, false otherwise.</returns>
    public async Task<bool> PermCheck(INewsChannel channel)
    {
        var currentUser = await channel.GetUserAsync(client.CurrentUser.Id);
        var permissions = currentUser.GetPermissions(channel);

        return permissions.Has(ChannelPermission.ManageMessages) ||
               currentUser.GuildPermissions.Has(GuildPermission.ManageMessages);
    }

    /// <summary>
    ///     Removes a channel from the list of auto-publish channels.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the channel resides.</param>
    /// <param name="channelId">The ID of the channel to stop auto-publishing.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> RemoveAutoPublish(ulong guildId, ulong channelId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var autoPublish = await dbContext.AutoPublish.FirstOrDefaultAsyncEF(x => x.ChannelId == channelId);
        if (autoPublish == null)
            return false;

        dbContext.AutoPublish.Remove(autoPublish);
        await dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Adds a user to the blacklist, preventing their messages from being auto-published.
    /// </summary>
    /// <param name="channelId">The ID of the channel where the blacklist applies.</param>
    /// <param name="userId">The ID of the user to blacklist.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> AddUserToBlacklist(ulong channelId, ulong userId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var existingBlacklist = await dbContext.PublishUserBlacklists
            .FirstOrDefaultAsyncEF(x => x.ChannelId == channelId && x.User == userId);
        if (existingBlacklist != null)
        {
            // User is already blacklisted in the channel
            return false;
        }

        var userBlacklist = new PublishUserBlacklist
        {
            ChannelId = channelId, User = userId
        };
        dbContext.PublishUserBlacklists.Add(userBlacklist);
        await dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Removes a user from the blacklist, allowing their messages to be auto-published again.
    /// </summary>
    /// <param name="channelId">The ID of the channel where the blacklist applies.</param>
    /// <param name="userId">The ID of the user to remove from the blacklist.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> RemoveUserFromBlacklist(ulong channelId, ulong userId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var userBlacklist = await dbContext.PublishUserBlacklists
            .FirstOrDefaultAsyncEF(x => x.ChannelId == channelId && x.User == userId);
        if (userBlacklist == null)
        {
            // User is not blacklisted in the channel
            return false;
        }

        dbContext.PublishUserBlacklists.Remove(userBlacklist);
        await dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Adds a word to the blacklist, preventing messages containing it from being auto-published.
    /// </summary>
    /// <param name="channelId">The ID of the channel where the blacklist applies.</param>
    /// <param name="word">The word to blacklist.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> AddWordToBlacklist(ulong channelId, string word)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var existingWordBlacklist = await dbContext.PublishWordBlacklists
            .FirstOrDefaultAsyncEF(x =>
                x.ChannelId == channelId && x.Word.Equals(word, StringComparison.CurrentCultureIgnoreCase));
        if (existingWordBlacklist != null)
        {
            // Word is already blacklisted in the channel
            return false;
        }

        var wordBlacklist = new PublishWordBlacklist
        {
            ChannelId = channelId, Word = word.ToLower()
        };
        dbContext.PublishWordBlacklists.Add(wordBlacklist);
        await dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Removes a word from the blacklist, allowing messages containing it to be auto-published again.
    /// </summary>
    /// <param name="channelId">The ID of the channel where the blacklist applies.</param>
    /// <param name="word">The word to remove from the blacklist.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> RemoveWordFromBlacklist(ulong channelId, string word)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var wordBlacklist = await dbContext.PublishWordBlacklists
            .FirstOrDefaultAsyncEF(x =>
                x.ChannelId == channelId && x.Word.Equals(word, StringComparison.CurrentCultureIgnoreCase));
        if (wordBlacklist == null)
        {
            // Word is not blacklisted in the channel
            return false;
        }

        dbContext.PublishWordBlacklists.Remove(wordBlacklist);
        await dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Checks if an auto-publish configuration exists for a given channel.
    /// </summary>
    /// <param name="channelId">The ID of the channel to check.</param>
    /// <returns>True if an auto-publish configuration exists, false otherwise.</returns>
    public async Task<bool> CheckIfExists(ulong channelId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        return await dbContext.AutoPublish.AnyAsyncEF(x => x.ChannelId == channelId);
    }
}