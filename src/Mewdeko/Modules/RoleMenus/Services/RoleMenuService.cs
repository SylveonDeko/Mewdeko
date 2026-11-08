using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using DataModel;
using Discord.Net;
using Humanizer;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.RoleMenus.Common;
using Mewdeko.Services.Analytics;
using Mewdeko.Services.Strings;
using PermValue = Discord.PermValue;

namespace Mewdeko.Modules.RoleMenus.Services;

/// <summary>
///     Stores role menus, posts and edits their messages, and gives and takes roles when members use them.
/// </summary>
public class RoleMenuService : INService, IReadyExecutor, IUnloadableService
{
    /// <summary>
    ///     Most options one menu can have: one dropdown of 25, or 25 buttons in 5 rows.
    /// </summary>
    public const int MaxOptions = 25;

    /// <summary>
    ///     Buttons per row on a buttons menu.
    /// </summary>
    public const int ButtonsPerRow = 5;

    /// <summary>
    ///     The most roles a reaction menu can offer, since a message holds 20 different reactions.
    /// </summary>
    public const int MaxReactionOptions = 20;

    /// <summary>
    ///     Longest menu name.
    /// </summary>
    public const int NameLength = 100;

    /// <summary>
    ///     Longest option label.
    /// </summary>
    public const int LabelLength = 80;

    /// <summary>
    ///     Longest option description.
    /// </summary>
    public const int DescriptionLength = 100;

    /// <summary>
    ///     Longest dropdown placeholder.
    /// </summary>
    public const int PlaceholderLength = 150;

    /// <summary>
    ///     Longest plain text message.
    /// </summary>
    public const int PlainMessageLength = 2000;

    /// <summary>
    ///     Longest raw message source, including embed builder JSON.
    /// </summary>
    public const int MessageSourceLength = 12000;

    /// <summary>
    ///     Longest temporary role duration, in seconds.
    /// </summary>
    public const int MaxTempSeconds = 365 * 24 * 60 * 60;

    /// <summary>
    ///     Most roles a menu can block.
    /// </summary>
    public const int MaxBlockedRoles = 25;

    /// <summary>
    ///     Role problem text for roles an integration manages.
    /// </summary>
    public const string ProblemManaged = "Managed by an integration";

    /// <summary>
    ///     Role problem text for roles at or above the bot's highest role.
    /// </summary>
    public const string ProblemAboveBot = "Above the bot's highest role";

    /// <summary>
    ///     Role problem text for roles at or above the editor's highest role.
    /// </summary>
    public const string ProblemAboveActor = "Above your highest role";

    /// <summary>
    ///     Role problem text for when the bot lacks Manage Roles.
    /// </summary>
    public const string ProblemBotMissingManageRoles = "The bot is missing Manage Roles";

    /// <summary>
    ///     Option problem text for roles that were deleted.
    /// </summary>
    public const string ProblemRoleDeleted = "This role was deleted";

    /// <summary>
    ///     Channel problem text for channels the bot can't see.
    /// </summary>
    public const string ProblemCantSee = "The bot can't see this channel";

    /// <summary>
    ///     Channel problem text for channels the bot can't send messages in.
    /// </summary>
    public const string ProblemCantSend = "The bot can't send messages here";

    /// <summary>
    ///     Channel problem text for channels the bot can't embed links in.
    /// </summary>
    public const string ProblemCantEmbed = "The bot can't embed links here";

    /// <summary>
    ///     Channel problem text for channels the bot can't read history in.
    /// </summary>
    public const string ProblemCantReadHistory = "The bot can't read message history here";

    /// <summary>
    ///     Analytics feature key.
    /// </summary>
    private const string FeatureKey = "role_menus";

    /// <summary>
    ///     Name this service subscribes to events under.
    /// </summary>
    private const string EventModuleName = "RoleMenuService";

    /// <summary>
    ///     Number of member lock stripes.
    /// </summary>
    private const int MemberLockStripes = 64;

    /// <summary>
    ///     How often expired roles and menus are checked.
    /// </summary>
    private static readonly TimeSpan ExpiryInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Stops the expiry loop when the service unloads.
    /// </summary>
    private readonly CancellationTokenSource expiryCts = new();

    /// <summary>
    ///     How long the bot trusts its own record of a member's role change over the gateway cache.
    /// </summary>
    private static readonly TimeSpan RecentChangeWindow = TimeSpan.FromSeconds(15);

    /// <summary>
    ///     Serializer settings for copied messages: omit nulls.
    /// </summary>
    private static readonly JsonSerializerOptions CopyJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    ///     Handler for the ChannelDestroyed event, kept so it can be unsubscribed.
    /// </summary>
    private readonly Func<SocketChannel, Task> channelDestroyedHandler;

    /// <summary>
    ///     The Discord client.
    /// </summary>
    private readonly DiscordShardedClient client;

    /// <summary>
    ///     Analytics collector.
    /// </summary>
    private readonly IAnalyticsCollector collector;

    /// <summary>
    ///     Database connection factory.
    /// </summary>
    private readonly IDataConnectionFactory dbFactory;

    /// <summary>
    ///     Event handler used for message, channel, and role cleanup.
    /// </summary>
    private readonly EventHandler eventHandler;

    /// <summary>
    ///     Guild settings, used for prefixes.
    /// </summary>
    private readonly GuildSettingsService guildSettings;

    /// <summary>
    ///     Logger instance.
    /// </summary>
    private readonly ILogger<RoleMenuService> logger;

    /// <summary>
    ///     Decides which guilds this instance handles.
    /// </summary>
    private readonly GuildOwnershipService ownership;

    /// <summary>
    ///     Stripes that serialize role changes for one member.
    /// </summary>
    private readonly SemaphoreSlim[] memberLocks;

    /// <summary>
    ///     Every menu with its options sorted, by menu ID.
    /// </summary>
    private readonly ConcurrentDictionary<int, RoleMenu> menus = new();

    /// <summary>
    ///     Every menu group, by group ID.
    /// </summary>
    private readonly ConcurrentDictionary<int, RoleMenuGroup> groups = new();

    /// <summary>
    ///     Handler for the MessageDeleted event, kept so it can be unsubscribed.
    /// </summary>
    private readonly Func<Cacheable<IMessage, ulong>, Cacheable<IMessageChannel, ulong>, Task> messageDeletedHandler;

    /// <summary>
    ///     Posted message ID to menu ID, used to notice when a menu's message is deleted.
    /// </summary>
    private readonly ConcurrentDictionary<ulong, int> messageIndex = new();

    /// <summary>
    ///     Handler for the MessagesBulkDeleted event, kept so it can be unsubscribed.
    /// </summary>
    private readonly Func<IReadOnlyCollection<Cacheable<IMessage, ulong>>, Cacheable<IMessageChannel, ulong>, Task>
        messagesBulkDeletedHandler;

