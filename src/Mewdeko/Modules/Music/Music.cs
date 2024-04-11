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
using Mewdeko.Modules.Music.CustomPlayer;
using Swan;

namespace Mewdeko.Modules.Music;

/// <summary>
/// A module containing music commands.
/// </summary>
public class Music(IAudioService service, IDataCache cache, InteractiveService interactiveService) : MewdekoModule
{
    /// <summary>
    /// Retrieves the music player an attempts to join the voice channel.
    /// </summary>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Join()
    {
        var (player, result) = await GetPlayerAsync();
        if (string.IsNullOrWhiteSpace(result))
        {
            await ReplyConfirmLocalizedAsync("music_join_success", player.VoiceChannelId).ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorLocalizedAsync("music_join_fail").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disconnects the bot from the voice channel.
    /// </summary>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Leave()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await ReplyErrorLocalizedAsync("music_join_fail_not_channel").ConfigureAwait(false);
            return;
        }

        await cache.SetMusicQueue(ctx.Guild.Id, []).ConfigureAwait(false);

        await player.DisconnectAsync().ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("music_disconnect").ConfigureAwait(false);
    }

    /// <summary>
    /// Clears the music queue.
    /// </summary>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task ClearQueue()
    {
        await cache.SetMusicQueue(ctx.Guild.Id, []).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync("Queue cleared.").ConfigureAwait(false);
    }

    /// <summary>
    /// Plays a specified track in the current voice channel.
    /// </summary>
    /// <param name="queueNumber">The queue number to play.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Play([Remainder] int queueNumber)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await ReplyErrorLocalizedAsync("music_join_fail").ConfigureAwait(false);
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

