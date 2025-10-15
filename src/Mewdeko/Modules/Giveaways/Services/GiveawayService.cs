using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Configs;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Strings;
using Swan;

namespace Mewdeko.Modules.Giveaways.Services;

/// <summary>
///     Service for managing giveaways across Discord guilds.
/// </summary>
public class GiveawayService : INService, IDisposable
{
    private readonly Timer cleanupTimer;
    private readonly DiscordShardedClient client;
    private readonly BotConfig config;
    private readonly BotCredentials credentials;
    private readonly IDataConnectionFactory dbFactory;


    // Memory management
    private readonly ConcurrentDictionary<int, Timer> giveawayTimers = new();
    private readonly GuildSettingsService guildConfig;
    private readonly ConcurrentDictionary<ulong, (GuildConfig Config, DateTime Expiry)> guildConfigCache = new();
    private readonly ILogger<GiveawayService> logger;
    private readonly MessageCountService msgCntService;
    private readonly GeneratedBotStrings strings;
    private readonly SemaphoreSlim timerLock = new(1, 1);
    private bool isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GiveawayService" /> class.
    /// </summary>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="dbFactory">Provider for database contexts.</param>
    /// <param name="guildConfig">Service for accessing guild settings.</param>
    /// <param name="config">Bot configuration settings.</param>
    /// <param name="credentials">Bot credentials.</param>
    /// <param name="msgCntService">Service for tracking message counts.</param>
    /// <param name="strings">Service for localized strings.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public GiveawayService(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        GuildSettingsService guildConfig,
        BotConfig config,
        BotCredentials credentials,
        MessageCountService msgCntService,
        GeneratedBotStrings strings, ILogger<GiveawayService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.guildConfig = guildConfig;
        this.config = config;
        this.credentials = credentials;
        this.msgCntService = msgCntService;
        this.strings = strings;
        this.logger = logger;

