using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
/// The voice channel role service. Pain.
/// </summary>
public class VcRoleService : INService
{
    private readonly DiscordSocketClient client;
    private readonly DbService db;
    private readonly GuildSettingsService guildSettingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VcRoleService"/> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="bot">The bot instance.</param>
    /// <param name="db">The database service.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="guildSettingsService">The guild settings service.</param>
    public VcRoleService(DiscordSocketClient client, Mewdeko bot, DbService db, EventHandler eventHandler,
        GuildSettingsService guildSettingsService)
    {
        // Assigning the database service and the Discord client
        this.db = db;
        this.guildSettingsService = guildSettingsService;
        this.client = client;

        // Subscribing to the UserVoiceStateUpdated event
        eventHandler.UserVoiceStateUpdated += ClientOnUserVoiceStateUpdated;

        ToAssign = new NonBlocking.ConcurrentDictionary<ulong, ConcurrentQueue<(bool, IGuildUser, IRole)>>();

        // Getting all guild configurations and initializing VC roles for each guild

        // Starting a new task that continuously assigns or removes roles from users
        Task.Run(async () =>
        {
            while (true)
            {
                var tasks = ToAssign.Values.Select(queue => Task.Run(async () =>
                {
                    while (queue.TryDequeue(out var item))
                    {
                        var (add, user, role) = item;
                        if (add)
                        {
                            if (!user.RoleIds.Contains(role.Id))
                            {
                                try
                                {
                                    await user.AddRoleAsync(role).ConfigureAwait(false);
                                }
                                catch
                                {
                                    // ignored
                                }
                            }
                        }
                        else
                        {
                            if (user.RoleIds.Contains(role.Id))
                            {
                                try
                                {
                                    await user.RemoveRoleAsync(role).ConfigureAwait(false);
                                }
                                catch
                                {
                                    // ignored
                                }
                            }
                        }

                        await Task.Delay(250).ConfigureAwait(false);
                    }
                }));

                await Task.WhenAll(tasks.Append(Task.Delay(1000))).ConfigureAwait(false);
            }
        });

        // Subscribing to the LeftGuild and JoinedGuild events
        this.client.LeftGuild += _client_LeftGuild;
        bot.JoinedGuild += Bot_JoinedGuild;
    }

    /// <summary>
    /// A dictionary that maps guild IDs to another dictionary, which maps voice channel IDs to roles.
    /// </summary>
    public NonBlocking.ConcurrentDictionary<ulong, NonBlocking.ConcurrentDictionary<ulong, IRole>> VcRoles { get; } =
        new();

    /// <summary>
    /// A dictionary that maps guild IDs to a queue of tuples, each containing a boolean indicating whether to add or remove a role, a guild user, and a role.
    /// </summary>
    private NonBlocking.ConcurrentDictionary<ulong, ConcurrentQueue<(bool, IGuildUser, IRole)>> ToAssign { get; }

    /// <summary>
    /// Event handler for when the bot joins a guild. Initializes voice channel roles for the guild.
    /// </summary>
    /// <param name="arg">The guild configuration.</param>
    private async Task Bot_JoinedGuild(GuildConfig arg)
    {
        // includeall no longer loads vcrole
        // need to load new guildconfig with vc role included
        await using var uow = db.GetDbContext();
        var configWithVcRole = await uow.ForGuildId(
            arg.GuildId,
            set => set.Include(x => x.VcRoleInfos)
        );
        _ = InitializeVcRole(configWithVcRole);
    }

