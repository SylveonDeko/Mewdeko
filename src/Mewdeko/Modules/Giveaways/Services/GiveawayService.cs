using System.Threading;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.Configs;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Strings;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Swan;

namespace Mewdeko.Modules.Giveaways.Services
{
    /// <summary>
    /// Service for managing giveaways across Discord guilds.
    /// </summary>
    public class GiveawayService : INService, IDisposable
    {
        private readonly DiscordShardedClient client;
        private readonly BotConfig config;
        private readonly BotCredentials credentials;
        private readonly DbContextProvider dbProvider;
        private readonly GuildSettingsService guildSettings;
        private readonly MessageCountService msgCntService;
        private readonly GeneratedBotStrings strings;

        // Memory management
        private readonly ConcurrentDictionary<int, Timer> giveawayTimers = new();
        private readonly ConcurrentDictionary<ulong, (GuildConfig Config, DateTime Expiry)> guildConfigCache = new();
        private readonly SemaphoreSlim timerLock = new(1, 1);
        private readonly Timer cleanupTimer;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="GiveawayService"/> class.
        /// </summary>
        /// <param name="client">The Discord client instance.</param>
        /// <param name="dbProvider">Provider for database contexts.</param>
        /// <param name="guildSettings">Service for accessing guild settings.</param>
        /// <param name="config">Bot configuration settings.</param>
        /// <param name="credentials">Bot credentials.</param>
        /// <param name="msgCntService">Service for tracking message counts.</param>
        /// <param name="strings">Service for localized strings.</param>
        public GiveawayService(
            DiscordShardedClient client,
            DbContextProvider dbProvider,
            GuildSettingsService guildSettings,
            BotConfig config,
            BotCredentials credentials,
            MessageCountService msgCntService,
            GeneratedBotStrings strings)
        {
            this.client = client;
            this.dbProvider = dbProvider;
            this.guildSettings = guildSettings;
            this.config = config;
            this.credentials = credentials;
            this.msgCntService = msgCntService;
            this.strings = strings;

            // Set up periodic cleanup (every 60 minutes)
            cleanupTimer = new Timer(_ => CleanupResources(), null, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60));

            // Initialize giveaways
            _ = InitializeGiveawaysAsync();
        }

        #region Public Methods

        /// <summary>
        /// Sets the emote to be used for giveaways in the specified guild.
        /// </summary>
        /// <param name="guild">The guild where the emote is to be set.</param>
        /// <param name="emote">The emote to set.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SetGiveawayEmote(IGuild guild, string emote)
        {
            await using var dbContext = await dbProvider.GetContextAsync();
            var gc = await dbContext.ForGuildId(guild.Id, set => set);
            gc.GiveawayEmote = emote;
            await guildSettings.UpdateGuildConfig(guild.Id, gc);

            // Update cache if exists
            InvalidateGuildConfigCache(guild.Id);
        }

        /// <summary>
        /// Retrieves the emote used for giveaways in the guild with the specified ID.
        /// </summary>
        /// <param name="id">The ID of the guild.</param>
        /// <returns>The emote used for giveaways.</returns>
        public async Task<string> GetGiveawayEmote(ulong id)
        {
            return (await GetGuildConfigCached(id)).GiveawayEmote;
        }

