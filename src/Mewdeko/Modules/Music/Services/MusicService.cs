using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using LinqToDB.Mapping;
using LinqToDB.Tools;
using Mewdeko.Modules.Music.Extensions;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using Mewdeko.Services.Database.Repositories.Impl;
using Victoria;
using Victoria.EventArgs;
using Victoria.Responses.Search;
using Mewdeko._Extensions;
using SpotifyAPI.Web;
using Victoria.Enums;

#nullable enable

namespace Mewdeko.Modules.Music.Services
{
    public sealed class MusicService : INService
    {
        private LavaNode _lavaNode;
        public DbService Db;
        
        private readonly ConcurrentDictionary<ulong, MusicPlayerSettings> _settings;
        public ConcurrentDictionary<ulong, IList<AdvancedLavaTrack>> Queues;
        public SpotifyClient SpotifyClient;
        

        public MusicService(LavaNode lava, DbService db)
        {
            Db = db;
            _lavaNode = lava;
            _lavaNode.OnTrackEnded += TrackEnded;
            _settings = new ConcurrentDictionary<ulong, MusicPlayerSettings>();
            Queues = new ConcurrentDictionary<ulong, IList<AdvancedLavaTrack>>();
            _lavaNode.OnTrackStarted += TrackStarted;
            var config = SpotifyClientConfig.CreateDefault();

            var request =
                new ClientCredentialsRequest("dc237c779f55479fae3d5418c4bb392e", "db01b63b808040efbdd02098e0840d90");
            var response = new OAuthClient(config).RequestToken(request).Result;

            SpotifyClient = new SpotifyClient(config.WithToken(response.AccessToken));
        }

        public Task Enqueue(ulong guildId, IUser user, LavaTrack lavaTrack, AdvancedLavaTrack.Platform queuedPlatform = AdvancedLavaTrack.Platform.Youtube)
        {
            var queue = Queues.GetOrAdd(guildId, new List<AdvancedLavaTrack>());
            queue.Add(new AdvancedLavaTrack(lavaTrack, queue.Count + 1, user, queuedPlatform));
            return Task.CompletedTask;
        }
        public Task Enqueue(ulong guildId, IUser user, LavaTrack[] lavaTracks, AdvancedLavaTrack.Platform queuedPlatform = AdvancedLavaTrack.Platform.Youtube)
        {
                var queue = Queues.GetOrAdd(guildId, new List<AdvancedLavaTrack>());
                queue.AddRange(lavaTracks.Select(x => new AdvancedLavaTrack(x, queue.Count + 1, user, queuedPlatform)));
                return Task.CompletedTask;
        }

