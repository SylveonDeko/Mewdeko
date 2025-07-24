using System.Diagnostics;
using System.Threading;
using Discord.Interactions;
using Discord.Rest;
using Serilog;

namespace Mewdeko.Services.Impl;

/// <summary>
///     Provides asynchronous event handling for Discord.NET
/// </summary>
public sealed class EventHandler : IDisposable
{
    private readonly ConcurrentDictionary<string, CircuitBreaker> circuitBreakers = new();
    private readonly DiscordShardedClient client;

    // Metrics tracking
    private readonly ConcurrentDictionary<string, EventMetrics> eventMetrics = new();
    private readonly InteractionService interaction;
    private readonly ILogger<EventHandler> logger;
    private readonly BatchedEventProcessor<SocketMessage> messageProcessor;
    private readonly Timer metricsResetTimer;
    private readonly ConcurrentDictionary<string, ModuleMetrics> moduleMetrics = new();
    private readonly EventHandlerOptions options;
    private readonly PerformanceMonitorService perfService;
    private readonly BatchedEventProcessor<(SocketUser, SocketPresence, SocketPresence)> presenceProcessor;

    // Rate limiting
    private readonly ConcurrentDictionary<string, RateLimiter> rateLimiters = new();

    // String-based event subscription tracking for proper disposal
    private readonly ConcurrentDictionary<string, List<(string ModuleName, Delegate Handler)>>
        stringEventSubscriptions = new();

    // Event subscription management

    private readonly BatchedEventProcessor<(Cacheable<IUser, ulong>, Cacheable<IMessageChannel, ulong>)>
        typingProcessor;

