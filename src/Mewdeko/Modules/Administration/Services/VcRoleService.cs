using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Administration.Services;

public class VcRoleService : INService
{
    private readonly DiscordSocketClient client;
    private readonly DbService db;

    public VcRoleService(DiscordSocketClient client, Mewdeko bot, DbService db, EventHandler eventHandler)
    {
        this.db = db;
        this.client = client;

        eventHandler.UserVoiceStateUpdated += ClientOnUserVoiceStateUpdated;
        VcRoles = new NonBlocking.ConcurrentDictionary<ulong, NonBlocking.ConcurrentDictionary<ulong, IRole>>();
        ToAssign = new NonBlocking.ConcurrentDictionary<ulong, ConcurrentQueue<(bool, IGuildUser, IRole)>>();

        using (var uow = db.GetDbContext())
        {
            var guildIds = client.Guilds.Select(x => x.Id).ToList();
            var configs = uow.GuildConfigs
                .AsQueryable()
                .Include(x => x.VcRoleInfos)
                .Where(x => guildIds.Contains(x.GuildId))
                .ToList();

            Task.WhenAll(configs.Select(InitializeVcRole));
        }

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

        this.client.LeftGuild += _client_LeftGuild;
        bot.JoinedGuild += Bot_JoinedGuild;
    }

    public NonBlocking.ConcurrentDictionary<ulong, NonBlocking.ConcurrentDictionary<ulong, IRole>> VcRoles { get; }
    public NonBlocking.ConcurrentDictionary<ulong, ConcurrentQueue<(bool, IGuildUser, IRole)>> ToAssign { get; }

    private async Task Bot_JoinedGuild(GuildConfig arg)
    {
        // includeall no longer loads vcrole
        // need to load new guildconfig with vc role included
        await using var uow = db.GetDbContext();
        var configWithVcRole = await uow.ForGuildId(
            arg.GuildId,
            set => set.Include(x => x.VcRoleInfos)
        );
        var _ = InitializeVcRole(configWithVcRole);
    }

    private Task _client_LeftGuild(SocketGuild arg)
    {
        VcRoles.TryRemove(arg.Id, out _);
        ToAssign.TryRemove(arg.Id, out _);
        return Task.CompletedTask;
    }

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
            Log.Warning("Removing {MissingRolesCount} missing roles from {VcRoleServiceName}", missingRoles.Count, nameof(VcRoleService));
            uow.RemoveRange(missingRoles);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    public async Task AddVcRole(ulong guildId, IRole role, ulong vcId)
    {
        if (role == null)
            throw new ArgumentNullException(nameof(role));

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
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

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
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

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

    private void Assign(bool v, SocketGuildUser gusr, IRole role)
    {
        var queue = ToAssign.GetOrAdd(gusr.Guild.Id, new ConcurrentQueue<(bool, IGuildUser, IRole)>());
        queue.Enqueue((v, gusr, role));
    }
}