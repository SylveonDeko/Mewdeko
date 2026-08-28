using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Common.DiscordImplementations;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Common.TriggerPlaceholders;
using Mewdeko.Common.Yml;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Chat_Triggers.Common;
using Mewdeko.Modules.Chat_Triggers.Extensions;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Modules.Xp.Events;
using Mewdeko.Modules.Xp.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.Strings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using CTModel = DataModel.ChatTrigger;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Mewdeko.Modules.Chat_Triggers.Services;

/// <summary>
///     The service for managing chat triggers. Hell.
/// </summary>
public sealed class ChatTriggersService : IEarlyBehavior, INService, IReadyExecutor
{
    /// <summary>
    ///     Enumerates the fields of a chat trigger.
    /// </summary>
    public enum CtField
    {
        /// <summary>
        ///     Auto delete trigger field.
        /// </summary>
        AutoDelete,

        /// <summary>
        ///     Direct message response field.
        /// </summary>
        DmResponse,

        /// <summary>
        ///     Allow targeting field.
        /// </summary>
        AllowTarget,

        /// <summary>
        ///     Contains anywhere field.
        /// </summary>
        ContainsAnywhere,

        /// <summary>
        ///     Message field.
        /// </summary>
        Message,

        /// <summary>
        ///     React to trigger field.
        /// </summary>
        ReactToTrigger,

        /// <summary>
        ///     No respond field.
        /// </summary>
        NoRespond,

        /// <summary>
        ///     Permissions enabled by default field.
        /// </summary>
        PermsEnabledByDefault,

        /// <summary>
        ///     Channels enabled by default field.
        /// </summary>
        ChannelsEnabledByDefault
    }


    private const string MentionPh = "%bot.mention%";

    /// <summary>
    ///     How many times a regex trigger may time out before it is disabled.
    /// </summary>
    private const int RegexTimeoutStrikes = 5;

    /// <summary>
    ///     How many triggers a single chain may run before it is cut off.
    /// </summary>
    private const int MaxChainDepth = 5;

    private const string PrependExport =
        """
        # WARNING: crossposting information is not saved.
        # Keys are triggers, Each key has a LIST of custom reactions in the following format:
        # - res: Response string
        #   react:
        #     - <List
        #     -  of
        #     - reactions>
        #   at: Whether custom reaction allows targets (see .h .crat)
        #   ca: Whether custom reaction expects trigger anywhere (see .h .crca)
        #   dm: Whether custom reaction DMs the response (see .h .crdm)
        #   ad: Whether custom reaction automatically deletes triggering message (see .h .crad)
        #   rtt: Whether custom reaction emotes are added to the response or trigger


        """;

    /// <summary>
    ///     How long a single regex trigger may spend matching one message. Tight enough to bound the cost of a
    ///     catastrophically backtracking pattern, but far enough above the previous 1ms that ordinary patterns are not
    ///     failed nondeterministically under load.
    /// </summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    private static readonly ISerializer ExportSerializer = new SerializerBuilder()
        .WithEventEmitter(args => new MultilineScalarFlowStyleEmitter(args))
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithIndentedSequences()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
        .DisableAliases()
        .Build();


    /// <summary>
    ///     A regex pattern to validate command names.
    /// </summary>
    public static readonly Regex ValidCommandRegex = new(@"^(?:[\w-]{1,32} {0,1}){1,3}$", RegexOptions.Compiled);

    /// <summary>
    ///     Match indexes, keyed by the trigger array they describe so that they are collected along with it.
    /// </summary>
    private static readonly ConditionalWeakTable<CTModel[], TriggerIndex> IndexCache = new();

    private readonly DiscordShardedClient client;
    private readonly CmdCdService cmdCds;
    private readonly BotConfigService configService;
    private readonly TriggerCounterService counters;
    private readonly TypedKey<CTModel> crAdded = new("cr.added");
    private readonly IBotCredentials creds;
    private readonly TypedKey<bool> crsReloadedKey = new("crs.reloaded");
    private readonly ICurrencyService currency;

    private readonly IDataConnectionFactory dbFactory;
    private readonly DiscordPermOverrideService discordPermOverride;
    private readonly EventHandler eventHandler;

    private readonly TypedKey<CTModel> gcrAddedKey = new("gcr.added");
    private readonly TypedKey<int> gcrDeletedkey = new("gcr.deleted");
    private readonly TypedKey<CTModel> gcrEditedKey = new("gcr.edited");

    private readonly object gcrWriteLock = new();
    private readonly GlobalPermissionService gperm;
    private readonly GuildSettingsService guildSettings;
    private readonly ILogger<ChatTriggersService> logger;
    private readonly PermissionService perms;
    private readonly TriggerPlaceholderService placeholders;
    private readonly IPubSub pubSub;

    private readonly ConcurrentDictionary<int, int> regexTimeouts = new();
    private readonly Random rng;
    private readonly StickyConditionService stickyConditions;
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     When each trigger's own cooldown expires, keyed by trigger and cooldown scope.
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTime> triggerCooldowns = new();

    private readonly XpService xpService;


    // it is perfectly fine to have global chattriggers as an array
    // 1. custom reactions are almost never added (compared to how many times they are being looped through)
    // 2. only need write locks for this as we'll rebuild+replace the array on every edit
    // 3. there's never many of them (at most a thousand, usually < 100)
    private CTModel[] globalReactions;
    private ConcurrentDictionary<ulong, CTModel[]> newGuildReactions;

    private bool ready;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatTriggersService" /> class.
    /// </summary>
    /// <param name="perms">The permission service.</param>
    /// <param name="dbFactory">The database service.</param>
    /// <param name="bot">The bot instance.</param>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="gperm">The global permission service.</param>
    /// <param name="cmdCds">The command cooldown service.</param>
    /// <param name="pubSub">The pub-sub service.</param>
    /// <param name="discordPermOverride">The Discord permission override service.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    /// <param name="configService">The bot configuration service.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="strings">The bot strings.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="placeholders">Resolves placeholders contributed by other modules.</param>
    /// <param name="currency">The currency service, for trigger costs and rewards.</param>
    /// <param name="xpService">The XP service, for level requirements and XP rewards.</param>
    /// <param name="stickyConditions">Evaluates the time conditions shared with sticky messages.</param>
    /// <param name="counters">The counter store used by counter placeholders and conditions.</param>
    public ChatTriggersService(
        PermissionService perms,
        IDataConnectionFactory dbFactory,
        Mewdeko bot,
        DiscordShardedClient client,
        GlobalPermissionService gperm,
        CmdCdService cmdCds,
        IPubSub pubSub,
        DiscordPermOverrideService discordPermOverride,
        GuildSettingsService guildSettings,
        BotConfigService configService,
        IBotCredentials creds, GeneratedBotStrings strings,
        EventHandler eventHandler, ILogger<ChatTriggersService> logger,
        TriggerPlaceholderService placeholders, ICurrencyService currency, XpService xpService,
        StickyConditionService stickyConditions, TriggerCounterService counters)
    {
        this.stickyConditions = stickyConditions;
        this.counters = counters;
        this.placeholders = placeholders;
        this.currency = currency;
        this.xpService = xpService;
        this.dbFactory = dbFactory;
        this.client = client;
        this.perms = perms;
        this.cmdCds = cmdCds;
        this.gperm = gperm;
        this.pubSub = pubSub;
        this.discordPermOverride = discordPermOverride;
        this.guildSettings = guildSettings;
        this.configService = configService;
        this.creds = creds;
        this.strings = strings;
        this.eventHandler = eventHandler;
        this.logger = logger;
        rng = new MewdekoRandom();

        pubSub.Sub(crsReloadedKey, OnCrsShouldReload);
        pubSub.Sub(gcrAddedKey, OnGcrAdded);
        pubSub.Sub(gcrDeletedkey, OnGcrDeleted);
        pubSub.Sub(gcrEditedKey, OnGcrEdited);
        pubSub.Sub(crAdded, OnCrAdded);

        bot.JoinedGuild += OnJoinedGuild;
        eventHandler.Subscribe("LeftGuild", "ChatTriggersService", OnLeftGuild);

        // Subscribe to reaction events for reaction triggers
        eventHandler.Subscribe("ReactionAdded", "ChatTriggersService", OnReactionAdded);
        eventHandler.Subscribe("ReactionRemoved", "ChatTriggersService", OnReactionRemoved);

        // Subscribe to the events other modules raise, so triggers can act as their notification layer
        eventHandler.Subscribe("XpLevelChanged", "ChatTriggersService", OnXpLevelChanged);
        eventHandler.Subscribe("UserJoined", "ChatTriggersService", OnUserJoined);
        eventHandler.Subscribe("UserLeft", "ChatTriggersService", OnUserLeft);
        eventHandler.Subscribe("UserVoiceStateUpdated", "ChatTriggersService", OnVoiceStateUpdated);
        eventHandler.Subscribe("GuildMemberUpdated", "ChatTriggersService", OnGuildMemberUpdated);

        // The command pipeline drops bot messages before early behaviours run, so triggers that opted into bot and
        // webhook messages have to be driven from the raw message event instead
        eventHandler.Subscribe("MessageReceived", "ChatTriggersService", OnMessageReceived);
    }

    /// <summary>
    ///     Gets the priority of the module.
    /// </summary>
    public int Priority
    {
        get
        {
            return -1;
        }
    }

    /// <summary>
    ///     Gets the behavior type of the module.
    /// </summary>
    public ModuleBehaviorType BehaviorType
    {
        get
        {
            return ModuleBehaviorType.Executor;
        }
    }

    /// <summary>
    ///     Executes the behavior associated with the chat triggers in response to a user message.
    /// </summary>
    /// <param name="socketClient">The Discord socket client.</param>
    /// <param name="guild">The guild where the message was sent.</param>
    /// <param name="msg">The user message triggering the behavior.</param>
    /// <returns>
    ///     A <see cref="Task{TResult}" /> representing the asynchronous operation, returning <c>true</c> if the behavior
    ///     is executed successfully, otherwise <c>false</c>.
    /// </returns>
    public Task<bool> RunBehavior(DiscordShardedClient socketClient, IGuild guild, IUserMessage msg)
    {
        return RunBehaviorInternal(guild, msg, false);
    }

    /// <summary>
    ///     Handles tasks to be executed when the bot is ready.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task OnReadyAsync()
    {
        await ReloadInternal(client.Guilds.Select(x => x.Id).ToList());
    }

    /// <summary>
    ///     Runs the message trigger pipeline for a message.
    /// </summary>
    /// <param name="guild">The guild the message was sent in.</param>
    /// <param name="msg">The message to match triggers against.</param>
    /// <param name="fromBot">
    ///     Whether the message was authored by another bot, in which case only triggers that opted into bot messages
    ///     are considered.
    /// </param>
    /// <param name="chainDepth">How many triggers deep the current chain is.</param>
    /// <param name="forced">A specific trigger to run instead of matching, used when running a chained trigger.</param>
    /// <param name="visited">The triggers this chain has already run, or null at the start of a chain.</param>
    /// <returns>True if a trigger handled the message.</returns>
    private async Task<bool> RunBehaviorInternal(IGuild guild, IUserMessage msg, bool fromBot, int chainDepth = 0,
        CTModel? forced = null, HashSet<int>? visited = null)
    {
        // Maybe this message is a custom reaction
        var ct = forced;
        Match? regexMatch = null;

        if (ct is null)
            (ct, regexMatch) = await TryGetChatTriggers(msg, fromBot);

        if (ct is null)
            return false;

        if (await cmdCds.TryBlock(guild, msg.Author, ct.Trigger).ConfigureAwait(false))
            return false;

        if (!((ChatTriggerType)ct.ValidTriggerTypes).HasFlag(ChatTriggerType.Message))
            return false;

        try
        {
            // Check if the "ActualChatTriggers" module is blocked
            if (gperm.BlockedModules.Contains("ActualChatTriggers"))
                return true;

            // Check if the user has permission to trigger the chat command
            if (guild is SocketGuild sg)
            {
                var pc = await perms.GetCacheFor(guild.Id);
                if (!pc.Permissions.CheckPermissions(msg, ct.Trigger, "ActualChatTriggers", out var index,
                        ct.Id.ToString()))
                {
                    if (pc.Verbose)
                    {
                        var returnMsg = strings.PermPrevent(guild.Id,
                            index + 1,
                            Format.Bold(pc.Permissions[index].GetCommand(await guildSettings.GetPrefix(guild), sg)));
                        try
                        {
                            await msg.Channel.SendErrorAsync(returnMsg, configService.Data).ConfigureAwait(false);
                        }
                        catch
                        {
                            // Ignored
                        }

                        if (configService.Data.LogChatTriggerFires)
                            logger.LogInformation(returnMsg);
                    }

                    return true;
                }

                // Check if there are any guild-specific permission overrides for the trigger
                if (discordPermOverride.TryGetOverrides(guild.Id, ct.Trigger, out var guildPermission))
                {
                    var user = msg.Author as IGuildUser;
                    if (!user.GuildPermissions.Has(guildPermission))
                    {
                        if (configService.Data.LogChatTriggerFires)
                        {
                            logger.LogInformation(
                                "Chat Trigger {CtTrigger} Blocked for {MsgAuthor} in {Guild} due to them missing {Perms}",
                                ct.Trigger, msg.Author, guild, guildPermission);
                        }

                        return false;
                    }
                }
            }

            // Check the trigger's own conditions before anything is charged or sent.
            if (!await PassesConditionsAsync(ct, guild.Id, msg.Channel.Id, msg.Author).ConfigureAwait(false))
                return true;

            // Apply economy requirements and rewards before anything is sent.
            if (!await TryApplyEconomyAsync(ct, guild.Id, msg.Author, msg.Channel).ConfigureAwait(false))
                return true;

            await IncrementTriggerUsage(ct).ConfigureAwait(false);

            // Update command usage statistics
            await RecordTriggerFireAsync(ct, guild.Id, msg.Channel.Id, msg.Author).ConfigureAwait(false);

            // Send the chat trigger response
            IUserMessage? sentMsg = null;
            foreach (var response in await SelectResponsesAsync(ct).ConfigureAwait(false))
            {
                sentMsg = await ct.Send(msg, client, false, dbFactory, placeholders, regexMatch, response)
                    .ConfigureAwait(false);
            }

            // Add reactions to the response message, off the message pipeline
            QueueTriggerReactions(ct, sentMsg, msg);

            // Delete the triggering message if necessary
            try
            {
                if (ct.AutoDeleteTrigger)
                    await msg.DeleteAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignored
            }

            // Grant or remove roles from users based on the trigger
            if (ct.GuildId is null || msg?.Author is not IGuildUser guildUser) return true;
            {
                var effectedUsers = (CtRoleGrantType)ct.RoleGrantType switch
                {
                    CtRoleGrantType.Mentioned => msg.Content.GetUserMentions().Take(5),
                    CtRoleGrantType.Sender => new List<ulong>
                    {
                        msg.Author.Id
                    },
                    CtRoleGrantType.Both => msg.Content.GetUserMentions().Take(4).Append(msg.Author.Id),
                    _ => new List<ulong>()
                };

                foreach (var userId in effectedUsers)
                {
                    var user = await guildUser.Guild.GetUserAsync(userId).ConfigureAwait(false);
                    try
                    {
                        var baseRoles = user.RoleIds.Where(x => x != guild.EveryoneRole.Id).ToList();
                        var roles = baseRoles.Where(x => !ct.RemovedRoles?.Contains(x.ToString()) ?? true).ToList();
                        roles.AddRange(ct.GetGrantedRoles().Where(x => !user.RoleIds.Contains(x)));
                        // difference is caused by @everyone
                        if (baseRoles.Any(x => !roles.Contains(x)) || roles.Any(x => !baseRoles.Contains(x)))
                            await user.ModifyAsync(x => x.RoleIds = new Optional<IEnumerable<ulong>>(roles))
                                .ConfigureAwait(false);
                    }
                    catch
                    {
                        logger.LogWarning("Unable to modify the roles of {User} in {GuildId}", guildUser.Id,
                            ct.GuildId);
                    }
                }
            }

            await RunChainedTriggerAsync(ct, guild, msg, fromBot, chainDepth, visited).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex.Message);
        }

