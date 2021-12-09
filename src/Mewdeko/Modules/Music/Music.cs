using System;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common;
using Mewdeko._Extensions;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Music.Services;
using Mewdeko.Services.Database.Models;
using Victoria;
using Victoria.Enums;
using Victoria.Responses.Search;

namespace Mewdeko.Modules.Music
{
    public class Music : MewdekoModuleBase<MusicService>
    {
        public LavaNode _lavaNode;

        public Music(LavaNode lava)
        {
            _lavaNode = lava;
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
        public async Task Play([Remainder] string searchQuery)
        {
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
            Regex plregex =
                new Regex(
                    @"(?:https?:\/\/)?(?:youtu\.be\/|(?:www\.|m\.)?youtube\.com\/(?:playlist|list|embed)(?:\.php)?(?:\?.*list=|\/))([a-zA-Z0-9\-_]+)");
            var match2 = plregex.Match(searchQuery);
            if (match2.Success)
            {
                searchResponse = await _lavaNode.SearchAsync(SearchType.Direct, searchQuery);
                player.Queue.Enqueue(searchResponse.Tracks);
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription(
                        $"Queued {searchResponse.Tracks.Count()} tracks from {searchResponse.Playlist.Name}");
                await ctx.Channel.SendMessageAsync(embed: eb.Build());
                if (player.PlayerState != PlayerState.Playing)
                    await player.PlayAsync(searchResponse.Tracks.FirstOrDefault());
            }

            Regex regex =
                new Regex(
                    @"(?:youtube\.com\/\S*(?:(?:\/e(?:mbed))?\/|watch\?(?:\S*?&?v\=))|youtu\.be\/)(?<id>[a-zA-Z0-9_-]{6,11})",
                    RegexOptions.Compiled);
            var match = regex.Match(searchQuery);
            if (match.Success)
            {
                searchResponse = await _lavaNode.SearchAsync(SearchType.Direct, searchQuery);
                player.Queue.Enqueue(searchResponse.Tracks);
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription($"Added {searchResponse.Tracks.FirstOrDefault().Title} to the queue.")
                    .WithThumbnailUrl(searchResponse.Tracks.FirstOrDefault().FetchArtworkAsync().Result);
                await ctx.Channel.SendMessageAsync(embed: eb.Build());
                if (player.PlayerState != PlayerState.Playing)
                    await player.PlayAsync(x =>
                {
                    x.Track = searchResponse.Tracks.FirstOrDefault();
                });
                
            }

            searchResponse = await _lavaNode.SearchAsync(SearchType.YouTubeMusic, searchQuery);
            if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
            {
                await ctx.Channel.SendErrorAsync("Seems like I can't find tha video, please try again.");
                return;
            }
            player.Queue.Enqueue(searchResponse.Tracks);
            var eb1 = new EmbedBuilder()
                .WithOkColor()
                .WithDescription($"Added {searchResponse.Tracks.FirstOrDefault().Title} to the queue.")
                .WithThumbnailUrl(searchResponse.Tracks.FirstOrDefault().FetchArtworkAsync().Result);
            await ctx.Channel.SendMessageAsync(embed: eb1.Build());
            if (player.PlayerState != PlayerState.Playing)
                await player.PlayAsync(x =>
                {
                    x.Track = searchResponse.Tracks.FirstOrDefault();
                });
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
                .WithFooter($"{track.Position}/{track.Duration}");

            await ReplyAsync(embed: embed.Build());
        }

        [MewdekoCommand]
        public Task QueueAsync() {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player)) {
                return ReplyAsync("I'm not connected to a voice channel.");
            }

            return ReplyAsync(player.PlayerState != PlayerState.Playing
                ? "Woaaah there, I'm not playing any tracks."
                : string.Join(Environment.NewLine, player.Queue.Select(x => x.Title)));
        }
        
    }
}
