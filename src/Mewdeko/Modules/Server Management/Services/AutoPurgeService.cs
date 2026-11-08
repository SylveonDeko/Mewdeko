using System.Text.RegularExpressions;
using System.Threading;
using DataModel;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Server_Management.Services;

/// <summary>
///     Why a set of auto-purge settings was refused.
/// </summary>
public enum AutoPurgeError
{
    /// <summary>
    ///     Nothing is wrong.
    /// </summary>
    None,

    /// <summary>
    ///     The age is outside 1 minute to 365 days.
    /// </summary>
    InvalidAge,

    /// <summary>
    ///     The interval is outside 5 minutes to 1 day.
    /// </summary>
    InvalidInterval
}

/// <summary>
///     Deletes old messages from channels on a schedule.
/// </summary>
public partial class AutoPurgeService : INService, IReadyExecutor
{
    /// <summary>
    ///     The longest message age allowed, in minutes.
    /// </summary>
    public const int MaxAgeMinutes = 525600;

    /// <summary>
    ///     The shortest interval allowed, in minutes.
    /// </summary>
    public const int MinIntervalMinutes = 5;

    /// <summary>
    ///     The longest interval allowed, in minutes.
    /// </summary>
    public const int MaxIntervalMinutes = 1440;

    private const int ScanLimit = 1000;
    private const int OldDeleteLimit = 100;
    private static readonly TimeSpan BulkWindow = TimeSpan.FromDays(14).Subtract(TimeSpan.FromMinutes(5));

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<AutoPurgeService> logger;
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> running = new();
    private readonly GeneratedBotStrings strings;
    private readonly GuildOwnershipService ownership;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AutoPurgeService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="strings">The generated bot strings.</param>
    public AutoPurgeService(DiscordShardedClient client, IDataConnectionFactory dbFactory, EventHandler eventHandler,
        ILogger<AutoPurgeService> logger, GeneratedBotStrings strings, GuildOwnershipService ownership)
    {
        this.ownership = ownership;
        this.strings = strings;
        this.client = client;
        this.dbFactory = dbFactory;
        this.logger = logger;
        eventHandler.Subscribe("ChannelDestroyed", "AutoPurgeService", OnChannelDestroyed);
    }

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();

    /// <summary>
    ///     Starts the loop that purges channels when they are due.
    /// </summary>
    public Task OnReadyAsync()
    {
        _ = Task.Run(RunLoop);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Checks the age and interval of a setting.
    /// </summary>
    /// <param name="maxAgeMinutes">How old a message must be to be deleted.</param>
    /// <param name="intervalMinutes">How often the channel is purged.</param>
    public static AutoPurgeError Validate(int maxAgeMinutes, int intervalMinutes)
    {
        if (maxAgeMinutes is < 1 or > MaxAgeMinutes)
            return AutoPurgeError.InvalidAge;
        if (intervalMinutes is < MinIntervalMinutes or > MaxIntervalMinutes)
            return AutoPurgeError.InvalidInterval;
        return AutoPurgeError.None;
    }

    /// <summary>
    ///     Describes a setting for a list.
    /// </summary>
    /// <param name="setting">The setting.</param>
    public string Line(AutoPurgeSetting setting) =>
        strings.ApListLine(setting.GuildId, $"<#{setting.ChannelId}>", FilterName(setting),
            JoinRoleTimeoutService.FormatMinutes(setting.MaxAgeMinutes),
            JoinRoleTimeoutService.FormatMinutes(setting.IntervalMinutes));

    /// <summary>
    ///     Describes a setting after it was saved.
    /// </summary>
    /// <param name="setting">The setting.</param>
    public string Summary(AutoPurgeSetting setting) =>
        strings.ApSet(setting.GuildId, $"<#{setting.ChannelId}>", FilterName(setting),
            JoinRoleTimeoutService.FormatMinutes(setting.MaxAgeMinutes),
            JoinRoleTimeoutService.FormatMinutes(setting.IntervalMinutes),
            setting.KeepPinned ? strings.ApPinnedKept(setting.GuildId) : strings.ApPinnedDeleted(setting.GuildId));

    private string FilterName(AutoPurgeSetting setting) => setting.Filter switch
    {
        AutoPurgeFilter.Humans => strings.ApFilterHumans(setting.GuildId),
        AutoPurgeFilter.Bots => strings.ApFilterBots(setting.GuildId),
        AutoPurgeFilter.Links => strings.ApFilterLinks(setting.GuildId),
        AutoPurgeFilter.Attachments => strings.ApFilterAttachments(setting.GuildId),
        _ => strings.ApFilterAll(setting.GuildId)
    };

    /// <summary>
    ///     Gets the auto-purge settings of a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public async Task<List<AutoPurgeSetting>> GetAllAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.AutoPurgeSettings.Where(x => x.GuildId == guildId).OrderBy(x => x.ChannelId).ToListAsync();
    }

