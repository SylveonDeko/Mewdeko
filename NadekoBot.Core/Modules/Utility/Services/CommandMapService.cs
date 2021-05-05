using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using NadekoBot.Common.ModuleBehaviors;
using NadekoBot.Extensions;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using NLog;
using System;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace NadekoBot.Modules.Utility.Services
{
    public class CommandMapService : IInputTransformer, INService
    {
        private readonly Logger _log;

        public ConcurrentDictionary<ulong, ConcurrentDictionary<string, string>> AliasMaps { get; } = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, string>>();

        private readonly DbService _db;

        //commandmap
        public CommandMapService(DiscordSocketClient client, DbService db)
        {
            _log = LogManager.GetCurrentClassLogger();

            using (var uow = db.GetDbContext())
            {
                var guildIds = client.Guilds.Select(x => x.Id).ToList();
                var configs = uow._context.Set<GuildConfig>()
                    .Include(gc => gc.CommandAliases)
                    .Where(x => guildIds.Contains(x.GuildId))
                    .ToList();
                
                AliasMaps = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, string>>(configs
                    .ToDictionary(
                        x => x.GuildId,
                        x => new ConcurrentDictionary<string, string>(x.CommandAliases
                            .Distinct(new CommandAliasEqualityComparer())
                            .ToDictionary(ca => ca.Trigger, ca => ca.Mapping))));

                _db = db;
            }
        }

        public int ClearAliases(ulong guildId)
        {
            AliasMaps.TryRemove(guildId, out _);

            int count;
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.CommandAliases));
                count = gc.CommandAliases.Count;
                gc.CommandAliases.Clear();
                uow.SaveChanges();
            }
            return count;
        }

        public async Task<string> TransformInput(IGuild guild, IMessageChannel channel, IUser user, string input)
        {
            await Task.Yield();

            if (guild == null || string.IsNullOrWhiteSpace(input))
                return input;

            if (guild != null)
            {
                if (AliasMaps.TryGetValue(guild.Id, out ConcurrentDictionary<string, string> maps))
                {
                    var keys = maps.Keys
                        .OrderByDescending(x => x.Length);

                    foreach (var k in keys)
                    {
                        string newInput;
                        if (input.StartsWith(k + " ", StringComparison.InvariantCultureIgnoreCase))
                            newInput = maps[k] + input.Substring(k.Length, input.Length - k.Length);
                        else if (input.Equals(k, StringComparison.InvariantCultureIgnoreCase))
                            newInput = maps[k];
                        else
                            continue;

                        try
                        {
                            var toDelete = await channel.SendConfirmAsync($"{input} => {newInput}").ConfigureAwait(false);
                            var _ = Task.Run(async () =>
                            {
                                await Task.Delay(1500).ConfigureAwait(false);
                                await toDelete.DeleteAsync(new RequestOptions()
                                {
                                    RetryMode = RetryMode.AlwaysRetry
                                }).ConfigureAwait(false);
                            });
                        }
                        catch { }
                        return newInput;
                    }
                }
            }

            return input;
        }
    }

    public class CommandAliasEqualityComparer : IEqualityComparer<CommandAlias>
    {
        public bool Equals(CommandAlias x, CommandAlias y) => x.Trigger == y.Trigger;

        public int GetHashCode(CommandAlias obj) => obj.Trigger.GetHashCode(StringComparison.InvariantCulture);
    }
}
