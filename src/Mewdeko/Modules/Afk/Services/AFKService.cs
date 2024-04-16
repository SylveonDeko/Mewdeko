using System.Threading;
using Humanizer;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Settings;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Afk.Services;

/// <summary>
/// Handles AFK-related commands and events. Was hell to make.
/// </summary>
public class AfkService : INService, IReadyExecutor
{
    private readonly IDataCache cache;
    private readonly DiscordSocketClient client;
    private readonly BotConfigService config;
    private readonly IBotCredentials creds;
    private readonly DbService db;
    private readonly GuildSettingsService guildSettings;


    /// <summary>
    /// Initializes a new instance of the AfkService class.
    /// </summary>
    /// <param name="db">The database service.</param>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="cache">The data cache.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="config">The bot configuration service.</param>
    public AfkService(
        DbService db,
        DiscordSocketClient client,
        IDataCache cache,
        GuildSettingsService guildSettings,
        EventHandler eventHandler,
        IBotCredentials creds,
        BotConfigService config)
    {
        this.cache = cache;
        this.guildSettings = guildSettings;
        this.creds = creds;
        this.config = config;
        this.db = db;
        this.client = client;
        eventHandler.MessageReceived += MessageReceived;
        eventHandler.MessageUpdated += MessageUpdated;
        eventHandler.UserIsTyping += UserTyping;
        _ = Task.Run(StartTimedAfkLoop);
    }


    /// <summary>
    ///     Handles actions to be performed when the bot is ready.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task OnReadyAsync()
    {
        // Retrieve all AFK entries from the database
        await using var uow = db.GetDbContext();
        var allafk = await uow.Afk.OrderByDescending(afk => afk.DateAdded).ToListAsyncEF();

        // Create a dictionary to store the latest AFK entry per user per guild
        var latestAfkPerUserPerGuild =
            new ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, Database.Models.Afk>>();

        // Get unique guild IDs with AFK entries
        var guildIdsWithAfk = allafk.Select(afk => afk.GuildId).Distinct();

        // Process each guild's AFK entries
        var tasks = guildIdsWithAfk.Select(guildId =>
        {
            var latestAfkPerUser = new ConcurrentDictionary<ulong, Database.Models.Afk>();

            // Get the latest AFK entry for each user in the guild
            var afkEntriesForGuild = allafk.Where(afk => afk.GuildId == guildId)
                .GroupBy(afk => afk.UserId)
                .Select(g => g.First());

            foreach (var afk in afkEntriesForGuild)
            {
                latestAfkPerUser.TryAdd(afk.UserId, afk);
            }

            latestAfkPerUserPerGuild.TryAdd(guildId, latestAfkPerUser);
            return Task.CompletedTask;
        });

        // Wait for all tasks to complete
        await Task.WhenAll(tasks);

        // Cache the latest AFK entries
        await CacheLatestAfks(latestAfkPerUserPerGuild);

        // Set an environment variable to indicate that AFK data is cached
        Environment.SetEnvironmentVariable($"AFK_CACHED_{client.ShardId}", "1");
        Log.Information("AFK Cached");
    }


    /// <summary>
    /// Starts the timed AFK loop asynchronously.
    /// </summary>
    /// <remarks>
    /// This method is used to periodically check for timed AFKs and execute them accordingly.
    /// </remarks>
    private async Task StartTimedAfkLoop()
    {
        // Create a timer to trigger the loop every second
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        // Loop until the timer stops
        while (await timer.WaitForNextTickAsync())
        {
            // Delay for 1 second to avoid high CPU usage
            await Task.Delay(1000).ConfigureAwait(false);
            try
            {
                // Get the current time in UTC
                var now = DateTime.UtcNow;
                // Get the list of timed AFKs that occurred before the current time
                var afks = GetAfkBeforeAsync(now);
                // If there are no timed AFKs, continue to the next iteration
                if (!afks.Any())
                    continue;

                // Log the number of timed AFKs being executed
                Log.Information($"Executing {afks.Count()} timed AFKs.");
                // Execute each timed AFK asynchronously
                await Task.WhenAll(afks.Select(TimedAfkFinished)).ConfigureAwait(false);
                // Delay for a short period to avoid concurrency issues
                await Task.Delay(1500).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Log any errors that occur during the loop
                Log.Warning($"Error in Timed AFK loop: {ex.Message}");
                Log.Warning(ex.ToString());
            }
        }
    }


