using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using Mewdeko.Services.Database.Repositories.Impl;
using Victoria;
using Victoria.EventArgs;
using Victoria.Responses.Search;

#nullable enable

namespace Mewdeko.Modules.Music.Services
{
    public sealed class MusicService : INService
    {
        private LavaNode _lavaNode;
        public DbService _db;
        
        private readonly ConcurrentDictionary<ulong, MusicPlayerSettings> _settings;
        private ConcurrentDictionary<ulong, LavaTrack> _queue;

        public MusicService(LavaNode lava, DbService db)
        {
            _db = db;
            _lavaNode = lava;
            _lavaNode.OnTrackEnded += TrackEnded;
            _settings = new ConcurrentDictionary<ulong, MusicPlayerSettings>();
        }
        
        public async Task TrackEnded(TrackEndedEventArgs args)
        {
            var loopsettings = GetSettingsInternalAsync(args.Player.TextChannel.Guild.Id).Result.PlayerRepeat;
            switch (loopsettings)
            {
                case PlayerRepeatType.None:
                    await args.Player.SkipAsync();
                    break;
                case PlayerRepeatType.Queue:
                    if (args.Player.Queue.LastOrDefault() == args.Track)
                        await args.Player.PlayAsync(x =>
                        {
                            x.Track = args.Player.Queue.FirstOrDefault();
                        });
                    break;
                case PlayerRepeatType.Track:
                    await args.Player.PlayAsync(x =>
                    {
                        x.Track = args.Track;
                    });
                    break;
                default:
                        await args.Player.SkipAsync();
                        break;
            }
        }
        public async Task Skip(IGuild guild, LavaPlayer player)
        {
            var qrp = GetSettingsInternalAsync(guild.Id).Result.PlayerRepeat;
            if (qrp == PlayerRepeatType.Track)
            {
               await player.PlayAsync(player.Track);
            }
            else
            {
               await player.SkipAsync();
            }
        }
        public async Task<MusicPlayerSettings> GetSettingsInternalAsync(ulong guildId)
        {
            if (_settings.TryGetValue(guildId, out var settings))
                return settings;

            using var uow = _db.GetDbContext();
            var toReturn = _settings[guildId] = await uow._context.MusicPlayerSettings.ForGuildAsync(guildId);
            await uow.SaveChangesAsync();

            return toReturn;
        }

        public async Task ModifySettingsInternalAsync<TState>(
            ulong guildId,
            Action<MusicPlayerSettings, TState> action,
            TState state)
        {
            using var uow = _db.GetDbContext();
            var ms = await uow._context.MusicPlayerSettings.ForGuildAsync(guildId);
            action(ms, state);
            await uow.SaveChangesAsync();
            _settings[guildId] = ms;
        }
    }
}