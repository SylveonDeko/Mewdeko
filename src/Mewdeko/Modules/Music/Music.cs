#nullable enable
using System;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Google.Apis.YouTube.v3.Data;
using Mewdeko.Common;
using Mewdeko._Extensions;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Music.Extensions;
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
        public LavaNode _lavaNode;
        public InteractiveService Interactivity;
        public readonly IServiceProvider ServiceProvider;
        public DiscordSocketClient Client;

        public Music(LavaNode lava, InteractiveService interactive, DiscordSocketClient client, IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            Client = client;
            Interactivity = interactive;
            _lavaNode = lava;
        }

        public enum Setting
        {
            Volume,
            Repeat,
            AutoDisconnect,
            FairSkip,
            ViewSettings,
            MusicChannel
        }
        [MewdekoCommand]
        public async Task Join() {
            if (_lavaNode.HasPlayer(Context.Guild)) {
                await ReplyAsync("I'm already connected to a voice channel!");
                return;
            }

            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null) {
                await ReplyAsync("You must be connected to a voice channel!");
                return;
            }

            try {
                await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
                await ReplyAsync($"Joined {voiceState.VoiceChannel.Name}!");
            }
            catch (Exception exception) {
                await ReplyAsync(exception.Message);
            }
        }

        [MewdekoCommand]
        public async Task Leave() 
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
                await ReplyAsync("I'm not connected to any voice channels!");
                return;
            }

            var voiceChannel = (Context.User as IVoiceState)?.VoiceChannel ?? player.VoiceChannel;
            if (voiceChannel == null) {
                await ReplyAsync("Not sure which voice channel to disconnect from.");
                return;
            }

            try {
                await _lavaNode.LeaveAsync(voiceChannel);
                await ReplyAsync($"I've left {voiceChannel.Name}!");
            }
            catch (Exception exception) {
                await ReplyAsync(exception.Message);
            }
        }

        [MewdekoCommand]
        public async Task MSettings(Setting setting = Setting.ViewSettings, string? value = null)
        {
            switch (setting)
            {
                case Setting.Volume:
                    if (value is null)
                    {
                        await ctx.Channel.SendConfirmAsync(
                            $"The current default volume is {Service.GetVolume(ctx.Guild.Id)}");
                    }

                    if (int.TryParse(value, out int volume))
                    {
                        if (volume > 200)
                        {
                            await ctx.Channel.SendErrorAsync("You cannot go above 200% volume!");
                            return;
                        }
                        await Service.ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => { settings.Volume = volume; }, volume);
                        await ctx.Channel.SendMessageAsync($"Volume set to {volume}%");
                    }
                    else
                    {
                        await ctx.Channel.SendErrorAsync(
                            "Seems like the amount you entered was incorrect, or invalid.");
                    }

                    break;
                case Setting.Repeat:
                    
                    break;
                case Setting.AutoDisconnect:
                    break;
                case Setting.FairSkip:
                    break;
                case Setting.ViewSettings:
                    break;
                case Setting.MusicChannel:
                    var tr = new ChannelTypeReader<ITextChannel>();
                    var channel = tr.ReadAsync(ctx, value, ServiceProvider).Result;
                    if (channel.IsSuccess)
                    {
                        ITextChannel ch = (ITextChannel) channel.BestMatch;
                        await Service.ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => { settings.MusicChannelId = ch.Id; }, ch.Id);
                        await ctx.Channel.SendConfirmAsync($"Music channel set to {ch.Mention}");
                    }
                    else
                    {
                        await ctx.Channel.SendErrorAsync(
                            "Seems like the channel you entered was incorrect, please try again.");
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(setting), setting, null);
            }
        }

        [MewdekoCommand]
        [Description]
        [Aliases]
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
                Console.Write("e");
                await ctx.Channel.SendMessageAsync(embed: eb.Build());
            }
        }
        [MewdekoCommand]
        [Description]
        [Aliases]
        public async Task Play([Remainder] string searchQuery)
        {
            int count = 0;
            try
            {
                count = Service.GetQueue(ctx.Guild.Id).Count;
            }
            catch
            {
                // none
            }
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
                else
                {
                    await _lavaNode.JoinAsync(vc.VoiceChannel);
                }
            }

            var player = _lavaNode.GetPlayer(ctx.Guild);
            SearchResponse searchResponse;
            if (Uri.IsWellFormedUriString(searchQuery, UriKind.RelativeOrAbsolute) && searchQuery.Contains("youtube.com") || searchQuery.Contains("youtu.be"))
            {
                
                searchResponse = await _lavaNode.SearchAsync(SearchType.Direct, searchQuery);
                var track1 = searchResponse.Tracks.FirstOrDefault();
                await Service.Enqueue(ctx.Guild.Id, ctx.User, searchResponse.Tracks.ToArray());
                
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
                            $"Queued {searchResponse.Tracks.Count()} tracks from {searchResponse.Playlist.Name}");
                    await ctx.Channel.SendMessageAsync(embed: eb.Build());
                    if (player.PlayerState != PlayerState.Playing)
                        await player.PlayAsync(x => { x.Track = track1; });
                    await player.UpdateVolumeAsync(Convert.ToUInt16(Service.GetVolume(ctx.Guild.Id)));
                }
            }

            searchResponse = await _lavaNode.SearchAsync(SearchType.YouTube, searchQuery);
            if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
            {
                await ctx.Channel.SendErrorAsync("Seems like I can't find tha video, please try again.");
                return;
            }
            var track = searchResponse.Tracks.FirstOrDefault();
            await Service.Enqueue(ctx.Guild.Id, ctx.User, searchResponse.Tracks.ToArray());
            var eb1 = new EmbedBuilder()
                .WithOkColor()
                .WithDescription($"Added {track.Title} to the queue.")
                .WithThumbnailUrl(await track.FetchArtworkAsync())
                .WithFooter($"{count} songs in queue");
            await ctx.Channel.SendMessageAsync(embed: eb1.Build());
            if (player.PlayerState != PlayerState.Playing)
                await player.PlayAsync(x =>
                {
                    x.Track = track;
                });
            await player.UpdateVolumeAsync(Convert.ToUInt16(Service.GetVolume(ctx.Guild.Id)));
        }

        [MewdekoCommand]
        public async Task Pause() 
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.PlayerState != PlayerState.Playing) {
                await ReplyAsync("I cannot pause when I'm not playing anything!");
                return;
            }

            try {
                await player.PauseAsync();
                await ReplyAsync($"Paused: {player.Track.Title}");
            }
            catch (Exception exception) {
                await ReplyAsync(exception.Message);
            }
        }

        [MewdekoCommand]
        public async Task Resume() {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.PlayerState != PlayerState.Paused) {
                await ReplyAsync("I cannot resume when I'm not playing anything!");
                return;
            }

            try {
                await player.ResumeAsync();
                await ReplyAsync($"Resumed: {player.Track.Title}");
            }
            catch (Exception exception) {
                await ReplyAsync(exception.Message);
            }
        }

        [MewdekoCommand]
        public async Task Stop() {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.PlayerState == PlayerState.Stopped) {
                await ReplyAsync("Woaaah there, I can't stop the stopped forced.");
                return;
            }

            try {
                await player.StopAsync();
                await ReplyAsync("No longer playing anything.");
            }
            catch (Exception exception) {
                await ReplyAsync(exception.Message);
            }
        }

        [MewdekoCommand]
        public async Task Skip() {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.PlayerState != PlayerState.Playing) {
                await ReplyAsync("Woaaah there, I can't skip when nothing is playing.");
                return;
            }
            
            
            try
            {
                await Service.Skip(ctx.Guild, player);
                await ReplyAsync($"Now Playing: {player.Track.Title}");
            }
            catch (Exception exception) {
                await ReplyAsync(exception.Message);
            }
        }

        [MewdekoCommand]
        public async Task Seek(TimeSpan timeSpan) {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.PlayerState != PlayerState.Playing) {
                await ReplyAsync("Woaaah there, I can't seek when nothing is playing.");
                return;
            }

            try {
                await player.SeekAsync(timeSpan);
                await ReplyAsync($"I've seeked `{player.Track.Title}` to {timeSpan}.");
            }
            catch (Exception exception) {
                await ReplyAsync(exception.Message);
            }
        }

        [MewdekoCommand]
        public async Task ClearQueue()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ctx.Channel.SendErrorAsync("I'm not connected to a voice channel.");
                return;
            }
            player.Queue.Clear();
            await ctx.Channel.SendConfirmAsync("Cleared the queue!");
        }

        [MewdekoCommand]
        public async Task Loop(PlayerRepeatType reptype = PlayerRepeatType.None)
        {
            await Service.ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => { settings.PlayerRepeat = reptype; }, reptype);
            await ctx.Channel.SendConfirmAsync($"Loop has now been set to {reptype}");
        }
        
        [MewdekoCommand]
        public async Task Volume(ushort volume) {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }

            try {
                await player.UpdateVolumeAsync(volume);
                await Service.ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => { settings.Volume = volume; }, volume);
                await ReplyAsync($"I've changed the player volume to {volume}.");
            }
            catch (Exception exception) {
                await ReplyAsync(exception.Message);
            }
        }

        [MewdekoCommand]
        public async Task NowPlaying() {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
                await ReplyAsync("I'm not connected to a voice channel.");
                return;
            }

            if (player.PlayerState != PlayerState.Playing) {
                await ReplyAsync("Woaaah there, I'm not playing any tracks.");
                return;
            }

            var track = player.Track;
            var artwork = await track.FetchArtworkAsync();

            var embed = new EmbedBuilder()
                .WithAuthor(track.Author, Context.Client.CurrentUser.GetAvatarUrl(), track.Url)
                .WithTitle($"Now Playing: {track.Title}")
                .WithImageUrl(artwork)
                .WithFooter($"{track.Position:hh\\:mm\\:ss}/{track.Duration:hh\\:mm\\:ss}");

            await ReplyAsync(embed: embed.Build());
        }

        [MewdekoCommand]
        public async Task Queue() {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ctx.Channel.SendErrorAsync("I am not playing anything at the moment!");
                return;
            }

            var queue = Service._queues.FirstOrDefault(x => x.Key == ctx.Guild.Id).Value;
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(queue.Count/10)
                .WithDefaultCanceledPage()
                .WithDefaultEmotes()
                .Build();
            
            await Interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

            Task<PageBuilder> PageFactory(int page)
            {
                var tracks = queue.Skip(page).Take(10);
                return Task.FromResult(new PageBuilder()
                    .WithDescription(string.Join("\n", tracks.Select(x =>
                        $"`{ x.Index}.` [{x.Title}]({x.Url})\n" +
                        $"`{x.Duration:mm\\:ss} {x.QueueUser.ToString()}`")))
                    .WithOkColor());
            }
        }
        
    }
}