    /// <summary>
    /// Retrieves timed AFKs that occurred before the specified time.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <returns>A collection of timed AFKs.</returns>
    private IEnumerable<Database.Models.Afk> GetAfkBeforeAsync(DateTime now)
    {
        using var uow = db.GetDbContext();
        IEnumerable<Database.Models.Afk> afks;

        // Check if the database provider is Npgsql (PostgreSQL)
        if (uow.Database.IsNpgsql())
        {
            // Retrieve timed AFKs using LINQ to DB query because Npgsql is ⭐ special ⭐
            afks = uow.Afk
                .ToLinqToDB()
                .Where(x =>
                    (int)(x.GuildId / (ulong)Math.Pow(2, 22) % (ulong)creds.TotalShards) == client.ShardId &&
                    x.When < now && x.WasTimed == 1)
                .ToList();
        }
        else // For other database providers
        {
            // Retrieve timed AFKs using raw SQL query
            afks = uow.Afk
                .FromSqlInterpolated(
                    $"select * from AFK where ((GuildId >> 22) % {creds.TotalShards}) = {client.ShardId} and \"WasTimed\" = 1 and \"when\" < {now};")
                .ToList();
        }

        return afks;
    }


    /// <summary>
    /// Handles the completion of a timed AFK.
    /// </summary>
    /// <param name="afk">The timed AFK to be handled.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task TimedAfkFinished(Database.Models.Afk afk)
    {
        // Check if the user is still AFK
        if (!await IsAfk(afk.GuildId, afk.UserId))
        {
            // If not AFK, remove the timed AFK and return
            await RemoveAfk(afk);
            return;
        }

        // Reset the user's AFK status
        await AfkSet(afk.GuildId, afk.UserId, "", 0);

        // Retrieve the guild and user
        var guild = client.GetGuild(afk.GuildId);
        var user = guild.GetUser(afk.UserId);

        try
        {
            // Attempt to remove "[AFK]" from the user's nickname
            await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", "")).ConfigureAwait(false);
        }
        catch
        {
            // Ignore any errors
        }

        // Remove the timed AFK from the database
        await RemoveAfk(afk);
    }


    /// <summary>
    /// Caches the latest AFK entries.
    /// </summary>
    /// <param name="latestAfks">A dictionary containing the latest AFK entries per user per guild.</param>
    private async Task CacheLatestAfks(
        ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, Database.Models.Afk>> latestAfks)
    {
        foreach (var guild in latestAfks)
        {
            foreach (var userAfk in guild.Value)
            {
                // Clear the AFK entry if the message is empty or cache the AFK entry
                if (string.IsNullOrEmpty(userAfk.Value.Message))
                    await cache.ClearAfk(guild.Key, userAfk.Key);
                else
                    await cache.CacheAfk(guild.Key, userAfk.Key, userAfk.Value);
            }
        }
    }


    /// <summary>
    /// Handles the event when a user starts typing.
    /// </summary>
    /// <param name="user">The user who started typing.</param>
    /// <param name="chan">The channel where the user started typing.</param>
    private async Task UserTyping(Cacheable<IUser, ulong> user, Cacheable<IMessageChannel, ulong> chan)
    {
        if (user.Value is IGuildUser use)
        {
            // Check if the guild has AFK type 2 or 4 and if the user is AFK
            if (await GetAfkType(use.GuildId) is 3 or 4 && await IsAfk(use.Guild.Id, use.Id))
            {
                var afkEntry = await GetAfk(use.Guild.Id, user.Id);
                // Check if the AFK entry was set less than the AFK timeout and was not timed
                if (afkEntry.DateAdded != null &&
                    afkEntry.DateAdded.Value.ToLocalTime() <
                    DateTime.Now.AddSeconds(-await GetAfkTimeout(use.GuildId)) &&
                    afkEntry.WasTimed == 0)
                {
                    // Disable the user's AFK status
                    await AfkSet(use.Guild.Id, use.Id, "", 0).ConfigureAwait(false);
                    // Send a message in the channel indicating the user is back from AFK
                    var msg = await chan.Value
                        .SendMessageAsync(
                            $"Welcome back {user.Value.Mention}! I noticed you typing so I disabled your AFK.")
                        .ConfigureAwait(false);
                    try
                    {
                        // Remove the AFK tag from the user's nickname
                        await use.ModifyAsync(x => x.Nickname = use.Nickname.Replace("[AFK]", ""))
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignored
                    }

                    // Delete the message after 5 seconds
                    msg.DeleteAfter(5);
                }
            }
        }
    }


