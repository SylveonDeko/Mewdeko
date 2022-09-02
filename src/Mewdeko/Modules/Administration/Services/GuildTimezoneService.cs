using System.Threading.Tasks;

namespace Mewdeko.Modules.Administration.Services;

public class GuildTimezoneService : INService
{
    private readonly DbService _db;
    private readonly ConcurrentDictionary<ulong, TimeZoneInfo> _timezones;

    public GuildTimezoneService(DiscordSocketClient client, Mewdeko bot, DbService db)
    {
        using var uow = db.GetDbContext();
        _timezones = uow.GuildConfigs.All().Where(x => client.Guilds.Select(x => x.Id).Contains(x.GuildId))
            .Select(GetTimzezoneTuple)
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            .Where(x => x.Timezone != null)
            .ToDictionary(x => x.GuildId, x => x.Timezone)
            .ToConcurrent();

        var curUser = client.CurrentUser;
        if (curUser != null)
            AllServices.TryAdd(curUser.Id, this);
        _db = db;

        bot.JoinedGuild += Bot_JoinedGuild;
    }

    public static ConcurrentDictionary<ulong, GuildTimezoneService> AllServices { get; } = new();

    private Task Bot_JoinedGuild(GuildConfig arg)
    {
        var (guildId, tz) = GetTimzezoneTuple(arg);
        if (tz != null)
            _timezones.TryAdd(guildId, tz);
        return Task.CompletedTask;
    }

    private static (ulong GuildId, TimeZoneInfo? Timezone) GetTimzezoneTuple(GuildConfig x)
    {
        TimeZoneInfo tz;
        try
        {
            if (x.TimeZoneId == null)
                tz = null;
            else
                tz = TimeZoneInfo.FindSystemTimeZoneById(x.TimeZoneId);
        }
        catch
        {
            tz = null;
        }

        return (x.GuildId, Timezone: tz);
    }

    public TimeZoneInfo? GetTimeZoneOrDefault(ulong guildId) 
        => _timezones.TryGetValue(guildId, out var tz) ? tz : null;

    public async Task SetTimeZone(ulong guildId, TimeZoneInfo? tz)
    {
        await using var uow = _db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);

        gc.TimeZoneId = tz?.Id;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        if (tz == null)
            _timezones.TryRemove(guildId, out tz);
        else
            _timezones.AddOrUpdate(guildId, tz, (_, _) => tz);
    }

    public TimeZoneInfo GetTimeZoneOrUtc(ulong guildId) => GetTimeZoneOrDefault(guildId) ?? TimeZoneInfo.Utc;
}