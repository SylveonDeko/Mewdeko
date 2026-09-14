using System.Diagnostics;
using DataModel;
using Discord.Commands;
using Discord.Interactions;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Services.Strings;
using CommandInfo = Discord.Commands.CommandInfo;

namespace Mewdeko.Modules.Permissions.Services;

/// <summary>
///     Manages permissions for commands and interactions within the guilds, allowing dynamic updates and checks.
/// </summary>
public class PermissionService : ILateBlocker, INService, IReadyExecutor
{
    /// <summary>
    ///     Service for accessing localized bot strings.
    /// </summary>
    public readonly GeneratedBotStrings Strings;

    private readonly DiscordShardedClient client;
    private readonly BotConfig config;
    private readonly IDataConnectionFactory dbFactory;

    private readonly GuildSettingsService guildSettings;

    private readonly SlashCommandIdentityService identity;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PermissionService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database service for accessing permission settings.</param>
    /// <param name="strings">The service for localized bot strings.</param>
    /// <param name="guildSettings">The service for managing guild-specific settings.</param>
    /// <param name="client">The discord socket client</param>
    /// <param name="configService">The service for bot-wide configurations.</param>
    /// <param name="identity">The resolver mapping slash commands to text command identities.</param>
    public PermissionService(IDataConnectionFactory dbFactory,
        GeneratedBotStrings strings,
        GuildSettingsService guildSettings, DiscordShardedClient client, BotConfig configService,
        SlashCommandIdentityService identity)
    {
        this.identity = identity;
        config = configService;
        this.dbFactory = dbFactory;
        Strings = strings;
        this.guildSettings = guildSettings;
        this.client = client;
    }

    /// <summary>
    ///     The cache of permissions for quick access.
    /// </summary>
    public ConcurrentDictionary<ulong, PermissionCache> Cache { get; } = new();

    /// <summary>
    ///     The priority order in which the early behavior should run, with lower numbers indicating higher priority.
    /// </summary>
    public int Priority { get; } = 0;

