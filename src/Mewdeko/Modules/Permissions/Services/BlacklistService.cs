using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Permissions.Services;

/// <summary>
/// Manages a blacklist to control access or actions within the bot's environment,
/// offering functionality to add, remove, and check entities against a blacklist.
/// </summary>
public sealed class BlacklistService : IEarlyBehavior, INService
{
    private readonly TypedKey<bool> blPrivKey = new("blacklist.reload.priv");

    private readonly TypedKey<BlacklistEntry[]> blPubKey = new("blacklist.reload");
    private readonly DiscordShardedClient client;
    private readonly BotConfig config;
    private readonly MewdekoContext dbContext;
    private readonly IPubSub pubSub;

    /// <summary>
    /// Gets or sets the collection of blacklist entries.
    /// </summary>
    public IList<BlacklistEntry> BlacklistEntries;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlacklistService"/> class, setting up listeners for guild joins and the bot's readiness,
    /// and preloading the blacklist.
    /// </summary>
    /// <param name="db">The database service to access persistence layer.</param>
    /// <param name="pubSub">The publish-subscribe service for inter-service communication.</param>
    /// <param name="handler">Event handler for listening to Discord client events.</param>
    /// <param name="client">The Discord socket client instance.</param>
    /// <param name="config">The bot configuration service.</param>
    /// <remarks>
    /// The service subscribes to relevant events to automatically enforce blacklist rules upon guild join events or when the bot starts.
    /// </remarks>
    public BlacklistService(MewdekoContext dbContext, IPubSub pubSub, EventHandler handler, DiscordShardedClient client,
        BotConfig config)
    {
        this.dbContext = dbContext;
        this.pubSub = pubSub;
        this.client = client;
        this.config = config;
        Reload(false);
        this.pubSub.Sub(blPubKey, OnReload);
        this.pubSub.Sub(blPrivKey, ManualCheck);
        handler.JoinedGuild += CheckBlacklist;
        _ = CheckAllGuilds();
        BlacklistEntries.Add(new BlacklistEntry
        {
            DateAdded = DateTime.Now, ItemId = 967780813741625344, Type = BlacklistType.User
        });
        BlacklistEntries.Add(new BlacklistEntry
        {
            DateAdded = DateTime.UtcNow, ItemId = 930096051900280882, Type = BlacklistType.User
        });
        BlacklistEntries.Add(new BlacklistEntry
        {
            DateAdded = DateTime.UtcNow, ItemId = 767459211373314118, Type = BlacklistType.User
        });
    }

    /// <summary>
    /// The priority order in which the early behavior should run, with lower numbers indicating higher priority.
    /// </summary>
    public int Priority => -100;

    /// <summary>
    /// The type of behavior this service represents, indicating when it should be run in the bot's lifecycle.
    /// </summary>
    public ModuleBehaviorType BehaviorType => ModuleBehaviorType.Blocker;