    private volatile bool disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EventHandler" /> class.
    /// </summary>
    /// <param name="client">The Discord sharded client instance to handle events for.</param>
    /// <param name="perfService">The performance monitoring service to track event execution times.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="options">Configuration options for the event handler.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public EventHandler(
        DiscordShardedClient client,
        InteractionService interaction,
        PerformanceMonitorService perfService,
        ILogger<EventHandler> logger,
        EventHandlerOptions options)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.interaction = interaction;
        this.perfService = perfService ?? throw new ArgumentNullException(nameof(perfService));
        this.logger = logger;
        this.options = options ?? throw new ArgumentNullException(nameof(options));

        // Initialize rate limiters
        rateLimiters["PresenceUpdated"] =
            new RateLimiter(this.options.PresenceUpdateRateLimit, TimeSpan.FromSeconds(1));
        rateLimiters["UserIsTyping"] = new RateLimiter(this.options.TypingRateLimit, TimeSpan.FromSeconds(1));

        // Initialize batched processors
        messageProcessor = new BatchedEventProcessor<SocketMessage>(
            TimeSpan.FromMilliseconds(100), 50, this.options.MaxQueueSize);
        presenceProcessor = new BatchedEventProcessor<(SocketUser, SocketPresence, SocketPresence)>(
            TimeSpan.FromSeconds(1), 100, this.options.MaxQueueSize);
        typingProcessor = new BatchedEventProcessor<(Cacheable<IUser, ulong>, Cacheable<IMessageChannel, ulong>)>(
            TimeSpan.FromMilliseconds(500), 25, this.options.MaxQueueSize);

        // Initialize metrics reset timer
        metricsResetTimer = new Timer(ResetMetrics, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

        RegisterEvents();
        StartBatchProcessors();

        logger.LogInformation("EventHandler initialized with {Options}", this.options);
    }

    #region Event Execution

    private async Task ExecuteHandlerByEventType<T>(string eventType, Delegate handler, T args)
    {
        switch (eventType)
        {
            // Single parameter events
            case "ApplicationCommandCreated":
            case "ApplicationCommandDeleted":
            case "ApplicationCommandUpdated":
            case "AutoModRuleCreated":
            case "AutoModRuleDeleted":
            case "AutocompleteExecuted":
            case "ButtonExecuted":
            case "ChannelCreated":
            case "ChannelDestroyed":
            case "EntitlementCreated":
            case "GuildAvailable":
            case "GuildScheduledEventCancelled":
            case "GuildScheduledEventCompleted":
            case "GuildScheduledEventCreated":
            case "GuildScheduledEventStarted":
            case "GuildStickerCreated":
            case "GuildStickerDeleted":
            case "GuildUnavailable":
            case "IntegrationCreated":
            case "IntegrationUpdated":
            case "InteractionCreated":
            case "InviteCreated":
            case "JoinedGuild":
            case "LeftGuild":
            case "MessageCommandExecuted":
            case "MessageReceived":
            case "ModalSubmitted":
            case "Ready":
            case "RecipientAdded":
            case "RecipientRemoved":
            case "RoleCreated":
            case "RoleDeleted":
            case "SelectMenuExecuted":
            case "SlashCommandExecuted":
            case "StageEnded":
            case "StageStarted":
            case "SubscriptionCreated":
            case "ThreadCreated":
            case "ThreadMemberJoined":
            case "ThreadMemberLeft":
            case "UserCommandExecuted":
            case "UserJoined":
            case "VoiceServerUpdated":
            case "GuildMembersDownloaded":
            case "XpLevelChanged":
                switch (handler)
                {
                    case AsyncEventHandler<T> singleHandler:
                        await singleHandler(args);
                        break;
                    case Func<T, Task> funcHandler:
                        await funcHandler(args);
                        break;
                }

                break;

            // Two parameter events
            case "AuditLogCreated":
                if (args is ValueTuple<SocketAuditLogEntry, SocketGuild> auditLogCreatedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<SocketAuditLogEntry, SocketGuild> auditLogCreatedHandler:
                            await auditLogCreatedHandler(auditLogCreatedArgs.Item1, auditLogCreatedArgs.Item2);
                            break;
                        case Func<SocketAuditLogEntry, SocketGuild, Task> auditLogCreatedFunc:
                            await auditLogCreatedFunc(auditLogCreatedArgs.Item1, auditLogCreatedArgs.Item2);
                            break;
                    }
                }

                break;
            case "AutoModRuleUpdated":
                if (args is ValueTuple<Cacheable<SocketAutoModRule, ulong>, SocketAutoModRule> autoModRuleUpdatedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<Cacheable<SocketAutoModRule, ulong>, SocketAutoModRule>
                            autoModRuleUpdatedHandler:
                            await autoModRuleUpdatedHandler(autoModRuleUpdatedArgs.Item1, autoModRuleUpdatedArgs.Item2);
                            break;
                        case Func<Cacheable<SocketAutoModRule, ulong>, SocketAutoModRule, Task> autoModRuleUpdatedFunc:
                            await autoModRuleUpdatedFunc(autoModRuleUpdatedArgs.Item1, autoModRuleUpdatedArgs.Item2);
                            break;
                    }
                }

                break;

            case "ChannelUpdated":
                if (args is ValueTuple<SocketChannel, SocketChannel> channelUpdatedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<SocketChannel, SocketChannel> channelUpdatedHandler:
                            await channelUpdatedHandler(channelUpdatedArgs.Item1, channelUpdatedArgs.Item2);
                            break;
                        case Func<SocketChannel, SocketChannel, Task> channelUpdatedFunc:
                            await channelUpdatedFunc(channelUpdatedArgs.Item1, channelUpdatedArgs.Item2);
                            break;
                    }
                }

                break;

            case "CurrentUserUpdated":
                if (args is ValueTuple<SocketSelfUser, SocketSelfUser> currentUserUpdatedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<SocketSelfUser, SocketSelfUser> currentUserUpdatedHandler:
                            await currentUserUpdatedHandler(currentUserUpdatedArgs.Item1, currentUserUpdatedArgs.Item2);
                            break;
                        case Func<SocketSelfUser, SocketSelfUser, Task> currentUserUpdatedFunc:
                            await currentUserUpdatedFunc(currentUserUpdatedArgs.Item1, currentUserUpdatedArgs.Item2);
                            break;
                    }
                }

                break;

            case "EntitlementDeleted":
                if (args is ValueTuple<Cacheable<SocketEntitlement, ulong>, SocketEntitlement?> entitlementDeletedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<Cacheable<SocketEntitlement, ulong>, SocketEntitlement?>
                            entitlementDeletedHandler:
                            await entitlementDeletedHandler(entitlementDeletedArgs.Item1, entitlementDeletedArgs.Item2);
                            break;
                        case Func<Cacheable<SocketEntitlement, ulong>, SocketEntitlement?, Task> entitlementDeletedFunc:
                            await entitlementDeletedFunc(entitlementDeletedArgs.Item1, entitlementDeletedArgs.Item2);
                            break;
                    }
                }

                break;

            case "EntitlementUpdated":
                if (args is ValueTuple<Cacheable<SocketEntitlement, ulong>, SocketEntitlement> entitlementUpdatedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<Cacheable<SocketEntitlement, ulong>, SocketEntitlement>
                            entitlementUpdatedHandler:
                            await entitlementUpdatedHandler(entitlementUpdatedArgs.Item1, entitlementUpdatedArgs.Item2);
                            break;
                        case Func<Cacheable<SocketEntitlement, ulong>, SocketEntitlement, Task> entitlementUpdatedFunc:
                            await entitlementUpdatedFunc(entitlementUpdatedArgs.Item1, entitlementUpdatedArgs.Item2);
                            break;
                    }
                }

                break;

            case "GuildJoinRequestDeleted":
                if (args is ValueTuple<Cacheable<SocketGuildUser, ulong>, SocketGuild> guildJoinRequestDeletedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<Cacheable<SocketGuildUser, ulong>, SocketGuild>
                            guildJoinRequestDeletedHandler:
                            await guildJoinRequestDeletedHandler(guildJoinRequestDeletedArgs.Item1,
                                guildJoinRequestDeletedArgs.Item2);
                            break;
                        case Func<Cacheable<SocketGuildUser, ulong>, SocketGuild, Task> guildJoinRequestDeletedFunc:
                            await guildJoinRequestDeletedFunc(guildJoinRequestDeletedArgs.Item1,
                                guildJoinRequestDeletedArgs.Item2);
                            break;
                    }
                }

                break;

            case "GuildMemberUpdated":
                if (args is ValueTuple<Cacheable<SocketGuildUser, ulong>, SocketGuildUser> guildMemberUpdatedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<Cacheable<SocketGuildUser, ulong>, SocketGuildUser>
                            guildMemberUpdatedHandler:
                            await guildMemberUpdatedHandler(guildMemberUpdatedArgs.Item1, guildMemberUpdatedArgs.Item2);
                            break;
                        case Func<Cacheable<SocketGuildUser, ulong>, SocketGuildUser, Task> guildMemberUpdatedFunc:
                            await guildMemberUpdatedFunc(guildMemberUpdatedArgs.Item1, guildMemberUpdatedArgs.Item2);
                            break;
                    }
                }

                break;

            case "GuildScheduledEventUpdated":
                if (args is ValueTuple<Cacheable<SocketGuildEvent, ulong>, SocketGuildEvent>
                    guildScheduledEventUpdatedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<Cacheable<SocketGuildEvent, ulong>, SocketGuildEvent>
                            guildScheduledEventUpdatedHandler:
                            await guildScheduledEventUpdatedHandler(guildScheduledEventUpdatedArgs.Item1,
                                guildScheduledEventUpdatedArgs.Item2);
                            break;
                        case Func<Cacheable<SocketGuildEvent, ulong>, SocketGuildEvent, Task>
                            guildScheduledEventUpdatedFunc:
                            await guildScheduledEventUpdatedFunc(guildScheduledEventUpdatedArgs.Item1,
                                guildScheduledEventUpdatedArgs.Item2);
                            break;
                    }
                }

                break;

            case "GuildScheduledEventUserAdd":
            case "GuildScheduledEventUserRemove":
                if (args is ValueTuple<Cacheable<SocketUser, RestUser, IUser, ulong>, SocketGuildEvent>
                    guildScheduledEventUserArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<Cacheable<SocketUser, RestUser, IUser, ulong>, SocketGuildEvent>
                            guildScheduledEventUserHandler:
                            await guildScheduledEventUserHandler(guildScheduledEventUserArgs.Item1,
                                guildScheduledEventUserArgs.Item2);
                            break;
                        case Func<Cacheable<SocketUser, RestUser, IUser, ulong>, SocketGuildEvent, Task>
                            guildScheduledEventUserFunc:
                            await guildScheduledEventUserFunc(guildScheduledEventUserArgs.Item1,
                                guildScheduledEventUserArgs.Item2);
                            break;
                    }
                }

                break;

            case "GuildStickerUpdated":
                if (args is ValueTuple<SocketCustomSticker, SocketCustomSticker> guildStickerUpdatedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<SocketCustomSticker, SocketCustomSticker> guildStickerUpdatedHandler:
                            await guildStickerUpdatedHandler(guildStickerUpdatedArgs.Item1,
                                guildStickerUpdatedArgs.Item2);
                            break;
                        case Func<SocketCustomSticker, SocketCustomSticker, Task> guildStickerUpdatedFunc:
                            await guildStickerUpdatedFunc(guildStickerUpdatedArgs.Item1, guildStickerUpdatedArgs.Item2);
                            break;
                    }
                }

                break;

            case "GuildUpdated":
                if (args is ValueTuple<SocketGuild, SocketGuild> guildUpdatedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<SocketGuild, SocketGuild> guildUpdatedHandler:
                            await guildUpdatedHandler(guildUpdatedArgs.Item1, guildUpdatedArgs.Item2);
                            break;
                        case Func<SocketGuild, SocketGuild, Task> guildUpdatedFunc:
                            await guildUpdatedFunc(guildUpdatedArgs.Item1, guildUpdatedArgs.Item2);
                            break;
                    }
                }

                break;

            case "InviteDeleted":
                if (args is ValueTuple<SocketGuildChannel, string> inviteDeletedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<SocketGuildChannel, string> inviteDeletedHandler:
                            await inviteDeletedHandler(inviteDeletedArgs.Item1, inviteDeletedArgs.Item2);
                            break;
                        case Func<SocketGuildChannel, string, Task> inviteDeletedFunc:
                            await inviteDeletedFunc(inviteDeletedArgs.Item1, inviteDeletedArgs.Item2);
                            break;
                    }
                }

                break;

            case "MessageDeleted":
                if (args is ValueTuple<Cacheable<IMessage, ulong>, Cacheable<IMessageChannel, ulong>>
                    messageDeletedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<Cacheable<IMessage, ulong>, Cacheable<IMessageChannel, ulong>>
                            messageDeletedHandler:
                            await messageDeletedHandler(messageDeletedArgs.Item1, messageDeletedArgs.Item2);
                            break;
                        case Func<Cacheable<IMessage, ulong>, Cacheable<IMessageChannel, ulong>, Task>
                            messageDeletedFunc:
                            await messageDeletedFunc(messageDeletedArgs.Item1, messageDeletedArgs.Item2);
                            break;
                    }
                }

                break;

            case "MessagesBulkDeleted":
                if (args is ValueTuple<IReadOnlyCollection<Cacheable<IMessage, ulong>>,
                        Cacheable<IMessageChannel, ulong>> messagesBulkDeletedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<IReadOnlyCollection<Cacheable<IMessage, ulong>>,
                            Cacheable<IMessageChannel, ulong>> messagesBulkDeletedHandler:
                            await messagesBulkDeletedHandler(messagesBulkDeletedArgs.Item1,
                                messagesBulkDeletedArgs.Item2);
                            break;
                        case Func<IReadOnlyCollection<Cacheable<IMessage, ulong>>, Cacheable<IMessageChannel, ulong>,
                            Task> messagesBulkDeletedFunc:
                            await messagesBulkDeletedFunc(messagesBulkDeletedArgs.Item1, messagesBulkDeletedArgs.Item2);
                            break;
                    }
                }

                break;

            case "ReactionsCleared":
                if (args is ValueTuple<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>>
                    reactionsClearedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>>
                            reactionsClearedHandler:
                            await reactionsClearedHandler(reactionsClearedArgs.Item1, reactionsClearedArgs.Item2);
                            break;
                        case Func<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>, Task>
                            reactionsClearedFunc:
                            await reactionsClearedFunc(reactionsClearedArgs.Item1, reactionsClearedArgs.Item2);
                            break;
                    }
                }

                break;

            case "RequestToSpeak":
            case "SpeakerAdded":
            case "SpeakerRemoved":
                if (args is ValueTuple<SocketStageChannel, SocketGuildUser> stageArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<SocketStageChannel, SocketGuildUser> stageHandler:
                            await stageHandler(stageArgs.Item1, stageArgs.Item2);
                            break;
                        case Func<SocketStageChannel, SocketGuildUser, Task> stageFunc:
                            await stageFunc(stageArgs.Item1, stageArgs.Item2);
                            break;
                    }
                }

                break;

            case "RoleUpdated":
                if (args is ValueTuple<SocketRole, SocketRole> roleUpdatedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<SocketRole, SocketRole> roleUpdatedHandler:
                            await roleUpdatedHandler(roleUpdatedArgs.Item1, roleUpdatedArgs.Item2);
                            break;
                        case Func<SocketRole, SocketRole, Task> roleUpdatedFunc:
                            await roleUpdatedFunc(roleUpdatedArgs.Item1, roleUpdatedArgs.Item2);
                            break;
                    }
                }

                break;

            case "StageUpdated":
                if (args is ValueTuple<SocketStageChannel, SocketStageChannel> stageUpdatedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<SocketStageChannel, SocketStageChannel> stageUpdatedHandler:
                            await stageUpdatedHandler(stageUpdatedArgs.Item1, stageUpdatedArgs.Item2);
                            break;
                        case Func<SocketStageChannel, SocketStageChannel, Task> stageUpdatedFunc:
                            await stageUpdatedFunc(stageUpdatedArgs.Item1, stageUpdatedArgs.Item2);
                            break;
                    }
                }

                break;

            case "SubscriptionDeleted":
                if (args is ValueTuple<Cacheable<SocketSubscription, ulong>, SocketSubscription?>
                    subscriptionDeletedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<Cacheable<SocketSubscription, ulong>, SocketSubscription?>
                            subscriptionDeletedHandler:
                            await subscriptionDeletedHandler(subscriptionDeletedArgs.Item1,
                                subscriptionDeletedArgs.Item2);
                            break;
                        case Func<Cacheable<SocketSubscription, ulong>, SocketSubscription?, Task>
                            subscriptionDeletedFunc:
                            await subscriptionDeletedFunc(subscriptionDeletedArgs.Item1, subscriptionDeletedArgs.Item2);
                            break;
                    }
                }

                break;

            case "SubscriptionUpdated":
                if (args is ValueTuple<Cacheable<SocketSubscription, ulong>, SocketSubscription>
                    subscriptionUpdatedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<Cacheable<SocketSubscription, ulong>, SocketSubscription>
                            subscriptionUpdatedHandler:
                            await subscriptionUpdatedHandler(subscriptionUpdatedArgs.Item1,
                                subscriptionUpdatedArgs.Item2);
                            break;
                        case Func<Cacheable<SocketSubscription, ulong>, SocketSubscription, Task>
                            subscriptionUpdatedFunc:
                            await subscriptionUpdatedFunc(subscriptionUpdatedArgs.Item1, subscriptionUpdatedArgs.Item2);
                            break;
                    }
                }

                break;

            case "ThreadDeleted":
                if (args is ValueTuple<Cacheable<SocketThreadChannel, ulong>, SocketThreadChannel?> threadDeletedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<Cacheable<SocketThreadChannel, ulong>, SocketThreadChannel?>
                            threadDeletedHandler:
                            await threadDeletedHandler(threadDeletedArgs.Item1, threadDeletedArgs.Item2);
                            break;
                        case Func<Cacheable<SocketThreadChannel, ulong>, SocketThreadChannel?, Task> threadDeletedFunc:
                            await threadDeletedFunc(threadDeletedArgs.Item1, threadDeletedArgs.Item2);
                            break;
                    }
                }

                break;

            case "ThreadUpdated":
                if (args is ValueTuple<Cacheable<SocketThreadChannel, ulong>, SocketThreadChannel> threadUpdatedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<Cacheable<SocketThreadChannel, ulong>, SocketThreadChannel>
                            threadUpdatedHandler:
                            await threadUpdatedHandler(threadUpdatedArgs.Item1, threadUpdatedArgs.Item2);
                            break;
                        case Func<Cacheable<SocketThreadChannel, ulong>, SocketThreadChannel, Task> threadUpdatedFunc:
                            await threadUpdatedFunc(threadUpdatedArgs.Item1, threadUpdatedArgs.Item2);
                            break;
                    }
                }

                break;

            case "UserBanned":
            case "UserUnbanned":
                if (args is ValueTuple<SocketUser, SocketGuild> userBannedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<SocketUser, SocketGuild> userBannedHandler:
                            await userBannedHandler(userBannedArgs.Item1, userBannedArgs.Item2);
                            break;
                        case Func<SocketUser, SocketGuild, Task> userBannedFunc:
                            await userBannedFunc(userBannedArgs.Item1, userBannedArgs.Item2);
                            break;
                    }
                }

                break;

            case "UserIsTyping":
                if (args is ValueTuple<Cacheable<IUser, ulong>, Cacheable<IMessageChannel, ulong>> userIsTypingArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<Cacheable<IUser, ulong>, Cacheable<IMessageChannel, ulong>>
                            userIsTypingHandler:
                            await userIsTypingHandler(userIsTypingArgs.Item1, userIsTypingArgs.Item2);
                            break;
                        case Func<Cacheable<IUser, ulong>, Cacheable<IMessageChannel, ulong>, Task> userIsTypingFunc:
                            await userIsTypingFunc(userIsTypingArgs.Item1, userIsTypingArgs.Item2);
                            break;
                    }
                }

                break;

            case "UserLeft":
                if (args is ValueTuple<SocketGuild, SocketUser> userLeftArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<SocketGuild, SocketUser> userLeftHandler:
                            await userLeftHandler(userLeftArgs.Item1, userLeftArgs.Item2);
                            break;
                        case Func<SocketGuild, SocketUser, Task> userLeftFunc:
                            await userLeftFunc(userLeftArgs.Item1, userLeftArgs.Item2);
                            break;
                    }
                }

                break;

            case "UserUpdated":
                if (args is ValueTuple<SocketUser, SocketUser> userUpdatedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<SocketUser, SocketUser> userUpdatedHandler:
                            await userUpdatedHandler(userUpdatedArgs.Item1, userUpdatedArgs.Item2);
                            break;
                        case Func<SocketUser, SocketUser, Task> userUpdatedFunc:
                            await userUpdatedFunc(userUpdatedArgs.Item1, userUpdatedArgs.Item2);
                            break;
                    }
                }

                break;

            case "WebhooksUpdated":
                if (args is ValueTuple<SocketGuild, SocketChannel> webhooksUpdatedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<SocketGuild, SocketChannel> webhooksUpdatedHandler:
                            await webhooksUpdatedHandler(webhooksUpdatedArgs.Item1, webhooksUpdatedArgs.Item2);
                            break;
                        case Func<SocketGuild, SocketChannel, Task> webhooksUpdatedFunc:
                            await webhooksUpdatedFunc(webhooksUpdatedArgs.Item1, webhooksUpdatedArgs.Item2);
                            break;
                    }
                }

                break;

            // Three parameter events
            case "MessageUpdated":
                if (args is ValueTuple<Cacheable<IMessage, ulong>, SocketMessage, ISocketMessageChannel> msgUpdatedArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<Cacheable<IMessage, ulong>, SocketMessage, ISocketMessageChannel>
                            msgUpdatedHandler:
                            await msgUpdatedHandler(msgUpdatedArgs.Item1, msgUpdatedArgs.Item2, msgUpdatedArgs.Item3);
                            break;
                        case Func<Cacheable<IMessage, ulong>, SocketMessage, ISocketMessageChannel, Task> msgUpdatedFunc
                            :
                            await msgUpdatedFunc(msgUpdatedArgs.Item1, msgUpdatedArgs.Item2, msgUpdatedArgs.Item3);
                            break;
                    }
                }

                break;

            case "UserVoiceStateUpdated":
                if (args is ValueTuple<SocketUser, SocketVoiceState, SocketVoiceState> voiceStateArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<SocketUser, SocketVoiceState, SocketVoiceState> voiceStateHandler:
                            await voiceStateHandler(voiceStateArgs.Item1, voiceStateArgs.Item2, voiceStateArgs.Item3);
                            break;
                        case Func<SocketUser, SocketVoiceState, SocketVoiceState, Task> voiceStateFunc:
                            await voiceStateFunc(voiceStateArgs.Item1, voiceStateArgs.Item2, voiceStateArgs.Item3);
                            break;
                    }
                }

                break;

            case "PresenceUpdated":
                if (args is ValueTuple<SocketUser, SocketPresence, SocketPresence> presenceArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<SocketUser, SocketPresence, SocketPresence> presenceHandler:
                            await presenceHandler(presenceArgs.Item1, presenceArgs.Item2, presenceArgs.Item3);
                            break;
                        case Func<SocketUser, SocketPresence, SocketPresence, Task> presenceFunc:
                            await presenceFunc(presenceArgs.Item1, presenceArgs.Item2, presenceArgs.Item3);
                            break;
                    }
                }

                break;

            case "ReactionAdded":
            case "ReactionRemoved":
                if (args is ValueTuple<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>,
                        SocketReaction> reactionArgs)
                {
                    switch (handler)
                    {
                        case AsyncEventHandler<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>,
                            SocketReaction> reactionHandler:
                            await reactionHandler(reactionArgs.Item1, reactionArgs.Item2, reactionArgs.Item3);
                            break;
                        case Func<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>, SocketReaction,
                            Task> reactionFunc:
                            await reactionFunc(reactionArgs.Item1, reactionArgs.Item2, reactionArgs.Item3);
                            break;
                    }
                }

                break;

            default:
                logger.LogWarning("Unknown event type for execution: {EventType}", eventType);
                break;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    ///     Subscribes to an event using its string name (matches ProcessDirectEvent event names).
    /// </summary>
    /// <param name="eventName">The exact event name used in ProcessDirectEvent calls.</param>
    /// <param name="moduleName">The name of the module subscribing to the event.</param>
    /// <param name="handler">The event handler delegate.</param>
    /// <param name="filter">Optional filter to determine which events to process.</param>
    /// <param name="priority">Event handler priority (higher values execute first).</param>
    public void Subscribe(string eventName, string moduleName, Delegate? handler, IEventFilter? filter = null,
        int priority = 0)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            throw new ArgumentException("Event name cannot be null or empty", nameof(eventName));
        if (string.IsNullOrWhiteSpace(moduleName))
            throw new ArgumentException("Module name cannot be null or empty", nameof(moduleName));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        // Store directly in string event subscriptions
        stringEventSubscriptions.AddOrUpdate(eventName,
            [(moduleName, handler)],
            (_, existing) =>
            {
                var newList = new List<(string, Delegate)>(existing)
                {
                    (moduleName, handler)
                };
                return newList;
            });

        logger.LogWarning("Module {ModuleName} subscribed to string event {EventName} with priority {Priority}",
            moduleName, eventName, priority);
    }

    /// <summary>
    ///     Unsubscribes from an event using its string name.
    /// </summary>
    /// <param name="eventName">The event name to unsubscribe from.</param>
    /// <param name="moduleName">The module name.</param>
    /// <param name="handler">The handler to remove.</param>
    public void Unsubscribe(string eventName, string moduleName, Delegate? handler)
    {
        if (string.IsNullOrWhiteSpace(eventName) || string.IsNullOrWhiteSpace(moduleName) || handler == null)
            return;

        if (!stringEventSubscriptions.TryGetValue(eventName, out var existing)) return;
        var updated = existing.Where(s => !(s.ModuleName == moduleName && ReferenceEquals(s.Handler, handler)))
            .ToList();
        if (updated.Count == existing.Count) return;
        if (updated.Count == 0)
            stringEventSubscriptions.TryRemove(eventName, out _);
        else
            stringEventSubscriptions[eventName] = updated;
    }

    /// <summary>
    ///     Gets current event metrics for all tracked events.
    /// </summary>
    /// <returns>A read-only dictionary of event metrics.</returns>
    public IReadOnlyDictionary<string, EventMetrics> GetEventMetrics()
    {
        return new Dictionary<string, EventMetrics>(eventMetrics);
    }

    /// <summary>
    ///     Gets current module metrics for all registered modules.
    /// </summary>
    /// <returns>A read-only dictionary of module metrics.</returns>
    public IReadOnlyDictionary<string, ModuleMetrics> GetModuleMetrics()
    {
        return new Dictionary<string, ModuleMetrics>(moduleMetrics);
    }

    /// <summary>
    ///     Publishes a custom event to all subscribers.
    /// </summary>
    /// <typeparam name="T">The event argument type.</typeparam>
    /// <param name="eventType">The event type name.</param>
    /// <param name="args">The event arguments.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task PublishEventAsync<T>(string eventType, T args)
    {
        return ProcessDirectEvent(eventType, args);
    }

    #endregion

    #region Event Registration and Processing

    private void RegisterEvents()
    {
        client.MessageReceived += ClientOnMessageReceived;
        client.UserJoined += ClientOnUserJoined;
        client.UserLeft += ClientOnUserLeft;
        client.MessageDeleted += ClientOnMessageDeleted;
        client.GuildMemberUpdated += ClientOnGuildMemberUpdated;
        client.MessageUpdated += ClientOnMessageUpdated;
        client.MessagesBulkDeleted += ClientOnMessagesBulkDeleted;
        client.UserBanned += ClientOnUserBanned;
        client.UserUnbanned += ClientOnUserUnbanned;
        client.UserVoiceStateUpdated += ClientOnUserVoiceStateUpdated;
        client.UserUpdated += ClientOnUserUpdated;
        client.ChannelCreated += ClientOnChannelCreated;
        client.ChannelDestroyed += ClientOnChannelDestroyed;
        client.ChannelUpdated += ClientOnChannelUpdated;
        client.RoleDeleted += ClientOnRoleDeleted;
        client.ReactionAdded += ClientOnReactionAdded;
        client.ReactionRemoved += ClientOnReactionRemoved;
        client.ReactionsCleared += ClientOnReactionsCleared;
        client.InteractionCreated += ClientOnInteractionCreated;
        client.UserIsTyping += ClientOnUserIsTyping;
        client.PresenceUpdated += ClientOnPresenceUpdated;
        client.JoinedGuild += ClientOnJoinedGuild;
        client.GuildScheduledEventCreated += ClientOnEventCreated;
        client.RoleUpdated += ClientOnRoleUpdated;
        client.GuildUpdated += ClientOnGuildUpdated;
        client.RoleCreated += ClientOnRoleCreated;
        client.ThreadCreated += ClientOnThreadCreated;
        client.ThreadUpdated += ClientOnThreadUpdated;
        client.ThreadDeleted += ClientOnThreadDeleted;
        client.ThreadMemberJoined += ClientOnThreadMemberJoined;
        client.ThreadMemberLeft += ClientOnThreadMemberLeft;
        client.AuditLogCreated += ClientOnAuditLogCreated;
        client.GuildAvailable += ClientOnGuildAvailable;
        client.GuildUnavailable += ClientOnGuildUnavailable;
        client.LeftGuild += ClientOnLeftGuild;
        client.InviteCreated += ClientOnInviteCreated;
        client.InviteDeleted += ClientOnInviteDeleted;
        client.ModalSubmitted += ClientOnModalSubmitted;
    }

    private void StartBatchProcessors()
    {
        messageProcessor.BatchReady +=
            async batch => await ProcessEventBatch("MessageReceived", batch).ConfigureAwait(false);
        presenceProcessor.BatchReady += async batch =>
            await ProcessEventBatch("PresenceUpdated", batch).ConfigureAwait(false);
        typingProcessor.BatchReady +=
            async batch => await ProcessEventBatch("UserIsTyping", batch).ConfigureAwait(false);
    }

    private async Task ProcessEventBatch<T>(string eventType, IReadOnlyList<T> batch)
    {
        if (disposed || !stringEventSubscriptions.TryGetValue(eventType, out var handlers))
            return;

        var metrics = GetOrCreateEventMetrics(eventType);
        var sw = Stopwatch.StartNew();

        try
        {
            foreach (var item in batch)
            {
                await ProcessSingleEventBatch(eventType, item, handlers).ConfigureAwait(false);
            }

            sw.Stop();
            Interlocked.Add(ref metrics.TotalProcessed, batch.Count);
            Interlocked.Add(ref metrics.TotalExecutionTime, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref metrics.TotalErrors);
            logger.LogError(ex, "Error processing batch for {EventType}", eventType);
        }
    }

    private async Task ProcessSingleEventBatch<T>(string eventType, T args,
        List<(string ModuleName, Delegate Handler)> handlers)
    {
        foreach (var (moduleName, handler) in handlers)
        {
            try
            {
                var orCreateModuleMetrics = GetOrCreateModuleMetrics(moduleName);
                var sw = Stopwatch.StartNew();

                try
                {
                    if (eventType.ToLower().Contains("intera"))
                        Log.Information($"Executing Interaction Handler for {moduleName}");
                    using (perfService.Measure($"Event_{eventType}_{moduleName}"))
                    {
                        await ExecuteHandlerByEventType(eventType, handler, args).ConfigureAwait(false);
                    }

                    sw.Stop();
                    Interlocked.Increment(ref orCreateModuleMetrics.EventsProcessed);
                    Interlocked.Add(ref orCreateModuleMetrics.TotalExecutionTime, sw.ElapsedMilliseconds);
                }
                catch (Exception)
                {
                    sw.Stop();
                    Interlocked.Increment(ref orCreateModuleMetrics.Errors);
                    throw;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error executing batch handler for {EventType} in module {ModuleName}", eventType,
                    moduleName);
            }
        }
    }

    private async Task ExecuteHandlerWithMetrics<T>(EventSubscription<T> subscription, T args, string eventType)
    {
        var createModuleMetrics = GetOrCreateModuleMetrics(subscription.ModuleName);
        var sw = Stopwatch.StartNew();

        try
        {
            using (perfService.Measure($"Event_{eventType}_{subscription.ModuleName}"))
            {
                await subscription.Handler(args).ConfigureAwait(false);
            }

            sw.Stop();
            Interlocked.Increment(ref createModuleMetrics.EventsProcessed);
            Interlocked.Add(ref createModuleMetrics.TotalExecutionTime, sw.ElapsedMilliseconds);
        }
        catch (Exception)
        {
            sw.Stop();
            Interlocked.Increment(ref createModuleMetrics.Errors);

            // Update circuit breaker
            if (!options.EnableCircuitBreaker) throw;
            var breakerKey = $"{subscription.ModuleName}_{eventType}";
            if (circuitBreakers.TryGetValue(breakerKey, out var breaker))
            {
                breaker.RecordFailure();
            }

            throw;
        }
    }

    private void HandleSubscriptionError<T>(EventSubscription<T> subscription, string eventType, Exception ex)
    {
        var moduleMetrics = GetOrCreateModuleMetrics(subscription.ModuleName);
        Interlocked.Increment(ref moduleMetrics.Errors);

        logger.LogError(ex,
            "Error in {ModuleName} handler for {EventType}: {ErrorMessage}",
            subscription.ModuleName, eventType, ex.Message);
    }

    #endregion

    #region Event Handlers

    private Task ClientOnMessageReceived(SocketMessage arg)
    {
        if (messageProcessor != null)
            messageProcessor.Enqueue(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnPresenceUpdated(SocketUser arg1, SocketPresence arg2, SocketPresence arg3)
    {
        if (rateLimiters.TryGetValue("PresenceUpdated", out var limiter) && !limiter.TryAcquire())
            return Task.CompletedTask;

        if (presenceProcessor != null)
            presenceProcessor.Enqueue((arg1, arg2, arg3));
        return Task.CompletedTask;
    }

    private Task ClientOnUserIsTyping(Cacheable<IUser, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        if (rateLimiters.TryGetValue("UserIsTyping", out var limiter) && !limiter.TryAcquire())
            return Task.CompletedTask;

        if (typingProcessor != null)
            typingProcessor.Enqueue((arg1, arg2));
        return Task.CompletedTask;
    }

    // Direct processing for lower-frequency events
    private Task ClientOnUserJoined(SocketGuildUser arg)
    {
        return ProcessDirectEvent("UserJoined", arg);
    }

    private Task ClientOnUserLeft(SocketGuild arg1, SocketUser arg2)
    {
        return ProcessDirectEvent("UserLeft", (arg1, arg2));
    }

    private Task ClientOnMessageDeleted(Cacheable<IMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        return ProcessDirectEvent("MessageDeleted", (arg1, arg2));
    }

    private Task ClientOnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> arg1, SocketGuildUser arg2)
    {
        return ProcessDirectEvent("GuildMemberUpdated", (arg1, arg2));
    }

    private Task ClientOnMessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
    {
        return ProcessDirectEvent("MessageUpdated", (arg1, arg2, arg3));
    }

    private Task ClientOnMessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> arg1,
        Cacheable<IMessageChannel, ulong> arg2)
    {
        return ProcessDirectEvent("MessagesBulkDeleted", (arg1, arg2));
    }

    private Task ClientOnUserBanned(SocketUser arg1, SocketGuild arg2)
    {
        return ProcessDirectEvent("UserBanned", (arg1, arg2));
    }

    private Task ClientOnUserUnbanned(SocketUser arg1, SocketGuild arg2)
    {
        return ProcessDirectEvent("UserUnbanned", (arg1, arg2));
    }

    private Task ClientOnUserVoiceStateUpdated(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
    {
        return ProcessDirectEvent("UserVoiceStateUpdated", (arg1, arg2, arg3));
    }

    private Task ClientOnUserUpdated(SocketUser arg1, SocketUser arg2)
    {
        return ProcessDirectEvent("UserUpdated", (arg1, arg2));
    }

    private Task ClientOnChannelCreated(SocketChannel arg)
    {
        return ProcessDirectEvent("ChannelCreated", arg);
    }

    private Task ClientOnChannelDestroyed(SocketChannel arg)
    {
        return ProcessDirectEvent("ChannelDestroyed", arg);
    }

    private Task ClientOnChannelUpdated(SocketChannel arg1, SocketChannel arg2)
    {
        return ProcessDirectEvent("ChannelUpdated", (arg1, arg2));
    }

    private Task ClientOnRoleDeleted(SocketRole arg)
    {
        return ProcessDirectEvent("RoleDeleted", arg);
    }

    private Task ClientOnReactionAdded(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2,
        SocketReaction arg3)
    {
        return ProcessDirectEvent("ReactionAdded", (arg1, arg2, arg3));
    }

    private Task ClientOnReactionRemoved(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2,
        SocketReaction arg3)
    {
        return ProcessDirectEvent("ReactionRemoved", (arg1, arg2, arg3));
    }

    private Task ClientOnReactionsCleared(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        return ProcessDirectEvent("ReactionsCleared", (arg1, arg2));
    }

    private Task ClientOnInteractionCreated(SocketInteraction arg)
    {
        return ProcessDirectEvent("InteractionCreated", arg);
    }

    private Task ClientOnJoinedGuild(SocketGuild arg)
    {
        return ProcessDirectEvent("JoinedGuild", arg);
    }

    private Task ClientOnEventCreated(SocketGuildEvent arg)
    {
        return ProcessDirectEvent("EventCreated", arg);
    }

    private Task ClientOnRoleUpdated(SocketRole arg1, SocketRole arg2)
    {
        return ProcessDirectEvent("RoleUpdated", (arg1, arg2));
    }

    private Task ClientOnGuildUpdated(SocketGuild arg1, SocketGuild arg2)
    {
        return ProcessDirectEvent("GuildUpdated", (arg1, arg2));
    }

    private Task ClientOnRoleCreated(SocketRole arg)
    {
        return ProcessDirectEvent("RoleCreated", arg);
    }

    private Task ClientOnThreadCreated(SocketThreadChannel arg)
    {
        return ProcessDirectEvent("ThreadCreated", arg);
    }

    private Task ClientOnThreadUpdated(Cacheable<SocketThreadChannel, ulong> arg1, SocketThreadChannel arg2)
    {
        return ProcessDirectEvent("ThreadUpdated", (arg1, arg2));
    }

    private Task ClientOnThreadDeleted(Cacheable<SocketThreadChannel, ulong> arg)
    {
        return ProcessDirectEvent("ThreadDeleted", arg);
    }

    private Task ClientOnThreadMemberJoined(SocketThreadUser arg)
    {
        return ProcessDirectEvent("ThreadMemberJoined", arg);
    }

    private Task ClientOnThreadMemberLeft(SocketThreadUser arg)
    {
        return ProcessDirectEvent("ThreadMemberLeft", arg);
    }

    private Task ClientOnAuditLogCreated(SocketAuditLogEntry arg1, SocketGuild arg2)
    {
        return ProcessDirectEvent("AuditLogCreated", (arg1, arg2));
    }

    private Task ClientOnGuildAvailable(SocketGuild arg)
    {
        return ProcessDirectEvent("GuildAvailable", arg);
    }

    private Task ClientOnGuildUnavailable(SocketGuild arg)
    {
        return ProcessDirectEvent("GuildUnavailable", arg);
    }

    private Task ClientOnLeftGuild(SocketGuild arg)
    {
        return ProcessDirectEvent("LeftGuild", arg);
    }

    private Task ClientOnInviteCreated(SocketInvite arg)
    {
        return ProcessDirectEvent("InviteCreated", arg);
    }

    private Task ClientOnInviteDeleted(SocketGuildChannel arg1, string arg2)
    {
        return ProcessDirectEvent("InviteDeleted", (arg1, arg2));
    }

    private Task ClientOnModalSubmitted(SocketModal arg)
    {
        return ProcessDirectEvent("ModalSubmitted", arg);
    }

    private Task ProcessDirectEvent<T>(string eventType, T args)
    {
        logger.LogDebug("ProcessDirectEvent called for {EventType}, disposed: {Disposed}", eventType, disposed);

        if (disposed)
        {
            logger.LogWarning("EventHandler is disposed, skipping event {EventType}", eventType);
            return Task.CompletedTask;
        }

        if (!stringEventSubscriptions.TryGetValue(eventType, out var handlers))
        {
            logger.LogDebug("No subscriptions found for event type {EventType}. Available event types: {EventTypes}",
                eventType, string.Join(", ", stringEventSubscriptions.Keys));
            return Task.CompletedTask;
        }

        logger.LogDebug("Found {SubscriptionCount} string-based subscriptions for event {EventType}", handlers.Count,
            eventType);
        _ = Task.Run(async () =>
        {
            foreach (var (moduleName, handler) in handlers)
            {
                try
                {
                    await ExecuteHandlerByEventType(eventType, handler, args);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error executing subscription for {EventType} in module {ModuleName}",
                        eventType,
                        moduleName);
                }
            }
        });
        return Task.CompletedTask;
    }

    #endregion

    #region Utility Methods

    private ulong? ExtractGuildId<T>(T args)
    {
        return args switch
        {
            SocketGuildUser guildUser => guildUser.Guild.Id,
            SocketGuild guild => guild.Id,
            SocketGuildChannel guildChannel => guildChannel.Guild.Id,
            SocketMessage { Channel: SocketGuildChannel channel } => channel.Guild.Id,
            _ => null
        };
    }

    private ulong? ExtractChannelId<T>(T args)
    {
        return args switch
        {
            SocketMessage message => message.Channel.Id,
            SocketGuildChannel channel => channel.Id,
            _ => null
        };
    }

    private ulong? ExtractUserId<T>(T args)
    {
        return args switch
        {
            SocketGuildUser guildUser => guildUser.Id,
            SocketUser user => user.Id,
            SocketMessage message => message.Author.Id,
            _ => null
        };
    }

    private EventMetrics GetOrCreateEventMetrics(string eventType)
    {
        return eventMetrics.GetOrAdd(eventType, _ => new EventMetrics());
    }

    private ModuleMetrics GetOrCreateModuleMetrics(string moduleName)
    {
        return moduleMetrics.GetOrAdd(moduleName, _ => new ModuleMetrics());
    }

    private void ResetMetrics(object? state)
    {
        foreach (var metric in eventMetrics.Values)
        {
            Interlocked.Exchange(ref metric.TotalProcessed, 0);
            Interlocked.Exchange(ref metric.TotalErrors, 0);
            Interlocked.Exchange(ref metric.TotalExecutionTime, 0);
        }

        foreach (var metric in moduleMetrics.Values)
        {
            Interlocked.Exchange(ref metric.EventsProcessed, 0);
            Interlocked.Exchange(ref metric.Errors, 0);
            Interlocked.Exchange(ref metric.TotalExecutionTime, 0);
        }

        logger.LogWarning("Event and module metrics reset");
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        try
        {
            // Stop timers
            metricsResetTimer.Dispose();

            // Dispose processors
            messageProcessor.Dispose();
            presenceProcessor.Dispose();
            typingProcessor.Dispose();

            // Unregister Discord events
            UnregisterEvents();

            // Clean up string-based subscriptions
            foreach (var kvp in stringEventSubscriptions)
            {
                var eventName = kvp.Key;
                var handlers = kvp.Value;
                foreach (var (moduleName, handler) in handlers)
                {
                    try
                    {
                        Unsubscribe(eventName, moduleName, handler);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error unsubscribing {ModuleName} from {EventName}", moduleName, eventName);
                    }
                }
            }

            stringEventSubscriptions.Clear();

            logger.LogInformation("EventHandler disposed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while disposing EventHandler");
        }
    }

    private void UnregisterEvents()
    {
        client.MessageReceived -= ClientOnMessageReceived;
        client.UserJoined -= ClientOnUserJoined;
        client.UserLeft -= ClientOnUserLeft;
        client.MessageDeleted -= ClientOnMessageDeleted;
        client.GuildMemberUpdated -= ClientOnGuildMemberUpdated;
        client.MessageUpdated -= ClientOnMessageUpdated;
        client.MessagesBulkDeleted -= ClientOnMessagesBulkDeleted;
        client.UserBanned -= ClientOnUserBanned;
        client.UserUnbanned -= ClientOnUserUnbanned;
        client.UserVoiceStateUpdated -= ClientOnUserVoiceStateUpdated;
        client.UserUpdated -= ClientOnUserUpdated;
        client.ChannelCreated -= ClientOnChannelCreated;
        client.ChannelDestroyed -= ClientOnChannelDestroyed;
        client.ChannelUpdated -= ClientOnChannelUpdated;
        client.RoleDeleted -= ClientOnRoleDeleted;
        client.ReactionAdded -= ClientOnReactionAdded;
        client.ReactionRemoved -= ClientOnReactionRemoved;
        client.ReactionsCleared -= ClientOnReactionsCleared;
        client.InteractionCreated -= ClientOnInteractionCreated;
        client.UserIsTyping -= ClientOnUserIsTyping;
        client.PresenceUpdated -= ClientOnPresenceUpdated;
        client.JoinedGuild -= ClientOnJoinedGuild;
        client.GuildScheduledEventCreated -= ClientOnEventCreated;
        client.RoleUpdated -= ClientOnRoleUpdated;
        client.GuildUpdated -= ClientOnGuildUpdated;
        client.RoleCreated -= ClientOnRoleCreated;
        client.ThreadCreated -= ClientOnThreadCreated;
        client.ThreadUpdated -= ClientOnThreadUpdated;
        client.ThreadDeleted -= ClientOnThreadDeleted;
        client.ThreadMemberJoined -= ClientOnThreadMemberJoined;
        client.ThreadMemberLeft -= ClientOnThreadMemberLeft;
        client.AuditLogCreated -= ClientOnAuditLogCreated;
        client.GuildAvailable -= ClientOnGuildAvailable;
        client.GuildUnavailable -= ClientOnGuildUnavailable;
        client.LeftGuild -= ClientOnLeftGuild;
        client.InviteCreated -= ClientOnInviteCreated;
        client.InviteDeleted -= ClientOnInviteDeleted;
        client.ModalSubmitted -= ClientOnModalSubmitted;
    }

    #endregion

    #region Delegates

    /// <summary>
    ///     Represents an asynchronous event handler with a single parameter.
    /// </summary>
    /// <typeparam name="T">The type of the event argument.</typeparam>
    /// <param name="args">The event data.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public delegate Task AsyncEventHandler<in T>(T args);

    /// <summary>
    ///     Represents an asynchronous event handler with two parameters.
    /// </summary>
    /// <typeparam name="T1">The type of the first event argument.</typeparam>
    /// <typeparam name="T2">The type of the second event argument.</typeparam>
    /// <param name="args1">The first event argument.</param>
    /// <param name="args2">The second event argument.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public delegate Task AsyncEventHandler<in T1, in T2>(T1 args1, T2 args2);

    /// <summary>
    ///     Represents an asynchronous event handler with three parameters.
    /// </summary>
    /// <typeparam name="T1">The type of the first event argument.</typeparam>
    /// <typeparam name="T2">The type of the second event argument.</typeparam>
    /// <typeparam name="T3">The type of the third event argument.</typeparam>
    /// <param name="args1">The first event argument.</param>
    /// <param name="args2">The second event argument.</param>
    /// <param name="args3">The third event argument.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public delegate Task AsyncEventHandler<in T1, in T2, in T3>(T1 args1, T2 args2, T3 args3);

    /// <summary>
    ///     Represents an asynchronous event handler with four parameters.
    /// </summary>
    /// <typeparam name="T1">The type of the first event argument.</typeparam>
    /// <typeparam name="T2">The type of the second event argument.</typeparam>
    /// <typeparam name="T3">The type of the third event argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth event argument.</typeparam>
    /// <param name="args1">The first event argument.</param>
    /// <param name="args2">The second event argument.</param>
    /// <param name="args3">The third event argument.</param>
    /// <param name="args4">The fourth event argument.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public delegate Task AsyncEventHandler<in T1, in T2, in T3, in T4>(T1 args1, T2 args2, T3 args3, T4 args4);

    #endregion
}

