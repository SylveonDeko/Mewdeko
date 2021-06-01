using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Services;
using Mewdeko.Extensions;
using Mewdeko.Modules;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Music.Services;
using SpotifyAPI.Web;
using KSoftNet;
namespace Mewdeko.Core.Modules.Music
{
    [NoPublicBot]
    public sealed partial class Music : MewdekoModule<IMusicService>
    {
        private readonly IGoogleApiService _google;
        private readonly LogCommandService _logService;
        private readonly KSoftAPI _ksoft;

        public Music(IGoogleApiService google, LogCommandService _logService, KSoftAPI ks)
        {
            _ksoft = ks;
            _google = google;
            this._logService = _logService;
        }
        
        private async Task<bool> ValidateAsync()
        {
            var user = (IGuildUser) ctx.User;
            var userVoiceChannelId = user.VoiceChannel?.Id;
            
            if (userVoiceChannelId is null)
            {
                await ReplyErrorLocalizedAsync("must_be_in_voice");
                return false;
            }

            var currentUser = await ctx.Guild.GetCurrentUserAsync();
            if (currentUser.VoiceChannel?.Id != userVoiceChannelId)
            {
                await ReplyErrorLocalizedAsync("not_with_bot_in_voice");
                return false;
            }

            return true;
        }

        private static readonly SemaphoreSlim voiceChannelLock = new SemaphoreSlim(1, 1);
        private async Task EnsureBotInVoiceChannelAsync(ulong voiceChannelId, IGuildUser botUser = null)
        {
            botUser ??= await ctx.Guild.GetCurrentUserAsync();
            await voiceChannelLock.WaitAsync();
            try
            {
                if (botUser.VoiceChannel?.Id is null || !_service.TryGetMusicPlayer(Context.Guild.Id, out _))
                    await _service.JoinVoiceChannelAsync(ctx.Guild.Id, voiceChannelId);
            }
            finally
            {
                voiceChannelLock.Release();
            }
        }
        
        private async Task<bool> QueuePreconditionInternalAsync()
        {
            var user = (IGuildUser) Context.User;
            var voiceChannelId = user.VoiceChannel?.Id;
            
            if (voiceChannelId is null)
            {
                await ReplyErrorLocalizedAsync("must_be_in_voice");
                return false;
            }

            _ = ctx.Channel.TriggerTypingAsync();
            
            var botUser = await ctx.Guild.GetCurrentUserAsync();
            await EnsureBotInVoiceChannelAsync(voiceChannelId!.Value, botUser);
            
            if (botUser.VoiceChannel?.Id != voiceChannelId)
            {
                await ReplyErrorLocalizedAsync("not_with_bot_in_voice");
                return false;
            }

            return true;
        }

        private async Task QueueByQuery(string query, bool asNext = false, MusicPlatform? forcePlatform = null)
        {
            var succ = await QueuePreconditionInternalAsync();
            if (!succ)
                return;
            
            var mp = _service.GetOrCreateMusicPlayer((ITextChannel) Context.Channel);
            if (mp is null)
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }
            
            var (trackInfo, index) = await mp.TryEnqueueTrackAsync(query, 
                Context.User.ToString(),
                asNext,
                forcePlatform);
            if (trackInfo is null)
            {
                await ReplyErrorLocalizedAsync("song_not_found");
                return;
            }

            try
            {
                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithAuthor(eab => eab.WithName(GetText("queued_song") + " #" + (index + 1)).WithMusicIcon())
                    .WithDescription($"{trackInfo.PrettyName()}\n{GetText("queue")} ")
                    .WithFooter(ef => ef.WithText(trackInfo.Platform.ToString()));

                if (!string.IsNullOrWhiteSpace(trackInfo.Thumbnail))
                    embed.WithThumbnailUrl(trackInfo.Thumbnail);

                var queuedMessage = await _service.SendToOutputAsync(Context.Guild.Id, embed).ConfigureAwait(false);
                queuedMessage?.DeleteAfter(10, _logService);
                if (mp.IsStopped)
                {
                    var msg = await ReplyErrorLocalizedAsync("queue_stopped", Format.Code(Prefix + "play"));
                    msg.DeleteAfter(10, _logService);
                }
            }
            catch
            {
                // ignored
            }
        }

