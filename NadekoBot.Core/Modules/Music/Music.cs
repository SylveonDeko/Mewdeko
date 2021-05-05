using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Common;
using NadekoBot.Common.Attributes;
using NadekoBot.Common.Collections;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Core.Services.Impl;
using NadekoBot.Extensions;
using NadekoBot.Modules.Administration.Services;
using NadekoBot.Modules.Music.Common;
using NadekoBot.Modules.Music.Common.Exceptions;
using NadekoBot.Modules.Music.Extensions;
using NadekoBot.Modules.Music.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Music
{
    [NoPublicBot]
    public class Music : NadekoTopLevelModule<MusicService>
    {
        private readonly DiscordSocketClient _client;
        private readonly IBotCredentials _creds;
        private readonly IGoogleApiService _google;
        private readonly DbService _db;
        private readonly LogCommandService _logService;

        public Music(DiscordSocketClient client,
            IBotCredentials creds,
            IGoogleApiService google,
            DbService db,
            LogCommandService logService)
        {
            _client = client;
            _creds = creds;
            _google = google;
            _db = db;
            _logService = logService;
        }

        //private Task Client_UserVoiceStateUpdated(SocketUser iusr, SocketVoiceState oldState, SocketVoiceState newState)
        //{
        //    var t = Task.Run(() =>
        //    {
        //        var usr = iusr as SocketGuildUser;
        //        if (usr == null ||
        //            oldState.VoiceChannel == newState.VoiceChannel)
        //            return;

        //        var player = _music.GetPlayerOrDefault(usr.Guild.Id);

        //        if (player == null)
        //            return;

        //        try
        //        {
        //            //if bot moved
        //            if ((player.VoiceChannel == oldState.VoiceChannel) &&
        //                    usr.Id == _client.CurrentUser.Id)
        //            {
        //                //if (player.Paused && newState.VoiceChannel.Users.Count > 1) //unpause if there are people in the new channel
        //                //    player.TogglePause();
        //                //else if (!player.Paused && newState.VoiceChannel.Users.Count <= 1) // pause if there are no users in the new channel
        //                //    player.TogglePause();

        //               // player.SetVoiceChannel(newState.VoiceChannel);
        //                return;
        //            }

        //            ////if some other user moved
        //            //if ((player.VoiceChannel == newState.VoiceChannel && //if joined first, and player paused, unpause
        //            //        player.Paused &&
        //            //        newState.VoiceChannel.Users.Count >= 2) ||  // keep in mind bot is in the channel (+1)
        //            //    (player.VoiceChannel == oldState.VoiceChannel && // if left last, and player unpaused, pause
        //            //        !player.Paused &&
        //            //        oldState.VoiceChannel.Users.Count == 1))
        //            //{
        //            //    player.TogglePause();
        //            //    return;
        //            //}
        //        }
        //        catch
        //        {
        //            // ignored
        //        }
        //    });
        //    return Task.CompletedTask;
        //}

        private async Task InternalQueue(MusicPlayer mp, SongInfo songInfo, bool silent, bool queueFirst = false, bool forcePlay = false)
        {
            if (songInfo == null)
            {
                if (!silent)
                    await ReplyErrorLocalizedAsync("song_not_found").ConfigureAwait(false);
                return;
            }

            int index;
            try
            {
                index = queueFirst
                    ? mp.EnqueueNext(songInfo, forcePlay)
                    : mp.Enqueue(songInfo, forcePlay);
            }
            catch (QueueFullException)
            {
                await ReplyErrorLocalizedAsync("queue_full", mp.MaxQueueSize).ConfigureAwait(false);
                throw;
            }
            if (index != -1)
            {
                if (!silent)
                {
                    try
                    {
                        var embed = new EmbedBuilder().WithOkColor()
                                        .WithAuthor(eab => eab.WithName(GetText("queued_song") + " #" + (index + 1)).WithMusicIcon())
                                        .WithDescription($"{songInfo.PrettyName}\n{GetText("queue")} ")
                                        .WithFooter(ef => ef.WithText(songInfo.PrettyProvider));

                        if (Uri.IsWellFormedUriString(songInfo.Thumbnail, UriKind.Absolute))
                            embed.WithThumbnailUrl(songInfo.Thumbnail);

                        var queuedMessage = await mp.OutputTextChannel.EmbedAsync(embed).ConfigureAwait(false);
                        if (mp.Stopped)
                        {
                            (await ReplyErrorLocalizedAsync("queue_stopped", Format.Code(Prefix + "play")).ConfigureAwait(false)).DeleteAfter(10, _logService);
                        }
                        queuedMessage?.DeleteAfter(10, _logService);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Play([Leftover] string query = null)
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(query))
            {
                await Next().ConfigureAwait(false);
            }
            else if (int.TryParse(query, out var index))
                if (index >= 1)
                    mp.SetIndex(index - 1);
                else
                    return;
            else
            {
                try
                {
                    await InternalPlay(query, forceplay: true).ConfigureAwait(false);
                }
                catch { }
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public Task Queue([Leftover] string query)
            => InternalPlay(query, forceplay: false);

        private async Task InternalPlay(string query, bool forceplay)
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            var songInfo = await _service.ResolveSong(query, ctx.User.ToString()).ConfigureAwait(false);
            try { await InternalQueue(mp, songInfo, false, forcePlay: forceplay).ConfigureAwait(false); } catch (QueueFullException) { return; }
            if ((await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false)).GetPermissions((IGuildChannel)ctx.Channel).ManageMessages)
            {
                ctx.Message.DeleteAfter(10, _logService);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task QueueNext([Leftover] string query)
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            var songInfo = await _service.ResolveSong(query, ctx.User.ToString()).ConfigureAwait(false);
            try { await InternalQueue(mp, songInfo, false, true).ConfigureAwait(false); } catch (QueueFullException) { return; }
            if ((await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false)).GetPermissions((IGuildChannel)ctx.Channel).ManageMessages)
            {
                ctx.Message.DeleteAfter(10, _logService);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task QueueSearch([Leftover] string query)
        {
            var videos = (await _google.GetVideoInfosByKeywordAsync(query, 5).ConfigureAwait(false))
                .ToArray();

            if (!videos.Any())
            {
                await ReplyErrorLocalizedAsync("song_not_found").ConfigureAwait(false);
                return;
            }

            var msg = await ctx.Channel.SendConfirmAsync(string.Join("\n", videos.Select((x, i) => $"`{i + 1}.`\n\t{Format.Bold(x.Name)}\n\t{x.Url}"))).ConfigureAwait(false);

            try
            {
                var input = await GetUserInputAsync(ctx.User.Id, ctx.Channel.Id).ConfigureAwait(false);
                if (input == null
                    || !int.TryParse(input, out var index)
                    || (index -= 1) < 0
                    || index >= videos.Length)
                {
                    _logService.AddDeleteIgnore(msg.Id);
                    try { await msg.DeleteAsync().ConfigureAwait(false); } catch { }
                    return;
                }

                query = videos[index].Url;

                await Queue(query).ConfigureAwait(false);
            }
            finally
            {
                _logService.AddDeleteIgnore(msg.Id);
                try { await msg.DeleteAsync().ConfigureAwait(false); } catch { }
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ListQueue(int page = 0)
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            var (current, songs) = mp.QueueArray();

            if (!songs.Any())
            {
                await ReplyErrorLocalizedAsync("no_player").ConfigureAwait(false);
                return;
            }

            if (--page < -1)
                return;

            try { await mp.UpdateSongDurationsAsync().ConfigureAwait(false); } catch { }

            const int itemsPerPage = 10;

            if (page == -1)
                page = current / itemsPerPage;

            //if page is 0 (-1 after this decrement) that means default to the page current song is playing from
            var total = mp.TotalPlaytime;
            var totalStr = total == TimeSpan.MaxValue ? "∞" : GetText("time_format",
                (int)total.TotalHours,
                total.Minutes,
                total.Seconds);
            var maxPlaytime = mp.MaxPlaytimeSeconds;

            EmbedBuilder printAction(int curPage)
            {
                var startAt = itemsPerPage * curPage;
                var number = 0 + startAt;
                var desc = string.Join("\n", songs
                        .Skip(startAt)
                        .Take(itemsPerPage)
                        .Select(v =>
                        {
                            if (number++ == current)
                                return $"**⇒**`{number}.` {v.PrettyFullName}";
                            else
                                return $"`{number}.` {v.PrettyFullName}";
                        }));

                desc = $"`🔊` {songs[current].PrettyFullName}\n\n" + desc;

                var add = "";
                if (mp.Stopped)
                    add += Format.Bold(GetText("queue_stopped", Format.Code(Prefix + "play"))) + "\n";
                var mps = mp.MaxPlaytimeSeconds;
                if (mps > 0)
                    add += Format.Bold(GetText("song_skips_after", TimeSpan.FromSeconds(mps).ToString("HH\\:mm\\:ss"))) + "\n";
                if (mp.RepeatCurrentSong)
                    add += "🔂 " + GetText("repeating_cur_song") + "\n";
                else if (mp.Shuffle)
                    add += "🔀 " + GetText("shuffling_playlist") + "\n";
                else
                {
                    if (mp.Autoplay)
                        add += "↪ " + GetText("autoplaying") + "\n";
                    if (mp.FairPlay && !mp.Autoplay)
                        add += " " + GetText("fairplay") + "\n";
                    else if (mp.RepeatPlaylist)
                        add += "🔁 " + GetText("repeating_playlist") + "\n";
                }

                if (!string.IsNullOrWhiteSpace(add))
                    desc = add + "\n" + desc;

                var embed = new EmbedBuilder()
                    .WithAuthor(eab => eab.WithName(GetText("player_queue", curPage + 1, (songs.Length / itemsPerPage) + 1))
                        .WithMusicIcon())
                    .WithDescription(desc)
                    .WithFooter(ef => ef.WithText($"{mp.PrettyVolume} | {songs.Length} " +
                                                  $"{("tracks".SnPl(songs.Length))} | {totalStr}"))
                    .WithOkColor();

                return embed;
            }

            await ctx.SendPaginatedConfirmAsync(page, printAction, songs.Length,
                itemsPerPage, false).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Next(int skipCount = 1)
        {
            if (skipCount < 1)
                return;

            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);

            mp.Next(skipCount);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Stop()
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            mp.Stop();
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task AutoDisconnect()
        {
            var newVal = _service.ToggleAutoDc(ctx.Guild.Id);

            if (newVal)
                await ReplyConfirmLocalizedAsync("autodc_enable").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("autodc_disable").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Destroy()
        {
            await _service.DestroyPlayer(ctx.Guild.Id).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Pause()
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            mp.TogglePause();
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Volume(int val)
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            if (val < 0 || val > 100)
            {
                await ReplyErrorLocalizedAsync("volume_input_invalid").ConfigureAwait(false);
                return;
            }
            mp.SetVolume(val);
            await ReplyConfirmLocalizedAsync("volume_set", val).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Defvol([Leftover] int val)
        {
            if (val < 0 || val > 100)
            {
                await ReplyErrorLocalizedAsync("volume_input_invalid").ConfigureAwait(false);
                return;
            }
            using (var uow = _db.GetDbContext())
            {
                uow.GuildConfigs.ForId(ctx.Guild.Id, set => set).DefaultMusicVolume = val / 100.0f;
                uow.SaveChanges();
            }
            await ReplyConfirmLocalizedAsync("defvol_set", val).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public async Task SongRemove(int index)
        {
            if (index < 1)
            {
                await ReplyErrorLocalizedAsync("removed_song_error").ConfigureAwait(false);
                return;
            }
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            try
            {
                var song = mp.RemoveAt(index - 1);
                var embed = new EmbedBuilder()
                            .WithAuthor(eab => eab.WithName(GetText("removed_song") + " #" + (index)).WithMusicIcon())
                            .WithDescription(song.PrettyName)
                            .WithFooter(ef => ef.WithText(song.PrettyInfo))
                            .WithErrorColor();

                await mp.OutputTextChannel.EmbedAsync(embed).ConfigureAwait(false);
            }
            catch (ArgumentOutOfRangeException)
            {
                await ReplyErrorLocalizedAsync("removed_song_error").ConfigureAwait(false);
            }
        }

        public enum All { All }
        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        public async Task SongRemove(All _)
        {
            var mp = _service.GetPlayerOrDefault(ctx.Guild.Id);
            if (mp == null)
                return;
            mp.Stop(true);
            await ReplyConfirmLocalizedAsync("queue_cleared").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Playlists([Leftover] int num = 1)
        {
            if (num <= 0)
                return;

            List<MusicPlaylist> playlists;

            using (var uow = _db.GetDbContext())
            {
                playlists = uow.MusicPlaylists.GetPlaylistsOnPage(num);
            }

            var embed = new EmbedBuilder()
                .WithAuthor(eab => eab.WithName(GetText("playlists_page", num)).WithMusicIcon())
                .WithDescription(string.Join("\n", playlists.Select(r =>
                    GetText("playlists", r.Id, r.Name, r.Author, r.Songs.Count))))
                .WithOkColor();
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task DeletePlaylist([Leftover] int id)
        {
            var success = false;
            try
            {
                using (var uow = _db.GetDbContext())
                {
                    var pl = uow.MusicPlaylists.GetById(id);

                    if (pl != null)
                    {
                        if (_creds.IsOwner(ctx.User) || pl.AuthorId == ctx.User.Id)
                        {
                            uow.MusicPlaylists.Remove(pl);
                            await uow.SaveChangesAsync();
                            success = true;
                        }
                    }
                }

                if (!success)
                    await ReplyErrorLocalizedAsync("playlist_delete_fail").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("playlist_deleted").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Warn(ex);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task PlaylistShow(int id, int page = 1)
        {
            if (page-- < 1)
                return;

            MusicPlaylist mpl;
            using (var uow = _db.GetDbContext())
            {
                mpl = uow.MusicPlaylists.GetWithSongs(id);
            }

            await ctx.SendPaginatedConfirmAsync(page, (cur) =>
            {
                var i = 0;
                var str = string.Join("\n", mpl.Songs
                    .Skip(cur * 20)
                    .Take(20)
                    .Select(x => $"`{++i}.` [{x.Title.TrimTo(45)}]({x.Query}) `{x.Provider}`"));
                return new EmbedBuilder()
                    .WithTitle($"\"{mpl.Name}\" by {mpl.Author}")
                    .WithOkColor()
                    .WithDescription(str);
            }, mpl.Songs.Count, 20).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Save([Leftover] string name)
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);

            var songs = mp.QueueArray().Songs
                .Select(s => new PlaylistSong()
                {
                    Provider = s.Provider,
                    ProviderType = s.ProviderType,
                    Title = s.Title,
                    Query = s.Query,
                }).ToList();

            MusicPlaylist playlist;
            using (var uow = _db.GetDbContext())
            {
                playlist = new MusicPlaylist
                {
                    Name = name,
                    Author = ctx.User.Username,
                    AuthorId = ctx.User.Id,
                    Songs = songs.ToList(),
                };
                uow.MusicPlaylists.Add(playlist);
                await uow.SaveChangesAsync();
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithTitle(GetText("playlist_saved"))
                .AddField(efb => efb.WithName(GetText("name")).WithValue(name))
                .AddField(efb => efb.WithName(GetText("id")).WithValue(playlist.Id.ToString()))).ConfigureAwait(false);
        }

        private static readonly ConcurrentHashSet<ulong> PlaylistLoadBlacklist = new ConcurrentHashSet<ulong>();

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Load([Leftover] int id)
        {
            if (!PlaylistLoadBlacklist.Add(ctx.Guild.Id))
                return;
            try
            {
                var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
                MusicPlaylist mpl;
                using (var uow = _db.GetDbContext())
                {
                    mpl = uow.MusicPlaylists.GetWithSongs(id);
                }

                if (mpl == null)
                {
                    await ReplyErrorLocalizedAsync("playlist_id_not_found").ConfigureAwait(false);
                    return;
                }
                IUserMessage msg = null;
                try { msg = await ctx.Channel.SendMessageAsync(GetText("attempting_to_queue", Format.Bold(mpl.Songs.Count.ToString()))).ConfigureAwait(false); } catch (Exception ex) { _log.Warn(ex); }
                foreach (var item in mpl.Songs)
                {
                    try
                    {
                        await Task.Yield();
                        var song = await _service.ResolveSong(item.Query,
                            ctx.User.ToString(),
                            item.ProviderType).ConfigureAwait(false);
                        var queueTask = InternalQueue(mp, song, true);
                        await Task.WhenAll(Task.Delay(1000), queueTask).ConfigureAwait(false);
                    }
                    catch (SongNotFoundException) { }
                    catch { break; }
                }
                if (msg != null)
                    await msg.ModifyAsync(m => m.Content = GetText("playlist_queue_complete")).ConfigureAwait(false);
            }
            finally
            {
                PlaylistLoadBlacklist.TryRemove(ctx.Guild.Id);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Fairplay()
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            var val = mp.FairPlay = !mp.FairPlay;

            if (val)
            {
                await ReplyConfirmLocalizedAsync("fp_enabled").ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("fp_disabled").ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task SongAutoDelete()
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            var val = mp.AutoDelete = !mp.AutoDelete;

            _service.SetSongAutoDelete(ctx.Guild.Id, val);
            if (val)
            {
                await ReplyConfirmLocalizedAsync("sad_enabled").ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("sad_disabled").ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task SoundCloudQueue([Leftover] string query)
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            var song = await _service.ResolveSong(query, ctx.User.ToString(), MusicType.Soundcloud).ConfigureAwait(false);
            await InternalQueue(mp, song, false).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task SoundCloudPl([Leftover] string pl)
        {
            pl = pl?.Trim();

            if (string.IsNullOrWhiteSpace(pl))
                return;

            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);

            using (var http = new HttpClient())
            {
                var scvids = JObject.Parse(await http.GetStringAsync($"https://scapi.nadeko.bot/resolve?url={pl}").ConfigureAwait(false))["tracks"].ToObject<SoundCloudVideo[]>();
                IUserMessage msg = null;
                try { msg = await ctx.Channel.SendMessageAsync(GetText("attempting_to_queue", Format.Bold(scvids.Length.ToString()))).ConfigureAwait(false); } catch { }
                foreach (var svideo in scvids)
                {
                    try
                    {
                        await Task.Yield();
                        var sinfo = await svideo.GetSongInfo().ConfigureAwait(false);
                        sinfo.QueuerName = ctx.User.ToString();
                        await InternalQueue(mp, sinfo, true).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _log.Warn(ex);
                        break;
                    }
                }
                if (msg != null)
                    await msg.ModifyAsync(m => m.Content = GetText("playlist_queue_complete")).ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task NowPlaying()
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            var (_, currentSong) = mp.Current;
            if (currentSong == null)
                return;
            try { await mp.UpdateSongDurationsAsync().ConfigureAwait(false); } catch { }

            var embed = new EmbedBuilder().WithOkColor()
                            .WithAuthor(eab => eab.WithName(GetText("now_playing")).WithMusicIcon())
                            .WithDescription(currentSong.PrettyName)
                            .WithThumbnailUrl(currentSong.Thumbnail)
                            .WithFooter(ef => ef.WithText(mp.PrettyVolume + " | " + mp.PrettyFullTime + $" | {currentSong.PrettyProvider} | {currentSong.QueuerName}"));

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task PlaylistShuffle()
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            var val = mp.ToggleShuffle();
            if (val)
                await ReplyConfirmLocalizedAsync("songs_shuffle_enable").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("songs_shuffle_disable").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Playlist([Leftover] string playlist)
        {
            if (string.IsNullOrWhiteSpace(playlist))
                return;

            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);

            string plId = null;
            try
            {
                plId = (await _google.GetPlaylistIdsByKeywordsAsync(playlist).ConfigureAwait(false)).FirstOrDefault();
            }
            catch (Exception ex)
            {
                _log.Warn(ex.Message);
            }

            if (plId == null)
            {
                await ReplyErrorLocalizedAsync("no_search_results").ConfigureAwait(false);
                return;
            }
            var ids = await _google.GetPlaylistTracksAsync(plId, 500).ConfigureAwait(false);
            if (!ids.Any())
            {
                await ReplyErrorLocalizedAsync("no_search_results").ConfigureAwait(false);
                return;
            }
            var count = ids.Count();
            var msg = await ctx.Channel.SendMessageAsync("🎵 " + GetText("attempting_to_queue",
                Format.Bold(count.ToString()))).ConfigureAwait(false);

            foreach (var song in ids)
            {
                try
                {
                    if (mp.Exited)
                        return;

                    await Task.WhenAll(Task.Delay(150), InternalQueue(mp, await _service.ResolveSong(song, ctx.User.ToString(), MusicType.YouTube).ConfigureAwait(false), true)).ConfigureAwait(false);
                }
                catch (SongNotFoundException) { }
                catch { break; }
            }

            await msg.ModifyAsync(m => m.Content = "✅ " + Format.Bold(GetText("playlist_queue_complete"))).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Radio(string radioLink)
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            var song = await _service.ResolveSong(radioLink, ctx.User.ToString(), MusicType.Radio).ConfigureAwait(false);
            await InternalQueue(mp, song, false).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        public async Task Local([Leftover] string path)
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            var song = await _service.ResolveSong(path, ctx.User.ToString(), MusicType.Local).ConfigureAwait(false);
            await InternalQueue(mp, song, false).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        public async Task LocalPlaylist([Leftover] string dirPath)
        {
            if (string.IsNullOrWhiteSpace(dirPath))
                return;

            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);

            DirectoryInfo dir;
            try { dir = new DirectoryInfo(dirPath); } catch { return; }
            var fileEnum = dir.GetFiles("*", SearchOption.AllDirectories)
                                .Where(x => !x.Attributes.HasFlag(FileAttributes.Hidden | FileAttributes.System) && x.Extension != ".jpg" && x.Extension != ".png");
            foreach (var file in fileEnum)
            {
                try
                {
                    await Task.Yield();
                    var song = await _service.ResolveSong(file.FullName, ctx.User.ToString(), MusicType.Local).ConfigureAwait(false);
                    await InternalQueue(mp, song, true).ConfigureAwait(false);
                }
                catch (QueueFullException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.Warn(ex);
                    break;
                }
            }
            await ReplyConfirmLocalizedAsync("dir_queue_complete").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Move()
        {
            var vch = ((IGuildUser)ctx.User).VoiceChannel;

            if (vch == null)
                return;

            var mp = _service.GetPlayerOrDefault(ctx.Guild.Id);

            if (mp == null)
                return;

            await mp.SetVoiceChannel(vch).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task MoveSong([Leftover] string fromto)
        {
            if (string.IsNullOrWhiteSpace(fromto))
                return;

            MusicPlayer mp = _service.GetPlayerOrDefault(ctx.Guild.Id);
            if (mp == null)
                return;

            fromto = fromto?.Trim();
            var fromtoArr = fromto.Split('>');

            SongInfo s;
            if (fromtoArr.Length != 2 || !int.TryParse(fromtoArr[0], out var n1) ||
                !int.TryParse(fromtoArr[1], out var n2) || n1 < 1 || n2 < 1 || n1 == n2
                || (s = mp.MoveSong(--n1, --n2)) == null)
            {
                await ReplyConfirmLocalizedAsync("invalid_input").ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(s.Title.TrimTo(65))
                .WithUrl(s.SongUrl)
                .WithAuthor(eab => eab.WithName(GetText("song_moved")).WithIconUrl("https://cdn.discordapp.com/attachments/155726317222887425/258605269972549642/music1.png"))
                .AddField(fb => fb.WithName(GetText("from_position")).WithValue($"#{n1 + 1}").WithIsInline(true))
                .AddField(fb => fb.WithName(GetText("to_position")).WithValue($"#{n2 + 1}").WithIsInline(true))
                .WithColor(NadekoBot.OkColor);

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task SetMaxQueue(uint size = 0)
        {
            if (size < 0)
                return;
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);

            mp.MaxQueueSize = size;

            if (size == 0)
                await ReplyConfirmLocalizedAsync("max_queue_unlimited").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("max_queue_x", size).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task SetMaxPlaytime(uint seconds)
        {
            if (seconds < 15 && seconds != 0)
                return;

            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            mp.MaxPlaytimeSeconds = seconds;
            if (seconds == 0)
                await ReplyConfirmLocalizedAsync("max_playtime_none").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("max_playtime_set", seconds).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ReptCurSong()
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            var (_, currentSong) = mp.Current;
            if (currentSong == null)
                return;
            var currentValue = mp.ToggleRepeatSong();

            if (currentValue)
                await ctx.Channel.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithAuthor(eab => eab.WithMusicIcon().WithName("🔂 " + GetText("repeating_track")))
                    .WithDescription(currentSong.PrettyName)
                    .WithFooter(ef => ef.WithText(currentSong.PrettyInfo))).ConfigureAwait(false);
            else
                await ctx.Channel.SendConfirmAsync("🔂 " + GetText("repeating_track_stopped"))
                                            .ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task PlaylistRepeat()
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);
            var currentValue = mp.ToggleRepeatPlaylist();
            if (currentValue)
                await ReplyConfirmLocalizedAsync("rpl_enabled").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("rpl_disabled").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Autoplay()
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);

            if (!mp.ToggleAutoplay())
                await ReplyConfirmLocalizedAsync("autoplay_disabled").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("autoplay_enabled").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageMessages)]
        public async Task SetMusicChannel()
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);

            mp.OutputTextChannel = (ITextChannel)ctx.Channel;
            _service.SetMusicChannel(ctx.Guild.Id, ctx.Channel.Id);

            await ReplyConfirmLocalizedAsync("set_music_channel").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPerm.ManageMessages)]
        public async Task UnsetMusicChannel()
        {
            var mp = await _service.GetOrCreatePlayer(Context).ConfigureAwait(false);

            mp.OutputTextChannel = mp.OriginalTextChannel;
            _service.SetMusicChannel(ctx.Guild.Id, null);

            await ReplyConfirmLocalizedAsync("unset_music_channel").ConfigureAwait(false);
        }
    }
}
