using System.Diagnostics;
using System.Runtime.CompilerServices;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ZiggyCreatures.Caching.Fusion;

namespace Mewdeko.Services;

/// <summary>
///     Service for managing guild settings.
/// </summary>
public class GuildSettingsService(
    DbContextProvider dbProvider,
    IConfigService? bss,
    IServiceProvider services,
    IFusionCache cache)
{
    /// <summary>
    ///     Sets the prefix for the specified guild.
    /// </summary>
    public async Task<string> SetPrefix(IGuild guild, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentNullException(nameof(prefix));
        ArgumentNullException.ThrowIfNull(guild);

        var config = await GetGuildConfig(guild.Id);
        config.Prefix = prefix;
        await UpdateGuildConfig(guild.Id, config);
        return prefix;
    }

    /// <summary>
    ///     Gets the prefix for the specified guild.
    /// </summary>
    public async Task<string?> GetPrefix(IGuild? guild)
    {
        return await GetPrefix(guild?.Id);
    }

    /// <summary>
    ///     Gets the prefix for the guild with the specified ID.
    /// </summary>
    public async Task<string?> GetPrefix(ulong? id)
    {
        bss = services.GetRequiredService<BotConfigService>();
        if (!id.HasValue)
            return bss.GetSetting("prefix");
        var prefix = (await GetGuildConfig(id.Value)).Prefix;
        return string.IsNullOrWhiteSpace(prefix) ? bss.GetSetting("prefix") : prefix;
    }

    /// <summary>
    ///     Gets the default prefix.
    /// </summary>
    public Task<string?> GetPrefix()
    {
        return Task.FromResult(bss.GetSetting("prefix"));
    }

    /// <summary>
    ///     Gets the guild configuration for the specified guild ID.
    /// </summary>
    public async Task<GuildConfig> GetGuildConfig(ulong guildId,
        Func<DbSet<GuildConfig>, IQueryable<GuildConfig>>? includes = null, [CallerMemberName] string callerName = "",
        [CallerFilePath] string filePath = "")
    {
        try
        {
            await using var dbContext = await dbProvider.GetContextAsync();

            var sw = new Stopwatch();
            sw.Start();
            var toLoad = await dbContext.ForGuildId(guildId, includes);
            return toLoad;
        }
        catch (Exception e)
        {
            Log.Information(e.Message, "Failed to get guild config");
            throw;
        }
    }

    /// <summary>
    ///     Updates the guild configuration.
    /// </summary>
    public async Task UpdateGuildConfig(ulong guildId, GuildConfig toUpdate, [CallerMemberName] string callerName = "",
        [CallerFilePath] string filePath = "")
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var sw = new Stopwatch();
        sw.Start();
        try
        {
            dbContext.GuildConfigs.Update(toUpdate);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception e)
        {
            sw.Stop();
            Log.Error($"Executed from {callerName} in {filePath}");
            Log.Error(e, "There was an issue updating a GuildConfig");
        }
    }
}