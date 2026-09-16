using System.Text;
using System.Threading;
using DataModel;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Extensions;
using Mewdeko.Services.Analytics;
using Mewdeko.Services.Strings;
using Npgsql;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Tracks who invites whom. Every witnessed join is attributed to an invite code, the vanity URL, an OAuth bot add
///     or left unknown, run through fake detection, and credited to an inviter as a regular, fake or (later) left
///     invite. Staff can adjust totals with bonus invites, label codes, blacklist inviters and hide members from the
///     leaderboard.
/// </summary>
public class InviteCountService : INService, IReadyExecutor
{
    /// <summary>
    ///     How long a join attribution is kept for greet placeholders and the log channel to pick up.
    /// </summary>
    private static readonly TimeSpan JoinResultLifetime = TimeSpan.FromMinutes(5);

    private readonly DiscordShardedClient client;
    private readonly IAnalyticsCollector collector;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, IInviteMetadata>> guildInvites = new();
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> guildJoinLocks = new();
    private readonly ConcurrentDictionary<ulong, InviteCountSetting> inviteCountSettings = new();

    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), (InviteJoinResult Result, DateTime At)>
        joinResults = new();

    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), TaskCompletionSource<InviteJoinResult?>>
        joinWaiters = new();

    private readonly ILogger<InviteCountService> logger;
    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, int>> pendingUses = new();
    private readonly GeneratedBotStrings strings;
    private readonly ConcurrentDictionary<ulong, int> vanityUses = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="InviteCountService" /> class.
    /// </summary>
    /// <param name="handler">The event handler.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="collector">The analytics collector.</param>
    /// <param name="strings">Localized strings for log channel embeds.</param>
    public InviteCountService(EventHandler handler, IDataConnectionFactory dbFactory, DiscordShardedClient client,
        ILogger<InviteCountService> logger, IAnalyticsCollector collector, GeneratedBotStrings strings)
    {
        this.dbFactory = dbFactory;
        this.client = client;
        this.logger = logger;
        this.collector = collector;
        this.strings = strings;

        handler.Subscribe("JoinedGuild", "InviteCountService", UpdateGuildInvites);
        handler.Subscribe("UserJoined", "InviteCountService", OnUserJoined);
        handler.Subscribe("UserLeft", "InviteCountService", OnUserLeft);
        handler.Subscribe("InviteCreated", "InviteCountService", OnInviteCreated);
        handler.Subscribe("InviteDeleted", "InviteCountService", OnInviteDeleted);
        handler.Subscribe("LeftGuild", "InviteCountService", OnLeftGuild);
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        logger.LogInformation("Starting InviteCountService - Lazy loading enabled");

        var guildIds = client.Guilds.Select(g => g.Id).ToHashSet();
        await using var uow = await dbFactory.CreateConnectionAsync();

        var allSettings = (await uow.InviteCountSettings
                .Where(s => guildIds.Contains(s.GuildId))
                .ToListAsync())
            .GroupBy(s => s.GuildId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var guildId in guildIds)
        {
            inviteCountSettings[guildId] = allSettings.TryGetValue(guildId, out var settings)
                ? settings
                : NewDefaultSettings(guildId);
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

                var semaphore = new SemaphoreSlim(5);
                const int batchSize = 10;

                for (var i = 0; i < enabledGuilds.Count; i += batchSize)
                {
                    var batch = enabledGuilds.Skip(i).Take(batchSize).ToList();
                    await Task.WhenAll(batch.Select(guild => ProcessGuildWithRateLimiting(guild, semaphore)));

                    if (i + batchSize < enabledGuilds.Count)
                        await Task.Delay(5000);
                }

                logger.LogInformation("Completed background invite initialization");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during background invite initialization");
            }
        });

        _ = Task.Run(CleanupOrphanedDataAsync);
        _ = Task.Run(ExpireJoinResultsLoopAsync);

        logger.LogInformation("InviteCountService ready - invites load in background");
    }

    private static InviteCountSetting NewDefaultSettings(ulong guildId)
    {
        return new InviteCountSetting
        {
            GuildId = guildId,
            RemoveInviteOnLeave = true,
            IsEnabled = true,
            CountRejoins = true,
            MinAccountAge = TimeSpan.Zero
        };
    }

    private async Task CleanupOrphanedDataAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5));

            var currentGuildIds = client.Guilds.Select(g => g.Id).ToHashSet();
            await using var db = await dbFactory.CreateConnectionAsync();

            var settings = await db.InviteCountSettings.Where(s => !currentGuildIds.Contains(s.GuildId))
                .DeleteAsync();
            var counts = await db.InviteCounts.Where(c => !currentGuildIds.Contains(c.GuildId)).DeleteAsync();
            var invitedBy = await db.InvitedBies.Where(i => !currentGuildIds.Contains(i.GuildId)).DeleteAsync();
            var exclusions = await db.InviteTrackingExclusions.Where(i => !currentGuildIds.Contains(i.GuildId))
                .DeleteAsync();
            var labels = await db.InviteLabels.Where(i => !currentGuildIds.Contains(i.GuildId)).DeleteAsync();

            if (settings + counts + invitedBy + exclusions + labels > 0)
            {
                logger.LogInformation(
                    "Cleaned up orphaned invite data: {Settings} settings, {Counts} counts, {InvitedBy} joins, {Exclusions} exclusions, {Labels} labels",
                    settings, counts, invitedBy, exclusions, labels);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during invite data cleanup");
        }
    }

    private async Task ExpireJoinResultsLoopAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromMinutes(1));
            var cutoff = DateTime.UtcNow - JoinResultLifetime;
            foreach (var (key, value) in joinResults)
            {
                if (value.At < cutoff)
                    joinResults.TryRemove(key, out _);
            }
        }
    }

    #region Settings

    /// <summary>
    ///     Gets invite count settings for the guild, creating defaults on first access.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The settings.</returns>
    public async Task<InviteCountSetting> GetInviteCountSettingsAsync(ulong guildId)
    {
        if (inviteCountSettings.TryGetValue(guildId, out var cachedSettings))
            return cachedSettings;

        await using var uow = await dbFactory.CreateConnectionAsync();
        var settings = await uow.InviteCountSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (settings == null)
        {
            settings = NewDefaultSettings(guildId);
            settings.RemoveInviteOnLeave = false;
            settings = await InsertOrReadSettingsAsync(uow, settings);
        }

        inviteCountSettings[guildId] = settings;
        return settings;
    }

    private async Task<InviteCountSetting> UpdateInviteCountSettingsAsync(ulong guildId,
        Action<InviteCountSetting> updateAction)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var settings = await uow.InviteCountSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (settings == null)
        {
            settings = inviteCountSettings.TryGetValue(guildId, out var cached) ? cached : NewDefaultSettings(guildId);
            updateAction(settings);
            var stored = await InsertOrReadSettingsAsync(uow, settings);
            if (!ReferenceEquals(stored, settings))
            {
                settings = stored;
                updateAction(settings);
                await uow.UpdateAsync(settings);
            }
        }
        else
        {
            updateAction(settings);
            await uow.UpdateAsync(settings);
        }

        inviteCountSettings[guildId] = settings;
        return settings;
    }

    /// <summary>
    ///     Inserts a guild's settings row, or returns the row another request created in the meantime. The dashboard
    ///     fetches a fresh guild's settings from several requests at once, so a plain read-then-insert races itself
    ///     into the GuildId unique constraint.
    /// </summary>
    /// <param name="uow">The open connection.</param>
    /// <param name="settings">The row to insert.</param>
    /// <returns>The inserted row, or the existing one when the insert lost the race.</returns>
    private static async Task<InviteCountSetting> InsertOrReadSettingsAsync(MewdekoDb uow,
        InviteCountSetting settings)
    {
        try
        {
            settings.Id = await uow.InsertWithInt32IdentityAsync(settings);
            return settings;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return await uow.InviteCountSettings.FirstAsync(x => x.GuildId == settings.GuildId);
        }
    }

    /// <summary>
    ///     Sets whether invite tracking is enabled.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="isEnabled">The new state.</param>
    /// <returns>The new state.</returns>
    public async Task<bool> SetInviteTrackingEnabledAsync(ulong guildId, bool isEnabled)
    {
        await UpdateInviteCountSettingsAsync(guildId, settings => settings.IsEnabled = isEnabled);
        if (isEnabled && client.GetGuild(guildId) is { } guild)
            _ = UpdateGuildInvites(guild);
        return isEnabled;
    }

    /// <summary>
    ///     Sets whether inviters lose credit when their invited members leave.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="removeOnLeave">The new state.</param>
    /// <returns>The new state.</returns>
    public async Task<bool> SetRemoveInviteOnLeaveAsync(ulong guildId, bool removeOnLeave)
    {
        await UpdateInviteCountSettingsAsync(guildId, settings => settings.RemoveInviteOnLeave = removeOnLeave);
        return removeOnLeave;
    }

    /// <summary>
    ///     Sets the minimum account age below which a join is flagged as fake.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="minAge">The minimum age. Zero disables the check.</param>
    /// <returns>The new minimum.</returns>
    public async Task<TimeSpan> SetMinAccountAgeAsync(ulong guildId, TimeSpan minAge)
    {
        await UpdateInviteCountSettingsAsync(guildId, settings => settings.MinAccountAge = minAge);
        return minAge;
    }

    /// <summary>
    ///     Sets whether rejoining members earn their inviter a regular invite again.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="countRejoins">True to count rejoins, false to flag them as fake.</param>
    /// <returns>The new state.</returns>
    public async Task<bool> SetCountRejoinsAsync(ulong guildId, bool countRejoins)
    {
        await UpdateInviteCountSettingsAsync(guildId, settings => settings.CountRejoins = countRejoins);
        return countRejoins;
    }

    /// <summary>
    ///     Sets whether members without an avatar are flagged as fake joins.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="flag">The new state.</param>
    /// <returns>The new state.</returns>
    public async Task<bool> SetFakeOnNoAvatarAsync(ulong guildId, bool flag)
    {
        await UpdateInviteCountSettingsAsync(guildId, settings => settings.FakeOnNoAvatar = flag);
        return flag;
    }

    /// <summary>
    ///     Sets the channel that personal invite links are created for.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel, or null to use the system channel.</param>
    public async Task SetLinkChannelAsync(ulong guildId, ulong? channelId)
    {
        await UpdateInviteCountSettingsAsync(guildId, settings => settings.LinkChannelId = channelId);
    }

    /// <summary>
    ///     Sets the channel that receives join and leave attribution embeds.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel, or null to disable.</param>
    public async Task SetLogChannelAsync(ulong guildId, ulong? channelId)
    {
        await UpdateInviteCountSettingsAsync(guildId, settings => settings.LogChannelId = channelId);
    }

    #endregion

    #region Exclusions

    /// <summary>
    ///     Adds an exclusion.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="targetId">The user or role ID.</param>
    /// <param name="kind">The exclusion kind.</param>
    /// <returns>True when added, false when it already existed.</returns>
    public async Task<bool> AddExclusionAsync(ulong guildId, ulong targetId, InviteExclusionKind kind)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var exists = await db.InviteTrackingExclusions.AnyAsync(x =>
            x.GuildId == guildId && x.TargetId == targetId && x.Kind == (int)kind);
        if (exists)
            return false;

        await db.InsertAsync(new InviteTrackingExclusion
        {
            GuildId = guildId, TargetId = targetId, Kind = (int)kind, DateAdded = DateTime.UtcNow
        });
        return true;
    }

    /// <summary>
    ///     Removes an exclusion.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="targetId">The user or role ID.</param>
    /// <param name="kind">The exclusion kind.</param>
    /// <returns>True when a row was removed.</returns>
    public async Task<bool> RemoveExclusionAsync(ulong guildId, ulong targetId, InviteExclusionKind kind)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.InviteTrackingExclusions
            .Where(x => x.GuildId == guildId && x.TargetId == targetId && x.Kind == (int)kind)
            .DeleteAsync() > 0;
    }

    /// <summary>
    ///     Lists exclusions of a kind.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="kind">The exclusion kind.</param>
    /// <returns>The target IDs.</returns>
    public async Task<List<ulong>> GetExclusionsAsync(ulong guildId, InviteExclusionKind kind)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.InviteTrackingExclusions
            .Where(x => x.GuildId == guildId && x.Kind == (int)kind)
            .Select(x => x.TargetId)
            .ToListAsync();
    }

    #endregion

    #region Labels

    /// <summary>
    ///     Creates or updates the label on an invite code.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="code">The invite code.</param>
    /// <param name="label">The label text.</param>
    /// <param name="roleId">A role granted on join through the code, or null.</param>
    /// <param name="ownerUserId">A member credited instead of the code's creator, or null.</param>
    /// <returns>The saved label.</returns>
    public async Task<InviteLabel> SetLabelAsync(ulong guildId, string code, string label, ulong? roleId = null,
        ulong? ownerUserId = null)
    {
        code = NormalizeCode(code);
        await using var db = await dbFactory.CreateConnectionAsync();
        var existing = await db.InviteLabels.FirstOrDefaultAsync(x => x.GuildId == guildId && x.InviteCode == code);

        if (existing == null)
        {
            existing = new InviteLabel
            {
                GuildId = guildId,
                InviteCode = code,
                Label = label,
                RoleId = roleId,
                OwnerUserId = ownerUserId,
                DateAdded = DateTime.UtcNow
            };
            existing.Id = await db.InsertWithInt32IdentityAsync(existing);
            return existing;
        }

        existing.Label = label;
        existing.RoleId = roleId ?? existing.RoleId;
        existing.OwnerUserId = ownerUserId ?? existing.OwnerUserId;
        await db.UpdateAsync(existing);
        return existing;
    }

    /// <summary>
    ///     Updates only the role on a label.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="code">The invite code.</param>
    /// <param name="roleId">The role, or null to clear it.</param>
    /// <returns>True when the label existed.</returns>
    public async Task<bool> SetLabelRoleAsync(ulong guildId, string code, ulong? roleId)
    {
        code = NormalizeCode(code);
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.InviteLabels
            .Where(x => x.GuildId == guildId && x.InviteCode == code)
            .Set(x => x.RoleId, roleId)
            .UpdateAsync() > 0;
    }

    /// <summary>
    ///     Removes the label from an invite code.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="code">The invite code.</param>
    /// <returns>True when a label was removed.</returns>
    public async Task<bool> RemoveLabelAsync(ulong guildId, string code)
    {
        code = NormalizeCode(code);
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.InviteLabels.Where(x => x.GuildId == guildId && x.InviteCode == code).DeleteAsync() > 0;
    }

    /// <summary>
    ///     Lists every label in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The labels.</returns>
    public async Task<List<InviteLabel>> GetLabelsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.InviteLabels.Where(x => x.GuildId == guildId).OrderBy(x => x.Label).ToListAsync();
    }

    /// <summary>
    ///     Gets the label on a code.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="code">The invite code.</param>
    /// <returns>The label, or null.</returns>
    public async Task<InviteLabel?> GetLabelAsync(ulong guildId, string code)
    {
        code = NormalizeCode(code);
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.InviteLabels.FirstOrDefaultAsync(x => x.GuildId == guildId && x.InviteCode == code);
    }

    /// <summary>
    ///     Strips a full invite URL down to its code.
    /// </summary>
    /// <param name="code">A code or URL.</param>
    /// <returns>The bare code.</returns>
    public static string NormalizeCode(string code)
    {
        code = code.Trim();
        var slash = code.LastIndexOf('/');
        return slash >= 0 ? code[(slash + 1)..] : code;
    }

    #endregion

    #region Join and leave handling

    private async Task OnUserJoined(IGuildUser user)
    {
        var guild = user.Guild;
        var settings = await GetInviteCountSettingsAsync(guild.Id);
        if (!settings.IsEnabled)
            return;

        var curUser = await guild.GetCurrentUserAsync();
        if (!curUser.GuildPermissions.Has(GuildPermission.ManageGuild))
            return;

        var gate = guildJoinLocks.GetOrAdd(guild.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            var result = await AttributeJoinAsync(user, settings);
            PublishJoinResult(guild.Id, user.Id, result);
            collector.Feature("invite", guild.Id);
            await PostJoinLogAsync(guild, user, settings, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to attribute join of {UserId} in {GuildId}", user.Id, guild.Id);
            PublishJoinResult(guild.Id, user.Id, null);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<InviteJoinResult> AttributeJoinAsync(IGuildUser user, InviteCountSetting settings)
    {
        var guild = user.Guild;
        var now = DateTime.UtcNow;

        if (!guildInvites.ContainsKey(guild.Id))
            await UpdateGuildInvites(guild);

        IInviteMetadata? usedInvite = null;
        var joinType = InviteJoinType.Unknown;
        string? code = null;

        if (user.IsBot)
        {
            joinType = InviteJoinType.Bot;
        }
        else
        {
            var newInvites = (await guild.GetInvitesAsync()).ToList();
            usedInvite = FindUsedInvite(guild.Id, newInvites);
            RefreshInviteCache(guild.Id, newInvites);

            if (usedInvite != null)
            {
                joinType = InviteJoinType.Invite;
                code = usedInvite.Code;
            }
            else if (await DetectVanityJoinAsync(guild))
            {
                joinType = InviteJoinType.Vanity;
                code = "vanity";
            }
        }

        await using var db = await dbFactory.CreateConnectionAsync();

        var previousJoins = await db.InvitedBies.CountAsync(x => x.GuildId == guild.Id && x.UserId == user.Id);
        var label = code != null && code != "vanity"
            ? await db.InviteLabels.FirstOrDefaultAsync(x => x.GuildId == guild.Id && x.InviteCode == code)
            : null;

        var inviterId = label?.OwnerUserId ?? usedInvite?.Inviter?.Id ?? 0;
        IUser? inviter = null;
        if (inviterId != 0)
            inviter = await guild.GetUserAsync(inviterId) ?? usedInvite?.Inviter;

        var fakeReason = InviteFakeReason.None;
        if (joinType == InviteJoinType.Invite)
        {
            if (inviterId == user.Id)
                fakeReason = InviteFakeReason.Self;
            else if (settings.MinAccountAge > TimeSpan.Zero &&
                     DateTimeOffset.UtcNow - user.CreatedAt < settings.MinAccountAge)
                fakeReason = InviteFakeReason.NewAccount;
            else if (!settings.CountRejoins && previousJoins > 0)
                fakeReason = InviteFakeReason.Rejoin;
            else if (settings.FakeOnNoAvatar && user.AvatarId == null)
                fakeReason = InviteFakeReason.NoAvatar;
        }

        var record = new InvitedBy
        {
            UserId = user.Id,
            InviterId = inviterId,
            GuildId = guild.Id,
            InviteCode = code,
            JoinType = (int)joinType,
            IsFake = fakeReason != InviteFakeReason.None,
            FakeReason = (int)fakeReason,
            DateAdded = now
        };
        await db.InsertAsync(record);

        if (inviterId != 0 && !await IsInviterExcludedAsync(db, guild, inviterId))
        {
            await ApplyDeltaAsync(db, guild.Id, inviterId, count =>
            {
                if (record.IsFake)
                    count.Fake++;
                else
                    count.Regular++;
            });
        }

        if (label?.RoleId is { } roleId && guild.GetRole(roleId) is { } role)
        {
            try
            {
                await user.AddRoleAsync(role);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not grant label role {RoleId} to {UserId} in {GuildId}", roleId,
                    user.Id, guild.Id);
            }
        }

        return new InviteJoinResult(inviter, code, usedInvite?.Uses ?? 0, label?.Label, joinType, fakeReason,
            previousJoins + 1);
    }

    private async Task<bool> IsInviterExcludedAsync(MewdekoDb db, IGuild guild, ulong inviterId)
    {
        var exclusions = await db.InviteTrackingExclusions
            .Where(x => x.GuildId == guild.Id &&
                        (x.Kind == (int)InviteExclusionKind.BlacklistedUser ||
                         x.Kind == (int)InviteExclusionKind.BlacklistedRole))
            .ToListAsync();

        if (exclusions.Count == 0)
            return false;

        if (exclusions.Any(x => x.Kind == (int)InviteExclusionKind.BlacklistedUser && x.TargetId == inviterId))
            return true;

        var blacklistedRoles = exclusions
            .Where(x => x.Kind == (int)InviteExclusionKind.BlacklistedRole)
            .Select(x => x.TargetId)
            .ToHashSet();
        if (blacklistedRoles.Count == 0)
            return false;

        var inviter = await guild.GetUserAsync(inviterId);
        return inviter != null && inviter.RoleIds.Any(blacklistedRoles.Contains);
    }

    private async Task OnUserLeft(IGuild guild, IUser user)
    {
        var settings = await GetInviteCountSettingsAsync(guild.Id);
        if (!settings.IsEnabled)
            return;

        await using var uow = await dbFactory.CreateConnectionAsync();

        var invitedBy = await uow.InvitedBies
            .Where(x => x.UserId == user.Id && x.GuildId == guild.Id && x.LeftAt == null)
            .OrderByDescending(x => x.DateAdded)
            .FirstOrDefaultAsync();

        if (invitedBy == null)
            return;

        invitedBy.LeftAt = DateTime.UtcNow;
        await uow.UpdateAsync(invitedBy);

        if (settings.RemoveInviteOnLeave && invitedBy.InviterId != 0 && !invitedBy.IsFake)
            await ApplyDeltaAsync(uow, guild.Id, invitedBy.InviterId, count => count.Left++);

        await PostLeaveLogAsync(guild, user, settings, invitedBy);
    }

    private async Task OnLeftGuild(SocketGuild guild)
    {
        inviteCountSettings.TryRemove(guild.Id, out _);
        guildInvites.TryRemove(guild.Id, out _);
        pendingUses.TryRemove(guild.Id, out _);
        vanityUses.TryRemove(guild.Id, out _);
        guildJoinLocks.TryRemove(guild.Id, out _);
        logger.LogInformation("Cleaned up invite tracking for left guild {GuildId}", guild.Id);
        await Task.CompletedTask;
    }

    private void PublishJoinResult(ulong guildId, ulong userId, InviteJoinResult? result)
    {
        var key = (guildId, userId);
        if (result != null)
            joinResults[key] = (result, DateTime.UtcNow);
        if (joinWaiters.TryRemove(key, out var tcs))
            tcs.TrySetResult(result);
    }

    /// <summary>
    ///     Waits for the attribution of a join that may still be in progress, so greet placeholders can reference the
    ///     inviter without racing the invite fetch.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member who joined.</param>
    /// <param name="timeout">How long to wait before giving up.</param>
    /// <returns>The attribution, or null when it did not arrive in time or tracking is off.</returns>
    public async Task<InviteJoinResult?> WaitForJoinResultAsync(ulong guildId, ulong userId, TimeSpan timeout)
    {
        var key = (guildId, userId);
        if (joinResults.TryGetValue(key, out var existing))
            return existing.Result;

        if (!inviteCountSettings.TryGetValue(guildId, out var settings) || !settings.IsEnabled)
            return null;

        var tcs = joinWaiters.GetOrAdd(key,
            _ => new TaskCompletionSource<InviteJoinResult?>(TaskCreationOptions.RunContinuationsAsynchronously));

        if (joinResults.TryGetValue(key, out existing))
        {
            joinWaiters.TryRemove(key, out _);
            return existing.Result;
        }

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
        if (completed == tcs.Task)
            return await tcs.Task;

        joinWaiters.TryRemove(key, out _);
        return null;
    }

    /// <summary>
    ///     Fills a replacement builder with inviter, invite and join metadata placeholders for a greet message,
    ///     waiting briefly for the join attribution when it is still in flight.
    /// </summary>
    /// <param name="user">The member who joined.</param>
    /// <param name="builder">The builder to populate.</param>
    /// <returns>The builder, for chaining.</returns>
    public async Task<ReplacementBuilder> ApplyGreetPlaceholdersAsync(IGuildUser user, ReplacementBuilder builder)
    {
        builder.WithJoinTimestamps(user).WithMemberOrdinal(user.Guild);

        var settings = await GetInviteCountSettingsAsync(user.Guild.Id);
        if (!settings.IsEnabled)
            return builder.WithInviteInfo(null, null, 1, 0);

        var result = await WaitForJoinResultAsync(user.Guild.Id, user.Id, TimeSpan.FromSeconds(5));
        InviteCount? counts = null;
        if (result?.Inviter != null)
            counts = await GetInviteBreakdownAsync(user.Guild.Id, result.Inviter.Id);

        var (joins, leaves) = await GetJoinLeaveCountsAsync(user.Guild.Id, user.Id);
        return builder.WithInviteInfo(result, counts, Math.Max(joins, result?.JoinCount ?? 1), leaves);
    }

    /// <summary>
    ///     Fills a replacement builder with the inviter placeholders for a leave message, based on the member's most
    ///     recent join.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="user">The member who left.</param>
    /// <param name="builder">The builder to populate.</param>
    /// <returns>The builder, for chaining.</returns>
    public async Task<ReplacementBuilder> ApplyLeavePlaceholdersAsync(IGuild guild, IUser user,
        ReplacementBuilder builder)
    {
        builder.WithMemberOrdinal(guild);
        var record = await GetJoinRecordAsync(guild.Id, user.Id);
        if (record == null)
            return builder.WithInviteInfo(null, null, 0, 0);

        IUser? inviter = null;
        if (record.InviterId != 0)
        {
            inviter = await guild.GetUserAsync(record.InviterId);
            inviter ??= await client.Rest.GetUserAsync(record.InviterId);
        }

        var counts = inviter != null ? await GetInviteBreakdownAsync(guild.Id, inviter.Id) : null;
        var label = record.InviteCode != null ? await GetLabelAsync(guild.Id, record.InviteCode) : null;
        var (joins, leaves) = await GetJoinLeaveCountsAsync(guild.Id, user.Id);

        var result = new InviteJoinResult(inviter, record.InviteCode, 0, label?.Label,
            (InviteJoinType)record.JoinType, (InviteFakeReason)record.FakeReason, joins);
        return builder.WithInviteInfo(result, counts, joins, leaves);
    }

    private async Task PostJoinLogAsync(IGuild guild, IGuildUser user, InviteCountSetting settings,
        InviteJoinResult result)
    {
        if (settings.LogChannelId is not { } channelId)
            return;
        if (await guild.GetTextChannelAsync(channelId) is not { } channel)
            return;

        var description = BuildJoinLine(guild.Id, user, result);
        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithAuthor(user.ToString(), user.RealAvatarUrl().ToString())
            .WithDescription(description)
            .AddField(strings.InviteLogAccountCreated(guild.Id),
                TimestampTag.FromDateTimeOffset(user.CreatedAt, TimestampTagStyles.Relative).ToString(), true)
            .AddField(strings.InviteLogJoinType(guild.Id), result.JoinType.ToString(), true)
            .WithCurrentTimestamp();

        if (result.IsFake)
            eb.AddField(strings.InviteLogFlagged(guild.Id), result.FakeReason.ToString(), true);
        if (result.JoinCount > 1)
            eb.AddField(strings.InviteLogJoins(guild.Id), result.JoinCount.ToString(), true);

        try
        {
            await channel.SendMessageAsync(embed: eb.Build());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not post invite join log in {GuildId}", guild.Id);
        }
    }

    private string BuildJoinLine(ulong guildId, IGuildUser user, InviteJoinResult result)
    {
        return result.JoinType switch
        {
            InviteJoinType.Bot => strings.InviteLogJoinedBot(guildId, user.Mention),
            InviteJoinType.Vanity => strings.InviteLogJoinedVanity(guildId, user.Mention),
            InviteJoinType.Invite when result.Inviter != null => strings.InviteLogJoinedBy(guildId, user.Mention,
                result.Inviter.Mention, result.Code, result.Label != null ? $" ({result.Label})" : ""),
            InviteJoinType.Invite => strings.InviteLogJoinedCode(guildId, user.Mention, result.Code),
            _ => strings.InviteLogJoinedUnknown(guildId, user.Mention)
        };
    }

    private async Task PostLeaveLogAsync(IGuild guild, IUser user, InviteCountSetting settings, InvitedBy record)
    {
        if (settings.LogChannelId is not { } channelId)
            return;
        if (await guild.GetTextChannelAsync(channelId) is not { } channel)
            return;

        var stayed = record.DateAdded.HasValue ? DateTime.UtcNow - record.DateAdded.Value : TimeSpan.Zero;
        var inviterText = record.InviterId == 0
            ? strings.InviteLogNobody(guild.Id)
            : (await guild.GetUserAsync(record.InviterId))?.Mention ?? $"<@{record.InviterId}>";
        var codeText = record.InviteCode != null ? strings.InviteLogLeftCode(guild.Id, record.InviteCode) : "";
        var description = strings.InviteLogLeft(guild.Id, user.Mention, inviterText, codeText);

        var eb = new EmbedBuilder()
            .WithErrorColor()
            .WithAuthor(user.ToString(), user.RealAvatarUrl().ToString())
            .WithDescription(description)
            .AddField(strings.InviteLogStayed(guild.Id), $"{(int)stayed.TotalDays}d {stayed.Hours}h", true)
            .WithCurrentTimestamp();

        try
        {
            await channel.SendMessageAsync(embed: eb.Build());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not post invite leave log in {GuildId}", guild.Id);
        }
    }

    #endregion

    #region Invite cache

    private Task OnInviteCreated(IInvite invite)
    {
        if (invite.Guild == null)
            return Task.CompletedTask;

        var invites = guildInvites.GetOrAdd(invite.Guild.Id, _ => new ConcurrentDictionary<string, IInviteMetadata>());
        if (invite is IInviteMetadata metadata)
            invites[invite.Code] = metadata;
        return Task.CompletedTask;
    }

    private Task OnInviteDeleted(IGuildChannel channel, string code)
    {
        if (guildInvites.TryGetValue(channel.Guild.Id, out var invites))
            invites.TryRemove(code, out _);
        if (pendingUses.TryGetValue(channel.Guild.Id, out var pending))
            pending.TryRemove(code, out _);
        return Task.CompletedTask;
    }

    private async Task UpdateGuildInvites(IGuild guild)
    {
        try
        {
            var invites = (await guild.GetInvitesAsync()).ToList();
            RefreshInviteCache(guild.Id, invites);

            if (guild.VanityURLCode != null)
            {
                try
                {
                    var vanity = await guild.GetVanityInviteAsync();
                    if (vanity != null)
                        vanityUses[guild.Id] = vanity.Uses ?? 0;
                }
                catch (Exception ex) when (ex is HttpException or InvalidOperationException
                                               or NullReferenceException)
                {
                    logger.LogDebug(ex, "Vanity invite unavailable for guild {GuildId}", guild.Id);
                }
            }

            logger.LogDebug("Updated {Count} invites for guild {GuildId}", invites.Count, guild.Id);
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

    private void RefreshInviteCache(ulong guildId, IEnumerable<IInviteMetadata> invites)
    {
        guildInvites[guildId] =
            new ConcurrentDictionary<string, IInviteMetadata>(invites.ToDictionary(x => x.Code, x => x));
    }

    /// <summary>
    ///     Picks the invite whose use count rose since the last snapshot. When several joins land between snapshots the
    ///     surplus is remembered as pending credit for later join events that see no delta of their own.
    /// </summary>
    private IInviteMetadata? FindUsedInvite(ulong guildId, List<IInviteMetadata> newInvites)
    {
        if (!guildInvites.TryGetValue(guildId, out var oldInvites))
            return null;

        var pending = pendingUses.GetOrAdd(guildId, _ => new ConcurrentDictionary<string, int>());

        IInviteMetadata? best = null;
        var bestDelta = 0;
        foreach (var newInvite in newInvites)
        {
            var oldUses = oldInvites.TryGetValue(newInvite.Code, out var oldInvite) ? oldInvite.Uses ?? 0 : 0;
            var delta = (newInvite.Uses ?? 0) - oldUses;
            if (delta <= 0)
                continue;

            if (delta > 1)
                pending.AddOrUpdate(newInvite.Code, delta - 1, (_, v) => v + delta - 1);

            if (delta > bestDelta)
            {
                best = newInvite;
                bestDelta = delta;
            }
        }

        if (best != null)
            return best;

        foreach (var (code, remaining) in pending)
        {
            if (remaining <= 0)
            {
                pending.TryRemove(code, out _);
                continue;
            }

            var invite = newInvites.FirstOrDefault(x => x.Code == code);
            if (invite == null)
            {
                pending.TryRemove(code, out _);
                continue;
            }

            if (remaining == 1)
                pending.TryRemove(code, out _);
            else
                pending[code] = remaining - 1;
            return invite;
        }

        return null;
    }

    private async Task<bool> DetectVanityJoinAsync(IGuild guild)
    {
        if (guild.VanityURLCode == null)
            return false;

        try
        {
            var vanity = await guild.GetVanityInviteAsync();
            if (vanity == null)
                return false;

            var uses = vanity.Uses ?? 0;
            var previous = vanityUses.GetOrAdd(guild.Id, uses);
            vanityUses[guild.Id] = uses;
            return uses > previous;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task ProcessGuildWithRateLimiting(IGuild guild, SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        try
        {
            await UpdateGuildInvites(guild);
            await Task.Delay(1000);
        }
        finally
        {
            semaphore.Release();
        }
    }

    #endregion

    #region Counts

    private static async Task<InviteCount> ApplyDeltaAsync(MewdekoDb db, ulong guildId, ulong userId,
        Action<InviteCount> mutate)
    {
        var row = await db.InviteCounts.FirstOrDefaultAsync(x => x.UserId == userId && x.GuildId == guildId);
        if (row == null)
        {
            row = new InviteCount
            {
                UserId = userId, GuildId = guildId, DateAdded = DateTime.UtcNow
            };
            mutate(row);
            row.Recalculate();
            row.Id = await db.InsertWithInt32IdentityAsync(row);
            return row;
        }

        mutate(row);
        row.Recalculate();
        await db.UpdateAsync(row);
        return row;
    }

    /// <summary>
    ///     Gets the net invite count for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The net total.</returns>
    public async Task<int> GetInviteCount(ulong userId, ulong guildId)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        return await uow.InviteCounts
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .Select(x => x.Count)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Gets the full invite breakdown for a user.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The row, or a zeroed row when the user has no invites.</returns>
    public async Task<InviteCount> GetInviteBreakdownAsync(ulong guildId, ulong userId)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        return await uow.InviteCounts.FirstOrDefaultAsync(x => x.UserId == userId && x.GuildId == guildId)
               ?? new InviteCount
               {
                   GuildId = guildId, UserId = userId
               };
    }

    /// <summary>
    ///     Gets a user's rank on the all time leaderboard.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The 1-based rank, or null when the user has no invites.</returns>
    public async Task<int?> GetRankAsync(ulong guildId, ulong userId)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var mine = await uow.InviteCounts.Where(x => x.GuildId == guildId && x.UserId == userId)
            .Select(x => (int?)x.Count).FirstOrDefaultAsync();
        if (mine == null)
            return null;

        var hidden = await uow.InviteTrackingExclusions
            .Where(x => x.GuildId == guildId && x.Kind == (int)InviteExclusionKind.HiddenUser)
            .Select(x => x.TargetId)
            .ToListAsync();

        return await uow.InviteCounts.CountAsync(x =>
            x.GuildId == guildId && x.Count > mine && !hidden.Contains(x.UserId)) + 1;
    }

    /// <summary>
    ///     Adjusts a user's invite tally by hand.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="regular">Change to regular invites.</param>
    /// <param name="bonus">Change to bonus invites.</param>
    /// <param name="fake">Change to fake invites.</param>
    /// <returns>The updated row.</returns>
    public async Task<InviteCount> AdjustInvitesAsync(ulong guildId, ulong userId, int regular = 0, int bonus = 0,
        int fake = 0)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await ApplyDeltaAsync(db, guildId, userId, count =>
        {
            count.Regular = Math.Max(0, count.Regular + regular);
            count.Bonus += bonus;
            count.Fake = Math.Max(0, count.Fake + fake);
        });
    }

    /// <summary>
    ///     Resets invites for one user.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>True when the user had a tally.</returns>
    public async Task<bool> ResetInvitesAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var removed = await db.InviteCounts.Where(x => x.GuildId == guildId && x.UserId == userId).DeleteAsync();
        await db.InvitedBies.Where(x => x.GuildId == guildId && x.InviterId == userId)
            .Set(x => x.InviterId, 0UL)
            .UpdateAsync();
        return removed > 0;
    }

    /// <summary>
    ///     Resets invites for the whole guild or only for inviters who have left.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="scope">What to reset.</param>
    /// <returns>How many inviters were reset.</returns>
    public async Task<int> ResetInvitesAsync(IGuild guild, InviteResetScope scope)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        if (scope == InviteResetScope.Server)
        {
            var count = await db.InviteCounts.Where(x => x.GuildId == guild.Id).DeleteAsync();
            await db.InvitedBies.Where(x => x.GuildId == guild.Id).DeleteAsync();
            return count;
        }

        var inviterIds = await db.InviteCounts.Where(x => x.GuildId == guild.Id).Select(x => x.UserId)
            .ToListAsync();
        var gone = new List<ulong>();
        foreach (var inviterId in inviterIds)
        {
            if (await guild.GetUserAsync(inviterId) == null)
                gone.Add(inviterId);
        }

        if (gone.Count == 0)
            return 0;

        return await db.InviteCounts.Where(x => x.GuildId == guild.Id && gone.Contains(x.UserId)).DeleteAsync();
    }

    /// <summary>
    ///     Imports use counts from Discord's own invite metadata, raising each inviter's regular total to the sum of
    ///     uses across their codes. Totals are never lowered, so running it twice is safe.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="userId">Restrict the import to one inviter, or null for everyone.</param>
    /// <returns>How many inviters were raised.</returns>
    public async Task<int> SyncInvitesAsync(IGuild guild, ulong? userId = null)
    {
        var invites = (await guild.GetInvitesAsync()).ToList();
        var labels = await GetLabelsAsync(guild.Id);
        var owners = labels.Where(x => x.OwnerUserId.HasValue)
            .ToDictionary(x => x.InviteCode, x => x.OwnerUserId!.Value);

        var totals = new Dictionary<ulong, int>();
        foreach (var invite in invites)
        {
            var inviterId = owners.TryGetValue(invite.Code, out var owner) ? owner : invite.Inviter?.Id ?? 0;
            if (inviterId == 0 || userId.HasValue && inviterId != userId.Value)
                continue;
            totals[inviterId] = totals.GetValueOrDefault(inviterId) + (invite.Uses ?? 0);
        }

        await using var db = await dbFactory.CreateConnectionAsync();
        var raised = 0;
        foreach (var (inviterId, uses) in totals)
        {
            var before = await db.InviteCounts.Where(x => x.GuildId == guild.Id && x.UserId == inviterId)
                .Select(x => (int?)x.Regular).FirstOrDefaultAsync() ?? 0;
            if (uses <= before)
                continue;

            await ApplyDeltaAsync(db, guild.Id, inviterId, count => count.Regular = uses);
            raised++;
        }

        RefreshInviteCache(guild.Id, invites);
        return raised;
    }

    #endregion

    #region Relationships and lists

    /// <summary>
    ///     Gets who invited a user, based on their most recent join.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guild">The guild.</param>
    /// <returns>The inviter, or null.</returns>
    public async Task<IUser?> GetInviter(ulong userId, IGuild guild)
    {
        var record = await GetJoinRecordAsync(guild.Id, userId);
        if (record == null || record.InviterId == 0)
            return null;

        IUser? inviter = await guild.GetUserAsync(record.InviterId);
        inviter ??= await client.Rest.GetUserAsync(record.InviterId);
        return inviter;
    }

    /// <summary>
    ///     Gets the most recent join record for a user.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The record, or null when the join was never witnessed.</returns>
    public async Task<InvitedBy?> GetJoinRecordAsync(ulong guildId, ulong userId)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        return await uow.InvitedBies
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .OrderByDescending(x => x.DateAdded)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Counts how many times a user has joined and left the guild while tracking was on.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>Join and leave counts.</returns>
    public async Task<(int Joins, int Leaves)> GetJoinLeaveCountsAsync(ulong guildId, ulong userId)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var joins = await uow.InvitedBies.CountAsync(x => x.GuildId == guildId && x.UserId == userId);
        var leaves = await uow.InvitedBies.CountAsync(x =>
            x.GuildId == guildId && x.UserId == userId && x.LeftAt != null);
        return (joins, leaves);
    }

    /// <summary>
    ///     Gets the members currently in the guild that a user invited.
    /// </summary>
    /// <param name="inviterId">The inviter.</param>
    /// <param name="guild">The guild.</param>
    /// <returns>The invited members still present.</returns>
    public async Task<List<IUser>> GetInvitedUsers(ulong inviterId, IGuild guild)
    {
        var records = await GetInvitedRecordsAsync(guild.Id, inviterId, includeLeft: false);
        var users = new List<IUser>();
        foreach (var record in records)
        {
            var user = await guild.GetUserAsync(record.UserId);
            if (user != null)
                users.Add(user);
        }

        return users;
    }

    /// <summary>
    ///     Lists join records matching a set of filters.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="inviterId">Only joins credited to this inviter, or null.</param>
    /// <param name="code">Only joins through this code, or null.</param>
    /// <param name="label">Only joins through codes carrying this label, or null.</param>
    /// <param name="includeLeft">Whether to include members who have since left.</param>
    /// <param name="since">Only joins after this time, or null.</param>
    /// <returns>The matching records, newest first.</returns>
    public async Task<List<InvitedBy>> GetInvitedRecordsAsync(ulong guildId, ulong? inviterId = null,
        string? code = null, string? label = null, bool includeLeft = true, DateTime? since = null)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var query = uow.InvitedBies.Where(x => x.GuildId == guildId);

        if (inviterId.HasValue)
            query = query.Where(x => x.InviterId == inviterId.Value);
        if (code != null)
        {
            var normalized = NormalizeCode(code);
            query = query.Where(x => x.InviteCode == normalized);
        }

        if (label != null)
        {
            var codes = await uow.InviteLabels
                .Where(x => x.GuildId == guildId && x.Label.ToLower() == label.ToLower())
                .Select(x => x.InviteCode)
                .ToListAsync();
            query = query.Where(x => x.InviteCode != null && codes.Contains(x.InviteCode));
        }

        if (!includeLeft)
            query = query.Where(x => x.LeftAt == null);
        if (since.HasValue)
            query = query.Where(x => x.DateAdded >= since.Value);

        return await query.OrderByDescending(x => x.DateAdded).ToListAsync();
    }

    /// <summary>
    ///     Builds the invite leaderboard.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="range">The window. All time uses stored tallies including bonus invites; other windows are
    ///     derived from witnessed joins.</param>
    /// <param name="roleId">Only include inviters holding this role, or null.</param>
    /// <param name="limit">Maximum entries.</param>
    /// <returns>The leaderboard, best first.</returns>
    public async Task<List<InviteLeaderboardEntry>> GetInviteLeaderboardAsync(IGuild guild,
        StatsRange range = StatsRange.AllTime, ulong? roleId = null, int limit = 100)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();

        var hidden = (await uow.InviteTrackingExclusions
                .Where(x => x.GuildId == guild.Id && x.Kind == (int)InviteExclusionKind.HiddenUser)
                .Select(x => x.TargetId)
                .ToListAsync())
            .ToHashSet();

        var since = range.Since();
        var joinQuery = uow.InvitedBies.Where(x => x.GuildId == guild.Id && x.InviterId != 0);
        if (since.HasValue)
            joinQuery = joinQuery.Where(x => x.DateAdded >= since.Value);

        var joinStats = (await joinQuery
                .GroupBy(x => x.InviterId)
                .Select(g => new
                {
                    InviterId = g.Key,
                    Regular = g.Sum(x => x.IsFake ? 0 : 1),
                    Left = g.Sum(x => !x.IsFake && x.LeftAt != null ? 1 : 0),
                    Fake = g.Sum(x => x.IsFake ? 1 : 0),
                    Latest = g.Max(x => x.DateAdded)
                })
                .ToListAsync())
            .ToDictionary(x => x.InviterId);

        var entries =
            new List<(ulong UserId, int Total, int Regular, int Left, int Fake, int Bonus, DateTime? Latest)>();

        if (range == StatsRange.AllTime)
        {
            var counts = await uow.InviteCounts.Where(x => x.GuildId == guild.Id && x.Count != 0).ToListAsync();
            foreach (var count in counts)
            {
                joinStats.TryGetValue(count.UserId, out var js);
                entries.Add((count.UserId, count.Count, count.Regular, count.Left, count.Fake, count.Bonus,
                    js?.Latest));
            }
        }
        else
        {
            foreach (var (inviterId, js) in joinStats)
                entries.Add((inviterId, js.Regular - js.Left - js.Fake, js.Regular, js.Left, js.Fake, 0, js.Latest));
        }

        var result = new List<InviteLeaderboardEntry>();
        foreach (var entry in entries.Where(e => !hidden.Contains(e.UserId)).OrderByDescending(e => e.Total))
        {
            var user = await guild.GetUserAsync(entry.UserId);
            if (roleId.HasValue && (user == null || !user.RoleIds.Contains(roleId.Value)))
                continue;

            double? retention = entry.Regular > 0 ? (double)(entry.Regular - entry.Left) / entry.Regular : null;
            result.Add(new InviteLeaderboardEntry(entry.UserId, user?.Username ?? $"Unknown ({entry.UserId})",
                entry.Total, entry.Regular, entry.Left, entry.Fake, entry.Bonus, retention, entry.Latest));

            if (result.Count >= limit)
                break;
        }

        return result;
    }

    /// <summary>
    ///     Summarises growth for a window: joins, leaves, retention, fake joins, join sources and top codes.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="range">The window.</param>
    /// <returns>The analytics.</returns>
    public async Task<InviteAnalytics> GetAnalyticsAsync(IGuild guild, StatsRange range)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var since = range.Since() ?? DateTime.MinValue;

        var joins = await uow.InvitedBies.Where(x => x.GuildId == guild.Id && x.DateAdded >= since).ToListAsync();
        var leaves = await uow.InvitedBies.CountAsync(x => x.GuildId == guild.Id && x.LeftAt >= since);

        var labels = (await uow.InviteLabels.Where(x => x.GuildId == guild.Id).ToListAsync())
            .ToDictionary(x => x.InviteCode, x => x.Label);

        var topCodes = joins
            .Where(x => x.InviteCode != null)
            .GroupBy(x => x.InviteCode!)
            .Select(g => new InviteCodeSummary(g.Key, labels.GetValueOrDefault(g.Key), g.Count()))
            .OrderByDescending(x => x.Joins)
            .Take(10)
            .ToList();

        var topInviters = await GetInviteLeaderboardAsync(guild, range, limit: 5);

        return new InviteAnalytics(range,
            joins.Count,
            leaves,
            joins.Count(x => x.IsFake),
            joins.Count(x => x.LeftAt == null),
            joins.Count(x => x.JoinType == (int)InviteJoinType.Invite),
            joins.Count(x => x.JoinType == (int)InviteJoinType.Vanity),
            joins.Count(x => x.JoinType == (int)InviteJoinType.Bot),
            joins.Count(x => x.JoinType == (int)InviteJoinType.Unknown),
            topCodes,
            topInviters);
    }

    /// <summary>
    ///     Gets daily join and leave counts from witnessed joins, for charts and reports.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="days">How many days back to include.</param>
    /// <returns>One entry per day, oldest first.</returns>
    public async Task<List<(DateTime Day, int Joins, int Leaves)>> GetDailyGrowthAsync(ulong guildId, int days)
    {
        await using var uow = await dbFactory.CreateConnectionAsync();
        var start = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var joins = await uow.InvitedBies
            .Where(x => x.GuildId == guildId && x.DateAdded >= start)
            .Select(x => x.DateAdded!.Value)
            .ToListAsync();
        var leaves = await uow.InvitedBies
            .Where(x => x.GuildId == guildId && x.LeftAt >= start)
            .Select(x => x.LeftAt!.Value)
            .ToListAsync();

        var joinsByDay = joins.GroupBy(x => x.Date).ToDictionary(g => g.Key, g => g.Count());
        var leavesByDay = leaves.GroupBy(x => x.Date).ToDictionary(g => g.Key, g => g.Count());

        return Enumerable.Range(0, days)
            .Select(i => start.AddDays(i))
            .Select(day => (day, joinsByDay.GetValueOrDefault(day), leavesByDay.GetValueOrDefault(day)))
            .ToList();
    }

    #endregion

    #region Discord invite management

    /// <summary>
    ///     Lists the invite codes a user has created, plus any labelled codes they own.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The invites.</returns>
    public async Task<List<IInviteMetadata>> GetUserInviteCodesAsync(IGuild guild, ulong userId)
    {
        var invites = (await guild.GetInvitesAsync()).ToList();
        var owned = (await GetLabelsAsync(guild.Id))
            .Where(x => x.OwnerUserId == userId)
            .Select(x => x.InviteCode)
            .ToHashSet();

        return invites.Where(x => x.Inviter?.Id == userId || owned.Contains(x.Code)).ToList();
    }

    /// <summary>
    ///     Finds a permanent invite belonging to a user, creating and labelling one when they have none.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="user">The member.</param>
    /// <returns>The invite, or null when no channel allows the bot to create one.</returns>
    public async Task<IInviteMetadata?> GetOrCreateUserLinkAsync(IGuild guild, IGuildUser user)
    {
        var existing = (await GetUserInviteCodesAsync(guild, user.Id))
            .Where(x => !x.IsTemporary && x.MaxAge is null or 0 && x.MaxUses is null or 0)
            .OrderByDescending(x => x.Uses ?? 0)
            .FirstOrDefault();
        if (existing != null)
            return existing;

        var settings = await GetInviteCountSettingsAsync(guild.Id);
        ITextChannel? channel = null;
        if (settings.LinkChannelId is { } linkChannelId)
            channel = await guild.GetTextChannelAsync(linkChannelId);
        channel ??= await guild.GetSystemChannelAsync();
        channel ??= (await guild.GetTextChannelsAsync()).OrderBy(x => x.Position).FirstOrDefault();
        if (channel == null)
            return null;

        var invite = await channel.CreateInviteAsync(null, null, false, true);
        await SetLabelAsync(guild.Id, invite.Code, $"{user.Username}'s link", ownerUserId: user.Id);

        var cache = guildInvites.GetOrAdd(guild.Id, _ => new ConcurrentDictionary<string, IInviteMetadata>());
        cache[invite.Code] = invite;
        return invite;
    }

    /// <summary>
    ///     Deletes an invite code from the guild.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="code">The code or URL.</param>
    /// <returns>True when the invite existed and was deleted.</returns>
    public async Task<bool> DeleteInviteAsync(IGuild guild, string code)
    {
        code = NormalizeCode(code);
        var invite = (await guild.GetInvitesAsync()).FirstOrDefault(x => x.Code == code);
        if (invite == null)
            return false;

        await invite.DeleteAsync();
        if (guildInvites.TryGetValue(guild.Id, out var cache))
            cache.TryRemove(code, out _);
        return true;
    }

    /// <summary>
    ///     Deletes invite codes matching a set of filters.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="maxUses">Only delete codes used at most this many times, or null for any.</param>
    /// <param name="includeUsers">Only codes created by these users, or empty for any.</param>
    /// <param name="excludeUsers">Never delete codes created by these users.</param>
    /// <param name="includeChannels">Only codes pointing at these channels, or empty for any.</param>
    /// <param name="excludeChannels">Never delete codes pointing at these channels.</param>
    /// <param name="limit">Stop after this many deletions.</param>
    /// <returns>How many codes were deleted.</returns>
    public async Task<int> PurgeInviteCodesAsync(IGuild guild, int? maxUses, IReadOnlyCollection<ulong> includeUsers,
        IReadOnlyCollection<ulong> excludeUsers, IReadOnlyCollection<ulong> includeChannels,
        IReadOnlyCollection<ulong> excludeChannels, int limit = 200)
    {
        var invites = (await guild.GetInvitesAsync()).ToList();
        var labelled = (await GetLabelsAsync(guild.Id)).Select(x => x.InviteCode).ToHashSet();
        var deleted = 0;

        foreach (var invite in invites)
        {
            if (deleted >= limit)
                break;
            if (labelled.Contains(invite.Code))
                continue;
            if (maxUses.HasValue && (invite.Uses ?? 0) > maxUses.Value)
                continue;

            var inviterId = invite.Inviter?.Id ?? 0;
            if (includeUsers.Count > 0 && !includeUsers.Contains(inviterId))
                continue;
            if (excludeUsers.Contains(inviterId))
                continue;
            if (includeChannels.Count > 0 && !includeChannels.Contains(invite.ChannelId))
                continue;
            if (excludeChannels.Contains(invite.ChannelId))
                continue;

            try
            {
                await invite.DeleteAsync();
                deleted++;
                if (guildInvites.TryGetValue(guild.Id, out var cache))
                    cache.TryRemove(invite.Code, out _);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not delete invite {Code} in {GuildId}", invite.Code, guild.Id);
            }
        }

        return deleted;
    }

    /// <summary>
    ///     Bans every member that a user invited or that joined through a code.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="inviterId">The inviter, or null.</param>
    /// <param name="code">The invite code, or null.</param>
    /// <param name="reason">The audit log reason.</param>
    /// <param name="pruneDays">Days of messages to delete.</param>
    /// <param name="includeInviter">Whether to ban the inviter as well.</param>
    /// <returns>How many bans succeeded and failed.</returns>
    public async Task<(int Banned, int Failed)> MassBanAsync(IGuild guild, ulong? inviterId, string? code,
        string reason, int pruneDays = 0, bool includeInviter = false)
    {
        var records = await GetInvitedRecordsAsync(guild.Id, inviterId, code);
        var targets = records.Select(x => x.UserId).ToHashSet();
        if (includeInviter && inviterId.HasValue)
            targets.Add(inviterId.Value);

        var banned = 0;
        var failed = 0;
        foreach (var target in targets)
        {
            try
            {
                await guild.AddBanAsync(target, pruneDays, reason);
                banned++;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogWarning(ex, "Mass ban failed for {UserId} in {GuildId}", target, guild.Id);
            }
        }

        return (banned, failed);
    }

    #endregion

    #region Exports

    /// <summary>
    ///     Renders the leaderboard as CSV.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="range">The window.</param>
    /// <returns>CSV text.</returns>
    public async Task<string> ExportLeaderboardCsvAsync(IGuild guild, StatsRange range)
    {
        var rows = await GetInviteLeaderboardAsync(guild, range, limit: int.MaxValue);
        var sb = new StringBuilder();
        sb.AppendLine("rank,user_id,username,total,regular,left,fake,bonus,retention,latest_join_utc");
        var rank = 1;
        foreach (var row in rows)
        {
            sb.Append(rank++).Append(',')
                .Append(row.UserId).Append(',')
                .Append(Csv(row.Username)).Append(',')
                .Append(row.Total).Append(',')
                .Append(row.Regular).Append(',')
                .Append(row.Left).Append(',')
                .Append(row.Fake).Append(',')
                .Append(row.Bonus).Append(',')
                .Append(row.Retention.HasValue ? row.Retention.Value.ToString("F3") : "").Append(',')
                .Append(row.LatestJoinAt?.ToString("O") ?? "")
                .AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Renders join records as CSV.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="inviterId">Only joins credited to this inviter, or null.</param>
    /// <param name="code">Only joins through this code, or null.</param>
    /// <param name="label">Only joins through codes carrying this label, or null.</param>
    /// <returns>CSV text.</returns>
    public async Task<string> ExportInvitedListCsvAsync(IGuild guild, ulong? inviterId, string? code, string? label)
    {
        var rows = await GetInvitedRecordsAsync(guild.Id, inviterId, code, label);
        var sb = new StringBuilder();
        sb.AppendLine("joined_utc,user_id,inviter_id,invite_code,join_type,fake,fake_reason,left_utc");
        foreach (var row in rows)
        {
            sb.Append(row.DateAdded?.ToString("O") ?? "").Append(',')
                .Append(row.UserId).Append(',')
                .Append(row.InviterId).Append(',')
                .Append(Csv(row.InviteCode ?? "")).Append(',')
                .Append(((InviteJoinType)row.JoinType).ToString()).Append(',')
                .Append(row.IsFake ? "true" : "false").Append(',')
                .Append(((InviteFakeReason)row.FakeReason).ToString()).Append(',')
                .Append(row.LeftAt?.ToString("O") ?? "")
                .AppendLine();
        }

        return sb.ToString();
    }

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    #endregion
}