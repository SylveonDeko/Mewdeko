using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using LinqToDB;
using Mewdeko.Modules.Music.Extensions;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using Mewdeko.Services.Database.Repositories.Impl;
using Victoria;
using Victoria.EventArgs;
using Victoria.Responses.Search;
using Mewdeko._Extensions;
using Victoria.Enums;

#nullable enable

namespace Mewdeko.Modules.Music.Services
{
    public sealed class MusicService : INService
    {
        private LavaNode _lavaNode;
        public DbService _db;
        
        private readonly ConcurrentDictionary<ulong, MusicPlayerSettings> _settings;
        public ConcurrentDictionary<ulong, IList<IndexedLavaTrack>> _queues;

        public MusicService(LavaNode lava, DbService db)
        {
            _db = db;
            _lavaNode = lava;
            _lavaNode.OnTrackEnded += TrackEnded;
            _settings = new ConcurrentDictionary<ulong, MusicPlayerSettings>();
            _queues = new ConcurrentDictionary<ulong, IList<IndexedLavaTrack>>();
        }

        public async Task Enqueue(ulong guildId, IUser user, LavaTrack lavaTrack)
        {
            var queue = _queues.GetOrAdd(guildId, new List<IndexedLavaTrack>());
            queue.Add(new IndexedLavaTrack(lavaTrack, queue.Count + 1, user));
        }
        public async Task Enqueue(ulong guildId, IUser user, LavaTrack[] lavaTracks)
        {
            try
            {
                var queue = _queues.GetOrAdd(guildId, new List<IndexedLavaTrack>());
                queue.AddRange(lavaTracks.Select(x => new IndexedLavaTrack(x, queue.Count + 1, user)));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public IList<IndexedLavaTrack> GetQueue(ulong guildid)
        {
            return _queues.FirstOrDefault(x => x.Key == guildid).Value;
        }
        public async Task TrackEnded(TrackEndedEventArgs args)
        {
            var e = _queues.FirstOrDefault(x => x.Key == args.Player.VoiceChannel.GuildId).Value;
            if (e.Any())
            {
                try
                {
                    if (args.Reason is TrackEndReason.Replaced or TrackEndReason.Stopped or TrackEndReason.Cleanup) return;
                    var currentTrack = e.FirstOrDefault(x => args.Track.Url == x.Url);
                    var nextTrack = e.FirstOrDefault(x => x.Index == currentTrack.Index + 1);
                    await args.Player.PlayAsync(nextTrack);
                    var eb = new EmbedBuilder()
                        .WithDescription($"Now playing {nextTrack.Title}")
                        .WithOkColor()
                        .WithFooter(
                            $"Track {nextTrack.Index} | {nextTrack.Duration:hh\\:mm\\:ss} {nextTrack.QueueUser}");
                    var channel = await args.Player.VoiceChannel.Guild.GetTextChannelAsync((await GetSettingsInternalAsync(args.Player.VoiceChannel.GuildId)).MusicChannelId.Value);
                    
                    await channel.SendMessageAsync(embed: eb.Build());
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
                
            }
            
        }

        public int GetVolume(ulong guildid)
        {
            return GetSettingsInternalAsync(guildid).Result.Volume;
        }
        public async Task Skip(IGuild guild, LavaPlayer player)
        {
            var e = _queues.FirstOrDefault(x => x.Key == guild.Id).Value;
            if (e.Any())
            {
                try
                {
                    var currentTrack = e.FirstOrDefault(x => player.Track.Hash == x.Hash);
                    var nextTrack = e.FirstOrDefault(x => x.Index == currentTrack.Index + 1);
                    await player.PlayAsync(nextTrack);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
                
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