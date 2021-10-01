﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using Mewdeko.Modules.Utility.Common;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Utility.Services
{
    public class MessageRepeaterService : INService
    {
        private readonly Mewdeko.Services.Mewdeko _bot;
        private readonly DiscordSocketClient _client;
        private readonly IBotCredentials _creds;
        private readonly DbService _db;

        public MessageRepeaterService(Mewdeko.Services.Mewdeko bot, DiscordSocketClient client, DbService db, IBotCredentials creds)
        {
            _db = db;
            _creds = creds;
            _bot = bot;
            _client = client;
            var _ = LoadRepeaters();
        }

        public ConcurrentDictionary<ulong, ConcurrentDictionary<int, RepeatRunner>> Repeaters { get; set; }
        public bool RepeaterReady { get; private set; }

        private async Task LoadRepeaters()
        {
            await _bot.Ready.Task.ConfigureAwait(false);
#if GLOBAL_Mewdeko
            await Task.Delay(30000);
#endif
            Log.Information("Loading message repeaters on shard {ShardId}.", _client.ShardId);

            var repeaters = new Dictionary<ulong, ConcurrentDictionary<int, RepeatRunner>>();
            foreach (var gc in _bot.AllGuildConfigs)
            {
                // don't load repeaters which don't belong on this shard
                if ((gc.GuildId >> 22) % (ulong)_creds.TotalShards != (ulong)_client.ShardId)
                    continue;

                try
                {
                    var guild = _client.GetGuild(gc.GuildId);
                    if (guild is null)
                    {
                        Log.Information("Unable to find guild {GuildId} for message repeaters.", gc.GuildId);
                        continue;
                    }

                    var idToRepeater = gc.GuildRepeaters
                        .Where(gr => !(gr.DateAdded is null))
                        .Select(gr =>
                            new KeyValuePair<int, RepeatRunner>(gr.Id, new RepeatRunner(_client, guild, gr, this)))
                        .ToDictionary(x => x.Key, y => y.Value)
                        .ToConcurrent();


                    repeaters.TryAdd(gc.GuildId, idToRepeater);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to load repeaters on Guild {0}.", gc.GuildId);
                }
            }

            Repeaters = repeaters.ToConcurrent();
            RepeaterReady = true;
        }

        public async Task RemoveRepeater(Repeater r)
        {
            using (var uow = _db.GetDbContext())
            {
                var gr = uow.GuildConfigs.ForId(r.GuildId, x => x.Include(y => y.GuildRepeaters)).GuildRepeaters;
                var toDelete = gr.FirstOrDefault(x => x.Id == r.Id);
                if (toDelete != null)
                    uow._context.Set<Repeater>().Remove(toDelete);
                await uow.SaveChangesAsync();
            }
        }

        public void SetRepeaterLastMessage(int repeaterId, ulong lastMsgId)
        {
            using (var uow = _db.GetDbContext())
            {
                uow._context.Database.ExecuteSqlInterpolated($@"UPDATE GuildRepeater SET 
                    LastMessageId={lastMsgId} WHERE Id={repeaterId}");
            }
        }
    }
}