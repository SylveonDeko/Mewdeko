using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using DataModel;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.RoleMenus.Common;
using Mewdeko.Services.Analytics;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.RoleMenus.Services;

/// <summary>
///     Stores role menus, posts and edits their messages, and gives and takes roles when members use them.
/// </summary>
public class RoleMenuService : INService, IReadyExecutor, IUnloadableService
{
    /// <summary>
    ///     Most menus one server can have.
    /// </summary>
    public const int MaxMenusPerGuild = 50;

    /// <summary>
    ///     Most options one menu can have: one dropdown of 25, or 25 buttons in 5 rows.
    /// </summary>
    public const int MaxOptions = 25;

    /// <summary>
    ///     Buttons per row on a buttons menu.
    /// </summary>
    public const int ButtonsPerRow = 5;

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
    ///     Stripes that serialize role changes for one member.
    /// </summary>
    private readonly SemaphoreSlim[] memberLocks;

    /// <summary>
    ///     Every menu with its options sorted, by menu ID.
    /// </summary>
    private readonly ConcurrentDictionary<int, RoleMenu> menus = new();

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
    public RoleMenuService(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        GeneratedBotStrings strings,
        EventHandler eventHandler,
        RoleCommandsService roleCommands,
        GuildSettingsService guildSettings,
        IAnalyticsCollector collector,
        ILogger<RoleMenuService> logger)
    {
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

        eventHandler.Subscribe("MessageDeleted", EventModuleName, messageDeletedHandler);
        eventHandler.Subscribe("MessagesBulkDeleted", EventModuleName, messagesBulkDeletedHandler);
        eventHandler.Subscribe("ChannelDestroyed", EventModuleName, channelDestroyedHandler);
        eventHandler.Subscribe("RoleDeleted", EventModuleName, roleDeletedHandler);
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

            logger.LogInformation("Loaded {Count} role menus", all.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load role menus");
        }
    }

