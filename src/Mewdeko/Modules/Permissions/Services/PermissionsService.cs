using System.Threading.Tasks;
using Discord.Commands;
using Discord.Interactions;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Permissions.Common;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Permissions.Services;

public class PermissionService : ILateBlocker, INService
{
    private readonly DbService db;
    public readonly IBotStrings Strings;
    private readonly GuildSettingsService guildSettings;

    public PermissionService(
        DiscordSocketClient client,
        DbService db,
        IBotStrings strings,
        GuildSettingsService guildSettings)
    {
        this.db = db;
        Strings = strings;
        this.guildSettings = guildSettings;

        using var uow = this.db.GetDbContext();
        foreach (var x in uow.GuildConfigs.Permissionsv2ForAll(client.Guilds.ToArray().Select(x => x.Id).ToList()))
        {
            Cache.TryAdd(x.GuildId,
                new PermissionCache
                {
                    Verbose = x.VerbosePermissions, PermRole = x.PermissionRole, Permissions = new PermissionsCollection<Permissionv2>(x.Permissions)
                });
        }
    }

    //guildid, root permission
    public ConcurrentDictionary<ulong, PermissionCache> Cache { get; } = new();

    public int Priority { get; } = 0;

    public async Task<bool> TryBlockLate(
        DiscordSocketClient client,
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
        if (guild == null) return false;

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
                                .GetCommand(await guildSettings.GetPrefix(guild), (SocketGuild)guild))))
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
                        await channel.SendErrorAsync(returnMsg).ConfigureAwait(false);
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
                        await channel.SendErrorAsync(returnMsg).ConfigureAwait(false);
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

    public async Task<bool> TryBlockLate(DiscordSocketClient client, IInteractionContext ctx, ICommandInfo command)
    {
        var guild = ctx.Guild;
        var commandName = command.MethodName.ToLowerInvariant();

        await Task.Yield();
        if (guild == null) return false;

        var resetCommand = commandName == "resetperms";

        var pc = await GetCacheFor(guild.Id);
        if (resetCommand || pc.Permissions.CheckSlashPermissions(command.Module.SlashGroupName, commandName, ctx.User, ctx.Channel, out var index)) return false;
        try
        {
            await ctx.Interaction.SendEphemeralErrorAsync(Strings.GetText("perm_prevent", guild.Id, index + 1,
                    Format.Bold(pc.Permissions[index].GetCommand(await guildSettings.GetPrefix(guild), (SocketGuild)guild))))
                .ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }

        return true;
    }

    public async Task<PermissionCache?> GetCacheFor(ulong guildId)
    {
        if (Cache.TryGetValue(guildId, out var pc)) return pc;
        await using (var uow = db.GetDbContext())
        {
            var config = await uow.ForGuildId(guildId,
                set => set.Include(x => x.Permissions));
            UpdateCache(config);
        }

        Cache.TryGetValue(guildId, out pc);
        return pc ?? null;
    }

    public async Task AddPermissions(ulong guildId, params Permissionv2[] perms)
    {
        await using var uow = db.GetDbContext();
        var config = await uow.GcWithPermissionsv2For(guildId);
        //var orderedPerms = new PermissionsCollection<Permissionv2>(config.Permissions);
        var max = config.Permissions.Max(x => x.Index); //have to set its index to be the highest
        foreach (var perm in perms)
        {
            perm.Index = ++max;
            config.Permissions.Add(perm);
        }

        await uow.SaveChangesAsync().ConfigureAwait(false);
        UpdateCache(config);
    }

    public void UpdateCache(GuildConfig config) =>
        Cache.AddOrUpdate(config.GuildId, new PermissionCache
        {
            Permissions = new PermissionsCollection<Permissionv2>(config.Permissions), PermRole = config.PermissionRole, Verbose = config.VerbosePermissions
        }, (_, old) =>
        {
            old.Permissions = new PermissionsCollection<Permissionv2>(config.Permissions);
            old.PermRole = config.PermissionRole;
            old.Verbose = config.VerbosePermissions;
            return old;
        });

    public async Task Reset(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var config = await uow.GcWithPermissionsv2For(guildId);
        config.Permissions = Permissionv2.GetDefaultPermlist;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        UpdateCache(config);
    }
}