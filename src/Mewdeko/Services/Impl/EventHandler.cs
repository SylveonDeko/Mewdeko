namespace Mewdeko.Services.Impl;

/// <summary>
/// Used to combat discord.nets stupid non async event handlers.
/// </summary>
public class EventHandler
{
    #region Delegates

    /// <summary>
    /// Represents an asynchronous event handler.
    /// </summary>
    /// <typeparam name="TEventArgs">The type of the event arguments.</typeparam>
    public delegate Task AsyncEventHandler<in TEventArgs>(TEventArgs args);

    /// <summary>
    /// Represents an asynchronous event handler with two arguments.
    /// </summary>
    /// <typeparam name="TEventArgs">The type of the event arguments.</typeparam>
    /// <typeparam name="TArgs">The type of the second event arguments.</typeparam>
    public delegate Task AsyncEventHandler<in TEventArgs, in TArgs>(TEventArgs args, TArgs arsg2);

    /// <summary>
    /// Represents an asynchronous event handler with three arguments.
    /// </summary>
    /// <typeparam name="TEventArgs">The type of the event arguments.</typeparam>
    /// <typeparam name="TArgs">The type of the second event arguments.</typeparam>
    /// <typeparam name="TEvent">The type of the third event arguments.</typeparam>
    public delegate Task AsyncEventHandler<in TEventArgs, in TArgs, in TEvent>(TEventArgs args, TArgs args2,
        TEvent args3);

    /// <summary>
    /// Represents an asynchronous event handler with four arguments.
    /// </summary>
    /// <typeparam name="TEventArgs">The type of the event arguments.</typeparam>
    /// <typeparam name="TArgs">The type of the second event arguments.</typeparam>
    /// <typeparam name="TEvent">The type of the third event arguments.</typeparam>
    /// <typeparam name="TArgs2">The type of the fourth event arguments.</typeparam>
    public delegate Task AsyncEventHandler<in TEventArgs, in TArgs, in TEvent, in TArgs2>(TEventArgs args, TArgs args2,
        TEvent args3, TArgs2 args4);

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a message is received.
    /// </summary>
    public event AsyncEventHandler<SocketMessage>? MessageReceived;

    /// <summary>
    /// Occurs when a guild event is created.
    /// </summary>
    public event AsyncEventHandler<SocketGuildEvent>? EventCreated;

    /// <summary>
    /// Occurs when a role is created.
    /// </summary>
    public event AsyncEventHandler<SocketRole>? RoleCreated;

    /// <summary>
    /// Occurs when a guild is updated.
    /// </summary>
    public event AsyncEventHandler<SocketGuild, SocketGuild>? GuildUpdated;

    /// <summary>
    /// Occurs when a user joins a guild.
    /// </summary>
    public event AsyncEventHandler<IGuildUser>? UserJoined;

    /// <summary>
    /// Occurs when a role is updated.
    /// </summary>
    public event AsyncEventHandler<SocketRole, SocketRole>? RoleUpdated;

    /// <summary>
    /// Occurs when a user leaves a guild.
    /// </summary>
    public event AsyncEventHandler<IGuild, IUser>? UserLeft;

    /// <summary>
    /// Occurs when a message is deleted.
    /// </summary>
    public event AsyncEventHandler<Cacheable<IMessage, ulong>, Cacheable<IMessageChannel, ulong>>? MessageDeleted;

    /// <summary>
    /// Occurs when a guild member is updated.
    /// </summary>
    public event AsyncEventHandler<Cacheable<SocketGuildUser, ulong>, SocketGuildUser>? GuildMemberUpdated;

    /// <summary>
    /// Occurs when a message is updated.
    /// </summary>
    public event AsyncEventHandler<Cacheable<IMessage, ulong>, SocketMessage, ISocketMessageChannel>? MessageUpdated;

    /// <summary>
    /// Occurs when a collection of messages are bulk deleted.
    /// </summary>
    public event AsyncEventHandler<IReadOnlyCollection<Cacheable<IMessage, ulong>>, Cacheable<IMessageChannel, ulong>>?
        MessagesBulkDeleted;

    /// <summary>
    /// Occurs when a user is banned.
    /// </summary>
    public event AsyncEventHandler<SocketUser, SocketGuild>? UserBanned;

    /// <summary>
    /// Occurs when a user is unbanned.
    /// </summary>
    public event AsyncEventHandler<SocketUser, SocketGuild>? UserUnbanned;

    /// <summary>
    /// Occurs when a user's voice state is updated.
    /// </summary>
    public event AsyncEventHandler<SocketUser, SocketUser>? UserUpdated;

    /// <summary>
    /// Occurs when a user's voice state is updated.
    /// </summary>
    public event AsyncEventHandler<SocketUser, SocketVoiceState, SocketVoiceState>? UserVoiceStateUpdated;