    /// <summary>
    ///     Turns on auto-purge for a channel or changes its settings.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="maxAgeMinutes">How old a message must be to be deleted.</param>
    /// <param name="intervalMinutes">How often the channel is purged.</param>
    /// <param name="filter">Which messages are deleted.</param>
    /// <param name="keepPinned">Whether pinned messages are kept.</param>
    /// <returns>The error, or none, and the setting as it is now.</returns>
    public async Task<(AutoPurgeError Error, AutoPurgeSetting? Setting)> SetAsync(ulong guildId, ulong channelId,
        int maxAgeMinutes, int intervalMinutes, AutoPurgeFilter filter, bool keepPinned)
    {
        var error = Validate(maxAgeMinutes, intervalMinutes);
        if (error != AutoPurgeError.None)
            return (error, null);

        await using var db = await dbFactory.CreateConnectionAsync();
        var setting = await db.AutoPurgeSettings.FirstOrDefaultAsync(x => x.ChannelId == channelId);
        var isNew = setting is null;
        setting ??= new AutoPurgeSetting
        {
            GuildId = guildId, ChannelId = channelId, DateAdded = DateTime.UtcNow
        };
        setting.MaxAgeMinutes = maxAgeMinutes;
        setting.IntervalMinutes = intervalMinutes;
        setting.Filter = filter;
        setting.KeepPinned = keepPinned;

        if (isNew)
            await db.InsertAsync(setting);
        else
            await db.UpdateAsync(setting);

        return (AutoPurgeError.None, setting);
    }

    /// <summary>
    ///     Turns off auto-purge for a channel.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>False if the channel had no auto-purge.</returns>
    public async Task<bool> RemoveAsync(ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.AutoPurgeSettings.Where(x => x.ChannelId == channelId).DeleteAsync() > 0;
    }

    /// <summary>
    ///     Purges a channel now with its saved settings.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>How many messages were deleted, or null if the channel has no auto-purge or cannot be purged.</returns>
    public async Task<int?> RunNowAsync(ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var setting = await db.AutoPurgeSettings.FirstOrDefaultAsync(x => x.ChannelId == channelId);
        if (setting is null)
            return null;

        var deleted = await PurgeAsync(setting);
        await db.AutoPurgeSettings.Where(x => x.Id == setting.Id)
            .Set(x => x.LastRunAt, DateTime.UtcNow).UpdateAsync();
        return deleted;
    }

    private static bool Matches(IMessage message, AutoPurgeSetting setting)
    {
        if (setting.KeepPinned && message.IsPinned)
            return false;

        return setting.Filter switch
        {
            AutoPurgeFilter.Humans => !message.Author.IsBot,
            AutoPurgeFilter.Bots => message.Author.IsBot,
            AutoPurgeFilter.Links => LinkRegex().IsMatch(message.Content ?? ""),
            AutoPurgeFilter.Attachments => message.Attachments.Count > 0,
            _ => true
        };
    }

    private async Task<int?> PurgeAsync(AutoPurgeSetting setting)
    {
        var channel = client.GetGuild(setting.GuildId)?.GetTextChannel(setting.ChannelId);
        if (channel is null)
            return null;

        var gate = running.GetOrAdd(setting.ChannelId, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0))
            return 0;

        try
        {
            var perms = channel.Guild.CurrentUser.GetPermissions(channel);
            if (!perms.ViewChannel || !perms.ReadMessageHistory || !perms.ManageMessages)
                return null;

            var now = DateTime.UtcNow;
            var cutoff = now.AddMinutes(-setting.MaxAgeMinutes);
            var messages = (await channel
                    .GetMessagesAsync(SnowflakeUtils.ToSnowflake(cutoff), Direction.Before, ScanLimit)
                    .FlattenAsync())
                .Where(x => Matches(x, setting))
                .ToList();

            var recent = messages.Where(x => now - x.Timestamp.UtcDateTime < BulkWindow).ToList();
            var old = messages.Except(recent).Take(OldDeleteLimit).ToList();
            var deleted = 0;

            foreach (var chunk in recent.Chunk(100))
            {
                try
                {
                    if (chunk.Length == 1)
                        await chunk[0].DeleteAsync();
                    else
                        await channel.DeleteMessagesAsync(chunk);
                    deleted += chunk.Length;
                }
                catch (HttpException ex)
                {
                    logger.LogWarning(ex, "Auto-purge could not delete messages in {ChannelId}", channel.Id);
                }
            }

            foreach (var message in old)
            {
                try
                {
                    await message.DeleteAsync();
                    deleted++;
                }
                catch (HttpException ex)
                {
                    logger.LogDebug(ex, "Auto-purge could not delete {MessageId} in {ChannelId}", message.Id,
                        channel.Id);
                }
            }

            return deleted;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task OnChannelDestroyed(SocketChannel channel)
    {
        try
        {
            await RemoveAsync(channel.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not clear the auto-purge of {ChannelId}", channel.Id);
        }
    }

    private async Task RunLoop()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (await timer.WaitForNextTickAsync())
        {
            try
            {
                await ProcessAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Auto-purge pass failed");
            }
        }
    }

    private async Task ProcessAsync()
    {
        List<AutoPurgeSetting> all;
        await using (var db = await dbFactory.CreateConnectionAsync())
            all = await db.AutoPurgeSettings.ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var setting in all.Where(x => x.LastRunAt is null || x.LastRunAt.Value.AddMinutes(x.IntervalMinutes) <= now))
        {
            if (!ownership.IsMine(setting.GuildId) || client.GetGuild(setting.GuildId) is null)
                continue;

            try
            {
                await PurgeAsync(setting);
                await using var db = await dbFactory.CreateConnectionAsync();
                await db.AutoPurgeSettings.Where(x => x.Id == setting.Id)
                    .Set(x => x.LastRunAt, DateTime.UtcNow).UpdateAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Auto-purge failed in {ChannelId}", setting.ChannelId);
            }
        }
    }
}
