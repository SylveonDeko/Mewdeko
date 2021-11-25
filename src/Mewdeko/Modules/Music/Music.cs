using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using KSoftNet;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Extensions.Interactive;
using Mewdeko.Common.Extensions.Interactive.Entities.Page;
using Mewdeko.Common.Extensions.Interactive.Pagination;
using Mewdeko.Common.Extensions.Interactive.Pagination.Lazy;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Music.Common.SongResolver.Impl;
using Mewdeko.Modules.Music.Services;
using Mewdeko.Services.Database.Models;
using SpotifyAPI.Web;

namespace Mewdeko.Modules.Music
{
    public sealed partial class Music : MewdekoModuleBase<IMusicService>
    {
        public enum All
        {
            All = -1
        }

        public enum InputRepeatType
        {
            N = 0,
            No = 0,
            None = 0,
            T = 1,
            Track = 1,
            S = 1,
            Song = 1,
            Q = 2,
            Queue = 2,
            Playlist = 2,
            Pl = 2
        }

        private const int LqItemsPerPage = 9;
        private static readonly SemaphoreSlim VoiceChannelLock = new(1, 1);
        private readonly LogCommandService _logService;
        public KSoftApi Ksoft;
        private readonly InteractiveService Interactivity;

        public Music(LogCommandService _logService, KSoftApi ksoft, InteractiveService serv)
        {
            Interactivity = serv;
            Ksoft = ksoft;
            this._logService = _logService;
        }

        private async Task<bool> ValidateAsync()
        {
            var user = (IGuildUser)ctx.User;
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

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task MusicQuality()
        {
            var quality = await Service.GetMusicQualityAsync(ctx.Guild.Id);
            await ReplyConfirmLocalizedAsync("current_music_quality", Format.Bold(quality.ToString()));
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task MusicQuality(QualityPreset preset)
        {
            await Service.SetMusicQualityAsync(ctx.Guild.Id, preset);
            await ReplyConfirmLocalizedAsync("music_quality_set", Format.Bold(preset.ToString()));
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        public async Task Play([Remainder] string query)
        {
            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    await ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return;
                }

            if (query is not null && query.StartsWith("https://open.spotify.com/track"))
            {
                await Spotify(query);
                return;
            }

            if (query is not null && query.StartsWith("https://open.spotify.com/album"))
            {
                await SpotifyAlbum(query);
                return;
            }

            if (query is not null && query.StartsWith("https://open.spotify.com/playlist"))
                await SpotifyPlaylist(query);
            else
                await QueueByQuery(query);
        }

        private async Task SpotifyPlaylist(string url = null)
        {
            var succ = await QueuePreconditionInternalAsync();
            if (!succ) return;
            var mp = await Service.GetOrCreateMusicPlayerAsync((ITextChannel)Context.Channel);
            if (mp is null)
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }

            var config = SpotifyClientConfig.CreateDefault();

            var request =
                new ClientCredentialsRequest("dc237c779f55479fae3d5418c4bb392e", "db01b63b808040efbdd02098e0840d90");
            var response = await new OAuthClient(config).RequestToken(request);

            var spotify = new SpotifyClient(config.WithToken(response.AccessToken));
            var e = new Uri(url);
            var t = e.Segments;
            var playlist = await spotify.Playlists.Get(t[2]);
            int songs;
            if (playlist.Tracks.Items.Count > 100)
                songs = 100;
            else
                songs = playlist.Tracks.Items.Count;
            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    IconUrl = "http://assets.stickpng.com/images/5ece5029123d6d0004ce5f8b.png",
                    Name = playlist.Owner.DisplayName
                },
                Description = $"<a:loading:847706744741691402> Queueing {songs} songs...",
                Footer = new EmbedFooterBuilder
                {
                    Text = "Spotify Playlist"
                },
                ThumbnailUrl = playlist.Images.FirstOrDefault()?.Url,
                Color = Mewdeko.Services.Mewdeko.OkColor
            };
            var msg = await ctx.Channel.SendMessageAsync(embed: embed.Build());
            foreach (var item in playlist.Tracks.Items.Take(100))
                if (item.Track is FullTrack track)
                    await mp.TryEnqueueTrackAsync($"{track.Name} {track.Artists.FirstOrDefault()?.Name} Official Audio",
                        ctx.User.ToString(), true, MusicPlatform.Spotify);
            var em = new EmbedBuilder
            {
                Author = embed.Author,
                Description =
                    $"<a:checkfragutil:854536148411744276> Queued {songs} songs from this album!",
                Footer = embed.Footer,
                ImageUrl = embed.ImageUrl,
                Color = Mewdeko.Services.Mewdeko.OkColor
            };
            await msg.ModifyAsync(x => x.Embed = em.Build());
        }

