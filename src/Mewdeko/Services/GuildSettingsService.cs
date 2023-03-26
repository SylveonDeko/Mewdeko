using System.Threading.Tasks;
using Mewdeko.Services.Settings;

namespace Mewdeko.Services;

public class GuildSettingsService : INService
{
    private readonly DbService db;
    private readonly BotConfigService bss;
    private readonly IDataCache cache;

    public GuildSettingsService(DbService db, BotConfigService bss, IDataCache cache)
    {
        this.db = db;
        this.bss = bss;
        this.cache = cache;
        using var uow = db.GetDbContext();
    }

    public async Task<string> SetPrefix(IGuild guild, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentNullException(nameof(prefix));
        if (guild == null)
            throw new ArgumentNullException(nameof(guild));

        var config = await GetGuildConfig(guild.Id);
        config.Prefix = prefix;
        UpdateGuildConfig(guild.Id, config);
        return prefix;
    }

    public async Task<string?> GetPrefix(IGuild? guild) => await GetPrefix(guild.Id);

    public async Task<string?> GetPrefix(ulong? id)
    {
        if (!id.HasValue)
            return bss.GetSetting("prefix");
        return (await GetGuildConfig(id.Value)).Prefix ??= bss.GetSetting("prefix");
    }

    public Task<string?> GetPrefix() => Task.FromResult(bss.GetSetting("prefix"));

    public async Task<GuildConfig> GetGuildConfig(ulong guildId)
    {
        var config = await cache.GetGuildConfig(guildId);
        if (config is { })
            return config;
        await using var uow = db.GetDbContext();
        var newConfig = await uow.ForGuildId(guildId);
        cache.SetGuildConfig(guildId, newConfig);
        return newConfig;
    }

    public void UpdateGuildConfig(ulong guildId, GuildConfig toUpdate)
    {
        using var uow = db.GetDbContext();
        cache.SetGuildConfig(guildId, toUpdate);
        uow.GuildConfigs.Update(toUpdate);
        uow.SaveChanges();
    }
}