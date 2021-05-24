using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko.Common.Collections;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Core.Services.Impl;
using Mewdeko.Extensions;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Music.Common.Exceptions;
using Mewdeko.Modules.Music.Common.SongResolver;
using Microsoft.EntityFrameworkCore;
using NLog;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

namespace Mewdeko.Modules.Music.Services
{
    public class MusicService : INService, IUnloadableService
    {
        public const string MusicDataPath = "data/musicdata";
        public static string token;
        private static EmbedIOAuthServer _server;
        private readonly Timer _botlistTimer;

        private readonly DiscordSocketClient _client;
        private readonly IBotCredentials _creds;
        private readonly DbService _db;
        private readonly ConcurrentDictionary<ulong, float> _defaultVolumes;

        private readonly IGoogleApiService _google;
        private readonly ILocalization _localization;
        private readonly Logger _log;
        private readonly ConcurrentDictionary<ulong, MusicSettings> _musicSettings;
        private readonly SoundCloudApiService _sc;
        private readonly IBotStrings _strings;

        public MusicService(DiscordSocketClient client, IGoogleApiService google,
            IBotStrings strings, ILocalization localization, DbService db,
            SoundCloudApiService sc, IBotCredentials creds, Mewdeko bot)
        {
            _client = client;
            _google = google;
            _strings = strings;
            _localization = localization;
            _db = db;
            _sc = sc;
            _creds = creds;
            _log = LogManager.GetCurrentClassLogger();
            _musicSettings = bot.AllGuildConfigs.ToDictionary(x => x.GuildId, x => x.MusicSettings)
                .ToConcurrent();

            _client.LeftGuild += _client_LeftGuild;
            try
            {
                Directory.Delete(MusicDataPath, true);
            }
            catch
            {
            }

            _defaultVolumes = new ConcurrentDictionary<ulong, float>(
                bot.AllGuildConfigs
                    .ToDictionary(x => x.GuildId, x => x.DefaultMusicVolume));

            AutoDcServers =
                new ConcurrentHashSet<ulong>(bot.AllGuildConfigs.Where(x => x.AutoDcFromVc).Select(x => x.GuildId));

            Directory.CreateDirectory(MusicDataPath);
            _botlistTimer = new Timer(async state =>
            {
                try
                {
                    _server = new EmbedIOAuthServer(new Uri("http://localhost:1234/callback"),
                        1234);
                    await _server.Start();

                    _server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;
                    var request = new LoginRequest(_server.BaseUri, "***REMOVED***",
                        LoginRequest.ResponseType.Code)
                    {
                        Scope = new List<string> {Scopes.UserReadEmail}
                    };
                    BrowserUtil.Open(request.ToUri());
                }
                catch (Exception ex)
                {
                    _log.Error(ex);
                    // ignored
                }
            }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }


        public ConcurrentHashSet<ulong> AutoDcServers { get; }

        public ConcurrentDictionary<ulong, MusicPlayer> MusicPlayers { get; } = new();

        public Task Unload()
        {
            _client.LeftGuild -= _client_LeftGuild;
            return Task.CompletedTask;
        }

        private static async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            await _server.Stop();
            _server.Dispose();

            var config = SpotifyClientConfig.CreateDefault();
            var tokenResponse = await new OAuthClient(config).RequestToken(
                new AuthorizationCodeTokenRequest(
                    "***REMOVED***", "***REMOVED***", response.Code,
                    new Uri("http://localhost:1234/callback")
                )
            );

            token = tokenResponse.AccessToken;
        }

        private Task _client_LeftGuild(SocketGuild arg)
        {
            var t = DestroyPlayer(arg.Id);
            return Task.CompletedTask;
        }

        public float GetDefaultVolume(ulong guildId)
        {
            return _defaultVolumes.GetOrAdd(guildId, id =>
            {
                using (var uow = _db.GetDbContext())
                {
                    return uow.GuildConfigs.ForId(guildId, set => set).DefaultMusicVolume;
                }
            });
        }

        public Task<MusicPlayer> GetOrCreatePlayer(ICommandContext context)
        {
            var gUsr = (IGuildUser) context.User;
            var txtCh = (ITextChannel) context.Channel;
            var vCh = gUsr.VoiceChannel;
            return GetOrCreatePlayer(context.Guild.Id, vCh, txtCh);
        }