        public async Task Shuffle(IGuild guild)
        {
            var random = new Random();
            var queue = GetQueue(guild.Id);
            List<int> numbers = new List<int>();
            IList<AdvancedLavaTrack> toadd = new List<AdvancedLavaTrack>();
            try
            {
                foreach (var i in queue)
                {
                    var rng = random.Next(1, queue.Count + 1);
                    while (numbers.Contains(rng))
                    {
                        rng = random.Next(1, queue.Count);
                    }

                    var toremove = i;
                    toremove.Index = rng;
                    toadd.Add(toremove);
                    numbers.Add(rng);
                }
                queue.Clear();
                queue.AddRange(toadd);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        public Task QueueClear(ulong guildid)
        {
            var toremove = Queues.GetOrAdd(guildid, new List<AdvancedLavaTrack>());
            toremove.Clear();
            return Task.CompletedTask;
        }

        public AdvancedLavaTrack GetCurrentTrack(LavaPlayer player, IGuild guild)
        {
            var queue = GetQueue(guild.Id);
            return queue.FirstOrDefault(x => x.Hash == player.Track.Hash)!;
        }
        public async Task SpotifyQueue(IGuild guild, IUser user, ITextChannel chan, LavaPlayer player, string uri)
        {
            var spotifyUrl = new Uri(uri);
            switch (spotifyUrl.Segments[1])
            {
                case "playlist/":
                    var result = await SpotifyClient.Playlists.Get(spotifyUrl.Segments[2]);
                    if (result.Tracks.Items.Any())
                    {
                        var items = result.Tracks.Items;
                        var eb = new EmbedBuilder()
                            .WithAuthor("Spotify Playlist", "https://assets.stickpng.com/images/5ece5029123d6d0004ce5f8b.png")
                            .WithOkColor()
                            .WithDescription($"Trying to queue {items.Count} tracks from {result.Name}...")
                            .WithThumbnailUrl(result.Images.FirstOrDefault()?.Url);
                        var msg = await chan.SendMessageAsync(embed: eb.Build());
                        var addedcount = 0;
                        foreach (var track in items.Select(i => i.Track as FullTrack))
                        {
                            var lavaTrack = await _lavaNode.SearchAsync(SearchType.YouTubeMusic, $"{track.Name} {track.Artists.FirstOrDefault().Name}");
                            if (lavaTrack.Status is SearchStatus.NoMatches) continue;
                            await Enqueue(guild.Id, user, lavaTrack.Tracks.FirstOrDefault(), AdvancedLavaTrack.Platform.Spotify);
                            addedcount++;
                        }
                        if (addedcount == 0)
                        {
                            eb.WithErrorColor()
                                .WithDescription(
                                    $"Seems like I couldn't load any tracks from {result.Name}... Perhaps its private?");
                            await msg.ModifyAsync(x => x.Embed = eb.Build());
                        }

                        eb.WithDescription($"Successfully queued {addedcount} tracks!");
                        await msg.ModifyAsync(x => x.Embed = eb.Build());
                    }
                    break;
                case "album/":
                    var result1 = await SpotifyClient.Albums.Get(spotifyUrl.Segments[2]);
                    if (result1.Tracks.Items.Any())
                    {
                        var items = result1.Tracks.Items;
                        var eb = new EmbedBuilder()
                            .WithAuthor("Spotify Album", "https://assets.stickpng.com/images/5ece5029123d6d0004ce5f8b.png")
                            .WithOkColor()
                            .WithDescription($"Trying to queue {items.Count} tracks from {result1.Name}...")
                            .WithThumbnailUrl(result1.Images.FirstOrDefault()?.Url);
                        var msg = await chan.SendMessageAsync(embed: eb.Build());
                        var addedcount = 0;
                        foreach (var track in items)
                        {
                            var lavaTrack = await _lavaNode.SearchAsync(SearchType.YouTubeMusic, $"{track.Name} {track.Artists.FirstOrDefault().Name}");
                            if (lavaTrack.Status is SearchStatus.NoMatches) continue;
                            await Enqueue(guild.Id, user, lavaTrack.Tracks.FirstOrDefault(), AdvancedLavaTrack.Platform.Spotify);
                            if (GetQueue(guild.Id).Count == 1)
                            {
                                await player.PlayAsync(x =>
                                {
                                    x.Track = lavaTrack.Tracks.FirstOrDefault();
                                    x.Volume = GetVolume(guild.Id);
                                });
                            }
                            addedcount++;
                        }
                        if (addedcount == 0)
                        {
                            eb.WithErrorColor()
                                .WithDescription(
                                    $"Seems like I couldn't load any tracks from {result1.Name}... Perhaps the songs weren't found or are exclusive?");
                            await msg.ModifyAsync(x => x.Embed = eb.Build());
                        }

                        eb
                            .WithDescription($"Successfully queued {addedcount} tracks!")
                            .WithTitle(result1.Name);
                        await msg.ModifyAsync(x => x.Embed = eb.Build());
                    }
                    break;
                
                case "track/":
                    var result3 = await SpotifyClient.Tracks.Get(spotifyUrl.Segments[2]);
                    if (result3.Name is null)
                    {
                        await chan.SendErrorAsync(
                            "Seems like i can't find or play this. Please try with a different link!");
                        return;
                    }
                    var lavaTrack3 = await _lavaNode.SearchAsync(SearchType.YouTubeMusic, $"{result3.Name} {result3.Artists.FirstOrDefault().Name}");
                    await Enqueue(guild.Id, user, lavaTrack3.Tracks.FirstOrDefault(), AdvancedLavaTrack.Platform.Spotify);
                    if (player.PlayerState != PlayerState.Playing)
                    {
                        await player.PlayAsync(x =>
                        {
                            x.Track = lavaTrack3.Tracks.FirstOrDefault();
                            x.Volume = GetVolume(guild.Id);
                        });
                    }
                    break;
                default:
                    await chan.SendErrorAsync("Seems like that isn't supported at the moment!");
                    break;
                
            }
        }
        public IList<AdvancedLavaTrack> GetQueue(ulong guildid)
        {
            return Queues.FirstOrDefault(x => x.Key == guildid).Value;
        }

        private async Task TrackStarted(TrackStartEventArgs args)
        {
            var queue = GetQueue(args.Player.VoiceChannel.GuildId);
            var track = queue.FirstOrDefault(x => x.Url == args.Track.Url);
            var nextTrack = queue.FirstOrDefault(x => x.Index == track.Index + 1);
            var resultMusicChannelId = GetSettingsInternalAsync(args.Player.VoiceChannel.GuildId).Result.MusicChannelId;
            if (resultMusicChannelId != null)
            {
                var channel = await args.Player.VoiceChannel.Guild.GetTextChannelAsync(
                    resultMusicChannelId.Value);
                if (channel is not null)
                {
                    var eb = new EmbedBuilder()
                        .WithDescription($"Now playing {track.Title} by {track.Author}")
                        .WithTitle($"Track #{track.Index}")
                        .WithFooter(
                            $"{track.Duration:hh\\:mm\\:ss} | {track.QueueUser} | {track.QueuedPlatform} | {queue.Count} tracks in queue")
                        .WithThumbnailUrl(track.FetchArtworkAsync().Result);
                    if (nextTrack is not null)
                    {
                        eb.AddField("Up Next", $"{nextTrack.Title} by {track.Author}");
                    }

                    await channel.SendMessageAsync(embed: eb.Build());
                }
            }
        }
        private async Task TrackEnded(TrackEndedEventArgs args)
        {
            var e = Queues.FirstOrDefault(x => x.Key == args.Player.VoiceChannel.GuildId).Value;
            if (e.Any())
            {
                var musicChannelId = (await GetSettingsInternalAsync(args.Player.VoiceChannel.GuildId)).MusicChannelId;
                    var channel = await args.Player.VoiceChannel.Guild.GetTextChannelAsync(musicChannelId.Value);
                    if (args.Reason is TrackEndReason.Replaced or TrackEndReason.Stopped or TrackEndReason.Cleanup) return;
                    var currentTrack = e.FirstOrDefault(x => args.Track.Url == x.Url);
                    var nextTrack = e.FirstOrDefault(x => x.Index == currentTrack.Index + 1);
                    var nextNextTrack = e.FirstOrDefault(x => x.Index == nextTrack.Index + 1);
                    if (nextTrack is null && channel != null)
                    {
                        var eb1 = new EmbedBuilder()
                            .WithOkColor()
                            .WithDescription("I have reached the end of the queue!");
                        await channel.SendMessageAsync(embed: eb1.Build());
                        if (GetSettingsInternalAsync(args.Player.VoiceChannel.Guild.Id).Result.AutoDisconnect is
                            AutoDisconnect.Either or AutoDisconnect.Queue)
                        {
                            await _lavaNode.LeaveAsync(args.Player.VoiceChannel);
                            return;
                        }
                    }
                    await args.Player.PlayAsync(nextTrack);

            }
            
        }

        public int GetVolume(ulong guildid)
        {
            return GetSettingsInternalAsync(guildid).Result.Volume;
        }
        public async Task Skip(IGuild guild, ITextChannel chan, LavaPlayer player)
        {
            var e = Queues.FirstOrDefault(x => x.Key == guild.Id).Value;
            if (e.Any())
            {
                var currentTrack = e.FirstOrDefault(x => player.Track.Hash == x.Hash);
                var nextTrack = e.FirstOrDefault(x => x.Index == currentTrack.Index + 1);
                if (nextTrack is null)
                {
                    await chan.SendErrorAsync("This is the last track!");
                    return;
                }

                if (GetSettingsInternalAsync(guild.Id).Result.PlayerRepeat == PlayerRepeatType.Track)
                {
                    await player.PlayAsync(currentTrack);
                    return;
                }
                await player.PlayAsync(nextTrack);
                    
            }
        }

        private async Task<MusicPlayerSettings> GetSettingsInternalAsync(ulong guildId)
        {
            if (_settings.TryGetValue(guildId, out var settings))
                return settings;

            using var uow = Db.GetDbContext();
            var toReturn = _settings[guildId] = await uow._context.MusicPlayerSettings.ForGuildAsync(guildId);
            await uow.SaveChangesAsync();

            return toReturn;
        }

        public async Task ModifySettingsInternalAsync<TState>(
            ulong guildId,
            Action<MusicPlayerSettings, TState> action,
            TState state)
        {
            using var uow = Db.GetDbContext();
            var ms = await uow._context.MusicPlayerSettings.ForGuildAsync(guildId);
            action(ms, state);
            await uow.SaveChangesAsync();
            _settings[guildId] = ms;
        }
    }
}