    /// <summary>
    /// Handles the event when a message is received, and processes AFK-related actions.
    /// </summary>
    /// <param name="msg">The received message.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task MessageReceived(SocketMessage msg)
    {
        try
        {
            // Ignore messages from bots
            if (msg.Author.IsBot)
                return;

            // Process messages from guild users
            if (msg.Author is IGuildUser user)
            {
                // Retrieve the user's AFK status
                var afk = await GetAfk(user.GuildId, user.Id);

                // Check if the guild has AFK type 3 or 4 and if the user is AFK
                if (await GetAfkType(user.Guild.Id) is 2 or 4)
                {
                    if (await IsAfk(user.Guild.Id, user.Id))
                    {
                        // Check if the AFK entry was set less than the AFK timeout and was not timed
                        if (afk.DateAdded != null &&
                            afk.DateAdded.Value.ToLocalTime() <
                            DateTime.Now.AddSeconds(-await GetAfkTimeout(user.GuildId)) && afk.WasTimed == 0)
                        {
                            // Disable the user's AFK status
                            await AfkSet(user.Guild.Id, user.Id, "", 0).ConfigureAwait(false);

                            // Send a message in the channel indicating the user is back from AFK
                            var ms = await msg.Channel
                                .SendMessageAsync($"Welcome back {user.Mention}, I have disabled your AFK for you.")
                                .ConfigureAwait(false);
                            ms.DeleteAfter(5);

                            try
                            {
                                // Remove the AFK tag from the user's nickname
                                await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", ""))
                                    .ConfigureAwait(false);
                            }
                            catch
                            {
                                // Ignore any errors
                            }

                            return;
                        }
                    }
                }

                // Process messages that mention other users and are not from bots
                if (msg.MentionedUsers.Count > 0 && !msg.Author.IsBot)
                {
                    var prefix = await guildSettings.GetPrefix(user.Guild);

                    // Ignore AFK-related commands
                    if (msg.Content.Contains($"{prefix}afkremove") || msg.Content.Contains($"{prefix}afkrm") ||
                        msg.Content.Contains($"{prefix}afk"))
                    {
                        return;
                    }

                    // Check if the channel is not disabled for AFK
                    if (await GetDisabledAfkChannels(user.GuildId) is not "0" and not null)
                    {
                        var chans = await GetDisabledAfkChannels(user.GuildId);
                        var e = chans.Split(",");
                        if (e.Contains(msg.Channel.Id.ToString())) return;
                    }

                    // Process messages that mention a guild user
                    if (msg.MentionedUsers.FirstOrDefault() is not IGuildUser mentuser) return;
                    if (await IsAfk(user.Guild.Id, mentuser.Id))
                    {
                        try
                        {
                            // Remove the AFK tag from the user's nickname
                            await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", ""))
                                .ConfigureAwait(false);
                        }
                        catch
                        {
                            // Ignore any errors
                        }

                        // Retrieve the custom AFK message
                        var customafkmessage = await GetCustomAfkMessage(user.Guild.Id);
                        var afkdel = await GetAfkDel(user.Guild.Id);

                        // Check if there is a custom AFK message
                        if (customafkmessage is null or "-")
                        {
                            // Send a default AFK message
                            var a = await msg.Channel.SendMessageAsync(embed: new EmbedBuilder()
                                    .WithAuthor(eab =>
                                        eab.WithName($"{mentuser} is currently away")
                                            .WithIconUrl(mentuser.GetAvatarUrl()))
                                    .WithDescription(afk.Message
                                        .Truncate(await GetAfkLength(user.Guild.Id)))
                                    .WithFooter(new EmbedFooterBuilder
                                    {
                                        Text =
                                            // ReSharper disable once PossibleInvalidOperationException
                                            $"AFK for {(DateTime.UtcNow - afk.DateAdded.Value).Humanize()}"
                                    }).WithOkColor().Build(),
                                components: config.Data.ShowInviteButton
                                    ? new ComponentBuilder()
                                        .WithButton(style: ButtonStyle.Link,
                                            url:
                                            "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                                            label: "Invite Me!",
                                            emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build()
                                    : null).ConfigureAwait(false);
                            if (afkdel > 0)
                                a.DeleteAfter(afkdel);
                            return;
                        }

                        // Replace placeholders in the custom AFK message
                        var replacer = new ReplacementBuilder()
                            .WithOverride("%afk.message%",
                                () => afk.Message.SanitizeMentions(true)
                                    .Truncate(GetAfkLength(user.GuildId).GetAwaiter().GetResult()))
                            .WithOverride("%afk.user%", () => mentuser.ToString())
                            .WithOverride("%afk.user.mention%", () => mentuser.Mention)
                            .WithOverride("%afk.user.avatar%", () => mentuser.GetAvatarUrl(size: 2048))
                            .WithOverride("%afk.user.id%", () => mentuser.Id.ToString())
                            .WithOverride("%afk.triggeruser%", () => msg.Author.ToString().EscapeWeirdStuff())
                            .WithOverride("%afk.triggeruser.avatar%", () => msg.Author.RealAvatarUrl().ToString())
                            .WithOverride("%afk.triggeruser.id%", () => msg.Author.Id.ToString())
                            .WithOverride("%afk.triggeruser.mention%", () => msg.Author.Mention)
                            .WithOverride("%afk.time%", () =>
                                // ReSharper disable once PossibleInvalidOperationException
                                $"{(DateTime.UtcNow - afk.DateAdded.Value).Humanize()}")
                            .Build();

                        // Parse the custom AFK message into an embed
                        var ebe = SmartEmbed.TryParse(replacer.Replace(customafkmessage),
                            ((ITextChannel)msg.Channel)?.GuildId, out var embed, out var plainText,
                            out var components);
                        if (!ebe)
                        {
                            // Send the custom AFK message as plain text
                            var a = await msg.Channel
                                .SendMessageAsync(replacer.Replace(customafkmessage).SanitizeMentions(true))
                                .ConfigureAwait(false);
                            if (afkdel != 0)
                                a.DeleteAfter(afkdel);
                            return;
                        }

                        // Send the custom AFK message as an embed
                        var b = await msg.Channel
                            .SendMessageAsync(plainText, embeds: embed, components: components?.Build())
                            .ConfigureAwait(false);
                        if (afkdel > 0)
                            b.DeleteAfter(afkdel);
                    }
                }
            }
        }
        catch (Exception e)
        {
            // Log any errors that occur during the handling of the message
            Log.Error("Error in AfkHandler: " + e);
        }
    }

    /// <summary>
    /// Retrieves the AFK entry for the specified user in the guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>The AFK entry for the user if found; otherwise, null.</returns>
    public async Task<Database.Models.Afk?> GetAfk(ulong guildId, ulong userId)
    {
        return await cache.RetrieveAfk(guildId, userId);
    }

    /// <summary>
    /// Gets a list of AFK users in the specified guild.
    /// </summary>
    /// <param name="guild">The guild to get AFK users from.</param>
    /// <returns>A list of AFK users in the guild.</returns>
    public async Task<List<IGuildUser>> GetAfkUsers(IGuild guild)
    {
        var afkUsers = new List<IGuildUser>();
        var users = await guild.GetUsersAsync();

        foreach (var user in users)
        {
            if (await IsAfk(guild.Id, user.Id).ConfigureAwait(false))
                afkUsers.Add(user);
        }

        return afkUsers;
    }

    /// <summary>
    /// Sets a custom AFK message for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the custom AFK message for.</param>
    /// <param name="afkMessage">The custom AFK message to set.</param>
    public async Task SetCustomAfkMessage(IGuild guild, string afkMessage)
    {
        await using var uow = db.GetDbContext();
        var guildConfig = await uow.ForGuildId(guild.Id, set => set);
        guildConfig.AfkMessage = afkMessage;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
    }

    /// <summary>
    /// Checks if the specified user is AFK in the guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>True if the user is AFK in the guild; otherwise, false.</returns>
    public async Task<bool> IsAfk(ulong guildId, ulong userId)
    {
        var afkMessage = await cache.RetrieveAfk(guildId, userId);
        return afkMessage is not null;
    }

    /// <summary>
    /// Handles the event when a message is updated.
    /// </summary>
    /// <param name="msg">The updated message.</param>
    /// <param name="msg2">The updated message as a SocketMessage.</param>
    /// <param name="channel">The channel where the message was updated.</param>
    private async Task MessageUpdated(Cacheable<IMessage, ulong> msg, SocketMessage msg2, ISocketMessageChannel channel)
    {
        var message = await msg.GetOrDownloadAsync().ConfigureAwait(false);
        if (message is null)
            return;

        var originalDateUnspecified = message.Timestamp.ToUniversalTime();
        var originalDate = new DateTime(originalDateUnspecified.Ticks, DateTimeKind.Unspecified);
        if (DateTime.UtcNow > originalDate.Add(TimeSpan.FromMinutes(30)))
            return;

        await MessageReceived(msg2).ConfigureAwait(false);
    }


    /// <summary>
    /// Sets the AFK type for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK type for.</param>
    /// <param name="num">The AFK type to set.</param>
    public async Task AfkTypeSet(IGuild guild, int num)
    {
        await using var uow = db.GetDbContext();
        var guildConfig = await uow.ForGuildId(guild.Id, set => set);
        guildConfig.AfkType = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
    }

    /// <summary>
    /// Sets the AFK deletion for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK deletion for.</param>
    /// <param name="inputNum">The input number representing AFK deletion.</param>
    public async Task AfkDelSet(IGuild guild, int inputNum)
    {
        var num = inputNum.ToString();
        await using var uow = db.GetDbContext();
        var guildConfig = await uow.ForGuildId(guild.Id, set => set);
        guildConfig.AfkDel = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
    }

    /// <summary>
    /// Sets the AFK length for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK length for.</param>
    /// <param name="num">The AFK length to set.</param>
    public async Task AfkLengthSet(IGuild guild, int num)
    {
        await using var uow = db.GetDbContext();
        var guildConfig = await uow.ForGuildId(guild.Id, set => set);
        guildConfig.AfkLength = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
    }

    /// <summary>
    /// Sets the AFK timeout for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK timeout for.</param>
    /// <param name="num">The AFK timeout to set.</param>
    public async Task AfkTimeoutSet(IGuild guild, int num)
    {
        await using var uow = db.GetDbContext();
        var guildConfig = await uow.ForGuildId(guild.Id, set => set);
        guildConfig.AfkTimeout = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
    }

    /// <summary>
    /// Sets the AFK disabled channels for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK disabled channels for.</param>
    /// <param name="num">The AFK disabled channels to set.</param>
    public async Task AfkDisabledSet(IGuild guild, string num)
    {
        await using var uow = db.GetDbContext();
        var guildConfig = await uow.ForGuildId(guild.Id, set => set);
        guildConfig.AfkDisabledChannels = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
    }


    /// <summary>
    /// Retrieves the custom AFK message for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The custom AFK message.</returns>
    public async Task<string> GetCustomAfkMessage(ulong id) => (await guildSettings.GetGuildConfig(id)).AfkMessage;

    /// <summary>
    /// Retrieves the AFK deletion setting for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The AFK deletion setting.</returns>
    public async Task<int> GetAfkDel(ulong id)
    {
        var config = await guildSettings.GetGuildConfig(id);
        return int.TryParse(config.AfkDel, out var num) ? num : 0;
    }

    /// <summary>
    /// Retrieves the AFK type for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The AFK type.</returns>
    private async Task<int> GetAfkType(ulong id) => (await guildSettings.GetGuildConfig(id)).AfkType;

    /// <summary>
    /// Retrieves the AFK length for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The AFK length.</returns>
    public async Task<int> GetAfkLength(ulong id) => (await guildSettings.GetGuildConfig(id)).AfkLength;

    /// <summary>
    /// Retrieves the disabled AFK channels for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The disabled AFK channels.</returns>
    public async Task<string?> GetDisabledAfkChannels(ulong id) =>
        (await guildSettings.GetGuildConfig(id)).AfkDisabledChannels;

    /// <summary>
    /// Retrieves the AFK timeout for the specified guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The AFK timeout.</returns>
    private async Task<int> GetAfkTimeout(ulong id) => (await guildSettings.GetGuildConfig(id)).AfkTimeout;

    /// <summary>
    /// Sets the AFK status for the specified user in the specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="message">The AFK message.</param>
    /// <param name="timed">Whether the AFK is timed.</param>
    /// <param name="when">The time when the AFK was set.</param>
    public async Task AfkSet(ulong guildId, ulong userId, string message, int timed, DateTime when = new())
    {
        var afk = new Database.Models.Afk
        {
            GuildId = guildId,
            UserId = userId,
            Message = message,
            WasTimed = timed,
            When = when
        };
        await using var uow = db.GetDbContext();
        uow.Afk.Update(afk);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        if (string.IsNullOrEmpty(message))
            await cache.ClearAfk(guildId, userId);
        else
            await cache.CacheAfk(guildId, userId, afk);
    }

    /// <summary>
    /// Removes the specified AFK entry.
    /// </summary>
    /// <param name="afk">The AFK entry to remove.</param>
    private async Task RemoveAfk(Database.Models.Afk afk)
    {
        await cache.ClearAfk(afk.GuildId, afk.UserId);

        await using var uow = db.GetDbContext();
        uow.Afk.Remove(afk);
        await uow.SaveChangesAsync();
    }
}