        private async Task MoveToIndex(int index)
        {
            if (--index < 0)
                return;
            
            var succ = await QueuePreconditionInternalAsync();
            if (!succ)
                return;
            
            var mp = _service.GetOrCreateMusicPlayer((ITextChannel) Context.Channel);
            if (mp is null)
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }

            mp.MoveTo(index);
        }
        
        // join vc
        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Join()
        {
            var user = (IGuildUser) Context.User;

            var voiceChannelId = user.VoiceChannel?.Id;

            if (voiceChannelId is null)
            {
                await ReplyErrorLocalizedAsync("must_be_in_voice");
                return;
            }

            await _service.JoinVoiceChannelAsync(user.GuildId, voiceChannelId.Value);
        }

        // leave vc (destroy)
        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Destroy()
        {
            var valid = await ValidateAsync();
            if (!valid)
                return;

            await _service.LeaveVoiceChannelAsync(Context.Guild.Id);
        }
        
        // play - no args = next
        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(2)]
        public Task Play()
            => Next();
        
        // play - index = skip to that index
        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public Task Play(int index)
            => MoveToIndex(index);

        // play - query = q(query)
        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        public async Task Play([Leftover] string query)
        {
            if (query is not null && query.StartsWith("https://open.spotify.com/track"))
            {
                await Spotify(query);
                return;
            }

            if (query is not null && query.StartsWith("https://open.spotify.com/playlist"))
            {
                await SpotifyPlaylist(query);
                return;
            }
            else await QueueByQuery(query);
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Lyrics([Remainder] string songname = null)
        {
            var lyrics = await _ksoft.musicAPI.SearchLyrics(songname, true, 30);
            await ctx.SendPaginatedConfirmAsync(0, cur =>
            {
                return new EmbedBuilder().WithOkColor()
                    .WithTitle(Format.Bold(
                        $"{lyrics.Data.Skip(cur).FirstOrDefault().Artist} - {lyrics.Data.Skip(cur).FirstOrDefault().Name}"))

                    .WithDescription(lyrics.Data.Skip(cur).FirstOrDefault().Lyrics);
            }, lyrics.Data.ToArray().Length, 1).ConfigureAwait(false);
        }
        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public Task Queue([Leftover] string query)
            => QueueByQuery(query);
        
        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public Task QueueNext([Leftover] string query)
            => QueueByQuery(query, asNext: true);

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Volume(int vol)
        {
            if (vol < 0 || vol > 100)
            {
                await ReplyErrorLocalizedAsync("volume_input_invalid");
                return;
            }
            
            var valid = await ValidateAsync();
            if (!valid)
                return;

            if (!_service.TryGetMusicPlayer(ctx.Guild.Id, out var mp))
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }

            mp.SetVolume(vol);
            await ReplyConfirmLocalizedAsync("volume_set", vol);
        }
        private async Task SpotifyPlaylist(string url = null)
        {
            var succ = await QueuePreconditionInternalAsync();
            if (!succ)
                return;
            var mp = _service.GetOrCreateMusicPlayer((ITextChannel)Context.Channel);
            if (mp is null)
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }
            var config = SpotifyClientConfig.CreateDefault();

            var request = new ClientCredentialsRequest("***REMOVED***", "***REMOVED***");
            var response = await new OAuthClient(config).RequestToken(request);

            var spotify = new SpotifyClient(config.WithToken(response.AccessToken));
            var e = new Uri(url);
            if (!e.Host.Contains("open.spotify.com"))
            {
                await ctx.Channel.SendErrorAsync("This is not a valid spotify link!");
                return;
            }

            var t = e.Segments;
            var playlist = await spotify.Playlists.Get(t[2]);
            var count = playlist.Tracks.Total;
            var msg = await ctx.Channel
                .SendMessageAsync($"<a:loading:834915210967253013> Queueing {count} Spotify Songs...")
                .ConfigureAwait(false);

            foreach (var item in playlist.Tracks.Items)
            {
                    if (item.Track is FullTrack track)
                    {

                        await mp.TryEnqueueTrackAsync($"{track.Name} {track.Artists.FirstOrDefault().Name} Official Audio",
            Context.User.ToString(),
            true,
            MusicPlatform.Spotify);
                    }

                    await msg.ModifyAsync(m =>
                            m.Content =
                                $"<a:check_animated:780103746432139274> Successfully queued {count} Songs!")
                        .ConfigureAwait(false);
                if (mp.IsStopped)
                {
                    var msg2 = await ReplyErrorLocalizedAsync("queue_stopped", Format.Code(Prefix + "play"));
                    msg2.DeleteAfter(10, _logService);
                }
            }
        }
        private async Task Spotify(string url = null)
        {
            var succ = await QueuePreconditionInternalAsync();
            if (!succ)
                return;
            var config = SpotifyClientConfig.CreateDefault();

            var request = new ClientCredentialsRequest("***REMOVED***", "***REMOVED***");
            var response = await new OAuthClient(config).RequestToken(request);

            var spotify = new SpotifyClient(config.WithToken(response.AccessToken));
            var e = new Uri(url);
            if (!e.Host.Contains("open.spotify.com"))
            {
                await ctx.Channel.SendErrorAsync("This is not a valid spotify link!");
                return;
            }

            var t = e.Segments;

            var track = await spotify.Tracks.Get(t[2]);
            try
            {
                await QueueByQuery($"{track.Name} {track.Artists.FirstOrDefault().Name} Official Audio", false, MusicPlatform.Spotify);
            }
            catch (Exception ex)
            {
                await ctx.Channel.SendMessageAsync(ex.ToString());
            }
        }
        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Next()
        {
            var valid = await ValidateAsync();
            if (!valid)
                return;

            var success = await _service.PlayAsync(Context.Guild.Id, ((IGuildUser)Context.User).VoiceChannel.Id);
            if (!success)
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }
        }

        private const int LQ_ITEMS_PER_PAGE = 9;
        
        // list queue, relevant page
        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ListQueue()
        {
            // show page with the current song
            if (!_service.TryGetMusicPlayer(ctx.Guild.Id, out var mp))
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }
            
            await ListQueue(mp.CurrentIndex / LQ_ITEMS_PER_PAGE + 1);
        }
        
        // list queue, specify page
        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ListQueue(int page)
        {
            if (--page < 0)
                return;

            IReadOnlyCollection<IQueuedTrackInfo> tracks;
            if (!_service.TryGetMusicPlayer(ctx.Guild.Id, out var mp) || (tracks = mp.GetQueuedTracks()).Count == 0)
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }
            
            EmbedBuilder printAction(int curPage)
            {
                string desc = string.Empty;
                var current = mp.GetCurrentTrack(out var currentIndex);
                if (!(current is null))
                {
                    desc = $"`🔊` {current.PrettyFullName()}\n\n" + desc;
                }

                var add = "";
                if (mp.IsStopped)
                    add += Format.Bold(GetText("queue_stopped", Format.Code(Prefix + "play"))) + "\n";
                 // var mps = mp.MaxPlaytimeSeconds;
                 // if (mps > 0)
                 //     add += Format.Bold(GetText("song_skips_after", TimeSpan.FromSeconds(mps).ToString("HH\\:mm\\:ss"))) + "\n";
                 if (mp.IsRepeatingCurrentSong)
                     add += "🔂 " + GetText("repeating_cur_song") + "\n";
                 else
                 {
                     // if (mp.Autoplay)
                     //     add += "↪ " + GetText("autoplaying") + "\n";
                     // if (mp.FairPlay && !mp.Autoplay)
                     //     add += " " + GetText("fairplay") + "\n";
                     if (mp.IsRepeatingQueue)
                         add += "🔁 " + GetText("repeating_playlist") + "\n";
                 }


                desc += tracks
                    .Skip(LQ_ITEMS_PER_PAGE * curPage)
                    .Take(LQ_ITEMS_PER_PAGE)
                    .Select((v, index) =>
                    {
                        index += LQ_ITEMS_PER_PAGE * curPage;
                        if (index == currentIndex)
                             return $"**⇒**`{index + 1}.` {v.PrettyFullName()}";
                         
                        return $"`{index + 1}.` {v.PrettyFullName()}";
                     })
                    .JoinWith('\n');
                 
                if (!string.IsNullOrWhiteSpace(add))
                    desc = add + "\n" + desc;

                var embed = new EmbedBuilder()
                    .WithAuthor(eab => eab
                        .WithName(GetText("player_queue", curPage + 1, (tracks.Count / LQ_ITEMS_PER_PAGE) + 1))
                        .WithMusicIcon())
                    .WithDescription(desc)
                    .WithFooter($"  {mp.PrettyVolume()}  |  🎶 {tracks.Count}  |  ⌛ {mp.PrettyTotalTime()}  ")
                    .WithOkColor();

                return embed;
             }

            await ctx.SendPaginatedConfirmAsync(
                page,
                printAction,
                tracks.Count,
                LQ_ITEMS_PER_PAGE,
                false);
        }

        // search
        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task QueueSearch([Leftover] string query)
        {
            _ = ctx.Channel.TriggerTypingAsync();
            
            var videos = (await _google.GetVideoInfosByKeywordAsync(query, 5).ConfigureAwait(false))
                .ToArray();

            if (!videos.Any())
            {
                await ReplyErrorLocalizedAsync("song_not_found").ConfigureAwait(false);
                return;
            }

            var resultsString = videos
                .Select((x, i) => $"`{i + 1}.`\n\t{Format.Bold(x.Name)}\n\t{x.Url}")
                .JoinWith('\n');
            
            var msg = await ctx.Channel.SendConfirmAsync(resultsString);

            try
            {
                var input = await GetUserInputAsync(ctx.User.Id, ctx.Channel.Id).ConfigureAwait(false);
                if (input == null
                    || !int.TryParse(input, out var index)
                    || (index -= 1) < 0
                    || index >= videos.Length)
                {
                    _logService.AddDeleteIgnore(msg.Id);
                    try
                    {
                        await msg.DeleteAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                    }

                    return;
                }
                query = videos[index].Url;

                await Play(query);
            }
            finally
            {
                _logService.AddDeleteIgnore(msg.Id);
                try
                {
                    await msg.DeleteAsync().ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }

        [MewdekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public async Task SongRemove(int index)
        {
            if (index < 1)
            {
                await ReplyErrorLocalizedAsync("removed_song_error").ConfigureAwait(false);
                return;
            }
            
            var valid = await ValidateAsync();
            if (!valid)
                return;

            if (!_service.TryGetMusicPlayer(ctx.Guild.Id, out var mp))
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }
            
            if (!mp.TryRemoveTrackAt(index - 1, out var song))
            {
                await ReplyErrorLocalizedAsync("removed_song_error").ConfigureAwait(false);
                return;
            }
            
            var embed = new EmbedBuilder()
                .WithAuthor(eab => eab.WithName(GetText("removed_song") + " #" + (index)).WithMusicIcon())
                .WithDescription(song.PrettyName())
                .WithFooter(ef => ef.WithText(song.PrettyInfo()))
                .WithErrorColor();

            await _service.SendToOutputAsync(Context.Guild.Id, embed);
        }

         public enum All { All = -1 }
         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         [Priority(0)]
         public async Task SongRemove(All _ = All.All)
         {
             var valid = await ValidateAsync();
             if (!valid)
                 return;

             if (!_service.TryGetMusicPlayer(ctx.Guild.Id, out var mp))
             {
                 await ReplyErrorLocalizedAsync("no_player");
                 return;
             }
             
             mp.Clear();
             await ReplyConfirmLocalizedAsync("queue_cleared").ConfigureAwait(false);
         }
         
         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         public async Task Defvol([Leftover] int val)
         {
             if (val < 0 || val > 100)
             {
                 await ReplyErrorLocalizedAsync("volume_input_invalid").ConfigureAwait(false);
                 return;
             }

             _service.SetDefaultVolume(Context.Guild.Id, val);

             await ReplyConfirmLocalizedAsync("defvol_set", val).ConfigureAwait(false);
         }
         
         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         public async Task Stop()
         {
             var valid = await ValidateAsync();
             if (!valid)
                 return;

             if (!_service.TryGetMusicPlayer(ctx.Guild.Id, out var mp))
             {
                 await ReplyErrorLocalizedAsync("no_player");
                 return;
             }
             
             mp.Stop();
         }
         
         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         public async Task QueueRepeat()
         {
             var valid = await ValidateAsync();
             if (!valid)
                 return;

             if (!_service.TryGetMusicPlayer(ctx.Guild.Id, out var mp))
             {
                 await ReplyErrorLocalizedAsync("no_player");
                 return;
             }
             
             if (mp.ToggleRpl())
                 await ReplyConfirmLocalizedAsync("rpl_enabled").ConfigureAwait(false);
             else
                 await ReplyConfirmLocalizedAsync("rpl_disabled").ConfigureAwait(false);
         }
         
         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         public async Task ReptCurSong()
         {
             var valid = await ValidateAsync();
             if (!valid)
                 return;

             IQueuedTrackInfo current;
             if (!_service.TryGetMusicPlayer(ctx.Guild.Id, out var mp) || (current = mp.GetCurrentTrack(out _)) is null)
             {
                 await ReplyErrorLocalizedAsync("no_player");
                 return;
             }

             if (mp.ToggleRcs())
             {
                 await ctx.Channel.EmbedAsync(new EmbedBuilder()
                     .WithOkColor()
                     .WithAuthor(eab => eab.WithMusicIcon().WithName("🔂 " + GetText("repeating_track")))
                     .WithDescription(current.PrettyName())
                     .WithFooter(ef => ef.WithText(current.PrettyInfo())));
             }
             else
             {
                 await ctx.Channel.SendConfirmAsync("🔂 " + GetText("repeating_track_stopped"));
             }
         }
         
         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         public async Task Pause()
         {
             var valid = await ValidateAsync();
             if (!valid)
                 return;

             if (!_service.TryGetMusicPlayer(ctx.Guild.Id, out var mp) || mp.GetCurrentTrack(out _) is null)
             {
                 await ReplyErrorLocalizedAsync("no_player");
                 return;
             }

             mp.TogglePause();
         }

         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         public async Task SongAutoDelete()
         {
             var valid = await ValidateAsync();
             if (!valid)
                 return;

             if (!_service.TryGetMusicPlayer(ctx.Guild.Id, out var mp))
             {
                 await ReplyErrorLocalizedAsync("no_player");
                 return;
             }
             
             if (mp.ToggleAd())
             {
                 await ReplyConfirmLocalizedAsync("sad_enabled").ConfigureAwait(false);
             }
             else
             {
                 await ReplyConfirmLocalizedAsync("sad_disabled").ConfigureAwait(false);
             }
         }
         
         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         public Task Radio(string radioLink)
             => QueueByQuery(radioLink, false, MusicPlatform.Radio);

         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         [OwnerOnly]
         public Task Local([Leftover] string path)
             => QueueByQuery(path, false, MusicPlatform.Local);

         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         [OwnerOnly]
         public async Task LocalPlaylist([Leftover] string dirPath)
         {
             if (string.IsNullOrWhiteSpace(dirPath))
                 return;

             var user = (IGuildUser) Context.User;
             var voiceChannelId = user.VoiceChannel?.Id;
        
             if (voiceChannelId is null)
             {
                 await ReplyErrorLocalizedAsync("must_be_in_voice");
                 return;
             }

             _ = ctx.Channel.TriggerTypingAsync();
        
             var botUser = await ctx.Guild.GetCurrentUserAsync();
             await EnsureBotInVoiceChannelAsync(voiceChannelId!.Value, botUser);
        
             if (botUser.VoiceChannel?.Id != voiceChannelId)
             {
                 await ReplyErrorLocalizedAsync("not_with_bot_in_voice");
                 return;
             }
            
             var mp = _service.GetOrCreateMusicPlayer((ITextChannel) Context.Channel);
             if (mp is null)
             {
                 await ReplyErrorLocalizedAsync("no_player");
                 return;
             }
             
             await _service.EnqueueDirectoryAsync(mp, dirPath, ctx.User.ToString());
             
             await ReplyConfirmLocalizedAsync("dir_queue_complete").ConfigureAwait(false);
         }
         
         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         public async Task MoveSong(int from, int to)
         {
             if (--from < 0 || --to < 0 || from == to)
             {
                 await ReplyErrorLocalizedAsync("invalid_input").ConfigureAwait(false);
                 return;
             }

             var valid = await ValidateAsync();
             if (!valid)
                 return;
             
             var mp = _service.GetOrCreateMusicPlayer((ITextChannel) Context.Channel);
             if (mp is null)
             {
                 await ReplyErrorLocalizedAsync("no_player");
                 return;
             }

             var track = mp.MoveTrack(from, to);
             if (track is null)
             {
                 await ReplyErrorLocalizedAsync("invalid_input").ConfigureAwait(false);
                 return;
             }
             
             var embed = new EmbedBuilder()
                 .WithTitle(track.Title.TrimTo(65))
                 .WithAuthor(eab => eab.WithName(GetText("song_moved")).WithIconUrl("https://cdn.discordapp.com/attachments/155726317222887425/258605269972549642/music1.png"))
                 .AddField(fb => fb.WithName(GetText("from_position")).WithValue($"#{from + 1}").WithIsInline(true))
                 .AddField(fb => fb.WithName(GetText("to_position")).WithValue($"#{to + 1}").WithIsInline(true))
                 .WithColor(Mewdeko.OkColor);

             if (Uri.IsWellFormedUriString(track.Url, UriKind.Absolute))
                 embed.WithUrl(track.Url);

             await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
         }

         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         public Task SoundCloudQueue([Leftover] string query)
             => QueueByQuery(query, false, MusicPlatform.SoundCloud);
         
         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         public async Task SoundCloudPl([Leftover] string playlist)
         {
             if (string.IsNullOrWhiteSpace(playlist))
                 return;

             var succ = await QueuePreconditionInternalAsync();
             if (!succ)
                 return;

             var mp = _service.GetOrCreateMusicPlayer((ITextChannel) Context.Channel);
             if (mp is null)
             {
                 await ReplyErrorLocalizedAsync("no_player");
                 return;
             }
             
             _ = ctx.Channel.TriggerTypingAsync();

             await _service.EnqueueSoundcloudPlaylistAsync(mp, playlist, ctx.User.ToString());

             await ctx.OkAsync();
         }

         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         public async Task Playlist([Leftover] string playlistQuery)
         {
             if (string.IsNullOrWhiteSpace(playlistQuery))
                 return;

             var succ = await QueuePreconditionInternalAsync();
             if (!succ)
                 return;

             var mp = _service.GetOrCreateMusicPlayer((ITextChannel) Context.Channel);
             if (mp is null)
             {
                 await ReplyErrorLocalizedAsync("no_player");
                 return;
             }

             _ = Context.Channel.TriggerTypingAsync();


             var queuedCount = await _service.EnqueueYoutubePlaylistAsync(mp, playlistQuery, ctx.User.ToString());
             if (queuedCount == 0)
             {
                 await ReplyErrorLocalizedAsync("no_search_results").ConfigureAwait(false);
                 return;
             }
             await ctx.OkAsync();
         }

         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         public async Task NowPlaying()
         {
             var mp = _service.GetOrCreateMusicPlayer((ITextChannel) Context.Channel);
             if (mp is null)
             {
                 await ReplyErrorLocalizedAsync("no_player");
                 return;
             }

             var currentTrack = mp.GetCurrentTrack(out _);
             if (currentTrack == null)
                 return;

             var embed = new EmbedBuilder().WithOkColor()
                 .WithAuthor(eab => eab.WithName(GetText("now_playing")).WithMusicIcon())
                 .WithDescription(currentTrack.PrettyName())
                 .WithThumbnailUrl(currentTrack.Thumbnail)
                 .WithFooter($"{mp.PrettyVolume()} | {mp.PrettyTotalTime()} | {currentTrack.Platform} | {currentTrack.Queuer}");

             await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
         }

         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         public async Task PlaylistShuffle()
         {
             var valid = await ValidateAsync();
             if (!valid)
                 return;
             
             var mp = _service.GetOrCreateMusicPlayer((ITextChannel) Context.Channel);
             if (mp is null)
             {
                 await ReplyErrorLocalizedAsync("no_player");
                 return;
             }
             
             mp.ShuffleQueue();
             await ReplyConfirmLocalizedAsync("queue_shuffled");
         }

         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         [UserPerm(GuildPerm.ManageMessages)]
         public async Task SetMusicChannel()
         {
             _service.SetMusicChannel(ctx.Guild.Id, ctx.Channel.Id);

             await ReplyConfirmLocalizedAsync("set_music_channel");
         }
         
         [MewdekoCommand, Usage, Description, Aliases]
         [RequireContext(ContextType.Guild)]
         [UserPerm(GuildPerm.ManageMessages)]
         public async Task UnsetMusicChannel()
         {
             _service.UnsetMusicChannel(ctx.Guild.Id);

             await ReplyConfirmLocalizedAsync("unset_music_channel");
         }
    }
}