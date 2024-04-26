using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Common;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class GuildConfigExtensions
{
    private static List<WarningPunishment> DefaultWarnPunishments
    {
        get
        {
            return
            [
                new WarningPunishment
                {
                    Count = 3, Punishment = PunishmentAction.Kick
                },

                new WarningPunishment
                {
                    Count = 5, Punishment = PunishmentAction.Ban
                }
            ];
        }
    }


    public static IEnumerable<GuildConfig> GetAllGuildConfigs(
        this DbSet<GuildConfig> configs,
        IEnumerable<ulong> availableGuilds)
    {
        return configs.IncludeEverything().ToLinqToDB().AsNoTracking().Where(x => availableGuilds.Contains(x.GuildId))
            .ToList();
    }


    public static IndexedCollection<ReactionRoleMessage> GetReactionRoles(this MewdekoContext ctx, ulong guildId)
        => ctx.GuildConfigs
            .Include(x => x.ReactionRoleMessages)
            .ThenInclude(x => x.ReactionRoles)
            .FirstOrDefault(x => x.GuildId == guildId)?.ReactionRoleMessages;

    public static async Task<GuildConfig> ForGuildId(this MewdekoContext ctx, ulong guildId,
        Func<DbSet<GuildConfig>, IQueryable<GuildConfig>> includes = null)
    {
        GuildConfig config;

        if (includes is null)
        {
            config = await ctx
                .GuildConfigs
                .FirstOrDefaultAsyncEF(c => c.GuildId == guildId);
        }
        else
        {
            var set = includes(ctx.GuildConfigs);
            config = await set.FirstOrDefaultAsync(c => c.GuildId == guildId);
        }

        if (config is null)
        {
            await ctx.GuildConfigs.AddAsync(config = new GuildConfig
            {
                GuildId = guildId,
                Permissions = Permissionv2.GetDefaultPermlist,
                WarningsInitialized = true,
                WarnPunishments = DefaultWarnPunishments
            });
            await ctx.SaveChangesAsync();
        }

        if (config.WarningsInitialized) return config;
        config.WarningsInitialized = true;
        config.WarnPunishments = DefaultWarnPunishments;

        return config;
    }

    public static IEnumerable<GuildConfig> Permissionsv2ForAll(this DbSet<GuildConfig> configs, int totalShards,
        int shardId)
    {
        var query = configs
            .Include(gc => gc.Permissions)
            .ToLinqToDB()
            .AsQueryable()
            .Where(x => (int)(x.GuildId / (ulong)Math.Pow(2, 22) % (ulong)totalShards) == shardId);

        return query.ToList();
    }

    public static async Task<GuildConfig> GcWithPermissionsv2For(this MewdekoContext ctx, ulong guildId)
    {
        var config = await ctx
            .GuildConfigs
            .AsQueryable()
            .Where(gc => gc.GuildId == guildId)
            .Include(gc => gc.Permissions)
            .FirstOrDefaultAsyncEF().ConfigureAwait(false);

        if (config is null) // if there is no guildconfig, create new one
        {
            await ctx.GuildConfigs.AddAsync(config = new GuildConfig
            {
                GuildId = guildId, Permissions = Permissionv2.GetDefaultPermlist
            });
            await ctx.SaveChangesAsync();
        }
        else if (config.Permissions is null || !config.Permissions.Any()) // if no perms, add default ones
        {
            config.Permissions = Permissionv2.GetDefaultPermlist;
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        return config;
    }

    public static async Task<StreamRoleSettings> GetStreamRoleSettings(this MewdekoContext ctx, ulong guildId)
    {
        var conf = await ctx.ForGuildId(guildId, set => set.Include(y => y.StreamRole)
            .Include(y => y.StreamRole.Whitelist)
            .Include(y => y.StreamRole.Blacklist));

        return conf.StreamRole ?? (conf.StreamRole = new StreamRoleSettings());
    }

    public static async Task<XpSettings> XpSettingsFor(this MewdekoContext ctx, ulong guildId)
    {
        var gc = await ctx.ForGuildId(guildId,
            set => set.Include(x => x.XpSettings)
                .ThenInclude(x => x.RoleRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.CurrencyRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.ExclusionList));

        return gc.XpSettings ?? (gc.XpSettings = new XpSettings());
    }

    public static async Task<GuildConfig> LogSettingsFor(this MewdekoContext ctx, ulong guildId)
    {
        var config = await ctx.GuildConfigs
            .Include(gc => gc.LogSetting)
            .ThenInclude(gc => gc.IgnoredChannels)
            .ToLinqToDB()
            .FirstOrDefaultAsyncLinqToDB(x => x.GuildId == guildId);

        if (config == null)
        {
            await ctx.AddAsync(config = new GuildConfig
            {
                GuildId = guildId,
                Permissions = Permissionv2.GetDefaultPermlist,
                WarningsInitialized = true,
                WarnPunishments = DefaultWarnPunishments
            });
            await ctx.SaveChangesAsync();
        }

        if (config.WarningsInitialized) return config;
        config.WarningsInitialized = true;
        config.WarnPunishments = DefaultWarnPunishments;

        return config;
    }

    public static ulong GetCleverbotChannel(this DbSet<GuildConfig> set, ulong guildid) =>
        set.AsQueryable()
            .Where(x => x.GuildId == guildid)
            .Select(x => x.CleverbotChannel).Single();

    public static IQueryable<GuildConfig> IncludeEverything(this DbSet<GuildConfig> config) =>
        config
            .AsQueryable()
            .Include(gc => gc.LogSetting)
            .ThenInclude(gc => gc.IgnoredChannels)
            .Include(gc => gc.Permissions)
            .Include(gc => gc.CommandCooldowns)
            .Include(gc => gc.GuildRepeaters)
            .Include(gc => gc.FollowedStreams)
            .Include(gc => gc.StreamRole)
            .Include(gc => gc.NsfwBlacklistedTags)
            .Include(gc => gc.XpSettings)
            .ThenInclude(x => x.ExclusionList)
            .Include(gc => gc.DelMsgOnCmdChannels)
            .Include(gc => gc.ReactionRoleMessages)
            .ThenInclude(x => x.ReactionRoles)
            .Include(x => x.XpSettings)
            .ThenInclude(x => x.RoleRewards)
            .Include(x => x.XpSettings)
            .ThenInclude(x => x.CurrencyRewards)
            .Include(x => x.XpSettings)
            .ThenInclude(x => x.ExclusionList)
            .Include(x => x.FilteredWords)
            .Include(x => x.FilterInvitesChannelIds)
            .Include(x => x.FilterWordsChannelIds)
            .Include(x => x.FilterLinksChannelIds);

    public class GeneratingChannel
    {
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
    }
}