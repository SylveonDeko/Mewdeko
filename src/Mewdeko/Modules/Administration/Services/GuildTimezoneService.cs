namespace Mewdeko.Modules.Administration.Services;

/// <summary>
/// Service for managing guild timezones.
/// </summary>
public class GuildTimezoneService : INService
{
    private readonly DbService db;
    private readonly ConcurrentDictionary<ulong, TimeZoneInfo> timezones;

    /// <summary>
    /// Constructs a new instance of the GuildTimezoneService.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="bot">The Mewdeko bot.</param>
    /// <param name="db">The database service.</param>
    public GuildTimezoneService(DiscordSocketClient client, Mewdeko bot, DbService db)
    {
        using var uow = db.GetDbContext();
        var allgc = bot.AllGuildConfigs;
        timezones = allgc
            .Select(GetTimzezoneTuple)
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            .Where(x => x.Timezone != null)
            .ToDictionary(x => x.GuildId, x => x.Timezone)
            .ToConcurrent();

        var curUser = client.CurrentUser;
        if (curUser != null)
            AllServices.TryAdd(curUser.Id, this);
        this.db = db;

        bot.JoinedGuild += Bot_JoinedGuild;
    }

    /// <summary>
    /// A dictionary of all GuildTimezoneService instances.
    /// </summary>
    public static ConcurrentDictionary<ulong, GuildTimezoneService> AllServices { get; } = new();

    /// <summary>
    /// Handles the JoinedGuild event.
    /// </summary>
    /// <param name="arg">The guild configuration.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task Bot_JoinedGuild(GuildConfig arg)
    {
        var (guildId, tz) = GetTimzezoneTuple(arg);
        if (tz != null)
            timezones.TryAdd(guildId, tz);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the timezone tuple for a guild configuration.
    /// </summary>
    /// <param name="x">The guild configuration.</param>
    /// <returns>A tuple containing the guild ID and the timezone.</returns>
    private static (ulong GuildId, TimeZoneInfo? Timezone) GetTimzezoneTuple(GuildConfig x)
    {
        TimeZoneInfo tz;
        try
        {
            tz = x.TimeZoneId == null ? null : TimeZoneInfo.FindSystemTimeZoneById(x.TimeZoneId);
        }
        catch
        {
            tz = null;
        }

        return (x.GuildId, Timezone: tz);
    }

    /// <summary>
    /// Gets the timezone for a guild, or null if no timezone is set.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The timezone for the guild, or null if no timezone is set.</returns>
    public TimeZoneInfo? GetTimeZoneOrDefault(ulong guildId)
        => timezones.TryGetValue(guildId, out var tz) ? tz : null;

    /// <summary>
    /// Sets the timezone for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="tz">The timezone to set.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetTimeZone(ulong guildId, TimeZoneInfo? tz)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);

        gc.TimeZoneId = tz?.Id;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        if (tz == null)
            timezones.TryRemove(guildId, out tz);
        else
            timezones.AddOrUpdate(guildId, tz, (_, _) => tz);
    }

    /// <summary>
    /// Gets the timezone for a guild, or UTC if no timezone is set.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The timezone for the guild, or UTC if no timezone is set.</returns>
    public TimeZoneInfo GetTimeZoneOrUtc(ulong guildId) => GetTimeZoneOrDefault(guildId) ?? TimeZoneInfo.Utc;
}