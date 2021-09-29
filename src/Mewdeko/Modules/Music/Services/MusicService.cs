#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.WebSocket;
using Mewdeko.Core.Modules.Music;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using Mewdeko.Services.Database.Repositories.Impl;
using Mewdeko.Extensions;
using Serilog;

namespace Mewdeko.Modules.Music.Services
{
    public sealed class MusicService : IMusicService
    {
        private readonly ConcurrentDictionary<ulong, (int Default, int Override)> _autoplay;
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;
        private readonly IGoogleApiService _googleApiService;
        private readonly ILocalTrackResolver _localResolver;
        private readonly ConcurrentDictionary<ulong, (ITextChannel Default, ITextChannel? Override)> _outputChannels;

        private readonly ConcurrentDictionary<ulong, IMusicPlayer> _players;
        private readonly ISoundcloudResolver _scResolver;
        private readonly ConcurrentDictionary<ulong, MusicPlayerSettings> _settings;
        private readonly IBotStrings _strings;
        private readonly ITrackResolveProvider _trackResolveProvider;
        private readonly AyuVoiceStateService _voiceStateService;
        private readonly YtLoader _ytLoader;
        private readonly IYoutubeResolver _ytResolver;

        public MusicService(AyuVoiceStateService voiceStateService, ITrackResolveProvider trackResolveProvider,
            DbService db, IYoutubeResolver ytResolver, ILocalTrackResolver localResolver,
            ISoundcloudResolver scResolver,
            DiscordSocketClient client, IBotStrings strings, IGoogleApiService googleApiService, YtLoader ytLoader)
        {
            _voiceStateService = voiceStateService;
            _trackResolveProvider = trackResolveProvider;
            _db = db;
            _ytResolver = ytResolver;
            _localResolver = localResolver;
            _scResolver = scResolver;
            _client = client;
            _strings = strings;
            _googleApiService = googleApiService;
            _ytLoader = ytLoader;

            _players = new ConcurrentDictionary<ulong, IMusicPlayer>();
            _outputChannels = new ConcurrentDictionary<ulong, (ITextChannel, ITextChannel?)>();
            _settings = new ConcurrentDictionary<ulong, MusicPlayerSettings>();
            _autoplay = new ConcurrentDictionary<ulong, (int Default, int Override)>();

            _client.LeftGuild += ClientOnLeftGuild;
        }

        public async Task LeaveVoiceChannelAsync(ulong guildId)
        {
            RemoveMusicPlayer(guildId);
            await _voiceStateService.LeaveVoiceChannel(guildId);
        }

       

        public Task JoinVoiceChannelAsync(ulong guildId, ulong voiceChannelId)
        {
// #pragma warning disable 4014
//             SetMusicCount(guildId, true);
// #pragma warning restore 4014
            return _voiceStateService.JoinVoiceChannel(guildId, voiceChannelId);
        }

        public async Task<IMusicPlayer?> GetOrCreateMusicPlayerAsync(ITextChannel contextChannel)
        {
            // await SetMusicCount(contextChannel.GuildId, true);
            var newPLayer = await CreateMusicPlayerInternalAsync(contextChannel.GuildId, contextChannel);
            if (newPLayer is null)
                return null;

            return _players.GetOrAdd(contextChannel.GuildId, newPLayer);
        }
        public bool CheckServerCount()
        {
            var count = _players
                .Select(x => x.Value.GetCurrentTrack(out _))
                .Count(x => !(x is null));
            if (count == 10) return true;
            return false;
        }
        public bool TryGetMusicPlayer(ulong guildId, out IMusicPlayer musicPlayer)
        {
#pragma warning disable CS8601 // Possible null reference assignment.
            return _players.TryGetValue(guildId, out musicPlayer);
#pragma warning restore CS8601 // Possible null reference assignment.
        }

        public async Task<int> EnqueueYoutubePlaylistAsync(IMusicPlayer mp, string query, string queuer)
        {
            var count = 0;
            await foreach (var track in _ytResolver.ResolveTracksFromPlaylistAsync(query))
            {
                if (mp.IsKilled)
                    break;

                mp.EnqueueTrack(track, queuer);
                ++count;
            }

            return count;
        }

