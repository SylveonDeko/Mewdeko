
using System.Threading;
using Humanizer;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Settings;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ZiggyCreatures.Caching.Fusion;
using System.Timers;
using Mewdeko.Database.DbContextStuff;
using Timer = System.Threading.Timer;

namespace Mewdeko.Modules.Afk.Services;

/// <summary>
/// Handles AFK-related commands and events. Was hell to make.
/// </summary>
public class AfkService : INService, IReadyExecutor
{
    private readonly IFusionCache cache;
    private readonly DiscordShardedClient client;
    private readonly BotConfigService config;
    private readonly DbContextProvider dbProvider;
    private readonly GuildSettingsService guildSettings;
    private readonly ConcurrentDictionary<(ulong, ulong), Timer> afkTimers = new();


    /// <summary>
    /// Initializes a new instance of the AfkService class.
    /// </summary>
    /// <param name="db">The database service.</param>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="cache">The FusionCache instance.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="config">The bot configuration service.</param>
    public AfkService(
        DbContextProvider dbProvider,
        DiscordShardedClient client,
        IFusionCache cache,
        GuildSettingsService guildSettings,
        EventHandler eventHandler,
        BotConfigService config)
    {
        this.cache = cache;
        this.guildSettings = guildSettings;
        this.config = config;
        this.dbProvider = dbProvider;
        this.client = client;
        eventHandler.MessageReceived += MessageReceived;
        eventHandler.MessageUpdated += MessageUpdated;
        eventHandler.UserIsTyping += UserTyping;
        _ = InitializeTimedAfksAsync();
    }

    /// <summary>
    ///     Handles actions to be performed when the bot is ready.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task OnReadyAsync()
    {
        Log.Information($"Starting {this.GetType()} Cache");
        // Retrieve all AFK entries from the database
        await using var dbContext = await dbProvider.GetContextAsync();
        var allafk = await dbContext.Afk.AsNoTracking().OrderByDescending(afk => afk.DateAdded).ToListAsyncEF();

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
        Environment.SetEnvironmentVariable($"AFK_CACHED", "1");
        Log.Information("AFK Cached");
    }

    /// <summary>
    /// Initializes all timed AFKs and sets timers.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task InitializeTimedAfksAsync()
    {
        var now = DateTime.UtcNow;
        var afks = await GetAfkBeforeAsync(now);

        foreach (var afk in afks)
        {
            ScheduleTimedAfk(afk);
        }
    }

    /// <summary>
    /// Schedules a timed AFK by setting a timer.
    /// </summary>
    /// <param name="afk">The AFK entry to be scheduled.</param>
    private void ScheduleTimedAfk(Database.Models.Afk afk)
    {
        var timeToGo = afk.When.Value - DateTime.UtcNow;
        if (timeToGo <= TimeSpan.Zero)
        {
            timeToGo = TimeSpan.Zero;
        }

        var timer = new Timer(async _ => await TimedAfkFinished(afk), null, timeToGo, Timeout.InfiniteTimeSpan);
        afkTimers[(afk.GuildId, afk.UserId)] = timer;
    }