    /// <summary>
    ///     Role changes the bot made recently, by guild and member, so rapid clicks see the latest state
    ///     before the gateway cache catches up.
    /// </summary>
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), RecentRoleChange> recentChanges = new();

    /// <summary>
    ///     The older emoji role setup service, used for imports.
    /// </summary>
    private readonly RoleCommandsService roleCommands;

    /// <summary>
    ///     Handler for the RoleDeleted event, kept so it can be unsubscribed.
    /// </summary>
    private readonly Func<SocketRole, Task> roleDeletedHandler;

    /// <summary>
    ///     The handler for reactions added to reaction menus, kept so it can be unsubscribed.
    /// </summary>
    private readonly Func<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>, SocketReaction, Task>
        reactionAddedHandler;

    /// <summary>
    ///     The handler for reactions removed from reaction menus, kept so it can be unsubscribed.
    /// </summary>
    private readonly Func<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>, SocketReaction, Task>
        reactionRemovedHandler;

    /// <summary>
    ///     Localized bot strings.
    /// </summary>
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Serializes every menu write.
    /// </summary>
    private readonly SemaphoreSlim writeLock = new(1, 1);

    /// <summary>
    ///     Initializes a new instance of the <see cref="RoleMenuService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">Database connection factory.</param>
    /// <param name="strings">Localized bot strings.</param>
    /// <param name="eventHandler">Event handler for cleanup events.</param>
    /// <param name="roleCommands">The older emoji role setup service.</param>
    /// <param name="guildSettings">Guild settings service.</param>
    /// <param name="collector">Analytics collector.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="ownership">The service deciding which guilds this instance handles.</param>
    public RoleMenuService(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        GeneratedBotStrings strings,
        EventHandler eventHandler,
        RoleCommandsService roleCommands,
        GuildSettingsService guildSettings,
        IAnalyticsCollector collector,
        ILogger<RoleMenuService> logger,
        GuildOwnershipService ownership)
    {
        this.ownership = ownership;
        this.client = client;
        this.dbFactory = dbFactory;
        this.strings = strings;
        this.eventHandler = eventHandler;
        this.roleCommands = roleCommands;
        this.guildSettings = guildSettings;
        this.collector = collector;
        this.logger = logger;

        memberLocks = new SemaphoreSlim[MemberLockStripes];
        for (var i = 0; i < MemberLockStripes; i++)
            memberLocks[i] = new SemaphoreSlim(1, 1);

        messageDeletedHandler = OnMessageDeletedAsync;
        messagesBulkDeletedHandler = OnMessagesBulkDeletedAsync;
        channelDestroyedHandler = OnChannelDestroyedAsync;
        roleDeletedHandler = OnRoleDeletedAsync;
        reactionAddedHandler = (message, channel, reaction) =>
            OnReactionAsync(message, channel, reaction, RoleMenuTrigger.ReactionAdded);
        reactionRemovedHandler = (message, channel, reaction) =>
            OnReactionAsync(message, channel, reaction, RoleMenuTrigger.ReactionRemoved);

        eventHandler.Subscribe("MessageDeleted", EventModuleName, messageDeletedHandler);
        eventHandler.Subscribe("MessagesBulkDeleted", EventModuleName, messagesBulkDeletedHandler);
        eventHandler.Subscribe("ChannelDestroyed", EventModuleName, channelDestroyedHandler);
        eventHandler.Subscribe("RoleDeleted", EventModuleName, roleDeletedHandler);
        eventHandler.Subscribe("ReactionAdded", EventModuleName, reactionAddedHandler);
        eventHandler.Subscribe("ReactionRemoved", EventModuleName, reactionRemovedHandler);
    }

    /// <summary>
    ///     Loads every menu into the cache when the bot is ready.
    /// </summary>
    /// <returns>A task that completes when the cache is filled.</returns>
    public async Task OnReadyAsync()
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var all = await db.RoleMenus.LoadWith(x => x.Options).ToListAsync();
            foreach (var menu in all)
            {
                SortOptions(menu);
                CacheMenu(menu);
            }

            foreach (var group in await db.RoleMenuGroups.ToListAsync())
                groups[group.Id] = group;

            logger.LogInformation("Loaded {Count} role menus", all.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load role menus");
        }

        _ = Task.Run(RunExpiryLoopAsync);
    }

    /// <summary>
    ///     Stops the expiry loop and unsubscribes from the cleanup events.
    /// </summary>
    /// <returns>A completed task.</returns>
    public Task Unload()
    {
        expiryCts.Cancel();
        eventHandler.Unsubscribe("MessageDeleted", EventModuleName, messageDeletedHandler);
        eventHandler.Unsubscribe("MessagesBulkDeleted", EventModuleName, messagesBulkDeletedHandler);
        eventHandler.Unsubscribe("ChannelDestroyed", EventModuleName, channelDestroyedHandler);
        eventHandler.Unsubscribe("RoleDeleted", EventModuleName, roleDeletedHandler);
        eventHandler.Unsubscribe("ReactionAdded", EventModuleName, reactionAddedHandler);
        eventHandler.Unsubscribe("ReactionRemoved", EventModuleName, reactionRemovedHandler);
        return Task.CompletedTask;
    }

    #region Reading

    /// <summary>
    ///     Gets a guild's command prefix.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <returns>The prefix.</returns>
    public Task<string> GetPrefixAsync(IGuild guild)
    {
        return guildSettings.GetPrefix(guild);
    }

    /// <summary>
    ///     Lists a guild's menus with their options, ordered by ID.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The menus.</returns>
    public async Task<List<RoleMenu>> GetMenusAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var list = await db.RoleMenus
            .LoadWith(x => x.Options)
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Id)
            .ToListAsync();
        foreach (var menu in list)
            SortOptions(menu);
        return list;
    }

    /// <summary>
    ///     Gets a menu by ID from the cache, falling back to the database.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <returns>The menu, or null when it does not exist.</returns>
    public async Task<RoleMenu?> GetMenuAsync(int menuId)
    {
        if (menus.TryGetValue(menuId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var menu = await LoadMenuAsync(db, menuId);
        if (menu is not null)
            CacheMenu(menu);
        return menu;
    }

    /// <summary>
    ///     Gets a menu by ID when it belongs to the given guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="menuId">The menu ID.</param>
    /// <returns>The menu, or null when it does not exist in this guild.</returns>
    public async Task<RoleMenu?> GetGuildMenuAsync(ulong guildId, int menuId)
    {
        var menu = await GetMenuAsync(menuId);
        return menu is not null && menu.GuildId == guildId ? menu : null;
    }

    /// <summary>
    ///     Lists a guild's older emoji role setups that can be imported.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The older setups, in their stored order.</returns>
    public async Task<List<ReactionRoleMessage>> GetImportSourcesAsync(ulong guildId)
    {
        var (_, messages) = await roleCommands.Get(guildId);
        return messages ?? [];
    }

    /// <summary>
    ///     Works out a menu's status.
    /// </summary>
    /// <param name="guild">The menu's guild, or null when the bot is not in it.</param>
    /// <param name="menu">The menu.</param>
    /// <returns>The status.</returns>
    public static RoleMenuStatus GetStatus(SocketGuild? guild, RoleMenu menu)
    {
        if (guild?.GetTextChannel(menu.ChannelId) is null)
            return RoleMenuStatus.ChannelMissing;
        if (menu.MessageId is null)
            return RoleMenuStatus.NotPosted;
        return menu.Enabled ? RoleMenuStatus.Live : RoleMenuStatus.Paused;
    }

    /// <summary>
    ///     Builds the jump link to a menu's message.
    /// </summary>
    /// <param name="menu">The menu.</param>
    /// <returns>The link, or null when the menu is not posted.</returns>
    public static string? GetJumpUrl(RoleMenu menu)
    {
        return menu.MessageId is { } messageId
            ? $"https://discord.com/channels/{menu.GuildId}/{menu.ChannelId}/{messageId}"
            : null;
    }

    #endregion

    #region Validation

    /// <summary>
    ///     Checks whether a role can be offered on a menu.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="actor">The person editing the menu, or null to skip the editor check.</param>
    /// <param name="roleId">The role ID.</param>
    /// <returns>The problem, or None.</returns>
    public RoleMenuRoleProblem CheckRole(SocketGuild guild, IGuildUser? actor, ulong roleId)
    {
        var role = guild.GetRole(roleId);
        if (role is null)
            return RoleMenuRoleProblem.Missing;
        if (role.Id == guild.Id)
            return RoleMenuRoleProblem.Everyone;
        if (role.IsManaged)
            return RoleMenuRoleProblem.Managed;
        if (!guild.CurrentUser.GuildPermissions.ManageRoles || role.Position >= guild.CurrentUser.Hierarchy)
            return RoleMenuRoleProblem.AboveBot;
        if (actor is not null && actor.Id != guild.OwnerId && role.Position >= ActorHierarchy(guild, actor))
            return RoleMenuRoleProblem.AboveActor;
        return RoleMenuRoleProblem.None;
    }

    /// <summary>
    ///     Gets the text shown for a role problem.
    /// </summary>
    /// <param name="problem">The problem.</param>
    /// <param name="botCanManageRoles">Whether the bot has Manage Roles in the guild.</param>
    /// <returns>The text, or null when there is no problem.</returns>
    public static string? RoleProblemText(RoleMenuRoleProblem problem, bool botCanManageRoles)
    {
        return problem switch
        {
            RoleMenuRoleProblem.None => null,
            RoleMenuRoleProblem.Missing => ProblemRoleDeleted,
            RoleMenuRoleProblem.Managed => ProblemManaged,
            RoleMenuRoleProblem.AboveActor => ProblemAboveActor,
            _ when !botCanManageRoles => ProblemBotMissingManageRoles,
            _ => ProblemAboveBot
        };
    }

    /// <summary>
    ///     Finds the first permission the bot is missing to post a menu in a channel.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="channel">The channel.</param>
    /// <returns>The problem text, or null when the bot can post there.</returns>
    public static string? ChannelProblem(SocketGuild guild, IGuildChannel channel)
    {
        var perms = guild.CurrentUser.GetPermissions(channel);
        if (!perms.ViewChannel)
            return ProblemCantSee;
        if (!perms.SendMessages)
            return ProblemCantSend;
        if (!perms.EmbedLinks)
            return ProblemCantEmbed;
        return !perms.ReadMessageHistory ? ProblemCantReadHistory : null;
    }

    /// <summary>
    ///     Checks that a channel can hold a menu.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="channel">The channel when it is valid.</param>
    /// <returns>The error and detail, or None.</returns>
    public (RoleMenuError Error, string? Detail) ValidateChannel(SocketGuild guild, ulong channelId,
        out SocketTextChannel? channel)
    {
        channel = guild.GetTextChannel(channelId);
        if (channel is null or IThreadChannel or IVoiceChannel)
        {
            channel = null;
            return (RoleMenuError.InvalidChannel, null);
        }

        var problem = ChannelProblem(guild, channel);
        return problem is null ? (RoleMenuError.None, null) : (RoleMenuError.InvalidChannel, problem);
    }

    /// <summary>
    ///     Checks whether a channel is a text or announcement channel that can hold a menu.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns>True when the channel type is allowed.</returns>
    public static bool IsMenuChannel(SocketTextChannel channel)
    {
        return channel is not IThreadChannel and not IVoiceChannel;
    }

    /// <summary>
    ///     Clamps a menu's limits to what its mode and option count allow.
    /// </summary>
    /// <param name="mode">Pick any or pick one.</param>
    /// <param name="min">Requested minimum.</param>
    /// <param name="max">Requested maximum, zero for no limit.</param>
    /// <param name="optionCount">Number of options on the menu.</param>
    /// <returns>The clamped limits.</returns>
    public static (int Min, int Max) ClampLimits(RoleMenuMode mode, int min, int max, int optionCount)
    {
        if (mode == RoleMenuMode.Exclusive)
            return (min >= 1 ? 1 : 0, 1);

        var clampedMax = Math.Clamp(max, 0, Math.Max(optionCount, 0));
        var clampedMin = Math.Clamp(min, 0, clampedMax == 0 ? Math.Max(optionCount, 0) : clampedMax);
        return (clampedMin, clampedMax);
    }

    /// <summary>
    ///     Normalizes a draft in place and rejects values that can't be stored.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="draft">The draft to normalize.</param>
    /// <returns>The error and detail, or None.</returns>
    private (RoleMenuError Error, string? Detail) ValidateDraft(SocketGuild guild, RoleMenuDraft draft)
    {
        if (!Enum.IsDefined(draft.Style))
            draft.Style = RoleMenuStyle.Dropdown;
        if (!Enum.IsDefined(draft.Mode))
            draft.Mode = RoleMenuMode.Multi;
        if (!Enum.IsDefined(draft.ReplyMode))
            draft.ReplyMode = RoleMenuReplyMode.Private;

        var name = draft.Name?.Trim();
        if (string.IsNullOrEmpty(name))
            name = strings.RolemenuDefaultName(guild.Id);
        if (name.Length > NameLength)
            return (RoleMenuError.NameInvalid, null);
        draft.Name = name;

        if (string.IsNullOrWhiteSpace(draft.Message))
        {
            draft.Message = null;
        }
        else
        {
            if (draft.Message.Length > MessageSourceLength)
                return (RoleMenuError.TextTooLong, "message");
            if (!draft.Message.TrimStart().StartsWith('{') && draft.Message.Length > PlainMessageLength)
                return (RoleMenuError.TextTooLong, "message");
        }

        var placeholder = draft.Placeholder?.Trim();
        if (string.IsNullOrEmpty(placeholder))
            placeholder = null;
        if (placeholder is { Length: > PlaceholderLength })
            return (RoleMenuError.TextTooLong, "placeholder");
        draft.Placeholder = placeholder;

        draft.Options ??= [];
        if (draft.Options.Count == 0)
            return (RoleMenuError.NoOptions, null);
        if (draft.Options.Count > MaxOptions)
            return (RoleMenuError.TooManyOptions, null);

        var seen = new HashSet<ulong>();
        foreach (var option in draft.Options)
        {
            var role = guild.GetRole(option.RoleId);
            if (!seen.Add(option.RoleId))
                return (RoleMenuError.DuplicateRole, role?.Name ?? option.RoleId.ToString());

            var label = option.Label?.Trim();
            if (string.IsNullOrEmpty(label))
                label = role is null ? option.RoleId.ToString() : role.Name.Trim().TrimTo(LabelLength, true);
            if (string.IsNullOrEmpty(label))
                label = option.RoleId.ToString();
            if (label.Length > LabelLength)
                return (RoleMenuError.TextTooLong, "label");
            option.Label = label;

            var description = option.Description?.Trim();
            if (string.IsNullOrEmpty(description))
                description = null;
            if (description is { Length: > DescriptionLength })
                return (RoleMenuError.TextTooLong, "description");
            option.Description = description;

            if (option.ButtonStyle is < 1 or > 4)
                option.ButtonStyle = 2;

            var emojiText = option.Emoji?.Trim();
            if (string.IsNullOrEmpty(emojiText))
            {
                option.Emoji = null;
                continue;
            }

            if (!emojiText.TryToIEmote(out var emote) || emote is null)
                return (RoleMenuError.InvalidEmoji, emojiText);

            switch (emote)
            {
                case Emote custom:
                    if (!client.Guilds.Any(g => g.Emotes.Any(e => e.Id == custom.Id)))
                        return (RoleMenuError.InvalidEmoji, emojiText);
                    option.Emoji = custom.ToString();
                    break;
                case Emoji unicode:
                    option.Emoji = unicode.Name;
                    break;
                default:
                    return (RoleMenuError.InvalidEmoji, emojiText);
            }
        }

        if (draft.Style == RoleMenuStyle.Reactions &&
            (draft.Options.Count > MaxReactionOptions || draft.Options.Any(o => o.Emoji is null) ||
             draft.Options.Select(o => o.Emoji).Distinct().Count() != draft.Options.Count))
            return (RoleMenuError.InvalidReactions, null);

        (draft.MinRoles, draft.MaxRoles) = ClampLimits(draft.Mode, draft.MinRoles, draft.MaxRoles,
            draft.Options.Count);

        if (draft.RequiredRoleId is 0)
            draft.RequiredRoleId = null;
        if (draft.RequiredRoleId is { } requiredId &&
            (requiredId == guild.Id || guild.GetRole(requiredId) is null))
            return (RoleMenuError.InvalidRequiredRole, requiredId.ToString());

        draft.BlockedRoleIds = (draft.BlockedRoleIds ?? []).Where(x => x != 0).Distinct().ToList();
        if (draft.BlockedRoleIds.Count > MaxBlockedRoles)
            return (RoleMenuError.InvalidRequiredRole, draft.BlockedRoleIds.Count.ToString());
        foreach (var blockedId in draft.BlockedRoleIds)
        {
            if (blockedId == guild.Id || guild.GetRole(blockedId) is null)
                return (RoleMenuError.InvalidRequiredRole, blockedId.ToString());
        }

        if (draft.GroupId is 0)
            draft.GroupId = null;
        if (draft.GroupId is { } groupId && !(groups.TryGetValue(groupId, out var group) && group.GuildId == guild.Id))
            return (RoleMenuError.InvalidGroup, groupId.ToString());

        if (draft.AccentColor is < 0 or > 0xFFFFFF || draft.ButtonColor is < 0 or > 4)
            return (RoleMenuError.InvalidColor, null);

        if (draft.TempRoleSeconds is < 0 or > MaxTempSeconds || draft.Options.Any(o =>
                o.TempRoleSeconds is < 0 or > MaxTempSeconds || o.MaxHolders < 0))
            return (RoleMenuError.InvalidDuration, null);

        if (draft.ExpiresAt is { } expiresAt)
        {
            draft.ExpiresAt = expiresAt.Kind == DateTimeKind.Local ? expiresAt.ToUniversalTime() : expiresAt;
            if (draft.ExpiresAt <= DateTime.UtcNow)
                return (RoleMenuError.InvalidDuration, null);
        }

        return (RoleMenuError.None, null);
    }

    /// <summary>
    ///     Parses stored comma separated role IDs.
    /// </summary>
    /// <param name="stored">The stored text.</param>
    /// <returns>The role IDs.</returns>
    public static List<ulong> ParseRoleIds(string? stored)
    {
        return (stored ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => ulong.TryParse(x, out var id) ? id : 0)
            .Where(x => x != 0)
            .ToList();
    }

    /// <summary>
    ///     Parses a duration such as 1h30m. "off" and "0" mean no duration.
    /// </summary>
    /// <param name="input">The text.</param>
    /// <param name="seconds">The duration in seconds, zero for none.</param>
    /// <returns>False when the text is not a valid duration.</returns>
    public static bool TryParseDuration(string? input, out int seconds)
    {
        seconds = 0;
        var text = input?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(text) || text is "off" or "none" or "0")
            return true;

        try
        {
            var total = StoopidTime.FromInput(text).Time.TotalSeconds;
            if (total is < 1 or > MaxTempSeconds)
                return false;
            seconds = (int)total;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Checks every role on a draft. The editor check only covers roles the stored menu does not have yet.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="actor">The person saving the menu, or null.</param>
    /// <param name="draft">The normalized draft.</param>
    /// <param name="stored">The stored menu when updating, or null when creating.</param>
    /// <returns>The error and the offending role name, or None.</returns>
    private (RoleMenuError Error, string? Detail) ValidateRoles(SocketGuild guild, IGuildUser? actor,
        RoleMenuDraft draft, RoleMenu? stored)
    {
        var storedRoles = stored?.Options.Select(x => x.RoleId).ToHashSet() ?? [];
        foreach (var option in draft.Options)
        {
            var checkActor = !storedRoles.Contains(option.RoleId);
            var problem = CheckRole(guild, checkActor ? actor : null, option.RoleId);
            var roleName = guild.GetRole(option.RoleId)?.Name ?? option.RoleId.ToString();
            switch (problem)
            {
                case RoleMenuRoleProblem.None:
                    continue;
                case RoleMenuRoleProblem.Missing:
                    return (RoleMenuError.RoleMissing, roleName);
                case RoleMenuRoleProblem.AboveActor:
                    return (RoleMenuError.RoleAboveActor, roleName);
                default:
                    return (RoleMenuError.RoleNotAssignable, roleName);
            }
        }

        return (RoleMenuError.None, null);
    }

    /// <summary>
    ///     Gets the position of an editor's highest role.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="actor">The editor.</param>
    /// <returns>The highest role position.</returns>
    private static int ActorHierarchy(SocketGuild guild, IGuildUser actor)
    {
        if (actor is SocketGuildUser socketUser)
            return socketUser.Hierarchy;

        return actor.RoleIds
            .Select(guild.GetRole)
            .Where(r => r is not null)
            .Select(r => r!.Position)
            .DefaultIfEmpty(0)
            .Max();
    }

    #endregion

    #region Writing

    /// <summary>
    ///     Creates a menu and posts its message.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="actor">The person creating the menu, or null.</param>
    /// <param name="draft">The menu to create.</param>
    /// <returns>The created menu or the reason it was refused.</returns>
    public async Task<RoleMenuResult> CreateAsync(ulong guildId, IGuildUser? actor, RoleMenuDraft draft)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return RoleMenuResult.Fail(RoleMenuError.NotFound);

        await writeLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var (error, detail) = ValidateDraft(guild, draft);
            if (error != RoleMenuError.None)
                return RoleMenuResult.Fail(error, detail);

            (error, detail) = ValidateChannel(guild, draft.ChannelId, out var channel);
            if (error != RoleMenuError.None || channel is null)
                return RoleMenuResult.Fail(RoleMenuError.InvalidChannel, detail);

            (error, detail) = ValidateRoles(guild, actor, draft, null);
            if (error != RoleMenuError.None)
                return RoleMenuResult.Fail(error, detail);

            var now = DateTime.UtcNow;
            var menu = new RoleMenu
            {
                GuildId = guildId,
                CreatedBy = actor?.Id ?? 0,
                DateAdded = now
            };
            ApplyDraft(menu, draft, now);

            await using var tx = await db.BeginTransactionAsync();
            menu.Id = await db.InsertWithInt32IdentityAsync(menu);

            var options = new List<RoleMenuOption>();
            for (var i = 0; i < draft.Options.Count; i++)
            {
                var option = NewOption(menu.Id, draft.Options[i], i, now);
                option.Id = await db.InsertWithInt32IdentityAsync(option);
                options.Add(option);
            }

            menu.Options = options;

            IUserMessage posted;
            try
            {
                posted = await PostAsync(guild, channel, menu);
            }
            catch (HttpException ex)
            {
                await tx.RollbackAsync();
                return RoleMenuResult.Fail(RoleMenuError.PostFailed, ex.Reason ?? ex.Message);
            }

            try
            {
                menu.MessageId = posted.Id;
                await db.RoleMenus
                    .Where(x => x.Id == menu.Id)
                    .Set(x => x.MessageId, (ulong?)posted.Id)
                    .UpdateAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await DeleteMessageQuietlyAsync(guild, channel.Id, posted.Id);
                throw;
            }

            CacheMenu(menu);
            return RoleMenuResult.Ok(menu);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    ///     Replaces a menu with a draft and rebuilds its message in place. A new message is posted only when the
    ///     channel changed or the old message is gone.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="actor">The person editing the menu, or null.</param>
    /// <param name="draft">The desired state.</param>
    /// <returns>The updated menu or the reason it was refused.</returns>
    public async Task<RoleMenuResult> UpdateAsync(ulong guildId, int menuId, IGuildUser? actor,
        RoleMenuDraft draft)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return RoleMenuResult.Fail(RoleMenuError.NotFound);

        await writeLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var stored = await LoadMenuAsync(db, menuId);
            if (stored is null || stored.GuildId != guildId)
                return RoleMenuResult.Fail(RoleMenuError.NotFound);

            var (error, detail) = ValidateDraft(guild, draft);
            if (error != RoleMenuError.None)
                return RoleMenuResult.Fail(error, detail);

            (error, detail) = ValidateChannel(guild, draft.ChannelId, out var channel);
            if (error != RoleMenuError.None || channel is null)
                return RoleMenuResult.Fail(RoleMenuError.InvalidChannel, detail);

            (error, detail) = ValidateRoles(guild, actor, draft, stored);
            if (error != RoleMenuError.None)
                return RoleMenuResult.Fail(error, detail);

            var now = DateTime.UtcNow;
            var oldChannelId = stored.ChannelId;
            var oldMessageId = stored.MessageId;
            var oldGroupId = stored.GroupId;

            await using var tx = await db.BeginTransactionAsync();

            var storedById = stored.Options.ToDictionary(x => x.Id);
            var kept = new HashSet<int>();
            var options = new List<RoleMenuOption>();
            for (var i = 0; i < draft.Options.Count; i++)
            {
                var source = draft.Options[i];
                if (source.Id is { } optionId && optionId > 0 && storedById.TryGetValue(optionId, out var existing) &&
                    kept.Add(optionId))
                {
                    existing.RoleId = source.RoleId;
                    existing.Label = source.Label!;
                    existing.Emoji = source.Emoji;
                    existing.Description = source.Description;
                    existing.ButtonStyle = source.ButtonStyle;
                    existing.TempRoleSeconds = source.TempRoleSeconds;
                    existing.MaxHolders = source.MaxHolders;
                    existing.Position = i;
                    await db.UpdateAsync(existing);
                    options.Add(existing);
                    continue;
                }

                var option = NewOption(menuId, source, i, now);
                option.Id = await db.InsertWithInt32IdentityAsync(option);
                options.Add(option);
            }

            var removed = storedById.Keys.Where(id => !kept.Contains(id)).ToList();
            if (removed.Count > 0)
            {
                var removedRoleIds = removed.Select(id => storedById[id].RoleId).ToList();
                await db.RoleMenuReverseStates
                    .Where(x => x.MenuId == menuId && removedRoleIds.Contains(x.RoleId))
                    .DeleteAsync();
                await db.RoleMenuOptions
                    .Where(x => x.RoleMenuId == menuId && removed.Contains(x.Id))
                    .DeleteAsync();
            }

            ApplyDraft(stored, draft, now);
            stored.Options = options;

            IUserMessage? posted = null;
            ulong? staleChannelId = null;
            try
            {
                if (oldChannelId == channel.Id && oldMessageId is { } currentId &&
                    await FetchOwnMessageAsync(channel, currentId) is { } current)
                {
                    await ModifyAsync(guild, channel, current, stored);
                }
                else
                {
                    posted = await PostAsync(guild, channel, stored);
                    stored.MessageId = posted.Id;
                    if (oldChannelId != channel.Id)
                        staleChannelId = oldChannelId;
                }
            }
            catch (HttpException ex)
            {
                await tx.RollbackAsync();
                return RoleMenuResult.Fail(RoleMenuError.PostFailed, ex.Reason ?? ex.Message);
            }

            try
            {
                await db.UpdateAsync(stored);
                await tx.CommitAsync();
            }
            catch
            {
                if (posted is not null)
                    await DeleteMessageQuietlyAsync(guild, channel.Id, posted.Id);
                throw;
            }

            if (oldMessageId is { } previous && previous != stored.MessageId)
                messageIndex.TryRemove(previous, out _);
            CacheMenu(stored);
            if (oldGroupId != stored.GroupId)
                await PruneGroupAsync(db, oldGroupId);

            if (staleChannelId is { } staleChannel && oldMessageId is { } staleMessage)
                await DeleteMessageQuietlyAsync(guild, staleChannel, staleMessage);

            return RoleMenuResult.Ok(stored);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    ///     Deletes a menu and its message. Members keep the roles they already have.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="menuId">The menu ID.</param>
    /// <returns>The deleted menu or NotFound.</returns>
    public async Task<RoleMenuResult> DeleteAsync(ulong guildId, int menuId)
    {
        await writeLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var stored = await LoadMenuAsync(db, menuId);
            if (stored is null || stored.GuildId != guildId)
                return RoleMenuResult.Fail(RoleMenuError.NotFound);

            if (stored.MessageId is { } messageId)
                messageIndex.TryRemove(messageId, out _);

            await db.RoleMenus.Where(x => x.Id == menuId).DeleteAsync();
            menus.TryRemove(menuId, out _);
            await PruneGroupAsync(db, stored.GroupId);

            if (stored.MessageId is { } postedId && client.GetGuild(guildId) is { } guild)
                await DeleteMessageQuietlyAsync(guild, stored.ChannelId, postedId);

            return RoleMenuResult.Ok(stored);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    ///     Pauses or resumes a menu and edits its message so the controls match.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="enabled">False to pause, true to resume.</param>
    /// <returns>The updated menu or the reason it was refused.</returns>
    public async Task<RoleMenuResult> SetEnabledAsync(ulong guildId, int menuId, bool enabled)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return RoleMenuResult.Fail(RoleMenuError.NotFound);

        await writeLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var stored = await LoadMenuAsync(db, menuId);
            if (stored is null || stored.GuildId != guildId)
                return RoleMenuResult.Fail(RoleMenuError.NotFound);

            stored.Enabled = enabled;
            stored.DateModified = DateTime.UtcNow;

            await using var tx = await db.BeginTransactionAsync();
            await db.UpdateAsync(stored);

            try
            {
                await EditInPlaceAsync(guild, stored);
            }
            catch (HttpException ex)
            {
                await tx.RollbackAsync();
                return RoleMenuResult.Fail(RoleMenuError.PostFailed, ex.Reason ?? ex.Message);
            }

            await tx.CommitAsync();
            CacheMenu(stored);
            return RoleMenuResult.Ok(stored);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    ///     Reorders a menu's options and edits its message.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="optionIds">Every option ID of the menu, exactly once, in the new order.</param>
    /// <returns>The updated menu or the reason it was refused.</returns>
    public async Task<RoleMenuResult> ReorderAsync(ulong guildId, int menuId, IReadOnlyList<int> optionIds)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return RoleMenuResult.Fail(RoleMenuError.NotFound);

        await writeLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var stored = await LoadMenuAsync(db, menuId);
            if (stored is null || stored.GuildId != guildId)
                return RoleMenuResult.Fail(RoleMenuError.NotFound);

            var byId = stored.Options.ToDictionary(x => x.Id);
            if (optionIds.Count != byId.Count || optionIds.Distinct().Count() != optionIds.Count ||
                !optionIds.All(byId.ContainsKey))
                return RoleMenuResult.Fail(RoleMenuError.InvalidOrder);

            await using var tx = await db.BeginTransactionAsync();
            for (var i = 0; i < optionIds.Count; i++)
            {
                var option = byId[optionIds[i]];
                option.Position = i;
                var position = i;
                await db.RoleMenuOptions
                    .Where(x => x.Id == option.Id)
                    .Set(x => x.Position, position)
                    .UpdateAsync();
            }

            stored.DateModified = DateTime.UtcNow;
            SortOptions(stored);
            await db.UpdateAsync(stored);

            try
            {
                await EditInPlaceAsync(guild, stored);
            }
            catch (HttpException ex)
            {
                await tx.RollbackAsync();
                return RoleMenuResult.Fail(RoleMenuError.PostFailed, ex.Reason ?? ex.Message);
            }

            await tx.CommitAsync();
            CacheMenu(stored);
            return RoleMenuResult.Ok(stored);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    ///     Posts a fresh copy of a menu and deletes the old message.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="channelId">The target channel, or null or zero for the menu's channel.</param>
    /// <returns>The updated menu or the reason it was refused.</returns>
    public async Task<RoleMenuResult> RepostAsync(ulong guildId, int menuId, ulong? channelId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return RoleMenuResult.Fail(RoleMenuError.NotFound);

        await writeLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var stored = await LoadMenuAsync(db, menuId);
            if (stored is null || stored.GuildId != guildId)
                return RoleMenuResult.Fail(RoleMenuError.NotFound);

            if (stored.Options.Count == 0)
                return RoleMenuResult.Fail(RoleMenuError.NoOptions);

            var targetId = channelId is > 0 ? channelId.Value : stored.ChannelId;
            var (error, detail) = ValidateChannel(guild, targetId, out var channel);
            if (error != RoleMenuError.None || channel is null)
                return RoleMenuResult.Fail(RoleMenuError.InvalidChannel, detail);

            var oldChannelId = stored.ChannelId;
            var oldMessageId = stored.MessageId;

            stored.ChannelId = channel.Id;
            stored.DateModified = DateTime.UtcNow;

            await using var tx = await db.BeginTransactionAsync();

            IUserMessage posted;
            try
            {
                posted = await PostAsync(guild, channel, stored);
            }
            catch (HttpException ex)
            {
                await tx.RollbackAsync();
                return RoleMenuResult.Fail(RoleMenuError.PostFailed, ex.Reason ?? ex.Message);
            }

            try
            {
                stored.MessageId = posted.Id;
                await db.UpdateAsync(stored);
                await tx.CommitAsync();
            }
            catch
            {
                await DeleteMessageQuietlyAsync(guild, channel.Id, posted.Id);
                throw;
            }

            if (oldMessageId is { } previous)
                messageIndex.TryRemove(previous, out _);
            CacheMenu(stored);

            if (oldMessageId is { } stale)
                await DeleteMessageQuietlyAsync(guild, oldChannelId, stale);

            return RoleMenuResult.Ok(stored);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    ///     Moves an older emoji role setup into a new menu, then optionally retires the older setup.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="actor">The person importing, or null.</param>
    /// <param name="import">Import settings.</param>
    /// <returns>The created menu or the reason it was refused.</returns>
    public async Task<RoleMenuResult> ImportAsync(ulong guildId, IGuildUser? actor, RoleMenuImportDraft import)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return RoleMenuResult.Fail(RoleMenuError.NotFound);

        var sources = await GetImportSourcesAsync(guildId);
        var source = sources.FirstOrDefault(x => x.Id == import.SourceId);
        if (source is null)
            return RoleMenuResult.Fail(RoleMenuError.ImportSourceNotFound, import.SourceId.ToString());

        var pairs = (source.ReactionRoles ?? [])
            .Where(x => guild.GetRole(x.RoleId) is not null && x.RoleId != guild.Id)
            .DistinctBy(x => x.RoleId)
            .Take(MaxOptions)
            .ToList();
        if (pairs.Count == 0)
            return RoleMenuResult.Fail(RoleMenuError.ImportNoRoles);

        var channelId = import.ChannelId is > 0 ? import.ChannelId.Value : source.ChannelId;

        var sourceChannel = guild.GetTextChannel(source.ChannelId);
        IUserMessage? original = null;
        if (sourceChannel is not null)
        {
            try
            {
                original = await sourceChannel.GetMessageAsync(source.MessageId) as IUserMessage;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not fetch the older role setup message {MessageId}", source.MessageId);
            }
        }

        string? message = null;
        if (import.CopyMessage && original is not null)
        {
            message = SerializeForBuilder(original);
            if (message is { Length: > MessageSourceLength })
                message = null;
        }

        var isExclusive = source.Exclusive;
        var draft = new RoleMenuDraft
        {
            Name = import.Name,
            ChannelId = channelId,
            Message = message,
            Style = import.Style,
            Mode = isExclusive ? RoleMenuMode.Exclusive : RoleMenuMode.Multi,
            MaxRoles = isExclusive ? 1 : 0,
            ReplyMode = RoleMenuReplyMode.Private,
            Enabled = true,
            Options = pairs.Select(x => new RoleMenuOptionDraft
            {
                RoleId = x.RoleId,
                Label = guild.GetRole(x.RoleId)!.Name.Trim().TrimTo(LabelLength, true),
                Emoji = ResolveLegacyEmoji(guild, x.EmoteName),
                ButtonStyle = 2
            }).ToList()
        };

        var result = await CreateAsync(guildId, actor, draft);
        if (!result.Success || !import.RetireOriginal)
            return result;

        await roleCommands.RemoveByIdAsync(guildId, source.Id);

        if (original is null || sourceChannel is null)
            return result;

        try
        {
            if (original.Author.Id == client.CurrentUser.Id)
            {
                await original.DeleteAsync();
            }
            else if (guild.CurrentUser.GetPermissions(sourceChannel).ManageMessages)
            {
                await original.RemoveAllReactionsAsync();
            }
            else
            {
                foreach (var pair in source.ReactionRoles ?? [])
                {
                    var emote = ResolveLegacyEmoji(guild, pair.EmoteName);
                    if (emote is not null && emote.TryToIEmote(out var parsed) && parsed is not null)
                        await original.RemoveReactionAsync(parsed, client.CurrentUser);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not retire the older role setup message {MessageId}", source.MessageId);
        }

        return result;
    }

    /// <summary>
    ///     Resolves an emoji stored by the older setup into text the menu can use.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="emoteName">The stored emoji text, which may be a bare custom emoji name.</param>
    /// <returns>Usable emoji text, or null.</returns>
    private static string? ResolveLegacyEmoji(SocketGuild guild, string? emoteName)
    {
        if (string.IsNullOrWhiteSpace(emoteName))
            return null;

        var trimmed = emoteName.Trim();
        if (trimmed.TryToIEmote(out var parsed) && parsed is not null)
            return trimmed;

        var match = guild.Emotes.FirstOrDefault(e => e.Name == trimmed);
        return match?.ToString();
    }

    /// <summary>
    ///     Converts a message's text and embeds into embed builder JSON.
    /// </summary>
    /// <param name="message">The message to copy.</param>
    /// <returns>The JSON source, or null when the message has nothing to copy.</returns>
    private static string? SerializeForBuilder(IMessage message)
    {
        var content = string.IsNullOrWhiteSpace(message.Content) ? null : message.Content;
        var embeds = message.Embeds
            .Where(e => e.Type == EmbedType.Rich)
            .Select(e => new
            {
                title = e.Title,
                description = e.Description,
                url = e.Url,
                color = e.Color is { } color ? $"#{color.RawValue:X6}" : null,
                author = e.Author is { } author
                    ? new
                    {
                        name = author.Name, url = author.Url, icon_url = author.IconUrl
                    }
                    : null,
                thumbnail = e.Thumbnail is { } thumbnail
                    ? new
                    {
                        url = thumbnail.Url
                    }
                    : null,
                image = e.Image is { } image
                    ? new
                    {
                        url = image.Url
                    }
                    : null,
                footer = e.Footer is { } footer
                    ? new
                    {
                        text = footer.Text, icon_url = footer.IconUrl
                    }
                    : null,
                fields = e.Fields.Length == 0
                    ? null
                    : e.Fields.Select(f => new
                    {
                        name = f.Name, value = f.Value, inline = f.Inline
                    }).ToList()
            })
            .ToList();

        if (content is null && embeds.Count == 0)
            return null;

        var payload = new
        {
            content, embeds = embeds.Count == 0 ? null : embeds
        };
        return JsonSerializer.Serialize(payload, CopyJsonOptions);
    }

    /// <summary>
    ///     Copies a draft's menu level fields onto a stored menu.
    /// </summary>
    /// <param name="menu">The menu to update.</param>
    /// <param name="draft">The normalized draft.</param>
    /// <param name="now">The modification time.</param>
    private static void ApplyDraft(RoleMenu menu, RoleMenuDraft draft, DateTime now)
    {
        menu.Name = draft.Name!;
        menu.ChannelId = draft.ChannelId;
        menu.Message = draft.Message;
        menu.Style = (int)draft.Style;
        menu.Placeholder = draft.Placeholder;
        menu.Mode = (int)draft.Mode;
        menu.MinRoles = draft.MinRoles;
        menu.MaxRoles = draft.MaxRoles;
        menu.RequiredRoleId = draft.RequiredRoleId;
        menu.BlockedRoleIds = string.Join(',', draft.BlockedRoleIds);
        menu.TempRoleSeconds = draft.TempRoleSeconds;
        menu.ExpiresAt = draft.ExpiresAt;
        menu.GroupId = draft.GroupId;
        menu.AccentColor = draft.AccentColor;
        menu.ButtonColor = draft.ButtonColor;
        menu.ReplyMode = (int)draft.ReplyMode;
        menu.Enabled = draft.Enabled;
        menu.DateModified = now;
    }

    /// <summary>
    ///     Creates a new option row from a normalized option draft.
    /// </summary>
    /// <param name="menuId">The owning menu ID.</param>
    /// <param name="source">The normalized option draft.</param>
    /// <param name="position">The 0-based position.</param>
    /// <param name="now">The creation time.</param>
    /// <returns>The option row, not yet inserted.</returns>
    private static RoleMenuOption NewOption(int menuId, RoleMenuOptionDraft source, int position, DateTime now)
    {
        return new RoleMenuOption
        {
            RoleMenuId = menuId,
            RoleId = source.RoleId,
            Label = source.Label!,
            Emoji = source.Emoji,
            Description = source.Description,
            ButtonStyle = source.ButtonStyle,
            TempRoleSeconds = source.TempRoleSeconds,
            MaxHolders = source.MaxHolders,
            Position = position,
            DateAdded = now
        };
    }

    #endregion

    #region Messages

    /// <summary>
    ///     Builds the text, embeds, and controls for a menu's message.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="channel">The channel the message lives in.</param>
    /// <param name="menu">The menu with its options sorted.</param>
    /// <returns>Plain text, embeds, and controls.</returns>
    public (string? Text, Discord.Embed[]? Embeds, MessageComponent Components) BuildMessage(SocketGuild guild,
        ITextChannel channel, RoleMenu menu)
    {
        string? text;
        Discord.Embed[]? embeds;

        if (!string.IsNullOrWhiteSpace(menu.Message))
        {
            var replacer = new ReplacementBuilder()
                .WithDefault(client.CurrentUser, channel, guild, client)
                .Build();
            var rendered = replacer.Replace(menu.Message) ?? menu.Message;
            if (SmartEmbed.TryParse(rendered, guild.Id, out var parsedEmbeds, out var plain, out _))
            {
                text = string.IsNullOrWhiteSpace(plain) ? null : plain;
                embeds = parsedEmbeds is { Length: > 0 } ? parsedEmbeds : null;
            }
            else
            {
                text = rendered.TrimTo(PlainMessageLength);
                embeds = null;
            }

            if (text is null && embeds is null)
                embeds = [BuildDefaultEmbed(guild, menu)];
        }
        else
        {
            text = null;
            embeds = [BuildDefaultEmbed(guild, menu)];
        }

        return (text, embeds, BuildComponents(guild, menu));
    }

    /// <summary>
    ///     Builds the default message: the menu name and one line per option.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="menu">The menu.</param>
    /// <returns>The embed.</returns>
    private Discord.Embed BuildDefaultEmbed(SocketGuild guild, RoleMenu menu)
    {
        var lines = menu.Options.Select(o =>
            (o.Emoji is null ? "" : o.Emoji + " ") + "**" + o.Label + "**" +
            (o.Description is null ? "" : ": " + o.Description));
        var title = menu.Name.TrimTo(256);
        var intro = strings.RolemenuDefaultDescription(guild.Id);
        var description = (intro + "\n\n" + string.Join("\n", lines)).TrimTo(4096);
        var embed = new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description);
        if (menu.AccentColor is { } accent)
            embed.WithColor(new Color((uint)accent));
        else
            embed.WithOkColor(guild.Id);
        return embed.Build();
    }

    /// <summary>
    ///     Builds a menu's dropdown or buttons.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="menu">The menu with its options sorted.</param>
    /// <returns>The controls.</returns>
    private MessageComponent BuildComponents(SocketGuild guild, RoleMenu menu)
    {
        var builder = new ComponentBuilder();
        var disabled = !menu.Enabled;
        var options = menu.Options;
        if (options.Count == 0 || (RoleMenuStyle)menu.Style == RoleMenuStyle.Reactions)
            return builder.Build();

        if ((RoleMenuStyle)menu.Style == RoleMenuStyle.Buttons)
        {
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var button = new ButtonBuilder()
                    .WithLabel(option.Label)
                    .WithCustomId($"rolemenu:btn:{menu.Id}:{option.Id}")
                    .WithStyle(ToButtonStyle(option.ButtonStyle == 2 && menu.ButtonColor != 0
                        ? menu.ButtonColor
                        : option.ButtonStyle))
                    .WithDisabled(disabled);
                if (ParseEmote(option.Emoji) is { } emote)
                    button.WithEmote(emote);
                builder.WithButton(button, i / ButtonsPerRow);
            }

            return builder.Build();
        }

        var exclusive = (RoleMenuMode)menu.Mode == RoleMenuMode.Exclusive;
        var placeholder = string.IsNullOrWhiteSpace(menu.Placeholder)
            ? exclusive
                ? strings.RolemenuDefaultPlaceholderOne(guild.Id)
                : strings.RolemenuDefaultPlaceholderAny(guild.Id)
            : menu.Placeholder;
        var maxValues = exclusive
            ? 1
            : menu.MaxRoles == 0
                ? options.Count
                : Math.Min(menu.MaxRoles, options.Count);

        var select = new SelectMenuBuilder()
            .WithCustomId($"rolemenu:sel:{menu.Id}")
            .WithPlaceholder(placeholder.TrimTo(PlaceholderLength, true))
            .WithMinValues(1)
            .WithMaxValues(Math.Max(maxValues, 1))
            .WithDisabled(disabled);

        foreach (var option in options)
        {
            select.AddOption(option.Label, option.Id.ToString(),
                string.IsNullOrWhiteSpace(option.Description) ? null : option.Description,
                ParseEmote(option.Emoji));
        }

        builder.WithSelectMenu(select, 0);
        return builder.Build();
    }

    /// <summary>
    ///     Maps a stored button style to a Discord button style, never a link.
    /// </summary>
    /// <param name="style">The stored style.</param>
    /// <returns>The Discord style.</returns>
    private static ButtonStyle ToButtonStyle(int style)
    {
        return style is >= 1 and <= 4 ? (ButtonStyle)style : ButtonStyle.Secondary;
    }

    /// <summary>
    ///     Parses stored emoji text.
    /// </summary>
    /// <param name="emoji">The stored emoji text.</param>
    /// <returns>The emote, or null.</returns>
    private static IEmote? ParseEmote(string? emoji)
    {
        if (string.IsNullOrWhiteSpace(emoji))
            return null;
        return emoji.TryToIEmote(out var emote) ? emote : null;
    }

    /// <summary>
    ///     Posts a menu's message.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="menu">The menu.</param>
    /// <returns>The posted message.</returns>
    private async Task<IUserMessage> PostAsync(SocketGuild guild, ITextChannel channel, RoleMenu menu)
    {
        var (text, embeds, components) = BuildMessage(guild, channel, menu);
        var message = await channel.SendMessageAsync(text, embeds: embeds, components: components,
            allowedMentions: AllowedMentions.None);

        if ((RoleMenuStyle)menu.Style != RoleMenuStyle.Reactions)
            return message;

        try
        {
            await SyncReactionsAsync(message, menu);
        }
        catch
        {
            await DeleteMessageQuietlyAsync(guild, channel.Id, message.Id);
            throw;
        }

        return message;
    }

    /// <summary>
    ///     Makes a posted message carry exactly the bot's reactions for the menu's options, or none when the menu is
    ///     no longer a reaction menu.
    /// </summary>
    /// <param name="message">The posted message.</param>
    /// <param name="menu">The menu.</param>
    /// <returns>A task that completes when the reactions match.</returns>
    private async Task SyncReactionsAsync(IUserMessage message, RoleMenu menu)
    {
        if ((RoleMenuStyle)menu.Style != RoleMenuStyle.Reactions)
        {
            if (message.Reactions.Count > 0)
                await message.RemoveAllReactionsAsync();
            return;
        }

        var wanted = menu.Options.Select(o => ParseEmote(o.Emoji)).OfType<IEmote>().ToList();
        foreach (var stale in message.Reactions.Keys.Where(x => !wanted.Contains(x)).ToList())
            await message.RemoveAllReactionsForEmoteAsync(stale);

        foreach (var emote in wanted)
        {
            if (!message.Reactions.TryGetValue(emote, out var existing) || !existing.IsMe)
                await message.AddReactionAsync(emote);
        }
    }

    /// <summary>
    ///     Rebuilds a posted message in place.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="message">The message to edit.</param>
    /// <param name="menu">The menu.</param>
    /// <returns>A task that completes when the edit is done.</returns>
    private async Task ModifyAsync(SocketGuild guild, ITextChannel channel, IUserMessage message, RoleMenu menu)
    {
        var (text, embeds, components) = BuildMessage(guild, channel, menu);
        await message.ModifyAsync(p =>
        {
            p.Content = text ?? "";
            p.Embeds = embeds ?? [];
            p.Components = components;
            p.AllowedMentions = AllowedMentions.None;
        });
        await SyncReactionsAsync(message, menu);
    }

    /// <summary>
    ///     Rebuilds a menu's message in place when it still exists.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="menu">The menu.</param>
    /// <returns>True when the message was edited, false when it is missing.</returns>
    private async Task<bool> EditInPlaceAsync(SocketGuild guild, RoleMenu menu)
    {
        if (menu.MessageId is not { } messageId)
            return false;
        var channel = guild.GetTextChannel(menu.ChannelId);
        if (channel is null)
            return false;
        var message = await FetchOwnMessageAsync(channel, messageId);
        if (message is null)
            return false;
        await ModifyAsync(guild, channel, message, menu);
        return true;
    }

    /// <summary>
    ///     Fetches a message when it exists and the bot wrote it.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="messageId">The message ID.</param>
    /// <returns>The message, or null when it is gone or not the bot's.</returns>
    private async Task<IUserMessage?> FetchOwnMessageAsync(ITextChannel channel, ulong messageId)
    {
        IMessage? message;
        try
        {
            message = await channel.GetMessageAsync(messageId);
        }
        catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return message is IUserMessage userMessage && userMessage.Author.Id == client.CurrentUser.Id
            ? userMessage
            : null;
    }

    /// <summary>
    ///     Deletes a message and ignores any error.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="messageId">The message ID.</param>
    /// <returns>A task that completes when the attempt is done.</returns>
    private async Task DeleteMessageQuietlyAsync(SocketGuild guild, ulong channelId, ulong messageId)
    {
        try
        {
            var channel = guild.GetTextChannel(channelId);
            if (channel is not null)
                await channel.DeleteMessageAsync(messageId);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not delete role menu message {MessageId}", messageId);
        }
    }

    #endregion

    #region Interactions

    /// <summary>
    ///     Handles a click on a menu button.
    /// </summary>
    /// <param name="component">The interaction.</param>
    /// <param name="menuId">The menu ID from the button.</param>
    /// <param name="optionId">The option ID from the button.</param>
    /// <returns>A task that completes when the member has been answered.</returns>
    public async Task HandleButtonAsync(SocketMessageComponent component, int menuId, int optionId)
    {
        if (component.User is not SocketGuildUser member)
            return;
        var guild = member.Guild;
        if (guild is null)
            return;

        var menu = await GetMenuAsync(menuId);
        if (menu is null || menu.GuildId != guild.Id)
        {
            await component.RespondAsync(strings.RolemenuGone(guild.Id), ephemeral: true);
            return;
        }

        var blocked = AccessProblem(guild, member, menu);
        if (blocked is not null)
        {
            await component.RespondAsync(blocked, ephemeral: true, allowedMentions: AllowedMentions.None);
            return;
        }

        var isPrivate = (RoleMenuReplyMode)menu.ReplyMode == RoleMenuReplyMode.Private;
        if (isPrivate)
            await component.DeferLoadingAsync(true);
        else
            await component.DeferAsync();

        var option = menu.Options.FirstOrDefault(o => o.Id == optionId);
        if (option is null)
        {
            await component.FollowupAsync(strings.RolemenuGone(guild.Id), ephemeral: true);
            return;
        }

        var outcome = await ApplyAsync(menu, member, [option]);
        await ReplyOutcomeAsync(component, guild.Id, isPrivate, outcome);
    }

    /// <summary>
    ///     Handles a pick on a menu dropdown.
    /// </summary>
    /// <param name="component">The interaction.</param>
    /// <param name="menuId">The menu ID from the dropdown.</param>
    /// <returns>A task that completes when the member has been answered.</returns>
    public async Task HandleSelectAsync(SocketMessageComponent component, int menuId)
    {
        if (component.User is not SocketGuildUser member)
            return;
        var guild = member.Guild;
        if (guild is null)
            return;

        var menu = await GetMenuAsync(menuId);
        if (menu is null || menu.GuildId != guild.Id)
        {
            await component.RespondAsync(strings.RolemenuGone(guild.Id), ephemeral: true);
            return;
        }

        var components = BuildComponents(guild, menu);
        await component.UpdateAsync(p => p.Components = components);

        var blocked = AccessProblem(guild, member, menu);
        if (blocked is not null)
        {
            await component.FollowupAsync(blocked, ephemeral: true, allowedMentions: AllowedMentions.None);
            return;
        }

        var picked = new List<RoleMenuOption>();
        foreach (var value in component.Data.Values ?? [])
        {
            if (!int.TryParse(value, out var optionId))
                continue;
            var option = menu.Options.FirstOrDefault(o => o.Id == optionId);
            if (option is not null && picked.All(p => p.Id != option.Id))
                picked.Add(option);
        }

        if (picked.Count == 0)
        {
            await component.FollowupAsync(strings.RolemenuGone(guild.Id), ephemeral: true);
            return;
        }

        var isPrivate = (RoleMenuReplyMode)menu.ReplyMode == RoleMenuReplyMode.Private;
        var outcome = await ApplyAsync(menu, member, picked);
        await ReplyOutcomeAsync(component, guild.Id, isPrivate, outcome);
    }

    /// <summary>
    ///     Gives or takes roles when a member adds or removes a reaction on a reaction menu.
    /// </summary>
    /// <param name="message">The message reacted to.</param>
    /// <param name="channel">The channel it is in.</param>
    /// <param name="reaction">The reaction.</param>
    /// <param name="trigger">Whether the reaction was added or removed.</param>
    /// <returns>A task that completes when the roles are updated.</returns>
    private async Task OnReactionAsync(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction, RoleMenuTrigger trigger)
    {
        try
        {
            if (reaction.UserId == client.CurrentUser.Id || !messageIndex.TryGetValue(message.Id, out var menuId) ||
                !menus.TryGetValue(menuId, out var menu) || (RoleMenuStyle)menu.Style != RoleMenuStyle.Reactions ||
                channel.Value is not SocketTextChannel textChannel)
                return;

            var guild = textChannel.Guild;
            var option = menu.Options.FirstOrDefault(o => ParseEmote(o.Emoji)?.Equals(reaction.Emote) == true);
            var member = guild.GetUser(reaction.UserId);
            if (member is null || member.IsBot)
                return;

            if (option is null)
            {
                if (trigger == RoleMenuTrigger.ReactionAdded && await message.GetOrDownloadAsync() is { } stray)
                    await stray.RemoveReactionAsync(reaction.Emote, member);
                return;
            }

            if (trigger == RoleMenuTrigger.ReactionAdded && AccessProblem(guild, member, menu) is not null)
            {
                if (await message.GetOrDownloadAsync() is { } blocked)
                    await blocked.RemoveReactionAsync(reaction.Emote, member);
                return;
            }

            if (trigger == RoleMenuTrigger.ReactionRemoved && AccessProblem(guild, member, menu) is not null)
                return;

            var outcome = await ApplyAsync(menu, member, [option], trigger);

            if (trigger != RoleMenuTrigger.ReactionAdded)
                return;

            var mode = (RoleMenuMode)menu.Mode;
            var refused = outcome.Added.Count == 0 && outcome.Notes.Count > 0 &&
                          mode is not (RoleMenuMode.Reverse or RoleMenuMode.RemoveOnly);
            if (!refused && mode != RoleMenuMode.Exclusive)
                return;

            if (await message.GetOrDownloadAsync() is not { } source)
                return;

            if (refused)
            {
                await source.RemoveReactionAsync(reaction.Emote, member);
                return;
            }

            foreach (var other in menu.Options.Where(o => o.Id != option.Id))
            {
                if (ParseEmote(other.Emoji) is { } otherEmote)
                    await source.RemoveReactionAsync(otherEmote, member);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Role menu reaction failed for message {MessageId}", message.Id);
        }
    }

    /// <summary>
    ///     Checks whether a member may use a menu right now.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="member">The member.</param>
    /// <param name="menu">The menu.</param>
    /// <returns>The text explaining why not, or null when they may.</returns>
    private string? AccessProblem(SocketGuild guild, SocketGuildUser member, RoleMenu menu)
    {
        if (!menu.Enabled || menu.ExpiresAt is { } expiresAt && expiresAt <= DateTime.UtcNow)
            return strings.RolemenuPaused(guild.Id);

        if (ParseRoleIds(menu.BlockedRoleIds).Any(id => member.Roles.Any(r => r.Id == id)))
            return strings.RolemenuBlocked(guild.Id);

        if (menu.RequiredRoleId is { } requiredId && guild.GetRole(requiredId) is { } required &&
            member.Roles.All(r => r.Id != required.Id))
            return strings.RolemenuRequiresRole(guild.Id, required.Mention);

        return null;
    }

    /// <summary>
    ///     Sends the private note for an outcome when the menu or the outcome calls for one.
    /// </summary>
    /// <param name="component">The interaction.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="isPrivate">Whether the menu uses private confirmations.</param>
    /// <param name="outcome">The outcome.</param>
    /// <returns>A task that completes when the note is sent.</returns>
    private async Task ReplyOutcomeAsync(SocketMessageComponent component, ulong guildId, bool isPrivate,
        RoleMenuOutcome outcome)
    {
        if (!isPrivate && outcome.Notes.Count == 0 && !outcome.Failed)
            return;

        var note = RenderOutcome(guildId, outcome);
        await component.FollowupAsync(note, ephemeral: true, allowedMentions: AllowedMentions.None);
    }

    /// <summary>
    ///     Turns an outcome into the lines shown to the member.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The note text.</returns>
    private string RenderOutcome(ulong guildId, RoleMenuOutcome outcome)
    {
        var lines = new List<string>();
        if (outcome.Added.Count > 0)
            lines.Add(strings.RolemenuAdded(guildId, RoleMentions(outcome.Added)));
        if (outcome.Removed.Count > 0)
            lines.Add(strings.RolemenuRemoved(guildId, RoleMentions(outcome.Removed)));
        lines.AddRange(outcome.Notes);
        if (lines.Count == 0)
            lines.Add(strings.RolemenuNoChange(guildId));
        return string.Join('\n', lines);
    }

    /// <summary>
    ///     Joins role mentions with commas.
    /// </summary>
    /// <param name="roleIds">The role IDs.</param>
    /// <returns>The mentions.</returns>
    public static string RoleMentions(IEnumerable<ulong> roleIds)
    {
        return string.Join(", ", roleIds.Select(MentionUtils.MentionRole));
    }

    /// <summary>
    ///     Downloads the guild's members when the cache is incomplete, so role holder counts are accurate.
    /// </summary>
    /// <param name="guild">The guild.</param>
    public static async Task EnsureMembersAsync(SocketGuild guild)
    {
        if (!guild.HasAllMembers)
            await guild.DownloadUsersAsync();
    }

    /// <summary>
    ///     Gives and takes roles for a member's pick, following the menu's mode and limits.
    /// </summary>
    /// <param name="menu">The menu.</param>
    /// <param name="member">The member.</param>
    /// <param name="picked">The options picked, in pick order.</param>
    /// <param name="trigger">What caused the change.</param>
    /// <returns>What changed and any notes.</returns>
    private async Task<RoleMenuOutcome> ApplyAsync(RoleMenu menu, SocketGuildUser member,
        IReadOnlyList<RoleMenuOption> picked, RoleMenuTrigger trigger = RoleMenuTrigger.Pick)
    {
        var guild = member.Guild;
        var outcome = new RoleMenuOutcome();
        var gate = memberLocks[(int)((guild.Id ^ member.Id) % MemberLockStripes)];

        await gate.WaitAsync();
        try
        {
            var current = CurrentRoleIds(guild.Id, member);
            var canManage = guild.CurrentUser.GuildPermissions.ManageRoles;
            var botTop = guild.CurrentUser.Hierarchy;

            bool UsableRole(ulong roleId)
            {
                var role = guild.GetRole(roleId);
                return canManage && role is not null && role.Id != guild.Id && !role.IsManaged &&
                       role.Position < botTop;
            }

            bool Usable(RoleMenuOption option)
            {
                return UsableRole(option.RoleId);
            }

            string Display(RoleMenuOption option)
            {
                return guild.GetRole(option.RoleId)?.Mention ?? option.Label;
            }

            var held = menu.Options
                .Where(o => current.Contains(o.RoleId) && guild.GetRole(o.RoleId) is not null)
                .ToList();

            var usable = new List<RoleMenuOption>();
            foreach (var option in picked)
            {
                if (Usable(option))
                    usable.Add(option);
                else
                    outcome.Notes.Add(strings.RolemenuRoleUnavailable(guild.Id, Display(option)));
            }

            if (usable.Any(o => o.MaxHolders > 0))
                await EnsureMembersAsync(guild);

            for (var i = usable.Count - 1; i >= 0; i--)
            {
                var option = usable[i];
                if (option.MaxHolders <= 0 || current.Contains(option.RoleId) ||
                    (RoleMenuMode)menu.Mode == RoleMenuMode.RemoveOnly || trigger == RoleMenuTrigger.ReactionRemoved)
                    continue;
                if (guild.GetRole(option.RoleId)!.Members.Count() < option.MaxHolders)
                    continue;

                usable.RemoveAt(i);
                outcome.Notes.Add(strings.RolemenuRoleFull(guild.Id, Display(option)));
            }

            var toAdd = new List<ulong>();
            var toRemove = new List<ulong>();
            var reverseSet = new List<ulong>();
            var reverseClear = new List<ulong>();

            if ((RoleMenuMode)menu.Mode == RoleMenuMode.Reverse)
            {
                HashSet<ulong> opted;
                await using (var db = await dbFactory.CreateConnectionAsync())
                {
                    var optionRoleIds = usable.Select(o => o.RoleId).ToList();
                    opted = (await db.RoleMenuReverseStates
                            .Where(x => x.MenuId == menu.Id && x.UserId == member.Id && optionRoleIds.Contains(x.RoleId))
                            .Select(x => x.RoleId)
                            .ToListAsync())
                        .ToHashSet();
                }

                foreach (var option in usable)
                {
                    var giveBack = trigger switch
                    {
                        RoleMenuTrigger.ReactionAdded => false,
                        RoleMenuTrigger.ReactionRemoved => true,
                        _ => opted.Contains(option.RoleId)
                    };

                    if (giveBack)
                    {
                        reverseClear.Add(option.RoleId);
                        if (!current.Contains(option.RoleId))
                            toAdd.Add(option.RoleId);
                    }
                    else
                    {
                        reverseSet.Add(option.RoleId);
                        if (current.Contains(option.RoleId))
                            toRemove.Add(option.RoleId);
                    }
                }
            }
            else if ((RoleMenuMode)menu.Mode == RoleMenuMode.Exclusive)
            {
                var pick = usable.FirstOrDefault();
                if (pick is not null)
                {
                    if (current.Contains(pick.RoleId))
                    {
                        if (trigger != RoleMenuTrigger.ReactionAdded)
                        {
                            if (menu.MinRoles >= 1)
                                outcome.Notes.Add(strings.RolemenuKeepOne(guild.Id));
                            else
                                toRemove.Add(pick.RoleId);
                        }
                    }
                    else if (trigger != RoleMenuTrigger.ReactionRemoved)
                    {
                        toAdd.Add(pick.RoleId);
                        foreach (var other in held.Where(h => h.RoleId != pick.RoleId))
                        {
                            if (Usable(other))
                                toRemove.Add(other.RoleId);
                            else
                                outcome.Notes.Add(strings.RolemenuRoleUnavailable(guild.Id, Display(other)));
                        }
                    }
                }
            }
            else
            {
                toAdd = usable.Where(o => !current.Contains(o.RoleId)).Select(o => o.RoleId).ToList();
                toRemove = usable.Where(o => current.Contains(o.RoleId)).Select(o => o.RoleId).ToList();

                switch ((RoleMenuMode)menu.Mode)
                {
                    case RoleMenuMode.Verify:
                        toRemove.Clear();
                        break;
                    case RoleMenuMode.RemoveOnly:
                        toAdd.Clear();
                        break;
                }

                switch (trigger)
                {
                    case RoleMenuTrigger.ReactionAdded when (RoleMenuMode)menu.Mode != RoleMenuMode.RemoveOnly:
                        toRemove.Clear();
                        break;
                    case RoleMenuTrigger.ReactionRemoved:
                        toAdd.Clear();
                        break;
                }

                if (menu.MaxRoles > 0)
                {
                    var room = menu.MaxRoles - (held.Count - toRemove.Count);
                    if (toAdd.Count > room)
                    {
                        var keep = Math.Max(room, 0);
                        var refused = toAdd.Skip(keep).ToList();
                        toAdd = toAdd.Take(keep).ToList();
                        outcome.Notes.Add(strings.RolemenuMaxReached(guild.Id, menu.MaxRoles, RoleMentions(refused)));
                    }
                }

                if (menu.MinRoles > 0 && held.Count - toRemove.Count + toAdd.Count < menu.MinRoles)
                {
                    var kept = new List<ulong>();
                    while (toRemove.Count > 0 && held.Count - toRemove.Count + toAdd.Count < menu.MinRoles)
                    {
                        kept.Insert(0, toRemove[^1]);
                        toRemove.RemoveAt(toRemove.Count - 1);
                    }

                    if (kept.Count > 0)
                        outcome.Notes.Add(strings.RolemenuMinReached(guild.Id, menu.MinRoles, RoleMentions(kept)));
                }
            }

            if (toAdd.Count > 0 && menu.GroupId is { } groupId && groups.TryGetValue(groupId, out var group) &&
                group.MaxRoles > 0)
            {
                var groupHeld = menus.Values
                    .Where(m => m.GroupId == groupId && m.GuildId == guild.Id)
                    .SelectMany(m => m.Options)
                    .Select(o => o.RoleId)
                    .Where(id => current.Contains(id) && guild.GetRole(id) is not null)
                    .Distinct()
                    .ToList();

                if (group.MaxRoles == 1)
                {
                    if (toAdd.Count > 1)
                    {
                        outcome.Notes.Add(strings.RolemenuMaxReached(guild.Id, 1, RoleMentions(toAdd.Skip(1))));
                        toAdd = toAdd.Take(1).ToList();
                    }

                    foreach (var roleId in groupHeld.Where(id => !toAdd.Contains(id) && !toRemove.Contains(id)))
                    {
                        if (UsableRole(roleId))
                            toRemove.Add(roleId);
                        else
                            outcome.Notes.Add(strings.RolemenuRoleUnavailable(guild.Id,
                                MentionUtils.MentionRole(roleId)));
                    }
                }
                else
                {
                    var room = group.MaxRoles - groupHeld.Count(id => !toRemove.Contains(id));
                    if (toAdd.Count > room)
                    {
                        var keep = Math.Max(room, 0);
                        outcome.Notes.Add(strings.RolemenuMaxReached(guild.Id, group.MaxRoles,
                            RoleMentions(toAdd.Skip(keep))));
                        toAdd = toAdd.Take(keep).ToList();
                    }
                }
            }

            try
            {
                var options = new RequestOptions
                {
                    AuditLogReason = strings.RolemenuAuditReason(guild.Id, menu.Name)
                };

                if (toRemove.Count > 0)
                {
                    try
                    {
                        await member.RemoveRolesAsync(toRemove, options);
                        outcome.Removed.AddRange(toRemove);
                    }
                    catch (HttpException ex) when (IsPermissionFailure(ex))
                    {
                        foreach (var id in toRemove)
                            outcome.Notes.Add(strings.RolemenuRoleUnavailable(guild.Id, MentionUtils.MentionRole(id)));
                    }
                }

                if (toAdd.Count > 0)
                {
                    try
                    {
                        await member.AddRolesAsync(toAdd, options);
                        outcome.Added.AddRange(toAdd);
                    }
                    catch (HttpException ex) when (IsPermissionFailure(ex))
                    {
                        foreach (var id in toAdd)
                            outcome.Notes.Add(strings.RolemenuRoleUnavailable(guild.Id, MentionUtils.MentionRole(id)));
                    }
                }

                await TrackTempGrantsAsync(menu, member, outcome);
                await SaveReverseStatesAsync(menu, member.Id,
                    reverseSet.Where(id => !current.Contains(id) || outcome.Removed.Contains(id)).ToList(),
                    reverseClear.Where(id => current.Contains(id) || outcome.Added.Contains(id)).ToList());
                collector.Feature(FeatureKey, guild.Id);
            }
            catch (Exception ex)
            {
                outcome.Failed = true;
                outcome.Notes.Add(strings.RolemenuFailed(guild.Id));
                logger.LogWarning(ex, "Role menu {MenuId} failed to update roles for {UserId} in {GuildId}",
                    menu.Id, member.Id, guild.Id);
                collector.Feature(FeatureKey, guild.Id, false, ex.GetType().Name);
            }

            RecordChange(guild.Id, member.Id, outcome.Added, outcome.Removed);
        }
        finally
        {
            gate.Release();
        }

        return outcome;
    }

    /// <summary>
    ///     Records which roles a member has made the first pick on and forgets the ones they picked again.
    /// </summary>
    /// <param name="menu">The reverse menu.</param>
    /// <param name="userId">The member's ID.</param>
    /// <param name="set">The roles taken by a first pick.</param>
    /// <param name="clear">The roles given back by a second pick.</param>
    /// <returns>A task that completes when the states are saved.</returns>
    private async Task SaveReverseStatesAsync(RoleMenu menu, ulong userId, IReadOnlyCollection<ulong> set,
        IReadOnlyCollection<ulong> clear)
    {
        if (set.Count == 0 && clear.Count == 0)
            return;

        await using var db = await dbFactory.CreateConnectionAsync();
        if (clear.Count > 0)
        {
            await db.RoleMenuReverseStates
                .Where(x => x.MenuId == menu.Id && x.UserId == userId && clear.Contains(x.RoleId))
                .DeleteAsync();
        }

        if (set.Count == 0)
            return;

        var existing = await db.RoleMenuReverseStates
            .Where(x => x.MenuId == menu.Id && x.UserId == userId && set.Contains(x.RoleId))
            .Select(x => x.RoleId)
            .ToListAsync();
        foreach (var roleId in set.Except(existing))
        {
            await db.InsertAsync(new RoleMenuReverseState
            {
                GuildId = menu.GuildId,
                UserId = userId,
                RoleId = roleId,
                MenuId = menu.Id,
                DateAdded = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    ///     Stores an expiry for each temporary role just given and forgets the ones just taken.
    /// </summary>
    /// <param name="menu">The menu.</param>
    /// <param name="member">The member.</param>
    /// <param name="outcome">What changed.</param>
    /// <returns>A task that completes when the grants are saved.</returns>
    private async Task TrackTempGrantsAsync(RoleMenu menu, SocketGuildUser member, RoleMenuOutcome outcome)
    {
        if (outcome.Added.Count == 0 && outcome.Removed.Count == 0)
            return;

        var now = DateTime.UtcNow;
        await using var db = await dbFactory.CreateConnectionAsync();
        var guildId = member.Guild.Id;

        if (outcome.Removed.Count > 0)
        {
            var removed = outcome.Removed.ToList();
            await db.RoleMenuTempGrants
                .Where(x => x.GuildId == guildId && x.UserId == member.Id && removed.Contains(x.RoleId))
                .DeleteAsync();
        }

        foreach (var roleId in outcome.Added)
        {
            var option = menu.Options.FirstOrDefault(o => o.RoleId == roleId);
            var seconds = option is { TempRoleSeconds: > 0 } ? option.TempRoleSeconds : menu.TempRoleSeconds;
            if (seconds <= 0)
                continue;

            await db.RoleMenuTempGrants
                .Where(x => x.GuildId == guildId && x.UserId == member.Id && x.RoleId == roleId)
                .DeleteAsync();
            await db.InsertAsync(new RoleMenuTempGrant
            {
                GuildId = guildId,
                UserId = member.Id,
                RoleId = roleId,
                MenuId = menu.Id,
                ExpiresAt = now.AddSeconds(seconds)
            });
        }
    }

    /// <summary>
    ///     Checks whether a Discord error means the bot lacks permission for a role.
    /// </summary>
    /// <param name="ex">The error.</param>
    /// <returns>True for a permission failure.</returns>
    private static bool IsPermissionFailure(HttpException ex)
    {
        return ex.DiscordCode == DiscordErrorCode.MissingPermissions;
    }

    /// <summary>
    ///     Gets a member's role IDs, overlaid with changes the bot made in the last few seconds.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="member">The member.</param>
    /// <returns>The role IDs.</returns>
    private HashSet<ulong> CurrentRoleIds(ulong guildId, SocketGuildUser member)
    {
        var ids = member.Roles.Select(r => r.Id).ToHashSet();
        var key = (guildId, member.Id);
        if (!recentChanges.TryGetValue(key, out var change))
            return ids;

        if (change.Expires < DateTime.UtcNow)
        {
            recentChanges.TryRemove(key, out _);
            return ids;
        }

        ids.UnionWith(change.Added);
        ids.ExceptWith(change.Removed);
        return ids;
    }

    /// <summary>
    ///     Remembers a role change so the next click sees it before the gateway cache does.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member ID.</param>
    /// <param name="added">Roles added.</param>
    /// <param name="removed">Roles removed.</param>
    private void RecordChange(ulong guildId, ulong userId, IReadOnlyCollection<ulong> added,
        IReadOnlyCollection<ulong> removed)
    {
        if (added.Count == 0 && removed.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var key = (guildId, userId);
        var addedSet = new HashSet<ulong>();
        var removedSet = new HashSet<ulong>();
        if (recentChanges.TryGetValue(key, out var previous) && previous.Expires >= now)
        {
            addedSet.UnionWith(previous.Added);
            removedSet.UnionWith(previous.Removed);
        }

        addedSet.ExceptWith(removed);
        removedSet.ExceptWith(added);
        addedSet.UnionWith(added);
        removedSet.UnionWith(removed);
        recentChanges[key] = new RecentRoleChange(addedSet, removedSet, now + RecentChangeWindow);

        if (recentChanges.Count <= 1000)
            return;
        foreach (var (staleKey, value) in recentChanges)
        {
            if (value.Expires < now)
                recentChanges.TryRemove(staleKey, out _);
        }
    }

    #endregion

    #region Expiry

    /// <summary>
    ///     Takes back expired temporary roles and shuts down expired menus until the service unloads.
    /// </summary>
    /// <returns>A task that completes when the loop stops.</returns>
    private async Task RunExpiryLoopAsync()
    {
        using var timer = new PeriodicTimer(ExpiryInterval);
        try
        {
            do
            {
                try
                {
                    await ExpireGrantsAsync();
                    await ExpireMenusAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Role menu expiry check failed");
                }
            } while (await timer.WaitForNextTickAsync(expiryCts.Token));
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    ///     Removes every temporary role that has run out.
    /// </summary>
    /// <returns>A task that completes when the batch is done.</returns>
    private async Task ExpireGrantsAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var due = await db.RoleMenuTempGrants
            .Where(x => x.ExpiresAt <= DateTime.UtcNow)
            .OrderBy(x => x.ExpiresAt)
            .ToListAsync();
        due = due.Where(x => ownership.IsMine(x.GuildId)).Take(200).ToList();

        foreach (var grant in due)
        {
            var done = true;
            var guild = client.GetGuild(grant.GuildId);
            var member = guild?.GetUser(grant.UserId);
            var role = guild?.GetRole(grant.RoleId);
            if (member is not null && role is not null && member.Roles.Any(r => r.Id == role.Id))
            {
                try
                {
                    await member.RemoveRoleAsync(role, new RequestOptions
                    {
                        AuditLogReason = strings.RolemenuTempExpiredReason(guild!.Id)
                    });
                    RecordChange(guild.Id, member.Id, [], [role.Id]);
                }
                catch (HttpException ex) when (ex.HttpCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                {
                    logger.LogDebug(ex, "Could not take back temporary role {RoleId} from {UserId}", role.Id,
                        member.Id);
                }
                catch (Exception ex)
                {
                    done = false;
                    logger.LogWarning(ex, "Failed to take back temporary role {RoleId} from {UserId}", role.Id,
                        member.Id);
                }
            }

            if (done)
                await db.RoleMenuTempGrants.Where(x => x.Id == grant.Id).DeleteAsync();
        }
    }

    /// <summary>
    ///     Deletes the message of every menu past its expiry and turns the menu off.
    /// </summary>
    /// <returns>A task that completes when the batch is done.</returns>
    private async Task ExpireMenusAsync()
    {
        var due = menus.Values
            .Where(x => x.ExpiresAt is { } at && at <= DateTime.UtcNow && ownership.IsMine(x.GuildId))
            .Select(x => x.Id)
            .ToList();

        foreach (var menuId in due)
        {
            await writeLock.WaitAsync();
            try
            {
                await using var db = await dbFactory.CreateConnectionAsync();
                var stored = await LoadMenuAsync(db, menuId);
                if (stored is null)
                {
                    menus.TryRemove(menuId, out _);
                    continue;
                }

                if (stored.ExpiresAt is not { } at || at > DateTime.UtcNow)
                {
                    CacheMenu(stored);
                    continue;
                }

                if (stored.MessageId is { } messageId)
                {
                    messageIndex.TryRemove(messageId, out _);
                    if (client.GetGuild(stored.GuildId) is { } guild)
                        await DeleteMessageQuietlyAsync(guild, stored.ChannelId, messageId);
                }

                stored.MessageId = null;
                stored.Enabled = false;
                stored.ExpiresAt = null;
                stored.DateModified = DateTime.UtcNow;
                await db.UpdateAsync(stored);
                CacheMenu(stored);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to expire role menu {MenuId}", menuId);
            }
            finally
            {
                writeLock.Release();
            }
        }
    }

    #endregion

    #region Cleanup

    /// <summary>
    ///     Removes a menu when its message is deleted by someone other than the bot.
    /// </summary>
    /// <param name="message">The deleted message.</param>
    /// <param name="channel">The channel it was in.</param>
    /// <returns>A task that completes when cleanup is done.</returns>
    private async Task OnMessageDeletedAsync(Cacheable<IMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel)
    {
        if (!messageIndex.TryRemove(message.Id, out var menuId))
            return;
        await RemoveForDeletedMessageAsync(menuId, message.Id);
    }

    /// <summary>
    ///     Removes menus whose messages were bulk deleted.
    /// </summary>
    /// <param name="messages">The deleted messages.</param>
    /// <param name="channel">The channel they were in.</param>
    /// <returns>A task that completes when cleanup is done.</returns>
    private async Task OnMessagesBulkDeletedAsync(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages,
        Cacheable<IMessageChannel, ulong> channel)
    {
        foreach (var message in messages)
        {
            if (messageIndex.TryRemove(message.Id, out var menuId))
                await RemoveForDeletedMessageAsync(menuId, message.Id);
        }
    }

    /// <summary>
    ///     Deletes a menu whose message was deleted.
    /// </summary>
    /// <param name="menuId">The menu ID.</param>
    /// <param name="messageId">The deleted message ID.</param>
    /// <returns>A task that completes when the row is gone.</returns>
    private async Task RemoveForDeletedMessageAsync(int menuId, ulong messageId)
    {
        await writeLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var groupId = menus.TryGetValue(menuId, out var cached) ? cached.GroupId : null;
            var deleted = await db.RoleMenus
                .Where(x => x.Id == menuId && x.MessageId == messageId)
                .DeleteAsync();
            if (deleted == 0)
                return;
            menus.TryRemove(menuId, out _);
            await PruneGroupAsync(db, groupId);
            logger.LogInformation("Role menu {MenuId} removed because its message was deleted", menuId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove role menu {MenuId} after its message was deleted", menuId);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    ///     Removes every menu in a deleted channel.
    /// </summary>
    /// <param name="channel">The deleted channel.</param>
    /// <returns>A task that completes when cleanup is done.</returns>
    private async Task OnChannelDestroyedAsync(SocketChannel channel)
    {
        if (channel is not SocketGuildChannel guildChannel)
            return;

        await writeLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var affected = await db.RoleMenus
                .Where(x => x.GuildId == guildChannel.Guild.Id && x.ChannelId == channel.Id)
                .Select(x => new
                {
                    x.Id, x.MessageId, x.GroupId
                })
                .ToListAsync();
            if (affected.Count == 0)
                return;

            var ids = affected.Select(x => x.Id).ToList();
            await db.RoleMenus.Where(x => ids.Contains(x.Id)).DeleteAsync();
            foreach (var item in affected)
            {
                if (item.MessageId is { } messageId)
                    messageIndex.TryRemove(messageId, out _);
                menus.TryRemove(item.Id, out _);
            }

            foreach (var groupId in affected.Select(x => x.GroupId).Distinct())
                await PruneGroupAsync(db, groupId);

            logger.LogInformation("Removed {Count} role menus because channel {ChannelId} was deleted",
                affected.Count, channel.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove role menus for deleted channel {ChannelId}", channel.Id);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    ///     Drops a deleted role from every menu that used it, rebuilding or unposting those menus.
    /// </summary>
    /// <param name="role">The deleted role.</param>
    /// <returns>A task that completes when cleanup is done.</returns>
    private async Task OnRoleDeletedAsync(SocketRole role)
    {
        var guild = role.Guild;
        await writeLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var optionMenus = await db.RoleMenuOptions
                .Where(x => x.RoleId == role.Id)
                .Select(x => x.RoleMenuId)
                .ToListAsync();
            var requiredMenus = await db.RoleMenus
                .Where(x => x.GuildId == guild.Id && x.RequiredRoleId == role.Id)
                .Select(x => x.Id)
                .ToListAsync();

            var guildMenuIds = await db.RoleMenus
                .Where(x => x.GuildId == guild.Id && optionMenus.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

            await db.RoleMenuTempGrants
                .Where(x => x.GuildId == guild.Id && x.RoleId == role.Id)
                .DeleteAsync();

            var blockingMenus = (await db.RoleMenus
                    .Where(x => x.GuildId == guild.Id && x.BlockedRoleIds != "")
                    .ToListAsync())
                .Where(x => ParseRoleIds(x.BlockedRoleIds).Contains(role.Id))
                .ToList();
            foreach (var blocking in blockingMenus)
            {
                var remaining = ParseRoleIds(blocking.BlockedRoleIds).Where(x => x != role.Id);
                await db.RoleMenus
                    .Where(x => x.Id == blocking.Id)
                    .Set(x => x.BlockedRoleIds, string.Join(',', remaining))
                    .UpdateAsync();
            }

            var affected = guildMenuIds.Concat(requiredMenus).Concat(blockingMenus.Select(x => x.Id)).Distinct()
                .ToList();
            if (affected.Count == 0)
                return;

            await db.RoleMenuOptions
                .Where(x => x.RoleId == role.Id && guildMenuIds.Contains(x.RoleMenuId))
                .DeleteAsync();
            await db.RoleMenus
                .Where(x => x.GuildId == guild.Id && x.RequiredRoleId == role.Id)
                .Set(x => x.RequiredRoleId, (ulong?)null)
                .UpdateAsync();

            foreach (var menuId in affected)
            {
                var menu = await LoadMenuAsync(db, menuId);
                if (menu is null)
                    continue;

                if (menu.Options.Count > 0)
                {
                    (menu.MinRoles, menu.MaxRoles) = ClampLimits((RoleMenuMode)menu.Mode, menu.MinRoles,
                        menu.MaxRoles, menu.Options.Count);
                    for (var i = 0; i < menu.Options.Count; i++)
                        menu.Options[i].Position = i;
                    foreach (var option in menu.Options)
                        await db.UpdateAsync(option);
                    menu.DateModified = DateTime.UtcNow;
                    await db.UpdateAsync(menu);

                    try
                    {
                        await EditInPlaceAsync(guild, menu);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Could not rebuild role menu {MenuId} after a role was deleted", menuId);
                    }
                }
                else if (menu.MessageId is { } messageId)
                {
                    messageIndex.TryRemove(messageId, out _);
                    await DeleteMessageQuietlyAsync(guild, menu.ChannelId, messageId);
                    menu.MessageId = null;
                    menu.DateModified = DateTime.UtcNow;
                    await db.UpdateAsync(menu);
                }

                CacheMenu(menu);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clean up role menus for deleted role {RoleId}", role.Id);
        }
        finally
        {
            writeLock.Release();
        }
    }

    #endregion

    #region Groups

    /// <summary>
    ///     Lists a guild's menu groups, ordered by name.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The groups.</returns>
    public List<RoleMenuGroup> GetGroups(ulong guildId)
    {
        return groups.Values.Where(x => x.GuildId == guildId).OrderBy(x => x.Name).ToList();
    }

    /// <summary>
    ///     Gets a group by name, creating it with a limit of one role when it does not exist yet.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The group name.</param>
    /// <returns>The group, or null when the name is blank or too long.</returns>
    public async Task<RoleMenuGroup?> EnsureGroupAsync(ulong guildId, string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Length > NameLength)
            return null;

        await writeLock.WaitAsync();
        try
        {
            var existing = groups.Values.FirstOrDefault(x =>
                x.GuildId == guildId && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                return existing;

            await using var db = await dbFactory.CreateConnectionAsync();
            var group = new RoleMenuGroup
            {
                GuildId = guildId, Name = name, MaxRoles = 1, DateAdded = DateTime.UtcNow
            };
            group.Id = await db.InsertWithInt32IdentityAsync(group);
            groups[group.Id] = group;
            return group;
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    ///     Sets how many roles a member can hold across a group's menus.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="groupId">The group ID.</param>
    /// <param name="maxRoles">The limit, zero for none.</param>
    /// <returns>The updated group, or null when it does not exist or the limit is negative.</returns>
    public async Task<RoleMenuGroup?> SetGroupMaxAsync(ulong guildId, int groupId, int maxRoles)
    {
        if (maxRoles < 0)
            return null;

        await writeLock.WaitAsync();
        try
        {
            if (!groups.TryGetValue(groupId, out var group) || group.GuildId != guildId)
                return null;

            await using var db = await dbFactory.CreateConnectionAsync();
            await db.RoleMenuGroups.Where(x => x.Id == groupId).Set(x => x.MaxRoles, maxRoles).UpdateAsync();
            group.MaxRoles = maxRoles;
            return group;
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    ///     Deletes a group and unlinks its menus.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="groupId">The group ID.</param>
    /// <returns>True when the group existed.</returns>
    public async Task<bool> DeleteGroupAsync(ulong guildId, int groupId)
    {
        await writeLock.WaitAsync();
        try
        {
            if (!groups.TryGetValue(groupId, out var group) || group.GuildId != guildId)
                return false;

            await using var db = await dbFactory.CreateConnectionAsync();
            await db.RoleMenus.Where(x => x.GroupId == groupId).Set(x => x.GroupId, (int?)null).UpdateAsync();
            await db.RoleMenuGroups.Where(x => x.Id == groupId).DeleteAsync();
            groups.TryRemove(groupId, out _);
            foreach (var menu in menus.Values.Where(x => x.GroupId == groupId))
                menu.GroupId = null;
            return true;
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    ///     Deletes a group if no menu uses it.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <returns>A task that completes when the check is done.</returns>
    public async Task PruneUnusedGroupAsync(int groupId)
    {
        await writeLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            await PruneGroupAsync(db, groupId);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    ///     Deletes a group once no menu uses it. Callers hold the write lock.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="groupId">The group ID, or null for none.</param>
    /// <returns>A task that completes when the check is done.</returns>
    private async Task PruneGroupAsync(MewdekoDb db, int? groupId)
    {
        if (groupId is not { } id || await db.RoleMenus.AnyAsync(x => x.GroupId == id))
            return;

        await db.RoleMenuGroups.Where(x => x.Id == id).DeleteAsync();
        groups.TryRemove(id, out _);
    }

    #endregion

    #region Cache

    /// <summary>
    ///     Loads one menu with its options sorted.
    /// </summary>
    /// <param name="db">The connection.</param>
    /// <param name="menuId">The menu ID.</param>
    /// <returns>The menu, or null.</returns>
    private static async Task<RoleMenu?> LoadMenuAsync(MewdekoDb db, int menuId)
    {
        var menu = await db.RoleMenus
            .LoadWith(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == menuId);
        if (menu is not null)
            SortOptions(menu);
        return menu;
    }

    /// <summary>
    ///     Sorts a menu's options by position, then ID.
    /// </summary>
    /// <param name="menu">The menu.</param>
    private static void SortOptions(RoleMenu menu)
    {
        menu.Options = (menu.Options ?? []).OrderBy(x => x.Position).ThenBy(x => x.Id).ToList();
    }

    /// <summary>
    ///     Stores a menu in the cache and indexes its message.
    /// </summary>
    /// <param name="menu">The menu.</param>
    private void CacheMenu(RoleMenu menu)
    {
        menus[menu.Id] = menu;
        if (menu.MessageId is { } messageId)
            messageIndex[messageId] = menu.Id;
    }

    #endregion

    #region Display

    /// <summary>
    ///     Gets the localized status word for a menu.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="status">The status.</param>
    /// <returns>The text.</returns>
    public string StatusText(ulong guildId, RoleMenuStatus status)
    {
        return status switch
        {
            RoleMenuStatus.Live => strings.RolemenuStatusLive(guildId),
            RoleMenuStatus.Paused => strings.RolemenuStatusPaused(guildId),
            RoleMenuStatus.NotPosted => strings.RolemenuStatusNotPosted(guildId),
            _ => strings.RolemenuStatusChannelMissing(guildId)
        };
    }

    /// <summary>
    ///     Gets the localized name of a style.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="style">The style.</param>
    /// <returns>The text.</returns>
    public string StyleText(ulong guildId, RoleMenuStyle style)
    {
        return style switch
        {
            RoleMenuStyle.Buttons => strings.RolemenuStyleButtons(guildId),
            RoleMenuStyle.Reactions => strings.RolemenuStyleReactions(guildId),
            _ => strings.RolemenuStyleDropdown(guildId)
        };
    }

    /// <summary>
    ///     Gets the localized name of a mode.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The text.</returns>
    public string ModeText(ulong guildId, RoleMenuMode mode)
    {
        return mode switch
        {
            RoleMenuMode.Exclusive => strings.RolemenuModeOne(guildId),
            RoleMenuMode.Verify => strings.RolemenuModeVerify(guildId),
            RoleMenuMode.RemoveOnly => strings.RolemenuModeRemoveOnly(guildId),
            RoleMenuMode.Reverse => strings.RolemenuModeReverse(guildId),
            _ => strings.RolemenuModeAny(guildId)
        };
    }

    /// <summary>
    ///     Gets the localized text for a duration in seconds, or the off text.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="seconds">The duration, zero for none.</param>
    /// <returns>The text.</returns>
    public string DurationText(ulong guildId, int seconds)
    {
        return seconds <= 0 ? strings.RolemenuOff(guildId) : TimeSpan.FromSeconds(seconds).Humanize(3);
    }

    /// <summary>
    ///     Gets the localized name of a reply mode.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="mode">The reply mode.</param>
    /// <returns>The text.</returns>
    public string ReplyText(ulong guildId, RoleMenuReplyMode mode)
    {
        return mode == RoleMenuReplyMode.Silent
            ? strings.RolemenuReplySilent(guildId)
            : strings.RolemenuReplyPrivate(guildId);
    }

    /// <summary>
    ///     Gets the localized maximum for a menu, or the no limit text.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="maxRoles">The maximum, zero for no limit.</param>
    /// <returns>The text.</returns>
    public string MaxText(ulong guildId, int maxRoles)
    {
        return maxRoles == 0 ? strings.RolemenuNoLimit(guildId) : maxRoles.ToString();
    }

    /// <summary>
    ///     Formats an RGB value as hex text.
    /// </summary>
    /// <param name="rgb">The colour value.</param>
    /// <returns>Text like #5865F2.</returns>
    public static string ColorText(int rgb)
    {
        return $"#{rgb & 0xFFFFFF:X6}";
    }

    /// <summary>
    ///     Parses a colour typed by a person into an RGB value.
    /// </summary>
    /// <param name="input">A name, hex code, or RGB text.</param>
    /// <param name="rgb">The RGB value when parsing worked.</param>
    /// <returns>True when the input is a colour.</returns>
    public static bool TryParseAccent(string? input, out int rgb)
    {
        rgb = 0;
        if (string.IsNullOrWhiteSpace(input) || !ColorUtils.TryParseColor(input, out var color))
            return false;

        rgb = (int)(color.RawValue & 0xFFFFFF);
        return true;
    }

    /// <summary>
    ///     Gets the name of a button colour.
    /// </summary>
    /// <param name="color">The button colour.</param>
    /// <returns>The text.</returns>
    public static string ButtonColorText(RoleMenuButtonColor color)
    {
        return color.ToString();
    }

    /// <summary>
    ///     Creates a text channel for role menus: members can read, react and click, the bot can post.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="name">The channel name.</param>
    /// <param name="category">The category to put it in, or null for none.</param>
    /// <returns>The channel, or null when Discord refused.</returns>
    public async Task<ITextChannel?> CreateMenuChannelAsync(SocketGuild guild, string name, ICategoryChannel? category)
    {
        try
        {
            return await guild.CreateTextChannelAsync(name, props =>
            {
                props.CategoryId = category?.Id;
                props.PermissionOverwrites = new List<Overwrite>
                {
                    new(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(
                        viewChannel: PermValue.Allow,
                        readMessageHistory: PermValue.Allow,
                        addReactions: PermValue.Allow,
                        sendMessages: PermValue.Deny,
                        sendMessagesInThreads: PermValue.Deny,
                        createPublicThreads: PermValue.Deny,
                        createPrivateThreads: PermValue.Deny)),
                    new(guild.CurrentUser.Id, PermissionTarget.User, new OverwritePermissions(
                        viewChannel: PermValue.Allow,
                        sendMessages: PermValue.Allow,
                        embedLinks: PermValue.Allow,
                        addReactions: PermValue.Allow,
                        readMessageHistory: PermValue.Allow,
                        manageMessages: PermValue.Allow))
                };
            });
        }
        catch (HttpException ex)
        {
            logger.LogWarning(ex, "Failed to create a role menu channel in {GuildId}", guild.Id);
            return null;
        }
    }

    /// <summary>
    ///     Explains a refused write for command replies.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="result">The refused result.</param>
    /// <param name="menuId">The menu ID, when there is one.</param>
    /// <param name="channelId">The channel involved, when there is one.</param>
    /// <returns>The localized text.</returns>
    public string DescribeError(ulong guildId, RoleMenuResult result, int? menuId = null, ulong? channelId = null)
    {
        var detail = result.Detail ?? "";
        var channelMention = channelId is { } id ? MentionUtils.MentionChannel(id) : detail;
        return result.Error switch
        {
            RoleMenuError.NotFound => strings.RolemenuNotFound(guildId, menuId?.ToString() ?? detail),
            RoleMenuError.NameInvalid => strings.RolemenuNameInvalid(guildId),
            RoleMenuError.NoOptions => strings.RolemenuNeedsOption(guildId),
            RoleMenuError.TooManyOptions => strings.RolemenuTooManyOptions(guildId),
            RoleMenuError.DuplicateRole => strings.RolemenuOptionExists(guildId, detail),
            RoleMenuError.RoleMissing => strings.RolemenuRoleNotAssignable(guildId, detail),
            RoleMenuError.RoleNotAssignable => strings.RolemenuRoleNotAssignable(guildId, detail),
            RoleMenuError.RoleAboveActor => strings.RolemenuRoleAboveYou(guildId, detail),
            RoleMenuError.InvalidEmoji => strings.RolemenuInvalidEmoji(guildId, detail),
            RoleMenuError.InvalidChannel when result.Detail is null => strings.RolemenuChannelInvalid(guildId),
            RoleMenuError.InvalidChannel => strings.RolemenuPostFailed(guildId, channelMention),
            RoleMenuError.PostFailed => strings.RolemenuPostFailed(guildId, channelMention),
            RoleMenuError.TextTooLong => strings.RolemenuMessageTooLong(guildId),
            RoleMenuError.InvalidRequiredRole => strings.RolemenuRoleNotAssignable(guildId, detail),
            RoleMenuError.InvalidDuration => strings.RolemenuDurationInvalid(guildId),
            RoleMenuError.ImportSourceNotFound => strings.RolemenuImportNotFound(guildId, detail),
            RoleMenuError.ImportNoRoles => strings.RolemenuImportNoRoles(guildId),
            RoleMenuError.InvalidGroup => strings.RolemenuGroupInvalid(guildId),
            RoleMenuError.InvalidColor => strings.RolemenuColorInvalid(guildId),
            RoleMenuError.InvalidReactions => strings.RolemenuReactionsInvalid(guildId, MaxReactionOptions),
            RoleMenuError.TemplateNotFound => strings.RolemenuTemplateExpired(guildId),
            RoleMenuError.RolesNotCreated => strings.RolemenuTemplateRolesFailed(guildId),
            _ => strings.RolemenuFailed(guildId)
        };
    }

    #region Templates

    /// <summary>
    ///     Matches the colour at the end of a template role line, such as <c>#E74C3C</c>.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex TemplateColorRegex = new(@"\s#([0-9A-Fa-f]{6})$");

    /// <summary>
    ///     How long a template draft stays editable.
    /// </summary>
    private static readonly TimeSpan TemplateLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    ///     Template drafts being edited, one per person per server.
    /// </summary>
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), RoleMenuTemplateSession> templateSessions =
        new();

    /// <summary>
    ///     Starts a template draft for a person, replacing any draft they already had.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The person's ID.</param>
    /// <param name="kind">The template to start from.</param>
    /// <param name="channelId">The channel to post the menu in.</param>
    /// <param name="style">The style to use, or null for the template's own.</param>
    /// <returns>The draft.</returns>
    public RoleMenuTemplateSession StartTemplate(ulong guildId, ulong userId, RoleMenuTemplateKind kind,
        ulong channelId, RoleMenuStyle? style)
    {
        var now = DateTime.UtcNow;
        foreach (var stale in templateSessions.Where(x => x.Value.Expires <= now).Select(x => x.Key).ToList())
            templateSessions.TryRemove(stale, out _);

        var template = RoleMenuTemplates.Get(kind);
        var session = new RoleMenuTemplateSession
        {
            Template = template,
            ChannelId = channelId,
            Style = style ?? template.Style,
            Name = template.Name,
            Entries = [..template.Entries],
            Expires = now + TemplateLifetime
        };
        templateSessions[(guildId, userId)] = session;
        return session;
    }

    /// <summary>
    ///     Gets a person's template draft when it has not expired.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The person's ID.</param>
    /// <returns>The draft, or null.</returns>
    public RoleMenuTemplateSession? GetTemplateSession(ulong guildId, ulong userId)
    {
        if (!templateSessions.TryGetValue((guildId, userId), out var session))
            return null;
        if (session.Expires > DateTime.UtcNow)
            return session;

        templateSessions.TryRemove((guildId, userId), out _);
        return null;
    }

    /// <summary>
    ///     Throws away a person's template draft.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The person's ID.</param>
    public void DropTemplateSession(ulong guildId, ulong userId)
    {
        templateSessions.TryRemove((guildId, userId), out _);
    }

    /// <summary>
    ///     Writes a draft's roles as text, one per line, the way the edit box shows them.
    /// </summary>
    /// <param name="entries">The roles.</param>
    /// <returns>The text.</returns>
    public static string EntriesToText(IEnumerable<RoleMenuTemplateEntry> entries)
    {
        return string.Join('\n', entries.Select(e =>
            (e.Emoji.Length == 0 ? "" : e.Emoji + " ") + e.Label +
            (e.Color is { } rgb ? " #" + rgb.ToString("X6") : "")));
    }

    /// <summary>
    ///     Reads roles from text with one per line as an optional emoji, a name, and an optional <c>#RRGGBB</c> colour.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="entries">The roles when the text is valid.</param>
    /// <returns>True when every line could be read and there is at least one role.</returns>
    public static bool TryParseEntries(string? text, out List<RoleMenuTemplateEntry> entries)
    {
        entries = [];
        foreach (var raw in (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var line = raw;
            int? color = null;
            var match = TemplateColorRegex.Match(line);
            if (match.Success)
            {
                color = Convert.ToInt32(match.Groups[1].Value, 16);
                line = line[..match.Index].TrimEnd();
            }

            var emoji = "";
            var parts = line.Split(' ', 2, StringSplitOptions.TrimEntries);
            if (parts.Length > 0 && parts[0].TryToIEmote(out _))
            {
                emoji = parts[0];
                line = parts.Length > 1 ? parts[1] : "";
            }

            var label = line.Trim().TrimTo(LabelLength);
            if (label.Length == 0)
                return false;

            entries.Add(new RoleMenuTemplateEntry(label, emoji, color));
        }

        return entries.Count > 0;
    }

    /// <summary>
    ///     Builds the preview embed for a template draft.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="session">The draft.</param>
    /// <returns>The embed.</returns>
    public EmbedBuilder BuildTemplateEmbed(ulong guildId, RoleMenuTemplateSession session)
    {
        return new EmbedBuilder()
            .WithOkColor()
            .WithTitle(strings.RolemenuTemplateTitle(guildId, session.Name).TrimTo(256))
            .WithDescription(strings.RolemenuTemplateHint(guildId))
            .AddField(strings.RolemenuInfoChannel(guildId), MentionUtils.MentionChannel(session.ChannelId), true)
            .AddField(strings.RolemenuInfoType(guildId), StyleText(guildId, session.Style), true)
            .AddField(strings.RolemenuInfoPicking(guildId), ModeText(guildId, session.Template.Mode), true)
            .AddField(strings.RolemenuInfoOptions(guildId), EntriesToText(session.Entries).TrimTo(1024));
    }

    /// <summary>
    ///     Builds the post, edit, and cancel buttons for a template draft.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The person the draft belongs to.</param>
    /// <returns>The buttons.</returns>
    public MessageComponent BuildTemplateComponents(ulong guildId, ulong userId)
    {
        return new ComponentBuilder()
            .WithButton(strings.RolemenuTemplatePost(guildId), $"rolemenu:tpl:post:{userId}", ButtonStyle.Success)
            .WithButton(strings.RolemenuTemplateEdit(guildId), $"rolemenu:tpl:edit:{userId}")
            .WithButton(strings.RolemenuTemplateCancel(guildId), $"rolemenu:tpl:cancel:{userId}", ButtonStyle.Danger)
            .Build();
    }

    /// <summary>
    ///     Applies an edit to a template draft.
    /// </summary>
    /// <param name="session">The draft.</param>
    /// <param name="name">The new menu name.</param>
    /// <param name="message">The new message above the menu, empty for the default.</param>
    /// <param name="entriesText">The roles, one per line.</param>
    /// <returns>True when the edit was valid and applied.</returns>
    public bool TryEditTemplate(RoleMenuTemplateSession session, string? name, string? message, string? entriesText)
    {
        var maxEntries = session.Style == RoleMenuStyle.Reactions ? MaxReactionOptions : MaxOptions;
        if (!TryParseEntries(entriesText, out var entries) || entries.Count > maxEntries)
            return false;

        var trimmedName = name?.Trim();
        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > NameLength)
            return false;

        session.Name = trimmedName;
        session.Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        session.Entries = entries;
        return true;
    }

    /// <summary>
    ///     Creates a template draft's roles and posts its menu. The roles are deleted again if the menu can't be posted.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="actor">The person posting the template.</param>
    /// <param name="session">The draft.</param>
    /// <returns>The posted menu or the reason it was refused.</returns>
    public async Task<RoleMenuResult> PostTemplateAsync(SocketGuild guild, IGuildUser actor,
        RoleMenuTemplateSession session)
    {
        var created = new List<IRole>();
        try
        {
            var options = new RequestOptions
            {
                AuditLogReason = strings.RolemenuAuditReason(guild.Id, session.Name)
            };

            foreach (var entry in session.Entries)
            {
                created.Add(await guild.CreateRoleAsync(entry.Label, GuildPermissions.None,
                    entry.Color is { } rgb ? new Color((uint)rgb) : null, false, session.Template.Mentionable,
                    options));
            }
        }
        catch (HttpException)
        {
            await DeleteRolesQuietlyAsync(created);
            return RoleMenuResult.Fail(RoleMenuError.RolesNotCreated);
        }

        for (var attempt = 0; attempt < 50 && created.Any(r => guild.GetRole(r.Id) is null); attempt++)
            await Task.Delay(100);

        var draft = new RoleMenuDraft
        {
            Name = session.Name,
            ChannelId = session.ChannelId,
            Message = session.Message,
            Style = session.Style,
            Mode = session.Template.Mode,
            ReplyMode = RoleMenuReplyMode.Private,
            Options = session.Entries
                .Select((entry, i) => new RoleMenuOptionDraft
                {
                    RoleId = created[i].Id,
                    Label = entry.Label,
                    Emoji = entry.Emoji.Length == 0 ? null : entry.Emoji
                })
                .ToList()
        };

        var result = await CreateAsync(guild.Id, actor, draft);
        if (result.Success)
            DropTemplateSession(guild.Id, actor.Id);
        else
            await DeleteRolesQuietlyAsync(created);
        return result;
    }

    /// <summary>
    ///     Deletes roles, ignoring any that can't be deleted.
    /// </summary>
    /// <param name="roles">The roles.</param>
    /// <returns>A task that completes when every delete was tried.</returns>
    private async Task DeleteRolesQuietlyAsync(IEnumerable<IRole> roles)
    {
        foreach (var role in roles)
        {
            try
            {
                await role.DeleteAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not delete template role {RoleId}", role.Id);
            }
        }
    }

    #endregion

    /// <summary>
    ///     Builds the list embed for a guild's menus.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="list">The menus.</param>
    /// <returns>The embed.</returns>
    public EmbedBuilder BuildListEmbed(SocketGuild guild, IReadOnlyList<RoleMenu> list)
    {
        var lines = list.Select(menu => strings.RolemenuListEntry(guild.Id, menu.Id, menu.Name,
            MentionUtils.MentionChannel(menu.ChannelId), StyleText(guild.Id, (RoleMenuStyle)menu.Style),
            menu.Options.Count, StatusText(guild.Id, GetStatus(guild, menu))));
        var title = strings.RolemenuListTitle(guild.Id);
        var body = string.Join('\n', lines).TrimTo(4096);
        return new EmbedBuilder().WithOkColor(guild.Id).WithTitle(title).WithDescription(body);
    }

    /// <summary>
    ///     Builds the list embed for a guild's menu groups.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="list">The groups.</param>
    /// <returns>The embed.</returns>
    public EmbedBuilder BuildGroupListEmbed(SocketGuild guild, IReadOnlyList<RoleMenuGroup> list)
    {
        var lines = list.Select(group => strings.RolemenuGroupListEntry(guild.Id, group.Name,
            MaxText(guild.Id, group.MaxRoles),
            string.Join(", ", menus.Values.Where(m => m.GroupId == group.Id).OrderBy(m => m.Id).Select(m => m.Id))));
        return new EmbedBuilder()
            .WithOkColor(guild.Id)
            .WithTitle(strings.RolemenuGroupListTitle(guild.Id))
            .WithDescription(string.Join('\n', lines).TrimTo(4096));
    }

    /// <summary>
    ///     Builds the info embed for one menu.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="menu">The menu.</param>
    /// <returns>The embed.</returns>
    public EmbedBuilder BuildInfoEmbed(SocketGuild guild, RoleMenu menu)
    {
        var guildId = guild.Id;
        var status = GetStatus(guild, menu);
        var required = menu.RequiredRoleId is { } requiredId
            ? MentionUtils.MentionRole(requiredId)
            : strings.RolemenuInfoAnyone(guildId);
        var blocked = ParseRoleIds(menu.BlockedRoleIds);
        var blockedText = blocked.Count == 0 ? strings.RolemenuOff(guildId) : RoleMentions(blocked);
        var expiresText = menu.ExpiresAt is { } expiresAt
            ? TimestampTag.FromDateTime(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc), TimestampTagStyles.Relative)
                .ToString()
            : strings.RolemenuOff(guildId);
        var optionLines = menu.Options.Select(o =>
            (o.Emoji is null ? "" : o.Emoji + " ") + o.Label + " " + MentionUtils.MentionRole(o.RoleId) +
            (o.TempRoleSeconds > 0 ? " " + strings.RolemenuInfoOptionTemp(guildId, DurationText(guildId, o.TempRoleSeconds)) : "") +
            (o.MaxHolders > 0 ? " " + strings.RolemenuInfoOptionCap(guildId, o.MaxHolders) : ""));
        var optionsText = string.Join('\n', optionLines).TrimTo(1024);
        if (string.IsNullOrWhiteSpace(optionsText))
            optionsText = strings.RolemenuNeedsOption(guildId);
        var groupText = menu.GroupId is { } groupId && groups.TryGetValue(groupId, out var group)
            ? strings.RolemenuInfoGroupValue(guildId, group.Name, MaxText(guildId, group.MaxRoles))
            : strings.RolemenuOff(guildId);
        var colorText = menu.AccentColor is { } accent ? ColorText(accent) : strings.RolemenuOff(guildId);
        var buttonColorText = menu.ButtonColor == 0
            ? strings.RolemenuOff(guildId)
            : ButtonColorText((RoleMenuButtonColor)menu.ButtonColor);
        var messageText = GetJumpUrl(menu) ?? StatusText(guildId, RoleMenuStatus.NotPosted);
        var title = strings.RolemenuInfoTitle(guildId, menu.Name, menu.Id).TrimTo(256);

        return new EmbedBuilder()
            .WithOkColor(guildId)
            .WithTitle(title)
            .AddField(strings.RolemenuInfoChannel(guildId), MentionUtils.MentionChannel(menu.ChannelId), true)
            .AddField(strings.RolemenuInfoType(guildId), StyleText(guildId, (RoleMenuStyle)menu.Style), true)
            .AddField(strings.RolemenuInfoPicking(guildId), ModeText(guildId, (RoleMenuMode)menu.Mode), true)
            .AddField(strings.RolemenuInfoLimits(guildId),
                strings.RolemenuInfoLimitsValue(guildId, menu.MinRoles, MaxText(guildId, menu.MaxRoles)), true)
            .AddField(strings.RolemenuInfoRequired(guildId), required, true)
            .AddField(strings.RolemenuInfoBlocked(guildId), blockedText.TrimTo(1024), true)
            .AddField(strings.RolemenuInfoTemp(guildId), DurationText(guildId, menu.TempRoleSeconds), true)
            .AddField(strings.RolemenuInfoExpires(guildId), expiresText, true)
            .AddField(strings.RolemenuInfoGroup(guildId), groupText, true)
            .AddField(strings.RolemenuInfoColor(guildId), colorText, true)
            .AddField(strings.RolemenuInfoButtonColor(guildId), buttonColorText, true)
            .AddField(strings.RolemenuInfoConfirmation(guildId),
                ReplyText(guildId, (RoleMenuReplyMode)menu.ReplyMode), true)
            .AddField(strings.RolemenuInfoStatus(guildId), StatusText(guildId, status), true)
            .AddField(strings.RolemenuInfoOptions(guildId), optionsText)
            .AddField(strings.RolemenuInfoMessage(guildId), messageText);
    }

    #endregion

    /// <summary>
    ///     A role change the bot made for a member, trusted until it expires.
    /// </summary>
    /// <param name="Added">Roles added.</param>
    /// <param name="Removed">Roles removed.</param>
    /// <param name="Expires">When the record stops being trusted.</param>
    private sealed record RecentRoleChange(HashSet<ulong> Added, HashSet<ulong> Removed, DateTime Expires);
}