        private async Task SpotifyAlbum(string url = null)
        {
            var succ = await QueuePreconditionInternalAsync();
            if (!succ) return;
            var mp = await Service.GetOrCreateMusicPlayerAsync((ITextChannel)Context.Channel);
            if (mp is null)
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }

            var config = SpotifyClientConfig.CreateDefault();

            var request =
                new ClientCredentialsRequest("dc237c779f55479fae3d5418c4bb392e", "db01b63b808040efbdd02098e0840d90");
            var response = await new OAuthClient(config).RequestToken(request);

            var spotify = new SpotifyClient(config.WithToken(response.AccessToken));
            var e = new Uri(url);
            var t = e.Segments;
            var playlist = await spotify.Albums.Get(t[2]);
            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    IconUrl = "http://assets.stickpng.com/images/5ece5029123d6d0004ce5f8b.png",
                    Name = playlist.Artists.FirstOrDefault().Name
                },
                Description =
                    $"<a:loading:847706744741691402> Queuing {playlist.TotalTracks} songs...",
                Footer = new EmbedFooterBuilder
                {
                    Text = "Spotify Album"
                },
                ThumbnailUrl = playlist.Images.FirstOrDefault().Url,
                Color = Mewdeko.Services.Mewdeko.OkColor
            };
            var msg = await ctx.Channel.SendMessageAsync(embed: embed.Build());
            foreach (var item in playlist.Tracks.Items.Take(100))
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (mp is null)
                    // ReSharper disable once HeuristicUnreachableCode
                    return;
                if (item is SimpleTrack track)
                    await mp.TryEnqueueTrackAsync($"{track.Name} {track.Artists.FirstOrDefault().Name} Official Audio",
                        ctx.User.ToString(), false, MusicPlatform.Spotify);

            }
            var em = new EmbedBuilder
            {
                Author = embed.Author,
                Description =
                    $"<a:checkfragutil:854536148411744276> Successfully queued {playlist.TotalTracks} songs.",
                Footer = embed.Footer,
                ImageUrl = embed.ImageUrl,
                Color = Mewdeko.Services.Mewdeko.OkColor
            };
            await msg.ModifyAsync(x => x.Embed = em.Build());
        }

        private async Task Spotify(string url = null)
        {
            var succ = await QueuePreconditionInternalAsync();
            if (!succ)
                return;
            var config = SpotifyClientConfig.CreateDefault();
            var request =
                new ClientCredentialsRequest("dc237c779f55479fae3d5418c4bb392e",
                    "db01b63b808040efbdd02098e0840d90");
            var response = await new OAuthClient(config).RequestToken(request);
            var spotify = new SpotifyClient(config.WithToken(response.AccessToken));
            var e = new Uri(url);
            var t = e.Segments;
            var track = await spotify.Tracks.Get(t[2]);
            await QueueByQuery($"{track.Name} {track.Artists.FirstOrDefault().Name} Official Audio",
                false,
                MusicPlatform.Spotify);
        }

        private async Task EnsureBotInVoiceChannelAsync(ulong voiceChannelId, IGuildUser botUser = null)
        {
            botUser ??= await ctx.Guild.GetCurrentUserAsync();
            await VoiceChannelLock.WaitAsync();
            try
            {
                if (botUser.VoiceChannel?.Id is null || !Service.TryGetMusicPlayer(Context.Guild.Id, out _))
                    await Service.JoinVoiceChannelAsync(ctx.Guild.Id, voiceChannelId);
                if (botUser.VoiceChannel is IStageChannel channel)
                    try
                    {
                        await channel.BecomeSpeakerAsync();
                    }
                    catch
                    {
                        await ctx.Channel.SendErrorAsync("Seems i don't have permission to join as a speaker.");
                    }
            }
            finally
            {
                VoiceChannelLock.Release();
            }
        }

        private async Task<bool> QueuePreconditionInternalAsync()
        {
            var user = (IGuildUser)Context.User;
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

            var mp = await Service.GetOrCreateMusicPlayerAsync((ITextChannel)Context.Channel);
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

                var queuedMessage = await Service.SendToOutputAsync(Context.Guild.Id, embed).ConfigureAwait(false);
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

            var mp = await Service.GetOrCreateMusicPlayerAsync((ITextChannel)Context.Channel);
            if (mp is null)
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }

            mp.MoveTo(index);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task AutoPlay()
        {

            var e = await Service.ToggleAutoPlay(ctx.Guild.Id);
            if (e)
                await ctx.Channel.SendConfirmAsync("Enabled AutoPlay");
            else
                await ctx.Channel.SendConfirmAsync("Disabled AutoPlay");
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Join()
        {

            var user = (IGuildUser)Context.User;
            var channel = user?.VoiceChannel;
            var voiceChannelId = user.VoiceChannel.Id;
            if (voiceChannelId is 0)
            {
                await ReplyErrorLocalizedAsync("must_be_in_voice");
                return;
            }

            if (channel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    await ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return;
                }

            await Service.JoinVoiceChannelAsync(user.GuildId, voiceChannelId);
            if (channel is SocketStageChannel chan1)
                try
                {
                    await chan1.BecomeSpeakerAsync();
                }
                catch
                {
                    await ctx.Channel.SendErrorAsync(
                        "Ive joined the stage channel but was unable to make myself a speaker! Please fix permissions or add me manually.");
                }
        }

        // leave vc (destroy)
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Destroy()
        {
            var user = ctx.User as IGuildUser;
            var valid = await ValidateAsync();
            if (!valid)
                return;
            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    await ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return;
                }

            await Service.LeaveVoiceChannelAsync(Context.Guild.Id);
            await ctx.Channel.SendConfirmAsync("Succesfully stopped the player and cleared the queue!");
        }

        // play - no args = next
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(2)]
        public Task Play()
        {
            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return Task.CompletedTask;
                }

            return Next();
        }

        // play - index = skip to that index
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public Task Play(int index)
        {
            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return Task.CompletedTask;
                }

            return MoveToIndex(index);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public Task Queue([Remainder] string query)
        {
            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return Task.CompletedTask;
                }

            return QueueByQuery(query);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public Task QueueNext([Remainder] string query)
        {
            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return Task.CompletedTask;
                }

            return QueueByQuery(query, true);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
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
            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    await ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return;
                }

            await Service.SetVolumeAsync(ctx.Guild.Id, vol);
            await ReplyConfirmLocalizedAsync("volume_set", vol);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Next()
        {
            

            var valid = await ValidateAsync();
            if (!valid)
                return;
            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    await ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return;
                }

            var success = await Service.PlayAsync(Context.Guild.Id, ((IGuildUser)Context.User).VoiceChannel.Id);
            if (!success) await ReplyErrorLocalizedAsync("no_player");
        }

        // list queue, relevant page
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ListQueue()
        {
            // show page with the current song
            if (!Service.TryGetMusicPlayer(ctx.Guild.Id, out var mp))
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }

            await ListQueue(mp.CurrentIndex / LqItemsPerPage + 1);
        }

        // list queue, specify page
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ListQueue(int page)
        {
            if (--page < 0)
                return;

            IReadOnlyCollection<IQueuedTrackInfo> tracks;
            if (!Service.TryGetMusicPlayer(ctx.Guild.Id, out var mp) || (tracks = mp.GetQueuedTracks()).Count == 0)
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(tracks.Count / 9)
                .WithDefaultEmotes()
                .Build();

            await Interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

            Task<PageBuilder> PageFactory(int page)
            {
                {
                    var desc = string.Empty;
                    var current = mp.GetCurrentTrack(out var currentIndex);
                    if (current is not null) desc = $"`🔊` {current.PrettyFullName()}\n\n" + desc;

                    var repeatType = mp.Repeat;
                    var add = "";
                    if (mp.IsStopped)
                        add += Format.Bold(GetText("queue_stopped", Format.Code(Prefix + "play"))) + "\n";
                    if (repeatType == PlayerRepeatType.Track)
                    {
                        add += "🔂 " + GetText("repeating_track") + "\n";
                    }
                    else
                    {
                        if (repeatType == PlayerRepeatType.Queue)
                            add += "🔁 " + GetText("repeating_queue") + "\n";
                    }


                    desc += tracks
                        .Skip(LqItemsPerPage * page)
                        .Take(LqItemsPerPage)
                        .Select((v, index) =>
                        {
                            index += LqItemsPerPage * page;
                            if (index == currentIndex)
                                return $"**⇒**`{index + 1}.` {v.PrettyFullName()}";

                            return $"`{index + 1}.` {v.PrettyFullName()}";
                        })
                        .JoinWith('\n');

                    if (!string.IsNullOrWhiteSpace(add))
                        desc = add + "\n" + desc;

                    return Task.FromResult(new PageBuilder()
                        .WithAuthor(eab => eab
                            .WithName(GetText("player_queue", page + 1, tracks.Count / LqItemsPerPage + 1))
                            .WithMusicIcon())
                        .WithDescription(desc)
                        .WithFooter($"  {mp.PrettyVolume()}  |  🎶 {tracks.Count}  |  ⌛ {mp.PrettyTotalTime()}  ")
                        .WithOkColor());
                }
            }
        }

        // search
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task QueueSearch([Remainder] string query)
        {
            
            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is not null && user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    await ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return;
                }

            _ = ctx.Channel.TriggerTypingAsync();

            var videos = await Service.SearchVideosAsync(query);

            if (videos is null || videos.Count == 0)
            {
                await ReplyErrorLocalizedAsync("song_not_found").ConfigureAwait(false);
                return;
            }

            var resultsString = videos
                .Select((x, i) => $"`{i + 1}.`\n\t{Format.Bold(x.Title)}\n\t{x.Url}")
                .JoinWith('\n');
            var buttons = new ComponentBuilder();
            var count = 0;
            foreach (var (_, _) in videos)
            {
                var button = new ButtonBuilder(customId: (count + 1).ToString(), label: (count + 1).ToString());
                count++;
                buttons.WithButton(button);
            }
            var eb = new EmbedBuilder().WithOkColor().WithDescription(resultsString);
            var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build(), component: buttons.Build());

            try
            {
                var input = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id).ConfigureAwait(false);
                if (input == null
                    || !int.TryParse(input, out var index)
                    || (index -= 1) < 0
                    || index >= videos.Count)
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

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(1)]
        public async Task TrackRemove(int index)
        {
            if (index < 1)
            {
                await ReplyErrorLocalizedAsync("removed_song_error").ConfigureAwait(false);
                return;
            }

            var valid = await ValidateAsync();
            if (!valid)
                return;
            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    await ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return;
                }

            if (!Service.TryGetMusicPlayer(ctx.Guild.Id, out var mp))
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
                .WithAuthor(eab => eab.WithName(GetText("removed_song") + " #" + index).WithMusicIcon())
                .WithDescription(song.PrettyName())
                .WithFooter(ef => ef.WithText(song.PrettyInfo()))
                .WithErrorColor();

            await Service.SendToOutputAsync(Context.Guild.Id, embed);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Priority(0)]
        public async Task TrackRemove(All _ = All.All)
        {
            var valid = await ValidateAsync();
            if (!valid)
                return;

            if (!Service.TryGetMusicPlayer(ctx.Guild.Id, out var mp))
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }

            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    await ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return;
                }

            mp.Clear();
            await ReplyConfirmLocalizedAsync("queue_cleared").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Defvol(int val)
        {
            await ReplyErrorLocalizedAsync("obsolete", $"`{Prefix}vol`");
            await Volume(val);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Stop()
        {
            var valid = await ValidateAsync();
            if (!valid)
                return;

            if (!Service.TryGetMusicPlayer(ctx.Guild.Id, out var mp))
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }

            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    await ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return;
                }

            mp.Stop();
        }

        private static PlayerRepeatType InputToDbType(InputRepeatType type)
        {
            return type switch
            {
                InputRepeatType.None => PlayerRepeatType.None,
                InputRepeatType.Queue => PlayerRepeatType.Queue,
                InputRepeatType.Track => PlayerRepeatType.Track,
                _ => PlayerRepeatType.Queue
            };
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task QueueRepeat(InputRepeatType type = InputRepeatType.Queue)
        {
            var valid = await ValidateAsync();
            if (!valid)
                return;
            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    await ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return;
                }

            await Service.SetRepeatAsync(ctx.Guild.Id, InputToDbType(type));

            if (type == InputRepeatType.None)
                await ReplyConfirmLocalizedAsync("repeating_none");
            else if (type == InputRepeatType.Queue)
                await ReplyConfirmLocalizedAsync("repeating_queue");
            else
                await ReplyConfirmLocalizedAsync("repeating_track");
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ReptCurSong()
        {
            await ReplyErrorLocalizedAsync("obsolete_use", $"`{Prefix}qrp song`");
            await QueueRepeat(InputRepeatType.Song);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Pause()
        {
            var valid = await ValidateAsync();
            if (!valid)
                return;

            if (!Service.TryGetMusicPlayer(ctx.Guild.Id, out var mp) || mp.GetCurrentTrack(out _) is null)
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }

            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    await ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return;
                }

            mp.TogglePause();
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public Task Radio(string radioLink)
        {
           

            return QueueByQuery(radioLink, false, MusicPlatform.Radio);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        public Task Local([Remainder] string path)
        {
            return QueueByQuery(path, false, MusicPlatform.Local);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        public async Task LocalPlaylist([Remainder] string dirPath)
        {
            if (string.IsNullOrWhiteSpace(dirPath))
                return;

            var user = (IGuildUser)Context.User;
            var voiceChannelId = user.VoiceChannel?.Id;

            if (voiceChannelId is null)
            {
                await ReplyErrorLocalizedAsync("must_be_in_voice");
                return;
            }

            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    await ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
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

            var mp = await Service.GetOrCreateMusicPlayerAsync((ITextChannel)Context.Channel);
            if (mp is null)
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }

            await Service.EnqueueDirectoryAsync(mp, dirPath, ctx.User.ToString());

            await ReplyConfirmLocalizedAsync("dir_queue_complete").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
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

            var mp = await Service.GetOrCreateMusicPlayerAsync((ITextChannel)Context.Channel);
            if (mp is null)
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }

            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    await ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
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
                .WithAuthor(eab =>
                    eab.WithName(GetText("song_moved")).WithIconUrl(
                        "https://cdn.discordapp.com/attachments/155726317222887425/258605269972549642/music1.png"))
                .AddField(fb => fb.WithName(GetText("from_position")).WithValue($"#{from + 1}").WithIsInline(true))
                .AddField(fb => fb.WithName(GetText("to_position")).WithValue($"#{to + 1}").WithIsInline(true))
                .WithColor(Mewdeko.Services.Mewdeko.OkColor);

            if (Uri.IsWellFormedUriString(track.Url, UriKind.Absolute))
                embed.WithUrl(track.Url);

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public Task SoundCloudQueue([Remainder] string query)
        {

            return QueueByQuery(query, false, MusicPlatform.SoundCloud);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task SoundCloudPl([Remainder] string playlist)
        {
            if (string.IsNullOrWhiteSpace(playlist))
                return;

            var succ = await QueuePreconditionInternalAsync();
            if (!succ)
                return;

            var mp = await Service.GetOrCreateMusicPlayerAsync((ITextChannel)Context.Channel);
            if (mp is null)
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }

            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    await ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return;
                }

            _ = ctx.Channel.TriggerTypingAsync();

            await Service.EnqueueSoundcloudPlaylistAsync(mp, playlist, ctx.User.ToString());

            await ctx.OkAsync();
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Playlist([Remainder] string playlistQuery)
        {
                if (string.IsNullOrWhiteSpace(playlistQuery))
                    return;

                var succ = await QueuePreconditionInternalAsync();
                if (!succ)
                    return;

                var mp = await Service.GetOrCreateMusicPlayerAsync((ITextChannel)Context.Channel);
                if (mp is null)
                {
                    await ReplyErrorLocalizedAsync("no_player");
                    return;
                }

                var user = ctx.User as IGuildUser;
                if (user.VoiceChannel is SocketStageChannel chan)
                    if (!chan.Speakers.Contains(user))
                    {
                        await ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                        return;
                    }

                _ = Context.Channel.TriggerTypingAsync();


                var queuedCount = await Service.EnqueueYoutubePlaylistAsync(mp, playlistQuery, ctx.User.ToString());
                if (queuedCount == 0)
                {
                    await ReplyErrorLocalizedAsync("no_search_results").ConfigureAwait(false);
                    return;
                }

                await ctx.OkAsync();
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task NowPlaying()
        {
            var mp = await Service.GetOrCreateMusicPlayerAsync((ITextChannel)Context.Channel);
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
                .WithFooter(
                    $"{mp.PrettyVolume()} | {mp.PrettyTotalTime()} | {currentTrack.Platform} | {currentTrack.Queuer}");

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task PlaylistShuffle()
        {
            var valid = await ValidateAsync();
            if (!valid)
                return;

            var mp = await Service.GetOrCreateMusicPlayerAsync((ITextChannel)Context.Channel);
            if (mp is null)
            {
                await ReplyErrorLocalizedAsync("no_player");
                return;
            }

            var user = ctx.User as IGuildUser;
            if (user.VoiceChannel is SocketStageChannel chan)
                if (!chan.Speakers.Contains(user))
                {
                    await ctx.Channel.SendErrorAsync("You must be a speaker to do this!");
                    return;
                }

            mp.ShuffleQueue();
            await ReplyConfirmLocalizedAsync("queue_shuffled");
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task SetMusicChannel()
        {
            await Service.SetMusicChannelAsync(ctx.Guild.Id, ctx.Channel.Id);

            await ReplyConfirmLocalizedAsync("set_music_channel");
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task UnsetMusicChannel()
        {
            await Service.SetMusicChannelAsync(ctx.Guild.Id, null);

            await ReplyConfirmLocalizedAsync("unset_music_channel");
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task AutoDisconnect()
        {
            var newState = await Service.ToggleAutoDisconnectAsync(ctx.Guild.Id);

            if (newState)
                await ReplyConfirmLocalizedAsync("autodc_enable");
            else
                await ReplyConfirmLocalizedAsync("autodc_disable");
        }
    }
}