#region Supporting Classes

/// <summary>
///     Defines a contract for filtering events based on guild, channel, and user criteria.
/// </summary>
public interface IEventFilter
{
    /// <summary>
    ///     Determines whether an event should be processed based on the provided identifiers.
    /// </summary>
    /// <param name="guildId">The guild ID associated with the event, if any.</param>
    /// <param name="channelId">The channel ID associated with the event, if any.</param>
    /// <param name="userId">The user ID associated with the event, if any.</param>
    /// <returns>True if the event should be processed, false otherwise.</returns>
    public bool ShouldProcess(ulong? guildId, ulong? channelId, ulong? userId);
}

/// <summary>
///     Base interface for event subscriptions.
/// </summary>
public interface IEventSubscription
{
    /// <summary>
    ///     Gets the priority of this subscription.
    /// </summary>
    public int Priority { get; }

    /// <summary>
    ///     Gets the name of the module that owns this subscription.
    /// </summary>
    public string ModuleName { get; }
}

/// <summary>
///     Represents a subscription to an event with filtering and priority support.
/// </summary>
/// <typeparam name="T">The event argument type.</typeparam>
public class EventSubscription<T> : IEventSubscription
{
    /// <summary>
    ///     Gets or sets the event handler delegate.
    /// </summary>
    public EventHandler.AsyncEventHandler<T> Handler { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the optional filter for this subscription.
    /// </summary>
    public IEventFilter? Filter { get; set; }

    /// <summary>
    ///     Gets or sets the priority of this subscription (higher values execute first).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    ///     Gets or sets the name of the module that owns this subscription.
    /// </summary>
    public string ModuleName { get; set; } = string.Empty;
}

/// <summary>
///     Implements a circuit breaker pattern for event handlers.
/// </summary>
public class CircuitBreaker
{
    private readonly int threshold;
    private readonly TimeSpan timeout;
    private int failureCount;
    private DateTime lastFailureTime;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CircuitBreaker" /> class.
    /// </summary>
    /// <param name="threshold">The failure threshold before opening the circuit.</param>
    /// <param name="timeout">The timeout before attempting to close the circuit.</param>
    public CircuitBreaker(int threshold, TimeSpan timeout)
    {
        this.threshold = threshold;
        this.timeout = timeout;
    }

