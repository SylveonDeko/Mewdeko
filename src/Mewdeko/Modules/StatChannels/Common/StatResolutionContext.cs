using System.Text.Json;
using DataModel;
using LinqToDB.Async;
using Mewdeko.Modules.Twitch.Common;
using Mewdeko.Modules.Twitch.Services;

namespace Mewdeko.Modules.StatChannels.Common;

/// <summary>
///     Per guild, per update-cycle scratch space. Several stat channels in the same guild routinely need the same
///     Twitch snapshot or Minecraft reading, so each external lookup is done at most once per cycle.
/// </summary>
public class StatResolutionContext
{
    private readonly SocketGuild guild;
    private CountingChannel? countingChannel;
    private bool countingLoaded;
    private (string Raider, int Viewers)? lastRaid;
    private bool minecraftLoaded;
    private MinecraftServerSnapshot? minecraftSnapshot;
    private bool twitchLoaded;
    private TwitchChannelSnapshot? twitchSnapshot;

    /// <summary>
    ///     Creates a resolution context for a guild.
    /// </summary>
    /// <param name="guild">The guild being resolved.</param>
    public StatResolutionContext(SocketGuild guild)
    {
        this.guild = guild;
    }

    /// <summary>
    ///     Gets the guild's Twitch snapshot, fetching it at most once per cycle.
    /// </summary>
    /// <param name="twitchService">The Twitch service.</param>
    /// <returns>The snapshot, or null when the guild has no linked Twitch channel.</returns>
    public async Task<TwitchChannelSnapshot?> GetTwitchSnapshotAsync(TwitchService twitchService)
    {
        if (twitchLoaded) return twitchSnapshot;
        twitchLoaded = true;
        twitchSnapshot = await twitchService.GetChannelSnapshotAsync(guild.Id);
        return twitchSnapshot;
    }

    /// <summary>
    ///     Gets the most recent recorded Twitch raid for the guild.
    /// </summary>
    /// <param name="dbFactory">The data connection factory.</param>
    /// <returns>The raider name and viewer count, or a placeholder when there has been no raid.</returns>
    public async Task<(string Raider, int Viewers)> GetLastRaidAsync(IDataConnectionFactory dbFactory)
    {
        if (lastRaid.HasValue) return lastRaid.Value;

        await using var db = await dbFactory.CreateConnectionAsync();
        var raid = await db.TwitchEventHistory
            .Where(e => e.GuildId == guild.Id && e.EventType == "channel.raid")
            .OrderByDescending(e => e.DateAdded)
            .FirstOrDefaultAsync();

        lastRaid = ParseRaid(raid?.RawPayload);
        return lastRaid.Value;
    }

    /// <summary>
    ///     Pulls the raider name and viewer count out of a stored EventSub payload. The history row's message column is
    ///     a generic notification string, so the payload is the only place the raider is actually named.
    /// </summary>
    private static (string Raider, int Viewers) ParseRaid(string? rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
            return ("Nobody yet", 0);

        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            if (!document.RootElement.TryGetProperty("event", out var evt))
                return ("Nobody yet", 0);

            var raider = evt.TryGetProperty("from_broadcaster_user_name", out var name)
                ? name.GetString()
                : null;
            var viewers = evt.TryGetProperty("viewers", out var count) && count.TryGetInt32(out var parsed)
                ? parsed
                : 0;

            return (string.IsNullOrWhiteSpace(raider) ? "Nobody yet" : raider, viewers);
        }
        catch (JsonException)
        {
            return ("Nobody yet", 0);
        }
    }

    /// <summary>
    ///     Gets the latest snapshot for a watched Minecraft server.
    /// </summary>
    /// <param name="dbFactory">The data connection factory.</param>
    /// <param name="serverId">The Minecraft server row ID, or null to use the guild's default server.</param>
    /// <returns>The latest snapshot, or null when no reading exists.</returns>
    public async Task<MinecraftServerSnapshot?> GetMinecraftSnapshotAsync(IDataConnectionFactory dbFactory,
        ulong? serverId)
    {
        if (minecraftLoaded) return minecraftSnapshot;
        minecraftLoaded = true;

        await using var db = await dbFactory.CreateConnectionAsync();
        var server = serverId.HasValue
            ? await db.MinecraftServers.FirstOrDefaultAsync(s =>
                s.GuildId == guild.Id && s.Id == (int)serverId.Value)
            : await db.MinecraftServers
                .Where(s => s.GuildId == guild.Id)
                .OrderByDescending(s => s.IsDefault)
                .FirstOrDefaultAsync();

        if (server == null) return null;

        minecraftSnapshot = await db.MinecraftServerSnapshots
            .Where(s => s.ServerId == server.Id)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync();

        return minecraftSnapshot;
    }

    /// <summary>
    ///     Gets the counting channel a stat channel is pointed at.
    /// </summary>
    /// <param name="dbFactory">The data connection factory.</param>
    /// <param name="channelId">The counting channel ID, or null to use the guild's first active counting channel.</param>
    /// <returns>The counting channel, or null when none is configured.</returns>
    public async Task<CountingChannel?> GetCountingChannelAsync(IDataConnectionFactory dbFactory, ulong? channelId)
    {
        if (countingLoaded) return countingChannel;
        countingLoaded = true;

        await using var db = await dbFactory.CreateConnectionAsync();
        countingChannel = channelId.HasValue
            ? await db.CountingChannels.FirstOrDefaultAsync(c =>
                c.GuildId == guild.Id && c.ChannelId == channelId.Value)
            : await db.CountingChannels
                .Where(c => c.GuildId == guild.Id && c.IsActive)
                .FirstOrDefaultAsync();

        return countingChannel;
    }
}