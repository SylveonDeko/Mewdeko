using System.Threading;
using DataModel;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Invite Count Service
/// </summary>
public class InviteCountService : INService, IReadyExecutor
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, IInviteMetadata>> guildInvites = new();
    private readonly ConcurrentDictionary<ulong, InviteCountSetting> inviteCountSettings = new();
    private readonly ILogger<InviteCountService> logger;

    /// <summary>
    ///     Service for counting invites
    /// </summary>
    /// <param name="handler"></param>
    /// <param name="dbFactory"></param>
    /// <param name="client"></param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public InviteCountService(EventHandler handler, IDataConnectionFactory dbFactory, DiscordShardedClient client,
        ILogger<InviteCountService> logger)
    {
        this.dbFactory = dbFactory;
        this.client = client;
        this.logger = logger;

        handler.Subscribe("JoinedGuild", "InviteCountService", UpdateGuildInvites);
        handler.Subscribe("UserJoined", "InviteCountService", OnUserJoined);
        handler.Subscribe("UserLeft", "InviteCountService", OnUserLeft);
        handler.Subscribe("InviteCreated", "InviteCountService", OnInviteCreated);
        handler.Subscribe("InviteDeleted", "InviteCountService", OnInviteDeleted);

        // Clean up when leaving guilds
        this.client.LeftGuild += OnLeftGuild;
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        logger.LogInformation("Starting InviteCountService - Lazy loading enabled");

        // Load settings into memory cache (relatively small)
        var guildIds = client.Guilds.Select(g => g.Id).ToHashSet();
        await using var uow = await dbFactory.CreateConnectionAsync();

        var allSettings = (await uow.InviteCountSettings
                .Where(s => guildIds.Contains(s.GuildId))
                .ToListAsync())
            .GroupBy(s => s.GuildId)
            .ToDictionary(g => g.Key, g => g.First());

        // Initialize settings for all guilds (small data, needed for quick lookups)
        foreach (var guildId in guildIds)
        {
            if (allSettings.TryGetValue(guildId, out var settings))
            {
                inviteCountSettings[guildId] = settings;
            }
            else
            {
                // Create default settings but don't save yet - will be saved when first accessed
                inviteCountSettings[guildId] = new InviteCountSetting
                {
                    GuildId = guildId, RemoveInviteOnLeave = true, IsEnabled = true
                };
            }
        }

        logger.LogInformation("Loaded invite settings for {Count} guilds", inviteCountSettings.Count);

        _ = Task.Run(async () =>
        {
            try
            {
                var enabledGuilds = client.Guilds
                    .Where(g => inviteCountSettings.TryGetValue(g.Id, out var s) && s.IsEnabled)
                    .Where(g => g.CurrentUser?.GuildPermissions.Has(GuildPermission.ManageGuild) == true)
                    .ToList();

                logger.LogInformation("Starting background invite initialization for {Count} enabled guilds",
                    enabledGuilds.Count);

                // Process guilds in batches with rate limiting
                var semaphore = new SemaphoreSlim(5); // More conservative than 10
                var batchSize = 10;

                for (var i = 0; i < enabledGuilds.Count; i += batchSize)
                {
                    var batch = enabledGuilds.Skip(i).Take(batchSize).ToList();
                    var tasks = batch.Select(guild => ProcessGuildWithRateLimiting(guild, semaphore)).ToList();
                    await Task.WhenAll(tasks);

                    // Add delay between batches to avoid rate limits
                    if (i + batchSize < enabledGuilds.Count)
                    {
                        await Task.Delay(5000); // 5 second delay between batches
                    }
                }

                logger.LogInformation("Completed background invite initialization");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during background invite initialization");
            }
        });

        // Background cleanup of orphaned data
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5)); // Wait 5 minutes after startup

                var currentGuildIds = client.Guilds.Select(g => g.Id).ToHashSet();
                await using var db = await dbFactory.CreateConnectionAsync();

                // Clean up settings for guilds we're no longer in
                var orphanedSettings = await db.InviteCountSettings
                    .Where(s => !currentGuildIds.Contains(s.GuildId))
                    .CountAsync();

                if (orphanedSettings > 0)
                {
                    await db.InviteCountSettings
                        .Where(s => !currentGuildIds.Contains(s.GuildId))
                        .DeleteAsync();

                    logger.LogInformation("Cleaned up {Count} orphaned invite settings", orphanedSettings);
                }

                // Clean up invite counts for guilds we're no longer in
                var orphanedCounts = await db.InviteCounts
                    .Where(c => !currentGuildIds.Contains(c.GuildId))
                    .CountAsync();

                if (orphanedCounts > 0)
                {
                    await db.InviteCounts
                        .Where(c => !currentGuildIds.Contains(c.GuildId))
                        .DeleteAsync();

                    logger.LogInformation("Cleaned up {Count} orphaned invite counts", orphanedCounts);
                }

                // Clean up invited-by records for guilds we're no longer in
                var orphanedInvitedBy = await db.InvitedBies
                    .Where(i => !currentGuildIds.Contains(i.GuildId))
                    .CountAsync();

                if (orphanedInvitedBy > 0)
                {
                    await db.InvitedBies
                        .Where(i => !currentGuildIds.Contains(i.GuildId))
                        .DeleteAsync();

                    logger.LogInformation("Cleaned up {Count} orphaned invited-by records", orphanedInvitedBy);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during invite data cleanup");
            }
        });

        logger.LogInformation("InviteCountService ready - invites load in background");
    }

    private async Task OnUserLeft(IGuild guild, IUser user)
    {
        var settings = await GetInviteCountSettingsAsync(guild.Id);
        if (!settings.IsEnabled || !settings.RemoveInviteOnLeave) return;

        await using var uow = await dbFactory.CreateConnectionAsync();

        var invitedBy = await uow.InvitedBies
            .FirstOrDefaultAsync(x => x.UserId == user.Id && x.GuildId == guild.Id);

        if (invitedBy != null)
        {
            var inviterCount = await uow.InviteCounts
                .FirstOrDefaultAsync(x => x.UserId == invitedBy.InviterId && x.GuildId == guild.Id);

            if (inviterCount is { Count: > 0 })
            {
                inviterCount.Count--;
            }

            // Remove the InvitedBy record
            await uow.DeleteAsync(invitedBy);
        }
    }

    private async Task OnLeftGuild(SocketGuild guild)
    {
        try
        {
            // Remove from in-memory caches
            inviteCountSettings.TryRemove(guild.Id, out _);
            guildInvites.TryRemove(guild.Id, out _);

            logger.LogInformation("Cleaned up invite tracking for left guild {GuildId}", guild.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up invite tracking for left guild {GuildId}", guild.Id);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    ///     Gets invite count settings for the guild
    /// </summary>
    /// <param name="guildId"></param>
    /// <returns></returns>
    public async Task<InviteCountSetting> GetInviteCountSettingsAsync(ulong guildId)
    {
        if (inviteCountSettings.TryGetValue(guildId, out var cachedSettings))
        {
            return cachedSettings;
        }

        await using var uow = await dbFactory.CreateConnectionAsync();
        var settings = await uow.InviteCountSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (settings == null)
        {
            settings = new InviteCountSetting
            {
                GuildId = guildId, RemoveInviteOnLeave = false, MinAccountAge = TimeSpan.Zero, IsEnabled = true
            };
            await uow.InsertAsync(settings);
        }

        inviteCountSettings[guildId] = settings;
        return settings;
    }

    private async Task UpdateInviteCountSettingsAsync(ulong guildId, Action<InviteCountSetting> updateAction)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var settings = await uow.InviteCountSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (settings == null)
        {
            settings = new InviteCountSetting
            {
                GuildId = guildId
            };
            await uow.InsertAsync(settings);
        }

        updateAction(settings);
        await uow.UpdateAsync(settings);


        inviteCountSettings[guildId] = settings;
    }

    /// <summary>
    ///     Sets whether invite tracking is enabled or disabled
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="isEnabled"></param>
    /// <returns></returns>
    public async Task<bool> SetInviteTrackingEnabledAsync(ulong guildId, bool isEnabled)
    {
        await UpdateInviteCountSettingsAsync(guildId, settings => settings.IsEnabled = isEnabled);
        return isEnabled;
    }

    /// <summary>
    ///     Sets whether invite count gets removed when a user leaves
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="removeOnLeave"></param>
    /// <returns></returns>
    public async Task<bool> SetRemoveInviteOnLeaveAsync(ulong guildId, bool removeOnLeave)
    {
        await UpdateInviteCountSettingsAsync(guildId, settings => settings.RemoveInviteOnLeave = removeOnLeave);
        return removeOnLeave;
    }

    /// <summary>
    ///     Sets the minimum account age for an invite to get counted
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="minAge"></param>
    /// <returns></returns>
    public async Task<TimeSpan> SetMinAccountAgeAsync(ulong guildId, TimeSpan minAge)
    {
        await UpdateInviteCountSettingsAsync(guildId, settings => settings.MinAccountAge = minAge);
        return minAge;
    }


    private async Task OnUserJoined(IGuildUser user)
    {
        if (inviteCountSettings.TryGetValue(user.Guild.Id, out var settings))
            if (!settings.IsEnabled)
                return;
        var guild = user.Guild;
        var curUser = await guild.GetCurrentUserAsync();
        if (!curUser.GuildPermissions.Has(GuildPermission.ManageGuild))
            return;

        // Ensure invites are loaded for this guild
        if (!guildInvites.ContainsKey(guild.Id))
        {
            await UpdateGuildInvites(guild);
        }

        var newInvites = await guild.GetInvitesAsync();
        var usedInvite = FindUsedInvite(guild.Id, newInvites);

        if (usedInvite != null)
        {
            await UpdateInviteCount(usedInvite.Inviter.Id, guild.Id);
            await UpdateInvitedBy(user.Id, usedInvite.Inviter.Id, guild.Id);
        }

        await UpdateGuildInvites(user.Guild);
    }

    private Task OnInviteCreated(IInvite invite)
    {
        if (!this.guildInvites.TryGetValue(invite.Guild.Id, out var concurrentDictionary))
        {
            concurrentDictionary = new ConcurrentDictionary<string, IInviteMetadata>();
            this.guildInvites[invite.Guild.Id] = concurrentDictionary;
        }

        concurrentDictionary[invite.Code] = invite as IInviteMetadata;
        return Task.CompletedTask;
    }

    private Task OnInviteDeleted(IGuildChannel channel, string code)
    {
        if (this.guildInvites.TryGetValue(channel.Guild.Id, out var invites))
        {
            invites.TryRemove(code, out _);
        }

        return Task.CompletedTask;
    }


    private async Task UpdateGuildInvites(IGuild guild)
    {
        try
        {
            var invites = await guild.GetInvitesAsync();
            guildInvites[guild.Id] = new ConcurrentDictionary<string, IInviteMetadata>(
                invites.ToDictionary(x => x.Code, x => x));

            logger.LogDebug("Updated {Count} invites for guild {GuildId}", invites.Count(), guild.Id);
        }
        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.MissingPermissions)
        {
            logger.LogWarning("Missing permissions to fetch invites for guild {GuildId}", guild.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update invites for guild {GuildId}", guild.Id);
        }
    }

    private IInvite? FindUsedInvite(ulong guildId, IEnumerable<IInviteMetadata> newInvites)
    {
        if (!guildInvites.TryGetValue(guildId, out var oldInvites))
            return null;

        foreach (var newInvite in newInvites)
        {
            if (!oldInvites.TryGetValue(newInvite.Code, out var oldInvite)) continue;
            if (newInvite.Uses > oldInvite.Uses)
                return newInvite;
        }

        return null;
    }

    private async Task UpdateInviteCount(ulong inviterId, ulong guildId)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var inviter = await uow.InviteCounts.FirstOrDefaultAsync(x => x.UserId == inviterId && x.GuildId == guildId);

        if (inviter == null)
        {
            inviter = new InviteCount
            {
                UserId = inviterId, GuildId = guildId, Count = 1
            };
            await uow.InsertAsync(inviter);
        }
        else
        {
            inviter.Count++;
        }
    }

    private async Task UpdateInvitedBy(ulong userId, ulong inviterId, ulong guildId)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var invitedUser = new InvitedBy
        {
            UserId = userId, InviterId = inviterId, GuildId = guildId
        };

        await uow.InsertAsync(invitedUser);
    }

    /// <summary>
    ///     Gets the invite count for a user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="guildId"></param>
    /// <returns></returns>
    public async Task<int> GetInviteCount(ulong userId, ulong guildId)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var inviteCount = await uow.InviteCounts
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .Select(x => x.Count)
            .FirstOrDefaultAsync();

        return inviteCount;
    }

    /// <summary>
    ///     Gets who invited a user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="guild"></param>
    /// <returns></returns>
    public async Task<IUser?> GetInviter(ulong userId, IGuild guild)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var inviterId = await uow.InvitedBies
            .Where(x => x.UserId == userId && x.GuildId == guild.Id)
            .Select(x => x.InviterId)
            .FirstOrDefaultAsync();

        return inviterId != 0 ? await guild.GetUserAsync(inviterId) : null;
    }

    /// <summary>
    ///     Gets all users invited by a user
    /// </summary>
    /// <param name="inviterId"></param>
    /// <param name="guild"></param>
    /// <returns></returns>
    public async Task<List<IUser>> GetInvitedUsers(ulong inviterId, IGuild guild)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var invitedUserIds = await uow.InvitedBies
            .Where(x => x.InviterId == inviterId && x.GuildId == guild.Id)
            .Select(x => x.UserId)
            .ToListAsync();

        var invitedUsers = new List<IUser>();
        foreach (var userId in invitedUserIds)
        {
            var user = await guild.GetUserAsync(userId);
            if (user != null)
                invitedUsers.Add(user);
        }

        return invitedUsers;
    }

    /// <summary>
    ///     Gets the invite leaderboard
    /// </summary>
    /// <param name="guild"></param>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    public async Task<List<(ulong UserId, string Username, int InviteCount)>> GetInviteLeaderboardAsync(IGuild guild,
        int page = 1, int pageSize = 10)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var leaderboard = await uow.InviteCounts
            .Where(x => x.GuildId == guild.Id)
            .OrderByDescending(x => x.Count)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.UserId, x.Count
            })
            .ToListAsync();


        var result = new List<(ulong UserId, string Username, int InviteCount)>();
        foreach (var entry in leaderboard)
        {
            var user = await guild.GetUserAsync(entry.UserId);
            var username = user?.Username ?? "Unknown User";
            result.Add((entry.UserId, username, entry.Count));
        }

        return result;
    }

    private async Task ProcessGuildWithRateLimiting(IGuild guild, SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        try
        {
            await UpdateGuildInvites(guild);
            await Task.Delay(1000); // 1 second delay between guilds
        }
        finally
        {
            semaphore.Release();
        }
    }
}