        public async Task EnqueueDirectoryAsync(IMusicPlayer mp, string dirPath, string queuer)
        {
            await foreach (var track in _localResolver.ResolveDirectoryAsync(dirPath))
            {
                if (mp.IsKilled)
                    break;

                mp.EnqueueTrack(track, queuer);
            }
        }

        public async Task<int> EnqueueSoundcloudPlaylistAsync(IMusicPlayer mp, string playlist, string queuer)
        {
            var i = 0;
            await foreach (var track in _scResolver.ResolvePlaylistAsync(playlist))
            {
                if (mp.IsKilled)
                    break;

                mp.EnqueueTrack(track, queuer);
                ++i;
            }

            return i;
        }
        public async Task<QualityPreset> GetMusicQualityAsync(ulong guildId)
        {
            using var uow = _db.GetDbContext();
            var settings = await uow._context.MusicPlayerSettings.ForGuildAsync(guildId);
            return settings.QualityPreset;
        }

        public Task SetMusicQualityAsync(ulong guildId, QualityPreset preset)
        {
            return ModifySettingsInternalAsync(guildId, (settings, _) => { settings.QualityPreset = preset; }, preset);
        }

        public Task<IUserMessage?> SendToOutputAsync(ulong guildId, EmbedBuilder embed)
        {
            if (_outputChannels.TryGetValue(guildId, out var chan))
                return (chan.Default ?? chan.Override).EmbedAsync(embed);

            return Task.FromResult<IUserMessage?>(null);
        }

        // this has to be done because dragging bot to another vc isn't supported yet
        public async Task<bool> PlayAsync(ulong guildId, ulong voiceChannelId)
        {
            if (!TryGetMusicPlayer(guildId, out var mp)) return false;

            if (mp.IsStopped)
                if (!_voiceStateService.TryGetProxy(guildId, out var proxy)
                    || proxy.State == VoiceProxy.VoiceProxyState.Stopped)
                    await JoinVoiceChannelAsync(guildId, voiceChannelId);

            mp.Next();
            return true;
        }

        public async Task<IList<(string Title, string Url)>> SearchVideosAsync(string query)
        {
            try
            {
                IList<(string, string)> videos = await SearchYtLoaderVideosAsync(query);
                if (videos.Count > 0) return videos;
            }
            catch (Exception ex)
            {
                Log.Warning("Failed geting videos with YtLoader: {ErrorMessage}", ex.Message);
            }

            try
            {
                return await SearchGoogleApiVideosAsync(query);
            }
            catch (Exception ex)
            {
                Log.Warning("Failed getting video results with Google Api. " +
                            "Probably google api key missing: {ErrorMessage}", ex.Message);
            }

            return Array.Empty<(string, string)>();
        }

        public IEnumerable<(string Name, Func<string> Func)> GetPlaceholders()
        {
            // random song that's playing
            yield return ("%music.playing%", () =>
                    {
                        var randomPlayingTrack = _players
                            .Select(x => x.Value.GetCurrentTrack(out _))
                            .Where(x => !(x is null))
                            .Shuffle()
                            .FirstOrDefault();

                        if (randomPlayingTrack is null)
                            return "-";

                        return randomPlayingTrack.Title;
                    }
                );

            // number of servers currently listening to music
            yield return ("%music.servers%", () =>
                    {
                        var count = _players
                            .Select(x => x.Value.GetCurrentTrack(out _))
                            .Count(x => !(x is null));

                        return count.ToString();
                    }
                );

            yield return ("%music.queued%", () =>
                    {
                        var count = _players
                            .Sum(x => x.Value.GetQueuedTracks().Count);

                        return count.ToString();
                    }
                );
        }


        private void DisposeMusicPlayer(IMusicPlayer musicPlayer)
        {
            musicPlayer.Kill();
            _ = Task.Delay(10_000).ContinueWith(_ => musicPlayer.Dispose());
        }

        private void RemoveMusicPlayer(ulong guildId)
        {
            // SetMusicCount(guildId, false);
            _outputChannels.TryRemove(guildId, out _);
            if (_players.TryRemove(guildId, out var mp)) DisposeMusicPlayer(mp);
        }

