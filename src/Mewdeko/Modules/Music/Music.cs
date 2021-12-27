#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Html.Dom;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko._Extensions;
using Mewdeko.Modules.Music.Extensions;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Music.Services;
using Mewdeko.Services.Database.Models;
using Victoria;
using Victoria.Enums;
using Victoria.Responses.Search;
using Mewdeko.Common.Extensions.Interactive;
using Mewdeko.Common.Extensions.Interactive.Entities.Page;
using Mewdeko.Common.Extensions.Interactive.Pagination;
using Mewdeko.Common.Extensions.Interactive.Pagination.Lazy;

namespace Mewdeko.Modules.Music
{
    public class Music : MewdekoModuleBase<MusicService>
    {
        private readonly LavaNode _lavaNode;
        private readonly InteractiveService _interactivity;

        public Music(LavaNode lava, InteractiveService interactive)
        {
            _interactivity = interactive;
            _lavaNode = lava;
        }

        [MewdekoCommand]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task AutoDisconnect(AutoDisconnect disconnect)
        {
            await Service.ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => { settings.AutoDisconnect = disconnect; }, disconnect);
            await ctx.Channel.SendConfirmAsync(
                $"Successfully set AutoDisconnect to {Format.Code(disconnect.ToString())}");
        }
        [MewdekoCommand]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Join() {
            if (_lavaNode.HasPlayer(Context.Guild)) {
                await ctx.Channel.SendErrorAsync("I'm already connected to a voice channel!");
                return;
            }

            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null) {
                await ctx.Channel.SendErrorAsync("You must be connected to a voice channel!");
                return;
            }
            await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
            await ctx.Channel.SendConfirmAsync($"Joined {voiceState.VoiceChannel.Name}!");
        }

        [MewdekoCommand]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Leave() 
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
                await ctx.Channel.SendErrorAsync("I'm not connected to any voice channels!");
                return;
            }

            var voiceChannel = (Context.User as IVoiceState)?.VoiceChannel ?? player.VoiceChannel;
            if (voiceChannel == null) {
                await ctx.Channel.SendErrorAsync("Not sure which voice channel to disconnect from.");
                return;
            }
            
            await _lavaNode.LeaveAsync(voiceChannel);
            await ctx.Channel.SendConfirmAsync($"I've left {voiceChannel.Name}!");
            await Service.QueueClear(ctx.Guild.Id);
            }
        

        [MewdekoCommand]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Play(int number)
        {
            var queue = Service.GetQueue(ctx.Guild.Id);
            if (!_lavaNode.TryGetPlayer(ctx.Guild, out var player))
            {
                var vc = ctx.User as IVoiceState;
                if (vc.VoiceChannel is null)
                {
                    await ctx.Channel.SendErrorAsync("Looks like both you and the bot are not in a voice channel.");
                    return;
                }
            }

            if (queue.Any())
            {
                var track = queue.FirstOrDefault(x => x.Index == number);
                if (track is null)
                {
                    await Play($"{number}");
                    return;
                }

                await player.PlayAsync(track);
                var e = await track.FetchArtworkAsync();
                var eb = new EmbedBuilder()
                    .WithDescription($"Playing {track.Title}")
                    .WithFooter($"Track {track.Index} | {track.Duration:hh\\:mm\\:ss} | {track.QueueUser}")
                    .WithThumbnailUrl(e)
                    .WithOkColor();
                await ctx.Channel.SendMessageAsync(embed: eb.Build());
            }
        }
        [MewdekoCommand]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        // ReSharper disable once MemberCanBePrivate.Global
        public async Task Play([Remainder] string searchQuery)
        {
            var count = 0;
            if (string.IsNullOrWhiteSpace(searchQuery)) {
                await ReplyAsync("Please provide search terms.");
                return;
            }
            
            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                var vc = ctx.User as IVoiceState;
                if (vc.VoiceChannel is null)
                {
                    await ctx.Channel.SendErrorAsync("Looks like both you and the bot are not in a voice channel.");
                    return;
                }

                try
                {
                    await _lavaNode.JoinAsync(vc.VoiceChannel);
                    if (vc.VoiceChannel is SocketStageChannel chan)
                    {
                        try
                        {
                            await chan.BecomeSpeakerAsync();
                        }
                        catch
                        {
                            await ctx.Channel.SendErrorAsync(
                                "I tried to join as a speaker but I'm unable to! Please drag me to the channel manually.");
                        }
                    }
                }
                catch
                {
                    await ctx.Channel.SendErrorAsync("Seems I'm unable to join the channel! Check permissions!");
                    return;
                }
            }
            
            await Service.ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => { settings.MusicChannelId = ctx.Channel.Id; }, ctx.Channel.Id);
            var player = _lavaNode.GetPlayer(ctx.Guild);
            SearchResponse searchResponse;
            if (Uri.IsWellFormedUriString(searchQuery, UriKind.RelativeOrAbsolute))
            {
                if (searchQuery.Contains("youtube.com") || searchQuery.Contains("youtu.be") || searchQuery.Contains("soundcloud.com"))
                {
                    searchResponse = await _lavaNode.SearchAsync(SearchType.Direct, searchQuery);
                    var track1 = searchResponse.Tracks.FirstOrDefault();
                    var platform = AdvancedLavaTrack.Platform.Youtube;
                    if (searchQuery.Contains("soundcloud.com"))
                        platform = AdvancedLavaTrack.Platform.Soundcloud;
                    await Service.Enqueue(ctx.Guild.Id, ctx.User, searchResponse.Tracks.ToArray(), platform);
                    count = Service.GetQueue(ctx.Guild.Id).Count;
                    if (searchResponse.Playlist.Name is not null)
                    {
                        var eb = new EmbedBuilder()
                            .WithOkColor()
                            .WithDescription(
                                $"Queued {searchResponse.Tracks.Count()} tracks from {searchResponse.Playlist.Name}")
                            .WithFooter($"{count} songs now in the queue");
                        await ctx.Channel.SendMessageAsync(embed: eb.Build());
                        if (player.PlayerState != PlayerState.Playing)
                            await player.PlayAsync(track1);
                        await player.UpdateVolumeAsync(Convert.ToUInt16(Service.GetVolume(ctx.Guild.Id)));
                        return;
                    }
                    else
                    {
                        var eb = new EmbedBuilder()
                            .WithOkColor()
                            .WithDescription(
                                $"Queued {searchResponse.Tracks.Count()} tracks from {searchResponse.Playlist.Name} and bound the queue info to {ctx.Channel.Name}!");
                        await ctx.Channel.SendMessageAsync(embed: eb.Build());
                        if (player.PlayerState != PlayerState.Playing)
                            await player.PlayAsync(x => { x.Track = track1; });
                        await player.UpdateVolumeAsync(Convert.ToUInt16(Service.GetVolume(ctx.Guild.Id)));
                        return;
                    }
                }
            }

            if (searchQuery.Contains("spotify"))
            {
                await Service.SpotifyQueue(ctx.Guild, ctx.User, ctx.Channel as ITextChannel, player, searchQuery);
                return;
            }
            searchResponse = await _lavaNode.SearchAsync(SearchType.YouTube, searchQuery);
            if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
            {
                await ctx.Channel.SendErrorAsync("Seems like I can't find that video, please try again.");
                return;
            }

            var components = new ComponentBuilder().WithButton("Play All", "all").WithButton("Select", "select").WithButton("Play First", "pf").WithButton("Cancel", "cancel", ButtonStyle.Danger);
            var eb12 = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Would you like me to:")
                .WithDescription("Play all that I found\n" +
                                 "Let you select from the top 5\n" +
                                 "Just play the first thing I found");
            var msg = await ctx.Channel.SendMessageAsync(embed: eb12.Build(), components: components.Build());
            var button = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id);
            switch (button)
            {
                case "all":
                    await Service.Enqueue(ctx.Guild.Id, ctx.User, searchResponse.Tracks.ToArray());
                    count = Service.GetQueue(ctx.Guild.Id).Count;
                    var track = searchResponse.Tracks.FirstOrDefault();
                    var eb1 = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription($"Added {track.Title} along with {searchResponse.Tracks.Count} other tracks.")
                        .WithThumbnailUrl(await track.FetchArtworkAsync())
                        .WithFooter($"{count} songs in queue");
                    if (player.PlayerState != PlayerState.Playing)
                    {
                        await player.PlayAsync(x => { x.Track = track; });
                        await player.UpdateVolumeAsync(Convert.ToUInt16(Service.GetVolume(ctx.Guild.Id)));
                    }

                    await msg.ModifyAsync(x =>
                    {
                        x.Components = null;
                        x.Embed = eb1.Build();
                    });
                    break;
                case "select":
                    var tracks = searchResponse.Tracks.Take(5).ToArray();
                    int count1 = 1;
                    var eb = new EmbedBuilder()
                        .WithDescription(string.Join("\n", tracks.Select(x => $"{count1++}. {x.Title} by {x.Author}")))
                        .WithOkColor()
                        .WithTitle("Pick which one!");
                    count1 = 0;
                    var components1 = new ComponentBuilder();
                    foreach (var i in tracks)
                    {
                        var component = new ButtonBuilder(customId: (count1 + 1).ToString(), label: (count1 + 1).ToString());
                        count1++;
                        components1.WithButton(component);
                    }
                    await msg.ModifyAsync(x =>
                        {
                            x.Components = components1.Build();
                            x.Embed = eb.Build();
                        });
                    var input = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id);
                    var chosen = tracks[int.Parse(input) - 1];
                    await Service.Enqueue(ctx.Guild.Id, ctx.User, chosen);
                    count = Service.GetQueue(ctx.Guild.Id).Count;
                    eb1 = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription($"Added {chosen.Title} by {chosen.Author} to the queue.")
                        .WithThumbnailUrl(await chosen.FetchArtworkAsync())
                        .WithFooter($"{count} songs in queue");
                    if (player.PlayerState != PlayerState.Playing)
                    {
                        await player.PlayAsync(x => { x.Track = chosen; });
                        await player.UpdateVolumeAsync(Convert.ToUInt16(Service.GetVolume(ctx.Guild.Id)));
                    }

                    await msg.ModifyAsync(x =>
                    {
                        x.Components = null;
                        x.Embed = eb1.Build();
                    });
                    break;
                case "pf":
                    track = searchResponse.Tracks.FirstOrDefault();
                    await Service.Enqueue(ctx.Guild.Id, ctx.User, track);
                    count = Service.GetQueue(ctx.Guild.Id).Count;
                    eb1 = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription($"Added {track.Title} by {track.Author} to the queue.")
                        .WithThumbnailUrl(await track.FetchArtworkAsync())
                        .WithFooter($"{count} songs in queue");
                    await msg.ModifyAsync(x =>
                    {
                        x.Embed = eb1.Build();
                        x.Components = null;
                    });
                    if (player.PlayerState != PlayerState.Playing)
                    {
                        await player.PlayAsync(x => { x.Track = track; });
                        await player.UpdateVolumeAsync(Convert.ToUInt16(Service.GetVolume(ctx.Guild.Id)));
                    }
                    break;
                case "cancel":
                    var eb13 = new EmbedBuilder()
                        .WithDescription("Cancelled.")
                        .WithErrorColor();
                    await msg.ModifyAsync(x =>
                    {
                        x.Embed = eb13.Build();
                        x.Components = null;
                    });
                    break;
            }
        }

        [MewdekoCommand]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Pause() 
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) 
            {
                await ctx.Channel.SendErrorAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.PlayerState != PlayerState.Playing) 
            {
                await player.ResumeAsync();
                await ctx.Channel.SendConfirmAsync("Resumed player.");
                return;
            }
            await player.PauseAsync();
            await ctx.Channel.SendConfirmAsync($"Paused player. Do {Prefix}pause again to resume.");
        }
        
        [MewdekoCommand]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Shuffle()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out _)) {
                await ctx.Channel.SendErrorAsync("I'm not even playing anything.");
                return;
            }

            if (!Service.GetQueue(ctx.Guild.Id).Any())
            {
                await ctx.Channel.SendErrorAsync("There's nothing in queue.");
                return;
            }

            if (Service.GetQueue(ctx.Guild.Id).Count == 1)
            {
                await ctx.Channel.SendErrorAsync("... There's literally only one thing in queue.");
                return;
            }
            Service.Shuffle(ctx.Guild);
            await ctx.Channel.SendConfirmAsync("Successfully shuffled the queue!");
        }
        
        [MewdekoCommand]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Stop() 
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ctx.Channel.SendErrorAsync("I'm not connected to a channel!");
                return;
            }
            
            await player.StopAsync();
            await Service.QueueClear(ctx.Guild.Id);
            await ctx.Channel.SendConfirmAsync("Stopped the player and cleared the queue!");
        }

        [MewdekoCommand]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Skip()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ctx.Channel.SendErrorAsync("I'm not connected to a voice channel.");
                return;
            }

            await Service.Skip(ctx.Guild, ctx.Channel as ITextChannel, player);
        }

        [MewdekoCommand]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Seek(TimeSpan timeSpan) 
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) 
            {
                await ctx.Channel.SendErrorAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.PlayerState != PlayerState.Playing) 
            {
                await ctx.Channel.SendErrorAsync("Woaaah there, I can't seek when nothing is playing.");
                return;
            }

            if (timeSpan > player.Track.Duration)
            {
                await ctx.Channel.SendErrorAsync("That's longer than the song lol, try again.");
            }
            await player.SeekAsync(timeSpan);
            await ctx.Channel.SendConfirmAsync($"I've seeked `{player.Track.Title}` to {timeSpan}.");
        }

        [MewdekoCommand]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ClearQueue()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ctx.Channel.SendErrorAsync("I'm not connected to a voice channel.");
                return;
            }

            await player.StopAsync();
            await Service.QueueClear(ctx.Guild.Id);
            await ctx.Channel.SendConfirmAsync("Cleared the queue!");
        }

        [MewdekoCommand]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Loop(PlayerRepeatType reptype = PlayerRepeatType.None)
        {
            await Service.ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => { settings.PlayerRepeat = reptype; }, reptype);
            await ctx.Channel.SendConfirmAsync($"Loop has now been set to {reptype}");
        }
        
        [MewdekoCommand]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Volume(ushort volume) 
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
                await ctx.Channel.SendErrorAsync("I'm not connected to a voice channel.");
                return;
            }

            if (volume > 100)
            {
                await ctx.Channel.SendErrorAsync("Max is 100 m8");
                return;
            }
            await player.UpdateVolumeAsync(volume);
            await Service.ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => { settings.Volume = volume; }, volume);
        }

        [MewdekoCommand]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task NowPlaying() {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) 
            {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.PlayerState != PlayerState.Playing) {
                await ReplyAsync("Woaaah there, I'm not playing any tracks.");
                return;
            }

            var qcount = Service.GetQueue(ctx.Guild.Id).Count;
            var track = Service.GetCurrentTrack(player, ctx.Guild);
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"Track #{track.Index}")
                .WithDescription($"Now Playing {track.Title} by {track.Author}")
                .WithThumbnailUrl(track.FetchArtworkAsync().Result)
                .WithFooter($"{track.Position:hh\\:mm\\:ss}/{track.Duration:hh\\:mm\\:ss} | {track.QueueUser} | {track.QueuedPlatform} | {qcount} Tracks in queue");
            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }

        [MewdekoCommand]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Queue() 
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out _))
            {
                await ctx.Channel.SendErrorAsync("I am not playing anything at the moment!");
                return;
            }

            var queue = Service.GetQueue(ctx.Guild.Id);
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(queue.Count/10)
                .WithDefaultCanceledPage()
                .WithDefaultEmotes()
                .Build();
            
            await _interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

            Task<PageBuilder> PageFactory(int page)
            {
                var tracks = queue.OrderBy(x => x.Index).Skip(page * 10).Take(10);
                return Task.FromResult(new PageBuilder()
                    .WithDescription(string.Join("\n", tracks.Select(x =>
                        $"`{ x.Index}.` [{x.Title}]({x.Url})\n" +
                        $"`{x.Duration:mm\\:ss} {x.QueueUser.ToString()} {x.QueuedPlatform}`")))
                    .WithOkColor());
            }
        }
        
    }
}
