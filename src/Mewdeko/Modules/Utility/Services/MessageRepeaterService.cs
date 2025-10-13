using System.Text.Json;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Service responsible for managing and executing repeating messages across Discord guilds.
///     Handles initialization, scheduling, and cleanup of message repeaters.
/// </summary>
public class MessageRepeaterService : INService, IReadyExecutor, IDisposable
{
    private readonly Mewdeko bot;
    private readonly DiscordShardedClient client;
    private readonly StickyConditionService? conditionService;
    private readonly IDataConnectionFactory dbFactory;
    private readonly GuildTimezoneService? guildTimezoneService;
    private readonly EventHandler handler;
    private readonly ILogger<MessageRepeaterService> logger;
    private readonly MessageCountService? messageCountService;

    /// <summary>
    ///     Initializes a new instance of the MessageRepeaterService class.
    ///     Sets up event handlers for guild-related events and initializes the service dependencies.
    /// </summary>
    /// <param name="client">The Discord client instance used for sending messages and handling events.</param>
    /// <param name="dbFactory">Provider for database context access.</param>
    /// <param name="bot">The main bot instance.</param>
    /// <param name="handler">Service for handling Discord events asynchronously</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="conditionService">Service for evaluating sticky conditions.</param>
    /// <param name="messageCountService">Service for activity detection.</param>
    /// <param name="guildTimezoneService">Service for timezone handling.</param>
    public MessageRepeaterService(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        Mewdeko bot, EventHandler handler, ILogger<MessageRepeaterService> logger,
        StickyConditionService? conditionService = null, MessageCountService? messageCountService = null,
        GuildTimezoneService? guildTimezoneService = null)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.bot = bot;
        this.handler = handler;
        this.logger = logger;
        this.conditionService = conditionService;
        this.messageCountService = messageCountService;
        this.guildTimezoneService = guildTimezoneService;


