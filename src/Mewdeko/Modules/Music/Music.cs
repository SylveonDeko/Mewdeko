using System.Text;
using System.Text.Json;
using System.Threading;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Music.CustomPlayer;
using Serilog;
using Swan;

namespace Mewdeko.Modules.Music;

/// <summary>
///     A module containing music commands.
/// </summary>
public class Music(
    IAudioService service,
    IDataCache cache,
    InteractiveService interactiveService,
    GuildSettingsService guildSettingsService) : MewdekoModule
{
    /// <summary>
    ///     Retrieves the music player an attempts to join the voice channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Join()
    {
        var (player, result) = await GetPlayerAsync();
        if (string.IsNullOrWhiteSpace(result))
        {
            await ReplyConfirmLocalizedAsync("music_join_success", player.VoiceChannelId).ConfigureAwait(false);
        }
        else
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(GetText("music_player_error"))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Disconnects the bot from the voice channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Leave()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(GetText("music_player_error"))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await cache.SetMusicQueue(ctx.Guild.Id, []).ConfigureAwait(false);
        await cache.SetCurrentTrack(ctx.Guild.Id, null);

        await player.DisconnectAsync().ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("music_disconnect").ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears the music queue.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task ClearQueue()
    {
        var (player, result) = await GetPlayerAsync(false);

        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(GetText("music_player_error"))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await cache.SetMusicQueue(ctx.Guild.Id, []).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("music_queue_cleared").ConfigureAwait(false);
        await player.StopAsync();
        await cache.SetCurrentTrack(ctx.Guild.Id, null);
    }

    /// <summary>
    ///     Plays a specified track in the current voice channel.
    /// </summary>
    /// <param name="queueNumber">The queue number to play.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Play([Remainder] int queueNumber)
    {
        var (player, result) = await GetPlayerAsync(false);

        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(GetText("music_player_error"))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorLocalizedAsync("music_queue_empty").ConfigureAwait(false);
            return;
        }

        var actualNumber = queueNumber - 1;
        if (queueNumber < 1 || queueNumber > queue.Count)
        {
            await ReplyErrorLocalizedAsync("music_queue_invalid_index", queue.Count).ConfigureAwait(false);
            return;
        }

        var trackToPlay = queue.FirstOrDefault(x => x.Index == queueNumber);
        await player.StopAsync();
        await player.PlayAsync(trackToPlay.Track).ConfigureAwait(false);
        await cache.SetCurrentTrack(ctx.Guild.Id, trackToPlay);
    }

    /// <summary>
    ///     Plays a track in the current voice channel.
    /// </summary>
    /// <param name="query">The query to search for.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Play([Remainder] string query)
    {
        var (player, result) = await GetPlayerAsync();

        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(GetText("music_player_error"))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await player.SetVolumeAsync(await player.GetVolume() / 100f).ConfigureAwait(false);

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (Uri.TryCreate(query, UriKind.Absolute, out var uri))
        {
            TrackLoadOptions options;
            if (query.Contains("music.youtube"))
            {
                options = new TrackLoadOptions
                {
                    SearchMode = TrackSearchMode.YouTubeMusic
                };
            }
            else if (query.Contains("youtube.com") || query.Contains("youtu.be"))
            {
                options = new TrackLoadOptions
                {
                    SearchMode = TrackSearchMode.YouTube
                };
            }
            else if (query.Contains("open.spotify") || query.Contains("spotify.com"))
            {
                options = new TrackLoadOptions
                {
                    SearchMode = TrackSearchMode.Spotify
                };
            }
            else if (query.Contains("soundcloud.com"))
            {
                options = new TrackLoadOptions
                {
                    SearchMode = TrackSearchMode.SoundCloud
                };
            }
            else
            {
                options = new TrackLoadOptions
                {
                    SearchMode = TrackSearchMode.None
                };
            }

            var trackResults = await service.Tracks.LoadTracksAsync(query, options);
            if (!trackResults.IsSuccess)
            {
                await ReplyErrorLocalizedAsync("music_search_fail").ConfigureAwait(false);
                return;
            }

            if (trackResults.Tracks.Length > 1)
            {
                var startIndex = queue.Count + 1;
                queue.AddRange(trackResults.Tracks.Select(track =>
                    new MewdekoTrack(startIndex++, track, new PartialUser
                    {
                        Id = ctx.User.Id, Username = ctx.User.Username, AvatarUrl = ctx.User.GetAvatarUrl()
                    })));
                await cache.SetMusicQueue(ctx.Guild.Id, queue);

                var eb = new EmbedBuilder()
                    .WithDescription(
                        $"Added {trackResults.Tracks.Length} tracks to the queue from {trackResults.Playlist.Name}")
                    .WithThumbnailUrl(trackResults.Tracks[0].ArtworkUri?.ToString())
                    .WithOkColor()
                    .Build();

                await ctx.Channel.SendMessageAsync(embed: eb).ConfigureAwait(false);
            }
            else
            {
                queue.Add(new MewdekoTrack(queue.Count + 1, trackResults.Tracks[0], new PartialUser
                {
                    Id = ctx.User.Id, Username = ctx.User.Username, AvatarUrl = ctx.User.GetAvatarUrl()
                }));
                await cache.SetMusicQueue(ctx.Guild.Id, queue);
            }

            if (player.CurrentItem is null)
            {
                await player.PlayAsync(trackResults.Tracks[0]).ConfigureAwait(false);
                await cache.SetCurrentTrack(ctx.Guild.Id, queue[0]);
            }
        }
        else
        {
            var tracks = await service.Tracks.LoadTracksAsync(query, TrackSearchMode.YouTube);

            if (!tracks.IsSuccess)
            {
                await ReplyErrorLocalizedAsync("music_no_tracks").ConfigureAwait(false);
                return;
            }

            var trackList = tracks.Tracks.Take(25).ToList();
            var selectMenu = new SelectMenuBuilder()
                .WithCustomId($"track_select:{ctx.User.Id}")
                .WithPlaceholder(GetText("music_select_tracks"))
                .WithMaxValues(trackList.Count)
                .WithMinValues(1);

            foreach (var track in trackList)
            {
                var index = trackList.IndexOf(track);
                selectMenu.AddOption(track.Title.Truncate(100), $"track_{index}");
            }

            var eb = new EmbedBuilder()
                .WithDescription(GetText("music_select_tracks_embed"))
                .WithOkColor()
                .Build();

            var components = new ComponentBuilder().WithSelectMenu(selectMenu).Build();

            var message = await ctx.Channel.SendMessageAsync(embed: eb, components: components);

            await cache.Redis.GetDatabase().StringSetAsync($"{ctx.User.Id}_{message.Id}_tracks",
                JsonSerializer.Serialize(trackList), TimeSpan.FromMinutes(5));
        }
    }

    /// <summary>
    ///     Pauses or unpauses the player based on the current state.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Pause()
    {
        var (player, result) = await GetPlayerAsync();

        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(GetText("music_player_error"))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        if (player.State == PlayerState.Paused)
        {
            await player.ResumeAsync();
            await ReplyConfirmLocalizedAsync("music_resume").ConfigureAwait(false);
        }
        else
        {
            await player.PauseAsync();
            await ReplyConfirmLocalizedAsync("music_pause").ConfigureAwait(false);
        }
    }


    /// <summary>
    ///     Gets the now playing track, if any.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task NowPlaying()
    {
        try
        {
            var (player, result) = await GetPlayerAsync(false);

            if (result is not null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(GetText("music_player_error"))
                    .WithDescription(result);

                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                return;
            }

            var queue = await cache.GetMusicQueue(ctx.Guild.Id);

            if (queue.Count == 0)
            {
                await ReplyErrorLocalizedAsync("music_queue_empty").ConfigureAwait(false);
                return;
            }


            var embed = await player.PrettyNowPlayingAsync(queue);
            await ctx.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error("Failed to get now playing track: {Message}", e.Message);
        }
    }

    /// <summary>
    ///     Removes the selected track from the queue. If the selected track is the current track, it will be skipped. If next
    ///     track is not available, the player will stop.
    /// </summary>
    /// <param name="queueNumber">The queue number to remove.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task SongRemove(int queueNumber)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(GetText("music_player_error"))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        var currentTrack = await cache.GetCurrentTrack(ctx.Guild.Id);
        var nextTrack = queue.FirstOrDefault(x => x.Index == currentTrack.Index + 1);
        if (queue.Count == 0)
        {
            await ReplyErrorLocalizedAsync("music_queue_empty").ConfigureAwait(false);
            return;
        }

        if (queueNumber < 1 || queueNumber > queue.Count)
        {
            await ReplyErrorLocalizedAsync("music_queue_invalid").ConfigureAwait(false);
            return;
        }

        if (nextTrack is not null)
        {
            await player.StopAsync();
            await player.PlayAsync(nextTrack.Track);
            await cache.SetCurrentTrack(ctx.Guild.Id, nextTrack);
        }
        else
        {
            await player.StopAsync();
            await cache.SetCurrentTrack(ctx.Guild.Id, null);
        }

        queue.Remove(currentTrack);
        await cache.SetMusicQueue(ctx.Guild.Id, queue);

        if (player.State == PlayerState.Playing)
        {
            await ReplyConfirmLocalizedAsync("music_song_removed").ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("music_song_removed_stop").ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Moves a song in the queue to a new position.
    /// </summary>
    /// <param name="from">The current position of the song.</param>
    /// <param name="to">The new position of the song.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task MoveSong(int from, int to)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(GetText("music_player_error"))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorLocalizedAsync("music_queue_empty").ConfigureAwait(false);
            return;
        }

        if (from < 1 || from > queue.Count || to < 1 || to > queue.Count + 1)
        {
            await ReplyErrorLocalizedAsync("music_queue_invalid").ConfigureAwait(false);
            return;
        }

        var track = queue.FirstOrDefault(x => x.Index == from);
        var replace = queue.FirstOrDefault(x => x.Index == to);
        var currentSong = await cache.GetCurrentTrack(ctx.Guild.Id);

        queue[queue.IndexOf(track)].Index = to;

        if (currentSong is not null && currentSong.Index == from)
        {
            track.Index = to;
            await cache.SetCurrentTrack(ctx.Guild.Id, track);
        }

        if (replace is not null)
        {
            queue[queue.IndexOf(replace)].Index = from;
        }

        try
        {
            await cache.SetMusicQueue(ctx.Guild.Id, queue);
            await ReplyConfirmLocalizedAsync("music_song_moved", track.Track.Title, to).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to move song.");
        }
    }

    /// <summary>
    ///     Sets the players volume
    /// </summary>
    /// <param name="volume">The volume to set</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Volume(int volume)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(GetText("music_player_error"))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        if (volume is < 0 or > 100)
        {
            await ReplyErrorLocalizedAsync("music_volume_invalid").ConfigureAwait(false);
            return;
        }

        await player.SetVolumeAsync(volume / 100f).ConfigureAwait(false);
        await player.SetGuildVolumeAsync(volume).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("music_volume_set", volume).ConfigureAwait(false);
    }

    /// <summary>
    ///     Skips to the next track.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Skip()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(GetText("music_player_error"))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        if (player.CurrentItem is null)
        {
            await ReplyErrorLocalizedAsync("music_no_current_track").ConfigureAwait(false);
            return;
        }

        await player.SeekAsync(player.CurrentItem.Track.Duration).ConfigureAwait(false);
    }

    /// <summary>
    ///     The music queue.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Queue()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(GetText("music_player_error"))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorLocalizedAsync("music_queue_empty").ConfigureAwait(false);
            return;
        }

        var currentTrack = await cache.GetCurrentTrack(ctx.Guild.Id);

        var paginator = new LazyPaginatorBuilder().AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex((queue.Count - 1) / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(5));

        async Task<PageBuilder> PageFactory(int index)
        {
            await Task.CompletedTask;
            var tracks = queue.OrderBy(x => x.Index).Skip(index * 10).Take(10).ToList();
            var sb = new StringBuilder();
            foreach (var track in tracks)
            {
                if (currentTrack.Index == track.Index)
                    sb.AppendLine(
                        $":loud_sound: **{track.Index}. [{track.Track.Title}]({track.Track.Uri})**" +
                        $"\n`{track.Track.Duration} {track.Requester.Username} {track.Track.Provider}`");
                else
                    sb.AppendLine($"{track.Index}. [{track.Track.Title}]({track.Track.Uri})" +
                                  $"\n`{track.Track.Duration} {track.Requester.Username} {track.Track.Provider}`");
            }

            return new PageBuilder()
                .WithTitle($"Queue - {queue.Count} tracks")
                .WithDescription(sb.ToString())
                .WithOkColor();
        }
    }

    /// <summary>
    ///     Sets the autoplay amount in the guild. Uses spotify api so client secret and id must be valid.
    /// </summary>
    /// <param name="amount">The amount of tracks to autoplay. Max of 5</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AutoPlay(int amount)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(GetText("music_player_error"))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        if (amount is < 0 or > 5)
        {
            await ReplyErrorLocalizedAsync("music_autoplay_invalid").ConfigureAwait(false);
            return;
        }

        await player.SetAutoPlay(amount).ConfigureAwait(false);
        if (amount == 0)
        {
            await ReplyConfirmLocalizedAsync("music_autoplay_disabled").ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmLocalizedAsync("music_autoplay_set", amount).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Gets the guilds current settings for music.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task MusicSettings()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(GetText("music_player_error"))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var volume = await player.GetVolume();
        var autoplay = await player.GetAutoPlay();
        var repeat = await player.GetRepeatType();
        var musicChannel = await player.GetMusicChannel();

        var toSend = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(GetText("music_settings"))
            .WithDescription(
                $"{(autoplay == 0 ? GetText("musicsettings_autoplay_disabled") : GetText("musicsettings_autoplay", autoplay))}\n" +
                $"{GetText("musicsettings_volume", volume)}\n" +
                $"{GetText("musicsettings_repeat", repeat)}\n" +
                $"{(musicChannel == null ? GetText("musicsettings_channel_none") : GetText("musicsettings_channel", musicChannel.Id))}");

        await ctx.Channel.SendMessageAsync(embed: toSend.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the channel where music events will be sent.
    /// </summary>
    /// <param name="channel">The channel where music events will be sent.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task SetMusicChannel(IMessageChannel channel = null)
    {
        var channelToUse = channel ?? ctx.Channel;
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(GetText("music_player_error"))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await player.SetMusicChannelAsync(channelToUse.Id).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("music_channel_set", channelToUse.Id).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets if the bot should loop and how.
    /// </summary>
    /// <param name="repeatType">The repeat type.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Loop(PlayerRepeatType repeatType)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(GetText("music_player_error"))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await player.SetRepeatTypeAsync(repeatType).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("music_repeat_type", repeatType).ConfigureAwait(false);
    }


    private async ValueTask<(MewdekoPlayer, string?)> GetPlayerAsync(bool connectToVoiceChannel = true)
    {
        try
        {
            var channelBehavior = connectToVoiceChannel
                ? PlayerChannelBehavior.Join
                : PlayerChannelBehavior.None;

            var retrieveOptions = new PlayerRetrieveOptions(channelBehavior);

            var options = new MewdekoPlayerOptions
            {
                Channel = ctx.Channel as ITextChannel
            };

            var result = await service.Players
                .RetrieveAsync<MewdekoPlayer, MewdekoPlayerOptions>(Context, CreatePlayerAsync, options,
                    retrieveOptions)
                .ConfigureAwait(false);

            await result.Player.SetVolumeAsync(await result.Player.GetVolume() / 100f).ConfigureAwait(false);

            if (result.IsSuccess) return (result.Player, null);
            var errorMessage = result.Status switch
            {
                PlayerRetrieveStatus.UserNotInVoiceChannel => GetText("music_not_in_channel"),
                PlayerRetrieveStatus.BotNotConnected => GetText("music_bot_not_connect",
                    await guildSettingsService.GetPrefix(ctx.Guild)),
                PlayerRetrieveStatus.VoiceChannelMismatch => GetText("music_voice_channel_mismatch"),
                PlayerRetrieveStatus.Success => null,
                PlayerRetrieveStatus.UserInSameVoiceChannel => null,
                PlayerRetrieveStatus.PreconditionFailed => null,
                _ => throw new ArgumentOutOfRangeException()
            };
            return (null, errorMessage);
        }
        catch (TimeoutException)
        {
            return (null, GetText("music_lavalink_disconnected"));
        }
    }

    private static ValueTask<MewdekoPlayer> CreatePlayerAsync(
        IPlayerProperties<MewdekoPlayer, MewdekoPlayerOptions> properties,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);

        return ValueTask.FromResult(new MewdekoPlayer(properties));
    }
}