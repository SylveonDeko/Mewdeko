using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Common.Replacements;
using Mewdeko.Core.Common;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Administration.Services
{
    public sealed class PlayingRotateService : INService
    {
        private readonly Mewdeko _bot;
        private readonly BotConfigService _bss;
        private readonly DbService _db;
        private readonly Replacer _rep;
        private readonly Timer _t;

        public PlayingRotateService(DiscordSocketClient client, DbService db, Mewdeko bot,
            BotConfigService bss, IEnumerable<IPlaceholderProvider> phProviders)
        {
            _db = db;
            _bot = bot;
            _bss = bss;

            if (client.ShardId == 0)
            {
                _rep = new ReplacementBuilder()
                    .WithClient(client)
                    .WithProviders(phProviders)
                    .Build();

                _t = new Timer(RotatingStatuses, new TimerState(), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            }
        }

        private async void RotatingStatuses(object objState)
        {
            try
            {
                var state = (TimerState)objState;

                if (!_bss.Data.RotateStatuses) return;

                IReadOnlyList<RotatingPlayingStatus> rotatingStatuses;
                using (var uow = _db.GetDbContext())
                {
                    rotatingStatuses = uow._context.RotatingStatus
                        .AsNoTracking()
                        .OrderBy(x => x.Id)
                        .ToList();
                }

                if (rotatingStatuses.Count == 0)
                    return;

                var playingStatus = state.Index >= rotatingStatuses.Count
                    ? rotatingStatuses[state.Index = 0]
                    : rotatingStatuses[state.Index++];

                var statusText = _rep.Replace(playingStatus.Status);
                await _bot.SetGameAsync(statusText, playingStatus.Type);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Rotating playing status errored: {ErrorMessage}", ex.Message);
            }
        }

        public async Task<string> RemovePlayingAsync(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            using var uow = _db.GetDbContext();
            var toRemove = await uow._context.RotatingStatus
                .AsQueryable()
                .AsNoTracking()
                .Skip(index)
                .FirstOrDefaultAsync();

            if (toRemove is null)
                return null;

            uow._context.Remove(toRemove);
            await uow.SaveChangesAsync();
            return toRemove.Status;
        }

        public async Task AddPlaying(ActivityType t, string status)
        {
            using var uow = _db.GetDbContext();
            var toAdd = new RotatingPlayingStatus { Status = status, Type = t };
            uow._context.Add(toAdd);
            await uow.SaveChangesAsync();
        }

        public bool ToggleRotatePlaying()
        {
            var enabled = false;
            _bss.ModifyConfig(bs => { enabled = bs.RotateStatuses = !bs.RotateStatuses; });
            return enabled;
        }

        public IReadOnlyList<RotatingPlayingStatus> GetRotatingStatuses()
        {
            using var uow = _db.GetDbContext();
            return uow._context.RotatingStatus.AsNoTracking().ToList();
        }

        private class TimerState
        {
            public int Index { get; set; }
        }
    }
}