        public async Task<MusicPlayer> GetOrCreatePlayer(ulong guildId, IVoiceChannel voiceCh, ITextChannel textCh)
        {
            string GetText(string text, params object[] replacements)
            {
                return _strings.GetText(text, _localization.GetCultureInfo(textCh.Guild), "Music".ToLowerInvariant(),
                    replacements);
            }

            if (voiceCh == null || voiceCh.Guild != textCh.Guild)
            {
                if (textCh != null) await textCh.SendErrorAsync(GetText("must_be_in_voice")).ConfigureAwait(false);
                throw new NotInVoiceChannelException();
            }

            return MusicPlayers.GetOrAdd(guildId, _ =>
            {
                var vol = GetDefaultVolume(guildId);
                if (!_musicSettings.TryGetValue(guildId, out var ms))
                    ms = new MusicSettings();

                var mp = new MusicPlayer(this, ms, _google, voiceCh, textCh, vol);

                IUserMessage playingMessage = null;
                IUserMessage lastFinishedMessage = null;

                mp.OnCompleted += async (s, song) =>
                {
                    try
                    {
                        lastFinishedMessage?.DeleteAfter(0);

                        try
                        {
                            lastFinishedMessage = await mp.OutputTextChannel.EmbedAsync(new EmbedBuilder().WithOkColor()
                                    .WithAuthor(eab => eab.WithName(GetText("finished_song")).WithMusicIcon())
                                    .WithDescription(song.PrettyName)
                                    .WithFooter(ef => ef.WithText(song.PrettyInfo)))
                                .ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }

                        var (Index, Current) = mp.Current;
                        if (Current == null
                            && !mp.RepeatCurrentSong
                            && !mp.RepeatPlaylist
                            && !mp.FairPlay
                            && AutoDcServers.Contains(guildId))
                            await DestroyPlayer(guildId).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                };
                mp.OnStarted += async (player, song) =>
                {
                    //try { await mp.UpdateSongDurationsAsync().ConfigureAwait(false); }
                    //catch
                    //{
                    //    // ignored
                    //}
                    var sender = player;
                    if (sender == null)
                        return;
                    try
                    {
                        playingMessage?.DeleteAfter(0);

                        playingMessage = await mp.OutputTextChannel.EmbedAsync(new EmbedBuilder().WithOkColor()
                                .WithAuthor(
                                    eab => eab.WithName(GetText("playing_song", song.Index + 1)).WithMusicIcon())
                                .WithDescription(song.Song.PrettyName)
                                .WithImageUrl(song.Song.Thumbnail)
                                .WithFooter(ef => ef.WithText(mp.PrettyVolume + " | " + song.Song.PrettyInfo)))
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                };
                mp.OnPauseChanged += async (player, paused) =>
                {
                    try
                    {
                        IUserMessage msg;
                        if (paused)
                            msg = await mp.OutputTextChannel.SendConfirmAsync(GetText("paused")).ConfigureAwait(false);
                        else
                            msg = await mp.OutputTextChannel.SendConfirmAsync(GetText("resumed")).ConfigureAwait(false);

                        msg?.DeleteAfter(10);
                    }
                    catch
                    {
                        // ignored
                    }
                };
                _log.Info("Done creating");
                return mp;
            });
        }

        public MusicPlayer GetPlayerOrDefault(ulong guildId)
        {
            if (MusicPlayers.TryGetValue(guildId, out var mp))
                return mp;
            return null;
        }

        public async Task TryQueueRelatedSongAsync(SongInfo song, ITextChannel txtCh, IVoiceChannel vch)
        {
            var related = (await _google.GetRelatedVideosAsync(song.VideoId, 4).ConfigureAwait(false)).ToArray();
            if (!related.Any())
                return;

            var si = await ResolveSong(related[new MewdekoRandom().Next(related.Length)],
                _client.CurrentUser.ToString(), MusicType.YouTube).ConfigureAwait(false);
            if (si == null)
                throw new SongNotFoundException();
            var mp = await GetOrCreatePlayer(txtCh.GuildId, vch, txtCh).ConfigureAwait(false);
            mp.Enqueue(si);
        }

        public async Task<SongInfo> ResolveSong(string query, string queuerName, MusicType? musicType = null)
        {
            query.ThrowIfNull(nameof(query));
            ISongResolverFactory resolverFactory = new SongResolverFactory(_sc);
            var strategy = await resolverFactory.GetResolveStrategy(query, musicType).ConfigureAwait(false);
            var sinfo = await strategy.ResolveSong(query).ConfigureAwait(false);

            if (sinfo == null)
                return null;

            sinfo.QueuerName = queuerName;

            return sinfo;
        }

        public async Task DestroyAllPlayers()
        {
            foreach (var key in MusicPlayers.Keys) await DestroyPlayer(key).ConfigureAwait(false);
        }

        public async Task DestroyPlayer(ulong id)
        {
            if (MusicPlayers.TryRemove(id, out var mp))
                await mp.Destroy().ConfigureAwait(false);
        }

        public bool ToggleAutoDc(ulong id)
        {
            bool val;
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(id, set => set);
                val = gc.AutoDcFromVc = !gc.AutoDcFromVc;
                uow.SaveChanges();
            }

            if (val)
                AutoDcServers.Add(id);
            else
                AutoDcServers.TryRemove(id);

            return val;
        }

        public void UpdateSettings(ulong id, MusicSettings musicSettings)
        {
            _musicSettings.AddOrUpdate(id, musicSettings, delegate { return musicSettings; });
        }

        public void SetMusicChannel(ulong guildId, ulong? cid)
        {
            using (var uow = _db.GetDbContext())
            {
                var ms = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.MusicSettings)).MusicSettings;
                ms.MusicChannelId = cid;
                uow.SaveChanges();
            }
        }

        public void SetSongAutoDelete(ulong guildId, bool val)
        {
            using (var uow = _db.GetDbContext())
            {
                var ms = uow.GuildConfigs.ForId(guildId, set => set.Include(x => x.MusicSettings)).MusicSettings;
                ms.SongAutoDelete = val;
                uow.SaveChanges();
            }
        }
    }
}