    /// <summary>
    /// Event handler for when the bot leaves a guild. Removes voice channel roles for the guild.
    /// </summary>
    /// <param name="arg">The guild.</param>
    private Task _client_LeftGuild(SocketGuild arg)
    {
        VcRoles.TryRemove(arg.Id, out _);
        ToAssign.TryRemove(arg.Id, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Initializes voice channel roles for a guild.
    /// </summary>
    /// <param name="gconf">The guild configuration.</param>
    private async Task InitializeVcRole(GuildConfig gconf)
    {
        await Task.Yield();
        var g = client.GetGuild(gconf.GuildId);
        if (g == null)
            return;

        var infos = new NonBlocking.ConcurrentDictionary<ulong, IRole>();
        var missingRoles = new List<VcRoleInfo>();
        VcRoles.AddOrUpdate(gconf.GuildId, infos, delegate { return infos; });
        foreach (var ri in gconf.VcRoleInfos)
        {
            var role = g.GetRole(ri.RoleId);
            if (role == null)
            {
                missingRoles.Add(ri);
                continue;
            }

            infos.TryAdd(ri.VoiceChannelId, role);
        }

        if (missingRoles.Count > 0)
        {
            var uow = db.GetDbContext();
            await using var _ = uow.ConfigureAwait(false);
            Log.Warning("Removing {MissingRolesCount} missing roles from {VcRoleServiceName}", missingRoles.Count,
                nameof(VcRoleService));
            uow.RemoveRange(missingRoles);
            await guildSettingsService.UpdateGuildConfig(gconf.GuildId, gconf).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Adds a voice channel role to a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to add the role to.</param>
    /// <param name="role">The role to add.</param>
    /// <param name="vcId">The ID of the voice channel to associate the role with.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task AddVcRole(ulong guildId, IRole role, ulong vcId)
    {
        ArgumentNullException.ThrowIfNull(role);

        var guildVcRoles = VcRoles.GetOrAdd(guildId, new NonBlocking.ConcurrentDictionary<ulong, IRole>());

        guildVcRoles.AddOrUpdate(vcId, role, (_, _) => role);
        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set.Include(x => x.VcRoleInfos));
        var toDelete = conf.VcRoleInfos.FirstOrDefault(x => x.VoiceChannelId == vcId); // remove old one
        if (toDelete != null) uow.Remove(toDelete);
        conf.VcRoleInfos.Add(new VcRoleInfo
        {
            VoiceChannelId = vcId, RoleId = role.Id
        }); // add new one
        await guildSettingsService.UpdateGuildConfig(conf.GuildId, conf).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a voice channel role from a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to remove the role from.</param>
    /// <param name="vcId">The ID of the voice channel to disassociate the role from.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating whether the operation was successful.</returns>
    public async Task<bool> RemoveVcRole(ulong guildId, ulong vcId)
    {
        if (!VcRoles.TryGetValue(guildId, out var guildVcRoles))
            return false;

        if (!guildVcRoles.TryRemove(vcId, out _))
            return false;

        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set.Include(x => x.VcRoleInfos));
        var toRemove = conf.VcRoleInfos.Where(x => x.VoiceChannelId == vcId).ToList();
        uow.RemoveRange(toRemove);
        await guildSettingsService.UpdateGuildConfig(conf.GuildId, conf).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Event handler for when a user's voice state is updated. Assigns or removes roles based on the user's new voice state.
    /// </summary>
    /// <param name="usr">The user whose voice state was updated.</param>
    /// <param name="oldState">The user's old voice state.</param>
    /// <param name="newState">The user's new voice state.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private Task ClientOnUserVoiceStateUpdated(SocketUser usr, SocketVoiceState oldState,
        SocketVoiceState newState)
    {
        if (usr is not SocketGuildUser gusr)
            return Task.CompletedTask;

        var oldVc = oldState.VoiceChannel;
        var newVc = newState.VoiceChannel;
        _ = Task.Run(() =>
        {
            try
            {
                if (oldVc == newVc) return;
                var guildId = newVc?.Guild.Id ?? oldVc.Guild.Id;

                if (!VcRoles.TryGetValue(guildId, out var guildVcRoles)) return;
                //remove old
                if (oldVc != null && guildVcRoles.TryGetValue(oldVc.Id, out var role))
                    Assign(false, gusr, role);
                //add new
                if (newVc != null && guildVcRoles.TryGetValue(newVc.Id, out role)) Assign(true, gusr, role);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in VcRoleService VoiceStateUpdate");
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Assigns a role to a user in a guild.
    /// </summary>
    /// <param name="v">A boolean indicating whether to add or remove the role.</param>
    /// <param name="gusr">The user in the guild.</param>
    /// <param name="role">The role to assign or remove.</param>
    private void Assign(bool v, SocketGuildUser gusr, IRole role)
    {
        // Get or create a queue for the guild
        var queue = ToAssign.GetOrAdd(gusr.Guild.Id, new ConcurrentQueue<(bool, IGuildUser, IRole)>());

        // Enqueue the operation (add or remove role)
        queue.Enqueue((v, gusr, role));
    }
}