    /// <summary>
    ///     Gets a value indicating whether the circuit breaker is open (blocking requests).
    /// </summary>
    public bool IsOpen
    {
        get
        {
            return failureCount >= threshold && DateTime.UtcNow - lastFailureTime < timeout;
        }
    }

    /// <summary>
    ///     Records a failure and potentially opens the circuit.
    /// </summary>
    public void RecordFailure()
    {
        Interlocked.Increment(ref failureCount);
        lastFailureTime = DateTime.UtcNow;
    }

    /// <summary>
    ///     Records a success and potentially resets the failure count.
    /// </summary>
    public void RecordSuccess()
    {
        Interlocked.Exchange(ref failureCount, 0);
    }
}

/// <summary>
///     Manages weak references to event handlers to prevent memory leaks.
/// </summary>
public class WeakEventManager
{
    private readonly List<WeakReference> handlers = new();
    private readonly object @lock = new();

    /// <summary>
    ///     Subscribes a handler using a weak reference.
    /// </summary>
    /// <typeparam name="T">The event argument type.</typeparam>
    /// <param name="handler">The handler to subscribe.</param>
    public void Subscribe<T>(EventHandler.AsyncEventHandler<T> handler)
    {
        lock (@lock)
        {
            handlers.Add(new WeakReference(handler));
        }
    }