        private Task ClientOnLeftGuild(SocketGuild guild)
        {
            // SetMusicCount(guild.Id, false);
            RemoveMusicPlayer(guild.Id);
            return Task.CompletedTask;
        }

        private async Task<IMusicPlayer?> CreateMusicPlayerInternalAsync(ulong guildId, ITextChannel defaultChannel)
        {
            var queue = new MusicQueue();
            var resolver = _trackResolveProvider;

            if (!_voiceStateService.TryGetProxy(guildId, out var proxy)) return null;

            var settings = await GetSettingsInternalAsync(guildId);

            ITextChannel? overrideChannel = null;
            if (settings.MusicChannelId is ulong channelId)
            {
                overrideChannel = _client.GetGuild(guildId)?.GetTextChannel(channelId);

                if (overrideChannel is null)
                    Log.Warning("Saved music output channel doesn't exist, falling back to current channel");
            }

            _outputChannels[guildId] = (defaultChannel, overrideChannel);
            // await SetMusicCount(guildId, true);

            var mp = new MusicPlayer(
                queue,
                resolver,
                proxy,
                settings.QualityPreset
            );

            mp.SetRepeat(settings.PlayerRepeat);

            if (settings.Volume >= 0 && settings.Volume <= 100)
                mp.SetVolume(settings.Volume);
            else
                Log.Error("Saved Volume is outside of valid range >= 0 && <=100 ({Volume})", settings.Volume);

            mp.OnCompleted += OnTrackCompleted(guildId);
            mp.OnStarted += OnTrackStarted(guildId);
            mp.OnQueueStopped += OnQueueStopped(guildId);

            return mp;
        }

        private Func<IMusicPlayer, IQueuedTrackInfo, Task> OnTrackCompleted(ulong guildId)
        {
            IUserMessage? lastFinishedMessage = null;
            return async (mp, trackInfo) =>
            {
                _ = lastFinishedMessage?.DeleteAsync();
                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithAuthor(eab => eab.WithName(GetText(guildId, "finished_song")).WithMusicIcon())
                    .WithDescription(trackInfo.PrettyName())
                    .WithFooter(trackInfo.PrettyTotalTime());

                lastFinishedMessage = await SendToOutputAsync(guildId, embed);
            };
        }

        private Func<IMusicPlayer, IQueuedTrackInfo, int, Task> OnTrackStarted(ulong guildId)
        {
            IUserMessage? lastPlayingMessage = null;
            return async (mp, trackInfo, index) =>
            {
                _ = lastPlayingMessage?.DeleteAsync();
                var embed = new EmbedBuilder().WithOkColor()
                    .WithAuthor(eab => eab.WithName(GetText(guildId, "playing_song", index + 1)).WithMusicIcon())
                    .WithDescription(trackInfo.PrettyName())
                    .WithFooter(ef => ef.WithText($"{mp.PrettyVolume()} | {trackInfo.PrettyInfo()}"));
                lastPlayingMessage = await SendToOutputAsync(guildId, embed);
                if (_settings.TryGetValue(guildId, out var settings))
                    if (settings.AutoPlay == 1)
                        if (mp.GetQueuedTracks().Count - 1 == index)
                        {
                            var uri = new Uri(trackInfo.Url);
                            var query = HttpUtility.ParseQueryString(uri.Query);
                            var videoid = query["v"];
                            var rand = new Random();
                            var e = _googleApiService.GetRelatedVideosAsync(videoid, 15).Result.ToList();
                            var inde = rand.Next(e.Count());
                            await mp.TryEnqueueTrackAsync(e[inde], "Mewdeko Autoplay", true, MusicPlatform.Spotify);
                        }

                ;
            };
        }

        private Func<IMusicPlayer, Task> OnQueueStopped(ulong guildId)
        {
            return mp =>
            {
                if (_settings.TryGetValue(guildId, out var settings))
                    if (settings.AutoDisconnect)
                        return LeaveVoiceChannelAsync(guildId);

                return Task.CompletedTask;
            };
        }

