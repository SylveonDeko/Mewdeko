using System.Threading.Tasks;
using Mewdeko.Services.Settings;

namespace Mewdeko.Services;

public class GuildSettingsService : INService
{
    private readonly DbService db;
    private readonly BotConfigService bss;
    private readonly ConcurrentDictionary<ulong, GuildConfig> guildConfigs;

    public GuildSettingsService(DbService db, BotConfigService bss, DiscordSocketClient client)
    {
        this.db = db;
        this.bss = bss;
        using var uow = db.GetDbContext();
        var guildIds = client.Guilds.Select(x => x.Id);
        var configs = uow.GuildConfigs.Where(x => guildIds.Contains(x.GuildId));
        guildConfigs = configs.ToDictionary(x => x.GuildId, x => x).ToConcurrent();
    }

    public async Task<string> SetPrefix(IGuild guild, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentNullException(nameof(prefix));
        if (guild == null)
            throw new ArgumentNullException(nameof(guild));

        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.Prefix = prefix;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        guildConfigs.AddOrUpdate(guild.Id, gc, (_, _) => gc);
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
        if (guildConfigs.TryGetValue(guildId, out var cachedConfig)) return cachedConfig;
        var config = await uow.ForGuildId(guildId);
        guildConfigs.AddOrUpdate(guildId, config, (_, _) => config);
        return config;
    }

    public void UpdateGuildConfig(ulong guildId, GuildConfig config)
        => guildConfigs.AddOrUpdate(guildId, config, (_, _) => config);
}