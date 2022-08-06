using Mewdeko.Services.Settings;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Mewdeko.Services;

public class GuildSettingsService : INService
{
    private readonly DbService _db;
    private readonly BotConfigService _bss;
    private readonly ConcurrentDictionary<ulong, GuildConfig> _guildConfigs = new();

    public GuildSettingsService(DbService db, BotConfigService bss)
    {
        _db = db;
        _bss = bss;
    }
    
    public async Task<string> SetPrefix(IGuild guild, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentNullException(nameof(prefix));
        if (guild == null)
            throw new ArgumentNullException(nameof(guild));

        await using var uow = _db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.Prefix = prefix;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _guildConfigs.AddOrUpdate(guild.Id, gc, (_, _) => gc);
        return prefix;
    }
    
    public async Task<string?> GetPrefix(IGuild? guild) => await GetPrefix(guild.Id);

    public async Task<string?> GetPrefix(ulong id = 0)
    {
        if (id is 0)
            return _bss.GetSetting("prefix");
        return (await GetGuildConfig(id)).Prefix ??= _bss.GetSetting("prefix");
    }
    
    public async Task<GuildConfig> GetGuildConfig(ulong guildId)
    {
        await using var uow = _db.GetDbContext();
        if (_guildConfigs.TryGetValue(guildId, out var cachedConfig)) return cachedConfig;
        var config = await uow.ForGuildId(guildId);
        _guildConfigs.AddOrUpdate(guildId, config, (_, _) => config);
        return config;

    }

    public void UpdateGuildConfig(ulong guildId, GuildConfig config)
        => _guildConfigs.AddOrUpdate(guildId, config, (_, _) => config);
}