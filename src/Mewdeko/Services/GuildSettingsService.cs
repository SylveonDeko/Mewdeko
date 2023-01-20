using System.Threading.Tasks;
using Mewdeko.Services.Settings;

namespace Mewdeko.Services;

public class GuildSettingsService : INService
{
    private readonly DbService db;
    private readonly BotConfigService bss;
    private readonly IDataCache cache;

    public GuildSettingsService(DbService db, BotConfigService bss, DiscordSocketClient client, IDataCache cache)
    {
        this.db = db;
        this.bss = bss;
        this.cache = cache;
        using var uow = db.GetDbContext();
        var guildIds = client.Guilds.Select(x => x.Id);
        var configs = uow.GuildConfigs.Where(x => guildIds.Contains(x.GuildId));
        cache.SetGuildConfigs(configs.ToList());
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
        await using var uow = db.GetDbContext();
        var configs = cache.GetGuildConfigs();
        if (configs.FirstOrDefault(x => x.GuildId == guildId) is { } guildConfig)
            return guildConfig;
        var config = await uow.ForGuildId(guildId);
        configs.Add(config);
        await cache.SetGuildConfigs(configs);
        return config;
    }

    public void UpdateGuildConfig(ulong guildId, GuildConfig config)
    {
        using var uow = db.GetDbContext();
        var configs = cache.GetGuildConfigs();
        if (configs.FirstOrDefault(x => x.GuildId == guildId) is { } guildConfig)
            configs.Remove(guildConfig);
        configs.Add(config);
        cache.SetGuildConfigs(configs);
        uow.GuildConfigs.Update(config);
        uow.SaveChanges();
    }
}