    /// <summary>
    /// Evaluates whether the incoming message should be blocked based on the blacklist status of the user, channel, or guild.
    /// </summary>
    /// <param name="socketClient">The Discord socket client instance.</param>
    /// <param name="guild">The guild from which the message originated, if applicable.</param>
    /// <param name="usrMsg">The user message to be evaluated against the blacklist.</param>
    /// <returns>A task that resolves to true if the message should be blocked; otherwise, false.</returns>
    /// <remarks>
    /// This method allows the service to act as a pre-message processing step, blocking messages from blacklisted entities.
    /// </remarks>
    public Task<bool> RunBehavior(DiscordShardedClient socketClient, IGuild guild, IUserMessage usrMsg)
    {
        foreach (var bl in BlacklistEntries)
        {
            if (guild != null && bl.Type == BlacklistType.Server && bl.ItemId == guild.Id)
                return Task.FromResult(true);

            switch (bl.Type)
            {
                case BlacklistType.Channel when bl.ItemId == usrMsg.Channel.Id:
                    return Task.FromResult(true);
                case BlacklistType.User when bl.ItemId == usrMsg.Author.Id:
                    return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Manually triggers a comprehensive check across all guilds to enforce the blacklist.
    /// </summary>
    /// <param name="_">A placeholder parameter for compatibility with pub-sub triggers. Not used in the method body.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method is typically invoked through a pub-sub event to ensure the blacklist is enforced consistently.
    /// </remarks>
    private ValueTask ManualCheck(bool _)
    {
        CheckAllGuilds();
        return default;
    }

    /// <summary>
    /// Checks all guilds the bot is a member of against the blacklist, taking appropriate action for any matches found.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation of checking all guilds against the blacklist.</returns>
    /// <remarks>
    /// This method iterates over all guilds, removing the bot from those that are blacklisted.
    /// </remarks>
    private Task CheckAllGuilds()
    {
        _ = Task.Run(async () =>
        {
            var guilds = client.Guilds;
            foreach (var guild in guilds)
            {
                if (BlacklistEntries.Select(x => x.ItemId).Contains(guild.Id))
                {
                    await guild.LeaveAsync().ConfigureAwait(false);
                }

                if (!guild.Name.ToLower().Contains("nigger")) continue;
                Blacklist(BlacklistType.Server, guild.Id, "Inappropriate Name");
                await guild.LeaveAsync().ConfigureAwait(false);
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Signals a manual check of the blacklist across all guilds.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation of signaling the manual blacklist check.</returns>
    /// <remarks>
    /// This method is designed to be invoked by administrators to force a manual re-evaluation of all guilds against the blacklist.
    /// </remarks>
    public Task SendManualCheck()
    {
        this.pubSub.Pub(blPrivKey, true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes the event of the bot joining a guild, checking the guild against the blacklist.
    /// </summary>
    /// <param name="arg">The guild that the bot has joined.</param>
    /// <returns>A task that represents the asynchronous operation of checking the newly joined guild against the blacklist.</returns>
    /// <remarks>
    /// If the guild is found on the blacklist, the bot will automatically leave the guild and, if possible, notify the guild of the blacklist status.
    /// </remarks>
    private async Task CheckBlacklist(IGuild arg)
    {
        var channels = await arg.GetTextChannelsAsync();
        var channel = channels.FirstOrDefault(x => x is not IVoiceChannel);
        if (arg.Name.ToLower().Contains("nigger"))
        {
            Blacklist(BlacklistType.Server, arg.Id, "Inappropriate Name");
            try
            {
                await channel
                    .SendErrorAsync(
                        "This server has been blacklisted. Please click the button below to potentially appeal your server ban.",
                        config)
                    .ConfigureAwait(false);
            }
            catch
            {
                Log.Error($"Unable to send blacklist message to {arg.Name}");
            }
            finally
            {
                await arg.LeaveAsync().ConfigureAwait(false);
            }

            await arg.LeaveAsync();
        }

        if (BlacklistEntries.Select(x => x.ItemId).Contains(arg.Id))
        {
            if (channel is null)
            {
                await arg.LeaveAsync().ConfigureAwait(false);
                return;
            }

            try
            {
                await channel
                    .SendErrorAsync(
                        "This server has been blacklisted. Please click the button below to potentially appeal your server ban.",
                        config)
                    .ConfigureAwait(false);
            }
            catch
            {
                Log.Error($"Unable to send blacklist message to {arg.Name}");
            }
            finally
            {
                await arg.LeaveAsync().ConfigureAwait(false);
            }

            await arg.LeaveAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles a publish-subscribe notification to reload the blacklist from an updated source.
    /// </summary>
    /// <param name="blacklist">The updated array of blacklist entries to load.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private ValueTask OnReload(BlacklistEntry[] blacklist)
    {
        BlacklistEntries = blacklist;
        return default;
    }

    /// <summary>
    /// Reloads the blacklist from the database, optionally publishing a notification about the update.
    /// </summary>
    /// <param name="publish">Whether to publish a notification about the blacklist reload.</param>
    public void Reload(bool publish = true)
    {

        var toPublish = dbContext.Blacklist.AsNoTracking().ToArray();
        BlacklistEntries = toPublish.ToList();
        if (publish) pubSub.Pub(blPubKey, toPublish);
    }

    /// <summary>
    /// Blacklists the specified entity based on the provided type, ID, and reason.
    /// </summary>
    /// <param name="type">The type of entity to blacklist.</param>
    /// <param name="id">The ID of the entity to blacklist.</param>
    /// <param name="reason">The reason for blacklisting the entity.</param>
    /// <remarks>
    /// This method adds the entity to the blacklist and reloads the blacklist to ensure the changes are reflected.
    /// </remarks>
    public void Blacklist(BlacklistType type, ulong id, string? reason)
    {

        var item = new BlacklistEntry
        {
            ItemId = id, Type = type, Reason = reason ?? "No reason provided."
        };
        dbContext.Blacklist.Add(item);
        dbContext.SaveChanges();

        Reload();
    }

    /// <summary>
    /// Removes an entity from the blacklist.
    /// </summary>
    /// <param name="type">The type of the entity to be removed from the blacklist.</param>
    /// <param name="id">The ID of the entity to be removed from the blacklist.</param>
    /// <remarks>
    /// This method removes the specified entity from the blacklist and then reloads the blacklist to ensure the changes are reflected.
    /// </remarks>
    public void UnBlacklist(BlacklistType type, ulong id)
    {

        var toRemove = dbContext.Blacklist
            .FirstOrDefault(bi => bi.ItemId == id && bi.Type == type);

        if (toRemove is not null)
            dbContext.Blacklist.Remove(toRemove);

        dbContext.SaveChanges();

        Reload();
    }

    /// <summary>
    /// Adds a list of users to the blacklist.
    /// </summary>
    /// <param name="toBlacklist">The list of user IDs to be added to the blacklist.</param>
    /// <remarks>
    /// This method adds the specified users to the blacklist, clears their currencies, and then reloads the blacklist to ensure the changes are reflected.
    /// </remarks>
    public async void BlacklistUsers(IEnumerable<ulong> toBlacklist)
    {

        await using (dbContext.ConfigureAwait(false))
        {
            var bc = dbContext.Blacklist;
            //blacklist the users
            bc.AddRange(toBlacklist.Select(x =>
                new BlacklistEntry
                {
                    ItemId = x, Type = BlacklistType.User
                }));

            //clear their currencies
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
        }

        Reload();
    }
}