        return false;
    }

    /// <summary>
    ///     Adds a trigger then returns the added trigger, only used by the api
    /// </summary>
    /// <param name="guildId">The guild id of the trigger to add</param>
    /// <param name="toAdd">The trigger to add</param>
    /// <returns></returns>
    public async Task<CTModel> AddTrigger(ulong guildId, CTModel toAdd)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            toAdd.Id = await db.InsertWithInt32IdentityAsync(toAdd);

            newGuildReactions.AddOrUpdate(
                guildId,
                [toAdd],
                (key, existingTriggers) =>
                {
                    var updatedTriggers = new List<CTModel>(existingTriggers)
                    {
                        toAdd
                    };
                    return updatedTriggers.ToArray();
                }
            );
            return toAdd;
        }
        catch (Exception e)
        {
            logger.LogError(e, "error adding trigger");
            throw;
        }
    }


    /// <summary>
    ///     Handles the event when a chat trigger is added.
    /// </summary>
    /// <param name="arg">The chat trigger model.</param>
    private async ValueTask OnCrAdded(CTModel arg)
    {
        await AddAsync(arg.GuildId, arg.Trigger, arg.Response, arg.IsRegex);
    }


    /// <summary>
    ///     Runs an interaction trigger. Thank you to cottagedwelling cat for this. Really.
    /// </summary>
    /// <param name="inter">The SocketInteraction to process.</param>
    /// <param name="ct">The CTModel representing the chat trigger.</param>
    /// <param name="followup">A boolean indicating whether the response should be sent as a follow-up message.</param>
    public async Task RunInteractionTrigger(SocketInteraction inter, CTModel? ct, bool followup = false)
    {
        if (inter is null)
            return;

        // A component's custom id carries the trigger id, so a button outlives the trigger it points at. Deleting a
        // trigger leaves its buttons in place, and clicking one used to throw rather than say anything.
        if (ct is null)
        {
            try
            {
                var guildId = (inter.Channel as IGuildChannel)?.GuildId;
                await inter.RespondAsync(strings.CtTriggerGone(guildId), ephemeral: true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to report a missing chat trigger for an interaction");
            }

            return;
        }

        // Switch based on the type of interaction
        switch (inter)
        {
            // If the interaction is a command and the trigger type does not include interaction triggers, or
            // if the interaction is a message component (button) and the trigger type does not include buttons,
            // return without further processing.
            // Refusing to run still has to answer the interaction. Returning silently leaves Discord showing
            // "The application did not respond" a few seconds later, which reads as the bot being broken.
            case not null when ct.IsDisabled:
            case SocketCommandBase when !((ChatTriggerType)ct.ValidTriggerTypes).HasFlag(ChatTriggerType.Interaction):
            case SocketMessageComponent when !((ChatTriggerType)ct.ValidTriggerTypes).HasFlag(ChatTriggerType.Button):
                await AcknowledgeUnavailableTriggerAsync(inter).ConfigureAwait(false);
                return;
            default:
                try
                {
                    // Create a fake message to represent the interaction.
                    var fakeMsg = new MewdekoUserMessage
                    {
                        Author = inter.User, Content = ct.Trigger, Channel = inter.Channel
                    };

                    // If the ActualChatTriggers module is blocked, return without further processing.
                    if (gperm.BlockedModules.Contains("ActualChatTriggers"))
                        return;

                    // If the interaction occurs in a guild channel, check permissions.
                    if (inter.Channel is IGuildChannel { Guild: SocketGuild guild })
                    {
                        var pc = await perms.GetCacheFor(guild.Id);

                        // Check if the user has permissions to trigger the chat command.
                        if (!pc.Permissions.CheckPermissions(fakeMsg, ct.Trigger, "ActualChatTriggers",
                                out var index, ct.Id.ToString()))
                        {
                            // If verbose mode is enabled, provide a detailed message about the prevented action.
                            if (!pc.Verbose)
                                return;
                            var returnMsg = strings.PermPrevent(guild.Id,
                                index + 1,
                                Format.Bold(pc.Permissions[index]
                                    .GetCommand(await guildSettings.GetPrefix(guild), guild)));
                            try
                            {
                                await fakeMsg.Channel.SendErrorAsync(returnMsg, configService.Data)
                                    .ConfigureAwait(false);
                            }
                            catch
                            {
                                // ignored
                            }

                            if (configService.Data.LogChatTriggerFires)
                                logger.LogInformation(returnMsg);

                            return;
                        }

                        // Check for permission overrides.
                        if (discordPermOverride.TryGetOverrides(guild.Id, ct.Trigger, out var guildPermission))
                        {
                            var user = inter.User as IGuildUser;
                            if (!user.GuildPermissions.Has(guildPermission))
                            {
                                if (configService.Data.LogChatTriggerFires)
                                {
                                    logger.LogInformation(
                                        $"Chat Trigger {ct.Trigger} Blocked for {inter.User} in {guild} due to them missing {guildPermission}.");
                                }

                                return;
                            }
                        }
                    }

                    var channel = inter.Channel as IGuildChannel;

                    // Check the trigger's own conditions before anything is charged or sent.
                    if (!await PassesConditionsAsync(ct, channel.GuildId, channel.Id, inter.User)
                            .ConfigureAwait(false))
                    {
                        return;
                    }

                    // Apply economy requirements and rewards before anything is sent.
                    if (!await TryApplyEconomyAsync(ct, channel.GuildId, inter.User, inter.Channel)
                            .ConfigureAwait(false))
                    {
                        return;
                    }

                    await IncrementTriggerUsage(ct).ConfigureAwait(false);

                    // Get guild configuration.
                    var guildConfig = await guildSettings.GetGuildConfig(channel.GuildId);

                    // If stats tracking is enabled for the guild and the user has not opted out, record the usage.
                    await RecordTriggerFireAsync(ct, channel.GuildId, channel.Id, inter.User).ConfigureAwait(false);

                    var sentMsg = await ct.SendInteraction(inter, client, false, fakeMsg,
                        ct.EphemeralResponse, dbFactory, followup, placeholders,
                        (await SelectResponsesAsync(ct).ConfigureAwait(false)).FirstOrDefault()).ConfigureAwait(false);

                    // Add reactions to the sent message, if any. An interaction has no separate trigger message,
                    // so both cases target the response.
                    QueueTriggerReactions(ct, sentMsg, sentMsg);

                    // Process role grants for the interaction.
                    if (ct.GuildId is null || inter.User is not IGuildUser guildUser)
                        return;
                    {
                        var effectedUsers = inter is SocketUserCommand uCmd
                            ? (CtRoleGrantType)ct.RoleGrantType switch
                            {
                                CtRoleGrantType.Mentioned => [uCmd.Data.Member.Id],
                                CtRoleGrantType.Sender => [uCmd.User.Id],
                                CtRoleGrantType.Both => [uCmd.User.Id, uCmd.Data.Member.Id],
                                _ => []
                            }
                            : (CtRoleGrantType)ct.RoleGrantType switch
                            {
                                CtRoleGrantType.Mentioned => [],
                                CtRoleGrantType.Sender => [inter.User.Id],
                                CtRoleGrantType.Both => [inter.User.Id],
                                _ => new List<ulong>()
                            };

                        foreach (var userId in effectedUsers)
                        {
                            var user = await guildUser.Guild.GetUserAsync(userId).ConfigureAwait(false);
                            try
                            {
                                var baseRoles = user.RoleIds.Where(x => x != guildUser.Guild?.EveryoneRole.Id).ToList();
                                var roles = baseRoles.Where(x => !ct.RemovedRoles?.Contains(x.ToString()) ?? true)
                                    .ToList();
                                roles.AddRange(ct.GetGrantedRoles().Where(x => !user.RoleIds.Contains(x)));

                                // Apply role modifications.
                                if (baseRoles.Any(x => !roles.Contains(x)) || roles.Any(x => !baseRoles.Contains(x)))
                                    await user.ModifyAsync(x => x.RoleIds = new Optional<IEnumerable<ulong>>(roles))
                                        .ConfigureAwait(false);
                            }
                            catch
                            {
                                logger.LogWarning("Unable to modify the roles of {User} in {GuildId}", guildUser.Id,
                                    ct.GuildId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex.Message);
                }

                return;
        }
    }


    /// <summary>
    ///     Exports chat triggers data for a specific guild or all guilds.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which to export chat triggers. If null, exports for all guilds.</param>
    /// <returns>A string containing the exported chat triggers data.</returns>
    public async Task<string> ExportCrs(ulong? guildId)
    {
        // Retrieve chat triggers for the specified guild or all guilds
        var crs = await GetChatTriggersFor(guildId);

        // Group the chat triggers by trigger string and convert them to a dictionary
        var crsDict = crs
            .GroupBy(x => x.Trigger)
            .ToDictionary(x => x.Key, x => x.Select(ExportedTriggers.FromModel));

        // Serialize the dictionary to YAML format and prepend export metadata
        return PrependExport + ExportSerializer
            .Serialize(crsDict)
            .UnescapeUnicodeCodePoints();
    }

    /// <summary>
    ///     Imports chat triggers data into the database for a specific user.
    /// </summary>
    /// <param name="user">The user initiating the import operation.</param>
    /// <param name="input">The input string containing the chat triggers data to import.</param>
    /// <returns>True if the import operation is successful, false otherwise.</returns>
    public async Task<bool> ImportCrsAsync(IGuildUser user, string input)
    {
        try
        {
            Dictionary<string, List<ExportedTriggers>> data;
            try
            {
                // Deserialize the input string to a dictionary of trigger strings and exported triggers
                data = Yaml.Deserializer.Deserialize<Dictionary<string, List<ExportedTriggers>>>(input);
                if (data.Sum(x => x.Value.Count) == 0)
                    return false;
            }
            catch (Exception ex)
            {
                // Log and return false if deserialization fails
                logger.LogError(ex.ToString());
                return false;
            }

            // Convert exported triggers to CTModel objects. The dictionary key is the trigger text, which is what
            // exports from older versions and from NadekoBot carry it in.
            List<CTModel> triggers = [];
            foreach (var (key, value) in data)
            {
                triggers.AddRange(value
                    .Where(ct => !string.IsNullOrWhiteSpace(ct.Res))
                    .Select(ct => ct.ToModel(user.Guild.Id, key)));
            }

            if (triggers.Count == 0)
                return false;

            // Refuse the import if it would hand out any role the importing user cannot manage themselves
            List<ulong> roles = [];
            triggers.ForEach(x => roles.AddRange(x.GetGrantedRoles()));
            triggers.ForEach(x => roles.AddRange(x.GetRemovedRoles()));

            if (roles.Count > 0 && !roles.Distinct().All(y => user.Guild.GetRole(y)?.CanManageRole(user) == true))
                return false;
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            // Add chat triggers to the database and save changes
            await dbContext.ChatTriggers.BulkCopyAsync(triggers).ConfigureAwait(false);


            // Trigger the reload of chat triggers
            await TriggerReloadChatTriggers().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log and return false if an exception occurs
            logger.LogError(ex.ToString());
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Migrates old CrEmbed format triggers to the new embed format.
    ///     Old format: {"PlainText":"...","Title":"...","Description":"...","Color":12345,"Image":"url",...}
    ///     New format:
    ///     {"content":"...","embeds":[{"title":"...","description":"...","color":12345,"image":{"url":"url"},...}],...}
    /// </summary>
    /// <param name="guildId">The guild ID to migrate triggers for. If null, migrates global triggers.</param>
    /// <returns>A MigrationResult with the count of checked and migrated triggers.</returns>
    public async Task<MigrationResult> MigrateCrEmbedFormat(ulong? guildId)
    {
        var triggers = await GetChatTriggersFor(guildId);
        var migrated = 0;

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        foreach (var trigger in triggers)
        {
            if (string.IsNullOrWhiteSpace(trigger.Response))
                continue;

            var converted = ConvertCrEmbedToNewFormat(trigger.Response);
            if (converted == null)
                continue;

            // Update the trigger in the database
            await dbContext.ChatTriggers
                .Where(x => x.Id == trigger.Id)
                .Set(x => x.Response, converted)
                .UpdateAsync()
                .ConfigureAwait(false);

            migrated++;
        }

        if (migrated > 0)
        {
            // Reload triggers to reflect the changes
            await TriggerReloadChatTriggers().ConfigureAwait(false);
        }

        return new MigrationResult(triggers.Length, migrated);
    }

    /// <summary>
    ///     Converts an old CrEmbed format JSON to the new embed format.
    ///     Returns null if the response is not in old CrEmbed format.
    /// </summary>
    private static string? ConvertCrEmbedToNewFormat(string response)
    {
        // Check if this looks like JSON
        var trimmed = response.Trim();
        if (!trimmed.StartsWith('{') || !trimmed.EndsWith('}'))
            return null;

        try
        {
            var json = JObject.Parse(trimmed);

            // Check if it's already in new format (has "embeds" array)
            if (json["embeds"] != null)
                return null;

            // Check if this looks like old CrEmbed format
            // Old format has properties like PlainText, Title, Description, Image (as string), Color (as uint)
            var hasOldFormatProps = json["PlainText"] != null ||
                                    json["Title"] != null ||
                                    json["Description"] != null ||
                                    json["Image"] != null && json["Image"]?.Type == JTokenType.String ||
                                    json["Thumbnail"] != null && json["Thumbnail"]?.Type == JTokenType.String ||
                                    json["Author"] != null ||
                                    json["Footer"] != null ||
                                    json["Fields"] != null;

            // Also check for the case where it's just embed properties without the wrapper
            // (e.g., {"description":"...","color":53380,"image":"https://..."})
            var hasDirectEmbedProps = json["description"] != null ||
                                      json["title"] != null ||
                                      json["image"] != null && json["image"]?.Type == JTokenType.String ||
                                      json["thumbnail"] != null && json["thumbnail"]?.Type == JTokenType.String;

            if (!hasOldFormatProps && !hasDirectEmbedProps)
                return null;

            var newFormat = new JObject();

            // Handle PlainText -> content (old CrEmbed format)
            if (json["PlainText"] != null)
            {
                newFormat["content"] = json["PlainText"];
            }
            // Handle content passthrough (direct format)
            else if (json["content"] != null)
            {
                newFormat["content"] = json["content"];
            }

            // Build the embed object
            var embed = new JObject();
            var hasEmbedContent = false;

            // Handle both PascalCase (CrEmbed) and camelCase (direct) property names
            if (json["Title"] != null)
            {
                embed["title"] = json["Title"];
                hasEmbedContent = true;
            }
            else if (json["title"] != null)
            {
                embed["title"] = json["title"];
                hasEmbedContent = true;
            }

            if (json["Description"] != null)
            {
                embed["description"] = json["Description"];
                hasEmbedContent = true;
            }
            else if (json["description"] != null)
            {
                embed["description"] = json["description"];
                hasEmbedContent = true;
            }

            if (json["Url"] != null)
            {
                embed["url"] = json["Url"];
                hasEmbedContent = true;
            }
            else if (json["url"] != null)
            {
                embed["url"] = json["url"];
                hasEmbedContent = true;
            }

            // Handle Color (old format uses uint, new format uses int)
            if (json["Color"] != null)
            {
                embed["color"] = json["Color"];
                hasEmbedContent = true;
            }
            else if (json["color"] != null)
            {
                embed["color"] = json["color"];
                hasEmbedContent = true;
            }

            // Handle Image - old format: string, new format: { url: string }
            if (json["Image"] != null && json["Image"]?.Type == JTokenType.String)
            {
                embed["image"] = new JObject
                {
                    ["url"] = json["Image"]
                };
                hasEmbedContent = true;
            }
            else if (json["image"] != null && json["image"]?.Type == JTokenType.String)
            {
                embed["image"] = new JObject
                {
                    ["url"] = json["image"]
                };
                hasEmbedContent = true;
            }

            // Handle Thumbnail - old format: string, new format: { url: string }
            if (json["Thumbnail"] != null && json["Thumbnail"]?.Type == JTokenType.String)
            {
                embed["thumbnail"] = new JObject
                {
                    ["url"] = json["Thumbnail"]
                };
                hasEmbedContent = true;
            }
            else if (json["thumbnail"] != null && json["thumbnail"]?.Type == JTokenType.String)
            {
                embed["thumbnail"] = new JObject
                {
                    ["url"] = json["thumbnail"]
                };
                hasEmbedContent = true;
            }

            // Handle Author
            if (json["Author"] != null)
            {
                var author = json["Author"];
                var newAuthor = new JObject();
                if (author?["Name"] != null) newAuthor["name"] = author["Name"];
                if (author?["IconUrl"] != null) newAuthor["icon_url"] = author["IconUrl"];
                if (author?["Url"] != null) newAuthor["url"] = author["Url"];
                if (newAuthor.HasValues)
                {
                    embed["author"] = newAuthor;
                    hasEmbedContent = true;
                }
            }
            else if (json["author"] != null)
            {
                embed["author"] = json["author"];
                hasEmbedContent = true;
            }

            // Handle Footer
            if (json["Footer"] != null)
            {
                var footer = json["Footer"];
                var newFooter = new JObject();
                if (footer?["Text"] != null) newFooter["text"] = footer["Text"];
                if (footer?["IconUrl"] != null) newFooter["icon_url"] = footer["IconUrl"];
                if (newFooter.HasValues)
                {
                    embed["footer"] = newFooter;
                    hasEmbedContent = true;
                }
            }
            else if (json["footer"] != null)
            {
                embed["footer"] = json["footer"];
                hasEmbedContent = true;
            }

            // Handle Fields
            if (json["Fields"] != null && json["Fields"] is JArray oldFields)
            {
                var newFields = new JArray();
                foreach (var field in oldFields)
                {
                    var newField = new JObject();
                    if (field["Name"] != null) newField["name"] = field["Name"];
                    if (field["Value"] != null) newField["value"] = field["Value"];
                    if (field["Inline"] != null) newField["inline"] = field["Inline"];
                    newFields.Add(newField);
                }

                if (newFields.Count > 0)
                {
                    embed["fields"] = newFields;
                    hasEmbedContent = true;
                }
            }
            else if (json["fields"] != null)
            {
                embed["fields"] = json["fields"];
                hasEmbedContent = true;
            }

            // Only add embeds array if there's actual embed content
            if (hasEmbedContent)
            {
                newFormat["embeds"] = new JArray
                {
                    embed
                };
            }

            // Handle Components (pass through, format should be similar)
            if (json["Components"] != null)
            {
                newFormat["components"] = json["Components"];
            }
            else if (json["components"] != null)
            {
                newFormat["components"] = json["components"];
            }

            // If there's nothing to convert, return null
            if (!newFormat.HasValues)
                return null;

            return newFormat.ToString(Formatting.None);
        }
        catch (JsonException)
        {
            // Not valid JSON, return null
            return null;
        }
    }


    /// <summary>
    ///     Reloads internal chat trigger data for the current shard.
    /// </summary>
    /// <param name="allGuildIds">A list of all guild IDs.</param>
    private async Task ReloadInternal(IReadOnlyList<ulong> allGuildIds)
    {
        logger.LogInformation($"Starting {GetType()} Cache");
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Add logging to debug the query
        logger.LogInformation("Retrieving guild chat triggers...");
        var guildItems = await dbContext.ChatTriggers
            .ToListAsync();
        logger.LogInformation($"Retrieved {guildItems.Count} total triggers");

        // More detailed logging
        newGuildReactions = guildItems
            .Where(x => x.GuildId is not null)
            .GroupBy(k => k.GuildId!.Value)
            .ToDictionary(g => g.Key,
                g => g.Select(x =>
                {
                    x.Trigger = x.Trigger?.Replace(MentionPh, client.CurrentUser.Mention) ?? "";
                    return x;
                }).ToArray())
            .ToConcurrent();

        logger.LogInformation($"Loaded {newGuildReactions.Count} guild trigger groups");

        globalReactions = (await dbContext.ChatTriggers
                .Where(x => x.GuildId == null || x.GuildId == 0)
                .ToListAsync())
            .Select(x =>
            {
                x.Trigger = x.Trigger?.Replace(MentionPh, client.CurrentUser.Mention) ?? "";
                return x;
            })
            .ToArray();

        logger.LogInformation($"Loaded {globalReactions.Length} global triggers");

        ready = true;
    }


    /// <summary>
    ///     Tries to retrieve chat triggers associated with the provided user message.
    /// </summary>
    /// <param name="umsg">The user message to match against chat triggers.</param>
    /// <param name="fromBot">
    ///     Whether the message was authored by another bot, in which case only triggers that opted into bot messages
    ///     are considered.
    /// </param>
    /// <returns>The matched chat trigger model, or null if no match is found.</returns>
    private async Task<(CTModel? Trigger, Match? Match)> TryGetChatTriggers(IUserMessage umsg,
        bool fromBot = false)
    {
        // Check if the chat triggers are ready
        if (!ready)
            return (null, null);

        // Check if the message channel is a text channel
        if (umsg.Channel is not SocketTextChannel channel)
            return (null, null);

        // Trim and convert message content to lowercase for comparison
        var content = umsg.Content.Trim().ToLowerInvariant();

        // Check if there are guild-specific reactions for the current guild
        if (newGuildReactions.TryGetValue(channel.Guild.Id, out var reactions) && reactions.Length > 0)
        {
            // Attempt to match chat triggers against the message content
            var cr = await MatchChatTriggers(content, reactions, channel.Guild, fromBot);
            if (cr.Trigger is not null)
                return cr;
        }

        // Get the global reactions array
        // ReSharper disable once InconsistentlySynchronizedField
        var localGrs = globalReactions;

        // Match chat triggers against the message content
        return await MatchChatTriggers(content, localGrs, channel.Guild, fromBot);
    }


    /// <summary>
    ///     Matches chat triggers against the provided content to find a trigger that matches.
    /// </summary>
    /// <param name="content">The content to match against chat triggers.</param>
    /// <param name="crs">The array of chat triggers to match against.</param>
    /// <param name="guild">The guild associated with the chat triggers.</param>
    /// <param name="fromBot">
    ///     Whether the message was authored by another bot. Triggers that opted into bot messages only match when this
    ///     is true, and never match human messages.
    /// </param>
    /// <returns>The matched chat trigger model, or null if no match is found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<(CTModel? Trigger, Match? Match)> MatchChatTriggers(string content, CTModel[] crs,
        SocketGuild guild, bool fromBot = false)
    {
        try
        {
            // Get the prefix for the guild and the global prefix
            var guildPrefix = await guildSettings.GetPrefix(guild);
            var globalPrefix = configService.Data.Prefix;

            // Initialize a list to store matched chat triggers, along with the regex match that produced them
            var result = new List<(CTModel Trigger, Match? Match)>(1);

            var index = GetIndex(crs);

            // Plain triggers are looked up directly instead of being compared one by one
            if (index.Exact.TryGetValue(content, out var exactMatches))
            {
                foreach (var ct in exactMatches)
                {
                    if (ct.IsDisabled || ct.AllowBots != fromBot)
                        continue;

                    result.Add((ct, null));
                }
            }

            // Iterate through the triggers that need real evaluation
            foreach (var ct in index.Complex)
            {
                var trigger = ct.Trigger;

                if (string.IsNullOrEmpty(trigger) || ct.IsDisabled)
                    continue;

                // A bot-authored message only matches triggers that opted in, and a trigger that opted in is not
                // matched again through the normal human path
                if (ct.AllowBots != fromBot)
                    continue;

                // Each trigger is matched against its own copy of the content so that
                // prefix stripping never leaks into the following iterations
                var candidate = content;

                // Check the type of prefix required for the trigger
                switch ((RequirePrefixType)ct.PrefixType)
                {
                    case RequirePrefixType.Custom:
                        if (string.IsNullOrEmpty(ct.CustomPrefix) || !candidate.StartsWith(ct.CustomPrefix))
                            continue;
                        candidate = candidate[ct.CustomPrefix.Length..];
                        break;
                    case RequirePrefixType.GuildOrNone:
                        if (guildPrefix is null || !candidate.StartsWith(guildPrefix))
                            continue;
                        candidate = candidate[guildPrefix.Length..];
                        break;
                    case RequirePrefixType.GuildOrGlobal:
                        if (!candidate.StartsWith(guildPrefix ?? globalPrefix))
                            continue;
                        candidate = candidate[(guildPrefix ?? globalPrefix).Length..];
                        break;
                    case RequirePrefixType.Global:
                        if (!candidate.StartsWith(globalPrefix))
                            continue;
                        candidate = candidate[globalPrefix.Length..];
                        break;
                    case RequirePrefixType.None:
                    default:
                        break;
                }

                // Check if the trigger is a regex pattern
                if (ct.IsRegex)
                {
                    // Match the content against the trigger regex pattern, keeping the match so that its capture
                    // groups can be exposed to the response as %regex.1% / %regex.name% placeholders.
                    // A pattern that times out is skipped on its own rather than aborting the whole scan, so one
                    // pathological trigger cannot silently disable every trigger after it.
                    try
                    {
                        var regexMatch = Regex.Match(candidate, trigger, RegexOptions.None, RegexTimeout);
                        if (regexMatch.Success)
                            result.Add((ct, regexMatch));
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        NoteRegexTimeout(ct);
                    }
                    catch (ArgumentException ex)
                    {
                        logger.LogWarning(ex, "Chat trigger {TriggerId} has an invalid regex pattern", ct.Id);
                    }

                    continue;
                }

                // If the trigger depends on user mentions to grant roles,
                // remove user mentions from the content
                if ((CtRoleGrantType)ct.RoleGrantType is CtRoleGrantType.Mentioned or CtRoleGrantType.Both)
                {
                    candidate = candidate.RemoveUserMentions().Trim();
                }

                // Check if the content length is greater than the trigger length
                if (candidate.Length > trigger.Length)
                {
                    // If the trigger has ContainsAnywhere enabled, check if it is contained as a word within the content
                    if (ct.ContainsAnywhere)
                    {
                        var wp = candidate.AsSpan().GetWordPosition(trigger);
                        if (wp != WordPosition.None)
                            result.Add((ct, null));
                        continue;
                    }

                    // If AllowTarget is enabled, the content has to start with the trigger followed by a space
                    if (ct.AllowTarget && candidate.StartsWith(trigger, StringComparison.OrdinalIgnoreCase)
                                       && candidate[trigger.Length] == ' ')
                    {
                        result.Add((ct, null));
                    }
                }
                else if (candidate.Length < trigger.Length)
                {
                    // If the content length is less than the trigger length, the trigger can never be triggered
                }
                else
                {
                    // If the content length is equal to the trigger length, the strings have to be equal for the trigger to be matched
                    if (candidate.SequenceEqual(trigger))
                        result.Add((ct, null));
                }
            }

            // Return a randomly selected matched chat trigger, if any
            return result.Count == 0 ? (null, null) : result[rng.Next(0, result.Count)];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to match chat triggers in {GuildId}", guild.Id);
            return (null, null);
        }
    }

    /// <summary>
    ///     Resets the reactions of a chat trigger to empty string.
    /// </summary>
    /// <param name="maybeGuildId">The optional guild ID.</param>
    /// <param name="id">The ID of the chat trigger to reset reactions.</param>
    public async Task ResetCrReactions(ulong? maybeGuildId, int id)
    {
        // Open a database context

        await using var dbContext = await dbFactory.CreateConnectionAsync();
        // Retrieve the chat trigger by ID
        var ct = await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id);
        if (ct is null)
            return; // Exit if the chat trigger is not found

        // Reset reactions to empty string
        ct.Reactions = string.Empty;
        await dbContext.UpdateAsync(ct);
    }

    /// <summary>
    ///     Updates the chat trigger internally.
    /// </summary>
    /// <param name="maybeGuildId">The optional guild ID.</param>
    /// <param name="ct">The chat trigger model to update.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UpdateInternalAsync(ulong? maybeGuildId, CTModel ct)
    {
        // Check if the guild ID is provided
        if (maybeGuildId is { } guildId)
            await UpdateInternal(guildId, ct); // Update internally based on guild ID
        else
        {
            // Publish the chat trigger edited event
            _ = pubSub.Pub(gcrEditedKey, ct);
            return; // Return completed task
        }

        // Handle interaction updates
        if ((CtApplicationCommandType)ct.ApplicationCommandType == CtApplicationCommandType.None)
            return; // Return completed task if no application command type

        // Get the guild by guild ID
        var guild = client.GetGuild(guildId);
        await RegisterTriggersToGuildAsync(guild); // Register triggers to the guild asynchronously
    }

    /// <summary>
    ///     Updates the chat trigger internally based on the guild ID.
    /// </summary>
    /// <param name="maybeGuildId">The optional guild ID.</param>
    /// <param name="ct">The chat trigger model to update.</param>
    private async Task UpdateInternal(ulong? maybeGuildId, CTModel ct)
    {
        await Task.CompletedTask;
        // Check if the guild ID is provided
        if (maybeGuildId is { } guildId)
        {
            // Update internal reactions for the guild
            newGuildReactions.AddOrUpdate(guildId, [
                    ct
                ],
                (_, old) =>
                {
                    var newArray = old.ToArray();
                    for (var i = 0; i < newArray.Length; i++)
                    {
                        if (newArray[i].Id == ct.Id)
                            newArray[i] = ct; // Update the chat trigger in the array
                    }

                    return newArray;
                });
        }
        else
        {
            var crs = globalReactions;
            for (var i = 0; i < crs.Length; i++)
            {
                if (crs[i].Id == ct.Id)
                    crs[i] = ct; // Update the chat trigger in the array
            }
        }
    }


    /// <summary>
    ///     Adds a chat trigger internally.
    /// </summary>
    /// <param name="maybeGuildId">The optional guild ID.</param>
    /// <param name="ct">The chat trigger model to add.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private Task AddInternalAsync(ulong? maybeGuildId, CTModel ct)
    {
        // Replace placeholders in the trigger with the client's mention for performance
        ct.Trigger = ct.Trigger.Replace(MentionPh, client.CurrentUser.Mention);

        // Check if the guild ID is provided
        if (maybeGuildId is { } guildId)
        {
            // Add or update the chat trigger in the newGuildReactions dictionary
            newGuildReactions.AddOrUpdate(guildId,
                [
                    ct
                ],
                (_, old) => old.With(ct));
        }
        else
        {
            // Publish the chat trigger added event
            return pubSub.Pub(gcrAddedKey, ct);
        }

        return Task.CompletedTask; // Return completed task
    }

    /// <summary>
    ///     Deletes a chat trigger internally.
    /// </summary>
    /// <param name="maybeGuildId">The optional guild ID.</param>
    /// <param name="id">The ID of the chat trigger to delete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task DeleteInternalAsync(ulong? maybeGuildId, int id)
    {
        // Check if the guild ID is provided
        if (maybeGuildId is { } guildId)
        {
            // Add or update the chat trigger in the newGuildReactions dictionary
            newGuildReactions.AddOrUpdate(guildId,
                [],
                (_, old) => DeleteInternal(old, id));

            return; // Return completed task
        }

        // Find the chat trigger to delete
        var cr = Array.Find(globalReactions, item => item.Id == id);
        if (cr is not null)
            await pubSub.Pub(gcrDeletedkey, cr.Id); // Publish the chat trigger deleted event
    }

    /// <summary>
    ///     Deletes a chat trigger internally from the given list of chat triggers.
    /// </summary>
    /// <param name="cts">The list of chat triggers to delete from.</param>
    /// <param name="id">The ID of the chat trigger to delete.</param>
    /// <returns>The updated list of chat triggers.</returns>
    private static CTModel[] DeleteInternal(IReadOnlyList<CTModel>? cts, int id)
    {
        // Check if the list of chat triggers is null or empty
        if (cts is null || cts.Count == 0)
            return cts as CTModel[] ?? cts?.ToArray(); // Return the list as is

        // Create a new array for the updated chat triggers
        var newCrs = new CTModel[cts.Count - 1];
        for (int i = 0, k = 0; i < cts.Count; i++, k++)
        {
            // Skip the chat trigger with the specified ID
            if (cts[i].Id == id)
            {
                k--;
                continue;
            }

            // Add the chat trigger to the new array
            newCrs[k] = cts[i];
        }

        return newCrs; // Return the updated array of chat triggers
    }

    /// <summary>
    ///     Sets reactions for a chat trigger.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The ID of the chat trigger to set reactions for.</param>
    /// <param name="emojis">The emojis to set as reactions.</param>
    public async Task SetCrReactions(ulong? guildId, int id, IEnumerable<string> emojis)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        // Retrieve the chat trigger by ID
        var ct = await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id);
        if (ct is null)
            return; // Exit if the chat trigger is not found

        // Set the reactions for the chat trigger
        ct.Reactions = string.Join("@@@", emojis);

        await dbContext.UpdateAsync(ct);


        // Update internal representation of chat trigger asynchronously
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false);
    }


    /// <summary>
    ///     Toggles the value of a specified chat trigger option asynchronously.
    /// </summary>
    /// <param name="ct">The chat trigger to toggle the option for.</param>
    /// <param name="field">The field representing the option to toggle.</param>
    /// <returns>A tuple indicating the success of the operation and the new value of the option.</returns>
    public async Task<(bool Success, bool NewValue)> ToggleCrOptionAsync(CTModel? ct, CtField? field)
    {
        var newVal = false; // Variable to store the new value of the option
        // Initialize the database context

        // Check if the chat trigger is null
        if (ct is null)
            return (false, false); // Return failure if the chat trigger is null

        // Toggle the value of the specified field based on the option
        newVal = field switch
        {
            CtField.AutoDelete => !ct.AutoDeleteTrigger,
            CtField.ContainsAnywhere => !ct.ContainsAnywhere,
            CtField.DmResponse => !ct.DmResponse,
            CtField.AllowTarget => !ct.AllowTarget,
            CtField.ReactToTrigger => !ct.ReactToTrigger,
            CtField.NoRespond => !ct.NoRespond,
            _ => newVal // Default case: return the current value
        };

        switch (field)
        {
            case CtField.AutoDelete:
                ct.AutoDeleteTrigger = newVal;
                break;
            case CtField.ContainsAnywhere:
                ct.ContainsAnywhere = newVal;
                break;
            case CtField.DmResponse:
                ct.DmResponse = newVal;
                break;
            case CtField.AllowTarget:
                ct.AllowTarget = newVal;
                break;
            case CtField.ReactToTrigger:
                ct.ReactToTrigger = newVal;
                break;
            case CtField.NoRespond:
                ct.NoRespond = newVal;
                break;
        }

        await using var dbContext = await dbFactory.CreateConnectionAsync();
        // Update the chat trigger in the database
        await dbContext.UpdateAsync(ct);


        // Update the internal representation of the chat trigger asynchronously
        await UpdateInternalAsync(ct.GuildId, ct).ConfigureAwait(false);

        // Return success and the new value of the option
        return (true, newVal);
    }

    /// <summary>
    ///     Retrieves a chat trigger by ID and guild ID asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve the chat trigger for.</param>
    /// <param name="id">The ID of the chat trigger to retrieve.</param>
    /// <returns>The chat trigger if found, otherwise null.</returns>
    public async Task<CTModel?> GetChatTriggers(ulong? guildId, int id)
    {
        // Initialize the database context
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var ct = await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id); // Retrieve the chat trigger by ID
        // Check if the chat trigger is null or does not belong to the specified guild
        if (ct == null || ct.GuildId != guildId)
            return null; // Return null if the chat trigger is not found or does not belong to the guild
        return ct; // Return the chat trigger
    }

    /// <summary>
    ///     Retrieves a chat trigger by ID, considering both guild-specific and global triggers asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve the chat trigger for.</param>
    /// <param name="id">The ID of the chat trigger to retrieve.</param>
    /// <returns>The chat trigger if found, otherwise null.</returns>
    public async Task<CTModel?> GetGuildOrGlobalTriggers(ulong? guildId, int id)
    {
        // Initialize the database context
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var ct = await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id); // Retrieve the chat trigger by ID
        // Check if the chat trigger is null or does not belong to the specified guild or global context
        if (ct == null || ct.GuildId != guildId && ct.GuildId is not (0 or null))
            return
                null; // Return null if the chat trigger is not found or does not belong to the guild or global context
        return ct; // Return the chat trigger
    }

    /// <summary>
    ///     Deletes all chat triggers associated with a guild and returns the count of deleted triggers.
    /// </summary>
    /// <param name="guildId">The ID of the guild to delete chat triggers from.</param>
    /// <returns>The count of deleted chat triggers.</returns>
    public async Task<int> DeleteAllChatTriggers(ulong guildId)
    {
        // Initialize the database context
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var count = await dbContext.ChatTriggers.Where(x => x.GuildId == guildId)
            .DeleteAsync(); // Delete chat triggers associated with the guild
        newGuildReactions.TryRemove(guildId, out _); // Remove guild reactions from the internal representation
        return count; // Return the count of deleted chat triggers
    }


    /// <summary>
    ///     Checks if a reaction exists for a specific guild and input string.
    /// </summary>
    /// <param name="guildId">The ID of the guild to check for the reaction.</param>
    /// <param name="input">The input string to check for the reaction.</param>
    /// <returns>True if the reaction exists, otherwise false.</returns>
    public async Task<bool> ReactionExists(ulong? guildId, string input)
    {
        // Initialize the database context
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var ct = await dbContext.ChatTriggers.GetByGuildIdAndInput(guildId,
            input); // Retrieve the chat trigger by guild ID and input
        return ct != null; // Return true if the chat trigger exists, otherwise false
    }

    /// <summary>
    ///     Handles the event when a chat trigger should be reloaded.
    /// </summary>
    /// <param name="_">A boolean indicating if the chat trigger should be reloaded.</param>
    /// <returns>A value task representing the asynchronous operation.</returns>
    private ValueTask OnCrsShouldReload(bool _)
    {
        return new ValueTask(ReloadInternal(client.Guilds.Select(x => x.Id).ToList()));
    }

    /// <summary>
    ///     Handles the event when a global chat trigger is added.
    /// </summary>
    /// <param name="c">The chat trigger model that was added.</param>
    /// <returns>A value task representing the asynchronous operation.</returns>
    private async ValueTask OnGcrAdded(CTModel c)
    {
        await Task.CompletedTask;
        var newGlobalReactions =
            new CTModel[globalReactions.Length + 1]; // Create a new array with increased length
        Array.Copy(globalReactions, newGlobalReactions,
            globalReactions.Length); // Copy existing global reactions to the new array
        newGlobalReactions[globalReactions.Length] = c; // Add the new chat trigger to the end of the new array
        globalReactions = newGlobalReactions; // Update the global reactions array
    }

    /// <summary>
    ///     Handles the event when a global chat trigger is edited.
    /// </summary>
    /// <param name="c">The chat trigger model that was edited.</param>
    /// <returns>A value task representing the asynchronous operation.</returns>
    private async ValueTask OnGcrEdited(CTModel c)
    {
        {
            for (var i = 0; i < globalReactions.Length; i++)
            {
                if (globalReactions[i].Id != c.Id) // Check if the chat trigger ID does not match
                    continue;
                globalReactions[i] = c; // Update the chat trigger in the global reactions array
                return; // Return a completed value task
            }

            // If edited chat trigger is not found, add it
            await OnGcrAdded(c); // Call the method to handle the addition of the chat trigger
        }
    }

    /// <summary>
    ///     Handles the event when a global chat trigger is deleted.
    /// </summary>
    /// <param name="id">The ID of the chat trigger that was deleted.</param>
    /// <returns>A value task representing the asynchronous operation.</returns>
    private async ValueTask OnGcrDeleted(int id)
    {
        globalReactions =
            DeleteInternal(globalReactions, id); // Delete the chat trigger from the global reactions array
    }


    /// <summary>
    ///     Triggers the reloading of chat triggers.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task TriggerReloadChatTriggers()
    {
        return pubSub.Pub(crsReloadedKey, true);
    }

    /// <summary>
    ///     Handles the event when the bot leaves a guild.
    /// </summary>
    /// <param name="arg">The guild that the bot left.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private Task OnLeftGuild(SocketGuild arg)
    {
        newGuildReactions.TryRemove(arg.Id, out _); // Remove reactions for the guild from the dictionary
        return Task.CompletedTask; // Return a completed task
    }

    /// <summary>
    ///     Handles the event when the bot joins a guild.
    /// </summary>
    /// <param name="gc">The configuration of the guild that the bot joined.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task OnJoinedGuild(GuildConfig gc)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        // Initialize the database context
        newGuildReactions[gc.GuildId] = await dbContext // Update the reactions for the guild in the dictionary
            .ChatTriggers
            .Where(x => x.GuildId == gc.GuildId)
            .ToArrayAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a new chat trigger asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the trigger should be added.</param>
    /// <param name="key">The trigger key.</param>
    /// <param name="message">The trigger message.</param>
    /// <param name="regex">A boolean indicating whether the trigger uses regex.</param>
    /// <returns>The added chat trigger.</returns>
    public async Task<CTModel?> AddAsync(ulong? guildId, string key, string? message, bool regex)
    {
        key = key.ToLowerInvariant();
        var cr = new CTModel
        {
            GuildId = guildId,
            Trigger = key,
            Response = message,
            IsRegex = regex,
            ValidTriggerTypes = (int)ChatTriggerType.Message
        };

        if (cr.Response.Contains("%target", StringComparison.OrdinalIgnoreCase))
            cr.AllowTarget = true;

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        cr.Id = await dbContext.InsertWithInt32IdentityAsync(cr);

        await AddInternalAsync(guildId, cr).ConfigureAwait(false);
        return cr;
    }

    /// <summary>
    ///     Adds a reaction-based chat trigger asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the trigger should be added.</param>
    /// <param name="reaction">The emoji/emote that triggers the response.</param>
    /// <param name="message">The trigger message.</param>
    /// <returns>The added reaction trigger.</returns>
    public async Task<CTModel?> AddReactionTriggerAsync(ulong? guildId, string reaction, string? message)
    {
        // Clean up the reaction input
        reaction = reaction.Trim();

        // Convert potential emote formats to consistent format
        if (reaction.StartsWith('<') && reaction.EndsWith('>'))
        {
            // Custom emote format like <:name:id> - extract just the name
            var parts = reaction.Trim('<', '>').Split(':');
            if (parts.Length >= 2)
                reaction = parts[1]; // Get the emote name
        }
        else if (reaction.StartsWith(':') && reaction.EndsWith(':'))
        {
            // :emote_name: format - remove colons
            reaction = reaction.Trim(':');
        }

        var cr = new CTModel
        {
            GuildId = guildId,
            Trigger = reaction,
            Response = message,
            IsRegex = false, // Reaction triggers don't use regex
            ValidTriggerTypes = (int)ChatTriggerType.Reactions
        };

        // Check for target placeholder (though less common in reaction triggers)
        if (cr.Response.Contains("%target", StringComparison.OrdinalIgnoreCase))
            cr.AllowTarget = true;

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        cr.Id = await dbContext.InsertWithInt32IdentityAsync(cr);

        await AddInternalAsync(guildId, cr).ConfigureAwait(false);
        return cr;
    }

    /// <summary>
    ///     Edits an existing chat trigger asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the trigger belongs.</param>
    /// <param name="id">The ID of the trigger to edit.</param>
    /// <param name="message">The new trigger message.</param>
    /// <param name="regex">A boolean indicating whether the trigger uses regex.</param>
    /// <param name="trigger">The new trigger key.</param>
    /// <returns>The edited chat trigger.</returns>
    public async Task<CTModel?> EditAsync(ulong? guildId, int id, string? message, bool? regex, string? trigger = null)
    {
        // Initialize the database context
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var ct = await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id); // Retrieve the chat trigger by ID

        if (ct == null || ct.GuildId != guildId) // Check if the chat trigger exists or belongs to the guild
            return null;

        ct.IsRegex = regex ?? ct.IsRegex; // Update the regex flag

        // Disable allow target if message had target but it was removed
        if (!message.Contains("%target%", StringComparison.OrdinalIgnoreCase)
            && ct.Response.Contains("%target%", StringComparison.OrdinalIgnoreCase))
        {
            ct.AllowTarget = false; // Disable targeting
        }

        ct.Response = message; // Update the trigger message

        var oldTrigger = ct.Trigger;
        ct.Trigger = trigger ?? ct.Trigger; // Update the trigger key

        // Permissions used to be keyed by trigger text, which orphaned them whenever the trigger was renamed.
        // Re-key any legacy entries onto the trigger id so the rename keeps them attached.
        if (guildId.HasValue && !string.IsNullOrWhiteSpace(oldTrigger) &&
            !string.Equals(oldTrigger, ct.Trigger, StringComparison.OrdinalIgnoreCase))
        {
            await RekeyLegacyTriggerPermissionsAsync(guildId.Value, oldTrigger, ct.Id).ConfigureAwait(false);
        }

        // Enable allow target if message is edited to contain target
        if (ct.Response.Contains("%target%", StringComparison.OrdinalIgnoreCase))
            ct.AllowTarget = true; // Enable targeting

        // Save changes
        await UpdateInternalAsync(guildId.Value, ct).ConfigureAwait(false); // Update the trigger internally

        return ct; // Return the edited chat trigger
    }


    /// <summary>
    ///     Renders a duration in minutes as a compact day, hour and minute string.
    /// </summary>
    /// <param name="minutes">The duration in minutes.</param>
    /// <returns>A readable duration, such as "2d 4h".</returns>
    private static string FormatMinutes(int minutes)
    {
        var span = TimeSpan.FromMinutes(minutes);
        var parts = new List<string>();

        if (span.Days > 0)
            parts.Add($"{span.Days}d");
        if (span.Hours > 0)
            parts.Add($"{span.Hours}h");
        if (span.Minutes > 0 || parts.Count == 0)
            parts.Add($"{span.Minutes}m");

        return string.Join(" ", parts);
    }

    /// <summary>
    ///     Renders a trigger's serialized time conditions in a form suitable for an embed field.
    /// </summary>
    /// <param name="timeConditionsJson">The serialized conditions.</param>
    /// <returns>A readable summary of the windows the trigger is active in.</returns>
    private string FormatTimeConditions(string timeConditionsJson)
    {
        try
        {
            var conditions = JsonSerializer.Deserialize<TimeCondition[]>(timeConditionsJson);
            if (conditions is null || conditions.Length == 0)
                return "-";

            return string.Join("\n", conditions.Select(c =>
            {
                var days = c.DaysOfWeek is { Length: > 0 }
                    ? string.Join(", ", c.DaysOfWeek.Select(d => ((DayOfWeek)d).ToString()))
                    : "every day";
                return $"{c.StartTime} - {c.EndTime} ({days})";
            }));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to format time conditions");
            return "-";
        }
    }

    /// <summary>
    ///     Answers an interaction for a trigger that will not run, so Discord does not report a timeout.
    /// </summary>
    /// <param name="inter">The interaction to answer.</param>
    private async Task AcknowledgeUnavailableTriggerAsync(SocketInteraction inter)
    {
        try
        {
            var guildId = (inter.Channel as IGuildChannel)?.GuildId;
            await inter.RespondAsync(strings.CtTriggerUnavailable(guildId), ephemeral: true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to acknowledge an unavailable chat trigger interaction");
        }
    }

    /// <summary>
    ///     Splits a set of triggers into those that can be matched by an exact lookup and those that need evaluating.
    /// </summary>
    /// <param name="crs">The triggers to index.</param>
    /// <returns>The index for that set.</returns>
    /// <remarks>
    ///     Matching used to compare every message against every trigger. Most triggers are plain text with no prefix,
    ///     no regex and no positional matching, so they can be resolved with a dictionary lookup and only the rest have
    ///     to be scanned. The index is keyed on the trigger array itself, and every mutation path replaces that array
    ///     rather than editing it, so an index can never outlive the data it describes.
    /// </remarks>
    private static TriggerIndex GetIndex(CTModel[] crs)
    {
        return IndexCache.GetValue(crs, BuildIndex);
    }

    /// <summary>
    ///     Builds the exact-match lookup and the remainder list for a set of triggers.
    /// </summary>
    /// <param name="crs">The triggers to index.</param>
    /// <returns>The built index.</returns>
    private static TriggerIndex BuildIndex(CTModel[] crs)
    {
        var exact = new Dictionary<string, List<CTModel>>(StringComparer.Ordinal);
        var complex = new List<CTModel>();

        foreach (var ct in crs)
        {
            if (string.IsNullOrEmpty(ct.Trigger))
                continue;

            // A trigger qualifies for the lookup only when the whole message has to equal the trigger exactly:
            // no regex, no prefix to strip, no substring or target matching, and no mention stripping beforehand
            var isExact = !ct.IsRegex
                          && (RequirePrefixType)ct.PrefixType == RequirePrefixType.None
                          && !ct.ContainsAnywhere
                          && !ct.AllowTarget
                          && (CtRoleGrantType)ct.RoleGrantType is not (CtRoleGrantType.Mentioned
                          or CtRoleGrantType.Both);

            if (!isExact)
            {
                complex.Add(ct);
                continue;
            }

            if (!exact.TryGetValue(ct.Trigger, out var bucket))
                exact[ct.Trigger] = bucket = [];

            bucket.Add(ct);
        }

        return new TriggerIndex(exact, complex.ToArray());
    }

    /// <summary>
    ///     Records that a trigger fired, so the fire history is available for the dashboard and for %usecount%.
    /// </summary>
    /// <param name="ct">The trigger that fired.</param>
    /// <param name="guildId">The guild it fired in.</param>
    /// <param name="channelId">The channel it fired in.</param>
    /// <param name="user">The user that fired it.</param>
    /// <remarks>
    ///     Previously only the message and interaction paths recorded anything, so reaction and event fires were
    ///     invisible. Guild and user statistics opt-outs are honoured, in which case nothing is written.
    /// </remarks>
    private async Task RecordTriggerFireAsync(CTModel ct, ulong guildId, ulong channelId, IUser user)
    {
        try
        {
            var guildConfig = await guildSettings.GetGuildConfig(guildId).ConfigureAwait(false);
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var dbUser = await dbContext.GetOrCreateUser(user).ConfigureAwait(false);

            if (guildConfig.StatsOptOut || dbUser.StatsOptOut)
                return;

            await dbContext.InsertAsync(new CommandStat
            {
                ChannelId = channelId,
                Trigger = true,
                NameOrId = $"{ct.Id}",
                GuildId = guildId,
                UserId = user.Id,
                DateAdded = DateTime.UtcNow
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record fire for chat trigger {TriggerId}", ct.Id);
        }
    }

    /// <summary>
    ///     Gets a trigger's recent fires.
    /// </summary>
    /// <param name="guildId">The guild to read history for.</param>
    /// <param name="triggerId">The trigger to read history for.</param>
    /// <param name="limit">The maximum number of entries to return.</param>
    /// <returns>The total number of recorded fires, and the most recent ones, newest first.</returns>
    public async Task<(int Total, List<CommandStat> Recent)> GetTriggerHistoryAsync(ulong guildId, int triggerId,
        int limit = 10)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var query = dbContext.CommandStats
            .Where(x => x.Trigger && x.GuildId == guildId && x.NameOrId == $"{triggerId}");

        var total = await query.CountAsync().ConfigureAwait(false);
        var recent = await query
            .OrderByDescending(x => x.DateAdded)
            .Take(limit)
            .ToListAsync()
            .ConfigureAwait(false);

        return (total, recent);
    }

    /// <summary>
    ///     Explains why a trigger would or would not fire for a given user and sample message, without firing it.
    /// </summary>
    /// <param name="ct">The trigger to test.</param>
    /// <param name="guild">The guild to test in.</param>
    /// <param name="user">The user to test as.</param>
    /// <param name="channel">The channel to test in.</param>
    /// <param name="sample">The message text to match against.</param>
    /// <returns>
    ///     Whether the sample matched the trigger, and the reason it would be blocked, or null if it would fire.
    /// </returns>
    /// <remarks>
    ///     A trigger can now fail to fire for a dozen independent reasons, nearly all of them silent. This runs the
    ///     same checks the live paths run, in the same order, and reports the first one that rejects. Nothing here
    ///     mutates state: no currency is charged, no counter advances and no usage is recorded.
    /// </remarks>
    public async Task<(bool Matched, string? Blocker)> TestTriggerAsync(CTModel ct, SocketGuild guild, IGuildUser user,
        IMessageChannel channel, string sample)
    {
        if (ct.IsDisabled)
            return (false, strings.CtTestBlockedDisabled(guild.Id, ct.Id));

        if (ct.AllowBots)
            return (false, strings.CtTestBlockedBots(guild.Id));

        var (matched, _) = await MatchChatTriggers(sample.Trim().ToLowerInvariant(), [ct], guild)
            .ConfigureAwait(false);

        if (matched is null)
            return (false, strings.CtTestBlockedNoMatch(guild.Id));

        var fakeMsg = new MewdekoUserMessage
        {
            Author = user, Content = sample, Channel = channel
        };

        var pc = await perms.GetCacheFor(guild.Id).ConfigureAwait(false);
        if (!pc.Permissions.CheckPermissions(fakeMsg, ct.Trigger, "ActualChatTriggers", out var index,
                ct.Id.ToString()))
        {
            var entry = pc.Permissions[index].GetCommand(await guildSettings.GetPrefix(guild), guild);
            return (true, strings.CtTestBlockedPerm(guild.Id, index + 1, entry));
        }

        if (discordPermOverride.TryGetOverrides(guild.Id, ct.Trigger, out var guildPermission)
            && !user.GuildPermissions.Has(guildPermission))
        {
            return (true, strings.CtTestBlockedPermoverride(guild.Id, guildPermission.ToString()));
        }

        if (ct.ExpiresAt.HasValue && ct.ExpiresAt.Value <= DateTime.UtcNow)
        {
            return (true, strings.CtTestBlockedExpired(guild.Id,
                TimestampTag.FromDateTime(ct.ExpiresAt.Value).ToString()));
        }

        if (IsOnCooldown(ct, guild.Id, channel.Id, user.Id))
            return (true, strings.CtTestBlockedTriggerCooldown(guild.Id, ct.CooldownSeconds));

        if (!string.IsNullOrWhiteSpace(ct.CounterName) && (ct.CounterMin.HasValue || ct.CounterMax.HasValue))
        {
            var counterValue = await counters.GetAsync(guild.Id, ct.CounterName.ToLowerInvariant())
                .ConfigureAwait(false);

            if (ct.CounterMin.HasValue && counterValue < ct.CounterMin.Value ||
                ct.CounterMax.HasValue && counterValue > ct.CounterMax.Value)
            {
                return (true, strings.CtTestBlockedCounter(guild.Id, ct.CounterName, counterValue));
            }
        }

        if (ct.MaxUses.HasValue && ct.UseCount >= (ulong)Math.Max(0, ct.MaxUses.Value))
            return (true, strings.CtTestBlockedMaxuses(guild.Id, ct.UseCount, ct.MaxUses.Value));

        if (!stickyConditions.IsWithinTimeConditions(ct.TimeConditions, guild.Id, $"chat trigger {ct.Id}"))
            return (true, strings.CtTestBlockedTime(guild.Id));

        if (ct.MinAccountAgeMinutes > 0 &&
            DateTimeOffset.UtcNow - user.CreatedAt < TimeSpan.FromMinutes(ct.MinAccountAgeMinutes))
        {
            return (true, strings.CtTestBlockedAccountAge(guild.Id, FormatMinutes(ct.MinAccountAgeMinutes)));
        }

        if (ct.MinServerMembershipMinutes > 0 &&
            (user.JoinedAt is null ||
             DateTimeOffset.UtcNow - user.JoinedAt.Value < TimeSpan.FromMinutes(ct.MinServerMembershipMinutes)))
        {
            return (true, strings.CtTestBlockedMembership(guild.Id, FormatMinutes(ct.MinServerMembershipMinutes)));
        }

        if (ct.RequiredXpLevel > 0)
        {
            var stats = await xpService.GetUserXpStatsAsync(guild.Id, user.Id).ConfigureAwait(false);
            var level = stats?.Level ?? 0;
            if (level < ct.RequiredXpLevel)
                return (true, strings.CtTestBlockedLevel(guild.Id, level, ct.RequiredXpLevel));
        }

        if (ct.CurrencyCost > 0)
        {
            var balance = await currency.GetUserBalanceAsync(user.Id, guild.Id).ConfigureAwait(false);
            if (balance < ct.CurrencyCost)
                return (true, strings.CtTestBlockedCurrency(guild.Id, ct.CurrencyCost.ToString("N0")));
        }

        // Read the active cooldown set directly rather than calling TryBlock, which would start a cooldown as a
        // side effect of testing
        if (cmdCds.ActiveCooldowns.TryGetValue(guild.Id, out var activeCds)
            && activeCds.Any(x => x.UserId == user.Id && x.Command == ct.Trigger))
        {
            return (true, strings.CtTestBlockedCooldown(guild.Id));
        }

        return (true, null);
    }

    /// <summary>
    ///     Runs the trigger a fired trigger chains to, if it defines one.
    /// </summary>
    /// <param name="ct">The trigger that just fired.</param>
    /// <param name="guild">The guild the chain is running in.</param>
    /// <param name="msg">The message that started the chain.</param>
    /// <param name="fromBot">Whether the originating message was authored by a bot.</param>
    /// <param name="chainDepth">How many triggers deep the chain already is.</param>
    /// <param name="visited">The triggers this chain has already run, or null at the start of a chain.</param>
    /// <remarks>
    ///     The chained trigger runs through the full pipeline, so its own conditions, costs and permissions still
    ///     apply. A trigger cannot run twice in one chain and the depth is capped, so a cycle terminates on its first
    ///     repeat instead of spinning up to the limit.
    /// </remarks>
    private async Task RunChainedTriggerAsync(CTModel ct, IGuild guild, IUserMessage msg, bool fromBot,
        int chainDepth, HashSet<int>? visited)
    {
        if (ct.NextTriggerId is not { } nextId)
            return;

        if (chainDepth >= MaxChainDepth)
        {
            logger.LogWarning("Chat trigger chain from {TriggerId} in {GuildId} hit the depth limit", ct.Id, guild.Id);
            return;
        }

        visited ??= [];
        visited.Add(ct.Id);

        if (!visited.Add(nextId))
        {
            logger.LogWarning("Chat trigger chain from {TriggerId} in {GuildId} loops back to {NextId}", ct.Id,
                guild.Id, nextId);
            return;
        }

        var next = await GetGuildOrGlobalTriggers(guild.Id, nextId).ConfigureAwait(false);
        if (next is null)
            return;

        await RunBehaviorInternal(guild, msg, fromBot, chainDepth + 1, next, visited).ConfigureAwait(false);
    }

    /// <summary>
    ///     Runs triggers that opted into bot and webhook messages.
    /// </summary>
    /// <param name="msg">The message that was received.</param>
    /// <remarks>
    ///     Only bot-authored messages are handled here, since human messages already reach the trigger service through
    ///     the command pipeline. The bot never reacts to its own messages, which is what keeps two triggers from
    ///     answering each other forever.
    /// </remarks>
    private async Task OnMessageReceived(SocketMessage msg)
    {
        if (!ready || msg is not SocketUserMessage userMsg)
            return;

        // Human messages are handled by the command pipeline; our own messages would loop
        if (!msg.Author.IsBot || msg.Author.Id == client.CurrentUser?.Id)
            return;

        if (msg.Channel is not SocketTextChannel channel)
            return;

        try
        {
            await RunBehaviorInternal(channel.Guild, userMsg, true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error running bot message chat trigger in {GuildId}", channel.Guild.Id);
        }
    }

    /// <summary>
    ///     Adds a trigger's configured reactions in the background.
    /// </summary>
    /// <param name="ct">The trigger that fired.</param>
    /// <param name="responseMsg">The message the trigger sent, if it sent one.</param>
    /// <param name="triggerMsg">The message that fired the trigger.</param>
    /// <remarks>
    ///     Discord is rate limited on reactions, so these are paced a second apart. That pacing used to run inline
    ///     inside the early behaviour the command pipeline awaits, which stalled the channel's message processing for
    ///     up to six seconds per fire. Running it detached keeps the pacing without holding up the pipeline.
    /// </remarks>
    private void QueueTriggerReactions(CTModel ct, IUserMessage? responseMsg, IUserMessage? triggerMsg)
    {
        var reactions = ct.GetReactions();
        if (reactions.Length == 0)
            return;

        // React to the response when the trigger sent one and is not configured to react to the trigger instead
        var target = !ct.ReactToTrigger && !ct.NoRespond ? responseMsg : triggerMsg;
        if (target is null)
            return;

        _ = Task.Run(async () =>
        {
            foreach (var reaction in reactions)
            {
                try
                {
                    await target.AddReactionAsync(reaction.ToIEmote()).ConfigureAwait(false);
                }
                catch
                {
                    logger.LogWarning("Unable to add reactions to message {Message} in server {GuildId}", target.Id,
                        ct.GuildId);
                    break;
                }

                await Task.Delay(1000).ConfigureAwait(false);
            }
        });
    }

    /// <summary>
    ///     Records that a regex trigger timed out, disabling it once it has done so repeatedly.
    /// </summary>
    /// <param name="ct">The trigger whose pattern timed out.</param>
    /// <remarks>
    ///     A pattern slow enough to time out will keep timing out on every message, burning the budget each time. After
    ///     a few strikes the trigger is disabled rather than left to degrade the guild silently, which also makes the
    ///     problem visible: the trigger shows as disabled instead of merely never matching.
    /// </remarks>
    private void NoteRegexTimeout(CTModel ct)
    {
        var strikes = regexTimeouts.AddOrUpdate(ct.Id, 1, (_, count) => count + 1);

        logger.LogWarning("Chat trigger {TriggerId} regex timed out ({Strikes}/{Limit})", ct.Id, strikes,
            RegexTimeoutStrikes);

        if (strikes < RegexTimeoutStrikes)
            return;

        regexTimeouts.TryRemove(ct.Id, out _);
        ct.IsDisabled = true;

        _ = Task.Run(async () =>
        {
            try
            {
                await using var dbContext = await dbFactory.CreateConnectionAsync();
                await dbContext.ChatTriggers
                    .Where(x => x.Id == ct.Id)
                    .Set(x => x.IsDisabled, true)
                    .UpdateAsync()
                    .ConfigureAwait(false);

                logger.LogWarning("Disabled chat trigger {TriggerId} after repeated regex timeouts", ct.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to disable timing out chat trigger {TriggerId}", ct.Id);
            }
        });
    }

    /// <summary>
    ///     Gets the contextual placeholders other modules contribute to trigger responses.
    /// </summary>
    /// <returns>The available placeholder tokens.</returns>
    public IReadOnlyCollection<string> GetContextualPlaceholders()
    {
        return placeholders.GetAvailablePlaceholders();
    }

    /// <summary>
    ///     Fires every enabled trigger in a guild that listens for the given event.
    /// </summary>
    /// <param name="guildId">The guild the event occurred in.</param>
    /// <param name="eventType">The event that occurred.</param>
    /// <param name="user">The user the event concerns.</param>
    /// <param name="fallbackChannel">
    ///     The channel to respond in when a trigger does not name one of its own, or null to skip triggers without an
    ///     explicit channel.
    /// </param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    ///     This is the entry point other modules call to let chat triggers format their notifications. Each trigger is
    ///     run through the same condition and economy gates as a message trigger, so a trigger cannot be made to bypass
    ///     its own rules by being driven from an event.
    /// </remarks>
    public async Task FireEventTriggersAsync(ulong guildId, CtEventType eventType, IUser user,
        IMessageChannel? fallbackChannel = null)
    {
        if (!ready || eventType == CtEventType.None)
            return;

        var guild = client.GetGuild(guildId);
        if (guild is null)
            return;

        var triggers = (await GetChatTriggersFor(guildId).ConfigureAwait(false))
            .Where(x => !x.IsDisabled
                        && x.EventType == (int)eventType
                        && ((ChatTriggerType)x.ValidTriggerTypes).HasFlag(ChatTriggerType.Event))
            .ToArray();

        foreach (var ct in triggers)
        {
            try
            {
                var channel = ct.EventChannelId != 0
                    ? guild.GetTextChannel(ct.EventChannelId) as IMessageChannel
                    : fallbackChannel;

                if (channel is null)
                    continue;

                if (!await PassesConditionsAsync(ct, guildId, channel.Id, user).ConfigureAwait(false))
                    continue;

                if (!await TryApplyEconomyAsync(ct, guildId, user, channel).ConfigureAwait(false))
                    continue;

                await IncrementTriggerUsage(ct).ConfigureAwait(false);
                await RecordTriggerFireAsync(ct, guildId, channel.Id, user).ConfigureAwait(false);

                var fakeMsg = new MewdekoUserMessage
                {
                    Author = user, Content = ct.Trigger ?? "", Channel = channel
                };

                foreach (var response in await SelectResponsesAsync(ct).ConfigureAwait(false))
                {
                    await ct.Send(fakeMsg, client, false, dbFactory, placeholders, null, response)
                        .ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(ct.GrantedRoles) || !string.IsNullOrWhiteSpace(ct.RemovedRoles))
                    await HandleRoleOperations(ct, guild, user, null, channel).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error running event chat trigger {TriggerId} in {GuildId}", ct.Id, guildId);
            }
        }
    }

    /// <summary>
    ///     Fires event triggers when a member's XP level changes.
    /// </summary>
    /// <param name="args">The level change details.</param>
    private async Task OnXpLevelChanged(XpLevelChangedEventArgs args)
    {
        var guild = client.GetGuild(args.GuildId);
        var user = guild?.GetUser(args.UserId);
        if (user is null)
            return;

        var channel = args.ChannelId == 0 ? null : guild.GetTextChannel(args.ChannelId) as IMessageChannel;

        await FireEventTriggersAsync(args.GuildId,
            args.IsLevelUp ? CtEventType.XpLevelUp : CtEventType.XpLevelDown, user, channel).ConfigureAwait(false);
    }

    /// <summary>
    ///     Fires event triggers when a member joins the guild.
    /// </summary>
    /// <param name="user">The member that joined.</param>
    private Task OnUserJoined(IGuildUser user)
    {
        return FireEventTriggersAsync(user.Guild.Id, CtEventType.MemberJoin, user);
    }

    /// <summary>
    ///     Fires event triggers when a member leaves the guild.
    /// </summary>
    /// <param name="guild">The guild the member left.</param>
    /// <param name="user">The member that left.</param>
    private Task OnUserLeft(IGuild guild, IUser user)
    {
        return FireEventTriggersAsync(guild.Id, CtEventType.MemberLeave, user);
    }

    /// <summary>
    ///     Fires event triggers when a member starts or stops boosting the server.
    /// </summary>
    /// <param name="before">The member's previous state, which may not be cached.</param>
    /// <param name="after">The member's new state.</param>
    private async Task OnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> before, SocketGuildUser after)
    {
        // Without the previous state there is no way to tell a new boost from an existing one, so nothing fires
        if (!before.HasValue)
            return;

        var wasBoosting = before.Value.PremiumSince.HasValue;
        var isBoosting = after.PremiumSince.HasValue;

        if (wasBoosting == isBoosting)
            return;

        await FireEventTriggersAsync(after.Guild.Id, isBoosting ? CtEventType.Boost : CtEventType.BoostEnd, after)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Fires event triggers when a member joins or leaves a voice channel.
    /// </summary>
    /// <param name="user">The member whose voice state changed.</param>
    /// <param name="before">The previous voice state.</param>
    /// <param name="after">The new voice state.</param>
    private async Task OnVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        if (user is not IGuildUser guildUser || before.VoiceChannel?.Id == after.VoiceChannel?.Id)
            return;

        if (after.VoiceChannel is not null && before.VoiceChannel is null)
            await FireEventTriggersAsync(guildUser.GuildId, CtEventType.VoiceJoin, user).ConfigureAwait(false);
        else if (after.VoiceChannel is null && before.VoiceChannel is not null)
            await FireEventTriggersAsync(guildUser.GuildId, CtEventType.VoiceLeave, user).ConfigureAwait(false);
    }

    /// <summary>
    ///     Checks the conditions that gate whether a trigger is allowed to fire at all.
    /// </summary>
    /// <param name="ct">The trigger being fired.</param>
    /// <param name="guildId">The guild the trigger fired in.</param>
    /// <param name="channelId">The channel the trigger fired in, used for channel scoped cooldowns.</param>
    /// <param name="user">The user that fired the trigger.</param>
    /// <returns>True if the trigger may fire.</returns>
    /// <remarks>
    ///     These are the conditions the permission system cannot express: when in the day or week the trigger is live,
    ///     how long it stays live, how often it may fire, and how established the invoking account has to be. Time
    ///     conditions reuse the sticky message condition format, so both features read the same way.
    /// </remarks>
    private async Task<bool> PassesConditionsAsync(CTModel ct, ulong guildId, ulong channelId, IUser user)
    {
        if (ct.ExpiresAt.HasValue && ct.ExpiresAt.Value <= DateTime.UtcNow)
            return false;

        if (IsOnCooldown(ct, guildId, channelId, user.Id))
            return false;

        if (!string.IsNullOrWhiteSpace(ct.CounterName) && (ct.CounterMin.HasValue || ct.CounterMax.HasValue))
        {
            var value = await counters.GetAsync(guildId, ct.CounterName.ToLowerInvariant()).ConfigureAwait(false);

            if (ct.CounterMin.HasValue && value < ct.CounterMin.Value)
                return false;

            if (ct.CounterMax.HasValue && value > ct.CounterMax.Value)
                return false;
        }

        if (ct.MaxUses.HasValue && ct.UseCount >= (ulong)Math.Max(0, ct.MaxUses.Value))
            return false;

        if (!stickyConditions.IsWithinTimeConditions(ct.TimeConditions, guildId, $"chat trigger {ct.Id}"))
            return false;

        if (ct.MinAccountAgeMinutes > 0 &&
            DateTimeOffset.UtcNow - user.CreatedAt < TimeSpan.FromMinutes(ct.MinAccountAgeMinutes))
        {
            return false;
        }

        if (ct.MinServerMembershipMinutes > 0)
        {
            if (user is not IGuildUser { JoinedAt: not null } guildUser)
                return false;

            if (DateTimeOffset.UtcNow - guildUser.JoinedAt.Value <
                TimeSpan.FromMinutes(ct.MinServerMembershipMinutes))
            {
                return false;
            }
        }

        StartCooldown(ct, guildId, channelId, user.Id);

        return true;
    }

    /// <summary>
    ///     Checks whether a trigger's own cooldown is currently active for a fire.
    /// </summary>
    /// <param name="ct">The trigger being fired.</param>
    /// <param name="guildId">The guild the trigger fired in.</param>
    /// <param name="channelId">The channel the trigger fired in.</param>
    /// <param name="userId">The user firing the trigger.</param>
    /// <returns>True if the trigger is still cooling down.</returns>
    /// <remarks>
    ///     This is separate from the guild-wide command cooldown set with <c>.cmdcd</c>, which is keyed by command
    ///     name and always per user. A trigger's own cooldown travels with the trigger, survives a rename and can be
    ///     shared across a channel or the whole server.
    /// </remarks>
    private bool IsOnCooldown(CTModel ct, ulong guildId, ulong channelId, ulong userId)
    {
        if (ct.CooldownSeconds <= 0)
            return false;

        var key = BuildCooldownKey(ct, guildId, channelId, userId);

        if (!triggerCooldowns.TryGetValue(key, out var until))
            return false;

        if (until > DateTime.UtcNow)
            return true;

        triggerCooldowns.TryRemove(key, out _);
        return false;
    }

    /// <summary>
    ///     Starts a trigger's cooldown after it has passed every other check.
    /// </summary>
    /// <param name="ct">The trigger that is about to fire.</param>
    /// <param name="guildId">The guild the trigger fired in.</param>
    /// <param name="channelId">The channel the trigger fired in.</param>
    /// <param name="userId">The user firing the trigger.</param>
    private void StartCooldown(CTModel ct, ulong guildId, ulong channelId, ulong userId)
    {
        if (ct.CooldownSeconds <= 0)
            return;

        triggerCooldowns[BuildCooldownKey(ct, guildId, channelId, userId)] =
            DateTime.UtcNow.AddSeconds(ct.CooldownSeconds);
    }

    /// <summary>
    ///     Builds the key a trigger's cooldown is tracked under, according to its scope.
    /// </summary>
    /// <param name="ct">The trigger being fired.</param>
    /// <param name="guildId">The guild the trigger fired in.</param>
    /// <param name="channelId">The channel the trigger fired in.</param>
    /// <param name="userId">The user firing the trigger.</param>
    /// <returns>The cooldown key.</returns>
    private static string BuildCooldownKey(CTModel ct, ulong guildId, ulong channelId, ulong userId)
    {
        return (CtCooldownScope)ct.CooldownScope switch
        {
            CtCooldownScope.Channel => $"{ct.Id}:c:{channelId}",
            CtCooldownScope.Guild => $"{ct.Id}:g:{guildId}",
            _ => $"{ct.Id}:u:{userId}"
        };
    }

    /// <summary>
    ///     Picks which of a trigger's responses to send, honouring its response mode.
    /// </summary>
    /// <param name="ct">The trigger being fired.</param>
    /// <returns>The responses to send, in order. Never empty unless the trigger has no response at all.</returns>
    /// <remarks>
    ///     Round robin advances a stored index, so the rotation is shared by everyone in the guild and survives a
    ///     restart rather than restarting per user.
    /// </remarks>
    private async Task<IReadOnlyList<string>> SelectResponsesAsync(CTModel ct)
    {
        var responses = ct.GetResponses();

        if (responses.Count <= 1)
            return responses;

        switch ((CtResponseMode)ct.ResponseMode)
        {
            case CtResponseMode.Random:
                return [responses[rng.Next(0, responses.Count)]];

            case CtResponseMode.RoundRobin:
                var index = ct.RoundRobinIndex % responses.Count;
                var next = (index + 1) % responses.Count;

                ct.RoundRobinIndex = next;
                try
                {
                    await using var dbContext = await dbFactory.CreateConnectionAsync();
                    await dbContext.ChatTriggers
                        .Where(x => x.Id == ct.Id)
                        .Set(x => x.RoundRobinIndex, next)
                        .UpdateAsync()
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to advance round robin index for chat trigger {TriggerId}", ct.Id);
                }

                return [responses[index]];

            case CtResponseMode.All:
                return responses;

            case CtResponseMode.Single:
            default:
                return [responses[0]];
        }
    }

    /// <summary>
    ///     Checks a trigger's economy requirements and, when they are met, applies its economy rewards.
    /// </summary>
    /// <param name="ct">The trigger being fired.</param>
    /// <param name="guildId">The guild the trigger fired in.</param>
    /// <param name="user">The user that fired the trigger.</param>
    /// <param name="channel">The channel to report a failed requirement in, or null to fail silently.</param>
    /// <returns>True if the trigger may fire; false if a requirement was not met.</returns>
    /// <remarks>
    ///     The currency cost is debited before any reward is granted, so a trigger that both costs and pays out can
    ///     never pay out when the user could not afford it. A failure only reports back to the channel when the trigger
    ///     defines a message for it, which keeps gated triggers from becoming a source of spam.
    /// </remarks>
    private async Task<bool> TryApplyEconomyAsync(CTModel ct, ulong guildId, IUser user, IMessageChannel? channel)
    {
        if (ct.RequiredXpLevel <= 0 && ct.CurrencyCost <= 0 && ct.CurrencyReward <= 0 && ct.XpReward <= 0)
            return true;

        try
        {
            if (ct.RequiredXpLevel > 0)
            {
                var stats = await xpService.GetUserXpStatsAsync(guildId, user.Id).ConfigureAwait(false);
                if ((stats?.Level ?? 0) < ct.RequiredXpLevel)
                {
                    await ReportRequirementFailureAsync(ct, guildId, channel).ConfigureAwait(false);
                    return false;
                }
            }

            if (ct.CurrencyCost > 0)
            {
                var debited = await currency
                    .TryDebitAsync(user.Id, ct.CurrencyCost, $"Chat trigger {ct.Id}", CurrencyCategory.ChatTrigger,
                        guildId, "chattrigger")
                    .ConfigureAwait(false);

                if (!debited)
                {
                    await ReportRequirementFailureAsync(ct, guildId, channel).ConfigureAwait(false);
                    return false;
                }
            }

            if (ct.CurrencyReward > 0)
            {
                await currency
                    .CreditAsync(user.Id, ct.CurrencyReward, $"Chat trigger {ct.Id}", CurrencyCategory.ChatTrigger,
                        guildId, "chattrigger")
                    .ConfigureAwait(false);
            }

            if (ct.XpReward > 0)
                await xpService.AddXpAsync(guildId, user.Id, ct.XpReward).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply economy effects for chat trigger {TriggerId} in {GuildId}",
                ct.Id, guildId);
            return true;
        }
    }

    /// <summary>
    ///     Sends a trigger's requirement failure message, if it defines one.
    /// </summary>
    /// <param name="ct">The trigger whose requirement was not met.</param>
    /// <param name="guildId">The guild the trigger fired in.</param>
    /// <param name="channel">The channel to report in, or null to stay silent.</param>
    private async Task ReportRequirementFailureAsync(CTModel ct, ulong guildId, IMessageChannel? channel)
    {
        if (channel is null || string.IsNullOrWhiteSpace(ct.RequirementFailMessage))
            return;

        try
        {
            await channel.SendErrorAsync(ct.RequirementFailMessage, configService.Data).ConfigureAwait(false);
        }
        catch
        {
            // Ignored: the trigger is already blocked, a failed notice should not surface as an error.
        }
    }

    /// <summary>
    ///     Rewrites permission entries that were keyed by a chat trigger's text so that they are keyed by the
    ///     trigger's id instead.
    /// </summary>
    /// <param name="guildId">The guild whose permissions should be rewritten.</param>
    /// <param name="oldTrigger">The trigger text the entries are currently keyed by.</param>
    /// <param name="triggerId">The id to re-key the entries onto.</param>
    private async Task RekeyLegacyTriggerPermissionsAsync(ulong guildId, string oldTrigger, int triggerId)
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();

            var newKey = triggerId.ToString();
            var updated = await dbContext.Permissions1
                .Where(x => x.GuildId == guildId
                            && x.IsCustomCommand
                            && x.SecondaryTarget == (int)SecondaryPermissionType.Command
                            && x.SecondaryTargetName == oldTrigger)
                .Set(x => x.SecondaryTargetName, newKey)
                .UpdateAsync()
                .ConfigureAwait(false);

            if (updated > 0)
                perms.Cache.TryRemove(guildId, out _);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to re-key permissions for chat trigger {TriggerId} in {GuildId}",
                triggerId, guildId);
        }
    }

    /// <summary>
    ///     Deletes a chat trigger asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the trigger belongs.</param>
    /// <param name="id">The ID of the trigger to delete.</param>
    /// <returns>The deleted chat trigger, or null if not found or not permitted.</returns>
    public async Task<CTModel?> DeleteAsync(ulong? guildId, int id)
    {
        // Initialize the database context
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var toDelete =
            await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id); // Retrieve the chat trigger by ID

        if (toDelete is null) // Check if the trigger exists
            return null;

        if (toDelete.GuildId is not (null or 0) &&
            (guildId == null || guildId != toDelete.GuildId)) // Check permission to delete
            return null; // Return null if deletion is not permitted
        await dbContext.DeleteAsync(toDelete); // Remove the trigger from the database
        await DeleteInternalAsync(guildId, id).ConfigureAwait(false); // Delete the trigger internally
        return toDelete; // Return the deleted trigger
    }

    /// <summary>
    ///     Sets the role grant type of a chat trigger asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the trigger belongs.</param>
    /// <param name="id">The ID of the trigger to update.</param>
    /// <param name="type">The new role grant type.</param>
    /// <returns>The updated chat trigger, or null if not found or not permitted.</returns>
    public async Task<CTModel?> SetRoleGrantType(ulong? guildId, int id, CtRoleGrantType type)
    {
        // Initialize the database context
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var ct = await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id); // Retrieve the chat trigger by ID

        if (ct == null || ct.GuildId != guildId) // Check if the trigger exists and belongs to the guild
            return null;

        ct.RoleGrantType = (int)type; // Update the role grant type
        await dbContext.UpdateAsync(ct);
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false); // Update the trigger internally

        return ct; // Return the updated trigger
    }

    /// <summary>
    ///     Sets the interaction type of a chat trigger asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the trigger belongs.</param>
    /// <param name="id">The ID of the trigger to update.</param>
    /// <param name="type">The new interaction type.</param>
    /// <returns>The updated chat trigger, or null if not found or not permitted.</returns>
    public async Task<CTModel?> SetInteractionType(ulong? guildId, int id, CtApplicationCommandType type)
    {
        // Initialize the database context
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var ct = await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id); // Retrieve the chat trigger by ID

        if (ct == null || ct.GuildId != guildId) // Check if the trigger exists and belongs to the guild
            return null;

        ct.ApplicationCommandType = (int)type; // Update the interaction type
        // Save changes
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false); // Update the trigger internally

        return ct; // Return the updated trigger
    }

    /// <summary>
    ///     Enables or disables every trigger in a category at once.
    /// </summary>
    /// <param name="guildId">The guild whose triggers should be changed.</param>
    /// <param name="category">The category to act on.</param>
    /// <param name="disabled">Whether the triggers should be disabled.</param>
    /// <returns>The number of triggers changed.</returns>
    public async Task<int> SetCategoryDisabledAsync(ulong guildId, string category, bool disabled)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var changed = await dbContext.ChatTriggers
            .Where(x => x.GuildId == guildId && x.Category != null && x.Category.ToLower() == category.ToLower())
            .Set(x => x.IsDisabled, disabled)
            .UpdateAsync()
            .ConfigureAwait(false);

        if (changed > 0)
            await TriggerReloadChatTriggers().ConfigureAwait(false);

        return changed;
    }

    /// <summary>
    ///     Gets the categories triggers are grouped into, with how many triggers each holds.
    /// </summary>
    /// <param name="guildId">The guild to list categories for.</param>
    /// <returns>Each category name, its trigger count and how many of those are disabled.</returns>
    public async Task<List<(string Category, int Total, int Disabled)>> GetCategoriesAsync(ulong guildId)
    {
        var triggers = await GetChatTriggersFor(guildId).ConfigureAwait(false);

        return triggers
            .Where(x => !string.IsNullOrWhiteSpace(x.Category))
            .GroupBy(x => x.Category!, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, g.Count(), g.Count(x => x.IsDisabled)))
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    ///     Applies an arbitrary modification to a chat trigger and persists it.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the trigger belongs.</param>
    /// <param name="id">The ID of the trigger to update.</param>
    /// <param name="modify">The modification to apply to the trigger.</param>
    /// <returns>The updated chat trigger, or null if not found or not permitted.</returns>
    public async Task<CTModel?> ModifyAsync(ulong? guildId, int id, Action<CTModel> modify)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var ct = await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id);

        if (ct == null || ct.GuildId != guildId)
            return null;

        modify(ct);
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false);

        return ct;
    }

    /// <summary>
    ///     Sets the interaction name of a chat trigger asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the trigger belongs.</param>
    /// <param name="id">The ID of the trigger to update.</param>
    /// <param name="name">The new interaction name.</param>
    /// <returns>The updated chat trigger, or null if not found or not permitted.</returns>
    public async Task<CTModel?> SetInteractionName(ulong? guildId, int id, string name)
    {
        // Initialize the database context
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var ct = await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id); // Retrieve the chat trigger by ID

        if (ct == null || ct.GuildId != guildId) // Check if the trigger exists and belongs to the guild
            return null;

        ct.ApplicationCommandName = name; // Update the interaction name
        // Save changes
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false); // Update the trigger internally

        return ct; // Return the updated trigger
    }


    /// <summary>
    ///     Sets the interaction description of a chat trigger asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the trigger belongs.</param>
    /// <param name="id">The ID of the trigger to update.</param>
    /// <param name="description">The new interaction description.</param>
    /// <returns>The updated chat trigger, or null if not found or not permitted.</returns>
    public async Task<CTModel?> SetInteractionDescription(ulong? guildId, int id, string description)
    {
        // Initialize the database context
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var ct = await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id); // Retrieve the chat trigger by ID

        if (ct == null || ct.GuildId != guildId) // Check if the trigger exists and belongs to the guild
            return null;

        ct.ApplicationCommandDescription = description; // Update the interaction description
        // Save changes
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false); // Update the trigger internally

        return ct; // Return the updated trigger
    }

    /// <summary>
    ///     Sets the ephemeral response property of a chat trigger asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the trigger belongs.</param>
    /// <param name="id">The ID of the trigger to update.</param>
    /// <param name="ephemeral">The value indicating whether the response should be ephemeral.</param>
    /// <returns>The updated chat trigger, or null if not found or not permitted.</returns>
    public async Task<CTModel?> SetInteractionEphemeral(ulong? guildId, int id, bool ephemeral)
    {
        // Initialize the database context
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var ct = await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id); // Retrieve the chat trigger by ID

        if (ct == null || ct.GuildId != guildId) // Check if the trigger exists and belongs to the guild
            return null;

        ct.EphemeralResponse = ephemeral; // Update the ephemeral response
        // Save changes
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false); // Update the trigger internally

        return ct; // Return the updated trigger
    }

    /// <summary>
    ///     Sets the prefix type of a chat trigger asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the trigger belongs.</param>
    /// <param name="id">The ID of the trigger to update.</param>
    /// <param name="type">The new prefix type.</param>
    /// <returns>The updated chat trigger, or null if not found or not permitted.</returns>
    public async Task<CTModel?> SetPrefixType(ulong? guildId, int id, RequirePrefixType type)
    {
        // Initialize the database context
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var ct = await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id); // Retrieve the chat trigger by ID

        if (ct == null || ct.GuildId != guildId) // Check if the trigger exists and belongs to the guild
            return null;

        ct.PrefixType = (int)type; // Update the prefix type
        // Save changes
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false); // Update the trigger internally

        return ct; // Return the updated trigger
    }

    /// <summary>
    ///     Sets the custom prefix of a chat trigger asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the trigger belongs.</param>
    /// <param name="id">The ID of the trigger to update.</param>
    /// <param name="name">The new custom prefix.</param>
    /// <returns>The updated chat trigger, or null if not found or not permitted.</returns>
    public async Task<CTModel?> SetPrefix(ulong? guildId, int id, string name)
    {
        // Initialize the database context
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var ct = await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id); // Retrieve the chat trigger by ID

        if (ct == null || ct.GuildId != guildId) // Check if the trigger exists and belongs to the guild
            return null;

        ct.CustomPrefix = name; // Update the custom prefix
        // Save changes
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false); // Update the trigger internally

        return ct; // Return the updated trigger
    }

    /// <summary>
    ///     Sets the crossposting webhook URL and channel ID of a chat trigger asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the trigger belongs.</param>
    /// <param name="id">The ID of the trigger to update.</param>
    /// <param name="webhookUrl">The new webhook URL to set.</param>
    /// <param name="bypassTest">Indicates whether to bypass the test of the webhook.</param>
    /// <returns>
    ///     A tuple containing the updated chat trigger (or null if not found or not permitted) and a boolean indicating if the
    ///     operation was successful.
    /// </returns>
    public async Task<(CTModel? Trigger, bool Valid)> SetCrosspostingWebhookUrl(ulong? guildId, int id,
        string webhookUrl, bool bypassTest = false)
    {
        if (!bypassTest) // Check if bypass test is disabled
        {
            try
            {
                using var discordWebhookClient =
                    new DiscordWebhookClient(webhookUrl); // Initialize a Discord webhook client
                await discordWebhookClient
                    .SendMessageAsync(strings.CrosspostTest(guildId)) // Send a test message
                    .ConfigureAwait(false);
            }
            catch // Handle exceptions
            {
                return (null, false); // Return false if test fails
            }
        }

        // Initialize the database context
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var ct = await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id); // Retrieve the chat trigger by ID

        if (ct == null || ct.GuildId != guildId) // Check if the trigger exists and belongs to the guild
            return (null, true); // Return true if the trigger is not found or permitted

        ct.CrosspostingWebhookUrl = webhookUrl; // Update the webhook URL
        ct.CrosspostingChannelId = 0ul; // Reset the channel ID
        // Save changes
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false); // Update the trigger internally

        return (ct, true); // Return the updated trigger and true
    }


    /// <summary>
    ///     Sets the channel ID for crossposting of a chat trigger asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the trigger belongs.</param>
    /// <param name="id">The ID of the trigger to update.</param>
    /// <param name="channelId">The ID of the channel where crossposting will occur.</param>
    /// <returns>The updated chat trigger, or null if not found or not permitted.</returns>
    public async Task<CTModel?> SetCrosspostingChannelId(ulong? guildId, int id, ulong channelId)
    {
        // Initialize the database context
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var ct = await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id); // Retrieve the chat trigger by ID

        if (ct == null || ct.GuildId != guildId) // Check if the trigger exists and belongs to the guild
            return null;

        ct.CrosspostingWebhookUrl = ""; // Clear the webhook URL
        ct.CrosspostingChannelId = channelId; // Set the crossposting channel ID
        // Save changes
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false); // Update the trigger internally

        return ct; // Return the updated trigger
    }

    /// <summary>
    ///     Sets the validity of a trigger type for a chat trigger asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the trigger belongs.</param>
    /// <param name="id">The ID of the trigger to update.</param>
    /// <param name="type">The type of trigger.</param>
    /// <param name="enabled">Whether the trigger type is enabled or disabled.</param>
    /// <returns>The updated chat trigger, or null if not found or not permitted.</returns>
    public async Task<CTModel?> SetValidTriggerType(ulong? guildId, int id, ChatTriggerType type, bool enabled)
    {
        // Initialize the database context
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var ct = await dbContext.ChatTriggers.FirstOrDefaultAsync(x => x.Id == id); // Retrieve the chat trigger by ID

        if (ct == null || ct.GuildId != guildId) // Check if the trigger exists and belongs to the guild
            return null;

        switch (enabled) // Update the validity of the trigger type
        {
            case true when !((ChatTriggerType)ct.ValidTriggerTypes).HasFlag(type):
                ct.ValidTriggerTypes |= (int)type; // Enable the trigger type
                break;
            case false when ((ChatTriggerType)ct.ValidTriggerTypes).HasFlag(type):
                ct.ValidTriggerTypes ^= (int)type; // Disable the trigger type
                break;
        }

        // Save changes
        await UpdateInternalAsync(guildId, ct).ConfigureAwait(false); // Update the trigger internally
        await dbContext.UpdateAsync(ct);
        return ct; // Return the updated trigger
    }

    /// <summary>
    ///     Retrieves chat triggers for a specified guild asynchronously.
    /// </summary>
    /// <param name="maybeGuildId">The ID of the guild to retrieve triggers for.</param>
    /// <returns>An array of chat triggers for the specified guild.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<CTModel[]> GetChatTriggersFor(ulong? maybeGuildId)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        if (maybeGuildId is { } guildId and not 0) // Check if a valid guild ID is provided
        {
            return newGuildReactions != null && newGuildReactions.TryGetValue(guildId, out var cts)
                ? cts
                : []; // Return an empty array if no triggers found
        }


        return globalReactions ?? []; // Return global triggers if no guild ID specified
    }


    /// <summary>
    ///     Toggles the granted role for a chat trigger asynchronously.
    /// </summary>
    /// <param name="ct">The chat trigger to update.</param>
    /// <param name="rId">The ID of the role to toggle.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ToggleGrantedRole(CTModel ct, ulong rId)
    {
        // Initialize the database context
        var roles = ct.GetGrantedRoles(); // Get the granted roles for the trigger

        if (!roles.Contains(rId))
            roles.Add(rId); // Add the role ID if not present
        else
            roles.RemoveAll(x => x == rId); // Remove the role ID if already present

        await using var dbContext = await dbFactory.CreateConnectionAsync();
        ct.GrantedRoles = string.Join("@@@", roles.Select(x => x.ToString())); // Update the granted roles
        await dbContext.UpdateAsync(ct); // Update the chat trigger in the database
        // Save changes
        await UpdateInternalAsync(ct.GuildId, ct).ConfigureAwait(false); // Update the trigger internally
    }

    /// <summary>
    ///     Toggles the removed role for a chat trigger asynchronously.
    /// </summary>
    /// <param name="ct">The chat trigger to update.</param>
    /// <param name="rId">The ID of the role to toggle.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ToggleRemovedRole(CTModel ct, ulong rId)
    {
        // Initialize the database context
        var roles = ct.GetRemovedRoles(); // Get the removed roles for the trigger

        if (!roles.Contains(rId))
            roles.Add(rId); // Add the role ID if not present
        else
            roles.RemoveAll(x => x == rId); // Remove the role ID if already present

        await using var dbContext = await dbFactory.CreateConnectionAsync();
        ct.RemovedRoles = string.Join("@@@", roles.Select(x => x.ToString())); // Update the removed roles
        await dbContext.UpdateAsync(ct); // Update the chat trigger in the database
        // Save changes
        await UpdateInternalAsync(ct.GuildId, ct).ConfigureAwait(false); // Update the trigger internally
    }

    /// <summary>
    ///     Retrieves an embed builder containing information about a chat trigger.
    /// </summary>
    /// <param name="ct">The chat trigger.</param>
    /// <param name="gId">The ID of the guild.</param>
    /// <param name="title">The title for the embed.</param>
    /// <returns>An embed builder containing information about the chat trigger.</returns>
    public EmbedBuilder GetEmbed(CTModel ct, ulong? gId = null, string? title = null)
    {
        var eb = new EmbedBuilder().WithOkColor()
            .WithTitle(title)
            .WithDescription(strings.ChatTriggerId(gId, ct.Id));

        try
        {
            eb.AddField(strings.CtInteractionTypeTitle(gId),
                strings.CtInteractionTypeBody(gId, ((CtApplicationCommandType)ct.ApplicationCommandType).ToString()));
        }
        catch
        {
            eb.AddField(strings.CtInteractionTypeTitle(gId), strings.CtUnknown(gId));
        }

        eb.AddField(strings.CtRealname(gId), ct.RealName() ?? strings.CtNotAvailable(gId))
            .AddField(efb =>
                efb.WithName(strings.Trigger(gId)).WithValue(ct.Trigger?.TrimTo(1024) ?? strings.CtNotAvailable(gId)))
            .AddField(efb =>
                efb.WithName(strings.Response(gId))
                    .WithValue($"```css\n{(ct.Response ?? strings.CtNotAvailable(gId)).TrimTo(1024 - 11)}```"))
            .AddField(strings.CtPrefixType(gId), ((RequirePrefixType)ct.PrefixType).ToString());

        try
        {
            var reactions = ct.GetReactions();
            if (reactions is { Length: > 0 })
            {
                eb.AddField(strings.TriggerReactions(gId), string.Join("", reactions));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error");
        }

        try
        {
            var addedRoles = ct.GetGrantedRoles();
            if (addedRoles?.Count > 0)
            {
                eb.AddField(strings.AddedRoles(gId),
                    string.Join(", ", addedRoles.Select(x => $"<@&{x}>")));
            }

            var removedRoles = ct.GetRemovedRoles();
            if (removedRoles?.Count > 0)
            {
                eb.AddField(strings.RemovedRoles(gId),
                    string.Join(", ", removedRoles.Select(x => $"<@&{x}>")));
            }

            if (addedRoles?.Count > 0 || removedRoles?.Count > 0)
            {
                eb.AddField(strings.RoleGrantType(gId), ct.RoleGrantType.ToString());
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error");
        }

        if (ct.EventType != (int)CtEventType.None)
        {
            eb.AddField(strings.CtEventType(gId), ((CtEventType)ct.EventType).ToString())
                .AddField(strings.CtEventChannel(gId), ct.EventChannelId == 0
                    ? strings.CtEventChannelDefault(gId)
                    : $"<#{ct.EventChannelId}>");
        }

        if (ct.ReplyToTrigger)
            eb.AddField(strings.CtReplyToTrigger(gId), "\u200b");

        if (ct.DeleteResponseAfter > 0)
            eb.AddField(strings.CtDeleteResponseAfter(gId), $"{ct.DeleteResponseAfter}s");

        if (ct.CooldownSeconds > 0)
        {
            eb.AddField(strings.CtCooldown(gId), $"{ct.CooldownSeconds}s")
                .AddField(strings.CtCooldownScope(gId), ((CtCooldownScope)ct.CooldownScope).ToString());
        }

        if (!string.IsNullOrWhiteSpace(ct.CounterName) && (ct.CounterMin.HasValue || ct.CounterMax.HasValue))
        {
            var min = ct.CounterMin?.ToString("N0") ?? "any";
            var max = ct.CounterMax?.ToString("N0") ?? "any";
            eb.AddField(strings.CtCounterCondition(gId), $"`{ct.CounterName}` between {min} and {max}");
        }

        if (!string.IsNullOrWhiteSpace(ct.Category))
            eb.AddField(strings.CtCategory(gId), ct.Category);

        if (ct.AllowBots)
            eb.AddField(strings.CtAllowBots(gId), "\u200b");

        if (ct.NextTriggerId.HasValue)
            eb.AddField(strings.CtNextTrigger(gId), ct.NextTriggerId.Value.ToString());

        if (ct.IsDisabled)
            eb.AddField(strings.CtStatusDisabled(gId), "\u200b");

        if (ct.ExpiresAt.HasValue)
            eb.AddField(strings.CtExpiresAt(gId), TimestampTag.FromDateTime(ct.ExpiresAt.Value).ToString());

        if (ct.MaxUses.HasValue)
            eb.AddField(strings.CtMaxUses(gId), $"{ct.UseCount} / {ct.MaxUses.Value}");

        if (ct.MinAccountAgeMinutes > 0)
        {
            eb.AddField(strings.CtMinAccountAge(gId),
                FormatMinutes(ct.MinAccountAgeMinutes));
        }

        if (ct.MinServerMembershipMinutes > 0)
        {
            eb.AddField(strings.CtMinMembership(gId),
                FormatMinutes(ct.MinServerMembershipMinutes));
        }

        if (!string.IsNullOrWhiteSpace(ct.TimeConditions))
            eb.AddField(strings.CtActiveHours(gId), FormatTimeConditions(ct.TimeConditions));

        var responses = ct.GetResponses();
        if (responses.Count > 1)
        {
            eb.AddField(strings.CtResponseMode(gId), ((CtResponseMode)ct.ResponseMode).ToString())
                .AddField(strings.CtExtraResponses(gId), (responses.Count - 1).ToString());
        }

        if (ct.CurrencyCost > 0)
            eb.AddField(strings.CtCurrencyCost(gId), ct.CurrencyCost.ToString("N0"));

        if (ct.CurrencyReward > 0)
            eb.AddField(strings.CtCurrencyReward(gId), ct.CurrencyReward.ToString("N0"));

        if (ct.XpReward > 0)
            eb.AddField(strings.CtXpReward(gId), ct.XpReward.ToString("N0"));

        if (ct.RequiredXpLevel > 0)
            eb.AddField(strings.CtRequiredXpLevel(gId), ct.RequiredXpLevel.ToString());

        if (!string.IsNullOrWhiteSpace(ct.RequirementFailMessage))
            eb.AddField(strings.CtRequirementFailMessage(gId), ct.RequirementFailMessage.TrimTo(1024));

        if (!string.IsNullOrWhiteSpace(ct.ApplicationCommandDescription))
        {
            eb.AddField(strings.CtInteractionDescription(gId), ct.ApplicationCommandDescription);
        }

        if (ct.ApplicationCommandId != 0)
        {
            eb.AddField(strings.CtInteractionId(gId), ct.ApplicationCommandId.ToString());
        }

        if (ct.ValidTriggerTypes != 0b11111)
        {
            eb.AddField(strings.CtValidFields(gId), ((ChatTriggerType)(ct.ValidTriggerTypes)).ToString());
        }

        if (!string.IsNullOrWhiteSpace(ct.CrosspostingWebhookUrl))
        {
            eb.AddField(strings.CtCrossposting(gId), strings.CtCrosspostingWebhook(gId));
        }

        if (ct.CrosspostingChannelId != 0)
        {
            eb.AddField(strings.CtCrossposting(gId),
                strings.CtCrosspostingChannel(gId, ct.CrosspostingChannelId));
        }

        if (ct.PrefixType == (int)RequirePrefixType.Custom && !string.IsNullOrWhiteSpace(ct.CustomPrefix))
        {
            eb.AddField(strings.CtCustomPrefix(gId), ct.CustomPrefix);
        }

        return eb;
    }

    /// <summary>
    ///     Gets the application command properties for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A list of application command properties.</returns>
    public async Task<List<ApplicationCommandProperties>> GetApplicationCommandProperties(ulong guildId)
    {
        var props = new List<ApplicationCommandProperties>();

        // A disabled trigger must not stay registered with Discord: its command would still appear, but the
        // interaction handler refuses to run it, leaving the user with a command that always fails. Disabled
        // triggers are dropped before validation as well, so disabling a trigger with a bad command name is a way
        // out of a guild whose registration is otherwise blocked by it.
        var triggers = (await GetChatTriggersFor(guildId)).Where(x => !x.IsDisabled).ToArray();

        if (GetAcctErrors(triggers)?.Any() ?? false)
        {
            throw new InvalidOperationException("ACCTs cannot be build when ACCT errors are detected.");
        }

        if (triggers.Length == 0)
            return props;

        var groups = triggers.Where(x => x.ApplicationCommandType == (int)CtApplicationCommandType.Slash
                                         && ((ChatTriggerType)x.ValidTriggerTypes).HasFlag(ChatTriggerType.Interaction)
                                         && x.RealName().Split(' ').Length == 1)
            .Select(x => new TriggerChildGrouping(x.RealName(), x, null)).ToList();
        triggers.Where(x =>
                x.ApplicationCommandType == (int)CtApplicationCommandType.Slash && x.RealName().Split(' ').Length == 2)
            .ForEach(x =>
            {
                if (groups.Any(y => y.Name == x.RealName().Split(' ').First()))
                    groups.First(y => y.Name == x.RealName().Split(' ').First()).Children
                        .Add(new TriggerChildGrouping(x.RealName().Split(' ').Last(), x, null));
                else
                    groups.Add(new TriggerChildGrouping(x.RealName().Split(' ').First(), null,
                        [new TriggerChildGrouping(x.RealName().Split(' ').Last(), x, null)]));
            });

        triggers.Where(x =>
            x.ApplicationCommandType == (int)CtApplicationCommandType.Slash
            && ((ChatTriggerType)x.ValidTriggerTypes).HasFlag(ChatTriggerType.Interaction)
            && x.RealName().Split(' ').Length == 3).Select(x =>
        {
            TriggerChildGrouping group;
            if (groups.Any(y => y.Name == x.RealName().Split(' ').First()))
                group = groups.First(y => y.Name == x.RealName().Split(' ').First());
            else
            {
                groups.Add(new TriggerChildGrouping(x.RealName().Split(' ').First(), null,
                    []));
                group = groups.First(y => y.Name == x.RealName().Split(' ').First());
            }

            return (Triggers: x, Group: group);
        }).Select(x =>
        {
            TriggerChildGrouping group;
            var groupChildren = x.Group.Children;
            if (groupChildren.Any(y => y.Name == x.Triggers.RealName().Split(' ')[1]))
                group = groupChildren.First(y => y.Name == x.Triggers.RealName().Split(' ')[1]);
            else
            {
                groupChildren.Add(new TriggerChildGrouping(x.Triggers.RealName().Split(' ')[1], null, []));
                group = groupChildren.First(y => y.Name == x.Triggers.RealName().Split(' ')[1]);
            }

            return x with
            {
                Group = group
            };
        }).ForEach(x => x.Group.Children.Add(new TriggerChildGrouping(x.Triggers.RealName(), x.Triggers, null)));

        props = groups.Select(x => new SlashCommandBuilder()
                .WithName(x.Name)
                .WithDescription(x.Triggers?.ApplicationCommandDescription.IsNullOrWhiteSpace() ?? true
                    ? strings.CtDefaultDescription(guildId)
                    : x.Triggers!.ApplicationCommandDescription)
                .AddOptions(x.Triggers is not null
                    ? []
                    : x.Children.Select(y => new SlashCommandOptionBuilder
                        {
                            Options = []
                        }
                        .WithName(y.Name)
                        .WithDescription(y.Triggers?.ApplicationCommandDescription.IsNullOrWhiteSpace() ?? true
                            ? strings.CtDefaultDescription(guildId)
                            : y.Triggers!.ApplicationCommandDescription)
                        .WithType(y.Triggers is null
                            ? ApplicationCommandOptionType.SubCommandGroup
                            : ApplicationCommandOptionType.SubCommand)
                        .AddOptions(y.Children is null
                            ? []
                            : y.Children.Select(z => new SlashCommandOptionBuilder()
                                .WithName(z.Name.Split(' ')[2])
                                .WithDescription(z.Triggers?.ApplicationCommandDescription.IsNullOrWhiteSpace() ?? true
                                    ? strings.CtDefaultDescription(guildId)
                                    : z.Triggers!.ApplicationCommandDescription)
                                .WithType(ApplicationCommandOptionType.SubCommand)).ToArray())).ToArray()))
            .Select(x => x.Build() as ApplicationCommandProperties).ToList();

        triggers.Where(x => x.ApplicationCommandType == (int)CtApplicationCommandType.Message).ForEach(x =>
            props.Add(new MessageCommandBuilder().WithName(x.RealName()).WithContextTypes(InteractionContextType.Guild)
                .Build()));

        triggers.Where(x => x.ApplicationCommandType == (int)CtApplicationCommandType.User).ForEach(x =>
            props.Add(new UserCommandBuilder().WithName(x.RealName()).WithContextTypes(InteractionContextType.Guild)
                .Build()));
        return props;
    }

    /// <summary>
    ///     Tries to retrieve the application command properties for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>
    ///     A tuple containing a boolean indicating success and the application command properties if successful, otherwise
    ///     null.
    /// </returns>
    public async Task<(bool, List<ApplicationCommandProperties>? props)> TryGetApplicationCommandProperties(
        ulong guildId)
    {
        var props = new List<ApplicationCommandProperties>();
        try
        {
            props = await GetApplicationCommandProperties(guildId);
            return (true, props);
        }
        catch
        {
            props = null;
            return (false, props);
        }
    }

    /// <summary>
    ///     Registers chat triggers as application commands to a guild asynchronously.
    /// </summary>
    /// <param name="guild">The guild to register the chat triggers to.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RegisterTriggersToGuildAsync(IGuild guild)
    {
        var result = await TryGetApplicationCommandProperties(guild.Id);
        // Try to get the application command properties for the guild
        if (!result.Item1 || result.props is null)
            return;

        // Create or overwrite application commands based on the debug mode
#if DEBUG
        var cmd = new List<IApplicationCommand>();
        foreach (var prop in result.props)
            cmd.Add(await guild.CreateApplicationCommandAsync(prop));
#else
    var cmd = await guild.BulkOverwriteApplicationCommandsAsync(result.props.ToArray()).ConfigureAwait(false);
    if (cmd is null) return;
#endif

        // Associate chat trigger IDs with their corresponding application command IDs
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var cts = dbContext.ChatTriggers.Where(x => x.GuildId == guild.Id).ToList();
        foreach (var x in cmd.SelectMany(applicationCommand =>
                     applicationCommand.GetCtNames().Select(name => (cmd: applicationCommand, name))))
        {
            cts.First(y => y.RealName() == x.name).ApplicationCommandId = x.cmd.Id;
        }
    }

    /// <summary>
    ///     Checks whether a given command name is valid for the specified application command type.
    /// </summary>
    /// <param name="type">The type of the application command.</param>
    /// <param name="name">The name of the command.</param>
    /// <returns><see langword="true" /> if the command name is valid; otherwise, <see langword="false" />.</returns>
    public static bool IsValidName(CtApplicationCommandType type, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length is > 32 or < 1)
            return false;

        return type is not CtApplicationCommandType.Slash || ValidCommandRegex.IsMatch(name);
    }

    /// <summary>
    ///     Gets a list of errors related to chat trigger interactions.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A list of errors related to chat trigger interactions, if any; otherwise, <see langword="null" />.</returns>
    public async Task<List<ChatTriggersInteractionError>?> GetAcctErrors(ulong? guildId)
    {
        return GetAcctErrors(await GetChatTriggersFor(guildId));
    }


    /// <summary>
    ///     Gets a list of errors related to chat trigger interactions based on the provided triggers.
    /// </summary>
    /// <param name="triggers">The collection of chat triggers to analyze.</param>
    /// <returns>
    ///     A list of errors related to chat trigger interactions, if any; otherwise, <see langword="null" />.
    /// </returns>
    public static List<ChatTriggersInteractionError>? GetAcctErrors(IEnumerable<CTModel> triggers)
    {
        // Filter out triggers with CtApplicationCommandType.None
        triggers = triggers.Where(x => x.ApplicationCommandType != (int)CtApplicationCommandType.None);

        // Initialize a dictionary to store parent-child relationships
        var totalChildren = new Dictionary<string?, List<(string Name, int Id)>>();

        // Initialize a list to store errors
        var errors = new List<ChatTriggersInteractionError>();

        // Iterate through each trigger to identify errors
        foreach (var trigger in triggers)
        {
            // Determine the depth of the trigger's name
            var triggerDepth = trigger.RealName().Split(' ').Length;

            // Determine the parent of the trigger (if exists)
            var parent = triggerDepth > 1 ? string.Join(' ', trigger.RealName().Split(' ').Take(triggerDepth - 1)) : "";

            // Update the totalChildren dictionary with parent-child relationships
            if (!parent.IsNullOrWhiteSpace())
            {
                var value = totalChildren.GetValueOrDefault(parent, new List<(string, int)>());
                value.Add((trigger.RealName(), trigger.Id));
                totalChildren[parent] = value;
            }

            // Check if the trigger name is valid
            if (!IsValidName((CtApplicationCommandType)trigger.ApplicationCommandType, trigger.RealName()))
            {
                errors.Add(new ChatTriggersInteractionError("invalid_name", [
                    trigger.Id
                ], [
                    trigger.RealName()
                ]));
            }

            // Check for duplicate trigger names and subcommand matching parent triggers
            foreach (var newTrigger in triggers.Where(x => x.Id != trigger.Id))
            {
                var newTriggerDepth = newTrigger.RealName().Split(' ').Length;

                if (trigger.RealName() == newTrigger.RealName())
                {
                    errors.Add(new ChatTriggersInteractionError("duplicate", [
                            trigger.Id, newTrigger.Id
                        ],
                        [
                            trigger.RealName(), newTrigger.RealName()
                        ]));
                }

                switch (triggerDepth)
                {
                    case 1 when newTriggerDepth == 2 && newTrigger.RealName().Split(' ')[0] == trigger.RealName():
                        errors.Add(new ChatTriggersInteractionError("subcommand_match_parent", [
                                trigger.Id, newTrigger.Id
                            ],
                            [
                                trigger.RealName(), newTrigger.RealName()
                            ]));
                        break;
                    case 2 when newTriggerDepth == 3 &&
                                string.Join(' ', newTrigger.RealName().Split(' ').Take(2)) == trigger.RealName():
                        errors.Add(new ChatTriggersInteractionError("subcommand_match_parent", [
                                trigger.Id, newTrigger.Id
                            ],
                            [
                                trigger.RealName(), newTrigger.RealName()
                            ]));
                        break;
                }
            }
        }

        // Check for triggers with too many children and add errors if necessary
        totalChildren.Where(x => x.Value.Count > 25).ForEach(x => errors.Add(new ChatTriggersInteractionError(
            "too_many_children",
            x.Value.Select(y => y.Id).ToArray(), x.Value.Select(y => y.Name).ToArray())));

        return errors.Any() ? errors : null;
    }

    /// <summary>
    ///     Handles reaction events for reaction-based chat triggers.
    /// </summary>
    /// <param name="message">The cached message that was reacted to.</param>
    /// <param name="channel">The cached channel where the reaction occurred.</param>
    /// <param name="reaction">The reaction that was added.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private Task OnReactionAdded(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        return HandleReactionAsync(message, channel, reaction, ChatTriggerType.Reactions);
    }

    /// <summary>
    ///     Handles a reaction being removed, which fires triggers that opted into the reaction removed type.
    /// </summary>
    /// <param name="message">The message the reaction was removed from.</param>
    /// <param name="channel">The channel the message is in.</param>
    /// <param name="reaction">The reaction that was removed.</param>
    private Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        return HandleReactionAsync(message, channel, reaction, ChatTriggerType.ReactionsRemoved);
    }

    /// <summary>
    ///     Fires the reaction triggers that match an added or removed reaction.
    /// </summary>
    /// <param name="message">The message that was reacted to.</param>
    /// <param name="channel">The channel the message is in.</param>
    /// <param name="reaction">The reaction that was added or removed.</param>
    /// <param name="triggerType">The trigger type the reaction corresponds to.</param>
    private async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction, ChatTriggerType triggerType)
    {
        if (!ready)
            return;

        // Don't process reactions from bots
        if (reaction.User.Value?.IsBot == true)
            return;

        // Get the message and channel
        var msg = await message.GetOrDownloadAsync().ConfigureAwait(false);
        var ch = await channel.GetOrDownloadAsync().ConfigureAwait(false);

        if (msg is null || ch is null)
            return;

        // Only process guild messages
        if (ch is not IGuildChannel guildChannel)
            return;

        var guild = guildChannel.Guild;
        var user = reaction.User.Value;

        if (user is null)
            return;

        // Get reaction triggers for this guild
        var triggers = await GetChatTriggersFor(guild.Id).ConfigureAwait(false);

        // Find matching reaction triggers
        var reactionTriggers = triggers.Where(ct =>
            !ct.IsDisabled &&
            ((ChatTriggerType)ct.ValidTriggerTypes).HasFlag(triggerType) &&
            IsReactionMatch(ct, reaction.Emote)).ToArray();

        foreach (var ct in reactionTriggers)
        {
            try
            {
                // Check cooldowns
                if (await cmdCds.TryBlock(guild, user, ct.Trigger).ConfigureAwait(false))
                    continue;

                // Check permissions (similar to message triggers)
                if (gperm.BlockedModules.Contains("ActualChatTriggers"))
                    continue;

                if (guild is SocketGuild sg)
                {
                    var sgUser = sg.GetUser(user.Id);
                    if (sgUser is null)
                        continue;

                    var pc = await perms.GetCacheFor(guild.Id);

                    // Create a fake message for permission checking
                    var fakeMsg = new MewdekoUserMessage
                    {
                        Author = user, Content = ct.Trigger, Channel = ch
                    };

                    if (!pc.Permissions.CheckPermissions(fakeMsg, ct.Trigger, "ActualChatTriggers", out var index,
                            ct.Id.ToString()))
                        continue;

                    // Honour permission overrides the same way the message and interaction paths do.
                    if (discordPermOverride.TryGetOverrides(guild.Id, ct.Trigger, out var guildPermission)
                        && !sgUser.GuildPermissions.Has(guildPermission))
                    {
                        continue;
                    }
                }

                // Check if trigger is owner-only
                if (ct.OwnerOnly && !creds.IsOwner(user))
                    continue;

                // Check the trigger's own conditions before anything is charged or sent.
                if (!await PassesConditionsAsync(ct, guild.Id, ch.Id, user).ConfigureAwait(false))
                    continue;

                // Apply economy requirements and rewards
                if (!await TryApplyEconomyAsync(ct, guild.Id, user, ch).ConfigureAwait(false))
                    continue;

                // Execute the trigger
                await TryExecuteReactionTrigger(ct, guild, ch, user, msg, reaction).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error in reaction trigger {TriggerId} for guild {GuildId}", ct.Id, guild.Id);
            }
        }
    }

    /// <summary>
    ///     Checks if a reaction matches a trigger pattern.
    /// </summary>
    /// <param name="trigger">The trigger to check against.</param>
    /// <param name="emote">The emote that was reacted with.</param>
    /// <returns>True if the reaction matches the trigger pattern.</returns>
    private static bool IsReactionMatch(CTModel trigger, IEmote emote)
    {
        // For reaction triggers, the Trigger field contains the emoji/emote to match
        if (string.IsNullOrWhiteSpace(trigger.Trigger))
            return false;

        var triggerEmote = trigger.Trigger.Trim();

        // Handle different emote types
        return emote switch
        {
            Emoji emoji => triggerEmote.Equals(emoji.Name, StringComparison.OrdinalIgnoreCase),
            Emote customEmote => triggerEmote.Equals(customEmote.Name, StringComparison.OrdinalIgnoreCase) ||
                                 triggerEmote.Equals(customEmote.ToString(), StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    /// <summary>
    ///     Executes a reaction-based trigger.
    /// </summary>
    /// <param name="ct">The trigger to execute.</param>
    /// <param name="guild">The guild where the reaction occurred.</param>
    /// <param name="channel">The channel where the reaction occurred.</param>
    /// <param name="user">The user who reacted.</param>
    /// <param name="message">The message that was reacted to.</param>
    /// <param name="reaction">The reaction that was added.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task TryExecuteReactionTrigger(CTModel ct, IGuild guild, IMessageChannel channel, IUser user,
        IUserMessage message, SocketReaction reaction)
    {
        // Update usage count
        await IncrementTriggerUsage(ct).ConfigureAwait(false);
        await RecordTriggerFireAsync(ct, guild.Id, channel.Id, user).ConfigureAwait(false);

        // Handle role operations
        if (!string.IsNullOrWhiteSpace(ct.GrantedRoles) || !string.IsNullOrWhiteSpace(ct.RemovedRoles))
        {
            await HandleRoleOperations(ct, guild, user, message, channel).ConfigureAwait(false);
        }

        // Create a fake message for the reaction trigger (similar to button/interaction triggers)
        var fakeMsg = new MewdekoUserMessage
        {
            Author = user,
            Content =
                $"{reaction.Emote} {strings.CtReactionOn(guild.Id)} {(message.Content?.Length > 50 ? message.Content[..50] + strings.CtContentTruncated(guild.Id) : message.Content ?? "")}",
            Channel = channel
        };

        // Send the response using the same method as regular triggers
        IUserMessage? sentMsg = null;
        foreach (var response in await SelectResponsesAsync(ct).ConfigureAwait(false))
        {
            sentMsg = await ct.Send(fakeMsg, client, false, dbFactory, placeholders, null, response)
                .ConfigureAwait(false);
        }

        // Add reactions (following same pattern as regular triggers)
        QueueTriggerReactions(ct, sentMsg, message);
    }

    /// <summary>
    ///     Increments the usage count for a trigger.
    /// </summary>
    /// <param name="ct">The trigger to increment usage for.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task IncrementTriggerUsage(CTModel ct)
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            await dbContext.ChatTriggers
                .Where(x => x.Id == ct.Id)
                .Set(x => x.UseCount, x => x.UseCount + 1)
                .UpdateAsync().ConfigureAwait(false);

            ct.UseCount++;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to increment usage count for trigger {TriggerId}", ct.Id);
        }
    }

    /// <summary>
    ///     Handles role grant/remove operations for triggers.
    /// </summary>
    /// <param name="ct">The trigger containing role operations.</param>
    /// <param name="guild">The guild to perform operations in.</param>
    /// <param name="user">The user who triggered the action.</param>
    /// <param name="message">The message that was reacted to.</param>
    /// <param name="channel">The channel where the reaction occurred.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleRoleOperations(CTModel ct, IGuild guild, IUser user, IUserMessage? message,
        IMessageChannel channel)
    {
        try
        {
            var guildUser = await guild.GetUserAsync(user.Id).ConfigureAwait(false);
            if (guildUser is null)
                return;

            var targetUsers = new List<IGuildUser>();

            // Determine target users based on RoleGrantType
            switch ((CtRoleGrantType)ct.RoleGrantType)
            {
                case CtRoleGrantType.Sender:
                    targetUsers.Add(guildUser);
                    break;
                case CtRoleGrantType.Mentioned:
                    // For reaction triggers, we can't get mentioned users from the reaction itself
                    // So we'll get mentions from the original message that was reacted to
                    var mentionedUserIds = message?.Content?.GetUserMentions() ?? [];
                    foreach (var userId in mentionedUserIds)
                    {
                        var mentionedGuildUser = await guild.GetUserAsync(userId).ConfigureAwait(false);
                        if (mentionedGuildUser is not null)
                            targetUsers.Add(mentionedGuildUser);
                    }

                    break;
                case CtRoleGrantType.Both:
                    targetUsers.Add(guildUser);
                    var mentionedUserIds2 = message?.Content?.GetUserMentions() ?? [];
                    foreach (var userId in mentionedUserIds2)
                    {
                        var mentionedGuildUser = await guild.GetUserAsync(userId).ConfigureAwait(false);
                        if (mentionedGuildUser is not null && !targetUsers.Contains(mentionedGuildUser))
                            targetUsers.Add(mentionedGuildUser);
                    }

                    break;
            }

            // Process role operations for each target user
            foreach (var targetUser in targetUsers)
            {
                // Grant roles
                if (!string.IsNullOrWhiteSpace(ct.GrantedRoles))
                {
                    foreach (var roleId in ct.GetGrantedRoles())
                    {
                        {
                            var role = guild.GetRole(roleId);
                            if (role is not null && !targetUser.RoleIds.Contains(roleId))
                            {
                                await targetUser.AddRoleAsync(role).ConfigureAwait(false);
                            }
                        }
                    }
                }

                // Remove roles
                if (!string.IsNullOrWhiteSpace(ct.RemovedRoles))
                {
                    foreach (var roleId in ct.GetRemovedRoles())
                    {
                        {
                            var role = guild.GetRole(roleId);
                            if (role is not null && targetUser.RoleIds.Contains(roleId))
                            {
                                await targetUser.RemoveRoleAsync(role).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to handle role operations for trigger {TriggerId}", ct.Id);
        }
    }


    /// <summary>
    ///     Unloads the service and unsubscribes from events.
    /// </summary>
    public Task Unload()
    {
        eventHandler.Unsubscribe("JoinedGuild", "ChatTriggersService", OnJoinedGuild);
        eventHandler.Unsubscribe("LeftGuild", "ChatTriggersService", OnLeftGuild);
        eventHandler.Unsubscribe("ReactionAdded", "ChatTriggersService", OnReactionAdded);
        eventHandler.Unsubscribe("ReactionRemoved", "ChatTriggersService", OnReactionRemoved);
        eventHandler.Unsubscribe("XpLevelChanged", "ChatTriggersService", OnXpLevelChanged);
        eventHandler.Unsubscribe("UserJoined", "ChatTriggersService", OnUserJoined);
        eventHandler.Unsubscribe("UserLeft", "ChatTriggersService", OnUserLeft);
        eventHandler.Unsubscribe("UserVoiceStateUpdated", "ChatTriggersService", OnVoiceStateUpdated);
        eventHandler.Unsubscribe("GuildMemberUpdated", "ChatTriggersService", OnGuildMemberUpdated);
        eventHandler.Unsubscribe("MessageReceived", "ChatTriggersService", OnMessageReceived);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Result of the CrEmbed migration operation.
    /// </summary>
    /// <param name="TotalChecked">Total number of triggers checked.</param>
    /// <param name="Migrated">Number of triggers that were migrated.</param>
    public record MigrationResult(int TotalChecked, int Migrated);

    /// <summary>
    ///     A set of triggers split for matching: those resolvable by an exact lookup, and those needing evaluation.
    /// </summary>
    /// <param name="Exact">Triggers whose whole message must equal the trigger, keyed by that text.</param>
    /// <param name="Complex">Triggers that require prefix stripping, regex or positional matching.</param>
    private sealed record TriggerIndex(Dictionary<string, List<CTModel>> Exact, CTModel[] Complex);

    /// <summary>
    ///     Represents the grouping of trigger children for building application command properties.
    /// </summary>
    private record TriggerChildGrouping(string Name, CTModel? Triggers, List<TriggerChildGrouping>? Children);
}