    /// <summary>
    ///     Cleans up dead weak references.
    /// </summary>
    public void Cleanup()
    {
        lock (@lock)
        {
            for (var i = handlers.Count - 1; i >= 0; i--)
            {
                if (!handlers[i].IsAlive)
                {
                    handlers.RemoveAt(i);
                }
            }
        }
    }
}

/// <summary>
///     Implements rate limiting using a token bucket algorithm.
/// </summary>
public class RateLimiter
{
    private readonly int maxTokens;
    private readonly TimeSpan refillInterval;
    private DateTime lastRefill;
    private int tokens;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RateLimiter" /> class.
    /// </summary>
    /// <param name="maxTokens">The maximum number of tokens in the bucket.</param>
    /// <param name="refillInterval">The interval at which to refill tokens.</param>
    public RateLimiter(int maxTokens, TimeSpan refillInterval)
    {
        this.maxTokens = maxTokens;
        this.refillInterval = refillInterval;
        tokens = maxTokens;
        lastRefill = DateTime.UtcNow;
    }

    /// <summary>
    ///     Attempts to acquire a token from the rate limiter.
    /// </summary>
    /// <returns>True if a token was acquired, false otherwise.</returns>
    public bool TryAcquire()
    {
        RefillTokens();

        if (tokens > 0)
        {
            Interlocked.Decrement(ref tokens);
            return true;
        }

        return false;
    }