    /// <summary>
    ///     Attempts to block a command execution based on the permissions configured for the guild.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="ctx">The context of the command.</param>
    /// <param name="moduleName">The name of the module containing the command.</param>
    /// <param name="command">The command information.</param>
    /// <returns>True if the command execution should be blocked, otherwise false.</returns>
    public async Task<bool> TryBlockLate(
        DiscordShardedClient client,
        ICommandContext ctx,
        string moduleName,
        CommandInfo command)
    {
        var guild = ctx.Guild;
        var msg = ctx.Message;
        var user = ctx.User;
        var channel = ctx.Channel;
        var commandName = command.Name.ToLowerInvariant();

        await Task.Yield();
        if (guild == null)
            return false;

        var resetCommand = commandName == "resetperms";

        var pc = await GetCacheFor(guild.Id);
        if (!resetCommand && !pc.Permissions.CheckPermissions(msg, commandName, moduleName, out var index))
        {
            if (pc.Verbose)
            {
                try
                {
                    await channel.SendErrorAsync(Strings.PermPrevent(ctx.Guild.Id, index + 1,
                            Format.Bold(pc.Permissions[index]
                                .GetCommand(await guildSettings.GetPrefix(guild), (SocketGuild)guild))), config)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }

            return true;
        }

        if (moduleName == nameof(Permissions))
        {
            if (user is not IGuildUser guildUser)
                return true;

            if (guildUser.GuildPermissions.Administrator)
                return false;

            var permRole = pc.PermRole;
            if (!ulong.TryParse(permRole, out var rid))
                rid = 0;
            string? returnMsg;
            IRole role;
            if (string.IsNullOrWhiteSpace(permRole) || (role = guild.GetRole(rid)) == null)
            {
                returnMsg = "You need Admin permissions in order to use permission commands.";
                if (pc.Verbose)
                {
                    try
                    {
                        await channel.SendErrorAsync(returnMsg, config).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                return true;
            }

            if (!guildUser.RoleIds.Contains(rid))
            {
                returnMsg = $"You need the {Format.Bold(role.Name)} role in order to use permission commands.";
                if (pc.Verbose)
                {
                    try
                    {
                        await channel.SendErrorAsync(returnMsg, config).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                return true;
            }

            return false;
        }

        return false;
    }

    /// <summary>
    ///     Attempts to block a slash command execution based on the permissions configured for the guild.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="ctx">The interaction context.</param>
    /// <param name="command">The slash command information.</param>
    /// <returns>True if the slash command execution should be blocked, otherwise false.</returns>
    /// *
    public async Task<bool> TryBlockLate(DiscordShardedClient client, IInteractionContext ctx, ICommandInfo command)
    {
        var guild = ctx.Guild;
        var resolved = identity.Resolve(command);

        await Task.Yield();
        if (guild == null)
            return false;

        var resetCommand = resolved.Alias == "resetperms";

        var pc = await GetCacheFor(guild.Id);
        if (resetCommand || pc.Permissions.CheckSlashPermissions(resolved.ModuleName, resolved.Alias, ctx.User,
                ctx.Channel, out var index))
            return false;
        try
        {
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.PermPrevent(ctx.Guild.Id, index + 1,
                    Format.Bold(pc.Permissions[index]
                        .GetCommand(await guildSettings.GetPrefix(guild), (SocketGuild)guild))), config)
                .ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }

        return true;
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get all permissions, grouped by GuildId
        var permissionsByGuild = await db.Permissions1
            .Where(x => x.GuildId != null)
            .ToListAsync();

        var groupedPerms = permissionsByGuild
            .GroupBy(p => p.GuildId)
            .ToList();

        foreach (var guildPerms in groupedPerms)
        {
            Debug.Assert(guildPerms.Key != null);
            var guildId = guildPerms.Key.Value;
            var permissions = guildPerms.ToList();

            if (permissions.Count == 0)
            {
                // Add default permissions for this guild
                var defaultPerms = PermissionExtensions.GetDefaultPermlist;
                foreach (var perm in defaultPerms)
                {
                    perm.GuildId = guildId;
                    await db.InsertAsync(perm);
                }

                permissions = defaultPerms.ToList();
            }

            // Get verbose and permission role settings
            var guildConfig = await db.GuildConfigs
                .FirstOrDefaultAsync(gc => gc.GuildId == guildId);

            Cache.TryAdd(guildId,
                new PermissionCache
                {
                    Verbose = guildConfig?.VerbosePermissions ?? false,
                    PermRole = guildConfig?.PermissionRole ?? null,
                    Permissions = permissions // Directly use the list
                });
        }
    }

    /// <summary>
    ///     Retrieves the permission cache for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The permission cache for the guild.</returns>
    public async Task<PermissionCache?> GetCacheFor(ulong guildId)
    {
        if (Cache.TryGetValue(guildId, out var pc))
            return pc;

        await using var db = await dbFactory.CreateConnectionAsync();

        // Get permissions directly by GuildId
        var permissions = await db.Permissions1
            .Where(p => p.GuildId == guildId)
            .ToListAsync();

        // Get guild config for other settings
        var guildConfig = await db.GuildConfigs
            .FirstOrDefaultAsync(gc => gc.GuildId == guildId);

        if (permissions.Count == 0)
        {
            // Add default permissions for this guild
            var defaultPerms = PermissionExtensions.GetDefaultPermlist;
            foreach (var perm in defaultPerms)
            {
                perm.GuildId = guildId;
                await db.InsertAsync(perm);
            }

            permissions = defaultPerms.ToList();
        }

        var permCache = new PermissionCache
        {
            Verbose = guildConfig?.VerbosePermissions ?? false,
            PermRole = guildConfig?.PermissionRole ?? null,
            Permissions = permissions // Directly use the list
        };

        Cache.TryAdd(guildId, permCache);
        return permCache;
    }

    /// <summary>
    ///     Adds new permissions to a guild's configuration.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="perms">The permissions to add.</param>
    /// <remarks>Updates both the database and the in-memory cache.</remarks>
    public async Task AddPermissions(ulong guildId, params Permission1[] perms)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get existing permissions directly
        var permissions = await db.Permissions1
            .Where(p => p.GuildId == guildId)
            .ToListAsync();

        if (permissions.Count == 0)
        {
            // Add default permissions first
            var defaultPerms = PermissionExtensions.GetDefaultPermlist;
            foreach (var perm in defaultPerms)
            {
                perm.GuildId = guildId;
                await db.InsertAsync(perm);
            }

            permissions = defaultPerms.ToList();
        }

        // Add new permissions with GuildId
        var max = permissions.Count > 0 ? permissions.Max(x => x.Index) : -1;
        foreach (var perm in perms)
        {
            perm.GuildId = guildId;
            perm.Index = ++max;
            await db.InsertAsync(perm);
        }

        // Update cache
        permissions = await db.Permissions1
            .Where(p => p.GuildId == guildId)
            .ToListAsync();

        var guildConfig = await db.GuildConfigs
            .FirstOrDefaultAsync(gc => gc.GuildId == guildId);

        UpdateCache(guildId, permissions, guildConfig);
    }

    /// <summary>
    ///     Updates the in-memory cache with the latest permissions from the database for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="permissions">The list of permissions for the guild.</param>
    /// <param name="config">The guild configuration containing permission settings.</param>
    public void UpdateCache(ulong guildId, List<Permission1> permissions, GuildConfig? config)
    {
        Cache.AddOrUpdate(guildId, new PermissionCache
        {
            Permissions = permissions, // Directly use the list
            PermRole = config?.PermissionRole,
            Verbose = config?.VerbosePermissions ?? false
        }, (_, old) =>
        {
            old.Permissions = permissions; // Directly use the list
            old.PermRole = config?.PermissionRole;
            old.Verbose = config?.VerbosePermissions ?? false;
            return old;
        });
    }

    /// <summary>
    ///     Resets all permissions for a guild to their default values.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <remarks>Updates both the database and the in-memory cache.</remarks>
    public async Task Reset(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Remove all existing permissions for this guild
        await db.Permissions1
            .Where(p => p.GuildId == guildId)
            .DeleteAsync();

        // Add default permissions with GuildId
        var defaultPerms = PermissionExtensions.GetDefaultPermlist;
        foreach (var perm in defaultPerms)
        {
            perm.GuildId = guildId;
            await db.InsertAsync(perm);
        }

        // Get guild config for other settings
        var guildConfig = await db.GuildConfigs
            .FirstOrDefaultAsync(gc => gc.GuildId == guildId);

        UpdateCache(guildId, defaultPerms.ToList(), guildConfig);
    }

    /// <summary>
    ///     Generates a mention string for a permission based on its type.
    /// </summary>
    /// <param name="t">The type of the permission.</param>
    /// <param name="id">The ID associated with the permission type.</param>
    /// <returns>A mention string for the permission.</returns>
    public static string MentionPerm(PrimaryPermissionType t, ulong id)
    {
        return t switch
        {
            PrimaryPermissionType.User => $"<@{id}>",
            PrimaryPermissionType.Channel => $"<#{id}>",
            PrimaryPermissionType.Role => $"<@&{id}>",
            PrimaryPermissionType.Server => "This Server",
            PrimaryPermissionType.Category => $"<#{id}>",
            _ =>
                "An unexpected type input error occurred in `PermissionsService.cs#MentionPerm(PrimaryPermissionType, ulong)`. Please contact a developer at https://discord.gg/mewdeko with a screenshot of this message for more information."
        };
    }

    /// <summary>
    ///     Removes a specific permission from a guild's configuration.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="index">The index of the permission to remove.</param>
    /// <remarks>Updates both the database and the in-memory cache.</remarks>
    public async Task RemovePerm(ulong guildId, int index)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get permissions directly
        var permissions = await db.Permissions1
            .Where(p => p.GuildId == guildId)
            .OrderBy(p => p.Index)
            .ToListAsync();

        if (permissions.Count <= index)
            return;

        if (index >= 0 && index < permissions.Count)
        {
            var p = permissions[index];
            permissions.RemoveAt(index);

            await db.Permissions
                .Where(perm => perm.Id == p.Id)
                .DeleteAsync();

            // Get guild config for other settings
            var guildConfig = await db.GuildConfigs
                .FirstOrDefaultAsync(gc => gc.GuildId == guildId);

            UpdateCache(guildId, permissions, guildConfig);
        }
    }

    /// <summary>
    ///     Updates the state of a specific permission in a guild's configuration.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="index">The index of the permission to update.</param>
    /// <param name="state">The new state of the permission.</param>
    /// <remarks>Updates both the database and the in-memory cache.</remarks>
    public async Task UpdatePerm(ulong guildId, int index, bool state)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get permissions directly
        var permissions = await db.Permissions1
            .Where(p => p.GuildId == guildId)
            .OrderBy(p => p.Index)
            .ToListAsync();

        if (permissions.Count <= index)
            return;

        if (index >= 0 && index < permissions.Count)
        {
            var p = permissions[index];
            p.State = state;

            await db.UpdateAsync(p);

            // Get guild config for other settings
            var guildConfig = await db.GuildConfigs
                .FirstOrDefaultAsync(gc => gc.GuildId == guildId);

            UpdateCache(guildId, permissions, guildConfig);
        }
    }

    /// <summary>
    ///     Moves a permission within the list, changing its order of evaluation.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="from">The current index of the permission.</param>
    /// <param name="to">The new index of the permission.</param>
    /// <remarks>Updates both the database and the in-memory cache.</remarks>
    public async Task UnsafeMovePerm(ulong guildId, int from, int to)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get permissions directly
        var permissions = await db.Permissions1
            .Where(p => p.GuildId == guildId)
            .OrderBy(p => p.Index)
            .ToListAsync();

        var fromFound = from < permissions.Count;
        var toFound = to < permissions.Count;

        if (!fromFound || !toFound)
            return;

        var fromPerm = permissions[from];

        permissions.RemoveAt(from);
        permissions.Insert(to, fromPerm);

        // Update indices
        for (var i = 0; i < permissions.Count; i++)
        {
            permissions[i].Index = i;
            await db.UpdateAsync(permissions[i]);
        }

        // Get guild config for other settings
        var guildConfig = await db.GuildConfigs
            .FirstOrDefaultAsync(gc => gc.GuildId == guildId);

        UpdateCache(guildId, permissions, guildConfig);
    }
}