        handler.Subscribe("GuildAvailable", "MessageRepeaterService", OnGuildAvailable);
        handler.Subscribe("GuildUnavailable", "MessageRepeaterService", OnGuildUnavailable);
        handler.Subscribe("JoinedGuild", "MessageRepeaterService", OnJoinedGuild);
        handler.Subscribe("LeftGuild", "MessageRepeaterService", OnLeftGuild);
        handler.Subscribe("ThreadCreated", "MessageRepeaterService", OnThreadCreated);
        handler.Subscribe("MessageReceived", "MessageRepeaterService", OnMessageReceived);
    }

    /// <summary>
    ///     Gets the collection of active repeaters organized by guild ID and repeater ID.
    ///     The outer dictionary maps guild IDs to their repeaters, while the inner dictionary maps
    ///     repeater IDs to their respective runner instances.
    /// </summary>
    public ConcurrentDictionary<ulong, ConcurrentDictionary<int, RepeatRunner>> Repeaters { get; }
        = new();

    /// <summary>
    ///     Gets a value indicating whether the repeater service has completed its initialization
    ///     and is ready to handle repeater operations.
    /// </summary>
    public bool RepeaterReady { get; private set; }

    /// <summary>
    ///     Performs cleanup of resources used by the service.
    ///     Stops all active repeaters and unsubscribes from Discord client events.
    /// </summary>
    public void Dispose()
    {
        foreach (var guildRepeaters in Repeaters.Values)
        {
            foreach (var runner in guildRepeaters.Values)
            {
                runner.Stop();
            }
        }

        handler.Unsubscribe("GuildAvailable", "MessageRepeaterService", OnGuildAvailable);
        handler.Unsubscribe("GuildUnavailable", "MessageRepeaterService", OnGuildUnavailable);
        handler.Unsubscribe("JoinedGuild", "MessageRepeaterService", OnJoinedGuild);
        handler.Unsubscribe("LeftGuild", "MessageRepeaterService", OnLeftGuild);
        handler.Unsubscribe("ThreadCreated", "MessageRepeaterService", OnThreadCreated);
        handler.Unsubscribe("MessageReceived", "MessageRepeaterService", OnMessageReceived);
    }


    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        try
        {
            logger.LogInformation($"Starting {GetType()} Cache");
            await bot.Ready.Task.ConfigureAwait(false);
            logger.LogInformation("Loading message repeaters");

            foreach (var guild in client.Guilds)
            {
                await InitializeGuildRepeaters(guild);
            }

            RepeaterReady = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during repeater initialization");
            RepeaterReady = false;
        }
    }

    /// <summary>
    ///     Removes a repeater from both the active runners and the database.
    ///     Ensures proper cleanup of resources associated with the repeater.
    /// </summary>
    /// <param name="r">The repeater configuration to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RemoveRepeater(GuildRepeater r)
    {
        if (Repeaters.TryGetValue(r.GuildId, out var guildRepeaters))
        {
            if (guildRepeaters.TryRemove(r.Id, out var runner))
            {
                runner.Stop();
            }
        }

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Direct query by Id and GuildId
        var toDelete = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(x => x.Id == r.Id && x.GuildId == r.GuildId);

        if (toDelete != null)
        {
            await dbContext.DeleteAsync(toDelete);
        }
    }

    /// <summary>
    ///     Updates the last message ID for a specific repeater in the database.
    ///     This is used to track the most recent message sent by each repeater.
    /// </summary>
    /// <param name="repeaterId">The ID of the repeater to update.</param>
    /// <param name="lastMsgId">The ID of the last message sent by the repeater.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetRepeaterLastMessage(int repeaterId, ulong lastMsgId)
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var rep = await dbContext.GuildRepeaters.FirstOrDefaultAsync(x => x.Id == repeaterId).ConfigureAwait(false);
            rep.LastMessageId = lastMsgId;
            await dbContext.UpdateAsync(rep);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update last message for repeater {RepeaterId}", repeaterId);
        }
    }

    private async Task InitializeGuildRepeaters(IGuild guild)
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();

            // Direct query with GuildId filter
            var guildRepeaters = await dbContext.GuildRepeaters
                .Where(gr => gr.GuildId == guild.Id && gr.DateAdded != null)
                .ToListAsync();

            var repeaterDictionary = new ConcurrentDictionary<int, RepeatRunner>();

            foreach (var repeater in guildRepeaters)
            {
                try
                {
                    var runner = new RepeatRunner(client, guild, repeater, this, conditionService, messageCountService,
                        guildTimezoneService);
                    repeaterDictionary.TryAdd(repeater.Id, runner);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialize repeater {RepeaterId} for guild {GuildId}",
                        repeater.Id, guild.Id);
                }
            }

            Repeaters.AddOrUpdate(guild.Id, repeaterDictionary,
                (_, existing) =>
                {
                    foreach (var runner in existing.Values)
                    {
                        runner.Stop();
                    }

                    return repeaterDictionary;
                });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load repeaters for Guild {GuildId}", guild.Id);
        }
    }


    private Task OnGuildAvailable(SocketGuild guild)
    {
        _ = Task.Run(async () =>
        {
            await InitializeGuildRepeaters(guild);
        });
        return Task.CompletedTask;
    }

    private Task OnGuildUnavailable(SocketGuild guild)
    {
        if (!Repeaters.TryRemove(guild.Id, out var repeaters)) return Task.CompletedTask;
        foreach (var runner in repeaters.Values)
        {
            runner.Stop();
        }

        return Task.CompletedTask;
    }

    private Task OnJoinedGuild(SocketGuild args)
    {
        return OnGuildAvailable(args);
    }

    private Task OnLeftGuild(SocketGuild guild)
    {
        return OnGuildUnavailable(guild);
    }

    private async Task OnThreadCreated(SocketThreadChannel thread)
    {
        try
        {
            if (!Repeaters.TryGetValue(thread.Guild.Id, out var guildRepeaters))
                return;

            if (thread.CachedMessages.Count > 0)
                return;

            // Find repeaters that should auto-create in threads for this channel
            var applicableRepeaters = guildRepeaters.Values
                .Where(r => r.Repeater.ThreadAutoSticky && r.Repeater.IsEnabled)
                .ToList();

            if (!applicableRepeaters.Any())
                return;

            // For forum channels, check tag conditions
            if (thread.ParentChannel is IForumChannel forumChannel)
            {
                var threadTags = thread.AppliedTags?.ToArray() ?? [];

                // Filter by forum tag conditions and channel match
                applicableRepeaters = applicableRepeaters
                    .Where(r => r.Repeater.ChannelId == forumChannel.Id && // Match the forum channel
                                (conditionService?.ShouldDisplayForForumTags(r.Repeater, threadTags) ?? true))
                    .ToList();
            }
            else
            {
                // For regular threads, match parent channel
                applicableRepeaters = applicableRepeaters
                    .Where(r => r.Repeater.ChannelId == thread.ParentChannel.Id)
                    .ToList();
            }

            // Create sticky messages in the new thread for applicable repeaters
            foreach (var repeater in applicableRepeaters)
            {
                try
                {
                    await CreateThreadStickyMessage(thread, repeater.Repeater);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to create thread sticky for repeater {RepeaterId} in thread {ThreadId}",
                        repeater.Repeater.Id, thread.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling thread creation for sticky messages");
        }
    }

    private async Task CreateThreadStickyMessage(SocketThreadChannel thread, GuildRepeater repeater)
    {
        try
        {
            var currentUser = thread.Guild.CurrentUser;
            var rep = new ReplacementBuilder()
                .WithDefault(currentUser, thread, thread.Guild, client)
                .Build();

            var message = rep.Replace(repeater.Message ?? "");
            IMessage? sentMessage = null;

            if (SmartEmbed.TryParse(message, thread.Guild.Id, out var embed, out var plainText, out var components))
            {
                sentMessage =
                    await thread.SendMessageAsync(plainText ?? "", embeds: embed, components: components?.Build());
            }
            else
            {
                sentMessage = await thread.SendMessageAsync(message);
            }

            // Track this thread sticky message for repositioning
            if (sentMessage != null)
            {
                await TrackThreadStickyMessage(repeater.Id, thread.Id, sentMessage.Id);

                // Also update the in-memory repeater to avoid race conditions
                if (Repeaters.TryGetValue(thread.Guild.Id, out var guildRepeaters) &&
                    guildRepeaters.TryGetValue(repeater.Id, out var runner))
                {
                    var tracker = GetThreadStickyTracker(repeater) ?? new ThreadStickyTracker();
                    tracker.SetThreadStickyMessage(thread.Id, sentMessage.Id);
                    runner.Repeater.ThreadStickyMessages = JsonSerializer.Serialize(tracker);
                }
            }

            logger.LogInformation("Created auto-sticky message in thread {ThreadId} for repeater {RepeaterId}",
                thread.Id, repeater.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send auto-sticky message in thread {ThreadId}", thread.Id);
        }
    }

    /// <summary>
    ///     Creates a new repeater with the specified configuration.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the repeater will run.</param>
    /// <param name="channelId">The ID of the channel where messages will be sent.</param>
    /// <param name="interval">The interval between messages.</param>
    /// <param name="message">The message to repeat.</param>
    /// <param name="startTimeOfDay">Optional specific time of day to start the repeater.</param>
    /// <param name="allowMentions">Whether to allow mentions in the message.</param>
    /// <param name="triggerMode">How the sticky would trigger.</param>
    /// <param name="threadAutoSticky">Whether threads automatically get sticky messages in the channel.</param>
    /// <param name="threadOnlyMode">Whether the repeater only runs in threads.</param>
    /// <returns>The created repeater runner.</returns>
    public async Task<RepeatRunner?> CreateRepeaterAsync(
        ulong guildId,
        ulong channelId,
        TimeSpan interval,
        string message,
        string? startTimeOfDay = null,
        bool allowMentions = false,
        StickyTriggerMode triggerMode = StickyTriggerMode.TimeInterval,
        bool threadAutoSticky = false,
        bool threadOnlyMode = false)
    {
        try
        {
            var toAdd = new GuildRepeater
            {
                ChannelId = channelId,
                GuildId = guildId,
                Interval = interval.ToString(),
                Message = allowMentions ? message : message.SanitizeMentions(true),
                NoRedundant = false,
                StartTimeOfDay = startTimeOfDay,
                TriggerMode = (int)triggerMode,
                ThreadAutoSticky = threadAutoSticky,
                ThreadOnlyMode = threadOnlyMode,
                DateAdded = DateTime.UtcNow
            };

            await using var dbContext = await dbFactory.CreateConnectionAsync();

            // Add directly to Repeaters and get the generated ID
            var insertedId = await dbContext.InsertWithInt32IdentityAsync(toAdd);
            toAdd.Id = insertedId;

            logger.LogDebug("Created repeater with ID {RepeaterId} for guild {GuildId}", insertedId, guildId);


            var guild = client.GetGuild(guildId);
            if (guild == null) return null;

            var runner = new RepeatRunner(client, guild, toAdd, this, conditionService, messageCountService,
                guildTimezoneService);

            Repeaters.AddOrUpdate(guildId,
                new ConcurrentDictionary<int, RepeatRunner>([
                    new KeyValuePair<int, RepeatRunner>(toAdd.Id, runner)
                ]), (_, old) =>
                {
                    old.TryAdd(runner.Repeater.Id, runner);
                    return old;
                });

            return runner;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    ///     Updates the message of an existing repeater.
    /// </summary>
    public async Task<bool> UpdateRepeaterMessageAsync(ulong guildId, int repeaterId, string newMessage,
        bool allowMentions)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Direct query by Id and GuildId
        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return false;

        item.Message = allowMentions ? newMessage : newMessage.SanitizeMentions(true);
        await dbContext.UpdateAsync(item);


        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.Message = item.Message;
        }

        return true;
    }


    /// <summary>
    ///     Updates the channel of an existing repeater.
    /// </summary>
    public async Task<bool> UpdateRepeaterChannelAsync(ulong guildId, int repeaterId, ulong newChannelId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Direct query by Id and GuildId
        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return false;

        item.ChannelId = newChannelId;
        await dbContext.UpdateAsync(item);


        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.ChannelId = newChannelId;
        }

        return true;
    }

    /// <summary>
    ///     Toggles the redundancy setting of a repeater.
    /// </summary>
    public async Task<bool> ToggleRepeaterRedundancyAsync(ulong guildId, int repeaterId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Direct query by Id and GuildId
        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return false;

        item.NoRedundant = !item.NoRedundant;
        await dbContext.UpdateAsync(item);


        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.NoRedundant = item.NoRedundant;
        }

        return true;
    }

    /// <summary>
    ///     Gets a repeater by its index in the guild's list.
    /// </summary>
    public RepeatRunner? GetRepeaterByIndex(ulong guildId, int index)
    {
        if (!Repeaters.TryGetValue(guildId, out var guildRepeaters))
            return null;

        var repeaterList = guildRepeaters.ToList();
        if (index < 0 || index >= repeaterList.Count)
            return null;

        return repeaterList[index].Value;
    }

    /// <summary>
    ///     Gets all repeaters for a guild.
    /// </summary>
    public IReadOnlyList<RepeatRunner> GetGuildRepeaters(ulong guildId)
    {
        if (!Repeaters.TryGetValue(guildId, out var guildRepeaters))
            return [];

        return guildRepeaters.Values.ToList();
    }

    /// <summary>
    ///     Updates display statistics for a repeater.
    /// </summary>
    /// <param name="repeaterId">The ID of the repeater to update.</param>
    /// <param name="displayCount">The new display count.</param>
    /// <param name="lastDisplayed">The timestamp of the last display.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UpdateRepeaterStatsAsync(int repeaterId, int displayCount, DateTime lastDisplayed)
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var repeater = await dbContext.GuildRepeaters.FirstOrDefaultAsync(x => x.Id == repeaterId)
                .ConfigureAwait(false);
            if (repeater != null)
            {
                repeater.DisplayCount = displayCount;
                repeater.LastDisplayed = lastDisplayed;
                await dbContext.UpdateAsync(repeater);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update stats for repeater {RepeaterId}", repeaterId);
        }
    }

    /// <summary>
    ///     Updates the trigger mode of an existing repeater.
    /// </summary>
    public async Task<bool> UpdateRepeaterTriggerModeAsync(ulong guildId, int repeaterId, StickyTriggerMode triggerMode)
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();

            var item = await dbContext.GuildRepeaters
                .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

            if (item == null) return false;

            item.TriggerMode = (int)triggerMode;
            await dbContext.UpdateAsync(item);

            if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
                guildRepeaters.TryGetValue(repeaterId, out var runner))
            {
                runner.Repeater.TriggerMode = item.TriggerMode;
                runner.Reset(); // Restart with new trigger mode
            }

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    ///     Updates the activity threshold of an existing repeater.
    /// </summary>
    public async Task<bool> UpdateRepeaterActivityThresholdAsync(ulong guildId, int repeaterId, int threshold,
        TimeSpan timeWindow)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return false;

        item.ActivityThreshold = threshold;
        item.ActivityTimeWindow = timeWindow.ToString();
        await dbContext.UpdateAsync(item);

        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.ActivityThreshold = item.ActivityThreshold;
            runner.Repeater.ActivityTimeWindow = item.ActivityTimeWindow;
            runner.Reset();
        }

        return true;
    }

    /// <summary>
    ///     Updates the priority of an existing repeater.
    /// </summary>
    public async Task<bool> UpdateRepeaterPriorityAsync(ulong guildId, int repeaterId, int priority)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return false;

        item.Priority = priority;
        await dbContext.UpdateAsync(item);

        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.Priority = item.Priority;
        }

        return true;
    }

    /// <summary>
    ///     Toggles conversation detection for a repeater.
    /// </summary>
    public async Task<bool> ToggleRepeaterConversationDetectionAsync(ulong guildId, int repeaterId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return false;

        item.ConversationDetection = !item.ConversationDetection;
        await dbContext.UpdateAsync(item);

        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.ConversationDetection = item.ConversationDetection;
        }

        return true;
    }

    /// <summary>
    ///     Updates the time conditions of an existing repeater.
    /// </summary>
    public async Task<bool> UpdateRepeaterTimeConditionsAsync(ulong guildId, int repeaterId, string? timeConditionsJson)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return false;

        item.TimeConditions = timeConditionsJson;
        await dbContext.UpdateAsync(item);

        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.TimeConditions = item.TimeConditions;
        }

        return true;
    }

    /// <summary>
    ///     Toggles the enabled state of a repeater.
    /// </summary>
    public async Task<bool> ToggleRepeaterEnabledAsync(ulong guildId, int repeaterId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return false;

        item.IsEnabled = !item.IsEnabled;
        await dbContext.UpdateAsync(item);

        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.IsEnabled = item.IsEnabled;
            if (item.IsEnabled)
                runner.Reset(); // Restart if enabled
            else
                runner.Stop(); // Stop if disabled
        }

        return true;
    }

    /// <summary>
    ///     Toggles the thread auto-sticky setting of a repeater.
    /// </summary>
    public async Task<bool> ToggleRepeaterThreadAutoStickyAsync(ulong guildId, int repeaterId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return false;

        item.ThreadAutoSticky = !item.ThreadAutoSticky;
        await dbContext.UpdateAsync(item);

        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.ThreadAutoSticky = item.ThreadAutoSticky;
        }

        return true;
    }

    /// <summary>
    ///     Updates forum tag conditions for a repeater.
    /// </summary>
    public async Task<string?> UpdateRepeaterForumTagConditionsAsync(ulong guildId, int repeaterId,
        string? action = null, string? tagType = null, List<ulong>? tagIds = null)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return null;

        string? newConditionsJson;

        if (action == null)
        {
            // Clear conditions
            newConditionsJson = null;
        }
        else
        {
            // Parse existing conditions or create new
            var existingConditions = conditionService?.ParseForumTagConditions(item.ForumTagConditions) ??
                                     new ForumTagCondition();

            switch (tagType)
            {
                case "required" when tagIds != null:
                {
                    var requiredList = existingConditions.RequiredTags?.ToList() ?? [];
                    switch (action)
                    {
                        case "add":
                            requiredList.AddRange(tagIds.Where(id => !requiredList.Contains(id)));
                            break;
                        case "remove":
                            requiredList.RemoveAll(tagIds.Contains);
                            break;
                    }

                    existingConditions.RequiredTags = requiredList.ToArray();
                    break;
                }
                case "excluded" when tagIds != null:
                {
                    var excludedList = existingConditions.ExcludedTags?.ToList() ?? [];
                    switch (action)
                    {
                        case "add":
                            excludedList.AddRange(tagIds.Where(id => !excludedList.Contains(id)));
                            break;
                        case "remove":
                            excludedList.RemoveAll(tagIds.Contains);
                            break;
                    }

                    existingConditions.ExcludedTags = excludedList.ToArray();
                    break;
                }
            }

            newConditionsJson = conditionService?.SerializeForumTagConditions(existingConditions);
        }

        item.ForumTagConditions = newConditionsJson;
        await dbContext.UpdateAsync(item);

        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.ForumTagConditions = item.ForumTagConditions;
        }

        return newConditionsJson;
    }

    /// <summary>
    ///     Toggles the thread-only mode setting of a repeater.
    /// </summary>
    public async Task<bool> ToggleRepeaterThreadOnlyModeAsync(ulong guildId, int repeaterId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return false;

        item.ThreadOnlyMode = !item.ThreadOnlyMode;
        await dbContext.UpdateAsync(item);

        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.ThreadOnlyMode = item.ThreadOnlyMode;
        }

        return true;
    }

    /// <summary>
    ///     Updates the conversation threshold for a repeater.
    /// </summary>
    public async Task<bool> UpdateRepeaterConversationThresholdAsync(ulong guildId, int repeaterId, int threshold)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return false;

        item.ConversationThreshold = threshold;
        await dbContext.UpdateAsync(item);

        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.ConversationThreshold = threshold;
        }

        return true;
    }

    /// <summary>
    ///     Updates the expiry settings (max age and max triggers) for a repeater.
    /// </summary>
    public async Task<bool> UpdateRepeaterExpiryAsync(ulong guildId, int repeaterId, string? maxAge, int? maxTriggers)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return false;

        // Set maxAge if provided (stored as TimeSpan string)
        if (!string.IsNullOrWhiteSpace(maxAge))
        {
            item.MaxAge = maxAge;
        }

        if (maxTriggers.HasValue)
        {
            item.MaxTriggers = maxTriggers.Value;
        }

        await dbContext.UpdateAsync(item);

        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.MaxAge = item.MaxAge;
            runner.Repeater.MaxTriggers = item.MaxTriggers;
        }

        return true;
    }

    /// <summary>
    ///     Updates forum tag conditions for a repeater using JSON string.
    /// </summary>
    public async Task<bool> UpdateRepeaterForumTagConditionsAsync(ulong guildId, int repeaterId,
        string forumTagConditionsJson)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var item = await dbContext.GuildRepeaters
            .FirstOrDefaultAsync(r => r.Id == repeaterId && r.GuildId == guildId);

        if (item == null) return false;

        item.ForumTagConditions = forumTagConditionsJson;
        await dbContext.UpdateAsync(item);

        if (Repeaters.TryGetValue(guildId, out var guildRepeaters) &&
            guildRepeaters.TryGetValue(repeaterId, out var runner))
        {
            runner.Repeater.ForumTagConditions = forumTagConditionsJson;
        }

        return true;
    }

    private async Task OnMessageReceived(SocketMessage message)
    {
        try
        {
            // Only process messages in guild channels, ignore bot messages
            if (message.Channel is not IGuildChannel guildChannel || message.Author.IsBot)
                return;

            // Skip if this is our own sticky message being posted
            if (message.Author.Id == client.CurrentUser?.Id)
                return;

            var guildId = guildChannel.GuildId;
            if (!Repeaters.TryGetValue(guildId, out var guildRepeaters))
                return;

            // Check for immediate mode repeaters in parent channels
            var immediateRepeaters = guildRepeaters.Values
                .Where(r => r.Repeater.IsEnabled &&
                            r.Repeater.ChannelId == guildChannel.Id &&
                            (StickyTriggerMode)r.Repeater.TriggerMode == StickyTriggerMode.Immediate &&
                            !r.Repeater.ThreadOnlyMode)
                .ToList();

            // Check for thread stickies if this is a thread message
            if (message.Channel is SocketThreadChannel threadChannel)
            {
                var threadRepeaters = guildRepeaters.Values
                    .Where(r => r.Repeater.IsEnabled &&
                                r.Repeater.ThreadAutoSticky &&
                                ShouldManageThreadSticky(r.Repeater, threadChannel))
                    .ToList();

                // Handle thread sticky repositioning
                foreach (var repeater in threadRepeaters)
                {
                    // Small delay to avoid race condition with ThreadCreated event
                    if (DateTime.UtcNow - threadChannel.CreatedAt < TimeSpan.FromSeconds(2))
                    {
                        logger.LogDebug("Skipping thread sticky check for new thread {ThreadId}, too recent",
                            threadChannel.Id);
                        continue;
                    }

                    await CheckAndRepositionThreadSticky(repeater, threadChannel);
                }
            }

            // Handle parent channel immediate stickies
            foreach (var repeater in immediateRepeaters)
            {
                try
                {
                    // Check if our sticky message is no longer the last message
                    if (repeater.Channel != null && repeater.Repeater.LastMessageId.HasValue)
                    {
                        var lastMessage = await GetLastChannelMessage(repeater.Channel);
                        if (lastMessage?.Id != repeater.Repeater.LastMessageId.Value)
                        {
                            // Our sticky is no longer at the bottom, repost it
                            _ = repeater.TriggerInternal(true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error checking immediate sticky position for repeater {RepeaterId}",
                        repeater.Repeater.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in OnMessageReceived for sticky messages");
        }
    }

    private async Task<IMessage?> GetLastChannelMessage(ITextChannel channel)
    {
        try
        {
            var messages = await channel.GetMessagesAsync(1).FlattenAsync();
            return messages.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private bool ShouldManageThreadSticky(GuildRepeater repeater, SocketThreadChannel thread)
    {
        // Check if this thread belongs to the repeater's target channel
        if (thread.ParentChannel is IForumChannel forumChannel)
        {
            // For forum threads, check if parent forum matches and tags are valid
            if (repeater.ChannelId != forumChannel.Id)
                return false;

            var threadTags = thread.AppliedTags?.ToArray() ?? Array.Empty<ulong>();
            return conditionService?.ShouldDisplayForForumTags(repeater, threadTags) ?? true;
        }

        // For regular threads, check if parent channel matches
        return repeater.ChannelId == thread.ParentChannel.Id;
    }

    private async Task CheckAndRepositionThreadSticky(RepeatRunner repeater, SocketThreadChannel thread)
    {
        try
        {
            // Get thread sticky tracker
            var tracker = GetThreadStickyTracker(repeater.Repeater);
            var currentStickyMessageId = tracker?.GetThreadStickyMessage(thread.Id);

            if (!currentStickyMessageId.HasValue)
                return; // No sticky in this thread yet

            // Check if sticky is still the last message
            var lastMessage = await GetLastThreadMessage(thread);
            if (lastMessage?.Id == currentStickyMessageId.Value)
                return; // Still at bottom, no need to reposition

            // Reposition the sticky
            await RepositionThreadSticky(repeater.Repeater, thread, currentStickyMessageId.Value);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Error checking thread sticky position for repeater {RepeaterId} in thread {ThreadId}",
                repeater.Repeater.Id, thread.Id);
        }
    }

    private async Task RepositionThreadSticky(GuildRepeater repeater, SocketThreadChannel thread, ulong oldMessageId)
    {
        try
        {
            // Delete old sticky message
            try
            {
                var oldMessage = await thread.GetMessageAsync(oldMessageId);
                if (oldMessage != null)
                    await oldMessage.DeleteAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete old thread sticky message {MessageId}", oldMessageId);
            }

            // Send new sticky message
            var currentUser = thread.Guild.CurrentUser;
            var rep = new ReplacementBuilder()
                .WithDefault(currentUser, thread, thread.Guild, client)
                .Build();

            var message = rep.Replace(repeater.Message ?? "");
            IMessage? newMessage = null;

            if (SmartEmbed.TryParse(message, thread.Guild.Id, out var embed, out var plainText, out var components))
            {
                newMessage =
                    await thread.SendMessageAsync(plainText ?? "", embeds: embed, components: components?.Build());
            }
            else
            {
                newMessage = await thread.SendMessageAsync(message);
            }

            // Update tracking
            if (newMessage != null)
            {
                await TrackThreadStickyMessage(repeater.Id, thread.Id, newMessage.Id);
                logger.LogDebug("Repositioned thread sticky in {ThreadId} for repeater {RepeaterId}",
                    thread.Id, repeater.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to reposition thread sticky for repeater {RepeaterId} in thread {ThreadId}",
                repeater.Id, thread.Id);
        }
    }

    private async Task<IMessage?> GetLastThreadMessage(SocketThreadChannel thread)
    {
        try
        {
            var messages = await thread.GetMessagesAsync(1).FlattenAsync();
            return messages.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private ThreadStickyTracker? GetThreadStickyTracker(GuildRepeater repeater)
    {
        if (string.IsNullOrWhiteSpace(repeater.ThreadStickyMessages))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ThreadStickyTracker>(repeater.ThreadStickyMessages);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse thread sticky tracker for repeater {RepeaterId}", repeater.Id);
            return null;
        }
    }

    private async Task TrackThreadStickyMessage(int repeaterId, ulong threadId, ulong messageId)
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var repeater = await dbContext.GuildRepeaters.FirstOrDefaultAsync(x => x.Id == repeaterId);

            if (repeater != null)
            {
                var tracker = GetThreadStickyTracker(repeater) ?? new ThreadStickyTracker();
                tracker.SetThreadStickyMessage(threadId, messageId);

                repeater.ThreadStickyMessages = JsonSerializer.Serialize(tracker);
                await dbContext.UpdateAsync(repeater);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to track thread sticky message for repeater {RepeaterId}", repeaterId);
        }
    }
}