        // Set up periodic cleanup (every 60 minutes)
        cleanupTimer = new Timer(_ => CleanupResources(), null, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60));

        // Initialize giveaways
        _ = InitializeGiveawaysAsync();
    }

    #region Public Methods

    /// <summary>
    ///     Sets the emote to be used for giveaways in the specified guild.
    /// </summary>
    /// <param name="guild">The guild where the emote is to be set.</param>
    /// <param name="emote">The emote to set.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetGiveawayEmote(IGuild guild, string emote)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var gc = await guildConfig.GetGuildConfig(guild.Id);
        gc.GiveawayEmote = emote;
        await guildConfig.UpdateGuildConfig(guild.Id, gc);

        // Update cache if exists
        InvalidateGuildConfigCache(guild.Id);
    }

    /// <summary>
    ///     Retrieves the emote used for giveaways in the guild with the specified ID.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The emote used for giveaways.</returns>
    public async Task<string> GetGiveawayEmote(ulong id)
    {
        return (await GetGuildConfigCached(id)).GiveawayEmote;
    }

    /// <summary>
    ///     Gets a giveaway by its ID.
    /// </summary>
    /// <param name="id">The giveaway ID.</param>
    /// <returns>A giveaway or null if not found.</returns>
    public async Task<Giveaway?> GetGiveawayById(int id)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        return await dbContext.Giveaways
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    /// <summary>
    ///     Adds a user to a giveaway.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="giveawayId">The giveaway ID.</param>
    /// <returns>A tuple containing a success flag and an optional error message.</returns>
    public async Task<(bool Success, string? ErrorMessage)> AddUserToGiveaway(ulong userId, int giveawayId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Validate giveaway exists and is active
        var giveaway = await GetGiveawayById(giveawayId);
        if (giveaway == null)
            return (false, strings.GiveawayNotFound(giveaway?.ServerId ?? 0));

        if (giveaway.Ended == 1)
            return (false, strings.GiveawayAlreadyEnded(giveaway.ServerId, "", 0));

        if (!giveaway.UseButton && !giveaway.UseCaptcha)
            return (false, strings.GiveawayInvalidEntryMethod(giveaway.ServerId));

        // Verify user is in the guild
        var guild = client.GetGuild(giveaway.ServerId);
        if (guild == null)
            return (false, strings.GiveawayGuildNotFound(giveaway.ServerId));

        var users = await guild.GetUsersAsync().FlattenAsync();
        if (users.All(u => u.Id != userId))
            return (false, strings.GiveawayUserNotInServer(giveaway.ServerId));

        // Check if user is already entered
        var existing = await dbContext.GiveawayUsers
            .AnyAsync(gu => gu.UserId == userId && gu.GiveawayId == giveawayId);

        if (existing)
            return (false, strings.GiveawayAlreadyEntered(giveaway.ServerId));

        // Add user to giveaway
        await dbContext.InsertAsync(new GiveawayUser
        {
            UserId = userId, GiveawayId = giveawayId
        });
        return (true, null);
    }

    /// <summary>
    ///     Creates a giveaway from the dashboard.
    /// </summary>
    /// <param name="serverId">The ID of the server where the giveaway is being created.</param>
    /// <param name="giveaway">The giveaway data.</param>
    /// <returns>The created giveaway.</returns>
    /// <exception cref="Exception">Thrown when guild or channel is not found.</exception>
    public async Task<Giveaway> CreateGiveawayFromDashboard(ulong serverId,
        Giveaway giveaway)
    {
        // Validate guild and channel
        IGuild guild = client.GetGuild(serverId);
        if (guild == null)
            throw new Exception(strings.GiveawayGuildNotFound(serverId));

        var channel = await guild.GetTextChannelAsync(giveaway.ChannelId);
        if (channel == null)
            throw new Exception(strings.GiveawayChannelNotFound(serverId));

        // Get guild config with caching
        var gconfig = await GetGuildConfigCached(serverId);

        // Prepare ping role
        IRole? pingRole = null;
        if (gconfig.GiveawayPingRole != 0)
        {
            pingRole = guild.GetRole(gconfig.GiveawayPingRole);
        }

        // Create embed
        var emote = (await GetGiveawayEmote(guild.Id)).ToIEmote();
        if (giveaway.Emote != null)
            emote = giveaway.Emote.ToIEmote();
        var hostUser = await guild.GetUserAsync(giveaway.UserId);
        var eb = CreateGiveawayEmbed(guild, giveaway, emote, gconfig, hostUser);

        // Send giveaway message
        var msg = await channel.SendMessageAsync(
            pingRole != null ? pingRole.Mention : "",
            embed: eb.Build());

        // Add reaction or button
        if (!giveaway.UseButton && !giveaway.UseCaptcha)
            await msg.AddReactionAsync(emote);

        var curUser = await guild.GetCurrentUserAsync();
        // Set giveaway properties
        giveaway.ServerId = serverId;
        giveaway.UserId = hostUser.Id;
        giveaway.Ended = 0;
        giveaway.MessageId = msg.Id;
        giveaway.Emote = emote.ToString();

        // Save to database
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var entry = await dbContext.InsertWithInt32IdentityAsync(giveaway);
        var gway = await dbContext.Giveaways.FirstOrDefaultAsync(x => x.Id == entry);

        // Add button or captcha if needed
        if (giveaway.UseButton)
        {
            var builder = new ComponentBuilder()
                .WithButton(strings.GiveawayEnterButton(serverId), $"entergiveaway:{entry}", emote: emote);

            await msg.ModifyAsync(x => x.Components = builder.Build());
        }

        if (giveaway.UseCaptcha)
        {
            try
            {
                var builder = new ComponentBuilder()
                    .WithButton(strings.GiveawayEnterCaptchaButton(serverId),
                        url: $"{credentials.GiveawayEntryUrl}?guildId={guild.Id}&giveawayId={entry}",
                        style: ButtonStyle.Link,
                        emote: emote);

                await msg.ModifyAsync(x => x.Components = builder.Build());
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error adding captcha button to giveaway {GiveawayId}", entry);
                throw;
            }
        }

        // Schedule timer
        await ScheduleGiveaway(gway);

        return gway;
    }

    /// <summary>
    ///     Initiates a giveaway with specified parameters.
    /// </summary>
    /// <param name="chan">The text channel where the giveaway will be initiated.</param>
    /// <param name="ts">The duration of the giveaway.</param>
    /// <param name="item">The item or prize being given away.</param>
    /// <param name="winners">The number of winners for the giveaway.</param>
    /// <param name="host">The ID of the user hosting the giveaway.</param>
    /// <param name="serverId">The ID of the server where the giveaway is being hosted.</param>
    /// <param name="currentChannel">The current text channel where the command is being executed.</param>
    /// <param name="guild">The guild where the giveaway is being initiated.</param>
    /// <param name="reqroles">Optional: Roles required to enter the giveaway.</param>
    /// <param name="blacklistusers">Optional: Users blacklisted from entering the giveaway.</param>
    /// <param name="blacklistroles">Optional: Roles blacklisted from entering the giveaway.</param>
    /// <param name="interaction">Optional: The Discord interaction related to the giveaway.</param>
    /// <param name="banner">Optional: The URL of the banner for the giveaway.</param>
    /// <param name="pingRole">Optional: The role to ping for the giveaway.</param>
    /// <param name="useButton">Whether to use a button for entry instead of reaction.</param>
    /// <param name="useCaptcha">Whether to use captcha for giveaway entry.</param>
    /// <param name="messageCount">Minimum message count required to enter.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task GiveawaysInternal(
        ITextChannel chan,
        TimeSpan ts,
        string item,
        int winners,
        ulong host,
        ulong serverId,
        ITextChannel currentChannel,
        IGuild guild,
        string? reqroles = null,
        string? blacklistusers = null,
        string? blacklistroles = null,
        IDiscordInteraction? interaction = null,
        string? banner = null,
        IRole? pingRole = null,
        bool useButton = false,
        bool useCaptcha = false,
        ulong messageCount = 0)
    {
        // Get guild config
        var gconfig = await GetGuildConfigCached(serverId);

        // Determine ping role
        IRole? role = null;
        if (gconfig.GiveawayPingRole != 0)
        {
            role = guild.GetRole(gconfig.GiveawayPingRole);
        }

        if (pingRole is not null)
        {
            role = pingRole;
        }

        // Get host user
        var hostuser = await guild.GetUserAsync(host);

        // Create giveaway embed
        var emote = (await GetGiveawayEmote(guild.Id)).ToIEmote();
        var endTime = DateTime.UtcNow.Add(ts);

        var giveawayData = new Giveaway
        {
            Item = item,
            When = endTime,
            Winners = winners,
            RestrictTo = reqroles,
            MessageCountReq = messageCount
        };

        var eb = CreateGiveawayEmbed(guild, giveawayData, emote, gconfig, hostuser);

        // Add custom banner if provided
        if (!string.IsNullOrEmpty(banner) && Uri.IsWellFormedUriString(banner, UriKind.Absolute))
        {
            eb.WithImageUrl(banner);
        }

        // Send giveaway message
        var msg = await chan.SendMessageAsync(
            role is not null ? role.Mention : "",
            embed: eb.Build());

        // Add reaction if not using button or captcha
        if (!useButton && !useCaptcha)
            await msg.AddReactionAsync(emote);

        // Create giveaway entry
        var giveaway = new Giveaway
        {
            ChannelId = chan.Id,
            UserId = host,
            ServerId = serverId,
            Ended = 0,
            When = endTime,
            Item = item,
            MessageId = msg.Id,
            Winners = winners,
            Emote = emote.ToString(),
            UseButton = useButton,
            UseCaptcha = useCaptcha,
            MessageCountReq = messageCount,
            RestrictTo = reqroles
        };

        // Save to database
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var entry = await dbContext.InsertWithInt32IdentityAsync(giveaway);
        var gway = await dbContext.Giveaways.FirstOrDefaultAsync(x => x.Id == entry);

        // Add button or captcha components if needed
        if (useButton)
        {
            var builder = new ComponentBuilder()
                .WithButton(strings.GiveawayEnterButton(serverId), $"entergiveaway:{entry}", emote: emote);

            await msg.ModifyAsync(x => x.Components = builder.Build());
        }

        if (useCaptcha)
        {
            try
            {
                var builder = new ComponentBuilder()
                    .WithButton(strings.GiveawayEnterCaptchaButton(serverId),
                        url: $"{credentials.GiveawayEntryUrl}?guildId={guild.Id}&giveawayId={entry}",
                        style: ButtonStyle.Link,
                        emote: emote);

                await msg.ModifyAsync(x => x.Components = builder.Build());
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error adding captcha button to giveaway {GiveawayId}", entry);
                throw;
            }
        }

        // Schedule the giveaway
        await ScheduleGiveaway(gway);

        // Send confirmation
        if (interaction is not null)
            await interaction.SendConfirmFollowupAsync(strings.GiveawayStarted(guild.Id, chan.Mention));
        else
            await currentChannel.SendConfirmAsync(strings.GiveawayStarted(guild.Id, chan.Mention));
    }

    /// <summary>
    ///     Performs giveaway completion actions, selecting winners and notifying participants.
    /// </summary>
    /// <param name="giveaway">The giveaway to complete.</param>
    /// <param name="inputGuild">Optional: The guild where the giveaway is being conducted.</param>
    /// <param name="inputChannel">Optional: The text channel where the giveaway is being conducted.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task GiveawayTimerAction(
        Giveaway giveaway,
        IGuild? inputGuild = null,
        ITextChannel? inputChannel = null)
    {
        try
        {
            // Get guild and channel
            var guild = inputGuild ?? client.GetGuild(giveaway.ServerId);
            if (guild is null)
            {
                logger.LogWarning(
                    "Guild {GuildId} not found for giveaway {GiveawayId}, marking as completed to clean up",
                    giveaway.ServerId,
                    giveaway.Id);
                await MarkGiveawayEnded(giveaway);
                return;
            }

            var channel = inputChannel ?? await guild.GetTextChannelAsync(giveaway.ChannelId);
            if (channel is null)
            {
                logger.LogWarning(
                    "Channel {ChannelId} not found for giveaway {GiveawayId}, marking as completed to clean up",
                    giveaway.ChannelId,
                    giveaway.Id);
                await MarkGiveawayEnded(giveaway);
                return;
            }

            // Get giveaway message
            IUserMessage? message;
            try
            {
                if (await channel.GetMessageAsync(giveaway.MessageId) is not IUserMessage msg)
                {
                    logger.LogWarning(
                        "Message {MessageId} not found for giveaway {GiveawayId}, marking as completed to clean up",
                        giveaway.MessageId,
                        giveaway.Id);
                    await MarkGiveawayEnded(giveaway);
                    return;
                }

                message = msg;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving message {MessageId} for giveaway {GiveawayId}",
                    giveaway.MessageId, giveaway.Id);
                return;
            }

            // Get command prefix for the guild
            var prefix = await guildConfig.GetPrefix(guild);

            // Get participants
            var emote = giveaway.Emote.ToIEmote();
            if (emote.Name == null)
            {
                await channel.SendErrorAsync(
                    $"[This Giveaway]({message.GetJumpUrl()}) failed because the emote used for it is invalid!",
                    config);
                return;
            }

            // Collect participants - either from reactions or from database
            HashSet<IUser> participants = new();

            if (giveaway.UseButton || giveaway.UseCaptcha)
            {
                await using var dbContext = await dbFactory.CreateConnectionAsync();
                var userIds = await dbContext.GiveawayUsers
                    .Where(x => x.GiveawayId == giveaway.Id)
                    .Select(x => x.UserId)
                    .ToListAsync();

                foreach (var userId in userIds)
                {
                    var user = await guild.GetUserAsync(userId);
                    if (user != null)
                        participants.Add(user);
                }
            }
            else
            {
                // Get reaction users with pagination to avoid memory issues
                participants = await GetAllReactionUsers(message, emote);

                // Fallback to configured emote if no participants
                if (participants.Count == 0)
                {
                    var configEmote = await GetGiveawayEmote(guild.Id);
                    var configEmoteObj = configEmote.ToIEmote();
                    if (configEmoteObj.Name != null)
                    {
                        participants = await GetAllReactionUsers(message, configEmoteObj);
                    }
                }
            }

            // Filter out bots
            var eligibleUsers = participants
                .Where(x => !x.IsBot)
                .Select(x => guild.GetUserAsync(x.Id).GetAwaiter().GetResult())
                .Where(x => x is not null)
                .ToList();

            // Handle not enough participants
            if (eligibleUsers.Count < giveaway.Winners)
            {
                var eb = new EmbedBuilder
                {
                    Color = Mewdeko.ErrorColor, Description = strings.GiveawayNotEnoughParticipants(guild.Id)
                };

                await message.ModifyAsync(x =>
                {
                    x.Embed = eb.Build();
                    x.Content = null;
                    x.Components = null;
                });

                await MarkGiveawayEnded(giveaway);
                return;
            }

            // Apply role requirements if specified
            if (!string.IsNullOrEmpty(giveaway.RestrictTo))
            {
                var requiredRoleIds = giveaway.RestrictTo.Split(" ")
                    .Where(id => ulong.TryParse(id, out _))
                    .Select(ulong.Parse)
                    .ToList();

                if (requiredRoleIds.Count > 0)
                {
                    eligibleUsers = eligibleUsers
                        .Where(user => user.GetRoles().Any() &&
                                       user.GetRoles().Select(role => role.Id)
                                           .Intersect(requiredRoleIds).Count() == requiredRoleIds.Count)
                        .ToList();
                }
            }

            // Apply message count requirements if specified
            if (giveaway.MessageCountReq > 0 && eligibleUsers.Count > 0)
            {
                var usersWithMessageCounts = new Dictionary<IGuildUser, ulong>();
                foreach (var user in eligibleUsers)
                {
                    var messageCount = await msgCntService.GetMessageCount(
                        MessageCountService.CountQueryType.User,
                        giveaway.ServerId,
                        user.Id);

                    usersWithMessageCounts.Add(user, messageCount);
                }

                eligibleUsers = usersWithMessageCounts
                    .Where(x => x.Value >= giveaway.MessageCountReq)
                    .Select(x => x.Key)
                    .ToList();
            }

            // Handle no eligible users after filtering
            if (eligibleUsers.Count == 0)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(strings.GiveawayNoRequirements(guild.Id))
                    .Build();

                await message.ModifyAsync(x =>
                {
                    x.Embed = eb;
                    x.Content = null;
                    x.Components = null;
                });

                await MarkGiveawayEnded(giveaway);
                return;
            }

            // Get guild settings for DM configuration
            var guildSettings = await GetGuildConfigCached(guild.Id);

            // Select winners and notify them
            if (giveaway.Winners == 1)
            {
                await HandleSingleWinner(
                    giveaway,
                    eligibleUsers,
                    message,
                    channel,
                    guild,
                    guildSettings,
                    prefix);
            }
            else
            {
                await HandleMultipleWinners(
                    giveaway,
                    eligibleUsers,
                    message,
                    channel,
                    guild,
                    prefix);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing giveaway {GiveawayId}", giveaway.Id);
        }
    }

    #endregion

    #region Private Helper Methods

    private async Task<HashSet<IUser>> GetAllReactionUsers(IUserMessage message, IEmote emote)
    {
        HashSet<IUser> users = new();

        try
        {
            var reactUsers = await message.GetReactionUsersAsync(emote, 1000).FlattenAsync();

            foreach (var user in reactUsers)
            {
                users.Add(user);
            }

            logger.LogDebug("Retrieved {Count} reaction users for message {MessageId}",
                users.Count, message.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error fetching reaction users for message {MessageId}", message.Id);
        }

        return users;
    }

    private async Task MarkGiveawayEnded(Giveaway giveaway)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        giveaway.Ended = 1;
        await dbContext.UpdateAsync(giveaway);

        // Clean up timer
        CleanupGiveawayTimer(giveaway.Id);
    }

    private async Task HandleSingleWinner(
        Giveaway giveaway,
        List<IGuildUser> eligibleUsers,
        IUserMessage message,
        ITextChannel channel,
        IGuild guild,
        GuildConfig guildSettings,
        string prefix)
    {
        // Select single winner randomly
        var rand = new Random();
        var index = rand.Next(eligibleUsers.Count);
        var winner = eligibleUsers[index];

        // Send DM to winner if configured
        if (guildSettings.DmOnGiveawayWin)
        {
            await SendWinnerDm(winner, giveaway, guild, guildSettings, channel);
        }

        // Update giveaway message
        var winnerEmbed = message.Embeds.FirstOrDefault().ToEmbedBuilder()
            .WithErrorColor()
            .WithDescription(strings.GiveawayWinnerAnnouncement(winner.Guild.Id, winner.Mention, giveaway.UserId))
            .WithFooter(strings.GiveawayEndedFooter(guild.Id,
                DateTime.UtcNow.ToString(strings.DatetimeFormat(guild.Id))));

        await message.ModifyAsync(x =>
        {
            x.Embed = winnerEmbed.Build();
            x.Content = strings.GiveawayEndedContent(guild.Id, giveaway.Emote);
            x.Components = null;
        });

        // Send winner announcement message
        await channel.SendMessageAsync(
            strings.GiveawayCongratulations(guild.Id, winner.Mention, giveaway.Emote),
            embed: new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(
                    strings.GiveawayWinnerDescription(guild.Id, winner.Mention, giveaway.Item, giveaway.ServerId,
                        giveaway.ChannelId, giveaway.MessageId, giveaway.UserId, prefix))
                .Build());

        // Mark giveaway as ended
        await MarkGiveawayEnded(giveaway);
    }

    private async Task HandleMultipleWinners(
        Giveaway giveaway,
        List<IGuildUser> eligibleUsers,
        IUserMessage message,
        ITextChannel channel,
        IGuild guild,
        string prefix)
    {
        // Select multiple winners randomly
        var rand = new Random();
        var winners = eligibleUsers
            .OrderBy(_ => rand.Next())
            .Take(giveaway.Winners)
            .ToList();

        // Update giveaway message
        var winnerEmbed = message.Embeds.FirstOrDefault().ToEmbedBuilder()
            .WithErrorColor()
            .WithDescription(
                strings.GiveawayWinnerSimple(guild.Id,
                    string.Join(strings.CommaSeparator(guild.Id), winners.Select(x => x.Mention))) + "\n" +
                $"Hosted by: <@{giveaway.UserId}>")
            .WithFooter(strings.GiveawayEndedFooter(guild.Id,
                DateTime.UtcNow.ToString(strings.DatetimeFormat(guild.Id))));

        await message.ModifyAsync(x =>
        {
            x.Embed = winnerEmbed.Build();
            x.Content = strings.GiveawayEndedContent(guild.Id, giveaway.Emote);
            x.Components = null;
        });

        // Send winner announcement message in chunks to avoid message limits
        foreach (var winnerChunk in winners.Chunk(50))
        {
            await channel.SendMessageAsync(
                strings.GiveawayCongratulations(guild.Id,
                    string.Join(strings.CommaSeparator(guild.Id), winnerChunk.Select(x => x.Mention)),
                    giveaway.Emote),
                embed: new EmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(
                        strings.GiveawayWinnerDescription(guild.Id,
                            string.Join(strings.CommaSeparator(guild.Id), winnerChunk.Select(x => x.Mention)),
                            giveaway.Item, giveaway.ServerId,
                            giveaway.ChannelId, giveaway.MessageId, giveaway.UserId, prefix))
                    .Build());
        }

        // Mark giveaway as ended
        await MarkGiveawayEnded(giveaway);
    }

    private async Task SendWinnerDm(
        IGuildUser winner,
        Giveaway giveaway,
        IGuild guild,
        GuildConfig guildSettings,
        ITextChannel channel)
    {
        try
        {
            // Use custom end message if available
            if (!string.IsNullOrEmpty(guildSettings.GiveawayEndMessage))
            {
                var replacer = new ReplacementBuilder()
                    .WithChannel(channel)
                    .WithClient(client)
                    .WithServer(client, guild as SocketGuild)
                    .WithUser(winner);

                replacer.WithOverride("%messagelink%",
                    () => $"https://discord.com/channels/{guild.Id}/{channel.Id}/{giveaway.MessageId}");
                replacer.WithOverride("%giveawayitem%", () => giveaway.Item);
                replacer.WithOverride("%giveawaywinners%", () => giveaway.Winners.ToString());

                var customMessage = replacer.Build().Replace(guildSettings.GiveawayEndMessage);

                // Try to parse as SmartEmbed
                if (SmartEmbed.TryParse(customMessage, guild.Id, out var embeds, out var plaintext, out var components))
                {
                    await winner.SendMessageAsync(plaintext, embeds: embeds, components: components?.Build());
                }
                else
                {
                    // Send as regular embed with custom message
                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription(
                            strings.GiveawayWinnerDm(guild.Id, giveaway.Item, giveaway.ServerId, giveaway.ChannelId,
                                giveaway.MessageId));

                    embed.AddField(strings.GiveawayMessageFromHost(guild.Id), customMessage);
                    await winner.SendMessageAsync(embed: embed.Build());
                }
            }
            else
            {
                // Send default winner DM
                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription(
                        strings.GiveawayWinnerDm(guild.Id, giveaway.Item, giveaway.ServerId, giveaway.ChannelId,
                            giveaway.MessageId));

                await winner.SendMessageAsync(embed: embed.Build());
            }
        }
        catch (Exception ex)
        {
            // Ignore DM errors
            logger.LogWarning(ex, "Failed to send DM to winner {UserId} for giveaway {GiveawayId}",
                winner.Id, giveaway.Id);
        }
    }

    private EmbedBuilder CreateGiveawayEmbed(
        IGuild guild,
        Giveaway giveaway,
        IEmote emote,
        GuildConfig guildConfig,
        IUser? hostUser = null)
    {
        // Create base embed
        var eb = new EmbedBuilder
        {
            Color = Mewdeko.OkColor, Title = giveaway.Item
        };

        // Build description based on available information
        string description;
        if (hostUser != null)
        {
            description = $"React with {emote} to enter!\n" +
                          $"Hosted by {hostUser.Mention}\n" +
                          $"End Time: <t:{giveaway.When.ToUnixEpochDate()}:R> (<t:{giveaway.When.ToUnixEpochDate()}>)\n";
        }
        else
        {
            description = $"React with {emote} to enter!\n" +
                          $"Hosted by {hostUser.Mention}\n" +
                          $"End Time: <t:{giveaway.When.ToUnixEpochDate()}:R> (<t:{giveaway.When.ToUnixEpochDate()}>)\n";
        }

        // Add role requirements if specified
        if (!string.IsNullOrEmpty(giveaway.RestrictTo))
        {
            var roleIds = giveaway.RestrictTo.Split(" ")
                .Where(s => ulong.TryParse(s, out _))
                .Select(ulong.Parse);

            var roles = new List<IRole>();
            foreach (var roleId in roleIds)
            {
                try
                {
                    var role = guild.GetRole(roleId);
                    if (role != null)
                        roles.Add(role);
                }
                catch
                {
                    // Ignored
                }
            }

            if (roles.Count > 0)
            {
                description = $"React with {emote} to enter!\n" +
                              $"Hosted by {hostUser.Mention}\n" +
                              $"Required Roles: {string.Join("\n", roles.Select(x => x.Mention))}\n" +
                              $"End Time: <t:{giveaway.When.ToUnixEpochDate()}:R> (<t:{giveaway.When.ToUnixEpochDate()}>)\n";
            }
        }

        // Add message count requirement if specified
        if (giveaway.MessageCountReq > 0)
        {
            description += $"\n{giveaway.MessageCountReq} Messages Required.";
        }

        eb.WithDescription(description);

        // Set footer
        eb.WithFooter(new EmbedFooterBuilder()
            .WithText($"{giveaway.Winners} Winners | {guild} Giveaways | Ends on {giveaway.When:dd.MM.yyyy}"));

        // Apply custom color if configured
        if (!string.IsNullOrEmpty(guildConfig.GiveawayEmbedColor))
        {
            var colorStr = guildConfig.GiveawayEmbedColor;

            if (colorStr.StartsWith("#"))
                eb.WithColor(new Color(Convert.ToUInt32(colorStr.Replace("#", ""), 16)));
            else if (colorStr.StartsWith("0x") && colorStr.Length == 8)
                eb.WithColor(new Color(Convert.ToUInt32(colorStr.Replace("0x", ""), 16)));
            else if (colorStr.Length == 6 && IsHex(colorStr))
                eb.WithColor(new Color(Convert.ToUInt32(colorStr, 16)));
            else if (uint.TryParse(colorStr, out var colorNumber))
                eb.WithColor(new Color(colorNumber));
        }

        // Add banner if configured
        if (!string.IsNullOrEmpty(guildConfig.GiveawayBanner) &&
            Uri.IsWellFormedUriString(guildConfig.GiveawayBanner, UriKind.Absolute))
        {
            eb.WithImageUrl(guildConfig.GiveawayBanner);
        }

        return eb;

        // Local helper function
        bool IsHex(string value)
        {
            return value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');
        }
    }

    private async Task<GuildConfig> GetGuildConfigCached(ulong guildId)
    {
        // Check if we have a valid cached config
        if (guildConfigCache.TryGetValue(guildId, out var cached) &&
            cached.Expiry > DateTime.UtcNow)
        {
            return cached.Config;
        }

        // Get config from service and cache for 15 minutes
        var config = await guildConfig.GetGuildConfig(guildId);
        guildConfigCache[guildId] = (config, DateTime.UtcNow.AddMinutes(15));

        return config;
    }

    private void InvalidateGuildConfigCache(ulong guildId)
    {
        guildConfigCache.TryRemove(guildId, out _);
    }

    private async Task<IEnumerable<Giveaway>> GetGiveawaysBeforeAsync(DateTime now)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Find active giveaways that should have ended by now
        var giveaways = await dbContext.Giveaways
            .Where(x => x.Ended != 1 && x.When < now)
            .ToListAsync();

        return giveaways;
    }

    private async Task ScheduleGiveaway(Giveaway giveaway)
    {
        try
        {
            await timerLock.WaitAsync();

            // Calculate time until giveaway ends
            var timeToGo = giveaway.When - DateTime.UtcNow;
            if (timeToGo <= TimeSpan.Zero)
            {
                timeToGo = TimeSpan.Zero;
            }

            // Use a separate state object to minimize memory leaks from closures
            var state = new GiveawayTimerState
            {
                GiveawayId = giveaway.Id
            };

            // Remove existing timer if present
            if (giveawayTimers.TryRemove(giveaway.Id, out var existingTimer))
            {
                await existingTimer.DisposeAsync();
            }

            // Create new timer
            var timer = new Timer(
                GiveawayTimerCallback,
                state,
                timeToGo,
                Timeout.InfiniteTimeSpan);

            giveawayTimers[giveaway.Id] = timer;

            logger.LogDebug("Scheduled giveaway {GiveawayId} to end in {TimeToGo}",
                giveaway.Id, timeToGo.Humanize());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error scheduling giveaway {GiveawayId}", giveaway.Id);
        }
        finally
        {
            timerLock.Release();
        }
    }

    private class GiveawayTimerState
    {
        public int GiveawayId { get; set; }
    }

    private async void GiveawayTimerCallback(object? state)
    {
        if (state is not GiveawayTimerState timerState)
            return;

        try
        {
            // Get latest giveaway data to ensure it's still active
            var giveaway = await GetGiveawayById(timerState.GiveawayId);

            if (giveaway == null || giveaway.Ended == 1)
            {
                CleanupGiveawayTimer(timerState.GiveawayId);
                return;
            }

            // Process the giveaway
            await GiveawayTimerAction(giveaway);

            // Clean up timer
            CleanupGiveawayTimer(timerState.GiveawayId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in giveaway timer callback for giveaway {GiveawayId}", timerState.GiveawayId);
            CleanupGiveawayTimer(timerState.GiveawayId);
        }
    }

    private void CleanupGiveawayTimer(int giveawayId)
    {
        if (giveawayTimers.TryRemove(giveawayId, out var timer))
        {
            timer.Dispose();
        }
    }

    private void CleanupResources()
    {
        try
        {
            logger.LogDebug("Running giveaway cleanup, current timer count: {TimerCount}", giveawayTimers.Count);

            // Clean up expired guild config cache entries
            var expiredConfigs = guildConfigCache
                .Where(kv => kv.Value.Expiry < DateTime.UtcNow)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var guildId in expiredConfigs)
            {
                guildConfigCache.TryRemove(guildId, out _);
            }

            // Clean up orphaned giveaways and query for all active giveaways to validate timers
            Task.Run(async () =>
            {
                try
                {
                    await CleanupOrphanedGiveaways();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during orphaned giveaway cleanup");
                }

                try
                {
                    await using var dbContext = await dbFactory.CreateConnectionAsync();
                    var activeGiveaways = await dbContext.Giveaways
                        .Where(g => g.Ended != 1)
                        .Select(g => g.Id)
                        .ToListAsync();

                    var activeIds = activeGiveaways.ToHashSet();

                    // Clean up timers for ended giveaways
                    var timersToRemove = giveawayTimers.Keys
                        .Where(id => !activeIds.Contains(id))
                        .ToList();

                    foreach (var id in timersToRemove)
                    {
                        CleanupGiveawayTimer(id);
                    }

                    // Check for active giveaways missing timers
                    var missingTimers = activeGiveaways
                        .Where(id => !giveawayTimers.ContainsKey(id))
                        .ToList();

                    foreach (var id in missingTimers)
                    {
                        var giveaway = await GetGiveawayById(id);
                        if (giveaway != null && giveaway.When > DateTime.UtcNow)
                        {
                            await ScheduleGiveaway(giveaway);
                        }
                    }

                    logger.LogDebug("Giveaway cleanup complete, new timer count: {TimerCount}", giveawayTimers.Count);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during giveaway timer verification");
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during giveaway cleanup");
        }
    }

    private async Task InitializeGiveawaysAsync()
    {
        try
        {
            logger.LogInformation("Initializing Giveaways");
            var now = DateTime.UtcNow;

            // Load active giveaways
            var activeGiveaways = await GetGiveawaysBeforeAsync(now.AddHours(24));
            var count = activeGiveaways.Count();

            logger.LogInformation("Found {Count} active giveaways to schedule", count);

            // Schedule each giveaway
            foreach (var giveaway in activeGiveaways)
            {
                await ScheduleGiveaway(giveaway);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing giveaways");
        }
    }

    /// <summary>
    ///     Releases all resources used by the <see cref="GiveawayService" />.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;

        // Dispose cleanup timer
        cleanupTimer?.Dispose();

        // Dispose all giveaway timers
        foreach (var timer in giveawayTimers.Values)
        {
            timer.Dispose();
        }

        giveawayTimers.Clear();
        guildConfigCache.Clear();

        // Dispose semaphore
        timerLock.Dispose();

        logger.LogInformation("GiveawayService disposed");
    }

    /// <summary>
    ///     Cleans up giveaways for guilds/channels that are no longer accessible.
    /// </summary>
    private async Task CleanupOrphanedGiveaways()
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();

            // Get all active giveaways
            var activeGiveaways = await dbContext.Giveaways
                .Where(g => g.Ended != 1)
                .ToListAsync();

            var orphanedCount = 0;

            foreach (var giveaway in activeGiveaways)
            {
                var guild = client.GetGuild(giveaway.ServerId);
                if (guild == null)
                {
                    logger.LogDebug(
                        "Marking orphaned giveaway {GiveawayId} as ended - guild {GuildId} no longer accessible",
                        giveaway.Id, giveaway.ServerId);
                    await MarkGiveawayEnded(giveaway);
                    orphanedCount++;
                    continue;
                }

                var channel = guild.GetTextChannel(giveaway.ChannelId);
                if (channel == null)
                {
                    logger.LogDebug(
                        "Marking orphaned giveaway {GiveawayId} as ended - channel {ChannelId} no longer accessible",
                        giveaway.Id, giveaway.ChannelId);
                    await MarkGiveawayEnded(giveaway);
                    orphanedCount++;
                }
            }

            if (orphanedCount > 0)
            {
                logger.LogInformation("Cleaned up {Count} orphaned giveaways", orphanedCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during orphaned giveaway cleanup");
        }
    }

    #endregion
}