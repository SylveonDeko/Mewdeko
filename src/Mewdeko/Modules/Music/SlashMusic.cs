using System.Text;
using System.Text.Json;
using System.Threading;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Music.CustomPlayer;
using Mewdeko.Modules.Music.Services;
using SpotifyAPI.Web;
using Swan;

namespace Mewdeko.Modules.Music;

/// <summary>
///     Slash commands module containing music commands.
/// </summary>
[Group("music", "Music commands")]
public class SlashMusic(
    IAudioService service,
    IDataCache cache,
    InteractiveService interactiveService,
    GuildSettingsService guildSettingsService,
    ILogger<SlashMusic> logger,
    MusicEventManager eventManager,
    MusicLinkService musicLinkService) : MewdekoSlashCommandModule
{
    /// <summary>
    ///     Joins the voice channel.
    /// </summary>
    [SlashCommand("join", "Joins your voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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

            await Context.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Disconnects the bot from the voice channel.
    /// </summary>
    [SlashCommand("leave", "Disconnects the bot from the voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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

            await Context.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        await cache.SetMusicQueue(Context.Guild.Id, []).ConfigureAwait(false);
        await cache.SetCurrentTrack(Context.Guild.Id, null);

        await player.DisconnectAsync().ConfigureAwait(false);

        // Notify connected clients that bot has disconnected
        await eventManager.BroadcastDisconnection(Context.Guild.Id).ConfigureAwait(false);

        await ReplyConfirmAsync(Strings.MusicDisconnect(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears the music queue.
    /// </summary>
    [SlashCommand("clearqueue", "Clears the music queue")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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

            await Context.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        await cache.SetMusicQueue(Context.Guild.Id, []).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicQueueCleared(ctx.Guild.Id)).ConfigureAwait(false);
        await player.StopAsync();
        await cache.SetCurrentTrack(Context.Guild.Id, null);
    }

    /// <summary>
    ///     Plays a specified track in the current voice channel.
    /// </summary>
    /// <param name="queueNumber">The queue number to play.</param>
    [SlashCommand("playnumber", "Plays a song from the queue by its number")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task PlayNumber(int queueNumber)
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

            await Context.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(Context.Guild.Id);
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
        await cache.SetCurrentTrack(Context.Guild.Id, trackToPlay);
    }

    /// <summary>
    ///     Plays music from various sources including YouTube, Spotify, and direct searches.
    ///     Supports tracks, playlists, and albums from supported platforms.
    /// </summary>
    /// <param name="query">URL or search query for the music to play</param>
    [SlashCommand("play", "Plays music from various sources")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Play(string query)
    {
        var user = ctx.User as IGuildUser;
        if (user.VoiceChannel is null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync();

        try
        {
            var (player, result) = await GetPlayerAsync();
            if (result is not null)
            {
                var components = new ComponentBuilderV2()
                    .WithContainer([
                        new TextDisplayBuilder($"# {Strings.MusicPlayerError(Context.Guild.Id)}")
                    ], Mewdeko.ErrorColor)
                    .WithSeparator()
                    .WithContainer(new TextDisplayBuilder(result));

                await FollowupAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                    allowedMentions: AllowedMentions.None);
                return;
            }

            await player.SetVolumeAsync(await player.GetVolume() / 100f);
            var queue = await cache.GetMusicQueue(Context.Guild.Id);

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
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Config.ErrorEmote} {Strings.MusicErrorTitle(Context.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(Strings.MusicGenericError(Context.Guild.Id)));

            await FollowupAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None);
        }
    }

    /// <summary>
    ///     Pauses or resumes the player based on the current state.
    /// </summary>
    [SlashCommand("pause", "Pauses or resumes the player")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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

            await Context.Channel.SendMessageAsync(components: components.Build(),
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
    ///     Displays the currently playing track.
    /// </summary>
    [SlashCommand("nowplaying", "Displays the currently playing track")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                    .WithDescription(result);

                await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                return;
            }

            var queue = await cache.GetMusicQueue(Context.Guild.Id);

            if (queue.Count == 0)
            {
                await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var components = await player.PrettyNowPlayingAsync(queue);
            await Context.Channel.SendMessageAsync(components: components, flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            logger.LogError("Failed to get now playing track: {Message}", e.Message);
        }
    }

    /// <summary>
    ///     Removes a song from the queue by its number.
    /// </summary>
    /// <param name="queueNumber">The queue number to remove.</param>
    [SlashCommand("removesong", "Removes a song from the queue by its number")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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

            await Context.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(Context.Guild.Id);
        var currentTrack = await cache.GetCurrentTrack(Context.Guild.Id);
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

        var trackToRemove = queue.FirstOrDefault(x => x.Index == queueNumber);
        if (trackToRemove == null)
        {
            await ReplyErrorAsync(Strings.MusicQueueInvalidIndex(ctx.Guild.Id, queue.Count)).ConfigureAwait(false);
            return;
        }

        queue.Remove(trackToRemove);

        if (currentTrack.Index == trackToRemove.Index)
        {
            if (nextTrack != null)
            {
                await player.StopAsync();
                await player.PlayAsync(nextTrack.Track);
                await cache.SetCurrentTrack(Context.Guild.Id, nextTrack);
            }
            else
            {
                await player.StopAsync();
                await cache.SetCurrentTrack(Context.Guild.Id, null);
            }
        }

        await cache.SetMusicQueue(Context.Guild.Id, queue);

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
    [SlashCommand("movesong", "Moves a song in the queue to a new position")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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

            await Context.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(Context.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (from < 1 || from > queue.Count || to < 1 || to > queue.Count)
        {
            await ReplyErrorAsync(Strings.MusicQueueInvalidIndex(ctx.Guild.Id, queue.Count)).ConfigureAwait(false);
            return;
        }

        var track = queue.FirstOrDefault(x => x.Index == from);
        queue.Remove(track);
        queue.Insert(to - 1, track);

        for (var i = 0; i < queue.Count; i++)
        {
            queue[i].Index = i + 1;
        }

        var currentSong = await cache.GetCurrentTrack(Context.Guild.Id);
        if (currentSong.Index == from)
        {
            await cache.SetCurrentTrack(Context.Guild.Id, track);
        }

        await cache.SetMusicQueue(Context.Guild.Id, queue);
        await ReplyConfirmAsync(Strings.MusicSongMoved(ctx.Guild.Id, track.Track.Title, to)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the player's volume.
    /// </summary>
    /// <param name="volume">The volume to set (0-100).</param>
    [SlashCommand("volume", "Sets the player's volume")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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

            await Context.Channel.SendMessageAsync(components: components.Build(),
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
    ///     Skips the current track.
    /// </summary>
    [SlashCommand("skip", "Skips the current track")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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

            await Context.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        if (player.CurrentItem is null)
        {
            await ReplyErrorAsync(Strings.MusicNoCurrentTrack(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await player.SeekAsync(player.CurrentItem.Track.Duration).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.SkippedTo(ctx.Guild.Id, player.CurrentTrack.Author, player.CurrentTrack.Title))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Displays the current music queue.
    /// </summary>
    [SlashCommand("queue", "Displays the current music queue")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await Context.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(Context.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var currentTrack = await cache.GetCurrentTrack(Context.Guild.Id);
        var orderedQueue = queue.OrderBy(x => x.Index).ToList();
        const int tracksPerPage = 5;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)orderedQueue.Count / tracksPerPage));

        var paginator = new ComponentPaginatorBuilder()
            .AddUser(Context.User)
            .WithPageFactory(GeneratePage)
            .WithPageCount(totalPages)
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(10));

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
    ///     Sets the autoplay amount in the guild.
    /// </summary>
    /// <param name="amount">The amount of tracks to autoplay (max 5).</param>
    [SlashCommand("autoplay", "Sets the autoplay amount")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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

            await Context.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        if (amount is < 0 or > 5)
        {
            await ReplyErrorAsync(Strings.MusicAutoPlayInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await player.SetAutoPlay(amount).ConfigureAwait(false);
        if (amount == 0)
        {
            await ReplyConfirmAsync(Strings.MusicAutoPlayDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.MusicAutoplaySet(ctx.Guild.Id, amount)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Displays the guild's current music settings.
    /// </summary>
    [SlashCommand("musicsettings", "Displays the current music settings")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task MusicSettings()
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

            await Context.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        var volume = await player.GetVolume();
        var autoplay = await player.GetAutoPlay();
        var repeat = await player.GetRepeatType();
        var musicChannel = await player.GetMusicChannel();

        var toSend = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.MusicSettings(ctx.Guild.Id))
            .WithDescription(
                $"{(autoplay == 0 ? Strings.MusicsettingsAutoplayDisabled(ctx.Guild.Id) : Strings.MusicsettingsAutoplay(ctx.Guild.Id, autoplay))}\n" +
                $"{Strings.MusicsettingsVolume(ctx.Guild.Id, volume)}\n" +
                $"{Strings.MusicsettingsRepeat(ctx.Guild.Id, repeat)}\n" +
                $"{(musicChannel == null ? Strings.UnsetMusicChannel(ctx.Guild.Id) : Strings.MusicsettingsChannel(ctx.Guild.Id, musicChannel.Id))}");

        await Context.Channel.SendMessageAsync(embed: toSend.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the channel where music events will be sent.
    /// </summary>
    /// <param name="channel">The channel where music events will be sent.</param>
    [SlashCommand("setmusicchannel", "Sets the channel where music events will be sent")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task SetMusicChannel(ITextChannel channel = null)
    {
        var channelToUse = channel ?? Context.Channel;
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(result));

            await Context.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        await player.SetMusicChannelAsync(channelToUse.Id).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicChannelSet(ctx.Guild.Id, channelToUse.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles automatic music link conversion for a channel. When enabled, any Apple Music,
    ///     Spotify, YouTube Music, or other supported music link posted in that channel gets a reply
    ///     embed with the track's title, artist, artwork, and equivalent links across providers.
    /// </summary>
    /// <param name="channel">The channel to toggle. Defaults to the current channel.</param>
    [SlashCommand("musiclinkchannel", "Toggles automatic music link conversion for a channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task MusicLinkChannel(ITextChannel channel = null)
    {
        var channelToUse = channel ?? (ITextChannel)Context.Channel;
        var enabled = await musicLinkService.EnableChannelAsync(ctx.Guild.Id, channelToUse.Id);
        if (enabled)
        {
            await ReplyConfirmAsync(Strings.MusicLinkChannelOn(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await musicLinkService.DisableChannelAsync(ctx.Guild.Id, channelToUse.Id);
        await ReplyConfirmAsync(Strings.MusicLinkChannelOff(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists the channels in this server where music link conversion is enabled.
    /// </summary>
    [SlashCommand("musiclinkchannels", "Lists channels with music link conversion enabled")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task MusicLinkChannels()
    {
        var channelIds = await musicLinkService.GetChannelsAsync(ctx.Guild.Id);
        if (channelIds.Count == 0)
        {
            await ReplyConfirmAsync(Strings.MusicLinkChannelsNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var mentions = string.Join('\n', channelIds.Select(id => $"<#{id}>"));
        await ReplyConfirmAsync(Strings.MusicLinkChannelsList(ctx.Guild.Id, mentions)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the loop mode.
    /// </summary>
    /// <param name="repeatType">The repeat type.</param>
    [SlashCommand("loop", "Sets the loop mode")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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

            await Context.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        await player.SetRepeatTypeAsync(repeatType).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicRepeatType(ctx.Guild.Id, repeatType)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Handling track selection for the play command select menu.
    /// </summary>
    /// <param name="userId">The original user who summoned the select menu</param>
    /// <param name="selectedValue">The selected track.</param>
    [ComponentInteraction("track_select:*", true)]
    [CheckPermissions]
    public async Task TrackSelect(ulong userId, string[] selectedValue)
    {
        await DeferAsync();

        if (ctx.User.Id != userId) return;

        var componentInteraction = ctx.Interaction as IComponentInteraction;

        var (player, _) = await GetPlayerAsync(false);

        var tracks = await cache.Redis.GetDatabase()
            .StringGetAsync($"{ctx.User.Id}_{componentInteraction.Message.Id}_tracks");

        var trackList = JsonSerializer.Deserialize<List<LavalinkTrack>>((string)tracks);

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);

        var selectedTracks = selectedValue.Select(i => trackList[Convert.ToInt32(i.Split("_")[1])]).ToList();

        var startIndex = queue.Count + 1;
        queue.AddRange(
            selectedTracks.Select(track => new MewdekoTrack(startIndex++, track, new PartialUser
            {
                Id = ctx.User.Id, Username = ctx.User.Username, AvatarUrl = ctx.User.GetAvatarUrl()
            })));

        if (selectedTracks.Count == 1)
        {
            var eb = new EmbedBuilder()
                .WithAuthor(Strings.MusicAdded(ctx.Guild.Id))
                .WithDescription($"[{selectedTracks[0].Title}]({selectedTracks[0].Uri}) by {selectedTracks[0].Author}")
                .WithImageUrl(selectedTracks[0].ArtworkUri.ToString())
                .WithOkColor();

            await FollowupAsync(embed: eb.Build());
        }
        else
        {
            var paginator = new LazyPaginatorBuilder().AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(queue.Count / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactiveService.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(5),
                InteractionResponseType.DeferredChannelMessageWithSource);

            async Task<PageBuilder> PageFactory(int index)
            {
                await Task.CompletedTask;
                var tracks = queue.Skip(index * 10).Take(10).ToList();
                var sb = new StringBuilder();
                foreach (var track in tracks)
                {
                    sb.AppendLine($"{track.Index}. [{track.Track.Title}]({track.Track.Uri})");
                }

                return new PageBuilder()
                    .WithTitle(Strings.MusicQueueTitle(ctx.Guild.Id, queue.Count))
                    .WithDescription(sb.ToString())
                    .WithOkColor();
            }
        }

        if (player.CurrentItem == null)
        {
            await cache.SetCurrentTrack(ctx.Guild.Id,
                new MewdekoTrack(1, selectedTracks[0], new PartialUser
                {
                    Id = ctx.User.Id, Username = ctx.User.Username, AvatarUrl = ctx.User.GetAvatarUrl()
                }));
            await player.PlayAsync(selectedTracks[0]);
        }

        await cache.SetMusicQueue(ctx.Guild.Id, queue);
        await cache.Redis.GetDatabase().KeyDeleteAsync($"{ctx.User.Id}_{componentInteraction.Message.Id}_tracks");
        await ctx.Channel.DeleteMessageAsync(componentInteraction.Message.Id);
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
                    var components = new ComponentBuilderV2()
                        .WithContainer([
                            new TextDisplayBuilder(
                                $"# {Config.ErrorEmote} {Strings.MusicSpotifyErrorTitle(Context.Guild.Id)}")
                        ], Mewdeko.ErrorColor)
                        .WithSeparator()
                        .WithContainer(new TextDisplayBuilder(Strings.MusicSpotifyProcessingError(Context.Guild.Id)));

                    await FollowupAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                        allowedMentions: AllowedMentions.None);
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
                    await FollowupAsync(embed: new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription(Strings.MusicSearchFail(Context.Guild.Id))
                        .Build());
                    return;
                }

                tracks = trackResults.Tracks.ToList();
            }

            await AddTracksToQueue(tracks, queue, player);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing URL: {Url}", url);
            await FollowupAsync(embed: new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(Strings.MusicUrlProcessError(Context.Guild.Id))
                .Build());
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
                await FollowupAsync(embed: new EmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(Strings.MusicNoTracks(Context.Guild.Id))
                    .Build());
                return;
            }

            var trackList = tracks.Tracks.Take(25).ToList();
            var selectMenu = new SelectMenuBuilder()
                .WithCustomId($"track_select:{Context.User.Id}")
                .WithPlaceholder(Strings.MusicSelectTracks(Context.Guild.Id))
                .WithMaxValues(trackList.Count)
                .WithMinValues(1);

            foreach (var track in trackList)
            {
                var index = trackList.IndexOf(track);
                selectMenu.AddOption(track.Title.Truncate(100), $"track_{index}");
            }

            var eb = new EmbedBuilder()
                .WithDescription(Strings.MusicSelectTracksEmbed(Context.Guild.Id))
                .WithOkColor()
                .Build();

            var components = new ComponentBuilder().WithSelectMenu(selectMenu).Build();

            await FollowupAsync(embed: eb, components: components);

            // Cache the track list for the selection menu handler
            await cache.Redis.GetDatabase().StringSetAsync(
                $"{Context.User.Id}_{Context.Interaction.Id}_tracks",
                JsonSerializer.Serialize(trackList),
                TimeSpan.FromMinutes(5)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing search query: {Query}", query);
            await FollowupAsync(embed: new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(Strings.MusicSearchError(Context.Guild.Id))
                .Build());
        }
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
                if (album.Tracks.Total > 10)
                {
                    var loadingComponents = new ComponentBuilderV2()
                        .WithContainer([
                            new TextDisplayBuilder($"# {Strings.MusicAlbumTitle(ctx.Guild.Id, album.Name)}")
                        ], Mewdeko.OkColor)
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

                    await ModifyOriginalResponseAsync(x =>
                    {
                        x.Embed = null;
                        x.Components = loadingComponents.Build();
                        x.Flags = MessageFlags.ComponentsV2;
                    });
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

                        // Update components to show now playing
                        if (album.Tracks.Total > 10)
                        {
                            var updatedComponents = new ComponentBuilderV2()
                                .WithContainer([
                                    new TextDisplayBuilder($"# {Strings.MusicAlbumTitle(ctx.Guild.Id, album.Name)}")
                                ], Mewdeko.OkColor)
                                .WithSeparator()
                                .WithSection([
                                        new TextDisplayBuilder(Strings.LoadingPlaylistWithTrack(ctx.Guild.Id,
                                            album.Tracks.Total,
                                            album.Artists.FirstOrDefault()?.Name ?? "Unknown", ytTrack.Title))
                                    ],
                                    album.Images.FirstOrDefault()?.Url != null
                                        ? new ThumbnailBuilder(album.Images.FirstOrDefault().Url)
                                        : null)
                                .WithSeparator()
                                .WithContainer(new TextDisplayBuilder(
                                    $"ℹ️ {Strings.MusicProcessingTracks(ctx.Guild.Id, tracks.Count, album.Tracks.Total)}"));

                            await ModifyOriginalResponseAsync(x =>
                            {
                                x.Embed = null;
                                x.Components = updatedComponents.Build();
                                x.Flags = MessageFlags.ComponentsV2;
                            });
                        }
                    }

                    // Update loading message every 5 tracks
                    if (album.Tracks.Total > 10 && tracks.Count % 5 == 0)
                    {
                        var updatedComponents = new ComponentBuilderV2()
                            .WithContainer([
                                new TextDisplayBuilder($"# {Strings.MusicAlbumTitle(ctx.Guild.Id, album.Name)}")
                            ], Mewdeko.OkColor)
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

                        await ModifyOriginalResponseAsync(x =>
                        {
                            x.Embed = null;
                            x.Components = updatedComponents.Build();
                            x.Flags = MessageFlags.ComponentsV2;
                        });
                    }
                }
            }
            else if (url.Contains("/playlist/"))
            {
                var id = url.Split("/playlist/")[1].Split("?")[0];
                var playlist = await spotify.Playlists.Get(id);

                // Show loading message for long playlists
                if (playlist.Items.Total > 10)
                {
                    var loadingComponents = new ComponentBuilderV2()
                        .WithContainer([
                            new TextDisplayBuilder($"# {playlist.Name}")
                        ], Mewdeko.OkColor)
                        .WithSeparator()
                        .WithSection([
                                new TextDisplayBuilder(Strings.LoadingPlaylist(ctx.Guild.Id, playlist.Items.Total,
                                    playlist.Owner.DisplayName))
                            ],
                            playlist.Images.FirstOrDefault()?.Url != null
                                ? new ThumbnailBuilder(playlist.Images.FirstOrDefault().Url)
                                : null)
                        .WithSeparator()
                        .WithContainer(new TextDisplayBuilder(
                            $"ℹ️ {Strings.MusicProcessingTracks(ctx.Guild.Id, tracks.Count, playlist.Items.Total)}"));

                    await ModifyOriginalResponseAsync(x =>
                    {
                        x.Embed = null;
                        x.Components = loadingComponents.Build();
                        x.Flags = MessageFlags.ComponentsV2;
                    });
                }

                foreach (var item in playlist.Items.Items)
                {
                    if (item.Track is not FullTrack track) continue;

                    var searchQuery = $"{track.Name} {string.Join(" ", track.Artists.Select(a => a.Name))}";
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

                        // Update components to show now playing
                        if (playlist.Items.Total > 10)
                        {
                            var updatedComponents = new ComponentBuilderV2()
                                .WithContainer([
                                    new TextDisplayBuilder($"# {playlist.Name}")
                                ], Mewdeko.OkColor)
                                .WithSeparator()
                                .WithSection([
                                        new TextDisplayBuilder(Strings.LoadingPlaylistWithTrack(ctx.Guild.Id,
                                            playlist.Items.Total,
                                            playlist.Owner.DisplayName, ytTrack.Title))
                                    ],
                                    playlist.Images.FirstOrDefault()?.Url != null
                                        ? new ThumbnailBuilder(playlist.Images.FirstOrDefault().Url)
                                        : null)
                                .WithSeparator()
                                .WithContainer(new TextDisplayBuilder(
                                    $"ℹ️ {Strings.MusicProcessingTracks(ctx.Guild.Id, tracks.Count, playlist.Items.Total)}"));

                            await ModifyOriginalResponseAsync(x =>
                            {
                                x.Embed = null;
                                x.Components = updatedComponents.Build();
                                x.Flags = MessageFlags.ComponentsV2;
                            });
                        }
                    }

                    // Update loading message every 5 tracks
                    if (playlist.Items.Total > 10 && tracks.Count % 5 == 0)
                    {
                        var updatedComponents = new ComponentBuilderV2()
                            .WithContainer([
                                new TextDisplayBuilder($"# {playlist.Name}")
                            ], Mewdeko.OkColor)
                            .WithSeparator()
                            .WithSection([
                                    new TextDisplayBuilder(Strings.LoadingPlaylist(ctx.Guild.Id, playlist.Items.Total,
                                        playlist.Owner.DisplayName))
                                ],
                                playlist.Images.FirstOrDefault()?.Url != null
                                    ? new ThumbnailBuilder(playlist.Images.FirstOrDefault().Url)
                                    : null)
                            .WithSeparator()
                            .WithContainer(new TextDisplayBuilder(
                                $"ℹ️ {Strings.MusicProcessingTracks(ctx.Guild.Id, tracks.Count, playlist.Items.Total)}"));

                        await ModifyOriginalResponseAsync(x =>
                        {
                            x.Embed = null;
                            x.Components = updatedComponents.Build();
                            x.Flags = MessageFlags.ComponentsV2;
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing Spotify URL: {Url}", url);
            throw; // Rethrow to be handled by caller
        }

        return tracks;
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
                     Id = Context.User.Id, Username = Context.User.Username, AvatarUrl = Context.User.GetAvatarUrl()
                 })))
        {
            queue.Add(mewdekoTrack);
            addedTracks.Add(mewdekoTrack);
        }

        await cache.SetMusicQueue(Context.Guild.Id, queue);

        // Start playback if nothing is currently playing
        if (player.CurrentItem is null && queue.Any())
        {
            await player.PlayAsync(queue[0].Track);
            await cache.SetCurrentTrack(Context.Guild.Id, queue[0]);
        }

        // Create response components
        var containerComponents = new List<IMessageComponentBuilder>();

        containerComponents.Add(new TextDisplayBuilder()
            .WithContent($"# {Strings.AddedToQueue(ctx.Guild.Id)}"));

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
                    new MediaGalleryItemProperties(new UnfurledMediaItemProperties
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

        await ModifyOriginalResponseAsync(x =>
        {
            x.Embed = null;
            x.Components = componentsV2.Build();
            x.Flags = MessageFlags.ComponentsV2;
            x.AllowedMentions = AllowedMentions.None;
        });
    }

    /// <summary>
    ///     Handles music control button interactions
    /// </summary>
    /// <param name="guildId">Guild ID from the button</param>
    /// <param name="action">Button action type</param>
    [ComponentInteraction("music:*:*", true)]
    public async Task HandleMusicControls(string action, string guildId)
    {
        // Verify the interaction is for the correct guild
        if (ctx.Guild.Id.ToString() != guildId)
        {
            await RespondAsync(Strings.MusicControlsWrongServer(ctx.Guild.Id));
            return;
        }

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await RespondAsync(embed: new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result)
                .Build());
            return;
        }

        // Defer the response to avoid interaction timeout
        await DeferAsync();

        try
        {
            switch (action)
            {
                case "playpause":
                    if (player.State == PlayerState.Paused)
                    {
                        await player.ResumeAsync();
                        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.MusicResume(ctx.Guild.Id));
                    }
                    else
                    {
                        await player.PauseAsync();
                        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.MusicPause(ctx.Guild.Id));
                    }

                    break;

                case "next":
                    if (player.CurrentItem != null)
                    {
                        await player.SeekAsync(player.CurrentItem.Track.Duration);
                        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(
                            Strings.MusicSkippedTrack(ctx.Guild.Id));
                    }
                    else
                    {
                        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(
                            Strings.MusicNoCurrentTrack(ctx.Guild.Id));
                    }

                    break;

                case "prev":
                    var queue = await cache.GetMusicQueue(ctx.Guild.Id);
                    var currentTrack = await cache.GetCurrentTrack(ctx.Guild.Id);
                    if (currentTrack is { Index: > 1 })
                    {
                        var prevTrack = queue.FirstOrDefault(x => x.Index == currentTrack.Index - 1);
                        if (prevTrack != null)
                        {
                            await player.PlayAsync(prevTrack.Track);
                            await cache.SetCurrentTrack(ctx.Guild.Id, prevTrack);
                            await ctx.Interaction.SendEphemeralFollowupConfirmAsync(
                                Strings.MusicPlayingPrevious(ctx.Guild.Id));
                        }
                    }
                    else
                    {
                        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(
                            Strings.MusicNoPreviousTrack(ctx.Guild.Id));
                    }

                    break;

                case "stop":
                    await player.StopAsync();
                    await cache.SetCurrentTrack(ctx.Guild.Id, null);
                    await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.MusicPlayerStopped(ctx.Guild.Id));
                    break;

                case "loop":
                    var currentRepeatType = await player.GetRepeatType();
                    var newRepeatType = currentRepeatType switch
                    {
                        PlayerRepeatType.None => PlayerRepeatType.Queue,
                        PlayerRepeatType.Queue => PlayerRepeatType.Track,
                        PlayerRepeatType.Track => PlayerRepeatType.None,
                        _ => PlayerRepeatType.None
                    };
                    await player.SetRepeatTypeAsync(newRepeatType);
                    await ctx.Interaction.SendEphemeralFollowupConfirmAsync(
                        Strings.MusicRepeatType(ctx.Guild.Id, newRepeatType));
                    break;

                case "volume_up":
                    var currentVolumeUp = await player.GetVolume();
                    if (currentVolumeUp < 100)
                    {
                        var newVolumeUp = Math.Min(currentVolumeUp + 10, 100);
                        await player.SetVolumeAsync(newVolumeUp / 100f);
                        await player.SetGuildVolumeAsync(newVolumeUp);
                        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(
                            Strings.MusicVolumeSet(ctx.Guild.Id, newVolumeUp));
                    }
                    else
                    {
                        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(
                            Strings.MusicVolumeMaximum(ctx.Guild.Id));
                    }

                    break;

                case "volume_down":
                    var currentVolumeDown = await player.GetVolume();
                    if (currentVolumeDown > 0)
                    {
                        var newVolumeDown = Math.Max(currentVolumeDown - 10, 0);
                        await player.SetVolumeAsync(newVolumeDown / 100f);
                        await player.SetGuildVolumeAsync(newVolumeDown);
                        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(
                            Strings.MusicVolumeSet(ctx.Guild.Id, newVolumeDown));
                    }
                    else
                    {
                        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(
                            Strings.MusicVolumeMinimum(ctx.Guild.Id));
                    }

                    break;

                case "queue":
                    await Queue();
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error in music control button handler {ex}");
            await FollowupAsync(Strings.MusicControlError(ctx.Guild.Id));
        }
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
                Channel = Context.Channel as ITextChannel
            };

            var result = await service.Players
                .RetrieveAsync<MewdekoPlayer, MewdekoPlayerOptions>(Context, CreatePlayerAsync, options,
                    retrieveOptions)
                .ConfigureAwait(false);

            if (result.IsSuccess && result.Player is not null)
            {
                await result.Player.SetVolumeAsync(await result.Player.GetVolume() / 100f).ConfigureAwait(false);
                return (result.Player, null);
            }


            var errorMessage = result.Status switch
            {
                PlayerRetrieveStatus.UserNotInVoiceChannel => Strings.MusicNotInChannel(ctx.Guild.Id),
                PlayerRetrieveStatus.BotNotConnected => Strings.MusicBotNotConnect(ctx.Guild.Id,
                    await guildSettingsService.GetPrefix(Context.Guild)),
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