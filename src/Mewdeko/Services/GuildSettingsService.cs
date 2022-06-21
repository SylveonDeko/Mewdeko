using Mewdeko.Services.Settings;

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

    public string SetPrefix(IGuild guild, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentNullException(nameof(prefix));
        if (guild == null)
            throw new ArgumentNullException(nameof(guild));

        using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.Prefix = prefix;
        uow.SaveChanges();
        UpdateGuildConfig(guild.Id, gc);
        return prefix;
    }

    public string GetPrefix(IGuild? guild) => GetPrefix(guild?.Id);

    public string GetPrefix(ulong? id = null)
    {
        if (id is null)
            return _bss.GetSetting("prefix");
        return GetGuildConfig(id.Value).Prefix ??= _bss.GetSetting("prefix");
    }

    public GuildConfig GetGuildConfig(ulong guildId)
    {
        using var uow = _db.GetDbContext();
        var cachedConfig = _cache.GetGuildConfig(guildId);
        return cachedConfig ?? uow.ForGuildId(guildId);
    }

    public void UpdateGuildConfig(ulong guildId, GuildConfig config)
        => _cache.AddOrUpdateGuildConfig(guildId, config);
}