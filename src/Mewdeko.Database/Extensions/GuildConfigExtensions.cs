using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class GuildConfigExtensions
{
    public class GeneratingChannel
    {
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; } 
    }

    public static GuildConfig[] All(this DbSet<GuildConfig> set) => set.AsQueryable().ToArray();
    public static GuildConfig ForGuildId(this MewdekoContext ctx, ulong guildId, Func<DbSet<GuildConfig>, IQueryable<GuildConfig>> includes = null)
    {
        GuildConfig config;

        if (includes is null)
        {
            config = ctx
                     .GuildConfigs
                     .IncludeEverything()
                     .FirstOrDefault(c => c.GuildId == guildId);
        }
        else
        {
            var set = includes(ctx.GuildConfigs);
            config = set.FirstOrDefault(c => c.GuildId == guildId);
        }

        if (config is null)
        {
            ctx.GuildConfigs.Add(config = new GuildConfig
            {
                GuildId = guildId,
                Permissions = Permissionv2.GetDefaultPermlist,
                WarningsInitialized = true,
                WarnPunishments = DefaultWarnPunishments,
            });
            ctx.SaveChanges();
        }

        if (config.WarningsInitialized) return config;
        config.WarningsInitialized = true;
        config.WarnPunishments = DefaultWarnPunishments;

        return config;
    }
    
    public static IEnumerable<GuildConfig> Permissionsv2ForAll(this DbSet<GuildConfig> configs, List<ulong> include)
    {
        var query = configs.AsQueryable()
                           .Where(x => include.Contains(x.GuildId))
                           .Include(gc => gc.Permissions);

        return query.ToList();
    }
    
    public static IEnumerable<GeneratingChannel> GetGeneratingChannels(this DbSet<GuildConfig> configs) =>
        configs
            .AsQueryable()
            .Include(x => x.GenerateCurrencyChannelIds)
            .Where(x => x.GenerateCurrencyChannelIds.Any())
            .SelectMany(x => x.GenerateCurrencyChannelIds)
            .Select(x => new GeneratingChannel()
            {
                ChannelId = x.ChannelId,
                GuildId = x.GuildConfig.GuildId
            })
            .ToArray();

    public static GuildConfig GcWithPermissionsv2For(this MewdekoContext ctx, ulong guildId)
    {
        var config = ctx
                     .GuildConfigs
                     .AsQueryable()
                     .Where(gc => gc.GuildId == guildId)
                     .Include(gc => gc.Permissions)
                     .FirstOrDefault();

        if (config is null) // if there is no guildconfig, create new one
        {
            ctx.GuildConfigs.Add((config = new GuildConfig
            {
                GuildId = guildId,
                Permissions = Permissionv2.GetDefaultPermlist
            }));
            ctx.SaveChanges();
        }
        else if (config.Permissions is null || !config.Permissions.Any()) // if no perms, add default ones
        {
            config.Permissions = Permissionv2.GetDefaultPermlist;
            ctx.SaveChanges();
        }

        return config;
    }
    public static StreamRoleSettings GetStreamRoleSettings(this MewdekoContext ctx, ulong guildId)
    {
        var conf = ctx.ForGuildId(guildId, set => set.Include(y => y.StreamRole)
                                                     .Include(y => y.StreamRole.Whitelist)
                                                     .Include(y => y.StreamRole.Blacklist));

        return conf.StreamRole ?? (conf.StreamRole = new StreamRoleSettings());
    }

    public static XpSettings XpSettingsFor(this MewdekoContext ctx, ulong guildId)
    {
        var gc = ctx.ForGuildId(guildId,
            set => set.Include(x => x.XpSettings)
                      .ThenInclude(x => x.RoleRewards)
                      .Include(x => x.XpSettings)
                      .ThenInclude(x => x.CurrencyRewards)
                      .Include(x => x.XpSettings)
                      .ThenInclude(x => x.ExclusionList));

        return gc.XpSettings ?? (gc.XpSettings = new XpSettings());
    }
    
    public static IEnumerable<GuildConfig> GetAllGuildConfigs(this DbSet<GuildConfig> configs, List<ulong> availableGuilds)
        => configs
           .IncludeEverything()
           .AsNoTracking()
           .Where(x => availableGuilds.Contains(x.GuildId))
           .ToList();
    public static GuildConfig LogSettingsFor(this MewdekoContext ctx, ulong guildId)
    {
        var config = ctx.GuildConfigs
                     .AsQueryable()
                     .Include(gc => gc.LogSetting)
                     .ThenInclude(gc => gc.IgnoredChannels)
                     .FirstOrDefault(x => x.GuildId == guildId);

        if (config == null)
        {
            ctx.Add(config = new GuildConfig
            {
                GuildId = guildId,
                Permissions = Permissionv2.GetDefaultPermlist,
                WarningsInitialized = true,
                WarnPunishments = DefaultWarnPunishments
            });
            ctx.SaveChanges();
        }

        if (config.WarningsInitialized) return config;
        config.WarningsInitialized = true;
        config.WarnPunishments = DefaultWarnPunishments;

        return config;
    }
    
    private static List<WarningPunishment> DefaultWarnPunishments =>
        new()
        {
            new WarningPunishment
            {
                Count = 3,
                Punishment = PunishmentAction.Kick
            },
            new WarningPunishment
            {
                Count = 5,
                Punishment = PunishmentAction.Ban
            }
        };
    
    public static ulong GetCleverbotChannel(this DbSet<GuildConfig> set, ulong guildid) =>
        set.AsQueryable()
           .Where(x => x.GuildId == guildid)
           .Select(x => x.CleverbotChannel).Single();
    
    private static IQueryable<GuildConfig> IncludeEverything(this DbSet<GuildConfig> config) =>
        config
            .AsQueryable()
            .Include(gc => gc.CommandCooldowns)
            .Include(gc => gc.GuildRepeaters)
            .Include(gc => gc.FollowedStreams)
            .Include(gc => gc.StreamRole)
            .Include(gc => gc.NsfwBlacklistedTags)
            .Include(gc => gc.XpSettings)
            .ThenInclude(x => x.ExclusionList)
            .Include(gc => gc.DelMsgOnCmdChannels)
            .Include(gc => gc.ReactionRoleMessages)
            .ThenInclude(x => x.ReactionRoles);
}