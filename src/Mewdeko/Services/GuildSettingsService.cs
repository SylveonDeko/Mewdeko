using Mewdeko.Services.Settings;

namespace Mewdeko.Services;

public class GuildSettingsService : INService
{
    private readonly DbService db;
    private readonly BotConfigService bss;
    private readonly Mewdeko bot;

    public GuildSettingsService(DbService db, BotConfigService bss, Mewdeko bot)
    {
        this.db = db;
        this.bss = bss;
        this.bot = bot;
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
        var configs = bot.AllGuildConfigs;
        var toReturn = configs.FirstOrDefault(x => x.GuildId == guildId);
        if (toReturn is not null) return toReturn;
        {
            await using var uow = db.GetDbContext();
            var toLoad = uow.GuildConfigs.IncludeEverything().FirstOrDefault(x => x.GuildId == guildId);
            configs.Add(toLoad);
            bot.AllGuildConfigs = configs;
            return toLoad;
        }
    }

    public async Task UpdateGuildConfig(ulong guildId, GuildConfig toUpdate)
    {
        await using var uow = db.GetDbContext();
        var configs = bot.AllGuildConfigs;
        var old = configs.FirstOrDefault(x => x.GuildId == guildId);

        if (old is not null)
        {
            configs.TryRemove(old);
            var properties = typeof(GuildConfig).GetProperties();
            foreach (var property in properties)
            {
                var oldValue = property.GetValue(old);
                var newValue = property.GetValue(toUpdate);

                if (newValue != null && !newValue.Equals(oldValue))
                {
                    property.SetValue(old, newValue);
                }
            }

            configs.Add(old);
            bot.AllGuildConfigs = configs;
            uow.GuildConfigs.Update(old);
            await uow.SaveChangesAsync();
        }
        else
        {
            var config = uow.GuildConfigs.IncludeEverything().FirstOrDefault(x => x.Id == toUpdate.Id);

            if (config != null)
            {
                var properties = typeof(GuildConfig).GetProperties();
                foreach (var property in properties)
                {
                    var oldValue = property.GetValue(config);
                    var newValue = property.GetValue(toUpdate);

                    if (newValue != null && !newValue.Equals(oldValue))
                    {
                        property.SetValue(config, newValue);
                    }
                }

                uow.GuildConfigs.Update(config);
                await uow.SaveChangesAsync();
                configs.Add(config);
                bot.AllGuildConfigs = configs;
            }
        }
    }
}