        /// <summary>
        /// Gets a giveaway by its ID.
        /// </summary>
        /// <param name="id">The giveaway ID.</param>
        /// <returns>A giveaway or null if not found.</returns>
        public async Task<Database.Models.Giveaways?> GetGiveawayById(int id)
        {
            await using var dbContext = await dbProvider.GetContextAsync();
            return await dbContext.Giveaways
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == id);
        }

        /// <summary>
        /// Adds a user to a giveaway.
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <param name="giveawayId">The giveaway ID.</param>
        /// <returns>A tuple containing a success flag and an optional error message.</returns>
        public async Task<(bool Success, string? ErrorMessage)> AddUserToGiveaway(ulong userId, int giveawayId)
        {
            await using var dbContext = await dbProvider.GetContextAsync();

            // Validate giveaway exists and is active
            var giveaway = await GetGiveawayById(giveawayId);
            if (giveaway == null)
                return (false, "That giveaway does not exist.");

            if (giveaway.Ended == 1)
                return (false, "That giveaway has ended.");

            if (!giveaway.UseButton && !giveaway.UseCaptcha)
                return (false, "This giveaway doesn't use a button/captcha.");

            // Verify user is in the guild
            var guild = client.GetGuild(giveaway.ServerId);
            if (guild == null)
                return (false, "The guild for this giveaway could not be found.");

            var users = await guild.GetUsersAsync().FlattenAsync();
            if (users.All(u => u.Id != userId))
                return (false, "That user is not in the server for this giveaway.");

            // Check if user is already entered
            var existing = await dbContext.GiveawayUsers
                .AsNoTracking()
                .AnyAsync(gu => gu.UserId == userId && gu.GiveawayId == giveawayId);

            if (existing)
                return (false, "User has already entered this giveaway.");

            // Add user to giveaway
            dbContext.GiveawayUsers.Add(new GiveawayUsers
            {
                UserId = userId,
                GiveawayId = giveawayId
            });

            await dbContext.SaveChangesAsync();
            return (true, null);
        }

        /// <summary>
        /// Creates a giveaway from the dashboard.
        /// </summary>
        /// <param name="serverId">The ID of the server where the giveaway is being created.</param>
        /// <param name="giveaway">The giveaway data.</param>
        /// <returns>The created giveaway.</returns>
        /// <exception cref="Exception">Thrown when guild or channel is not found.</exception>
        public async Task<Database.Models.Giveaways> CreateGiveawayFromDashboard(ulong serverId,
            Database.Models.Giveaways giveaway)
        {
            // Validate guild and channel
            var guild = client.GetGuild(serverId);
            if (guild == null)
                throw new Exception("Guild not found");

            var channel = guild.GetTextChannel(giveaway.ChannelId);
            if (channel == null)
                throw new Exception("Channel not found");

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
            var eb = CreateGiveawayEmbed(guild, giveaway, emote, gconfig);

            // Send giveaway message
            var msg = await channel.SendMessageAsync(
                pingRole != null ? pingRole.Mention : "",
                embed: eb.Build());

            // Add reaction or button
            if (!giveaway.UseButton && !giveaway.UseCaptcha)
                await msg.AddReactionAsync(emote);

            // Set giveaway properties
            giveaway.ServerId = serverId;
            giveaway.UserId = guild.CurrentUser.Id;
            giveaway.Ended = 0;
            giveaway.MessageId = msg.Id;
            giveaway.Emote = emote.ToString();

            // Save to database
            await using var dbContext = await dbProvider.GetContextAsync();
            var entry = dbContext.Giveaways.Add(giveaway);
            await dbContext.SaveChangesAsync();

            // Add button or captcha if needed
            if (giveaway.UseButton)
            {
                var builder = new ComponentBuilder()
                    .WithButton("Enter", customId: $"entergiveaway:{entry.Entity.Id}", emote: emote);

                await msg.ModifyAsync(x => x.Components = builder.Build());
            }

            if (giveaway.UseCaptcha)
            {
                try
                {
                    var builder = new ComponentBuilder()
                        .WithButton("Enter (Web Captcha)",
                            url: $"{credentials.GiveawayEntryUrl}?guildId={guild.Id}&giveawayId={entry.Entity.Id}",
                            style: ButtonStyle.Link,
                            emote: emote);

                    await msg.ModifyAsync(x => x.Components = builder.Build());
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error adding captcha button to giveaway {GiveawayId}", entry.Entity.Id);
                    throw;
                }
            }

            // Schedule timer
            await ScheduleGiveaway(entry.Entity);

            return entry.Entity;
        }

        /// <summary>
        /// Initiates a giveaway with specified parameters.
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

            var giveawayData = new Database.Models.Giveaways
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
            var giveaway = new Database.Models.Giveaways
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
            await using var dbContext = await dbProvider.GetContextAsync();
            var entry = dbContext.Giveaways.Add(giveaway);
            await dbContext.SaveChangesAsync();

            // Add button or captcha components if needed
            if (useButton)
            {
                var builder = new ComponentBuilder()
                    .WithButton("Enter", customId: $"entergiveaway:{entry.Entity.Id}", emote: emote);

                await msg.ModifyAsync(x => x.Components = builder.Build());
            }

            if (useCaptcha)
            {
                try
                {
                    var builder = new ComponentBuilder()
                        .WithButton("Enter (Web Captcha)",
                            url: $"{credentials.GiveawayEntryUrl}?guildId={guild.Id}&giveawayId={entry.Entity.Id}",
                            style: ButtonStyle.Link,
                            emote: emote);

                    await msg.ModifyAsync(x => x.Components = builder.Build());
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error adding captcha button to giveaway {GiveawayId}", entry.Entity.Id);
                    throw;
                }
            }

            // Schedule the giveaway
            await ScheduleGiveaway(entry.Entity);

            // Send confirmation
            if (interaction is not null)
                await interaction.SendConfirmFollowupAsync(strings.GiveawayStarted(guild.Id, chan.Mention));
            else
                await currentChannel.SendConfirmAsync(strings.GiveawayStarted(guild.Id, chan.Mention));
        }

        /// <summary>
        /// Performs giveaway completion actions, selecting winners and notifying participants.
        /// </summary>
        /// <param name="giveaway">The giveaway to complete.</param>
        /// <param name="inputGuild">Optional: The guild where the giveaway is being conducted.</param>
        /// <param name="inputChannel">Optional: The text channel where the giveaway is being conducted.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task GiveawayTimerAction(
            Database.Models.Giveaways giveaway,
            IGuild? inputGuild = null,
            ITextChannel? inputChannel = null)
        {
            try
            {
                // Get guild and channel
                var guild = inputGuild ?? client.GetGuild(giveaway.ServerId);
                if (guild is null)
                {
                    Log.Warning("Guild {GuildId} not found for giveaway {GiveawayId}", giveaway.ServerId, giveaway.Id);
                    return;
                }

                var channel = inputChannel ?? await guild.GetTextChannelAsync(giveaway.ChannelId);
                if (channel is null)
                {
                    Log.Warning("Channel {ChannelId} not found for giveaway {GiveawayId}", giveaway.ChannelId, giveaway.Id);
                    return;
                }

                // Get giveaway message
                IUserMessage? message;
                try
                {
                    if (await channel.GetMessageAsync(giveaway.MessageId) is not IUserMessage msg)
                    {
                        Log.Warning("Message {MessageId} not found for giveaway {GiveawayId}", giveaway.MessageId, giveaway.Id);
                        return;
                    }
                    message = msg;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error retrieving message {MessageId} for giveaway {GiveawayId}",
                        giveaway.MessageId, giveaway.Id);
                    return;
                }

                // Get command prefix for the guild
                var prefix = await this.guildSettings.GetPrefix(guild);

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
                    await using var dbContext = await dbProvider.GetContextAsync();
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
                        Color = Mewdeko.ErrorColor,
                        Description = "There were not enough participants!"
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
                        .WithDescription("Looks like nobody that actually met the requirements joined..")
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
                Log.Error(ex, "Error processing giveaway {GiveawayId}", giveaway.Id);
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

                Log.Debug("Retrieved {Count} reaction users for message {MessageId}",
                    users.Count, message.Id);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error fetching reaction users for message {MessageId}", message.Id);
            }

            return users;
        }

        private async Task MarkGiveawayEnded(Database.Models.Giveaways giveaway)
        {
            await using var dbContext = await dbProvider.GetContextAsync();
            giveaway.Ended = 1;
            dbContext.Giveaways.Update(giveaway);
            await dbContext.SaveChangesAsync();

            // Clean up timer
            CleanupGiveawayTimer(giveaway.Id);
        }

        private async Task HandleSingleWinner(
            Database.Models.Giveaways giveaway,
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
                .WithDescription($"Winner: {winner.Mention}!\nHosted by: <@{giveaway.UserId}>")
                .WithFooter($"Ended at {DateTime.UtcNow:dd.MM.yyyy HH:mm:ss}");

            await message.ModifyAsync(x =>
            {
                x.Embed = winnerEmbed.Build();
                x.Content = $"{giveaway.Emote} **Giveaway Ended!** {giveaway.Emote}";
                x.Components = null;
            });

            // Send winner announcement message
            await channel.SendMessageAsync(
                $"Congratulations to {winner.Mention}! {giveaway.Emote}",
                embed: new EmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(
                        $"{winner.Mention} won the giveaway for [{giveaway.Item}]" +
                        $"(https://discord.com/channels/{giveaway.ServerId}/{giveaway.ChannelId}/{giveaway.MessageId})! \n\n" +
                        $"- (Hosted by: <@{giveaway.UserId}>)\n" +
                        $"- Reroll: `{prefix}reroll {giveaway.MessageId}`")
                    .Build());

            // Mark giveaway as ended
            await MarkGiveawayEnded(giveaway);
        }

        private async Task HandleMultipleWinners(
            Database.Models.Giveaways giveaway,
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
                    $"Winner: {string.Join(", ", winners.Select(x => x.Mention))}!\n" +
                    $"Hosted by: <@{giveaway.UserId}>")
                .WithFooter($"Ended at {DateTime.UtcNow:dd.MM.yyyy HH:mm:ss}");

            await message.ModifyAsync(x =>
            {
                x.Embed = winnerEmbed.Build();
                x.Content = $"{giveaway.Emote} **Giveaway Ended!** {giveaway.Emote}";
                x.Components = null;
            });

            // Send winner announcement message in chunks to avoid message limits
            foreach (var winnerChunk in winners.Chunk(50))
            {
                await channel.SendMessageAsync(
                    $"Congratulations to {string.Join(", ", winnerChunk.Select(x => x.Mention))}! {giveaway.Emote}",
                    embed: new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription(
                            $"{string.Join(", ", winnerChunk.Select(x => x.Mention))} won the giveaway for " +
                            $"[{giveaway.Item}](https://discord.com/channels/{giveaway.ServerId}/{giveaway.ChannelId}/{giveaway.MessageId})! \n\n" +
                            $"- (Hosted by: <@{giveaway.UserId}>)\n" +
                            $"- Reroll: `{prefix}reroll {giveaway.MessageId}`")
                        .Build());
            }

            // Mark giveaway as ended
            await MarkGiveawayEnded(giveaway);
        }

        private async Task SendWinnerDm(
            IGuildUser winner,
            Database.Models.Giveaways giveaway,
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
                        await winner.SendMessageAsync(plaintext, embeds: embeds, components: components);
                    }
                    else
                    {
                        // Send as regular embed with custom message
                        var embed = new EmbedBuilder()
                            .WithOkColor()
                            .WithDescription(
                                $"Congratulations! You won a giveaway for [{giveaway.Item}]" +
                                $"(https://discord.com/channels/{giveaway.ServerId}/{giveaway.ChannelId}/{giveaway.MessageId})!");

                        embed.AddField("Message from Host", customMessage);
                        await winner.SendMessageAsync(embed: embed.Build());
                    }
                }
                else
                {
                    // Send default winner DM
                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription(
                            $"Congratulations! You won a giveaway for [{giveaway.Item}]" +
                            $"(https://discord.com/channels/{giveaway.ServerId}/{giveaway.ChannelId}/{giveaway.MessageId})!");

                    await winner.SendMessageAsync(embed: embed.Build());
                }
            }
            catch (Exception ex)
            {
                // Ignore DM errors
                Log.Warning(ex, "Failed to send DM to winner {UserId} for giveaway {GiveawayId}",
                    winner.Id, giveaway.Id);
            }
        }

        private EmbedBuilder CreateGiveawayEmbed(
            IGuild guild,
            Database.Models.Giveaways giveaway,
            IEmote emote,
            GuildConfig guildConfig,
            IUser? hostUser = null)
        {
            // Create base embed
            var eb = new EmbedBuilder
            {
                Color = Mewdeko.OkColor,
                Title = giveaway.Item
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
            var config = await guildSettings.GetGuildConfig(guildId, bypassCache: false);
            guildConfigCache[guildId] = (config, DateTime.UtcNow.AddMinutes(15));

            return config;
        }

        private void InvalidateGuildConfigCache(ulong guildId)
        {
            guildConfigCache.TryRemove(guildId, out _);
        }

        private async Task<IEnumerable<Database.Models.Giveaways>> GetGiveawaysBeforeAsync(DateTime now)
        {
            await using var dbContext = await dbProvider.GetContextAsync();

            // Find active giveaways that should have ended by now
            var giveaways = await dbContext.Giveaways
                .ToLinqToDB()
                .Where(x => x.Ended != 1 && x.When < now)
                .ToListAsyncEF();

            return giveaways;
        }

        private async Task ScheduleGiveaway(Database.Models.Giveaways giveaway)
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
                var state = new GiveawayTimerState { GiveawayId = giveaway.Id };

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

                Log.Debug("Scheduled giveaway {GiveawayId} to end in {TimeToGo}",
                    giveaway.Id, timeToGo.Humanize());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error scheduling giveaway {GiveawayId}", giveaway.Id);
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
                Log.Error(ex, "Error in giveaway timer callback for giveaway {GiveawayId}", timerState.GiveawayId);
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
                Log.Debug("Running giveaway cleanup, current timer count: {TimerCount}", giveawayTimers.Count);

                // Clean up expired guild config cache entries
                var expiredConfigs = guildConfigCache
                    .Where(kv => kv.Value.Expiry < DateTime.UtcNow)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var guildId in expiredConfigs)
                {
                    guildConfigCache.TryRemove(guildId, out _);
                }

                // Query for all active giveaways to validate timers
                Task.Run(async () =>
                {
                    try
                    {
                        await using var dbContext = await dbProvider.GetContextAsync();
                        var activeGiveaways = await dbContext.Giveaways
                            .AsNoTracking()
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

                        Log.Debug("Giveaway cleanup complete, new timer count: {TimerCount}", giveawayTimers.Count);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error during giveaway timer verification");
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during giveaway cleanup");
            }
        }

        private async Task InitializeGiveawaysAsync()
        {
            try
            {
                Log.Information("Initializing Giveaways");
                var now = DateTime.UtcNow;

                // Load active giveaways
                var activeGiveaways = await GetGiveawaysBeforeAsync(now.AddHours(24));
                var count = activeGiveaways.Count();

                Log.Information("Found {Count} active giveaways to schedule", count);

                // Schedule each giveaway
                foreach (var giveaway in activeGiveaways)
                {
                    await ScheduleGiveaway(giveaway);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing giveaways");
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="GiveawayService"/>.
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

            Log.Information("GiveawayService disposed");
        }

        #endregion
    }
}