using System.Text.Json;
using System.Threading;
using DataModel;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.PubSub;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Music.CustomPlayer;
using SpotifyAPI.Web;
using Swan;

namespace Mewdeko.Modules.Music;

/// <summary>
///     A module containing music commands.
/// </summary>
public partial class Music(
    IAudioService service,
    IDataCache cache,
    InteractiveService interactiveService,
    GuildSettingsService guildSettingsService,
    ILogger<Music> logger,
    MusicEventManager eventManager,
    IPubSub pubSub,
    IDataConnectionFactory dbFactory,
    IBotCredentials creds) : MewdekoModule
{
    /// <summary>
    ///     Retrieves the music player an attempts to join the voice channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Join()
    {
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (player, result) = await GetPlayerAsync();
        if (string.IsNullOrWhiteSpace(result))
            await ReplyConfirmAsync(Strings.MusicJoinSuccess(ctx.Guild.Id, player.VoiceChannelId))
                .ConfigureAwait(false);
        else
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
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
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        await cache.SetMusicQueue(ctx.Guild.Id, []).ConfigureAwait(false);
        await cache.SetCurrentTrack(ctx.Guild.Id, null);

        await player.DisconnectAsync().ConfigureAwait(false);

        // Notify connected clients that bot has disconnected
        await eventManager.BroadcastDisconnection(ctx.Guild.Id).ConfigureAwait(false);

        // Publish Redis event when player is destroyed (bot leaves voice)
        var key = new TypedKey<string>($"music:player:destroyed:{ctx.Guild.Id}");
        await pubSub.Pub(key, "destroyed");
        logger.LogInformation("Published player destroyed event for guild {GuildId}", ctx.Guild.Id);

        await ReplyConfirmAsync(Strings.MusicDisconnect(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears the music queue.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task ClearQueue()
    {
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (player, result) = await GetPlayerAsync(false);

        if (result is not null)
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        await cache.SetMusicQueue(ctx.Guild.Id, []).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicQueueCleared(ctx.Guild.Id)).ConfigureAwait(false);
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
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (player, result) = await GetPlayerAsync(false);

        if (result is not null)
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (queueNumber < 1 || queueNumber > queue.Count)
        {
            await ReplyErrorAsync(Strings.MusicQueueInvalidIndex(ctx.Guild.Id, queue.Count)).ConfigureAwait(false);
            return;
        }

        var trackToPlay = queue.FirstOrDefault(x => x.Index == queueNumber);
        await player.StopAsync();
        await player.PlayAsync(trackToPlay.Track).ConfigureAwait(false);
        await cache.SetCurrentTrack(ctx.Guild.Id, trackToPlay);
    }

    /// <summary>
    ///     Plays music from various sources including YouTube, Spotify, and direct searches.
    ///     Supports tracks, playlists, and albums from supported platforms.
    /// </summary>
    /// <param name="query">URL or search query for the music to play</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Play([Remainder] string query)
    {
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        try
        {
            // Get or create player
            var (player, result) = await GetPlayerAsync();
            if (result is not null)
            {
                await ctx.Channel.SendErrorAsync(Strings.MusicPlayerError(ctx.Guild.Id), Config);
                return;
            }

            // Set initial volume
            await player.SetVolumeAsync(await player.GetVolume() / 100f);
            var queue = await cache.GetMusicQueue(ctx.Guild.Id);

            if (Uri.TryCreate(query, UriKind.Absolute, out var uri))
            {
                await HandleUrlPlay(uri, queue, player);
            }
            else
            {
                await HandleSearchPlay(query);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Play command with query: {Query}", query);
            await ReplyErrorAsync(Strings.MusicGenericError(ctx.Guild.Id));
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
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (player, result) = await GetPlayerAsync();

        if (result is not null)
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        if (player.State == PlayerState.Paused)
        {
            await player.ResumeAsync();
            await ReplyConfirmAsync(Strings.MusicResume(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await player.PauseAsync();
            await ReplyConfirmAsync(Strings.MusicPause(ctx.Guild.Id)).ConfigureAwait(false);
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
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        try
        {
            var (player, result) = await GetPlayerAsync(false);

            if (result is not null)
            {
                var errorComponents = new ComponentBuilderV2()
                    .WithContainer([
                        new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                    ], Mewdeko.ErrorColor)
                    .WithSeparator()
                    .WithContainer(new TextDisplayBuilder(result));

                await ctx.Channel.SendMessageAsync(components: errorComponents.Build(),
                    flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
                return;
            }

            var queue = await cache.GetMusicQueue(ctx.Guild.Id);

            if (queue.Count == 0)
            {
                await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }


            var components = await player.PrettyNowPlayingAsync(queue);
            await ctx.Channel.SendMessageAsync(components: components,
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            logger.LogError("Failed to get now playing track: {Message}", e.Message);
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
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        var currentTrack = await cache.GetCurrentTrack(ctx.Guild.Id);
        var nextTrack = queue.FirstOrDefault(x => x.Index == currentTrack.Index + 1);
        if (queue.Count == 0)
        {
            await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (queueNumber < 1 || queueNumber > queue.Count)
        {
            await ReplyErrorAsync(Strings.MusicQueueInvalidIndex(ctx.Guild.Id, queue.Count)).ConfigureAwait(false);
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
            await ReplyConfirmAsync(Strings.MusicSongRemoved(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.MusicSongRemovedStop(ctx.Guild.Id)).ConfigureAwait(false);
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
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (_, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (from < 1 || from > queue.Count || to < 1 || to > queue.Count + 1)
        {
            await ReplyErrorAsync(Strings.MusicQueueInvalidIndex(ctx.Guild.Id, queue.Count)).ConfigureAwait(false);
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
            await ReplyConfirmAsync(Strings.MusicSongMoved(ctx.Guild.Id, track.Track.Title, to)).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to move song.");
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
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        if (volume is < 0 or > 100)
        {
            await ReplyErrorAsync(Strings.MusicVolumeInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await player.SetVolumeAsync(volume / 100f).ConfigureAwait(false);
        await player.SetGuildVolumeAsync(volume).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicVolumeSet(ctx.Guild.Id, volume)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Seeks to a specific position in the current track.
    /// </summary>
    /// <param name="timeSpan">Time to seek to in format mm:ss</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Seek([Remainder] string timeSpan)
    {
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        if (player.CurrentItem is null)
        {
            await ReplyErrorAsync(Strings.MusicNoCurrentTrack(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (!TimeSpan.TryParseExact(timeSpan, "mm\\:ss", null, out var position))
        {
            await ReplyErrorAsync(Strings.MusicInvalidTimeFormat(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (position > player.CurrentItem.Track.Duration)
        {
            await ReplyErrorAsync(Strings.MusicSeekOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await player.SeekAsync(position).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicSeekedTo(ctx.Guild.Id, position.ToString(@"mm\:ss")))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Initiates or participates in a vote to skip the current track.
    /// </summary>
    /// <remarks>
    ///     Requires 70% of users in the voice channel to vote for skipping.
    ///     Users with specific roles can be configured to skip without voting.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoteSkip()
    {
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        if (player.CurrentItem is null)
        {
            await ReplyErrorAsync(Strings.MusicNoCurrentTrack(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        // Check if user has DJ role for instant skip
        if (await HasDjRole(ctx.Guild, ctx.User as IGuildUser))
        {
            await player.SeekAsync(player.CurrentItem.Track.Duration).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicSkipDj(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var voiceChannel = (ctx.User as IGuildUser)?.VoiceChannel;
        if (voiceChannel == null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var votes = await cache.GetVoteSkip(ctx.Guild.Id) ?? [];
        if (votes.Add(ctx.User.Id))
            await cache.SetVoteSkip(ctx.Guild.Id, votes);

        var usersInVoice = (await voiceChannel.GetUsersAsync().FlattenAsync()).Count(x => !x.IsBot);
        var votesNeeded = (int)Math.Ceiling(usersInVoice * 0.7);

        if (votes.Count >= votesNeeded)
        {
            await player.SeekAsync(player.CurrentItem.Track.Duration).ConfigureAwait(false);
            await cache.SetVoteSkip(ctx.Guild.Id, null);
            await ReplyConfirmAsync(Strings.MusicSkipVoteSuccess(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(
                Strings.MusicSkipVoteCount(ctx.Guild.Id, votes.Count, votesNeeded)
            ).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets the DJ role for music commands that require elevated permissions.
    /// </summary>
    /// <param name="role">The role to set as DJ. If null, removes the DJ role.</param>
    [Cmd]
    [Aliases]
    [RequireUserPermission(GuildPermission.ManageGuild)]
    [RequireContext(ContextType.Guild)]
    public async Task SetDjRole(IRole role = null)
    {
        var settings = await cache.GetMusicPlayerSettings(ctx.Guild.Id)
                       ?? new MusicPlayerSetting
                       {
                           GuildId = ctx.Guild.Id
                       };

        settings.DjRoleId = role?.Id;
        await cache.SetMusicPlayerSettings(ctx.Guild.Id, settings);

        if (role == null)
            await ReplyConfirmAsync(Strings.MusicDjRoleRemoved(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.MusicDjRoleSet(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
    }

    private async Task<bool> HasDjRole(IGuild guild, IGuildUser user)
    {
        if (user.GuildPermissions.Administrator) return true;

        var settings = await cache.GetMusicPlayerSettings(guild.Id);
        if (settings?.DjRoleId == null) return false;

        return user.RoleIds.Contains(settings.DjRoleId.Value);
    }

    /// <summary>
    ///     Saves the current queue as a named playlist.
    /// </summary>
    /// <param name="name">The name to save the playlist as.</param>
    /// <remarks>
    ///     Saves all tracks currently in the queue to a persistent playlist that can be loaded later.
    ///     Playlists are saved per guild.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task SavePlaylist([Remainder] string name)
    {
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (_, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var playlist = new MusicPlaylist
        {
            Name = name,
            AuthorId = ctx.User.Id,
            MusicPlaylistTracks = queue.Select(x => new MusicPlaylistTrack
            {
                Title = x.Track.Title, Uri = x.Track.Uri.ToString(), Duration = x.Track.Duration
            }).ToList()
        };

        await cache.SavePlaylist(ctx.Guild.Id, playlist).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicPlaylistSaved(ctx.Guild.Id, name, queue.Count)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Loads a previously saved playlist into the queue.
    /// </summary>
    /// <param name="name">The name of the playlist to load.</param>
    /// <param name="clear">Whether to clear the current queue before loading. Defaults to false.</param>
    /// <remarks>
    ///     Loads all tracks from a saved playlist into the current queue.
    ///     Can optionally clear the current queue first.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task LoadPlaylist(string name, bool clear = false)
    {
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (player, result) = await GetPlayerAsync();
        if (result is not null)
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        var playlist = await cache.GetPlaylist(ctx.Guild.Id, name);
        if (playlist == null)
        {
            await ReplyErrorAsync(Strings.MusicPlaylistNotFound(ctx.Guild.Id, name)).ConfigureAwait(false);
            return;
        }

        var queue = clear ? [] : await cache.GetMusicQueue(ctx.Guild.Id);
        var startIndex = queue.Count + 1;

        foreach (var savedTrack in playlist.MusicPlaylistTracks)
        {
            var trackResult = await service.Tracks.LoadTrackAsync(savedTrack.Uri, TrackSearchMode.YouTube);
            if (trackResult is null) continue;

            queue.Add(new MewdekoTrack(startIndex++, trackResult, new PartialUser
            {
                Id = ctx.User.Id, Username = ctx.User.Username, AvatarUrl = ctx.User.GetAvatarUrl()
            }));
        }

        await cache.SetMusicQueue(ctx.Guild.Id, queue);

        if (player.CurrentItem is null && queue.Count > 0)
        {
            await player.PlayAsync(queue[0].Track).ConfigureAwait(false);
            await cache.SetCurrentTrack(ctx.Guild.Id, queue[0]);
        }

        await ReplyConfirmAsync(
            Strings.MusicPlaylistLoaded(ctx.Guild.Id, name, playlist.MusicPlaylistTracks.Count(), playlist.AuthorId)
        ).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all saved playlists for the guild.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Playlists()
    {
        var playlists = await cache.GetPlaylists(ctx.Guild.Id);
        if (!playlists.Any())
        {
            await ReplyErrorAsync(Strings.MusicNoPlaylists(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {Strings.MusicPlaylistsTitle(ctx.Guild.Id)}")
            ], Mewdeko.OkColor)
            .WithSeparator();

        foreach (var playlist in playlists)
        {
            var user = await ctx.Guild.GetUserAsync(playlist.AuthorId);
            components.WithContainer(new TextDisplayBuilder(Strings.MusicPlaylistEntry(
                ctx.Guild.Id,
                playlist.Name,
                playlist.MusicPlaylistTracks.Count(),
                user?.Username ?? "Unknown"
            )));
        }

        await ctx.Channel.SendMessageAsync(components: components.Build(),
            flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a saved playlist.
    /// </summary>
    /// <param name="name">The name of the playlist to remove.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task DeletePlaylist([Remainder] string name)
    {
        var success = await cache.DeletePlaylist(ctx.Guild.Id, name);
        if (success)
            await ReplyConfirmAsync(Strings.MusicPlaylistDeleted(ctx.Guild.Id, name)).ConfigureAwait(false);
        else
            await ReplyErrorAsync(Strings.MusicPlaylistNotFound(ctx.Guild.Id, name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Searches for tracks without automatically playing them.
    /// </summary>
    /// <param name="query">The search query</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Search([Remainder] string query)
    {
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var tracks = await service.Tracks.LoadTracksAsync(query, TrackSearchMode.YouTube);

        if (!tracks.IsSuccess)
        {
            await ReplyErrorAsync(Strings.MusicNoTracks(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var trackList = tracks.Tracks.Take(10).ToList();
        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {Strings.MusicSearchResults(ctx.Guild.Id)}")
            ], Mewdeko.OkColor)
            .WithSeparator();

        // Add each track as a section for better organization
        for (var i = 0; i < trackList.Count; i++)
        {
            var track = trackList[i];
            components.WithContainer(new TextDisplayBuilder($"`{i + 1}.` [{track.Title}]({track.Uri})\n" +
                                                            $"⏱️ Duration: `{track.Duration}`\n" +
                                                            $"👤 Artist: {track.Author}"));
        }

        components.WithSeparator()
            .WithContainer(new TextDisplayBuilder($"ℹ️ {Strings.MusicSearchUsePlay(ctx.Guild.Id)}"));

        await ctx.Channel.SendMessageAsync(components: components.Build(),
            flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None);
    }

    /// <summary>
    ///     Shuffles the current music queue.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Shuffle()
    {
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (_, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (queue.Count <= 1)
        {
            await ReplyErrorAsync(Strings.MusicQueueTooShort(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var currentTrack = await cache.GetCurrentTrack(ctx.Guild.Id);
        var remainingTracks = queue.Where(x => x.Index != currentTrack.Index).ToList();

        // Fisher-Yates shuffle
        var rng = new Random();
        var n = remainingTracks.Count;
        while (n > 1)
        {
            n--;
            var k = rng.Next(n + 1);
            (remainingTracks[k], remainingTracks[n]) = (remainingTracks[n], remainingTracks[k]);
        }

        // Reassign indices
        for (var i = 0; i < remainingTracks.Count; i++)
        {
            remainingTracks[i].Index = i + 1;
        }

        var newQueue = new List<MewdekoTrack>
        {
            currentTrack
        };
        newQueue.AddRange(remainingTracks);

        await cache.SetMusicQueue(ctx.Guild.Id, newQueue);
        await ReplyConfirmAsync(Strings.MusicQueueShuffled(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Skips to the next track.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Skip()
    {
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        if (player.CurrentItem is null)
        {
            await ReplyErrorAsync(Strings.MusicNoCurrentTrack(ctx.Guild.Id)).ConfigureAwait(false);
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
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (_, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var errorComponents = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: errorComponents.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var currentTrack = await cache.GetCurrentTrack(ctx.Guild.Id);
        var orderedQueue = queue.OrderBy(x => x.Index).ToList();
        const int tracksPerPage = 5;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)orderedQueue.Count / tracksPerPage));

        var paginator = new ComponentPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(GeneratePage)
            .WithPageCount(totalPages)
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(10));

        IPage GeneratePage(IComponentPaginator p)
        {
            var startIndex = p.CurrentPageIndex * tracksPerPage;
            var tracksOnPage = orderedQueue.Skip(startIndex).Take(tracksPerPage).ToList();

            var containerComponents = new List<IMessageComponentBuilder>();

            // Add title
            containerComponents.Add(new TextDisplayBuilder()
                .WithContent($"# {Strings.MusicQueueTitle(ctx.Guild.Id, queue.Count)}"));

            containerComponents.Add(new SeparatorBuilder());

            // Add currently playing track if it's on this page or it's the first page
            if (currentTrack != null &&
                (p.CurrentPageIndex == 0 || tracksOnPage.Any(t => t.Index == currentTrack.Index)))
            {
                var nowPlayingSection = new SectionBuilder()
                    .WithComponents([
                        new TextDisplayBuilder($"**Now Playing**\n" +
                                               $"**{currentTrack.Index}. [{currentTrack.Track.Title}]({currentTrack.Track.Uri})**\n" +
                                               $"`{currentTrack.Track.Duration} | {currentTrack.Requester.Username} | {currentTrack.Track.Provider}`")
                    ]);

                if (currentTrack.Track.ArtworkUri != null)
                {
                    var thumbnailBuilder = new ThumbnailBuilder()
                        .WithMedia(new UnfurledMediaItemProperties
                        {
                            Url = currentTrack.Track.ArtworkUri.ToString()
                        });
                    nowPlayingSection.WithAccessory(thumbnailBuilder);
                }

                containerComponents.Add(nowPlayingSection);
                containerComponents.Add(new SeparatorBuilder());
            }

            // Add tracks for this page
            var upcomingTracks = tracksOnPage.Where(t => t.Index != currentTrack?.Index).ToList();
            if (upcomingTracks.Any())
            {
                containerComponents.Add(new TextDisplayBuilder().WithContent("**Upcoming Tracks**"));

                foreach (var track in upcomingTracks)
                {
                    var trackSection = new SectionBuilder()
                        .WithComponents([
                            new TextDisplayBuilder($"{track.Index}. [{track.Track.Title}]({track.Track.Uri})\n" +
                                                   $"`{track.Track.Duration} | {track.Requester.Username} | {track.Track.Provider}`")
                        ]);

                    if (track.Track.ArtworkUri != null)
                    {
                        var thumbnailBuilder = new ThumbnailBuilder()
                            .WithMedia(new UnfurledMediaItemProperties
                            {
                                Url = track.Track.ArtworkUri.ToString()
                            });
                        trackSection.WithAccessory(thumbnailBuilder);
                    }

                    containerComponents.Add(trackSection);

                    if (track != upcomingTracks.Last())
                        containerComponents.Add(new SeparatorBuilder());
                }
            }

            containerComponents.Add(new SeparatorBuilder());

            // Create select menu with current page tracks
            if (tracksOnPage.Any())
            {
                var selectOptions = tracksOnPage.Select(track =>
                    new SelectMenuOptionBuilder()
                        .WithLabel(
                            $"{track.Index}. {(track.Track.Title.Length > 80 ? track.Track.Title[..77] + "..." : track.Track.Title)}")
                        .WithValue($"music_track_info:{track.Index}")
                        .WithDescription(Strings.MusicTrackInfo(Context.Guild.Id, track.Track.Duration,
                            track.Requester.Username))
                ).ToList();

                var selectMenuRow = new ActionRowBuilder()
                    .WithSelectMenu("music_track_select", selectOptions, "Select a track for options...",
                        disabled: p.ShouldDisable());

                containerComponents.Add(selectMenuRow);
            }

            // Create navigation row
            var navigationRow = new ActionRowBuilder()
                .AddPreviousButton(p, style: ButtonStyle.Secondary)
                .AddNextButton(p, style: ButtonStyle.Secondary)
                .AddStopButton(p);

            containerComponents.Add(navigationRow);

            // Add footer
            containerComponents.Add(new TextDisplayBuilder()
                .WithContent($"Page {p.CurrentPageIndex + 1}/{p.PageCount} • {queue.Count} tracks in queue"));

            // Create the main container
            var mainContainer = new ContainerBuilder()
                .WithComponents(containerComponents)
                .WithAccentColor(Mewdeko.OkColor);

            var componentsV2 = new ComponentBuilderV2()
                .AddComponent(mainContainer);

            return new PageBuilder()
                .WithComponents(componentsV2.Build())
                .Build();
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
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        if (amount is < 0 or > 5)
        {
            await ReplyErrorAsync(Strings.AutoplayDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await player.SetAutoPlay(amount).ConfigureAwait(false);
        if (amount == 0)
        {
            await ReplyConfirmAsync(Strings.AutoplayDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.MusicAutoplaySet(ctx.Guild.Id, amount)).ConfigureAwait(false);
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
            var errorComponents = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: errorComponents.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        var volume = await player.GetVolume();
        var autoplay = await player.GetAutoPlay();
        var repeat = await player.GetRepeatType();
        var musicChannel = await player.GetMusicChannel();

        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {Strings.MusicSettings(ctx.Guild.Id)}")
            ], Mewdeko.OkColor)
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(
                $"**🔄 Autoplay**\n{(autoplay == 0 ? Strings.MusicsettingsAutoplayDisabled(ctx.Guild.Id) : Strings.MusicsettingsAutoplay(ctx.Guild.Id, autoplay))}"))
            .WithContainer(
                new TextDisplayBuilder($"**🔊 Volume**\n{Strings.MusicsettingsVolume(ctx.Guild.Id, volume)}"))
            .WithContainer(
                new TextDisplayBuilder($"**🔁 Repeat Mode**\n{Strings.MusicsettingsRepeat(ctx.Guild.Id, repeat)}"))
            .WithContainer(new TextDisplayBuilder(
                $"**📺 Music Channel**\n{(musicChannel == null ? Strings.UnsetMusicChannel(ctx.Guild.Id) : Strings.MusicsettingsChannel(ctx.Guild.Id, musicChannel.Id))}"));

        await ctx.Channel.SendMessageAsync(components: components.Build(),
            flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
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
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        await player.SetMusicChannelAsync(channelToUse.Id).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicChannelSet(ctx.Guild.Id, channelToUse.Id)).ConfigureAwait(false);
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
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        await player.SetRepeatTypeAsync(repeatType).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicRepeatType(ctx.Guild.Id, repeatType)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Links the user's Last.fm account for scrobbling.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task LastFmLink()
    {
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var authUrl =
            $"https://www.last.fm/api/auth/?api_key={creds.LastFmApiKey}&cb={creds.DashboardUrl}/api/lastfm/callback";
        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {Strings.LastfmLinkTitle(ctx.Guild.Id)}")
            ], Mewdeko.OkColor)
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(Strings.LastfmLinkInstructions(ctx.Guild.Id)))
            .WithSeparator()
            .WithContainer(new ActionRowBuilder()
                .WithButton(Strings.LastfmLinkButton(ctx.Guild.Id),
                    url: authUrl,
                    style: ButtonStyle.Link));

        await ctx.Channel.SendMessageAsync(components: components.Build(),
            flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    /// <summary>
    ///     Unlinks the user's Last.fm account.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task LastFmUnlink()
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var userId = ctx.User.Id;

        var deleted = await db.GetTable<LastFmUser>()
            .Where(x => x.UserId == userId)
            .DeleteAsync();

        if (deleted > 0)
        {
            await ReplyConfirmAsync(Strings.LastfmUnlinked(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            var prefix = await guildSettingsService.GetPrefix(ctx.Guild);
            await ReplyErrorAsync(Strings.LastfmNotLinked(ctx.Guild.Id, prefix)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Toggles Last.fm scrobbling on or off.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task LastFmToggle()
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var userId = ctx.User.Id;

        var lastFmUser = await db.GetTable<LastFmUser>()
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (lastFmUser == null)
        {
            var prefix = await guildSettingsService.GetPrefix(ctx.Guild);
            await ReplyErrorAsync(Strings.LastfmNotLinked(ctx.Guild.Id, prefix)).ConfigureAwait(false);
            return;
        }

        lastFmUser.ScrobblingEnabled = !lastFmUser.ScrobblingEnabled;
        await db.UpdateAsync(lastFmUser);

        if (lastFmUser.ScrobblingEnabled)
        {
            await ReplyConfirmAsync(Strings.LastfmScrobblingEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.LastfmScrobblingDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Shows the user's Last.fm account status.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task LastFmStatus()
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var userId = ctx.User.Id;

        var lastFmUser = await db.GetTable<LastFmUser>()
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (lastFmUser == null)
        {
            var prefix = await guildSettingsService.GetPrefix(ctx.Guild);
            await ReplyErrorAsync(Strings.LastfmNotLinked(ctx.Guild.Id, prefix)).ConfigureAwait(false);
            return;
        }

        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {Strings.LastfmStatusTitle(ctx.Guild.Id)}")
            ], Mewdeko.OkColor)
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(
                Strings.LastfmStatusInfo(ctx.Guild.Id, lastFmUser.Username,
                    lastFmUser.ScrobblingEnabled
                        ? Strings.LastfmEnabled(ctx.Guild.Id)
                        : Strings.LastfmDisabled(ctx.Guild.Id))));

        await ctx.Channel.SendMessageAsync(components: components.Build(),
            flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles playing music from URLs (YouTube, Spotify, SoundCloud, etc.)
    /// </summary>
    private async Task HandleUrlPlay(Uri uri, List<MewdekoTrack> queue, MewdekoPlayer player)
    {
        var url = uri.ToString();

        try
        {
            List<LavalinkTrack> tracks;
            if (url.Contains("spotify.com"))
            {
                var spotify = await player.GetSpotifyClient();
                tracks = await ProcessSpotifyUrl(url, spotify);

                if (!tracks.Any())
                {
                    await ReplyErrorAsync(Strings.MusicSpotifyProcessingError(ctx.Guild.Id));
                    return;
                }
            }
            else
            {
                var options = new TrackLoadOptions
                {
                    SearchMode = GetSearchMode(url)
                };

                var trackResults = await service.Tracks.LoadTracksAsync(url, options);
                if (!trackResults.IsSuccess)
                {
                    await ReplyErrorAsync(Strings.MusicSearchFail(ctx.Guild.Id));
                    return;
                }

                tracks = trackResults.Tracks.ToList();
            }

            await AddTracksToQueue(tracks, queue, player);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing URL: {Url}", url);
            await ReplyErrorAsync(Strings.MusicUrlProcessError(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Handles playing music from search queries
    /// </summary>
    private async Task HandleSearchPlay(string query)
    {
        try
        {
            var tracks = await service.Tracks.LoadTracksAsync(query, TrackSearchMode.YouTube);

            if (!tracks.IsSuccess)
            {
                await ReplyErrorAsync(Strings.MusicNoTracks(ctx.Guild.Id));
                return;
            }

            var trackList = tracks.Tracks.Take(25).ToList();
            var selectMenu = new SelectMenuBuilder()
                .WithCustomId($"track_select:{ctx.User.Id}")
                .WithPlaceholder(Strings.MusicSelectTracks(ctx.Guild.Id))
                .WithMaxValues(trackList.Count)
                .WithMinValues(1);

            foreach (var track in trackList)
            {
                var index = trackList.IndexOf(track);
                selectMenu.AddOption(track.Title.Truncate(100), $"track_{index}");
            }

            var eb = new EmbedBuilder()
                .WithDescription(Strings.MusicSelectTracksEmbed(ctx.Guild.Id))
                .WithOkColor()
                .Build();

            var components = new ComponentBuilder().WithSelectMenu(selectMenu).Build();

            var message = await ctx.Channel.SendMessageAsync(embed: eb, components: components,
                allowedMentions: AllowedMentions.None);

            // Cache the track list for the selection menu handler
            await cache.Redis.GetDatabase().StringSetAsync(
                $"{ctx.User.Id}_{message.Id}_tracks",
                JsonSerializer.Serialize(trackList),
                TimeSpan.FromMinutes(5)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing search query: {Query}", query);
            await ReplyErrorAsync(Strings.MusicSearchError(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Processes and adds tracks to the queue
    /// </summary>
    private async Task AddTracksToQueue(List<LavalinkTrack> tracks, List<MewdekoTrack> queue, MewdekoPlayer player)
    {
        if (!tracks.Any()) return;

        var startIndex = queue.Count + 1;
        var addedTracks = new List<MewdekoTrack>();

        foreach (var mewdekoTrack in tracks.Select(track => new MewdekoTrack(startIndex++, track, new PartialUser
                 {
                     Id = ctx.User.Id, Username = ctx.User.Username, AvatarUrl = ctx.User.GetAvatarUrl()
                 })))
        {
            queue.Add(mewdekoTrack);
            addedTracks.Add(mewdekoTrack);
        }

        await cache.SetMusicQueue(ctx.Guild.Id, queue);

        // Start playback if nothing is currently playing
        if (player.CurrentItem is null && queue.Any())
        {
            await player.PlayAsync(queue[0].Track);
            await cache.SetCurrentTrack(ctx.Guild.Id, queue[0]);
        }

        // Create response components
        var containerComponents = new List<IMessageComponentBuilder>();

        containerComponents.Add(new TextDisplayBuilder()
            .WithContent($"# {Strings.MusicAddedTitle(ctx.Guild.Id)}"));

        containerComponents.Add(new SeparatorBuilder());

        if (addedTracks.Count == 1)
        {
            var track = addedTracks[0];
            var trackSection = new SectionBuilder()
                .WithComponents([
                    new TextDisplayBuilder($"### [{track.Track.Title}]({track.Track.Uri})\n" +
                                           $"**Artist:** {track.Track.Author}\n" +
                                           $"**Duration:** {track.Track.Duration}\n" +
                                           $"**Position in queue:** {track.Index}")
                ]);

            if (track.Track.ArtworkUri != null)
            {
                var thumbnailBuilder = new ThumbnailBuilder()
                    .WithMedia(new UnfurledMediaItemProperties
                    {
                        Url = track.Track.ArtworkUri.ToString()
                    });
                trackSection.WithAccessory(thumbnailBuilder);
            }

            containerComponents.Add(trackSection);
        }
        else
        {
            containerComponents.Add(new TextDisplayBuilder()
                .WithContent($"**{addedTracks.Count} tracks added**\n" +
                             $"Queue positions: {addedTracks[0].Index} - {addedTracks[^1].Index}"));

            containerComponents.Add(new SeparatorBuilder());

            // Show first few tracks with artwork in a media gallery if available
            var tracksWithArtwork = addedTracks.Where(t => t.Track.ArtworkUri != null).Take(4).ToList();
            if (tracksWithArtwork.Any())
            {
                var mediaItems = tracksWithArtwork.Select(track =>
                    new MediaGalleryItemProperties(
                        new UnfurledMediaItemProperties
                        {
                            Url = track.Track.ArtworkUri.ToString()
                        },
                        $"{track.Track.Title} - {track.Track.Author}"
                    )).ToList();

                var mediaGallery = new MediaGalleryBuilder()
                    .WithItems(mediaItems);

                containerComponents.Add(mediaGallery);
            }

            // Add summary of added tracks
            var trackList = string.Join("\n", addedTracks.Take(5).Select(t =>
                $"**{t.Index}.** {(t.Track.Title.Length > 50 ? t.Track.Title[..47] + "..." : t.Track.Title)}"));

            if (addedTracks.Count > 5)
                trackList += $"\n*...and {addedTracks.Count - 5} more tracks*";

            containerComponents.Add(new TextDisplayBuilder()
                .WithContent(trackList));
        }

        // Create the main container
        var mainContainer = new ContainerBuilder()
            .WithComponents(containerComponents)
            .WithAccentColor(Mewdeko.OkColor);

        var componentsV2 = new ComponentBuilderV2()
            .AddComponent(mainContainer);

        await ctx.Channel.SendMessageAsync(components: componentsV2.Build(),
            flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None);
    }

    /// <summary>
    ///     Determines the appropriate search mode based on the URL
    /// </summary>
    private static TrackSearchMode GetSearchMode(string url)
    {
        return url switch
        {
            var u when u.Contains("music.youtube") => TrackSearchMode.YouTubeMusic,
            var u when u.Contains("youtube.com") || u.Contains("youtu.be") => TrackSearchMode.YouTube,
            var u when u.Contains("soundcloud.com") => TrackSearchMode.SoundCloud,
            _ => TrackSearchMode.None
        };
    }

    /// <summary>
    ///     Processes Spotify URLs and converts them to playable tracks
    /// </summary>
    private async Task<List<LavalinkTrack>> ProcessSpotifyUrl(string url, SpotifyClient spotify)
    {
        var tracks = new List<LavalinkTrack>();
        var currentQueue = await cache.GetMusicQueue(Context.Guild.Id);
        var currentTrack = await cache.GetCurrentTrack(Context.Guild.Id);
        var (player, _) = await GetPlayerAsync();
        var shouldPlayFirst = currentQueue.Count == 0 && currentTrack == null && player.CurrentItem == null;

        try
        {
            if (url.Contains("/track/"))
            {
                var id = url.Split("/track/")[1].Split("?")[0];
                var track = await spotify.Tracks.Get(id);
                var searchQuery = $"{track.Name} {string.Join(" ", track.Artists.Select(a => a.Name))}";
                var ytTrack = await service.Tracks.LoadTrackAsync(searchQuery, TrackSearchMode.YouTube);
                if (ytTrack != null) tracks.Add(ytTrack);
            }
            else if (url.Contains("/album/"))
            {
                var id = url.Split("/album/")[1].Split("?")[0];
                var album = await spotify.Albums.Get(id);

                // Show loading message for long albums
                IUserMessage loadingMsg = null;
                if (album.Tracks.Total > 10)
                {
                    var loadingComponents = new ComponentBuilderV2()
                        .WithContainer([
                            new TextDisplayBuilder($"# {Strings.MusicAlbumTitle(ctx.Guild.Id, album.Name)}")
                        ], new Color(30, 215, 96))
                        .WithSeparator()
                        .WithSection([
                                new TextDisplayBuilder(Strings.LoadingAlbum(ctx.Guild.Id, Config.LoadingEmote,
                                    album.Tracks.Total,
                                    album.Artists.FirstOrDefault()?.Name ?? "Unknown"))
                            ],
                            album.Images.FirstOrDefault()?.Url != null
                                ? new ThumbnailBuilder(album.Images.FirstOrDefault().Url)
                                : null)
                        .WithSeparator()
                        .WithContainer(new TextDisplayBuilder(
                            $"ℹ️ {Strings.MusicProcessingTracks(ctx.Guild.Id, tracks.Count, album.Tracks.Total)}"));

                    loadingMsg = await ctx.Channel.SendMessageAsync(components: loadingComponents.Build(),
                        flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None);
                }

                foreach (var searchQuery in album.Tracks.Items.Select(track =>
                             $"{track.Name} {string.Join(" ", track.Artists.Select(a => a.Name))}"))
                {
                    var ytTrack = await service.Tracks.LoadTrackAsync(searchQuery, TrackSearchMode.YouTube);
                    if (ytTrack == null) continue;
                    tracks.Add(ytTrack);

                    // Play first track immediately if queue is empty
                    if (shouldPlayFirst && tracks.Count == 1)
                    {
                        var mewdekoTrack = new MewdekoTrack(1, ytTrack, new PartialUser
                        {
                            Id = Context.User.Id,
                            Username = Context.User.Username,
                            AvatarUrl = Context.User.GetAvatarUrl()
                        });
                        await cache.SetCurrentTrack(Context.Guild.Id, mewdekoTrack);
                        await player.PlayAsync(ytTrack);

                        if (loadingMsg != null)
                        {
                            var updatedEmbed = loadingMsg.Embeds.First().ToEmbedBuilder()
                                .WithDescription(
                                    Strings.LoadingPlaylistWithTrack(ctx.Guild.Id, album.Tracks.Total,
                                        album.Artists.FirstOrDefault()?.Name ?? "Unknown", ytTrack.Title))
                                .Build();
                            await loadingMsg.ModifyAsync(x => x.Embed = updatedEmbed);
                        }
                    }

                    // Update loading message every 5 tracks
                    if (loadingMsg == null || tracks.Count % 5 != 0) continue;
                    {
                        var updatedEmbed = loadingMsg.Embeds.First().ToEmbedBuilder()
                            .WithFooter(Strings.MusicProcessingTracks(ctx.Guild.Id, tracks.Count, album.Tracks.Total))
                            .Build();
                        await loadingMsg.ModifyAsync(x => x.Embed = updatedEmbed);
                    }
                }

                if (loadingMsg != null) await loadingMsg.DeleteAsync();
            }
            else if (url.Contains("/playlist/"))
            {
                var id = url.Split("/playlist/")[1].Split("?")[0];
                var playlist = await spotify.Playlists.Get(id);

                // Show loading message for long playlists
                IUserMessage loadingMsg = null;
                if (playlist.Tracks.Total > 10)
                {
                    var loadingComponents = new ComponentBuilderV2()
                        .WithContainer([
                            new TextDisplayBuilder($"# {playlist.Name}")
                        ], new Color(30, 215, 96))
                        .WithSeparator()
                        .WithSection([
                                new TextDisplayBuilder(Strings.LoadingPlaylist(ctx.Guild.Id, playlist.Tracks.Total,
                                    playlist.Owner.DisplayName))
                            ],
                            playlist.Images.FirstOrDefault()?.Url != null
                                ? new ThumbnailBuilder(playlist.Images.FirstOrDefault().Url)
                                : null)
                        .WithSeparator()
                        .WithContainer(new TextDisplayBuilder(
                            $"ℹ️ {Strings.MusicProcessingTracks(ctx.Guild.Id, tracks.Count, playlist.Tracks.Total)}"));

                    loadingMsg = await ctx.Channel.SendMessageAsync(components: loadingComponents.Build(),
                        flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None);
                }

                foreach (var item in playlist.Tracks.Items)
                {
                    if (item.Track is not FullTrack track) continue;

                    var searchQuery = $"{track.Name} {string.Join(" ", track.Artists.Select(a => a.Name))}";
                    var ytTrack = await service.Tracks.LoadTrackAsync(searchQuery, TrackSearchMode.YouTube);
                    if (ytTrack != null)
                    {
                        tracks.Add(ytTrack);

                        // Play first track immediately if queue is empty
                        if (shouldPlayFirst && tracks.Count == 1)
                        {
                            var mewdekoTrack = new MewdekoTrack(1, ytTrack, new PartialUser
                            {
                                Id = Context.User.Id,
                                Username = Context.User.Username,
                                AvatarUrl = Context.User.GetAvatarUrl()
                            });
                            await cache.SetCurrentTrack(Context.Guild.Id, mewdekoTrack);
                            await player.PlayAsync(ytTrack);

                            if (loadingMsg != null)
                            {
                                var updatedEmbed = loadingMsg.Embeds.First().ToEmbedBuilder()
                                    .WithDescription(
                                        Strings.LoadingPlaylistWithTrack(ctx.Guild.Id, playlist.Tracks.Total,
                                            playlist.Owner.DisplayName, ytTrack.Title))
                                    .Build();
                                await loadingMsg.ModifyAsync(x => x.Embed = updatedEmbed);
                            }
                        }

                        // Update loading message every 5 tracks
                        if (loadingMsg != null && tracks.Count % 5 == 0)
                        {
                            var updatedEmbed = loadingMsg.Embeds.First().ToEmbedBuilder()
                                .WithFooter(Strings.MusicProcessingTracks(ctx.Guild.Id, tracks.Count,
                                    playlist.Tracks.Total))
                                .Build();
                            await loadingMsg.ModifyAsync(x => x.Embed = updatedEmbed);
                        }
                    }
                }

                if (loadingMsg != null) await loadingMsg.DeleteAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing Spotify URL: {Url}", url);
            throw; // Rethrow to be handled by caller
        }

        return tracks;
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

            if (result.IsSuccess)
            {
                // Publish Redis event when player is created (bot joins voice)
                if (connectToVoiceChannel && result.Status == PlayerRetrieveStatus.Success)
                {
                    var key = new TypedKey<string>($"music:player:created:{ctx.Guild.Id}");
                    await pubSub.Pub(key, "created");
                    logger.LogInformation("Published player created event for guild {GuildId}", ctx.Guild.Id);
                }

                return (result.Player, null);
            }

            var errorMessage = result.Status switch
            {
                PlayerRetrieveStatus.UserNotInVoiceChannel => Strings.MusicNotInChannel(ctx.Guild.Id),
                PlayerRetrieveStatus.BotNotConnected => Strings.MusicBotNotConnect(ctx.Guild.Id,
                    await guildSettingsService.GetPrefix(ctx.Guild)),
                PlayerRetrieveStatus.VoiceChannelMismatch => Strings.MusicVoiceChannelMismatch(ctx.Guild.Id),
                PlayerRetrieveStatus.Success => null,
                PlayerRetrieveStatus.UserInSameVoiceChannel => null,
                PlayerRetrieveStatus.PreconditionFailed => null,
                _ => throw new ArgumentOutOfRangeException()
            };
            return (null, errorMessage);
        }
        catch (TimeoutException)
        {
            return (null, Strings.MusicLavalinkDisconnected(ctx.Guild.Id));
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