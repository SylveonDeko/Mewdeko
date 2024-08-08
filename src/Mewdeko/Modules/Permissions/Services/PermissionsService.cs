using Discord.Commands;
using Discord.Interactions;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Permissions.Services;

/// <summary>
/// Manages permissions for commands and interactions within the guilds, allowing dynamic updates and checks.
/// </summary>
public class PermissionService : ILateBlocker, INService, IReadyExecutor
{
    private readonly BotConfig config;
    private readonly DbContextProvider dbProvider;

    private readonly GuildSettingsService guildSettings;
    private readonly DiscordShardedClient client;

    /// <summary>
    /// Service for accessing localized bot strings.
    /// </summary>
    public readonly IBotStrings Strings;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionService"/> class.
    /// </summary>
    /// <param name="db">The database service for accessing permission settings.</param>
    /// <param name="strings">The service for localized bot strings.</param>
    /// <param name="guildSettings">The service for managing guild-specific settings.</param>
    /// <param name="client">The discord socket client</param>
    /// <param name="configService">The service for bot-wide configurations.</param>
    public PermissionService(DbContextProvider dbProvider,
        IBotStrings strings,
        GuildSettingsService guildSettings, DiscordShardedClient client, BotConfig configService)
    {
        config = configService;
        this.dbProvider = dbProvider;
        Strings = strings;
        this.guildSettings = guildSettings;
        this.client = client;
    }

    /// <summary>
    /// The cache of permissions for quick access.
    /// </summary>
    public ConcurrentDictionary<ulong, PermissionCache> Cache { get; } = new();

    /// <summary>
    /// The priority order in which the early behavior should run, with lower numbers indicating higher priority.
    /// </summary>
    public int Priority { get; } = 0;