        await player.StopAsync();
        await player.PlayAsync(queue[actualNumber]).ConfigureAwait(false);
    }

    /// <summary>
    /// Plays a track in the current voice channel.
    /// </summary>
    /// <param name="query">The query to search for.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Play([Remainder] string query)
    {
        var (player, result) = await GetPlayerAsync();

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);

        if (string.IsNullOrWhiteSpace(result))
        {
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
                    queue.AddRange(trackResults.Tracks);
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
                    queue.Add(trackResults.Tracks[0]);
                }

                if (player.CurrentItem is null)
                {
                    await player.PlayAsync(trackResults.Tracks[0]).ConfigureAwait(false);
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
                    .WithCustomId("track_select")
                    .WithPlaceholder("Select a track to play");

                foreach (var track in trackList)
                {
                    var index = trackList.IndexOf(track);
                    selectMenu.AddOption(track.Title.Truncate(100), $"track_{index}");
                }

                var eb = new EmbedBuilder()
                    .WithDescription("Select a track to play from the list below.")
                    .WithOkColor()
                    .Build();

                var components = new ComponentBuilder().WithSelectMenu(selectMenu).Build();

                var message = await ctx.Channel.SendMessageAsync(embed: eb, components: components);

                await cache.Redis.GetDatabase().StringSetAsync($"{ctx.User.Id}_{message.Id}_tracks",
                    JsonSerializer.Serialize(trackList), expiry: TimeSpan.FromMinutes(5));
            }
        }
        else
        {
            await ReplyErrorLocalizedAsync("music_join_fail").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Removes the selected track from the queue. If the selected track is the current track, it will be skipped. If next track is not available, the player will stop.
    /// </summary>
    /// <param name="queueNumber">The queue number to remove.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task SongRemove(int queueNumber)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await ReplyErrorLocalizedAsync("music_join_fail").ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        var currentTrack = queue.IndexOf(player.CurrentItem.Track);
        var nextTrack = queue.ElementAtOrDefault(currentTrack + 1);
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
            await player.PlayAsync(nextTrack);
        }
        else
        {
            await player.StopAsync();
        }

        queue.RemoveAt(currentTrack);
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

    ///<summary>
    /// Moves a song in the queue to a new position.
    /// </summary>
    /// <param name="from">The current position of the song.</param>
    /// <param name="to">The new position of the song.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task MoveSong(int from, int to)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await ReplyErrorLocalizedAsync("music_join_fail").ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorLocalizedAsync("music_queue_empty").ConfigureAwait(false);
            return;
        }

        if (from < 1 || from > queue.Count || to < 1 || to > queue.Count)
        {
            await ReplyErrorLocalizedAsync("music_queue_invalid").ConfigureAwait(false);
            return;
        }

        var actualFrom = from - 1;
        var actualTo = to - 1;

        var track = queue[actualFrom];
        queue.RemoveAt(actualFrom);
        queue.Insert(actualTo, track);

        await cache.SetMusicQueue(ctx.Guild.Id, queue);
        await ReplyConfirmLocalizedAsync("music_song_moved", track.Title, actualTo).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the players volume
    /// </summary>
    /// <param name="volume">The volume to set</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Volume(int volume)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await ReplyErrorLocalizedAsync("music_join_fail").ConfigureAwait(false);
            return;
        }

        if (volume is < 0 or > 100)
        {
            await ReplyErrorLocalizedAsync("music_volume_invalid").ConfigureAwait(false);
            return;
        }

        await player.SetVolumeAsync(volume / 100).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("music_volume_set", volume).ConfigureAwait(false);
    }

    ///<summary>
    /// Skips to the next track.
    /// </summary>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Skip()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await ReplyErrorLocalizedAsync("music_join_fail").ConfigureAwait(false);
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
    /// The music queue.
    /// </summary>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Queue()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await ReplyErrorLocalizedAsync("music_join_fail").ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorLocalizedAsync("music_queue_empty").ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder().AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(queue.Count / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(5));

        async Task<PageBuilder> PageFactory(int index)
        {
            var tracks = queue.Skip(index * 10).Take(10).ToList();
            var sb = new StringBuilder();
            foreach (var track in tracks)
            {
                if (player.CurrentItem is not null && player.CurrentItem.Track == track)
                    sb.AppendLine($":loud_sound: **{tracks.IndexOf(track) + 1}. [{track.Title}]({track.Uri})**");
                else
                    sb.AppendLine($"{tracks.IndexOf(track) + 1}. [{track.Title}]({track.Uri})");
            }

            return new PageBuilder()
                .WithTitle($"Queue - {queue.Count} tracks")
                .WithDescription(sb.ToString())
                .WithOkColor();
        }
    }

    /// <summary>
    /// Sets the channel where music events will be sent.
    /// </summary>
    /// <param name="channel">The channel where music events will be sent.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task SetMusicChannel(IMessageChannel channel)
    {
        var (player, reason) = await GetPlayerAsync(false);
        if (reason is not null)
        {
            await ReplyErrorLocalizedAsync("music_join_fail").ConfigureAwait(false);
            return;
        }

        await player.SetMusicChannelAsync(channel.Id, ctx.Guild.Id).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("music_channel_set", channel.Id).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets if the bot should loop and how.
    /// </summary>
    /// <param name="repeatType">The repeat type.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Loop(PlayerRepeatType repeatType)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await ReplyErrorLocalizedAsync("music_join_fail").ConfigureAwait(false);
            return;
        }

        await player.SetRepeatTypeAsync(repeatType, ctx.Guild.Id).ConfigureAwait(false);
        await ReplyConfirmLocalizedAsync("music_repeat_type", repeatType).ConfigureAwait(false);
    }


    private async ValueTask<(MewdekoPlayer, string?)> GetPlayerAsync(bool connectToVoiceChannel = true)
    {
        var channelBehavior = connectToVoiceChannel
            ? PlayerChannelBehavior.Join
            : PlayerChannelBehavior.None;

        var retrieveOptions = new PlayerRetrieveOptions(ChannelBehavior: channelBehavior);

        var options = new MewdekoPlayerOptions
        {
            Channel = ctx.Channel as ITextChannel
        };

        var result = await service.Players
            .RetrieveAsync<MewdekoPlayer, MewdekoPlayerOptions>(Context, CreatePlayerAsync, options, retrieveOptions)
            .ConfigureAwait(false);

        if (result.IsSuccess) return (result.Player, null);
        var errorMessage = result.Status switch
        {
            PlayerRetrieveStatus.UserNotInVoiceChannel => "You are not connected to a voice channel.",
            PlayerRetrieveStatus.BotNotConnected => "The bot is currently not connected.",
            _ => "Unknown error.",
        };
        return (null, errorMessage);
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