    private void RefillTokens()
    {
        var now = DateTime.UtcNow;
        var timeSinceRefill = now - lastRefill;

        if (timeSinceRefill >= refillInterval)
        {
            Interlocked.Exchange(ref tokens, maxTokens);
            lastRefill = now;
        }
    }
}

/// <summary>
///     Processes events in batches to improve performance.
/// </summary>
/// <typeparam name="T">The event argument type.</typeparam>
public class BatchedEventProcessor<T> : IDisposable
{
    private readonly object batchLock = new();
    private readonly Timer batchTimer;
    private readonly Channel<T> channel;
    private readonly List<T> currentBatch = new();
    private readonly int maxBatchSize;
    private volatile bool disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BatchedEventProcessor{T}" /> class.
    /// </summary>
    /// <param name="batchInterval">The interval at which to process batches.</param>
    /// <param name="maxBatchSize">The maximum number of items in a batch.</param>
    /// <param name="maxQueueSize">The maximum queue size.</param>
    public BatchedEventProcessor(TimeSpan batchInterval, int maxBatchSize, int maxQueueSize)
    {
        this.maxBatchSize = maxBatchSize;

        var options = new BoundedChannelOptions(maxQueueSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false
        };

        channel = Channel.CreateBounded<T>(options);
        batchTimer = new Timer(ProcessBatch, null, batchInterval, batchInterval);

        // Start background processor
        _ = Task.Run(ProcessChannelAsync);
    }

