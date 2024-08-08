using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Common;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
/// Provides extension methods for working with GuildConfig entities in the MewdekoContext.
/// </summary>
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

    /// <summary>
    /// Retrieves the reaction roles for a specific guild from the context.
    /// </summary>
    /// <param name="ctx">The database context.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The reaction role messages for the guild.</returns>
    public async static Task<IndexedCollection<ReactionRoleMessage>> GetReactionRoles(this MewdekoContext ctx, ulong guildId)
        => (await ctx.GuildConfigs
            .Include(x => x.ReactionRoleMessages)
            .ThenInclude(x => x.ReactionRoles)
            .FirstOrDefaultAsyncEF(x => x.GuildId == guildId))?.ReactionRoleMessages;

    /// <summary>
    /// Retrieves or creates a GuildConfig for a specific guild.
    /// </summary>
    /// <param name="ctx">The database context.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="includes">Optional function to include related entities.</param>
    /// <returns>The GuildConfig for the guild.</returns>
    public static async Task<GuildConfig> ForGuildId(this MewdekoContext ctx, ulong guildId,
        Func<DbSet<GuildConfig>, IQueryable<GuildConfig>>? includes = null)
    {
        GuildConfig config;

        if (includes is null)
        {
            config = await ctx
                .GuildConfigs
                .FirstOrDefaultAsync(c => c.GuildId == guildId);
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

    /// <summary>
    /// Retrieves GuildConfig entities with Permissionsv2 for a specific shard.
    /// </summary>
    /// <param name="configs">The DbSet of GuildConfig entities.</param>
    /// <returns>A collection of GuildConfig entities.</returns>
    public async static Task<IEnumerable<GuildConfig>> Permissionsv2ForAll(this DbSet<GuildConfig> configs)
    {
        var query = configs
            .Include(gc => gc.Permissions)
            .ToLinqToDB()
            .AsQueryable();

        return await query.ToListAsyncEF();
    }

    /// <summary>
    /// Retrieves or creates a GuildConfig with Permissionsv2 for a specific guild.
    /// </summary>
    /// <param name="ctx">The database context.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The GuildConfig for the guild.</returns>
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

    /// <summary>
    /// Retrieves the StreamRoleSettings for a specific guild.
    /// </summary>
    /// <param name="ctx">The database context.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The StreamRoleSettings for the guild.</returns>
    public static async Task<StreamRoleSettings> GetStreamRoleSettings(this MewdekoContext ctx, ulong guildId)
    {
        var conf = await ctx.ForGuildId(guildId, set => set.Include(y => y.StreamRole)
            .Include(y => y.StreamRole.Whitelist)
            .Include(y => y.StreamRole.Blacklist));

        return conf.StreamRole ?? (conf.StreamRole = new StreamRoleSettings());
    }

    /// <summary>
    /// Retrieves the XpSettings for a specific guild.
    /// </summary>
    /// <param name="ctx">The database context.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The XpSettings for the guild.</returns>
    public static async Task<XpSettings> XpSettingsFor(this MewdekoContext ctx, ulong guildId)
    {
        var gc = await ctx.ForGuildId(guildId,
            set => set.Include(x => x.XpSettings)
                .ThenInclude(x => x.RoleRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.CurrencyRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.ExclusionList));

        if (gc.XpSettings is not null)
            return gc.XpSettings;
        gc.XpSettings = new XpSettings();
        return gc.XpSettings;
    }

    /// <summary>
    /// Retrieves or creates a GuildConfig with LogSettings for a specific guild.
    /// </summary>
    /// <param name="ctx">The database context.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The GuildConfig for the guild.</returns>
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

        if (config.LogSetting == null)
        {
            config.LogSetting = new LogSetting();
            await ctx.SaveChangesAsync();
        }

        if (config.WarningsInitialized) return config;
        config.WarningsInitialized = true;
        config.WarnPunishments = DefaultWarnPunishments;

        return config;
    }

    /// <summary>
    /// Includes all related entities for the GuildConfig.
    /// </summary>
    /// <param name="config">The DbSet of GuildConfig entities.</param>
    /// <returns>The queryable with included related entities.</returns>
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
            .ThenInclude(x

 => x.ExclusionList)
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

    /// <summary>
    /// Represents a channel for generating content.
    /// </summary>
    public class GeneratingChannel
    {
        /// <summary>
        /// Gets or sets the guild ID.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        /// Gets or sets the channel ID.
        /// </summary>
        public ulong ChannelId { get; set; }
    }
}