        private async Task<IList<(string Title, string Url)>> SearchYtLoaderVideosAsync(string query)
        {
            var result = await _ytLoader.LoadResultsAsync(query);
            return result.Select(x => (x.Title, x.Url)).ToList();
        }

        private async Task<IList<(string Title, string Url)>> SearchGoogleApiVideosAsync(string query)
        {
            var result = await _googleApiService.GetVideoInfosByKeywordAsync(query, 5);
            return result.Select(x => (x.Name, x.Url)).ToList();
        }

        private string GetText(ulong guildId, string key, params object[] args)
        {
            return _strings.GetText(key, guildId, args);
        }

        #region Settings

        private async Task<MusicPlayerSettings> GetSettingsInternalAsync(ulong guildId)
        {
            if (_settings.TryGetValue(guildId, out var settings))
                return settings;

            using var uow = _db.GetDbContext();
            var toReturn = _settings[guildId] = await uow._context.MusicPlayerSettings.ForGuildAsync(guildId);
            await uow.SaveChangesAsync();

            return toReturn;
        }

        private async Task ModifySettingsInternalAsync<TState>(
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

        public async Task<bool> SetMusicChannelAsync(ulong guildId, ulong? channelId)
        {
            if (channelId is null)
            {
                await UnsetMusicChannelAsync(guildId);
                return true;
            }

            var channel = _client.GetGuild(guildId)?.GetTextChannel(channelId.Value);
            if (channel is null)
                return false;

            await ModifySettingsInternalAsync(guildId, (settings, chId) => { settings.MusicChannelId = chId; },
                channelId);

            _outputChannels.AddOrUpdate(guildId,
                (channel, channel),
                (key, old) => (old.Default, channel));

            return true;
        }

        public async Task<bool> ToggleAutoPlay(ulong GuildId)
        {
            _settings.TryGetValue(GuildId, out var settings);
            if (settings is null)
            {
                await ModifySettingsInternalAsync(GuildId,
                    (settings, currentval) => { settings.AutoPlay = currentval; }, 1);
                _autoplay.AddOrUpdate(GuildId, (1, 1), (key, old) => (old.Default, 1));
                return true;
            }

            if (settings is not null && settings.AutoPlay == 0)
            {
                await ModifySettingsInternalAsync(GuildId,
                    (settings, currentval) => { settings.AutoPlay = currentval; }, 1);
                _autoplay.AddOrUpdate(GuildId, (1, 1), (key, old) => (old.Default, 1));
                return true;
            }

            await ModifySettingsInternalAsync(GuildId, (settings, currentval) => { settings.AutoPlay = currentval; },
                0);
            _autoplay.AddOrUpdate(GuildId, (0, 0), (key, old) => (old.Default, 0));
            return false;
        }

        public async Task UnsetMusicChannelAsync(ulong guildId)
        {
            await ModifySettingsInternalAsync(guildId, (settings, _) => { settings.MusicChannelId = null; },
                (ulong?)null);

            if (_outputChannels.TryGetValue(guildId, out var old))
                _outputChannels[guildId] = (old.Default, null);
        }

        public async Task SetRepeatAsync(ulong guildId, PlayerRepeatType repeatType)
        {
            await ModifySettingsInternalAsync(guildId, (settings, type) => { settings.PlayerRepeat = type; },
                repeatType);

            if (TryGetMusicPlayer(guildId, out var mp))
                mp.SetRepeat(repeatType);
        }

        public async Task SetVolumeAsync(ulong guildId, int value)
        {
            if (value < 0 || value > 100)
                throw new ArgumentOutOfRangeException(nameof(value));

            await ModifySettingsInternalAsync(guildId, (settings, newValue) => { settings.Volume = newValue; }, value);

            if (TryGetMusicPlayer(guildId, out var mp))
                mp.SetVolume(value);
        }

        public async Task<bool> ToggleAutoDisconnectAsync(ulong guildId)
        {
            var newState = false;
            await ModifySettingsInternalAsync(guildId,
                (settings, _) => { newState = settings.AutoDisconnect = !settings.AutoDisconnect; }, default(object));

            return newState;
        }

        #endregion
    }
}