    /// <summary>
    ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        channel.Writer.Complete();
        batchTimer.Dispose();

        // Process any remaining items
        ProcessBatch(null);
    }

    /// <summary>
    ///     Occurs when a batch is ready for processing.
    /// </summary>
    public event Func<IReadOnlyList<T>, Task>? BatchReady;

    /// <summary>
    ///     Enqueues an item for batch processing.
    /// </summary>
    /// <param name="item">The item to enqueue.</param>
    public void Enqueue(T item)
    {
        if (!disposed)
        {
            channel.Writer.TryWrite(item);
        }
    }

    private async Task ProcessChannelAsync()
    {
        await foreach (var item in channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (disposed)
                break;

            lock (batchLock)
            {
                currentBatch.Add(item);

                if (currentBatch.Count >= maxBatchSize)
                {
                    ProcessBatch(null);
                }
            }
        }
    }

    private void ProcessBatch(object? state)
    {
        List<T>? batchToProcess = null;

        lock (batchLock)
        {
            if (currentBatch.Count > 0)
            {
                batchToProcess = new List<T>(currentBatch);
                currentBatch.Clear();
            }
        }

        if (batchToProcess?.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (BatchReady != null)
                        await BatchReady(batchToProcess).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing batch in {ProcessorType}", typeof(T).Name);
                }
            });
        }
    }
}