    /// <summary>
    ///     Unsubscribes from the cleanup events.
    /// </summary>
    /// <returns>A completed task.</returns>
    public Task Unload()
    {
        eventHandler.Unsubscribe("MessageDeleted", EventModuleName, messageDeletedHandler);
        eventHandler.Unsubscribe("MessagesBulkDeleted", EventModuleName, messagesBulkDeletedHandler);
        eventHandler.Unsubscribe("ChannelDestroyed", EventModuleName, channelDestroyedHandler);
        eventHandler.Unsubscribe("RoleDeleted", EventModuleName, roleDeletedHandler);
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

        (draft.MinRoles, draft.MaxRoles) = ClampLimits(draft.Mode, draft.MinRoles, draft.MaxRoles,
            draft.Options.Count);

        if (draft.RequiredRoleId is 0)
            draft.RequiredRoleId = null;
        if (draft.RequiredRoleId is { } requiredId &&
            (requiredId == guild.Id || guild.GetRole(requiredId) is null))
            return (RoleMenuError.InvalidRequiredRole, requiredId.ToString());

        return (RoleMenuError.None, null);
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

            var count = await db.RoleMenus.CountAsync(x => x.GuildId == guildId);
            if (count >= MaxMenusPerGuild)
                return RoleMenuResult.Fail(RoleMenuError.TooManyMenus, MaxMenusPerGuild.ToString());

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
        return new EmbedBuilder()
            .WithOkColor()
            .WithTitle(title)
            .WithDescription(description)
            .Build();
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
        if (options.Count == 0)
            return builder.Build();

        if ((RoleMenuStyle)menu.Style == RoleMenuStyle.Buttons)
        {
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var button = new ButtonBuilder()
                    .WithLabel(option.Label)
                    .WithCustomId($"rolemenu:btn:{menu.Id}:{option.Id}")
                    .WithStyle(ToButtonStyle(option.ButtonStyle))
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
        return await channel.SendMessageAsync(text, embeds: embeds, components: components,
            allowedMentions: AllowedMentions.None);
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
    ///     Checks whether a member may use a menu right now.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="member">The member.</param>
    /// <param name="menu">The menu.</param>
    /// <returns>The text explaining why not, or null when they may.</returns>
    private string? AccessProblem(SocketGuild guild, SocketGuildUser member, RoleMenu menu)
    {
        if (!menu.Enabled)
            return strings.RolemenuPaused(guild.Id);

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
    ///     Gives and takes roles for a member's pick, following the menu's mode and limits.
    /// </summary>
    /// <param name="menu">The menu.</param>
    /// <param name="member">The member.</param>
    /// <param name="picked">The options picked, in pick order.</param>
    /// <returns>What changed and any notes.</returns>
    private async Task<RoleMenuOutcome> ApplyAsync(RoleMenu menu, SocketGuildUser member,
        IReadOnlyList<RoleMenuOption> picked)
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

            bool Usable(RoleMenuOption option)
            {
                var role = guild.GetRole(option.RoleId);
                return canManage && role is not null && role.Id != guild.Id && !role.IsManaged &&
                       role.Position < botTop;
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

            var toAdd = new List<ulong>();
            var toRemove = new List<ulong>();

            if ((RoleMenuMode)menu.Mode == RoleMenuMode.Exclusive)
            {
                var pick = usable.FirstOrDefault();
                if (pick is not null)
                {
                    if (current.Contains(pick.RoleId))
                    {
                        if (menu.MinRoles >= 1)
                            outcome.Notes.Add(strings.RolemenuKeepOne(guild.Id));
                        else
                            toRemove.Add(pick.RoleId);
                    }
                    else
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
            var deleted = await db.RoleMenus
                .Where(x => x.Id == menuId && x.MessageId == messageId)
                .DeleteAsync();
            if (deleted == 0)
                return;
            menus.TryRemove(menuId, out _);
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
                    x.Id, x.MessageId
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

            var affected = guildMenuIds.Concat(requiredMenus).Distinct().ToList();
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
        return style == RoleMenuStyle.Buttons
            ? strings.RolemenuStyleButtons(guildId)
            : strings.RolemenuStyleDropdown(guildId);
    }

    /// <summary>
    ///     Gets the localized name of a mode.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The text.</returns>
    public string ModeText(ulong guildId, RoleMenuMode mode)
    {
        return mode == RoleMenuMode.Exclusive
            ? strings.RolemenuModeOne(guildId)
            : strings.RolemenuModeAny(guildId);
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
            RoleMenuError.TooManyMenus => strings.RolemenuTooManyMenus(guildId, MaxMenusPerGuild),
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
            RoleMenuError.ImportSourceNotFound => strings.RolemenuImportNotFound(guildId, detail),
            RoleMenuError.ImportNoRoles => strings.RolemenuImportNoRoles(guildId),
            _ => strings.RolemenuFailed(guildId)
        };
    }

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
        return new EmbedBuilder().WithOkColor().WithTitle(title).WithDescription(body);
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
        var optionLines = menu.Options.Select(o =>
            (o.Emoji is null ? "" : o.Emoji + " ") + o.Label + " " + MentionUtils.MentionRole(o.RoleId));
        var optionsText = string.Join('\n', optionLines).TrimTo(1024);
        if (string.IsNullOrWhiteSpace(optionsText))
            optionsText = strings.RolemenuNeedsOption(guildId);
        var messageText = GetJumpUrl(menu) ?? StatusText(guildId, RoleMenuStatus.NotPosted);
        var title = strings.RolemenuInfoTitle(guildId, menu.Name, menu.Id).TrimTo(256);

        return new EmbedBuilder()
            .WithOkColor()
            .WithTitle(title)
            .AddField(strings.RolemenuInfoChannel(guildId), MentionUtils.MentionChannel(menu.ChannelId), true)
            .AddField(strings.RolemenuInfoType(guildId), StyleText(guildId, (RoleMenuStyle)menu.Style), true)
            .AddField(strings.RolemenuInfoPicking(guildId), ModeText(guildId, (RoleMenuMode)menu.Mode), true)
            .AddField(strings.RolemenuInfoLimits(guildId),
                strings.RolemenuInfoLimitsValue(guildId, menu.MinRoles, MaxText(guildId, menu.MaxRoles)), true)
            .AddField(strings.RolemenuInfoRequired(guildId), required, true)
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