    /// <summary>
    /// Occurs when a channel is created.
    /// </summary>
    public event AsyncEventHandler<SocketChannel>? ChannelCreated;

    /// <summary>
    /// Occurs when a channel is destroyed.
    /// </summary>
    public event AsyncEventHandler<SocketChannel>? ChannelDestroyed;

    /// <summary>
    /// \Occurs when a channel is updated.
    /// </summary>
    public event AsyncEventHandler<SocketChannel, SocketChannel>? ChannelUpdated;

    /// <summary>
    /// Occurs when a role is deleted.
    /// </summary>
    public event AsyncEventHandler<SocketRole>? RoleDeleted;

    /// <summary>
    /// Occurs when a reaction is added to a message.
    /// </summary>
    public event AsyncEventHandler<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>, SocketReaction>?
        ReactionAdded;

    /// <summary>
    /// Occurs when a reaction is removed from a message.
    /// </summary>
    public event AsyncEventHandler<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>, SocketReaction>?
        ReactionRemoved;

    /// <summary>
    /// Occurs when reactions are cleared from a message.
    /// </summary>
    public event AsyncEventHandler<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>>? ReactionsCleared;

    /// <summary>
    /// Occurs when an interaction is created.
    /// </summary>
    public event AsyncEventHandler<SocketInteraction>? InteractionCreated;

    /// <summary>
    /// Occurs when a user starts typing.
    /// </summary>
    public event AsyncEventHandler<Cacheable<IUser, ulong>, Cacheable<IMessageChannel, ulong>>? UserIsTyping;

    /// <summary>
    /// Occurs when a users presence is updated.
    /// </summary>
    public event AsyncEventHandler<SocketUser, SocketPresence, SocketPresence>? PresenceUpdated;

    /// <summary>
    /// Occurs when the bot joins a guild.
    /// </summary>
    public event AsyncEventHandler<IGuild>? JoinedGuild;

    /// <summary>
    /// Occurs when a thread is created.
    /// </summary>
    public event AsyncEventHandler<SocketThreadChannel>? ThreadCreated;

    /// <summary>
    /// Occurs when a thread is updated.
    /// </summary>
    public event AsyncEventHandler<Cacheable<SocketThreadChannel, ulong>, SocketThreadChannel>? ThreadUpdated;

    /// <summary>
    /// Occurs when a thread is deleted.
    /// </summary>
    public event AsyncEventHandler<Cacheable<SocketThreadChannel, ulong>>? ThreadDeleted;

    /// <summary>
    /// Occurs when a user joins a thread.
    /// </summary>
    public event AsyncEventHandler<SocketThreadUser>? ThreadMemberJoined;

    /// <summary>
    /// Occurs when a user leaves a thread.
    /// </summary>
    public event AsyncEventHandler<SocketThreadUser>? ThreadMemberLeft;

    /// <summary>
    /// Occurs when an audit log event is created.
    /// </summary>
    public event AsyncEventHandler<SocketAuditLogEntry, SocketGuild>? AuditLogCreated;

    /// <summary>
    /// Occurs when the client is ready.
    /// </summary>
    public event AsyncEventHandler<DiscordShardedClient>? Ready;

    /// <summary>
    /// Occurs when a guild is available
    /// </summary>
    public event AsyncEventHandler<SocketGuild>? GuildAvailable;

    /// <summary>
    /// Occurs when the bot leaves a server.
    /// </summary>
    public event AsyncEventHandler<SocketGuild>? LeftGuild;

    #endregion

    private readonly DiscordShardedClient client;


    /// <summary>
    /// Initializes a new instance of the <see cref="EventHandler"/> class. Used to combat discord.nets stupid non async event handlers.
    /// </summary>
    /// <param name="client">The discord client.</param>
    public EventHandler(DiscordShardedClient client)
    {
        this.client = client;
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
        client.LeftGuild += ClientOnLeftGuild;
    }