    /// <summary>
    /// Retrieves timed AFKs that occurred before the specified time.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <returns>A collection of timed AFKs.</returns>
    private async Task<IEnumerable<Database.Models.Afk>> GetAfkBeforeAsync(DateTime now)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        IEnumerable<Database.Models.Afk> afks =
            await dbContext.Afk
                .ToLinqToDB()
                .Where(x => x.When < now && x.WasTimed)
                .ToListAsyncEF();

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
            afkTimers.TryRemove((afk.GuildId, afk.UserId), out var timer);
            timer?.Dispose();
            return;
        }

        // Reset the user's AFK status
        await AfkSet(afk.GuildId, afk.UserId, "", false);

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
        afkTimers.TryRemove((afk.GuildId, afk.UserId), out var timer2);
        timer2?.Dispose();
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
                    await cache.RemoveAsync($"{guild.Key}:{userAfk.Key}");
                else
                    await cache.SetAsync($"{guild.Key}:{userAfk.Key}", userAfk.Value);
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
                    afkEntry.WasTimed)
                {
                    // Disable the user's AFK status
                    await AfkSet(use.Guild.Id, use.Id, "", false).ConfigureAwait(false);
                    //Send a message in the channel indicating the user is back from AFK
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
                            DateTime.Now.AddSeconds(-await GetAfkTimeout(user.GuildId)) && !afk.WasTimed)
                        {
                            // Disable the user's AFK status
                            await AfkSet(user.Guild.Id, user.Id, "", false).ConfigureAwait(false);

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
        return await cache.GetOrDefaultAsync<Database.Models.Afk>($"{guildId}:{userId}");
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
        await using var dbContext = await dbProvider.GetContextAsync();
        var guildConfig = await dbContext.ForGuildId(guild.Id, set => set);
        guildConfig.AfkMessage = afkMessage;
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
        var afkMessage = await cache.GetOrDefaultAsync<Database.Models.Afk>($"{guildId}:{userId}");
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
        await using var dbContext = await dbProvider.GetContextAsync();
        var guildConfig = await dbContext.ForGuildId(guild.Id, set => set);
        guildConfig.AfkType = num;
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
        await using var dbContext = await dbProvider.GetContextAsync();
        var guildConfig = await dbContext.ForGuildId(guild.Id, set => set);
        guildConfig.AfkDel = num;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
    }

    /// <summary>
    /// Sets the AFK length for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK length for.</param>
    /// <param name="num">The AFK length to set.</param>
    public async Task AfkLengthSet(IGuild guild, int num)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var guildConfig = await dbContext.ForGuildId(guild.Id, set => set);
        guildConfig.AfkLength = num;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
    }

    /// <summary>
    /// Sets the AFK timeout for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK timeout for.</param>
    /// <param name="num">The AFK timeout to set.</param>
    public async Task AfkTimeoutSet(IGuild guild, int num)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var guildConfig = await dbContext.ForGuildId(guild.Id, set => set);
        guildConfig.AfkTimeout = num;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
    }

    /// <summary>
    /// Sets the AFK disabled channels for the guild.
    /// </summary>
    /// <param name="guild">The guild to set the AFK disabled channels for.</param>
    /// <param name="num">The AFK disabled channels to set.</param>
    public async Task AfkDisabledSet(IGuild guild, string num)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var guildConfig = await dbContext.ForGuildId(guild.Id, set => set);
        guildConfig.AfkDisabledChannels = num;
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
    /// Sets or removes the AFK status for the specified user in the specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="message">The AFK message. If empty, removes all AFK statuses for the user.</param>
    /// <param name="timed">Whether the AFK is timed.</param>
    /// <param name="when">The time when the AFK was set.</param>
    public async Task AfkSet(ulong guildId, ulong userId, string message, bool timed = false, DateTime when = new())
    {
        // Remove any existing timer for this user's AFK status in this guild
        if (afkTimers.TryRemove((guildId, userId), out var existingTimer))
        {
            await existingTimer.DisposeAsync();
        }

        await using var dbContext = await dbProvider.GetContextAsync();

        // Remove all existing AFK entries for this user in this guild
        var existingAfks = await dbContext.Afk
            .Where(a => a.GuildId == guildId && a.UserId == userId)
            .ToListAsync();

        var anyAfks = dbContext.Afk.Any(a => a.UserId == userId);

        if (existingAfks.Count!=0)
        {
            dbContext.Afk.RemoveRange(existingAfks);
        }

        if (string.IsNullOrEmpty(message))
        {
            // If message is empty, just remove the AFK status
            await dbContext.SaveChangesAsync();
            await cache.RemoveAsync($"{guildId}:{userId}");
        }
        else
        {
            // Create and add new AFK entry
            var newAfk = new Database.Models.Afk
            {
                GuildId = guildId,
                UserId = userId,
                Message = message,
                WasTimed = timed,
                When = when == default ? DateTime.UtcNow : when
            };

            dbContext.Afk.Add(newAfk);
            await dbContext.SaveChangesAsync();

            // Update cache
            await cache.SetAsync($"{guildId}:{userId}", newAfk);

            // Schedule timed AFK if necessary
            if (timed)
            {
                ScheduleTimedAfk(newAfk);
            }
        }
    }

    /// <summary>
    /// Removes the specified AFK entry.
    /// </summary>
    /// <param name="afk">The AFK entry to remove.</param>
    private async Task RemoveAfk(Database.Models.Afk afk)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        await cache.RemoveAsync($"{afk.GuildId}:{afk.UserId}");
        dbContext.Afk.Remove(afk);
        await dbContext.SaveChangesAsync();
        var exists = afkTimers.TryRemove((afk.GuildId, afk.UserId), out var timer);
        if (exists)
            await timer.DisposeAsync();
    }
}