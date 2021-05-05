using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Mewdeko.Core.Services;
using NLog;
using System.Collections.Generic;

namespace Mewdeko.Modules.Administration.Services
{
    public class AutoAssignRoleService : INService
    {
        private readonly Logger _log;
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;

        //guildid/roleid
        public ConcurrentDictionary<ulong, string> AutoAssignedRoles { get; }
        public ConcurrentDictionary<ulong, ConcurrentQueue<(SocketGuildUser, string)>> AssignQueue { get; }
            = new ConcurrentDictionary<ulong, ConcurrentQueue<(SocketGuildUser, string)>>();

        public AutoAssignRoleService(DiscordSocketClient client, Mewdeko bot, DbService db)
        {
            _log = LogManager.GetCurrentClassLogger();
            _client = client;
            _db = db;

            AutoAssignedRoles = new ConcurrentDictionary<ulong, string>(
                bot.AllGuildConfigs
                    .Where(x => x.AutoAssignRoleId != 0.ToString())
                    .ToDictionary(k => k.GuildId, v => v.AutoAssignRoleId));

            var _queueRunner = Task.Run(async () =>
            {
                while (true)
                {
                    var queues = AssignQueue
                        .Keys
                        .Select(k =>
                        {
                            if (AssignQueue.TryGetValue(k, out var q))
                            {
                                var l = new List<(SocketGuildUser, string)>();
                                while (q.TryDequeue(out var x))
                                    l.Add(x);
                                return l;
                            }
                            return Enumerable.Empty<(SocketGuildUser, string)>();
                        });


                    await Task.WhenAll(queues.Select(x => Task.Run(async () =>
                    {
                        foreach (var item in x)
                        {
                            var (user, roleId) = item;
                            try
                            {
                                

                                if (user.Guild != null)
                                {
                                    foreach (var i in roleId.Split())
                                    {
                                        var role = user.Guild.GetRole(Convert.ToUInt64(i));
                                        await user.AddRoleAsync(role).ConfigureAwait(false);
                                        await Task.Delay(250).ConfigureAwait(false);
                                    }
                                }
                                else
                                {
                                    _log.Warn($"Disabled 'Auto assign role' feature on {0} server the role doesn't exist.",
                                       roleId);
                                    DisableAar(user.Guild.Id);
                                }
                            }
                            catch (Discord.Net.HttpException ex) when (ex.HttpCode == System.Net.HttpStatusCode.Forbidden)
                            {
                                _log.Warn($"Disabled 'Auto assign role' feature on {0} server because I don't have role management permissions.",
                                    roleId);
                                DisableAar(user.Guild.Id);
                            }
                            catch (Exception ex)
                            {
                                _log.Warn(ex);
                            }
                        }
                    })).Append(Task.Delay(3000))).ConfigureAwait(false);
                }
            });

            _client.UserJoined += (user) =>
            {
                if (AutoAssignedRoles.TryGetValue(user.Guild.Id, out string roleId)
                    && roleId != 0.ToString())
                {
                    var pair = (user, roleId);
                    AssignQueue.AddOrUpdate(user.Guild.Id,
                        new ConcurrentQueue<(SocketGuildUser, string)>(new[] { pair }),
                        (key, old) =>
                        {
                            old.Enqueue(pair);
                            return old;
                        });
                }
                return Task.CompletedTask;
            };
        }

        public void EnableAar(ulong guildId, string roleId)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guildId, set => set);
                gc.AutoAssignRoleId = roleId;
                uow.SaveChanges();
            }
            AutoAssignedRoles.AddOrUpdate(guildId,
                roleId,
                delegate { return roleId; });
        }

        public void DisableAar(ulong guildId)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guildId, set => set);
                gc.AutoAssignRoleId = 0.ToString();
                uow.SaveChanges();
            }
            AutoAssignedRoles.TryRemove(guildId, out _);
        }
    }
}
