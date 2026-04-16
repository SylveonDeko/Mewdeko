using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.StatChannels.Common;
using Microsoft.Extensions.Caching.Memory;

namespace Mewdeko.Modules.StatChannels.Services;

/// <summary>
///     Service for managing stat channels that display live server statistics in voice channel names.
/// </summary>
public class StatChannelService : INService, IReadyExecutor, IDisposable
{
    private const string CacheKey = "stat_channels_{0}";
    private const int UpdateIntervalMinutes = 5;
    private readonly IMemoryCache cache;
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<StatChannelService> logger;
    private readonly SemaphoreSlim updateSemaphore = new(1, 1);
    private bool isDisposed;
    private Timer? updateTimer;

    /// <summary>
    ///     Creates a new stat channel service.
    /// </summary>
    public StatChannelService(
        IDataConnectionFactory dbFactory,
        DiscordShardedClient client,
        IMemoryCache cache,
        ILogger<StatChannelService> logger)
    {
        this.dbFactory = dbFactory;
        this.client = client;
        this.cache = cache;
        this.logger = logger;
    }

    /// <summary>
    ///     Disposes the update timer.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;
        updateTimer?.Dispose();
        updateSemaphore.Dispose();
    }

    /// <summary>
    ///     Initializes the update timer when the bot is ready.
    /// </summary>
    public Task OnReadyAsync()
    {
        updateTimer = new Timer(_ => _ = UpdateAllStatChannelsAsync(), null,
            TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(UpdateIntervalMinutes));
        logger.LogInformation("Stat Channel Service Ready");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Creates a new locked voice channel and registers it as a stat channel.
    /// </summary>
    /// <param name="guild">The Discord guild.</param>
    /// <param name="statType">The stat type to display.</param>
    /// <param name="template">The display template.</param>
    /// <param name="categoryId">The category to create the channel in, or null for no category.</param>
    /// <param name="roleId">The role ID for role member counts.</param>
    /// <param name="countdownDate">The countdown target date.</param>
    /// <param name="goalTarget">The member goal target.</param>
    /// <returns>The created stat channel and the new voice channel.</returns>
    public async Task<(StatChannel StatChannel, IVoiceChannel VoiceChannel)> CreateStatChannelAsync(
        IGuild guild, StatChannelType statType, string template, ulong? categoryId = null,
        ulong? roleId = null, DateTime? countdownDate = null, int goalTarget = 0)
    {
        var initialName = template.Replace("%count%", "...").Replace("%days%", "...").Replace("%goal%", "...");
        if (initialName.Length > 100) initialName = initialName[..100];

        var voiceChannel = await guild.CreateVoiceChannelAsync(initialName, props =>
        {
            if (categoryId.HasValue)
                props.CategoryId = categoryId;
        });

        await voiceChannel.AddPermissionOverwriteAsync(guild.EveryoneRole,
            new OverwritePermissions(connect: PermValue.Deny));

        var statChannel = await AddStatChannelAsync(guild.Id, voiceChannel.Id, statType, template,
            roleId, countdownDate, goalTarget);

        return (statChannel, voiceChannel);
    }

    /// <summary>
    ///     Adds a stat channel to a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The voice channel ID.</param>
    /// <param name="statType">The stat type to display.</param>
    /// <param name="template">The display template.</param>
    /// <param name="roleId">The role ID for role member counts.</param>
    /// <param name="countdownDate">The countdown target date.</param>
    /// <param name="goalTarget">The member goal target.</param>
    /// <returns>The created stat channel.</returns>
    public async Task<StatChannel> AddStatChannelAsync(ulong guildId, ulong channelId, StatChannelType statType,
        string template, ulong? roleId = null, DateTime? countdownDate = null, int goalTarget = 0)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = await db.StatChannels.FirstOrDefaultAsync(s => s.ChannelId == channelId);
        if (existing != null)
            throw new InvalidOperationException("This channel is already a stat channel.");

        var statChannel = new StatChannel
        {
            GuildId = guildId,
            ChannelId = channelId,
            StatType = (int)statType,
            Template = template,
            RoleId = roleId,
            CountdownDate = countdownDate,
            GoalTarget = goalTarget,
            DateAdded = DateTime.UtcNow
        };

        statChannel.Id = await db.InsertWithInt32IdentityAsync(statChannel);
        InvalidateCache(guildId);

        _ = UpdateStatChannelAsync(statChannel);

        return statChannel;
    }

    /// <summary>
    ///     Removes a stat channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID to remove.</param>
    /// <returns>True if removed.</returns>
    public async Task<bool> RemoveStatChannelAsync(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var deleted = await db.StatChannels
            .Where(s => s.GuildId == guildId && s.ChannelId == channelId)
            .DeleteAsync();

        if (deleted > 0)
            InvalidateCache(guildId);

        return deleted > 0;
    }

    /// <summary>
    ///     Gets all stat channels for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The list of stat channels.</returns>
    public async Task<List<StatChannel>> GetStatChannelsAsync(ulong guildId)
    {
        var cacheKey = string.Format(CacheKey, guildId);
        if (cache.TryGetValue(cacheKey, out List<StatChannel>? cached) && cached != null)
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var channels = await db.StatChannels
            .Where(s => s.GuildId == guildId)
            .ToListAsync();

        cache.Set(cacheKey, channels, TimeSpan.FromMinutes(10));
        return channels;
    }

    /// <summary>
    ///     Updates the template for a stat channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="template">The new template.</param>
    /// <returns>The updated stat channel, or null if not found.</returns>
    public async Task<StatChannel?> UpdateTemplateAsync(ulong guildId, ulong channelId, string template)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var sc = await db.StatChannels
            .FirstOrDefaultAsync(s => s.GuildId == guildId && s.ChannelId == channelId);

        if (sc == null) return null;

        sc.Template = template;
        await db.UpdateAsync(sc);
        InvalidateCache(guildId);

        _ = UpdateStatChannelAsync(sc);

        return sc;
    }

    /// <summary>
    ///     Resolves the current stat value for a stat channel.
    /// </summary>
    /// <param name="sc">The stat channel config.</param>
    /// <param name="guild">The Discord guild.</param>
    /// <returns>The resolved channel name.</returns>
    public string ResolveStatValue(StatChannel sc, SocketGuild guild)
    {
        var type = (StatChannelType)sc.StatType;
        var count = type switch
        {
            StatChannelType.TotalMembers => guild.MemberCount,
            StatChannelType.HumanMembers => guild.Users.Count(u => !u.IsBot),
            StatChannelType.BotCount => guild.Users.Count(u => u.IsBot),
            StatChannelType.OnlineMembers => guild.Users.Count(u => u.Status != UserStatus.Offline),
            StatChannelType.RoleMembers when sc.RoleId.HasValue =>
                guild.GetRole(sc.RoleId.Value)?.Members.Count() ?? 0,
            StatChannelType.ChannelCount => guild.Channels.Count,
            StatChannelType.RoleCount => guild.Roles.Count,
            StatChannelType.BoostCount => guild.PremiumSubscriptionCount,
            StatChannelType.BoostLevel => (int)guild.PremiumTier,
            StatChannelType.EmojiCount => guild.Emotes.Count,
            StatChannelType.Countdown when sc.CountdownDate.HasValue =>
                Math.Max(0, (int)(sc.CountdownDate.Value - DateTime.UtcNow).TotalDays),
            StatChannelType.MemberGoal when sc.GoalTarget > 0 => guild.MemberCount,
            _ => 0
        };

        var template = sc.Template
            .Replace("%count%", count.ToString("N0"))
            .Replace("%count.raw%", count.ToString())
            .Replace("%server.name%", guild.Name)
            .Replace("%server.id%", guild.Id.ToString())
            .Replace("%server.boostcount%", guild.PremiumSubscriptionCount.ToString())
            .Replace("%server.boostlevel%", ((int)guild.PremiumTier).ToString())
            .Replace("%server.members%", guild.MemberCount.ToString("N0"));

        if (type == StatChannelType.MemberGoal && sc.GoalTarget > 0)
        {
            template = template
                .Replace("%goal%", sc.GoalTarget.ToString("N0"))
                .Replace("%goal.raw%", sc.GoalTarget.ToString())
                .Replace("%goal.percent%", $"{(double)count / sc.GoalTarget * 100:F0}%");
        }

        if (type == StatChannelType.Countdown && sc.CountdownDate.HasValue)
        {
            var remaining = sc.CountdownDate.Value - DateTime.UtcNow;
            template = template
                .Replace("%days%", Math.Max(0, (int)remaining.TotalDays).ToString())
                .Replace("%hours%", Math.Max(0, (int)remaining.TotalHours).ToString());
        }

        if (type == StatChannelType.RoleMembers && sc.RoleId.HasValue)
        {
            var role = guild.GetRole(sc.RoleId.Value);
            template = template.Replace("%role.name%", role?.Name ?? "Unknown");
        }

        return template.Length > 100 ? template[..100] : template;
    }

    private async Task UpdateAllStatChannelsAsync()
    {
        if (!await updateSemaphore.WaitAsync(0))
            return;

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var allStatChannels = await db.StatChannels.ToListAsync();

            var grouped = allStatChannels.GroupBy(s => s.GuildId);

            foreach (var group in grouped)
            {
                var guild = client.GetGuild(group.Key);
                if (guild == null) continue;

                foreach (var sc in group)
                {
                    try
                    {
                        await UpdateStatChannelAsync(sc, guild);
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Failed to update stat channel {ChannelId} in guild {GuildId}",
                            sc.ChannelId, sc.GuildId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating stat channels");
        }
        finally
        {
            updateSemaphore.Release();
        }
    }

    private async Task UpdateStatChannelAsync(StatChannel sc, SocketGuild? guild = null)
    {
        guild ??= client.GetGuild(sc.GuildId);
        if (guild == null) return;

        var channel = guild.GetVoiceChannel(sc.ChannelId);
        if (channel == null) return;

        var newName = ResolveStatValue(sc, guild);
        if (channel.Name == newName) return;

        try
        {
            await channel.ModifyAsync(c => c.Name = newName);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to rename stat channel {ChannelId}", sc.ChannelId);
        }
    }

    private void InvalidateCache(ulong guildId)
    {
        cache.Remove(string.Format(CacheKey, guildId));
    }
}