    /// <summary>
    /// Attempts to block a command execution based on the permissions configured for the guild.
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
                    await channel.SendErrorAsync(Strings.GetText("perm_prevent", guild.Id, index + 1,
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
    /// Attempts to block a slash command execution based on the permissions configured for the guild.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="ctx">The interaction context.</param>
    /// <param name="command">The slash command information.</param>
    /// <returns>True if the slash command execution should be blocked, otherwise false.</returns>*
    public async Task<bool> TryBlockLate(DiscordShardedClient client, IInteractionContext ctx, ICommandInfo command)
    {
        var guild = ctx.Guild;
        var commandName = command.MethodName.ToLowerInvariant();

        await Task.Yield();
        if (guild == null)
            return false;

        var resetCommand = commandName == "resetperms";

        var pc = await GetCacheFor(guild.Id);
        if (resetCommand || pc.Permissions.CheckSlashPermissions(command.Module.SlashGroupName, commandName, ctx.User,
                ctx.Channel, out var index))
            return false;
        try
        {
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.GetText("perm_prevent", guild.Id, index + 1,
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
        await using var dbContext = await dbProvider.GetContextAsync();

        foreach (var x in await dbContext.GuildConfigs.Permissionsv2ForAll())
        {
            if (x.Permissions is null)
            {
                x.Permissions = Permissionv2.GetDefaultPermlist;
                await dbContext.SaveChangesAsync();
            }
            Cache.TryAdd(x.GuildId,
                new PermissionCache
                {
                    Verbose = x.VerbosePermissions,
                    PermRole = x.PermissionRole,
                    Permissions = new PermissionsCollection<Permissionv2>(x.Permissions)
                });
        }
    }

    /// <summary>
    /// Retrieves the permission cache for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The permission cache for the guild.</returns>
    public async Task<PermissionCache?> GetCacheFor(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        if (Cache.TryGetValue(guildId, out var pc))
            return pc;
        var config = await dbContext.ForGuildId(guildId,
            set => set.Include(x => x.Permissions));
        UpdateCache(config);

        Cache.TryGetValue(guildId, out pc);
        return pc ?? null;
    }

    /// <summary>
    /// Adds new permissions to a guild's configuration.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="perms">The permissions to add.</param>
    /// <remarks>Updates both the database and the in-memory cache.</remarks>
    public async Task AddPermissions(ulong guildId, params Permissionv2[] perms)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var config = await dbContext.GcWithPermissionsv2For(guildId);
        //var orderedPerms = new PermissionsCollection<Permissionv2>(config.Permissions);
        var max = config.Permissions.Max(x => x.Index); //have to set its index to be the highest
        foreach (var perm in perms)
        {
            perm.Index = ++max;
            config.Permissions.Add(perm);
        }

        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        UpdateCache(config);
    }

    /// <summary>
    /// Updates the in-memory cache with the latest permissions from the database for a guild.
    /// </summary>
    /// <param name="config">The guild configuration containing the permissions.</param>
    public void UpdateCache(GuildConfig config) =>
        Cache.AddOrUpdate(config.GuildId, new PermissionCache
        {
            Permissions = new PermissionsCollection<Permissionv2>(config.Permissions),
            PermRole = config.PermissionRole,
            Verbose = config.VerbosePermissions
        }, (_, old) =>
        {
            old.Permissions = new PermissionsCollection<Permissionv2>(config.Permissions);
            old.PermRole = config.PermissionRole;
            old.Verbose = config.VerbosePermissions;
            return old;
        });

    /// <summary>
    /// Resets all permissions for a guild to their default values.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <remarks>Updates both the database and the in-memory cache.</remarks>
    public async Task Reset(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var config = await dbContext.GcWithPermissionsv2For(guildId);
        config.Permissions = Permissionv2.GetDefaultPermlist;
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        UpdateCache(config);
    }

    /// <summary>
    /// Generates a mention string for a permission based on its type.
    /// </summary>
    /// <param name="t">The type of the permission.</param>
    /// <param name="id">The ID associated with the permission type.</param>
    /// <returns>A mention string for the permission.</returns>
    public static string MentionPerm(PrimaryPermissionType t, ulong id)
        => t switch
        {
            PrimaryPermissionType.User => $"<@{id}>",
            PrimaryPermissionType.Channel => $"<#{id}>",
            PrimaryPermissionType.Role => $"<@&{id}>",
            PrimaryPermissionType.Server => "This Server",
            PrimaryPermissionType.Category => $"<#{id}>",
            _ =>
                "An unexpected type input error occurred in `PermissionsService.cs#MentionPerm(PrimaryPermissionType, ulong)`. Please contact a developer at https://discord.gg/mewdeko with a screenshot of this message for more information."
        };

    /// <summary>
    /// Removes a specific permission from a guild's configuration.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="index">The index of the permission to remove.</param>
    /// <remarks>Updates both the database and the in-memory cache.</remarks>
    public async Task RemovePerm(ulong guildId, int index)
    {
        await using var dbContext = await dbProvider.GetContextAsync();


        var config = await dbContext.GcWithPermissionsv2For(guildId);
        var permsCol = new PermissionsCollection<Permissionv2>(config.Permissions);

        var p = permsCol[index];
        permsCol.RemoveAt(index);
        dbContext.Remove(p);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        UpdateCache(config);
    }

    /// <summary>
    /// Updates the state of a specific permission in a guild's configuration.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="index">The index of the permission to update.</param>
    /// <param name="state">The new state of the permission.</param>
    /// <remarks>Updates both the database and the in-memory cache.</remarks>
    public async Task UpdatePerm(ulong guildId, int index, bool state)
    {

        await using var dbContext = await dbProvider.GetContextAsync();

        var config = await dbContext.GcWithPermissionsv2For(guildId);
        var permsCol = new PermissionsCollection<Permissionv2>(config.Permissions);

        var p = permsCol[index];
        p.State = state;
        dbContext.Update(p);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        UpdateCache(config);
    }

    /// <summary>
    /// Moves a permission within the list, changing its order of evaluation.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="from">The current index of the permission.</param>
    /// <param name="to">The new index of the permission.</param>
    /// <remarks>Updates both the database and the in-memory cache.</remarks>
    public async Task UnsafeMovePerm(ulong guildId, int from, int to)
    {

        await using var dbContext = await dbProvider.GetContextAsync();

        var config = await dbContext.GcWithPermissionsv2For(guildId);
        var permsCol = new PermissionsCollection<Permissionv2>(config.Permissions);

        var fromFound = from < permsCol.Count;
        var toFound = to < permsCol.Count;

        if (!fromFound || !toFound)
        {
            return;
        }

        var fromPerm = permsCol[from];

        permsCol.RemoveAt(from);
        permsCol.Insert(to, fromPerm);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        UpdateCache(config);
    }
}