    private Task ClientOnLeftGuild(SocketGuild arg)
    {
        if (LeftGuild is not null)
            _ = LeftGuild(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnGuildAvailable(SocketGuild arg)
    {
        if (GuildAvailable is not null)
            _ = GuildAvailable(arg);
        return Task.CompletedTask;
    }

    #region Event Handlers

    private Task ClientOnAuditLogCreated(SocketAuditLogEntry arg1, SocketGuild arg2)
    {
        if (AuditLogCreated is not null)
            _ = AuditLogCreated(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnThreadMemberLeft(SocketThreadUser arg)
    {
        if (ThreadMemberLeft is not null)
            _ = ThreadMemberLeft(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnThreadMemberJoined(SocketThreadUser arg)
    {
        if (ThreadMemberJoined is not null)
            _ = ThreadMemberJoined(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnThreadDeleted(Cacheable<SocketThreadChannel, ulong> arg)
    {
        if (ThreadDeleted is not null)
            _ = ThreadDeleted(arg);
        return Task.CompletedTask;
    }


    private Task ClientOnThreadUpdated(Cacheable<SocketThreadChannel, ulong> arg1, SocketThreadChannel arg2)
    {
        if (ThreadUpdated is not null)
            _ = ThreadUpdated(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnThreadCreated(SocketThreadChannel arg)
    {
        if (ThreadCreated is not null)
            _ = ThreadCreated(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnJoinedGuild(SocketGuild arg)
    {
        if (JoinedGuild is not null)
            _ = JoinedGuild(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnPresenceUpdated(SocketUser arg1, SocketPresence arg2, SocketPresence arg3)
    {
        _ = PresenceUpdated?.Invoke(arg1, arg2, arg3);
        return Task.CompletedTask;
    }

    private Task ClientOnUserIsTyping(Cacheable<IUser, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        if (UserIsTyping is not null)
            _ = UserIsTyping(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnInteractionCreated(SocketInteraction arg)
    {
        if (InteractionCreated is not null)
            _ = InteractionCreated(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnReactionsCleared(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        if (ReactionsCleared is not null)
            _ = ReactionsCleared(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnReactionRemoved(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2,
        SocketReaction arg3)
    {
        if (ReactionRemoved is not null)
            _ = ReactionRemoved(arg1, arg2, arg3);
        return Task.CompletedTask;
    }

    private Task ClientOnReactionAdded(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2,
        SocketReaction arg3)
    {
        if (ReactionAdded is not null)
            _ = ReactionAdded(arg1, arg2, arg3);
        return Task.CompletedTask;
    }

    private Task ClientOnRoleDeleted(SocketRole arg)
    {
        if (RoleDeleted is not null)
            _ = RoleDeleted(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnChannelUpdated(SocketChannel arg1, SocketChannel arg2)
    {
        if (ChannelUpdated is not null)
            _ = ChannelUpdated(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnChannelDestroyed(SocketChannel arg)
    {
        if (ChannelDestroyed is not null)
            _ = ChannelDestroyed(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnChannelCreated(SocketChannel arg)
    {
        if (ChannelCreated is not null)
            _ = ChannelCreated(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnUserUpdated(SocketUser arg1, SocketUser arg2)
    {
        if (UserUpdated is not null)
            _ = UserUpdated(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnUserVoiceStateUpdated(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
    {
        if (UserVoiceStateUpdated is not null)
            _ = UserVoiceStateUpdated(arg1, arg2, arg3);
        return Task.CompletedTask;
    }

    private Task ClientOnUserUnbanned(SocketUser arg1, SocketGuild arg2)
    {
        if (UserUnbanned is not null)
            _ = UserUnbanned(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnUserBanned(SocketUser arg1, SocketGuild arg2)
    {
        if (UserBanned is not null)
            _ = UserBanned(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnMessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> arg1,
        Cacheable<IMessageChannel, ulong> arg2)
    {
        if (MessagesBulkDeleted is not null)
            _ = MessagesBulkDeleted(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnMessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
    {
        if (MessageUpdated is not null)
            _ = MessageUpdated(arg1, arg2, arg3);
        return Task.CompletedTask;
    }

    private Task ClientOnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> arg1, SocketGuildUser arg2)
    {
        if (GuildMemberUpdated is not null)
            _ = GuildMemberUpdated(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnMessageDeleted(Cacheable<IMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        if (MessageDeleted is not null)
            _ = MessageDeleted(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnUserLeft(SocketGuild arg1, SocketUser arg2)
    {
        if (UserLeft is not null)
            _ = UserLeft(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnUserJoined(SocketGuildUser arg)
    {
        if (UserJoined is not null)
            _ = UserJoined(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnMessageReceived(SocketMessage arg)
    {
        if (MessageReceived is not null)
            _ = MessageReceived(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnEventCreated(SocketGuildEvent args)
    {
        if (EventCreated is not null)
            _ = EventCreated(args);
        return Task.CompletedTask;
    }

    private Task ClientOnRoleUpdated(SocketRole arg1, SocketRole arg2)
    {
        if (RoleUpdated is not null)
            _ = RoleUpdated(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnGuildUpdated(SocketGuild arg1, SocketGuild arg2)
    {
        if (GuildUpdated is not null)
            _ = GuildUpdated(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnRoleCreated(SocketRole args)
    {
        if (RoleCreated is not null)
            _ = RoleCreated(args);
        return Task.CompletedTask;
    }

    #endregion
}