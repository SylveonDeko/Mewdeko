using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Common.Replacements;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Mewdeko.Modules.Administration.Services
{
    public class PlayingRotateService : INService
    {
        private readonly IBotConfigProvider _bcp;
        private readonly IDataCache _cache;
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;
        private readonly Logger _log;
        private readonly Replacer _rep;
        private readonly SelfService _selfService;
        private readonly Timer _t;

        public PlayingRotateService(DiscordSocketClient client, IBotConfigProvider bcp,
            DbService db, IDataCache cache, Mewdeko bot, MusicService music, SelfService selfService)
        {
            _client = client;
            _bcp = bcp;
            _db = db;
            _log = LogManager.GetCurrentClassLogger();
            _cache = cache;
            _selfService = selfService;

            if (client.ShardId == 0)
            {
                _rep = new ReplacementBuilder()
                    .WithClient(client)
                    .WithMusic(music)
                    .Build();

                _t = new Timer(async objState =>
                {
                    try
                    {
                        var state = (TimerState) objState;

                        if (!BotConfig.RotatingStatuses)
                            return;

                        if (state.Index >= BotConfig.RotatingStatusMessages.Count)
                            state.Index = 0;

                        if (!BotConfig.RotatingStatusMessages.Any())
                            return;
                        var msg = BotConfig.RotatingStatusMessages[state.Index++];
                        var status = msg.Status;
                        if (string.IsNullOrWhiteSpace(status))
                            return;

                        status = _rep.Replace(status);

                        try
                        {
                            await bot.SetGameAsync(status, msg.Type).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _log.Warn(ex);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Warn("Rotating playing status errored.\n" + ex);
                    }
                }, new TimerState(), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            }
        }

        public BotConfig BotConfig => _bcp.BotConfig;

        public async Task<string> RemovePlayingAsync(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            string msg;
            using (var uow = _db.GetDbContext())
            {
                var config = uow.BotConfig.GetOrCreate(set => set.Include(x => x.RotatingStatusMessages));

                if (index >= config.RotatingStatusMessages.Count)
                    return null;
                msg = config.RotatingStatusMessages[index].Status;
                var remove = config.RotatingStatusMessages[index];
                uow._context.Remove(remove);
                _bcp.BotConfig.RotatingStatusMessages = config.RotatingStatusMessages;
                await uow.SaveChangesAsync();
            }

            return msg;
        }

        public async Task AddPlaying(ActivityType t, string status)
        {
            using (var uow = _db.GetDbContext())
            {
                var config = uow.BotConfig.GetOrCreate(set => set.Include(x => x.RotatingStatusMessages));
                var toAdd = new PlayingStatus {Status = status, Type = t};
                config.RotatingStatusMessages.Add(toAdd);
                _bcp.BotConfig.RotatingStatusMessages = config.RotatingStatusMessages;
                await uow.SaveChangesAsync();
            }
        }

        public bool ToggleRotatePlaying()
        {
            bool enabled;
            using (var uow = _db.GetDbContext())
            {
                var config = uow.BotConfig.GetOrCreate(set => set);

                enabled = config.RotatingStatuses = !config.RotatingStatuses;
                uow.SaveChanges();
            }

            _selfService.ReloadBotConfig();
            return enabled;
        }

        private class TimerState
        {
            public int Index { get; set; }
        }
    }
}