/// <summary>
///     Contains metrics for event processing.
/// </summary>
public class EventMetrics
{
    /// <summary>
    ///     Gets the total number of errors encountered.
    /// </summary>
    public long TotalErrors;

    /// <summary>
    ///     Gets the total execution time in milliseconds.
    /// </summary>
    public long TotalExecutionTime;

    /// <summary>
    ///     Gets the total number of events processed.
    /// </summary>
    public long TotalProcessed;

    /// <summary>
    ///     Gets the average execution time per event in milliseconds.
    /// </summary>
    public double AverageExecutionTime
    {
        get
        {
            return TotalProcessed > 0 ? (double)TotalExecutionTime / TotalProcessed : 0;
        }
    }

    /// <summary>
    ///     Gets the error rate as a percentage.
    /// </summary>
    public double ErrorRate
    {
        get
        {
            return TotalProcessed > 0 ? (double)TotalErrors / TotalProcessed * 100 : 0;
        }
    }
}

/// <summary>
///     Contains metrics for module performance.
/// </summary>
public class ModuleMetrics
{
    /// <summary>
    ///     Gets the total number of errors in this module.
    /// </summary>
    public long Errors;

    /// <summary>
    ///     Gets the total number of events processed by this module.
    /// </summary>
    public long EventsProcessed;

    /// <summary>
    ///     Gets the total execution time for this module in milliseconds.
    /// </summary>
    public long TotalExecutionTime;

    /// <summary>
    ///     Gets the average execution time per event for this module in milliseconds.
    /// </summary>
    public double AverageExecutionTime
    {
        get
        {
            return EventsProcessed > 0 ? (double)TotalExecutionTime / EventsProcessed : 0;
        }
    }

    /// <summary>
    ///     Gets the error rate for this module as a percentage.
    /// </summary>
    public double ErrorRate
    {
        get
        {
            return EventsProcessed > 0 ? (double)Errors / EventsProcessed * 100 : 0;
        }
    }
}

#endregion