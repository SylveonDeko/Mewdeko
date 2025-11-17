using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Common.DiscordImplementations;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Common.Yml;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Chat_Triggers.Common;
using Mewdeko.Modules.Chat_Triggers.Extensions;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.Strings;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using CTModel = DataModel.ChatTrigger;

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

    private readonly DiscordShardedClient client;
    private readonly CmdCdService cmdCds;
    private readonly BotConfigService configService;
    private readonly TypedKey<CTModel> crAdded = new("cr.added");
    private readonly IBotCredentials creds;
    private readonly TypedKey<bool> crsReloadedKey = new("crs.reloaded");

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
    private readonly IPubSub pubSub;
    private readonly Random rng;
    private readonly GeneratedBotStrings strings;


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
        EventHandler eventHandler, ILogger<ChatTriggersService> logger)
    {
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
    public async Task<bool> RunBehavior(DiscordShardedClient socketClient, IGuild guild, IUserMessage msg)
    {
        // Maybe this message is a custom reaction
        var ct = await TryGetChatTriggers(msg);

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
                if (!pc.Permissions.CheckPermissions(msg, ct.Trigger, "ActualChatTriggers", out var index))
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
                        logger.LogInformation(
                            "Chat Trigger {CtTrigger} Blocked for {MsgAuthor} in {Guild} due to them missing {Perms}",
                            ct.Trigger, msg.Author, guild, guildPermission);
                        return false;
                    }
                }
            }

            // Update command usage statistics
            var guildConfig = await guildSettings.GetGuildConfig(guild.Id);
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var dbUser = await dbContext.GetOrCreateUser(msg.Author);
            if (!guildConfig.StatsOptOut && !dbUser.StatsOptOut)
            {
                var toAdd = new CommandStat
                {
                    ChannelId = msg.Channel.Id,
                    Trigger = true,
                    NameOrId = $"{ct.Id}",
                    GuildId = guild.Id,
                    UserId = msg.Author.Id
                };
                await dbContext.InsertAsync(toAdd);
            }

            // Send the chat trigger response
            var sentMsg = await ct.Send(msg, client, false, dbFactory).ConfigureAwait(false);

            // Add reactions to the response message
            foreach (var reaction in ct.GetReactions())
            {
                try
                {
                    if (!ct.ReactToTrigger && !ct.NoRespond)
                        await sentMsg.AddReactionAsync(reaction.ToIEmote()).ConfigureAwait(false);
                    else
                        await msg.AddReactionAsync(reaction.ToIEmote()).ConfigureAwait(false);
                }
                catch
                {
                    logger.LogWarning("Unable to add reactions to message {Message} in server {GuildId}", sentMsg.Id,
                        ct.GuildId);
                    break;
                }

                await Task.Delay(1000).ConfigureAwait(false);
            }

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

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex.Message);
        }

        return false;
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
    public async Task RunInteractionTrigger(SocketInteraction inter, CTModel ct, bool followup = false)
    {
        // Switch based on the type of interaction
        switch (inter)
        {
            // If the interaction is a command and the trigger type does not include interaction triggers, or
            // if the interaction is a message component (button) and the trigger type does not include buttons,
            // return without further processing.
            case SocketCommandBase when !((ChatTriggerType)ct.ValidTriggerTypes).HasFlag(ChatTriggerType.Interaction):
            case SocketMessageComponent when !((ChatTriggerType)ct.ValidTriggerTypes).HasFlag(ChatTriggerType.Button):
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
                                out var index))
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

                            logger.LogInformation(returnMsg);

                            return;
                        }

                        // Check for permission overrides.
                        if (discordPermOverride.TryGetOverrides(guild.Id, ct.Trigger, out var guildPermission))
                        {
                            var user = inter.User as IGuildUser;
                            if (!user.GuildPermissions.Has(guildPermission))
                            {
                                logger.LogInformation(
                                    $"Chat Trigger {ct.Trigger} Blocked for {inter.User} in {guild} due to them missing {guildPermission}.");
                                return;
                            }
                        }
                    }

                    var channel = inter.Channel as IGuildChannel;

                    // Get guild configuration.
                    var guildConfig = await guildSettings.GetGuildConfig(channel.GuildId);

                    // Retrieve or create a user entry in the database.
                    await using var dbContext = await dbFactory.CreateConnectionAsync();
                    var dbUser = await dbContext.GetOrCreateUser(fakeMsg.Author);

                    // If stats tracking is enabled for the guild and the user has not opted out, record the command usage.
                    if (!guildConfig.StatsOptOut && !dbUser.StatsOptOut)
                    {
                        var toAdd = new CommandStat
                        {
                            ChannelId = channel.Id,
                            Trigger = true,
                            NameOrId = $"{ct.Id}",
                            GuildId = channel.GuildId,
                            UserId = inter.User.Id
                        };
                        await dbContext.InsertAsync(toAdd);
                    }

                    var sentMsg = await ct.SendInteraction(inter, client, false, fakeMsg,
                        ct.EphemeralResponse, dbFactory, followup).ConfigureAwait(false);

                    // Add reactions to the sent message, if any.
                    foreach (var reaction in ct.GetReactions())
                    {
                        try
                        {
                            if (!ct.ReactToTrigger && !ct.NoRespond)
                                await sentMsg.AddReactionAsync(reaction.ToIEmote()).ConfigureAwait(false);
                            else
                                await sentMsg.AddReactionAsync(reaction.ToIEmote()).ConfigureAwait(false);
                        }
                        catch
                        {
                            logger.LogWarning("Unable to add reactions to message {Message} in server {GuildId}",
                                sentMsg.Id,
                                ct.GuildId);
                            break;
                        }

                        await Task.Delay(1000).ConfigureAwait(false);
                    }

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

            // Start a new database context


            // Initialize a list to store CTModel objects representing chat triggers
            List<CTModel> triggers = [];
            foreach (var (_, value) in data)
            {
                // Convert exported triggers to CTModel objects and add them to the list
                triggers.AddRange(value
                    .Where(ct => !string.IsNullOrWhiteSpace(ct.Res))
                    .Select(ct => new CTModel
                    {
                        // Populate CTModel properties from exported trigger data
                    }));
            }

            // Check if the user has permission to manage all roles involved in the import
            List<ulong> roles = [];
            triggers.ForEach(x => roles.AddRange(x.GetGrantedRoles()));
            triggers.ForEach(x => roles.AddRange(x.GetRemovedRoles()));

            if (roles.Count > 0 && roles.All(y => user.Guild.GetRole(y).CanManageRole(user)))
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
                    x.Trigger = x.Trigger.Replace(MentionPh, client.CurrentUser.Mention);
                    return x;
                }).ToArray())
            .ToConcurrent();

        logger.LogInformation($"Loaded {newGuildReactions.Count} guild trigger groups");

        globalReactions = (await dbContext.ChatTriggers
                .Where(x => x.GuildId == null || x.GuildId == 0)
                .ToListAsync())
            .Select(x =>
            {
                x.Trigger = x.Trigger.Replace(MentionPh, client.CurrentUser.Mention);
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
    /// <returns>The matched chat trigger model, or null if no match is found.</returns>
    private async Task<CTModel?> TryGetChatTriggers(IUserMessage umsg)
    {
        // Check if the chat triggers are ready
        if (!ready)
            return null;

        // Check if the message channel is a text channel
        if (umsg.Channel is not SocketTextChannel channel)
            return null;

        // Trim and convert message content to lowercase for comparison
        var content = umsg.Content.Trim().ToLowerInvariant();

        // Check if there are guild-specific reactions for the current guild
        if (newGuildReactions.TryGetValue(channel.Guild.Id, out var reactions) && reactions.Length > 0)
        {
            // Attempt to match chat triggers against the message content
            var cr = await MatchChatTriggers(content, reactions, channel.Guild);
            if (cr is not null)
                return cr;
        }

        // Get the global reactions array
        // ReSharper disable once InconsistentlySynchronizedField
        var localGrs = globalReactions;

        // Match chat triggers against the message content
        return await MatchChatTriggers(content, localGrs, channel.Guild);
    }


    /// <summary>
    ///     Matches chat triggers against the provided content to find a trigger that matches.
    /// </summary>
    /// <param name="content">The content to match against chat triggers.</param>
    /// <param name="crs">The array of chat triggers to match against.</param>
    /// <param name="guild">The guild associated with the chat triggers.</param>
    /// <returns>The matched chat trigger model, or null if no match is found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<CTModel?> MatchChatTriggers(string content, CTModel[] crs, SocketGuild guild)
    {
        try
        {
            // Get the prefix for the guild and the global prefix
            var guildPrefix = await guildSettings.GetPrefix(guild);
            var globalPrefix = configService.Data.Prefix;

            // Initialize a list to store matched chat triggers
            var result = new List<CTModel>(1);

            // Iterate through each chat trigger
            foreach (var ct in crs)
            {
                var trigger = ct.Trigger;

                // Check the type of prefix required for the trigger
                switch ((RequirePrefixType)ct.PrefixType)
                {
                    case RequirePrefixType.Custom:
                        if (!content.StartsWith(ct.CustomPrefix))
                            continue;
                        content = content[ct.CustomPrefix.Length..];
                        break;
                    case RequirePrefixType.GuildOrNone:
                        if (guildPrefix is null || !content.StartsWith(guildPrefix))
                            continue;
                        content = content[guildPrefix.Length..];
                        break;
                    case RequirePrefixType.GuildOrGlobal:
                        if (!content.StartsWith(guildPrefix ?? globalPrefix))
                            continue;
                        content = content[(guildPrefix ?? globalPrefix).Length..];
                        break;
                    case RequirePrefixType.Global:
                        if (!content.StartsWith(globalPrefix))
                            continue;
                        content = content[globalPrefix.Length..];
                        break;
                    case RequirePrefixType.None:
                    default:
                        break;
                }

                // Check if the trigger is a regex pattern
                if (ct.IsRegex)
                {
                    // Match the content against the trigger regex pattern
                    if (Regex.IsMatch(new string(content), trigger, RegexOptions.None, TimeSpan.FromMilliseconds(1)))
                        result.Add(ct);
                    continue;
                }

                // If the trigger depends on user mentions to grant roles,
                // remove user mentions from the content
                if ((CtRoleGrantType)ct.RoleGrantType is CtRoleGrantType.Mentioned or CtRoleGrantType.Both)
                {
                    content = content.RemoveUserMentions().Trim();
                }

                // Check if the content length is greater than the trigger length
                if (content.Length > trigger.Length)
                {
                    // If the trigger has ContainsAnywhere enabled, check if it is contained as a word within the content
                    if (ct.ContainsAnywhere)
                    {
                        var wp = content.AsSpan().GetWordPosition(trigger);
                        if (wp != WordPosition.None)
                            result.Add(ct);
                        continue;
                    }

                    // If AllowTarget is enabled, the content has to start with the trigger followed by a space
                    if (ct.AllowTarget && content.StartsWith(trigger, StringComparison.OrdinalIgnoreCase)
                                       && content[trigger.Length] == ' ')
                    {
                        result.Add(ct);
                    }
                }
                else if (content.Length < ct.Trigger.Length)
                {
                    // If the content length is less than the trigger length, the trigger can never be triggered
                }
                else
                {
                    // If the content length is equal to the trigger length, the strings have to be equal for the trigger to be matched
                    if (content.SequenceEqual(ct.Trigger))
                        result.Add(ct);
                }
            }

            // Return a randomly selected matched chat trigger, if any
            return result.Count == 0 ? null : result[rng.Next(0, result.Count)];
        }
        catch (RegexMatchTimeoutException)
        {
            // This is expected
            return null;
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
                (_, old) => DeleteInternal(old, id).GetAwaiter().GetResult());

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
    private static async Task<CTModel[]> DeleteInternal(IReadOnlyList<CTModel>? cts, int id)
    {
        await Task.CompletedTask;
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
        if (ct == null || ct.GuildId != guildId && ct.GuildId is not 0 or null)
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
            await DeleteInternal(globalReactions, id); // Delete the chat trigger from the global reactions array
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
        ct.Trigger = trigger ?? ct.Trigger; // Update the trigger key

        // Enable allow target if message is edited to contain target
        if (ct.Response.Contains("%target%", StringComparison.OrdinalIgnoreCase))
            ct.AllowTarget = true; // Enable targeting

        // Save changes
        await UpdateInternalAsync(guildId.Value, ct).ConfigureAwait(false); // Update the trigger internally

        return ct; // Return the edited chat trigger
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

        if (toDelete.GuildId is not null or 0 && guildId == null &&
            guildId != toDelete.GuildId) // Check permission to delete
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
            return newGuildReactions.TryGetValue(guildId, out var cts) // Retrieve triggers for the guild
                ? cts
                : []; // Return an empty array if no triggers found
        }


        return globalReactions; // Return global triggers if no guild ID specified
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

        if (!string.IsNullOrWhiteSpace(ct.ApplicationCommandDescription))
        {
            eb.AddField(strings.CtInteractionDescription(gId), ct.ApplicationCommandDescription);
        }

        if (ct.ApplicationCommandId != 0)
        {
            eb.AddField(strings.CtInteractionId(gId), ct.ApplicationCommandId.ToString());
        }

        if (ct.ValidTriggerTypes != 0b1111)
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

        var triggers = await GetChatTriggersFor(guildId);

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
    private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
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
            ((ChatTriggerType)ct.ValidTriggerTypes).HasFlag(ChatTriggerType.Reactions) &&
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

                    if (!pc.Permissions.CheckPermissions(fakeMsg, ct.Trigger, "ActualChatTriggers", out var index))
                        continue;
                }

                // Check if trigger is owner-only
                if (ct.OwnerOnly && !creds.IsOwner(user))
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
        var sentMsg = await ct.Send(fakeMsg, client, false, dbFactory).ConfigureAwait(false);

        // Add reactions (following same pattern as regular triggers)
        foreach (var reactionStr in ct.GetReactions())
        {
            try
            {
                if (!ct.ReactToTrigger && !ct.NoRespond)
                    await sentMsg.AddReactionAsync(reactionStr.ToIEmote()).ConfigureAwait(false);
                else
                    await message.AddReactionAsync(reactionStr.ToIEmote()).ConfigureAwait(false);
            }
            catch
            {
                logger.LogWarning("Unable to add reactions to message {Message} in server {GuildId}", sentMsg?.Id,
                    ct.GuildId);
                break;
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }
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
    private async Task HandleRoleOperations(CTModel ct, IGuild guild, IUser user, IUserMessage message,
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
                    var mentionedUserIds = message.Content?.GetUserMentions() ?? [];
                    foreach (var userId in mentionedUserIds)
                    {
                        var mentionedGuildUser = await guild.GetUserAsync(userId).ConfigureAwait(false);
                        if (mentionedGuildUser is not null)
                            targetUsers.Add(mentionedGuildUser);
                    }

                    break;
                case CtRoleGrantType.Both:
                    targetUsers.Add(guildUser);
                    var mentionedUserIds2 = message.Content?.GetUserMentions() ?? [];
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
                    var roleIds = ct.GrantedRoles.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var roleIdStr in roleIds)
                    {
                        if (ulong.TryParse(roleIdStr.Trim(), out var roleId))
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
                    var roleIds = ct.RemovedRoles.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var roleIdStr in roleIds)
                    {
                        if (ulong.TryParse(roleIdStr.Trim(), out var roleId))
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
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Represents the grouping of trigger children for building application command properties.
    /// </summary>
    private record TriggerChildGrouping(string Name, CTModel? Triggers, List<TriggerChildGrouping>? Children);
}