using Mewdeko.Services.Settings;
using System.Threading.Tasks;

namespace Mewdeko.Services;

public class GuildSettingsService : INService
{
    private readonly IDataCache _cache;
    private readonly DbService _db;
    private readonly BotConfigService _bss;

    public GuildSettingsService(IDataCache cache, DbService db, BotConfigService bss)
    {
        _cache = cache;
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
        UpdateGuildConfig(guild.Id, gc);
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
        var cachedConfig = _cache.GetGuildConfig(guildId);
        return cachedConfig ?? await uow.ForGuildId(guildId);
    }

    public void UpdateGuildConfig(ulong guildId, GuildConfig config)
        => _cache.AddOrUpdateGuildConfig(guildId, config);
}